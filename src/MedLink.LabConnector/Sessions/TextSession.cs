// =============================================================================
// MedLink LIS Analyzer Connector — сесія текстових протоколів (TEXT/HUMA5L/UC1000/
// CYAN/FUJI/RAPID/IRIS/TXT/JUNIOR): рамкування за bop/eop профілю, таймер
// «кінця пакета» для протоколів без eop (CYAN), ACK 0x06 після пакета (як legacy),
// відправлення замовлень як готових кадрів / тексту / ASTM2-послідовності без підтверджень.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text;
using System.Threading.Channels;
using MedLink.LabConnector.Transport;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;
using MedLink.LIS.Core.Protocols.Orders;

namespace MedLink.LabConnector.Sessions;

public sealed class TextSession : SessionBase
{
    private readonly TextProtocolFramer _framer;

    public TextSession(AnalyzerConfigDto cfg, ITransport transport, IOrderSource orders, IInboundSink sink, SessionOptions options, ILogger logger)
        : base(cfg, transport, orders, sink, options, logger)
    {
        _framer = TextProtocolFramer.FromBase64(cfg.Framing?.BopBase64, cfg.Framing?.EopBase64);
    }

    private bool AckAfterPacket => Config.Framing?.AckAfterRecord ?? true;
    private int SleepMs => Math.Clamp(Config.Framing?.SleepMs ?? 100, 0, 5000);

    protected override async Task ProcessLoopAsync(ChannelReader<byte[]> reader, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Для протоколів без eop (CYAN) або при «застряглому» пакеті — таймер тиші
            var idle = _framer.HasEop ? TimeSpan.FromSeconds(20) : TimeSpan.FromMilliseconds(Math.Max(200, Options.PacketIdleMs));
            var chunk = await ReadWithTimeoutAsync(reader, idle, ct);
            if (chunk == null)
            {
                var pending = _framer.FlushByTimeout();
                if (pending != null && pending.Length > 0)
                {
                    Logger.LogInformation("[{Code}] пакет завершено за таймером тиші ({Len} байт)", Config.Code, pending.Length);
                    await HandlePacketAsync(pending, ct);
                }
                continue;
            }
            foreach (var packet in _framer.Push(chunk))
                await HandlePacketAsync(packet, ct);
        }
    }

    private async Task HandlePacketAsync(byte[] packet, CancellationToken ct)
    {
        var text = DecodeText(packet, Encoding);
        if (text.Trim('\0', ' ', '\r', '\n').Length == 0) return;
        // Для RAPID парсеру потрібні STX/ETX (CRC) — відновлюємо обгортку bop/eop
        if (_framer.HasBop || _framer.HasEop)
            text = Encoding.GetString(_framer.Bop) + text + Encoding.GetString(_framer.Eop);
        await HandleInboundAsync(text, ct);
    }

    protected override async Task AfterInboundAsync(AnalyzerInboundMessage parsed, CancellationToken ct)
    {
        if (!AckAfterPacket) return;
        try { await SendBytesAsync(new[] { AstmControl.ACK }, ct); }
        catch (Exception ex) { Logger.LogDebug("[{Code}] ACK не відправлено: {Error}", Config.Code, ex.Message); }
    }

    protected override async Task SendOutboundAsync(IReadOnlyList<string> records, OrderOutputKind kind, CancellationToken ct)
    {
        if (records.Count == 0) return;
        using var _ = await AcquireSendAsync(ct);
        switch (kind)
        {
            case OrderOutputKind.RawFrames:
                foreach (var f in records)
                {
                    await SendRawAsync(f, ct, "кадр");
                    if (SleepMs > 0) await Task.Delay(SleepMs, ct);
                }
                break;
            case OrderOutputKind.TextLines:
                {
                    var terminator = OrderBuilder.RecordTerminator;
                    var text = string.Join(terminator, records) + terminator;
                    await SendRawAsync(text, ct, "текст замовлення");
                    break;
                }
            case OrderOutputKind.Hl7Messages:
                foreach (var m in records) await SendRawAsync(MllpCodec.WrapString(m), ct, "HL7");
                break;
            default:
                {
                    // ASTM2 (legacy Parse): ENQ, кадри зі sleep, EOT — без очікування ACK
                    bool checksum = Config.Framing?.Checksum ?? true;
                    int maxLen = Config.Framing?.MaxFrameLen > 0 ? Config.Framing.MaxFrameLen : AstmFrameCodec.DefaultMaxFrameLen;
                    var frames = AstmFrameCodec.BuildFrames(records, maxLen, checksum);
                    await SendBytesAsync(new[] { AstmControl.ENQ }, ct);
                    if (SleepMs > 0) await Task.Delay(SleepMs, ct);
                    foreach (var f in frames)
                    {
                        await SendRawAsync(f, ct, log: false);
                        if (SleepMs > 0) await Task.Delay(SleepMs, ct);
                    }
                    await SendBytesAsync(new[] { AstmControl.EOT }, ct);
                    Sink.OnOutbound(Config, AstmParser.Join(records), $"ASTM2: {frames.Count} кадр(ів) без підтвердження");
                    break;
                }
        }
    }

    public override Task<string> LinkTestAsync(CancellationToken ct)
        => Task.FromResult(Transport.IsConnected ? $"OK: канал підключено ({Transport.Description})" : "Немає підключення до приладу");
}
