// =============================================================================
// Локальний (резервний) парсер повідомлень аналізаторів для /analyzers/{id}/simulate.
// ІЗОЛЬОВАНО: коли команда коннектора завершить MedLink.LIS.Core.Protocols (AstmParser/Hl7Parser),
// достатньо зареєструвати іншу реалізацію IAnalyzerMessageParser у Program.cs.
// Підтримує ASTM E1394 (H/P/O/R/Q/L, кадри STX..ETX з контрольною сумою) та HL7 v2 ORU^R01 / QRY (MSH/PID/OBR/OBX/QRD).
// =============================================================================
using System.Globalization;
using System.Text.RegularExpressions;

namespace MedLink.LIS.Api.Services.Parsing;

public sealed class ParsedResultItem
{
    public string Barcode { get; set; } = "";
    public string AnalyzerCode { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Unit { get; set; }
    public string? Flags { get; set; }
    public string? ReferenceText { get; set; }
    public DateTime? MeasuredAt { get; set; }
    public string? ResultStatus { get; set; }
}

public sealed class ParsedAnalyzerMessage
{
    /// <summary>Query | Results | Order | Ack | Other</summary>
    public string Kind { get; set; } = "Other";
    public string Protocol { get; set; } = "UNKNOWN";
    public string? Barcode { get; set; }
    public string? PatientId { get; set; }
    public string? PatientName { get; set; }
    public string? SenderName { get; set; }
    public List<string> RequestedTests { get; set; } = new();
    public List<ParsedResultItem> Results { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Records { get; set; } = new();
}

public interface IAnalyzerMessageParser
{
    ParsedAnalyzerMessage Parse(string raw, string? protocolHint = null);
}

public sealed class AnalyzerMessageParserFallback : IAnalyzerMessageParser
{
    private const char STX = '\x02', ETX = '\x03', EOT = '\x04', ENQ = '\x05', ACK = '\x06', ETB = '\x17', CR = '\r', LF = '\n', VT = '\x0B', FS = '\x1C';

    public ParsedAnalyzerMessage Parse(string raw, string? protocolHint = null)
    {
        var text = Unescape(raw ?? "");
        var hint = (protocolHint ?? "").ToUpperInvariant();
        if (hint.Contains("HL7") || text.Contains("MSH|")) return ParseHl7(text);
        return ParseAstm(text);
    }

    /// <summary>Підтримка текстових позначень керуючих символів у зразках: &lt;STX&gt;, &lt;CR&gt; тощо.</summary>
    public static string Unescape(string s) => s
        .Replace("<ENQ>", ENQ.ToString()).Replace("<ACK>", ACK.ToString()).Replace("<NAK>", "\x15").Replace("<STX>", STX.ToString())
        .Replace("<ETX>", ETX.ToString()).Replace("<ETB>", ETB.ToString()).Replace("<EOT>", EOT.ToString()).Replace("<CR>", CR.ToString())
        .Replace("<LF>", LF.ToString()).Replace("<VT>", VT.ToString()).Replace("<FS>", FS.ToString()).Replace("\\r", CR.ToString()).Replace("\\n", LF.ToString());

    // ------------------------------------------------------------------ ASTM
    private ParsedAnalyzerMessage ParseAstm(string text)
    {
        var msg = new ParsedAnalyzerMessage { Protocol = "ASTM" };
        var records = new List<string>();
        if (text.Contains(STX))
        {
            // Кадри: <STX>N<record><CR><ETX|ETB>CS<CR><LF>
            foreach (Match f in Regex.Matches(text, "\x02(\\d)(.*?)(\x03|\x17)([0-9A-Fa-f]{2})?\r?\n?", RegexOptions.Singleline))
            {
                var body = f.Groups[2].Value;
                var checksum = f.Groups[4].Value;
                if (!string.IsNullOrEmpty(checksum))
                {
                    var calc = AstmChecksum(f.Groups[1].Value + body + f.Groups[3].Value);
                    if (!string.Equals(calc, checksum, StringComparison.OrdinalIgnoreCase)) msg.Warnings.Add($"Кадр {f.Groups[1].Value}: контрольна сума {checksum} ≠ обчислена {calc}");
                }
                records.AddRange(body.Split(CR, StringSplitOptions.RemoveEmptyEntries));
            }
        }
        else records.AddRange(text.Split(new[] { CR, LF }, StringSplitOptions.RemoveEmptyEntries));

        string? currentBarcode = null;
        foreach (var rec in records.Select(r => r.Trim()).Where(r => r.Length > 0))
        {
            msg.Records.Add(rec);
            var fields = rec.Split('|');
            var type = fields[0].ToUpperInvariant();
            switch (type)
            {
                case "H":
                    msg.SenderName = Field(fields, 4)?.Replace('^', ' ').Trim();
                    break;
                case "P":
                    msg.PatientId = FirstNonEmpty(Field(fields, 3), Field(fields, 2));
                    msg.PatientName = Field(fields, 5)?.Replace('^', ' ').Trim();
                    break;
                case "Q":
                    msg.Kind = "Query";
                    var q = Field(fields, 2) ?? "";
                    var parts = q.Split('^');
                    msg.Barcode = parts.Skip(1).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? parts.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
                    break;
                case "O":
                    currentBarcode = FirstNonEmpty(Field(fields, 2), Field(fields, 3)?.Split('^')[0]);
                    msg.Barcode ??= currentBarcode;
                    var tests = Field(fields, 4) ?? "";
                    foreach (var t in tests.Split('\\', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var code = t.Split('^').Skip(3).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? t.Trim('^');
                        if (!string.IsNullOrWhiteSpace(code)) msg.RequestedTests.Add(code);
                    }
                    if (msg.Kind == "Other") msg.Kind = "Order";
                    break;
                case "R":
                    msg.Kind = "Results";
                    var idParts = (Field(fields, 2) ?? "").Split('^');
                    var analyzerCode = idParts.Skip(3).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? idParts.LastOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "";
                    msg.Results.Add(new ParsedResultItem
                    {
                        Barcode = currentBarcode ?? msg.Barcode ?? "", AnalyzerCode = analyzerCode, Value = Field(fields, 3) ?? "", Unit = Field(fields, 4),
                        ReferenceText = Field(fields, 5), Flags = Field(fields, 6), ResultStatus = Field(fields, 8), MeasuredAt = ParseTs(Field(fields, 12))
                    });
                    break;
                case "L":
                    break;
                case "C":
                    break;
                default:
                    if (type.Length > 1 && !char.IsLetter(type[0])) msg.Warnings.Add($"Нерозпізнаний запис: {rec}");
                    break;
            }
        }
        if (msg.Records.Count == 0) msg.Warnings.Add("Повідомлення не містить ASTM-записів");
        if (msg.Kind == "Results" && msg.Results.Count == 0) msg.Warnings.Add("Записи R відсутні");
        return msg;
    }

    public static string AstmChecksum(string frameBodyWithEtx)
    {
        var sum = 0;
        foreach (var ch in frameBodyWithEtx) sum = (sum + ch) & 0xFF;
        return sum.ToString("X2");
    }

    // ------------------------------------------------------------------ HL7
    private ParsedAnalyzerMessage ParseHl7(string text)
    {
        var msg = new ParsedAnalyzerMessage { Protocol = "HL7" };
        var body = text.Trim(VT, FS, CR, LF, ' ');
        var segments = body.Split(new[] { CR, LF }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 3).ToList();
        string? barcode = null;
        foreach (var seg in segments)
        {
            msg.Records.Add(seg);
            var f = seg.Split('|');
            switch (f[0].ToUpperInvariant())
            {
                case "MSH":
                    msg.SenderName = $"{Field(f, 2)} {Field(f, 3)}".Trim();
                    var type = Field(f, 8) ?? "";
                    msg.Kind = type.StartsWith("ORU") ? "Results" : type.StartsWith("QRY") || type.StartsWith("QBP") ? "Query" : type.StartsWith("ORM") || type.StartsWith("OML") ? "Order" : type.StartsWith("ACK") ? "Ack" : "Other";
                    break;
                case "PID":
                    msg.PatientId = Field(f, 3)?.Split('^')[0];
                    msg.PatientName = Field(f, 5)?.Replace('^', ' ').Trim();
                    break;
                case "QRD":
                    msg.Barcode = Field(f, 8)?.Split('^')[0];
                    break;
                case "OBR":
                    barcode = FirstNonEmpty(Field(f, 3)?.Split('^')[0], Field(f, 2)?.Split('^')[0]);
                    msg.Barcode ??= barcode;
                    var code = Field(f, 4)?.Split('^')[0];
                    if (!string.IsNullOrWhiteSpace(code)) msg.RequestedTests.Add(code);
                    break;
                case "SPM":
                    barcode = FirstNonEmpty(Field(f, 2)?.Split('^')[0], barcode);
                    msg.Barcode ??= barcode;
                    break;
                case "OBX":
                    msg.Results.Add(new ParsedResultItem
                    {
                        Barcode = barcode ?? msg.Barcode ?? "", AnalyzerCode = Field(f, 3)?.Split('^')[0] ?? "", Value = Field(f, 5) ?? "", Unit = Field(f, 6)?.Split('^')[0],
                        ReferenceText = Field(f, 7), Flags = Field(f, 8), ResultStatus = Field(f, 11), MeasuredAt = ParseTs(Field(f, 14))
                    });
                    break;
                case "MSA":
                    msg.Kind = "Ack";
                    if (Field(f, 1) != "AA") msg.Warnings.Add($"ACK код {Field(f, 1)}: {Field(f, 3)}");
                    break;
            }
        }
        if (!segments.Any(s => s.StartsWith("MSH", StringComparison.OrdinalIgnoreCase))) msg.Warnings.Add("Відсутній сегмент MSH");
        return msg;
    }

    private static string? Field(string[] f, int i) => i < f.Length && !string.IsNullOrEmpty(f[i]) ? f[i] : null;
    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static DateTime? ParseTs(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var digits = new string(s.TakeWhile(char.IsDigit).ToArray());
        foreach (var fmt in new[] { "yyyyMMddHHmmss", "yyyyMMddHHmm", "yyyyMMdd" })
            if (DateTime.TryParseExact(digits, fmt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt)) return dt.ToUniversalTime();
        return null;
    }
}

/// <summary>Генерація тексту замовлення для приладу (для налагодження /order-preview): ASTM H/P/O/L або HL7 ORM^O01.</summary>
public static class AnalyzerOrderPreviewBuilder
{
    public static string Build(MedLink.LIS.Core.Contracts.AnalyzerOrderDto order, string protocol, string senderName = "MedLinkLIS")
    {
        var ts = DateTime.Now.ToString("yyyyMMddHHmmss");
        if (protocol.Contains("HL7", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"MSH|^~\\&|{senderName}|MedLink|Analyzer||{ts}||ORM^O01|{ts}|P|2.3.1\r");
            sb.Append($"PID|1||{order.Patient.Id}^^^MedLink||{order.Patient.LastName}^{order.Patient.FirstName}||{order.Patient.BirthDate:yyyyMMdd}|{order.Patient.Gender}\r");
            sb.Append($"PV1|1|O\r");
            var i = 0;
            foreach (var t in order.Tests)
            {
                sb.Append($"ORC|NW|{order.OrderNumber}|{order.Barcode}||||^^^^^{(order.Priority == "S" ? "S" : "R")}||{ts}\r");
                sb.Append($"OBR|{++i}|{order.OrderNumber}|{order.Barcode}|{t.AnalyzerCode}^{t.TestCode}|{(order.Priority == "S" ? "S" : "R")}||{ts}|||||||||||||||||{order.SampleType}\r");
            }
            return sb.ToString();
        }
        var records = new List<string>
        {
            $"H|\\^&|||{senderName}|||||||P|E1394-97|{ts}",
            $"P|1||{order.Patient.Id}||{order.Patient.LastName}^{order.Patient.FirstName}||{order.Patient.BirthDate:yyyyMMdd}|{order.Patient.Gender}",
            $"O|1|{order.Barcode}||{string.Join("\\", order.Tests.Select(t => $"^^^{t.AnalyzerCode}"))}|{order.Priority}|{ts}|||||A||||{order.SampleType}||||||||||O",
            "L|1|N"
        };
        var sb2 = new System.Text.StringBuilder();
        sb2.Append("<ENQ>\n");
        var n = 1;
        foreach (var r in records)
        {
            var frame = $"{n}{r}\r\x03";
            var cs = AnalyzerMessageParserFallback.AstmChecksum(frame);
            sb2.Append($"<STX>{n}{r}<CR><ETX>{cs}<CR><LF>\n");
            n = n % 7 + 1;
        }
        sb2.Append("<EOT>");
        return sb2.ToString();
    }
}
