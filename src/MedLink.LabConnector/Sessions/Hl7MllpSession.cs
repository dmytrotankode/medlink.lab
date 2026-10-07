// =============================================================================
// MedLink LIS Analyzer Connector — сесія HL7 v2 поверх MLLP (VT … FS CR).
// ACK (MSA|AA) на кожне повідомлення; розпізнавання повідомлень без MLLP-обгортки
// за «MSH|» та кінцем CR FS CR або таймером тиші (як Timer1 у legacy); ENQ → ACK.
// Запити QRY^Q02 (Mindray BS-240) → замовлення QCK^Q02 + DSR^Q03.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text;
using System.Threading.Channels;
using MedLink.LabConnector.Transport;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;
using MedLink.LIS.Core.Protocols.Orders;

namespace MedLink.LabConnector.Sessions;

public sealed class Hl7MllpSession : SessionBase
{
    private readonly List<byte> _buffer = new();
    private readonly Encoding _encoding;

    public Hl7MllpSession(AnalyzerConfigDto cfg, ITransport transport, IOrderSource orders, IInboundSink sink, SessionOptions options, ILogger logger)
        : base(cfg, transport, orders, sink, options, logger)
    {
        _encoding = Encoding.UTF8;
    }

    protected override Encoding Encoding => _encoding;

    protected override async Task ProcessLoopAsync(ChannelReader<byte[]> reader, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var chunk = await ReadWithTimeoutAsync(reader, TimeSpan.FromMilliseconds(Math.Max(200, Options.PacketIdleMs)), ct);
            if (chunk == null)
            {
                // Таймер тиші: повідомлення без MLLP-обгортки
                if (_buffer.Count > 0)
                {
                    var text = DecodeText(_buffer.ToArray(), Encoding);
                    _buffer.Clear();
                    if (Hl7Parser.LooksLikeHl7(text)) await HandleHl7Async(text, ct);
                    else if (text.Trim('\0', ' ', '\r', '\n', (char)0x0B, (char)0x1C).Length > 0)
                        Logger.LogWarning("[{Code}] відкинуто не-HL7 дані ({Len} байт): {Preview}", Config.Code, text.Length, ProtocolText.Escape(text.Length > 80 ? text[..80] : text));
                }
                continue;
            }
            foreach (var b in chunk)
            {
                if (b == AstmControl.ENQ && _buffer.Count == 0) { await SendBytesAsync(new[] { AstmControl.ACK }, ct); continue; }
                if ((b == AstmControl.ACK || b == AstmControl.NAK) && _buffer.Count == 0) continue;
                _buffer.Add(b);
            }
            await ExtractAsync(ct);
        }
    }

    private async Task ExtractAsync(CancellationToken ct)
    {
        var (messages, remainder) = MllpCodec.ExtractBlocks(_buffer.ToArray());
        if (messages.Count > 0)
        {
            _buffer.Clear();
            _buffer.AddRange(remainder);
            foreach (var m in messages) await HandleHl7Async(DecodeText(m, Encoding), ct);
        }
        else if (_buffer.Count > 0 && _buffer[0] != MllpCodec.StartBlock)
        {
            // Без VT: кінець за CR FS CR (legacy) або FS
            int fs = _buffer.IndexOf(MllpCodec.EndBlock);
            if (fs >= 0)
            {
                var msg = _buffer.GetRange(0, fs).ToArray();
                int cut = fs + 1;
                if (cut < _buffer.Count && _buffer[cut] == MllpCodec.CarriageReturn) cut++;
                _buffer.RemoveRange(0, cut);
                var text = DecodeText(msg, Encoding);
                if (Hl7Parser.LooksLikeHl7(text)) await HandleHl7Async(text, ct);
            }
        }
        if (_buffer.Count > 4 * 1024 * 1024) { Logger.LogWarning("[{Code}] буфер HL7 > 4 МБ — скинуто", Config.Code); _buffer.Clear(); }
    }

    private async Task HandleHl7Async(string text, CancellationToken ct)
    {
        var msg = Hl7Parser.Parse(text);
        // ACK одразу (крім відповідей ACK та власних відповідей на запити)
        if (msg.Msh != null && msg.MessageCode != "ACK" && !msg.Has("MSA"))
        {
            try
            {
                var ack = IsMindrayLegacy ? Hl7AckBuilder.BuildMindrayLegacyAck(DateTime.Now, msg.MessageType, msg.MessageControlId.Length > 0 ? msg.MessageControlId : "1")
                                          : Hl7AckBuilder.BuildAck(msg, DateTime.Now);
                await SendRawAsync(MllpCodec.WrapString(ack), ct, "ACK", log: false);
            }
            catch (Exception ex) { Logger.LogWarning("[{Code}] не вдалося відправити HL7 ACK: {Error}", Config.Code, ex.Message); }
        }
        // Запити QRY^Q02 обробляються в HandleInbound → HandleQuery (парсер повертає Kind=Query)
        _ = Task.Run(() => HandleInboundAsync(text, ct), ct);
    }

    private bool IsMindrayLegacy => string.Equals(Config.TypeCode?.Replace(" ", ""), "MINDRAYBS30", StringComparison.OrdinalIgnoreCase);

    protected override async Task SendOutboundAsync(IReadOnlyList<string> records, OrderOutputKind kind, CancellationToken ct)
    {
        if (records.Count == 0) return;
        using var _ = await AcquireSendAsync(ct);
        switch (kind)
        {
            case OrderOutputKind.Hl7Messages:
                foreach (var m in records)
                {
                    await SendRawAsync(MllpCodec.WrapString(m), ct, "HL7 замовлення");
                    if (Config.Framing?.SleepMs > 0) await Task.Delay(Math.Min(2000, Config.Framing.SleepMs), ct);
                }
                break;
            case OrderOutputKind.RawFrames:
                foreach (var f in records) await SendRawAsync(f, ct, "кадр");
                break;
            default:
                // ASTM-записи / текст на HL7-каналі — одним MLLP-блоком
                await SendRawAsync(MllpCodec.WrapString(string.Join("\r", records)), ct, "текст у MLLP");
                break;
        }
    }

    public override Task<string> LinkTestAsync(CancellationToken ct)
        => Task.FromResult(Transport.IsConnected ? "OK: канал HL7/MLLP підключено (прилад ініціює обмін сам)" : "Немає підключення до приладу");
}
