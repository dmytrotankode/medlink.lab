// =============================================================================
// MedLink LIS Analyzer Connector — базова сесія обміну з приладом: приймає байти
// від транспорту, виділяє повідомлення (протокольно-специфічно), розбирає їх
// парсером профілю, передає результати у конвеєр і відповідає замовленнями на запити.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text;
using System.Threading.Channels;
using MedLink.LabConnector.Transport;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;
using MedLink.LIS.Core.Protocols.Orders;
using MedLink.LIS.Core.Protocols.Parsers;

namespace MedLink.LabConnector.Sessions;

/// <summary>Джерело замовлень (сервер ЛІС або заглушка у тестах).</summary>
public interface IOrderSource
{
    Task<AnalyzerOrderDto?> GetByBarcodeAsync(string barcode, AnalyzerConfigDto cfg, CancellationToken ct);
    Task<IReadOnlyList<AnalyzerOrderDto>> GetPendingAsync(AnalyzerConfigDto cfg, CancellationToken ct);
}

/// <summary>Приймач вхідних повідомлень/подій сесії (конвеєр результатів, журнал, статус).</summary>
public interface IInboundSink
{
    /// <summary>Повне вхідне повідомлення (вже розібране). Має обробити результати та записати журнал.</summary>
    Task OnInboundAsync(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage parsed, CancellationToken ct);
    /// <summary>Вихідні дані (для журналу обміну).</summary>
    void OnOutbound(AnalyzerConfigDto cfg, string raw, string? note = null);
    void OnError(AnalyzerConfigDto cfg, string error);
    void OnConnection(AnalyzerConfigDto cfg, bool connected);
    /// <summary>Низькорівневий трафік (байти) — лише для файлів raw-*.log.</summary>
    void OnRawTraffic(AnalyzerConfigDto cfg, string direction, string raw);
}

public sealed class SessionOptions
{
    public int AstmTimeoutSec { get; init; } = 15;
    public int AstmMaxRetries { get; init; } = 6;
    public int PacketIdleMs { get; init; } = 700;
    public bool SendNoOrderReply { get; init; }
}

public interface IAnalyzerSession : IAsyncDisposable
{
    AnalyzerConfigDto Config { get; }
    ITransport Transport { get; }
    bool IsConnected { get; }
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
    /// <summary>Відправити пакет замовлень приладу (batch download / ручна відправка).</summary>
    Task SendOrdersAsync(IReadOnlyList<AnalyzerOrderDto> orders, CancellationToken ct);
    /// <summary>Тест лінії зв'язку (ASTM: ENQ→ACK→EOT; HL7/текст: лише перевірка підключення). Повертає опис результату.</summary>
    Task<string> LinkTestAsync(CancellationToken ct);
    /// <summary>Подати «сирі» байти так, ніби вони прийшли від приладу (симуляція / тести).</summary>
    void Inject(byte[] data);
}

public abstract class SessionBase : IAnalyzerSession
{
    protected readonly ILogger Logger;
    protected readonly IOrderSource Orders;
    protected readonly IInboundSink Sink;
    protected readonly SessionOptions Options;
    protected readonly IAnalyzerMessageParser Parser;
    protected readonly IAnalyzerOrderBuilder OrderBuilder;
    protected readonly Channel<byte[]> Rx = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    protected SessionBase(AnalyzerConfigDto cfg, ITransport transport, IOrderSource orders, IInboundSink sink, SessionOptions options, ILogger logger)
    {
        Config = cfg;
        Transport = transport;
        Orders = orders;
        Sink = sink;
        Options = options;
        Logger = logger;
        Parser = ParserFactory.ForConfig(cfg);
        OrderBuilder = OrderBuilderFactory.ForConfig(cfg);
    }

    public AnalyzerConfigDto Config { get; }
    public ITransport Transport { get; }
    public bool IsConnected => Transport.IsConnected;
    public string ParserKind => Parser.Kind;
    public string OrderTemplate => OrderBuilder.Template;
    protected CancellationToken StopToken => _cts?.Token ?? CancellationToken.None;

    /// <summary>Кодування тексту каналу (Latin-1 для ASTM/текстових — байт-прозоро; UTF-8 для HL7).</summary>
    protected virtual Encoding Encoding => Encoding.Latin1;

    public virtual async Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Transport.DataReceived += OnData;
        Transport.ConnectionChanged += OnConnection;
        _loop = Task.Run(() => RunLoopSafe(_cts.Token));
        await Transport.StartAsync(_cts.Token);
        Logger.LogInformation("[{Code}] сесію запущено: {Transport}, протокол {Protocol}, парсер {Parser}, шаблон {Template}", Config.Code, Transport.Description, Config.Protocol, Parser.Kind, OrderBuilder.Template);
    }

    private void OnData(byte[] data)
    {
        Sink.OnRawTraffic(Config, "IN", Encoding.GetString(data));
        Rx.Writer.TryWrite(data);
    }

    private void OnConnection(bool connected)
    {
        Sink.OnConnection(Config, connected);
        OnConnectionChanged(connected);
    }

    protected virtual void OnConnectionChanged(bool connected) { }

    private async Task RunLoopSafe(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessLoopAsync(Rx.Reader, ct);
                if (!ct.IsCancellationRequested) await Task.Delay(100, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Ніколи не падаємо: логуємо і продовжуємо
                Logger.LogError(ex, "[{Code}] помилка циклу обробки: {Error}", Config.Code, ex.Message);
                Sink.OnError(Config, ex.Message);
                try { await Task.Delay(500, ct); } catch { break; }
            }
        }
    }

    /// <summary>Протокольний цикл: читає байти з каналу та виділяє повідомлення.</summary>
    protected abstract Task ProcessLoopAsync(ChannelReader<byte[]> reader, CancellationToken ct);

    /// <summary>Читання наступного блоку з таймаутом; null — таймаут.</summary>
    protected static async Task<byte[]?> ReadWithTimeoutAsync(ChannelReader<byte[]> reader, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try { return await reader.ReadAsync(cts.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
    }

    /// <summary>Повна обробка вхідного повідомлення: парсинг → конвеєр → негайна відповідь → замовлення на запит.</summary>
    protected async Task HandleInboundAsync(string raw, CancellationToken ct)
    {
        AnalyzerInboundMessage parsed;
        try { parsed = Parser.Parse(Config, raw); }
        catch (Exception ex) { parsed = AnalyzerInboundMessage.Other(raw, "Парсер кинув виключення: " + ex.Message); }
        try { await Sink.OnInboundAsync(Config, raw, parsed, ct); }
        catch (Exception ex) { Logger.LogError(ex, "[{Code}] помилка конвеєра результатів", Config.Code); }

        if (!string.IsNullOrEmpty(parsed.ImmediateReply))
        {
            try { await SendRawAsync(parsed.ImmediateReply, ct, "протокольна відповідь"); }
            catch (Exception ex) { Logger.LogWarning("[{Code}] не вдалося відправити протокольну відповідь: {Error}", Config.Code, ex.Message); }
        }
        else if (parsed.Kind == InboundMessageKind.Results || parsed.Kind == InboundMessageKind.Other)
        {
            await AfterInboundAsync(parsed, ct);
        }

        if (parsed.Kind == InboundMessageKind.Query)
        {
            if (Config.AutoQueryOrders) await HandleQueryAsync(parsed, ct);
            else Logger.LogInformation("[{Code}] запит замовлення проігноровано (autoQueryOrders=false): {Barcodes}", Config.Code, string.Join(",", parsed.Barcodes));
        }
    }

    /// <summary>Гачок після результатів/інших повідомлень (TextSession: ACK після запису).</summary>
    protected virtual Task AfterInboundAsync(AnalyzerInboundMessage parsed, CancellationToken ct) => Task.CompletedTask;

    protected async Task HandleQueryAsync(AnalyzerInboundMessage parsed, CancellationToken ct)
    {
        var ctx = new OrderBuildContext { RawQueryToken = parsed.RawQueryToken, SamplePosition = parsed.SamplePosition, SendNoOrderReply = Options.SendNoOrderReply, SequenceNumber = Interlocked.Increment(ref _sampleSeq) };
        var found = new List<AnalyzerOrderDto>();
        var missing = new List<string>();
        foreach (var barcode in parsed.Barcodes)
        {
            try
            {
                var order = await Orders.GetByBarcodeAsync(barcode, Config, ct);
                if (order != null) found.Add(order); else missing.Add(barcode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[{Code}] запит замовлення {Barcode}: {Error}", Config.Code, barcode, ex.Message);
                Sink.OnError(Config, $"Запит замовлення {barcode}: {ex.Message}");
                missing.Add(barcode);
            }
        }
        if (found.Count > 0)
        {
            Logger.LogInformation("[{Code}] знайдено замовлень: {Count} ({Barcodes}); відправляємо приладу", Config.Code, found.Count, string.Join(",", found.Select(f => f.Barcode)));
            var messages = OrderBuilder.BuildBatch(found, Config, DateTime.Now, ctx);
            foreach (var m in messages) await SendOutboundAsync(m, OrderBuilder.Output, ct);
        }
        foreach (var barcode in missing)
        {
            Logger.LogInformation("[{Code}] замовлення для штрихкоду {Barcode} не знайдено", Config.Code, barcode);
            var records = OrderBuilder.BuildNoOrderRecords(barcode, Config, DateTime.Now, ctx);
            if (records.Count > 0) await SendOutboundAsync(records, OrderBuilder.Output, ct);
        }
    }

    private int _sampleSeq;

    public async Task SendOrdersAsync(IReadOnlyList<AnalyzerOrderDto> orders, CancellationToken ct)
    {
        if (orders.Count == 0) return;
        var ctx = new OrderBuildContext { SequenceNumber = Interlocked.Increment(ref _sampleSeq), SendNoOrderReply = Options.SendNoOrderReply };
        var messages = OrderBuilder.BuildBatch(orders, Config, DateTime.Now, ctx);
        foreach (var m in messages) await SendOutboundAsync(m, OrderBuilder.Output, ct);
    }

    /// <summary>Відправлення побудованих записів у протокольно-залежний спосіб.</summary>
    protected abstract Task SendOutboundAsync(IReadOnlyList<string> records, OrderOutputKind kind, CancellationToken ct);

    public abstract Task<string> LinkTestAsync(CancellationToken ct);

    /// <summary>Відправка «сирого» тексту (кодування сесії) з журналюванням.</summary>
    protected async Task SendRawAsync(string text, CancellationToken ct, string? note = null, bool log = true)
    {
        var bytes = Encoding.GetBytes(text);
        await Transport.SendAsync(bytes, ct);
        Sink.OnRawTraffic(Config, "OUT", text);
        if (log) Sink.OnOutbound(Config, text, note);
    }

    protected async Task SendBytesAsync(byte[] bytes, CancellationToken ct)
    {
        await Transport.SendAsync(bytes, ct);
        Sink.OnRawTraffic(Config, "OUT", Encoding.GetString(bytes));
    }

    /// <summary>Серіалізує вихідні дані одним блоком (єдиний відправник за раз).</summary>
    protected async Task<IDisposable> AcquireSendAsync(CancellationToken ct)
    {
        await _sendGate.WaitAsync(ct);
        return new Releaser(_sendGate);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _s; private bool _done;
        public Releaser(SemaphoreSlim s) => _s = s;
        public void Dispose() { if (!_done) { _done = true; _s.Release(); } }
    }

    public void Inject(byte[] data) => OnData(data);

    public virtual async Task StopAsync()
    {
        _cts?.Cancel();
        Transport.DataReceived -= OnData;
        Transport.ConnectionChanged -= OnConnection;
        try { await Transport.StopAsync(); } catch { }
        if (_loop != null) { try { await _loop; } catch { } }
        Logger.LogInformation("[{Code}] сесію зупинено", Config.Code);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await Transport.DisposeAsync();
        _cts?.Dispose();
        _sendGate.Dispose();
    }

    /// <summary>Декодує байти: UTF-8, якщо валідний, інакше Latin-1.</summary>
    public static string DecodeText(byte[] data, Encoding preferred)
    {
        if (preferred.Equals(Encoding.UTF8)) return Encoding.UTF8.GetString(data);
        bool high = data.Any(b => b >= 0x80);
        if (!high) return Encoding.ASCII.GetString(data);
        try { return new UTF8Encoding(false, true).GetString(data); }
        catch { return Encoding.Latin1.GetString(data); }
    }
}
