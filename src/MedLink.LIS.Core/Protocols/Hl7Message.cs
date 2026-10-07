// =============================================================================
// MedLink LIS 4.0 — HL7 v2.x: модель повідомлення, парсер, MLLP-кодек,
// побудовник ACK. Підтримувані сегменти: MSH, PID, PV1, ORC, OBR, OBX, NTE,
// QRD, QRF, SPM, DSP, DSC, MSA, QAK, ERR (та будь-які інші — як узагальнені).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text;

namespace MedLink.LIS.Core.Protocols;

/// <summary>Роздільники HL7 (із MSH-1/MSH-2; за замовчуванням |^~\&amp;).</summary>
public sealed record Hl7Delimiters(char Field = '|', char Component = '^', char Repeat = '~', char Escape = '\\', char SubComponent = '&')
{
    public static Hl7Delimiters Default { get; } = new();

    public static Hl7Delimiters FromMsh(string msh)
    {
        if (msh.Length >= 8 && msh.StartsWith("MSH"))
            return new Hl7Delimiters(msh[3], msh[4], msh[5], msh[6], msh[7]);
        return Default;
    }

    /// <summary>Знімає HL7-екранування: \F\ \S\ \R\ \T\ \E\ \Xdd..\ .</summary>
    public string Unescape(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf(Escape) < 0) return value;
        var sb = new StringBuilder(value.Length);
        int i = 0;
        while (i < value.Length)
        {
            char c = value[i];
            if (c != Escape) { sb.Append(c); i++; continue; }
            int end = value.IndexOf(Escape, i + 1);
            if (end < 0) { sb.Append(value, i, value.Length - i); break; }
            var seq = value.Substring(i + 1, end - i - 1);
            switch (seq)
            {
                case "F": sb.Append(Field); break;
                case "S": sb.Append(Component); break;
                case "R": sb.Append(Repeat); break;
                case "T": sb.Append(SubComponent); break;
                case "E": sb.Append(Escape); break;
                case ".br": sb.Append('\n'); break;
                default:
                    if (seq.Length > 1 && seq[0] == 'X')
                    {
                        var hex = seq[1..];
                        var bytes = new List<byte>();
                        for (int h = 0; h + 1 < hex.Length; h += 2)
                            if (byte.TryParse(hex.AsSpan(h, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)) bytes.Add(b);
                        sb.Append(Encoding.Latin1.GetString(bytes.ToArray()));
                    }
                    else sb.Append(Escape).Append(seq).Append(Escape);
                    break;
            }
            i = end + 1;
        }
        return sb.ToString();
    }

    /// <summary>Екранує службові символи у значенні поля.</summary>
    public string EscapeValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? "";
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c == Field) sb.Append(Escape).Append('F').Append(Escape);
            else if (c == Component) sb.Append(Escape).Append('S').Append(Escape);
            else if (c == Repeat) sb.Append(Escape).Append('R').Append(Escape);
            else if (c == SubComponent) sb.Append(Escape).Append('T').Append(Escape);
            else if (c == Escape) sb.Append(Escape).Append('E').Append(Escape);
            else sb.Append(c);
        }
        return sb.ToString();
    }
}

/// <summary>Сегмент HL7. Поля індексуються за стандартом: Field(1) — перше поле після імені (для MSH Field(1) = '|').</summary>
public sealed class Hl7Segment
{
    public Hl7Segment(string name, IReadOnlyList<string> fields, Hl7Delimiters delimiters, string rawText)
    {
        Name = name;
        Fields = fields;
        Delimiters = delimiters;
        RawText = rawText;
    }

    public string Name { get; }
    /// <summary>Fields[0] — ім'я сегмента; Fields[n] — поле n.</summary>
    public IReadOnlyList<string> Fields { get; }
    public Hl7Delimiters Delimiters { get; }
    public string RawText { get; }

    /// <summary>Поле за HL7-номером (сире значення, перший повтор не виділяється).</summary>
    public string Field(int n) => n >= 0 && n < Fields.Count ? Fields[n] : "";

    /// <summary>Повтори поля (~).</summary>
    public string[] Repeats(int n)
    {
        var f = Field(n);
        return f.Length == 0 ? Array.Empty<string>() : f.Split(Delimiters.Repeat);
    }

    /// <summary>Компонент (1-based) першого повтору поля, з розекрануванням.</summary>
    public string Component(int n, int component)
    {
        var f = Field(n).Split(Delimiters.Repeat)[0];
        var parts = f.Split(Delimiters.Component);
        return component >= 1 && component <= parts.Length ? Delimiters.Unescape(parts[component - 1]) : "";
    }

    /// <summary>Усі компоненти першого повтору.</summary>
    public string[] Components(int n) => Field(n).Split(Delimiters.Repeat)[0].Split(Delimiters.Component);

    /// <summary>Значення поля з розекрануванням (увесь текст поля).</summary>
    public string Value(int n) => Delimiters.Unescape(Field(n));

    public override string ToString() => RawText;
}

/// <summary>Повідомлення HL7 v2 — список сегментів.</summary>
public sealed class Hl7Message
{
    public Hl7Message(IReadOnlyList<Hl7Segment> segments, string rawText)
    {
        Segments = segments;
        RawText = rawText;
    }

    public IReadOnlyList<Hl7Segment> Segments { get; }
    public string RawText { get; }

    public Hl7Segment? Msh => Segments.FirstOrDefault(s => s.Name == "MSH");
    public Hl7Segment? First(string name) => Segments.FirstOrDefault(s => s.Name == name);
    public IEnumerable<Hl7Segment> All(string name) => Segments.Where(s => s.Name == name);
    public bool Has(string name) => Segments.Any(s => s.Name == name);

    /// <summary>MSH-9: тип повідомлення, напр. "ORU^R01".</summary>
    public string MessageType => Msh?.Field(9) ?? "";
    /// <summary>MSH-9.1</summary>
    public string MessageCode => Msh?.Component(9, 1) ?? "";
    /// <summary>MSH-9.2</summary>
    public string TriggerEvent => Msh?.Component(9, 2) ?? "";
    /// <summary>MSH-10</summary>
    public string MessageControlId => Msh?.Field(10) ?? "";
    /// <summary>MSH-12</summary>
    public string Version => Msh?.Field(12) ?? "";
    public string SendingApplication => Msh?.Field(3) ?? "";
    public string SendingFacility => Msh?.Field(4) ?? "";
    public string ReceivingApplication => Msh?.Field(5) ?? "";
    public string ReceivingFacility => Msh?.Field(6) ?? "";
}

/// <summary>Парсер HL7 v2 (сегменти розділені CR, LF або CR LF; MLLP-обгортка знімається).</summary>
public static class Hl7Parser
{
    public static Hl7Message Parse(string raw)
    {
        raw ??= "";
        var text = MllpCodec.Unwrap(raw);
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var delimiters = Hl7Delimiters.Default;
        var segments = new List<Hl7Segment>(lines.Length);
        foreach (var lineRaw in lines)
        {
            var line = lineRaw.Trim('\0', ' ', '\t');
            if (line.Length < 3) continue;
            if (line.StartsWith("MSH")) delimiters = Hl7Delimiters.FromMsh(line);
            var fields = new List<string>();
            if (line.StartsWith("MSH") && line.Length >= 8)
            {
                // MSH-1 = роздільник поля, MSH-2 = інші роздільники
                fields.Add("MSH");
                fields.Add(line[3].ToString());
                fields.Add(line.Substring(4, 4));
                if (line.Length > 8) fields.AddRange(line[9..].Split(delimiters.Field));
            }
            else
            {
                fields.AddRange(line.Split(delimiters.Field));
            }
            var name = fields[0].Trim().ToUpperInvariant();
            if (name.Length > 3) name = name[..3];
            segments.Add(new Hl7Segment(name, fields, delimiters, line));
        }
        return new Hl7Message(segments, raw);
    }

    /// <summary>Чи схоже на HL7 (містить "MSH|").</summary>
    public static bool LooksLikeHl7(string raw) => raw != null && raw.Contains("MSH|", StringComparison.Ordinal);

    /// <summary>Розбір дати/часу HL7 (yyyyMMdd[HHmmss[.ffff]][+zzzz]).</summary>
    public static DateTime? ParseTimestamp(string? ts)
    {
        if (string.IsNullOrWhiteSpace(ts)) return null;
        var s = ts.Trim();
        if (s.Length < 4) return null;
        int plus = s.Length > 8 ? s.IndexOfAny(new[] { '+', '-' }, 8) : -1;
        if (plus > 0) s = s[..plus];
        s = s.Replace(".", "");
        string[] formats = { "yyyyMMddHHmmssffff", "yyyyMMddHHmmssfff", "yyyyMMddHHmmssff", "yyyyMMddHHmmssf", "yyyyMMddHHmmss", "yyyyMMddHHmm", "yyyyMMddHH", "yyyyMMdd", "yyyyMM", "yyyy" };
        return DateTime.TryParseExact(s, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var dt) ? dt : null;
    }
}

/// <summary>MLLP: &lt;VT&gt; повідомлення &lt;FS&gt;&lt;CR&gt;.</summary>
public static class MllpCodec
{
    public const byte StartBlock = 0x0B;
    public const byte EndBlock = 0x1C;
    public const byte CarriageReturn = 0x0D;

    public static byte[] Wrap(string message, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var body = encoding.GetBytes(message.TrimEnd('\r', '\n'));
        var result = new byte[body.Length + 4];
        result[0] = StartBlock;
        Buffer.BlockCopy(body, 0, result, 1, body.Length);
        result[body.Length + 1] = CarriageReturn;
        result[body.Length + 2] = EndBlock;
        result[body.Length + 3] = CarriageReturn;
        return result;
    }

    /// <summary>Обгортає як рядок (символи VT/FS/CR вставлені у текст).</summary>
    public static string WrapString(string message)
        => (char)StartBlock + message.TrimEnd('\r', '\n') + (char)CarriageReturn + (char)EndBlock + (char)CarriageReturn;

    /// <summary>Знімає MLLP-обгортку (якщо є) з тексту.</summary>
    public static string Unwrap(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var s = raw;
        int vt = s.IndexOf((char)StartBlock);
        if (vt >= 0) s = s[(vt + 1)..];
        int fs = s.IndexOf((char)EndBlock);
        if (fs >= 0) s = s[..fs];
        return s.Trim('\r', '\n', '\0');
    }

    /// <summary>Знімає MLLP-обгортку з байтів; повертає текст у кодуванні (UTF-8 за замовчуванням).</summary>
    public static string Unwrap(ReadOnlySpan<byte> raw, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        int start = raw.IndexOf(StartBlock);
        var span = start >= 0 ? raw[(start + 1)..] : raw;
        int end = span.IndexOf(EndBlock);
        if (end >= 0) span = span[..end];
        return encoding.GetString(span).Trim('\r', '\n', '\0');
    }

    /// <summary>
    /// Витягує з буфера повні MLLP-блоки. Повертає список повідомлень (без обгортки) та залишок байтів.
    /// </summary>
    public static (List<byte[]> Messages, byte[] Remainder) ExtractBlocks(byte[] buffer)
    {
        var messages = new List<byte[]>();
        int pos = 0;
        while (pos < buffer.Length)
        {
            int start = Array.IndexOf(buffer, StartBlock, pos);
            if (start < 0) { pos = buffer.Length; break; }
            int end = Array.IndexOf(buffer, EndBlock, start + 1);
            if (end < 0) { pos = start; break; }
            var msg = new byte[end - start - 1];
            Buffer.BlockCopy(buffer, start + 1, msg, 0, msg.Length);
            messages.Add(msg);
            pos = end + 1;
            if (pos < buffer.Length && buffer[pos] == CarriageReturn) pos++;
        }
        var remainder = pos < buffer.Length ? buffer[pos..] : Array.Empty<byte>();
        return (messages, remainder);
    }
}

/// <summary>Побудовник підтверджень HL7 ACK (MSA|AA|…).</summary>
public static class Hl7AckBuilder
{
    /// <summary>
    /// Будує ACK на вхідне повідомлення: MSH із поміняними відправником/отримувачем, MSA|код|MSH-10.
    /// </summary>
    public static string BuildAck(Hl7Message inbound, DateTime now, string ackCode = "AA", string? textMessage = null, string sendingApp = "MedLinkLIS", string sendingFacility = "MedLink", string? version = null, string? controlId = null)
    {
        var msh = inbound.Msh;
        var d = msh?.Delimiters ?? Hl7Delimiters.Default;
        var recvApp = msh?.Field(3) ?? "";
        var recvFac = msh?.Field(4) ?? "";
        var ver = version ?? (string.IsNullOrWhiteSpace(msh?.Field(12)) ? "2.3.1" : msh!.Field(12));
        var inboundId = inbound.MessageControlId;
        controlId ??= "ACK" + now.ToString("yyyyMMddHHmmssfff");
        var ts = now.ToString("yyyyMMddHHmmss");
        var charset = msh?.Field(18) ?? "";
        var sb = new StringBuilder();
        sb.Append("MSH").Append(d.Field).Append(d.Component).Append(d.Repeat).Append(d.Escape).Append(d.SubComponent)
          .Append(d.Field).Append(sendingApp).Append(d.Field).Append(sendingFacility)
          .Append(d.Field).Append(recvApp).Append(d.Field).Append(recvFac)
          .Append(d.Field).Append(ts).Append(d.Field)
          .Append(d.Field).Append("ACK").Append(d.Component).Append(inbound.TriggerEvent).Append(d.Component).Append("ACK")
          .Append(d.Field).Append(controlId).Append(d.Field).Append("P").Append(d.Field).Append(ver);
        if (!string.IsNullOrEmpty(charset)) sb.Append(d.Field).Append(d.Field).Append(d.Field).Append(d.Field).Append(d.Field).Append(d.Field).Append(charset);
        sb.Append('\r');
        sb.Append("MSA").Append(d.Field).Append(ackCode).Append(d.Field).Append(inboundId);
        if (!string.IsNullOrEmpty(textMessage)) sb.Append(d.Field).Append(d.EscapeValue(textMessage));
        sb.Append('\r');
        return sb.ToString();
    }

    /// <summary>ACK у стилі Mindray BS-30 із legacy Simplex: MSH|^~\&amp;|||||ts||ORU^R01|1|P|2.3.1||||||UNICODE + MSA|AA|1.</summary>
    public static string BuildMindrayLegacyAck(DateTime now, string messageType = "ORU^R01", string controlId = "1", string charset = "UNICODE")
        => $"MSH|^~\\&|||||{now:yyyyMMddHHmmss}||{messageType}|{controlId}|P|2.3.1||||||{charset}\rMSA|AA|{controlId}\r";
}
