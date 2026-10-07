using System.Text;
using System.Threading.Channels;
using MedLink.LabConnector.Sessions;
using MedLink.LabConnector.Transport;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedLink.LIS.Tests.Protocols;

/// <summary>Транспорт у пам'яті: тест подає байти «від приладу» та читає те, що сесія відправляє.</summary>
public sealed class InMemoryTransport : ITransport
{
    private readonly Channel<byte[]> _sent = Channel.CreateUnbounded<byte[]>();
    public string Description => "in-memory";
    public bool IsConnected { get; private set; }
    public event Action<byte[]>? DataReceived;
    public event Action<bool>? ConnectionChanged;

    public Task StartAsync(CancellationToken ct) { IsConnected = true; ConnectionChanged?.Invoke(true); return Task.CompletedTask; }
    public Task StopAsync() { IsConnected = false; return Task.CompletedTask; }
    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct) { _sent.Writer.TryWrite(data.ToArray()); return Task.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Feed(string text) => DataReceived?.Invoke(Encoding.UTF8.GetBytes(text));
    public void Feed(byte[] data) => DataReceived?.Invoke(data);

    /// <summary>Читає наступний відправлений блок (таймаут 5 с).</summary>
    public async Task<string> NextSentAsync(int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var bytes = await _sent.Reader.ReadAsync(cts.Token);
        return Encoding.UTF8.GetString(bytes);
    }
}

public sealed class FakeOrderSource : IOrderSource
{
    public Dictionary<string, AnalyzerOrderDto> Orders { get; } = new();
    public List<string> Requested { get; } = new();
    public Task<AnalyzerOrderDto?> GetByBarcodeAsync(string barcode, AnalyzerConfigDto cfg, CancellationToken ct)
    {
        Requested.Add(barcode);
        return Task.FromResult(Orders.TryGetValue(barcode, out var o) ? o : null);
    }
    public Task<IReadOnlyList<AnalyzerOrderDto>> GetPendingAsync(AnalyzerConfigDto cfg, CancellationToken ct) => Task.FromResult<IReadOnlyList<AnalyzerOrderDto>>(Array.Empty<AnalyzerOrderDto>());
}

public sealed class FakeSink : IInboundSink
{
    public Channel<AnalyzerInboundMessage> Inbound { get; } = Channel.CreateUnbounded<AnalyzerInboundMessage>();
    public List<string> Outbound { get; } = new();
    public List<string> Errors { get; } = new();
    public Task OnInboundAsync(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage parsed, CancellationToken ct) { Inbound.Writer.TryWrite(parsed); return Task.CompletedTask; }
    public void OnOutbound(AnalyzerConfigDto cfg, string raw, string? note = null) { lock (Outbound) Outbound.Add(raw); }
    public void OnError(AnalyzerConfigDto cfg, string error) { lock (Errors) Errors.Add(error); }
    public void OnConnection(AnalyzerConfigDto cfg, bool connected) { }
    public void OnRawTraffic(AnalyzerConfigDto cfg, string direction, string raw) { }
    public async Task<AnalyzerInboundMessage> NextAsync(int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        return await Inbound.Reader.ReadAsync(cts.Token);
    }
}

public class AstmSessionE2ETests
{
    private static AnalyzerConfigDto SysmexCfg()
    {
        var cfg = AnalyzerProfileCatalog.Default.CreateDefaultConfig("SYSMEXXN", "a1");
        cfg.Code = "SYSMEX_XN";
        cfg.AutoQueryOrders = true;
        cfg.Framing.SleepMs = 0;
        return cfg;
    }

    private static readonly SessionOptions FastOptions = new() { AstmTimeoutSec = 3, AstmMaxRetries = 2, PacketIdleMs = 200 };

    [Fact]
    public async Task Query_from_sysmex_is_acked_order_is_downloaded_and_results_are_delivered()
    {
        var transport = new InMemoryTransport();
        var orders = new FakeOrderSource();
        orders.Orders["1026004819"] = new AnalyzerOrderDto
        {
            Barcode = "1026004819",
            OrderNumber = "2610-004819",
            Patient = new AnalyzerOrderPatientDto { Id = "108291", LastName = "Коваленко", FirstName = "Олена", BirthDate = new DateTime(1985, 4, 12), Gender = "F" },
            Tests = new() { new AnalyzerOrderTestDto { TestCode = "CBC", AnalyzerCode = "CBC" } },
        };
        var sink = new FakeSink();
        await using var session = new AstmSession(SysmexCfg(), transport, orders, sink, FastOptions, NullLogger.Instance);
        await session.StartAsync(CancellationToken.None);

        // 1. Прилад надсилає запит: ENQ → очікуємо ACK
        var query = SampleFiles.Load("01_astm_query_sysmex.txt");
        transport.Feed("\u0005");
        Assert.Equal("\u0006", await transport.NextSentAsync());

        // 2. Кадри → ACK на кожен
        var frames = SplitFrames(query);
        Assert.Equal(3, frames.Count);
        foreach (var f in frames)
        {
            transport.Feed(f);
            Assert.Equal("\u0006", await transport.NextSentAsync());
        }
        // 3. EOT → сесія розбирає запит
        transport.Feed("\u0004");
        var inbound = await sink.NextAsync();
        Assert.Equal(InboundMessageKind.Query, inbound.Kind);
        Assert.Equal("1026004819", inbound.Barcode);

        // 4. Сесія звертається до джерела замовлень і відкриває вихідну сесію: ENQ
        Assert.Equal("\u0005", await transport.NextSentAsync());
        Assert.Contains("1026004819", orders.Requested);
        transport.Feed("\u0006"); // ACK на ENQ

        // 5. Кадри замовлення, кожен підтверджуємо
        var received = new List<AstmFrame>();
        for (int i = 0; i < 4; i++)
        {
            var frame = await transport.NextSentAsync();
            var parsed = AstmFrameCodec.ParseFrame(Encoding.UTF8.GetBytes(frame), true, Encoding.UTF8);
            Assert.True(parsed.ChecksumOk, parsed.Error);
            Assert.Equal((i + 1) % 8, parsed.Number);
            received.Add(parsed);
            transport.Feed("\u0006");
        }
        Assert.Equal("\u0004", await transport.NextSentAsync());
        var message = AstmFrameCodec.AssembleMessage(received);
        Assert.Contains("O|1|^1026004819||^^^^CBC|R|", message);
        Assert.Contains("P|1||108291||Kovalenko^Olena||19850412|F", message);
        Assert.StartsWith("H|\\^&|||MedLinkLIS^^", message);
        // журнал OUT пишеться після відправки EOT — чекаємо коротко
        for (int i = 0; i < 50 && sink.Outbound.Count == 0; i++) await Task.Delay(20);
        Assert.Single(sink.Outbound);

        // 6. Прилад надсилає результати
        var results = SampleFiles.Load("04_astm_results_sysmex_xn.txt");
        transport.Feed("\u0005");
        Assert.Equal("\u0006", await transport.NextSentAsync());
        foreach (var f in SplitFrames(results))
        {
            transport.Feed(f);
            Assert.Equal("\u0006", await transport.NextSentAsync());
        }
        transport.Feed("\u0004");
        var res = await sink.NextAsync();
        Assert.Equal(InboundMessageKind.Results, res.Kind);
        Assert.Equal(5, res.Results.Count);
        Assert.Equal("WBC", res.Results[0].AnalyzerCode);
        Assert.Equal("1026004819", res.Results[0].Barcode);
        Assert.Empty(sink.Errors);
    }

    [Fact]
    public async Task Bad_checksum_frame_gets_NAK_and_retransmission_is_accepted()
    {
        var transport = new InMemoryTransport();
        var sink = new FakeSink();
        await using var session = new AstmSession(SysmexCfg(), transport, new FakeOrderSource(), sink, FastOptions, NullLogger.Instance);
        await session.StartAsync(CancellationToken.None);

        transport.Feed("\u0005");
        Assert.Equal("\u0006", await transport.NextSentAsync());
        var good = AstmFrameCodec.BuildFrame(1, "H|\\^&|||Sysmex", true);
        var bad = good[..^4] + "00\r\n"; // зіпсована контрольна сума
        transport.Feed(bad);
        Assert.Equal("\u0015", await transport.NextSentAsync()); // NAK
        transport.Feed(good);
        Assert.Equal("\u0006", await transport.NextSentAsync());
        transport.Feed(AstmFrameCodec.BuildFrame(2, "P|1||108291", true));
        Assert.Equal("\u0006", await transport.NextSentAsync());
        transport.Feed(AstmFrameCodec.BuildFrame(3, "O|1|10260048||^^^WBC|R", true));
        Assert.Equal("\u0006", await transport.NextSentAsync());
        transport.Feed(AstmFrameCodec.BuildFrame(4, "R|1|^^^WBC^|7.1|10*9/L||N", true));
        Assert.Equal("\u0006", await transport.NextSentAsync());
        transport.Feed(AstmFrameCodec.BuildFrame(5, "L|1|N", true));
        Assert.Equal("\u0006", await transport.NextSentAsync());
        transport.Feed("\u0004");
        var res = await sink.NextAsync();
        Assert.Equal(InboundMessageKind.Results, res.Kind);
        Assert.Single(res.Results);
        Assert.Equal("10260048", res.Results[0].Barcode);
        Assert.Single(sink.Errors); // одна помилка контрольної суми зафіксована
    }

    [Fact]
    public async Task Unframed_text_is_processed_after_idle_timeout_and_garbage_does_not_crash()
    {
        var transport = new InMemoryTransport();
        var sink = new FakeSink();
        var cfg = AnalyzerProfileCatalog.Default.CreateDefaultConfig("ABL80", "a2");
        await using var session = new AstmSession(cfg, transport, new FakeOrderSource(), sink, FastOptions, NullLogger.Instance);
        await session.StartAsync(CancellationToken.None);
        transport.Feed(new byte[] { 0xFF, 0xFE, 0x00 });
        transport.Feed("H|\\^&|||ABL80\rP|1||||10260048\rO|1\rR|1|^^^pH^|7.41||\rL|1|N\r");
        var res = await sink.NextAsync();
        Assert.Equal(InboundMessageKind.Results, res.Kind);
        Assert.Equal("10260048", res.Barcode);
        Assert.Equal("pH", res.Results[0].AnalyzerCode);
    }

    [Fact]
    public async Task Hl7_mllp_session_acks_oru_and_answers_mindray_query_with_qck_and_dsr()
    {
        var transport = new InMemoryTransport();
        var sink = new FakeSink();
        var orders = new FakeOrderSource();
        orders.Orders["10260048"] = new AnalyzerOrderDto { Barcode = "10260048", Tests = new() { new AnalyzerOrderTestDto { TestCode = "GLU", AnalyzerCode = "GLU" } }, Patient = new AnalyzerOrderPatientDto { LastName = "Коваленко", FirstName = "Олена", Gender = "F" } };
        var cfg = AnalyzerProfileCatalog.Default.CreateDefaultConfig("MINDRAY240", "m1");
        await using var session = new Hl7MllpSession(cfg, transport, orders, sink, FastOptions, NullLogger.Instance);
        await session.StartAsync(CancellationToken.None);

        transport.Feed(MllpCodec.Wrap(SampleFiles.Load("05_hl7_oru_r01_mindray.hl7")));
        var ack = Hl7Parser.Parse(await transport.NextSentAsync());
        Assert.Equal("AA", ack.First("MSA")!.Field(1));
        Assert.Equal("MSG009841", ack.First("MSA")!.Field(2));
        var res = await sink.NextAsync();
        Assert.Equal(InboundMessageKind.Results, res.Kind);
        Assert.Equal(5, res.Results.Count);

        transport.Feed(MllpCodec.Wrap(SampleFiles.Load("14_hl7_mindray_qry_q02.hl7")));
        var ack2 = Hl7Parser.Parse(await transport.NextSentAsync());
        Assert.Equal("ACK", ack2.MessageCode);
        var q = await sink.NextAsync();
        Assert.Equal(InboundMessageKind.Query, q.Kind);
        var qck = Hl7Parser.Parse(await transport.NextSentAsync());
        Assert.Equal("QCK^Q02", qck.MessageType);
        var dsrRaw = await transport.NextSentAsync();
        Assert.StartsWith("\u000b", dsrRaw);
        Assert.Contains("DSP|21||10260048|||", dsrRaw);
        Assert.Contains("DSP|29||^GLU^^|||", dsrRaw);
    }

    [Fact]
    public async Task Text_session_frames_rapid_packets_and_sends_crc_ack()
    {
        var transport = new InMemoryTransport();
        var sink = new FakeSink();
        var cfg = AnalyzerProfileCatalog.Default.CreateDefaultConfig("RAPID", "r1");
        await using var session = new TextSession(cfg, transport, new FakeOrderSource(), sink, FastOptions, NullLogger.Instance);
        await session.StartAsync(CancellationToken.None);
        var packet = SampleFiles.Load("11_rapid_smp_new_data.txt");
        var bytes = Encoding.Latin1.GetBytes(packet);
        transport.Feed(bytes[..20]);
        transport.Feed(bytes[20..]);
        var res = await sink.NextAsync();
        Assert.Equal(InboundMessageKind.Results, res.Kind);
        Assert.Equal(5, res.Results.Count);
        Assert.Equal("\u0002\u0006\u00030B\u0004", await transport.NextSentAsync());
    }

    private static List<string> SplitFrames(string raw)
    {
        var frames = new List<string>();
        int i = 0;
        while ((i = raw.IndexOf('\u0002', i)) >= 0)
        {
            int lf = raw.IndexOf('\n', i);
            if (lf < 0) break;
            frames.Add(raw.Substring(i, lf - i + 1));
            i = lf + 1;
        }
        return frames;
    }
}
