// =============================================================================
// MedLink LIS 4.0 — парсери текстових (не ASTM/HL7) протоколів приладів:
// Siemens RAPIDPoint (RAPID), Fujifilm DRI-CHEM (FUJI), Roche Cobas Integra (INTEGRA),
// Cypress Cyan / HumaCount 5L (CYAN), DIRUI H100 / ElytePlus (KEYVALUE), UC-1000,
// Vital Flexor Junior (JUNIOR), APOTI, Clinitek (CLINTEC), BioSystems A15 (TABULAR),
// IRIS iQ200 (IRIS). Порт гілок parse_lab_message.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MedLink.LIS.Core.Contracts;

namespace MedLink.LIS.Core.Protocols.Parsers;

/// <summary>Спільна основа для текстових парсерів: гарантія «ніколи не кидати».</summary>
public abstract class TextParserBase : IAnalyzerMessageParser
{
    public abstract string Kind { get; }

    public AnalyzerInboundMessage Parse(AnalyzerConfigDto cfg, string raw)
    {
        raw ??= "";
        var result = new AnalyzerInboundMessage { Raw = raw };
        try
        {
            ParseCore(cfg, raw, result);
        }
        catch (Exception ex)
        {
            result.Kind = InboundMessageKind.Other;
            result.Warnings.Add($"Помилка розбору {Kind}: {ex.Message}");
        }
        return result;
    }

    protected abstract void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result);

    protected static string[] Lines(string raw) => raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

    protected static string StripControl(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw) if (c >= 0x20 || c == '\t' || c == '\r' || c == '\n') sb.Append(c);
        return sb.ToString();
    }
}

/// <summary>
/// Siemens RAPIDPoint 500 (протокол RAPID): &lt;STX&gt;NAME&lt;FS&gt;&lt;RS&gt;key&lt;GS&gt;value&lt;GS&gt;&lt;GS&gt;&lt;GS&gt;&lt;FS&gt;…&lt;RS&gt;&lt;ETX&gt;CRC&lt;EOT&gt;.
/// SMP_NEW_DATA → результати (поля m*/c*), SMP_NEW_AV/QC_NEW_AV → SMP_REQ, PAT_DEMOG_REQ → Query (демографія),
/// службові стани → ACK-кадр.
/// </summary>
public sealed class RapidParser : TextParserBase
{
    public override string Kind => "TEXT_RAPID";

    public const char FS = (char)0x1C, GS = (char)0x1D, RS = (char)0x1E;
    private static readonly string[] AckTriggers = { "SYS_NOT_READY", "SMP_START", "SYS_WOPR", "SYS_READY", "SMP_ABORT", "SYS_MEASURING", "ID_REQ" };

    /// <summary>Контрольна сума RAPID: сума байтів від STX до ETX включно mod 256, 2 hex (get_crc у Simplex).</summary>
    public static string Crc(string frameFromStxToEtx) => AstmFrameCodec.Checksum(frameFromStxToEtx, Encoding.Latin1);

    /// <summary>Готовий ACK-кадр: &lt;STX&gt;&lt;ACK&gt;&lt;ETX&gt;0B&lt;EOT&gt;.</summary>
    public static string AckFrame()
    {
        var body = $"{AstmControl.StxChar}{AstmControl.AckChar}{AstmControl.EtxChar}";
        return body + Crc(body) + AstmControl.EotChar;
    }

    /// <summary>Будує кадр RAPID із пар ключ/значення; CRC та EOT додаються.</summary>
    public static string BuildFrame(string name, IEnumerable<(string Key, string Value)> fields)
    {
        var sb = new StringBuilder();
        sb.Append(AstmControl.StxChar).Append(name).Append(FS).Append(RS);
        foreach (var (k, v) in fields)
            sb.Append(k).Append(GS).Append(v).Append(GS).Append(GS).Append(GS).Append(FS);
        sb.Append(RS).Append(AstmControl.EtxChar);
        var body = sb.ToString();
        return body + Crc(body) + AstmControl.EotChar;
    }

    /// <summary>Розбирає поля key→value (перший GS-елемент після ключа).</summary>
    public static Dictionary<string, string> Fields(string raw)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in raw.Split(FS))
        {
            var p = part.Trim(RS, AstmControl.StxChar, AstmControl.EtxChar, AstmControl.EotChar, '\r', '\n', ' ');
            if (p.Length == 0) continue;
            var items = p.Split(GS);
            if (items.Length < 2) continue;
            var key = items[0].Trim();
            if (key.Length == 0 || dict.ContainsKey(key)) continue;
            dict[key] = items[1].Trim();
        }
        return dict;
    }

    protected override void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result)
    {
        if (raw.Contains("SMP_NEW_DATA"))
        {
            result.Kind = InboundMessageKind.Results;
            var fields = Fields(raw);
            var barcode = fields.TryGetValue("iPID", out var pid) ? pid : "";
            if (barcode.Length > 0) result.Barcodes.Add(barcode);
            if (raw.Contains("QC_") || (fields.TryGetValue("iSMPTYPE", out var st) && st.Contains("QC", StringComparison.OrdinalIgnoreCase))) result.IsQc = true;
            foreach (var (key, value) in fields)
            {
                if (key.Length > 1 && (key[0] == 'm' || key[0] == 'c') && !key.StartsWith("mod", StringComparison.OrdinalIgnoreCase))
                    result.AddResult(barcode, key, value);
            }
            if (barcode.Length == 0) result.Warnings.Add("SMP_NEW_DATA без iPID");
            // Прилад очікує підтвердження
            result.ImmediateReply = AckFrame();
            return;
        }
        if (raw.Contains("SMP_NEW_AV") || raw.Contains("QC_NEW_AV"))
        {
            // Доступні нові дані → запитуємо їх (SMP_REQ) за rSEQ
            var fields = Fields(raw);
            var seq = fields.TryGetValue("rSEQ", out var s) ? s : "";
            var iid = fields.TryGetValue("iIID", out var i) ? i : "";
            var mod = fields.TryGetValue("aMOD", out var m) ? m : "0500";
            result.Kind = InboundMessageKind.ProtocolReply;
            result.ImmediateReply = BuildFrame("SMP_REQ", new[] { ("aMOD", mod), ("iIID", iid), ("rSEQ", seq) });
            return;
        }
        if (raw.Contains("PAT_DEMOG_REQ"))
        {
            var fields = Fields(raw);
            var pid = fields.TryGetValue("iPID", out var p) ? p : "";
            result.Kind = InboundMessageKind.Query;
            if (pid.Length > 0) result.Barcodes.Add(pid); else result.Warnings.Add("PAT_DEMOG_REQ без iPID");
            result.RawQueryToken = pid;
            // iIID/aMOD потрібні побудовнику відповіді PAT_DEMOG_DATA
            result.SamplePosition = (fields.TryGetValue("iIID", out var iid) ? iid : "") + "^" + (fields.TryGetValue("aMOD", out var mod) ? mod : "0500");
            return;
        }
        if (AckTriggers.Any(raw.Contains) || raw.Contains(AstmControl.AckChar))
        {
            result.Kind = InboundMessageKind.ProtocolReply;
            result.ImmediateReply = AckFrame();
            return;
        }
        result.Kind = InboundMessageKind.Other;
        result.Warnings.Add("Невідоме повідомлення RAPID");
    }
}

/// <summary>Fujifilm DRI-CHEM (CSV): R,NORMAL,date,time,seq,sampleId,,…,code,=,"value unit",flag,low,high,blank,code,…</summary>
public sealed class FujiParser : TextParserBase
{
    public override string Kind => "TEXT_FUJI";

    protected override void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result)
    {
        var text = StripControl(raw).Replace("\r", "").Replace("\n", "");
        int start = text.IndexOf("R,", StringComparison.Ordinal);
        if (start < 0) { result.Warnings.Add("Відсутній запис R,"); return; }
        var f = text[start..].Split(',');
        if (f.Length < 15) { result.Warnings.Add("Замало полів у записі FUJI"); return; }
        result.Kind = InboundMessageKind.Results;
        var barcode = f[5].Trim();
        if (barcode.Length > 8) barcode = barcode[..8];
        if (barcode.Length == 0) barcode = f[4].Trim();
        result.Barcodes.Add(barcode);
        var measuredAt = ParseDate(f[2].Trim(), f[3].Trim());
        for (int i = 12; i + 2 < f.Length; i += 7)
        {
            var code = f[i].Trim();
            if (code.Length == 0 || code.StartsWith('h')) break;
            var valueUnit = f[i + 2].Trim();
            var sp = valueUnit.IndexOf(' ');
            var value = sp > 0 ? valueUnit[..sp] : valueUnit;
            var unit = sp > 0 ? valueUnit[(sp + 1)..].Trim() : null;
            var flag = i + 3 < f.Length ? f[i + 3].Trim() : null;
            string? reference = null;
            if (i + 5 < f.Length)
            {
                var lo = f[i + 4].Trim(); var hi = f[i + 5].Trim();
                if (lo.Length > 0 && hi.Length > 0 && !(lo == "0.00" && hi == "0.00")) reference = $"{lo}-{hi}";
            }
            result.AddResult(barcode, code, value, unit, flag == "1" ? null : flag, reference, measuredAt);
        }
    }

    private static DateTime? ParseDate(string d, string t)
    {
        if (DateTime.TryParseExact($"{d} {t}", new[] { "dd-MM-yyyy HH:mm", "MM-dd-yyyy HH:mm", "yyyy-MM-dd HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return dt;
        return null;
    }
}

/// <summary>Roche Cobas Integra 400 (текстовий): "42 …barcode" — запит; "53 barcode", "55 code", "00 value" — результати.</summary>
public sealed class IntegraParser : TextParserBase
{
    public override string Kind => "TEXT_INTEGRA";

    protected override void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result)
    {
        var lines = Lines(StripControl(raw)).Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToList();
        var q = lines.FirstOrDefault(l => l.StartsWith("42 "));
        if (q != null)
        {
            result.Kind = InboundMessageKind.Query;
            string barcode = q.Length >= 26 ? q.Substring(12, 14).Trim() : q[3..].Trim().Split(' ').LastOrDefault() ?? "";
            if (barcode.Length == 0) result.Warnings.Add("Запит 42 без штрихкоду"); else result.Barcodes.Add(barcode);
            result.RawQueryToken = q;
            return;
        }
        if (!lines.Any(l => l.StartsWith("53 "))) { result.Warnings.Add("Немає запису 53 (штрихкод)"); return; }
        result.Kind = InboundMessageKind.Results;
        string current = ""; string? code = null;
        foreach (var l in lines)
        {
            if (l.StartsWith("53 "))
            {
                current = l[3..].Trim();
                if (current.Length > 14) current = current[..14].Trim();
                var sp = current.IndexOf(' ');
                if (sp > 0) current = current[..sp];
                if (current.Length > 0 && !result.Barcodes.Contains(current)) result.Barcodes.Add(current);
                code = null;
            }
            else if (l.StartsWith("55 "))
            {
                code = l.Length >= 6 ? l.Substring(3, 3).Trim() : l[3..].Trim();
            }
            else if (l.StartsWith("00 ") && code != null)
            {
                var body = l[3..].Trim();
                var value = body.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                // legacy: CAST(... AS decimal(15,6)) — нормалізуємо число ("31.0" → "31", "5.210" → "5.21")
                if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec)) value = dec.ToString("0.######", CultureInfo.InvariantCulture);
                result.AddResult(current, code, value);
                code = null;
            }
        }
    }
}

/// <summary>
/// Cypress Cyan / Human HumaCount 5L — текстовий звіт із табуляціями:
/// "Sample ID:\tbarcode", заголовок "Param\tFlags\tValue\tUnit\t[min-max]", далі рядки параметрів до "WARNINGS:"/порожнього.
/// </summary>
public sealed class CyanParser : TextParserBase
{
    public override string Kind => "TEXT_CYAN";

    protected override void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result)
    {
        var lines = Lines(StripControl(raw));
        string barcode = "";
        foreach (var l in lines)
        {
            var t = l.Trim();
            if (t.StartsWith("Sample ID:", StringComparison.OrdinalIgnoreCase))
            {
                barcode = t["Sample ID:".Length..].Trim('\t', ' ', ':');
                break;
            }
        }
        int header = Array.FindIndex(lines, l => l.TrimStart().StartsWith("Param\t", StringComparison.OrdinalIgnoreCase));
        if (header < 0) { result.Warnings.Add("Заголовок таблиці Param/Value не знайдено"); return; }
        if (barcode.Length == 0) { result.Warnings.Add("Sample ID не знайдено"); }
        var cols = lines[header].Trim().Split('\t');
        int valueIdx = Array.FindIndex(cols, c => c.Trim().Equals("Value", StringComparison.OrdinalIgnoreCase));
        int unitIdx = Array.FindIndex(cols, c => c.Trim().Equals("Unit", StringComparison.OrdinalIgnoreCase));
        int flagIdx = Array.FindIndex(cols, c => c.Trim().Equals("Flags", StringComparison.OrdinalIgnoreCase));
        int rangeIdx = Array.FindIndex(cols, c => c.Trim().StartsWith("[", StringComparison.Ordinal));
        if (valueIdx < 0) valueIdx = 1;
        result.Kind = InboundMessageKind.Results;
        if (barcode.Length > 0) result.Barcodes.Add(barcode);
        for (int i = header + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var t = line.Trim();
            if (t.Length == 0) { if (result.Results.Count > 0) break; else continue; }
            if (t.StartsWith("WARNINGS", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Flags:", StringComparison.OrdinalIgnoreCase)) break;
            var c = line.Split('\t');
            var code = c[0].Trim();
            if (code.Length == 0 || c.Length < 2) continue;
            string value, unit = "", flags = "", range = "";
            if (c.Length > valueIdx && c.Length >= cols.Length - 1)
            {
                value = c[valueIdx].Trim();
                if (unitIdx >= 0 && unitIdx < c.Length) unit = c[unitIdx].Trim();
                if (flagIdx >= 0 && flagIdx < c.Length) flags = c[flagIdx].Trim();
                if (rangeIdx >= 0 && rangeIdx < c.Length) range = c[rangeIdx].Trim().Trim('[', ']');
            }
            else
            {
                // Стовпець Flags пропущено — значення: перший числовий стовпець після коду
                value = c.Skip(1).Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0 && (char.IsDigit(x[0]) || x[0] == '-' || x[0] == '<' || x[0] == '>')) ?? c[1].Trim();
                int vi = Array.FindIndex(c, x => x.Trim() == value);
                if (vi >= 0 && vi + 1 < c.Length) unit = c[vi + 1].Trim();
            }
            int bang = value.IndexOf('!');
            if (bang >= 0) value = value[..bang].Trim();
            result.AddResult(barcode, code, value, unit, flags, range);
        }
    }
}

/// <summary>
/// Узагальнений парсер «ключ значення»: DIRUI H100 (рядки "ID:barcode", "UBG  Normal 3.4umol/L"),
/// ElytePlus ("… Barcode 10042014 K 0.00 Na 138.6 … End") та подібні.
/// </summary>
public sealed class GenericKeyValueParser : TextParserBase
{
    public override string Kind => "TEXT_KEYVALUE";

    private static readonly string[] BarcodeMarkers = { "Barcode", "Sample ID", "SampleID", "ID:", "SID", "Sample No" };

    protected override void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result)
    {
        var text = StripControl(raw);
        var lines = Lines(text).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) { result.Warnings.Add("Порожнє повідомлення"); return; }

        // Режим «один рядок, пари токенів» (ElytePlus)
        var flat = string.Join(' ', lines);
        int bcIdx = flat.IndexOf("Barcode", StringComparison.OrdinalIgnoreCase);
        bool pairMode = bcIdx >= 0 && (lines.Count <= 3 || Regex.IsMatch(flat, @"Barcode\s+\S+\s+[A-Za-z]+\s+[-+]?\d"));
        if (pairMode)
        {
            var tokens = flat[(bcIdx + "Barcode".Length)..].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) { result.Warnings.Add("Barcode без значення"); return; }
            var barcode = tokens[0].Trim(':');
            if (barcode.Length > 8 && barcode.All(char.IsDigit)) barcode = barcode[..8];
            result.Kind = InboundMessageKind.Results;
            result.Barcodes.Add(barcode);
            for (int i = 1; i + 1 < tokens.Length; i += 2)
            {
                var code = tokens[i];
                if (code.Equals("End", StringComparison.OrdinalIgnoreCase)) break;
                if (code.Equals("En", StringComparison.OrdinalIgnoreCase)) continue;
                result.AddResult(barcode, code, tokens[i + 1]);
            }
            return;
        }

        // Режим рядків (DIRUI H100)
        string bc = "";
        var dataLines = new List<string>();
        foreach (var l in lines)
        {
            var marker = BarcodeMarkers.FirstOrDefault(m => l.StartsWith(m, StringComparison.OrdinalIgnoreCase));
            if (marker != null && bc.Length == 0)
            {
                bc = l[marker.Length..].Trim(':', ' ', '\t', '=');
                var sp = bc.IndexOfAny(new[] { ' ', '\t' });
                if (sp > 0) bc = bc[..sp];
                continue;
            }
            if (l.StartsWith("No.", StringComparison.OrdinalIgnoreCase) || l.StartsWith("Date", StringComparison.OrdinalIgnoreCase) || l.StartsWith("Time", StringComparison.OrdinalIgnoreCase)) continue;
            dataLines.Add(l);
        }
        if (bc.Length == 0) { result.Warnings.Add("Штрихкод (ID:/Barcode) не знайдено"); }
        result.Kind = InboundMessageKind.Results;
        if (bc.Length > 0) result.Barcodes.Add(bc);
        foreach (var l in dataLines)
        {
            var m = Regex.Match(l, @"^([A-Za-z][A-Za-z0-9_./%-]*)\s*[:=]?\s+(.+)$");
            if (!m.Success) continue;
            result.AddResult(bc, m.Groups[1].Value, m.Groups[2].Value.Trim());
        }
    }
}

/// <summary>Аналізатор сечі UC-1000 (фіксовані колонки): перший рядок — ID; далі "F value   refl ,CODE"; рядок COLOR.</summary>
public sealed class Uc1000Parser : TextParserBase
{
    public override string Kind => "TEXT_UC1000";
    private static readonly HashSet<string> TwoTokenCodes = new(StringComparer.OrdinalIgnoreCase) { "GLU", "P/C", "A/C" };

    protected override void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result)
    {
        var lines = Lines(StripControl(raw)).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count < 2) { result.Warnings.Add("Замало рядків UC-1000"); return; }
        var first = lines[0];
        var barcode = (first.Length >= 12 ? first[..12] : first.Split(',')[0]).Trim();
        if (barcode.Length == 0) { result.Warnings.Add("ID проби не знайдено"); return; }
        result.Kind = InboundMessageKind.Results;
        result.Barcodes.Add(barcode);
        foreach (var line in lines.Skip(1))
        {
            if (line.Contains("COLOR", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(line, @"^\S?\s+([A-Za-z ]+?)\s+\d\d\b");
                var color = m.Success ? m.Groups[1].Value.Trim() : line.Substring(Math.Min(6, line.Length)).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                result.AddResult(barcode, "COLOR", color);
                continue;
            }
            int comma = line.LastIndexOf(',');
            if (comma < 0) continue;
            var code = line[(comma + 1)..].Trim();
            if (code.Length == 0) continue;
            var body = line[..comma];
            string col1 = body.Length > 2 ? body.Substring(2, Math.Min(6, body.Length - 2)).Trim() : "";
            string col2 = body.Length > 8 ? body.Substring(8, Math.Min(6, body.Length - 8)).Trim() : "";
            string value;
            if (col1 == "-") value = "negative";
            else if (col1 == "+" && code.Equals("NIT", StringComparison.OrdinalIgnoreCase)) value = "positive";
            else if (TwoTokenCodes.Contains(code)) value = (col1 + " " + col2).Trim();
            else value = col1;
            var flag = body.Length > 0 && body[0] != '0' ? body[..1] : null;
            if (value.Length > 0) result.AddResult(barcode, code, value, null, flag);
        }
    }
}

/// <summary>Vital Scientific Flexor Junior: {r;FLEXOR;0;barcode;N;name;dob;sex;;date;time; seq;code;value;text;flag;unit;…}</summary>
public sealed class JuniorParser : TextParserBase
{
    public override string Kind => "TEXT_JUNIOR";

    protected override void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result)
    {
        int start = raw.IndexOf("{r", StringComparison.Ordinal);
        if (start < 0) { result.Warnings.Add("Відсутній запис {r"); return; }
        var f = raw[start..].Split(';');
        if (f.Length < 14) { result.Warnings.Add("Замало полів JUNIOR"); return; }
        var barcode = f[3].Trim();
        result.Kind = InboundMessageKind.Results;
        result.Barcodes.Add(barcode);
        int i = 11;
        while (i + 2 < f.Length)
        {
            if (f[i].Trim().StartsWith('}')) break;
            var code = f[i + 1].Trim();
            var value = f[i + 2].Trim();
            if (code.StartsWith('}') || value.StartsWith('}')) break;
            var unit = i + 5 < f.Length && !f[i + 5].Trim().StartsWith('}') ? f[i + 5].Trim() : null;
            var flag = i + 4 < f.Length ? f[i + 4].Trim() : null;
            if (code.Length > 0 && value.Length > 0) result.AddResult(barcode, code, value, unit, flag);
            i += 6;
        }
    }
}

/// <summary>APOTI: |barcode|seq|code|low|high|lowRef|highRef|value|unit|interp|datetime|…</summary>
public sealed class ApotiParser : TextParserBase
{
    public override string Kind => "TEXT_APOTI";

    protected override void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result)
    {
        var text = StripControl(raw).Trim();
        var f = text.Split('|');
        if (f.Length < 10) { result.Warnings.Add("Замало полів APOTI"); return; }
        var barcode = f[1].Trim();
        if (barcode.Length > 8) barcode = barcode[..8];
        result.Kind = InboundMessageKind.Results;
        result.Barcodes.Add(barcode);
        var measured = f.Length > 11 ? Hl7Parser.ParseTimestamp(f[11].Trim()) : null;
        var reference = f.Length > 7 && f[6].Trim().Length > 0 ? $"{f[6].Trim()}-{f[7].Trim()}" : null;
        result.AddResult(barcode, f[3].Trim(), f[8].Trim(), f[9].Trim(), f.Length > 10 ? f[10].Trim() : null, reference, measured);
    }
}

/// <summary>Siemens Clinitek (CLINTEC): рядки звіту; 5-й — ID, 11-й — Color, 12-й — Clarity, далі трійки code/value/unit.</summary>
public sealed class ClintecParser : TextParserBase
{
    public override string Kind => "TEXT_CLINTEC";

    protected override void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result)
    {
        var lines = Lines(StripControl(raw)).Select(l => l.Trim()).ToList();
        while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
        if (lines.Count < 13) { result.Warnings.Add("Замало рядків CLINTEC"); return; }
        var barcode = lines[4];
        result.Kind = InboundMessageKind.Results;
        result.Barcodes.Add(barcode);
        result.AddResult(barcode, "Color", lines[10]);
        result.AddResult(barcode, "Clarity", lines[11]);
        for (int i = 12; i + 1 < lines.Count && i <= 40; i += 3)
        {
            var code = lines[i]; var value = lines[i + 1];
            var unit = i + 2 < lines.Count ? lines[i + 2] : null;
            if (code.Length > 0 && value.Length > 0) result.AddResult(barcode, code, value, unit);
        }
    }
}

/// <summary>Табличний формат (BioSystems A15): у кожному рядку barcode TAB code TAB … TAB value.</summary>
public sealed class TabularParser : TextParserBase
{
    public override string Kind => "TEXT_TABULAR";

    protected override void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result)
    {
        var lines = Lines(StripControl(raw)).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0) { result.Warnings.Add("Порожнє повідомлення"); return; }
        result.Kind = InboundMessageKind.Results;
        foreach (var l in lines)
        {
            var c = l.Split('\t');
            if (c.Length < 4) continue;
            var barcode = c[0].Trim();
            if (barcode.Length == 0) continue;
            if (!result.Barcodes.Contains(barcode)) result.Barcodes.Add(barcode);
            result.AddResult(barcode, c[1].Trim(), c[3].Trim(), c.Length > 4 ? c[4].Trim() : null);
        }
        if (result.Results.Count == 0) result.Warnings.Add("Рядків із 4+ стовпцями не знайдено");
    }
}

/// <summary>
/// IRIS iQ200: XML у UTF-16LE, закодований hex-рядком, по кадрах STX…ETB/ETX. &lt;IRISPing/&gt; → відлуння (ProtocolReply);
/// &lt;SA ID="barcode"&gt;…&lt;AC&gt;&lt;AR Key="code"&gt;value&lt;/AR&gt;…
/// </summary>
public sealed class IrisParser : TextParserBase
{
    public override string Kind => "TEXT_IRIS";
    public const string PingHex = "3C004900520049005300500069006E0067002F003E000D000A00";

    protected override void ParseCore(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage result)
    {
        if (raw.Contains(PingHex, StringComparison.OrdinalIgnoreCase))
        {
            result.Kind = InboundMessageKind.ProtocolReply;
            // Відлуння кадру пінгу (як у legacy)
            int idx = raw.IndexOf(PingHex, StringComparison.OrdinalIgnoreCase);
            var frameNo = idx > 0 && char.IsDigit(raw[idx - 1]) ? raw[idx - 1].ToString() : "1";
            result.ImmediateReply = frameNo + PingHex;
            return;
        }
        var xml = DecodeHexFrames(raw);
        if (!xml.Contains("<?xml", StringComparison.OrdinalIgnoreCase) && !xml.Contains("<SA", StringComparison.Ordinal))
        {
            result.Warnings.Add("Не XML IRIS");
            return;
        }
        var doc = XDocument.Parse(xml.Trim('\0'));
        var sa = doc.Descendants("SA").FirstOrDefault();
        if (sa == null) { result.Warnings.Add("Елемент SA не знайдено"); return; }
        var barcode = (string?)sa.Attribute("ID") ?? "";
        result.Kind = InboundMessageKind.Results;
        if (barcode.Length > 0) result.Barcodes.Add(barcode);
        var ac = sa.Descendants("AC").FirstOrDefault();
        if (ac == null) { result.Warnings.Add("Елемент AC не знайдено"); return; }
        foreach (var ar in ac.Descendants("AR"))
        {
            var key = (string?)ar.Attribute("Key") ?? "";
            result.AddResult(barcode, key, ar.Value.Trim());
        }
    }

    /// <summary>Декодує hex-кадри (кожні 4 hex-символи = 1 символ UTF-16LE) у текст.</summary>
    public static string DecodeHexFrames(string raw)
    {
        var sb = new StringBuilder();
        var hex = new StringBuilder();
        // Збираємо лише hex-символи між STX(+номер) та ETB/ETX
        int i = 0;
        bool inFrame = !raw.Contains(AstmControl.StxChar);
        while (i < raw.Length)
        {
            char c = raw[i];
            if (c == AstmControl.StxChar) { inFrame = true; i++; if (i < raw.Length && char.IsDigit(raw[i])) i++; continue; }
            if (c == AstmControl.EtbChar || c == AstmControl.EtxChar) { inFrame = false; i++; continue; }
            if (inFrame && Uri.IsHexDigit(c)) hex.Append(c);
            i++;
        }
        var h = hex.ToString();
        for (int p = 0; p + 3 < h.Length; p += 4)
        {
            int lo = Convert.ToInt32(h.Substring(p, 2), 16);
            int hi = Convert.ToInt32(h.Substring(p + 2, 2), 16);
            sb.Append((char)(lo | (hi << 8)));
        }
        return sb.ToString();
    }
}
