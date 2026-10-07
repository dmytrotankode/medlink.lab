// =============================================================================
// MedLink LIS Analyzer Connector — сесія ASTM E1381: повний автомат
// ENQ/ACK/NAK/EOT, перевірка контрольних сум кадрів (NAK при помилці), контроль
// нумерації кадрів, обробка контенції (прилад має пріоритет), вихідна сесія для
// завантаження замовлень (ENQ → ACK → кадри з підтвердженням → EOT), повтори ≤ 6,
// таймаути 15 с. Також приймає «вільний» текст без кадрування (ASTM2-варіанти) за таймером.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text;
using System.Threading.Channels;
using MedLink.LabConnector.Transport;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;
using MedLink.LIS.Core.Protocols.Orders;

namespace MedLink.LabConnector.Sessions;

public sealed class AstmSession : SessionBase
{
    private enum State { Idle, Receiving, Sending }

    private volatile State _state = State.Idle;
    private readonly List<byte> _frameBuf = new();
    private bool _inFrame;
    private bool _terminatorSeen;
    private readonly List<AstmFrame> _frames = new();
    private readonly StringBuilder _rawStream = new();
    private readonly List<byte> _loose = new();
    private int _expectedFrame = 1;
    private int _lastFrameNo = -1;
    private int _nakCount;
    private DateTime _lastByteAt = DateTime.UtcNow;
    private TaskCompletionSource<byte>? _controlWaiter;

    public AstmSession(AnalyzerConfigDto cfg, ITransport transport, IOrderSource orders, IInboundSink sink, SessionOptions options, ILogger logger)
        : base(cfg, transport, orders, sink, options, logger) { }

    private bool RequireChecksum => Config.Framing?.Checksum ?? true;
    private int MaxFrameLen => Config.Framing?.MaxFrameLen > 0 ? Config.Framing.MaxFrameLen : AstmFrameCodec.DefaultMaxFrameLen;
    private int SleepMs => Math.Clamp(Config.Framing?.SleepMs ?? 100, 0, 5000);
    private TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(3, Options.AstmTimeoutSec));

    // ------------------------------------------------------------------ приймання

    protected override async Task ProcessLoopAsync(ChannelReader<byte[]> reader, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var idle = _state == State.Receiving ? TimeSpan.FromSeconds(30) : TimeSpan.FromMilliseconds(Math.Max(200, Options.PacketIdleMs));
            var chunk = await ReadWithTimeoutAsync(reader, idle, ct);
            if (chunk == null)
            {
                await OnIdleTimeoutAsync(ct);
                continue;
            }
            _lastByteAt = DateTime.UtcNow;
            foreach (var b in chunk) await OnByteAsync(b, ct);
        }
    }

    private Task OnIdleTimeoutAsync(CancellationToken ct)
    {
        if (_state == State.Receiving && (DateTime.UtcNow - _lastByteAt) > TimeSpan.FromSeconds(30))
        {
            Logger.LogWarning("[{Code}] таймаут приймання ASTM: сесію скинуто ({Frames} кадрів відкинуто)", Config.Code, _frames.Count);
            Sink.OnError(Config, "Таймаут приймання ASTM-сесії");
            if (_frames.Count > 0)
            {
                // Віддаємо те, що встигли прийняти — краще часткові результати, ніж нічого
                var partial = AstmFrameCodec.AssembleMessage(_frames);
                _ = Task.Run(() => HandleInboundAsync(partial, ct), ct);
            }
            ResetReceive();
            _state = State.Idle;
        }
        if (_loose.Count > 0 && _state != State.Receiving)
        {
            // Нефреймований текст (ASTM2 / прилад без STX): обробляємо за таймером тиші
            var text = DecodeText(_loose.ToArray(), Encoding);
            _loose.Clear();
            if (text.Trim('\0', ' ', '\r', '\n').Length > 0)
            {
                Logger.LogInformation("[{Code}] отримано нефреймований текст ({Len} символів) — обробка за таймером", Config.Code, text.Length);
                _ = Task.Run(() => HandleInboundAsync(text, ct), ct);
            }
        }
        return Task.CompletedTask;
    }

    private async Task OnByteAsync(byte b, CancellationToken ct)
    {
        if (_inFrame)
        {
            if (b == AstmControl.STX && _frameBuf.Count > 1)
            {
                // Новий STX усередині кадру — попередній кадр зіпсовано
                Logger.LogWarning("[{Code}] STX усередині кадру — кадр відкинуто", Config.Code);
                _frameBuf.Clear();
            }
            _frameBuf.Add(b);
            if (b == AstmControl.ETX || b == AstmControl.ETB) _terminatorSeen = true;
            else if (_terminatorSeen && b == AstmControl.LF) await OnFrameCompleteAsync(ct);
            else if (_terminatorSeen && _frameBuf.Count > 0 && FrameLooksCompleteWithoutLf(b)) { /* чекаємо LF */ }
            if (_frameBuf.Count > 64 * 1024) { Logger.LogWarning("[{Code}] кадр > 64 КБ — скинуто", Config.Code); _frameBuf.Clear(); _inFrame = false; _terminatorSeen = false; }
            return;
        }

        switch (b)
        {
            case AstmControl.ENQ:
                await OnEnqAsync(ct);
                break;
            case AstmControl.STX:
                if (_state == State.Sending) { Logger.LogDebug("[{Code}] STX під час відправки — ігноруємо", Config.Code); return; }
                if (_state == State.Idle)
                {
                    // Прилад почав передачу без ENQ (деякі прилади/ASTM2) — приймаємо
                    _state = State.Receiving;
                    ResetReceive();
                }
                _inFrame = true;
                _terminatorSeen = false;
                _frameBuf.Clear();
                _frameBuf.Add(b);
                break;
            case AstmControl.EOT:
                await OnEotAsync(ct);
                break;
            case AstmControl.ACK:
            case AstmControl.NAK:
                if (!CompleteControl(b) && _state != State.Sending)
                    Logger.LogDebug("[{Code}] отримано {Ctl} поза вихідною сесією — ігноруємо", Config.Code, b == AstmControl.ACK ? "ACK" : "NAK");
                break;
            case 0x00:
                break; // keep-alive NUL
            default:
                if (_state == State.Receiving && !_inFrame)
                {
                    // Дані між кадрами (сміття / CR LF) — ігноруємо
                    if (b != AstmControl.CR && b != AstmControl.LF) _loose.Add(b);
                }
                else if (_state != State.Sending)
                {
                    _loose.Add(b);
                    if (_loose.Count > 1024 * 1024) _loose.RemoveRange(0, _loose.Count - 1024 * 1024);
                }
                break;
        }
    }

    private static bool FrameLooksCompleteWithoutLf(byte b) => false;

    private async Task OnEnqAsync(CancellationToken ct)
    {
        if (_state == State.Sending)
        {
            // Контенція: прилад має пріоритет — припиняємо свою передачу, приймаємо
            Logger.LogInformation("[{Code}] контенція ENQ↔ENQ: поступаємося приладу", Config.Code);
            CompleteControl(AstmControl.ENQ);
        }
        _state = State.Receiving;
        ResetReceive();
        await SendControlAsync(AstmControl.ACK, ct);
        Logger.LogDebug("[{Code}] ENQ → ACK, сесія приймання розпочата", Config.Code);
    }

    private void ResetReceive()
    {
        _frames.Clear();
        _frameBuf.Clear();
        _rawStream.Clear();
        _inFrame = false;
        _terminatorSeen = false;
        _expectedFrame = 1;
        _lastFrameNo = -1;
        _nakCount = 0;
        _loose.Clear();
    }

    private async Task OnFrameCompleteAsync(CancellationToken ct)
    {
        var bytes = _frameBuf.ToArray();
        _frameBuf.Clear();
        _inFrame = false;
        _terminatorSeen = false;
        _rawStream.Append(Encoding.GetString(bytes));
        var frame = AstmFrameCodec.ParseFrame(bytes, RequireChecksum, Encoding);
        if (!frame.ChecksumOk)
        {
            _nakCount++;
            Logger.LogWarning("[{Code}] кадр {No} відхилено: {Error} (NAK #{Nak})", Config.Code, frame.Number, frame.Error, _nakCount);
            Sink.OnError(Config, $"Кадр {frame.Number}: {frame.Error}");
            await SendControlAsync(AstmControl.NAK, ct);
            return;
        }
        if (frame.Number >= 0 && frame.Number == _lastFrameNo && _frames.Count > 0 && _frames[^1].Content == frame.Content)
        {
            Logger.LogDebug("[{Code}] повторний кадр {No} — підтверджено без додавання", Config.Code, frame.Number);
            await SendControlAsync(AstmControl.ACK, ct);
            return;
        }
        if (frame.Number >= 0 && frame.Number != _expectedFrame)
            Logger.LogDebug("[{Code}] номер кадру {No} ≠ очікуваного {Expected} — приймаємо (толерантно)", Config.Code, frame.Number, _expectedFrame);
        _frames.Add(frame);
        _lastFrameNo = frame.Number;
        _expectedFrame = AstmFrameCodec.NextFrameNumber(frame.Number >= 0 ? frame.Number : _expectedFrame);
        _nakCount = 0;
        if (SleepMs > 0 && SleepMs < 50) await Task.Delay(SleepMs, ct);
        await SendControlAsync(AstmControl.ACK, ct);
    }

    private Task OnEotAsync(CancellationToken ct)
    {
        if (_state == State.Sending) { CompleteControl(AstmControl.EOT); return Task.CompletedTask; }
        if (_state != State.Receiving) return Task.CompletedTask;
        var message = AstmFrameCodec.AssembleMessage(_frames);
        int frames = _frames.Count;
        ResetReceive();
        _state = State.Idle;
        Logger.LogInformation("[{Code}] EOT: прийнято повідомлення ({Frames} кадрів, {Len} символів)", Config.Code, frames, message.Length);
        if (message.Length > 0)
        {
            // Обробка поза циклом, щоб приймання ACK/NAK вихідної сесії не блокувалося
            _ = Task.Run(() => HandleInboundAsync(message, ct), ct);
        }
        return Task.CompletedTask;
    }

    private bool CompleteControl(byte b)
    {
        var w = Interlocked.Exchange(ref _controlWaiter, null);
        if (w == null) return false;
        w.TrySetResult(b);
        return true;
    }

    private async Task SendControlAsync(byte ctl, CancellationToken ct)
    {
        try { await SendBytesAsync(new[] { ctl }, ct); }
        catch (Exception ex) { Logger.LogWarning("[{Code}] не вдалося відправити {Ctl}: {Error}", Config.Code, ProtocolText.Escape(((char)ctl).ToString()), ex.Message); }
    }

    // ------------------------------------------------------------------ відправлення

    protected override async Task SendOutboundAsync(IReadOnlyList<string> records, OrderOutputKind kind, CancellationToken ct)
    {
        if (records.Count == 0) return;
        switch (kind)
        {
            case OrderOutputKind.AstmRecords:
                await SendAstmMessageAsync(records, ct);
                break;
            case OrderOutputKind.TextLines:
                // Деякі ASTM-подібні прилади (HumaStar) приймають текст без кадрування — відправляємо як ASTM-сесію з одним записом на рядок
                await SendAstmMessageAsync(records, ct);
                break;
            case OrderOutputKind.Hl7Messages:
                foreach (var m in records) await SendRawAsync(MllpCodec.WrapString(m), ct, "HL7");
                break;
            default:
                foreach (var f in records) await SendRawAsync(f, ct, "кадр");
                break;
        }
    }

    /// <summary>Повна вихідна ASTM-сесія. Повертає true при успіху.</summary>
    public async Task<bool> SendAstmMessageAsync(IReadOnlyList<string> records, CancellationToken ct)
    {
        var frames = AstmFrameCodec.BuildFrames(records, MaxFrameLen, RequireChecksum);
        using var _ = await AcquireSendAsync(ct);
        var messageText = AstmParser.Join(records);
        try
        {
            if (!await EstablishAsync(ct))
            {
                Sink.OnError(Config, "Прилад не підтвердив ENQ — замовлення не відправлено");
                Sink.OnOutbound(Config, messageText, "НЕ ВІДПРАВЛЕНО: немає ACK на ENQ");
                return false;
            }
            foreach (var frame in frames)
            {
                bool acked = false;
                for (int attempt = 1; attempt <= Options.AstmMaxRetries && !acked; attempt++)
                {
                    var waiter = NewWaiter();
                    await SendRawAsync(frame, ct, log: false);
                    var r = await WaitControlAsync(waiter, ct);
                    switch (r)
                    {
                        case AstmControl.ACK: acked = true; break;
                        case AstmControl.NAK: Logger.LogWarning("[{Code}] NAK на кадр — повтор {Attempt}/{Max}", Config.Code, attempt, Options.AstmMaxRetries); break;
                        case AstmControl.EOT: Logger.LogWarning("[{Code}] прилад перервав сесію (EOT)", Config.Code); attempt = int.MaxValue - 1; break;
                        case AstmControl.ENQ: Logger.LogWarning("[{Code}] контенція під час передачі — припиняємо", Config.Code); attempt = int.MaxValue - 1; break;
                        default: Logger.LogWarning("[{Code}] немає відповіді на кадр — повтор {Attempt}/{Max}", Config.Code, attempt, Options.AstmMaxRetries); break;
                    }
                }
                if (!acked)
                {
                    await SendControlAsync(AstmControl.EOT, ct);
                    Sink.OnError(Config, "Кадр не підтверджено приладом — передачу перервано");
                    Sink.OnOutbound(Config, messageText, "НЕ ВІДПРАВЛЕНО: кадр не підтверджено");
                    return false;
                }
                if (SleepMs > 0) await Task.Delay(SleepMs, ct);
            }
            await SendControlAsync(AstmControl.EOT, ct);
            Sink.OnOutbound(Config, messageText, $"ASTM: {frames.Count} кадр(ів) підтверджено");
            Logger.LogInformation("[{Code}] замовлення відправлено: {Frames} кадр(ів)", Config.Code, frames.Count);
            return true;
        }
        finally
        {
            if (_state == State.Sending) _state = State.Idle;
            Interlocked.Exchange(ref _controlWaiter, null);
        }
    }

    /// <summary>Встановлення вихідної сесії: чекаємо Idle, ENQ, чекаємо ACK; контенція → поступаємося і повторюємо.</summary>
    private async Task<bool> EstablishAsync(CancellationToken ct)
    {
        for (int attempt = 1; attempt <= Options.AstmMaxRetries; attempt++)
        {
            if (!await WaitIdleAsync(ct)) { Logger.LogWarning("[{Code}] прилад не звільнив лінію — спроба {Attempt}", Config.Code, attempt); continue; }
            _state = State.Sending;
            var waiter = NewWaiter();
            await SendBytesAsync(new[] { AstmControl.ENQ }, ct);
            var r = await WaitControlAsync(waiter, ct);
            if (r == AstmControl.ACK) return true;
            if (r == AstmControl.ENQ)
            {
                // Контенція: цикл приймання вже перейшов у Receiving — чекаємо завершення сесії приладу
                await Task.Delay(1000, ct);
                continue;
            }
            _state = State.Idle;
            Logger.LogWarning("[{Code}] ENQ без ACK ({Reply}) — спроба {Attempt}/{Max}", Config.Code, r == 0 ? "таймаут" : ProtocolText.Escape(((char)r).ToString()), attempt, Options.AstmMaxRetries);
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(10, attempt * 2)), ct);
        }
        return false;
    }

    private async Task<bool> WaitIdleAsync(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + Timeout + TimeSpan.FromSeconds(15);
        while (_state != State.Idle)
        {
            if (DateTime.UtcNow > deadline) return false;
            await Task.Delay(100, ct);
        }
        return true;
    }

    private TaskCompletionSource<byte> NewWaiter()
    {
        var tcs = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _controlWaiter, tcs);
        return tcs;
    }

    /// <summary>Чекає керуючий символ; 0 — таймаут.</summary>
    private async Task<byte> WaitControlAsync(TaskCompletionSource<byte> waiter, CancellationToken ct)
    {
        var delay = Task.Delay(Timeout, ct);
        var done = await Task.WhenAny(waiter.Task, delay);
        if (done == waiter.Task) return waiter.Task.Result;
        Interlocked.CompareExchange(ref _controlWaiter, null, waiter);
        return 0;
    }

    public override async Task<string> LinkTestAsync(CancellationToken ct)
    {
        if (!Transport.IsConnected) return "Немає підключення до приладу";
        using var _ = await AcquireSendAsync(ct);
        try
        {
            if (!await WaitIdleAsync(ct)) return "Лінія зайнята приладом";
            _state = State.Sending;
            var waiter = NewWaiter();
            await SendBytesAsync(new[] { AstmControl.ENQ }, ct);
            var r = await WaitControlAsync(waiter, ct);
            await SendControlAsync(AstmControl.EOT, ct);
            return r switch
            {
                AstmControl.ACK => "OK: прилад відповів ACK на ENQ",
                AstmControl.NAK => "Прилад відповів NAK (зайнятий)",
                AstmControl.ENQ => "Контенція: прилад одночасно почав передачу",
                0 => "Таймаут: прилад не відповів на ENQ",
                _ => $"Неочікувана відповідь: {ProtocolText.Escape(((char)r).ToString())}",
            };
        }
        finally { if (_state == State.Sending) _state = State.Idle; }
    }
}
