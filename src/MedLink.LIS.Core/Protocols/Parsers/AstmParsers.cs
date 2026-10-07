// =============================================================================
// MedLink LIS 4.0 — парсери повідомлень ASTM E1394 за сімействами приладів.
// Порт гілок parse_lab_message (Simplex MySQL) із збереженням «особливостей»
// кожного приладу (Cobas: ^-обробка штрихкоду та обрізання «!», коди з «/»;
// Sysmex: Q|1|^^…^barcode; ABL: P|1||||barcode тощо).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Globalization;
using System.Text.RegularExpressions;
using MedLink.LIS.Core.Contracts;

namespace MedLink.LIS.Core.Protocols.Parsers;

/// <summary>Спільні допоміжні функції розбору ASTM.</summary>
public static class AstmParseHelpers
{
    private static readonly Regex DigitsOnly = new(@"^\d+$", RegexOptions.Compiled);

    /// <summary>
    /// Витягує штрихкод із токена поля Q-3 / O-3 (напр. "1^12345678^S1^SC", "^^   1^1026004819^B", "^1026004819", "12345678").
    /// Повертає штрихкод, частину токена ПІСЛЯ штрихкоду (напр. "S1^SC" — позиція/тип проби для Cobas) та частину ДО.
    /// </summary>
    public static (string Barcode, string Tail, string Head) SplitBarcodeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return ("", "", "");
        var t = token.Trim();
        // Перший повтор, якщо є
        int rep = t.IndexOf('\\');
        if (rep >= 0) t = t[..rep];
        var parts = t.Split('^');
        if (parts.Length == 1) return (CleanBarcode(parts[0]), "", "");
        int best = -1; int bestScore = int.MinValue;
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i].Trim().Trim('!');
            if (p.Length == 0) continue;
            int score = ScoreBarcodeCandidate(p);
            if (score > bestScore) { bestScore = score; best = i; }
        }
        if (best < 0) return ("", "", "");
        var tail = string.Join("^", parts.Skip(best + 1).Select(s => s.Trim()));
        var head = string.Join("^", parts.Take(best).Select(s => s.Trim()));
        return (CleanBarcode(parts[best]), tail, head);
    }

    private static int ScoreBarcodeCandidate(string p)
    {
        // Типи проб / контейнерів Roche (S1, S2, SC, S0), атрибути Sysmex (B, M), короткі числа (позиції) — низький пріоритет
        if (Regex.IsMatch(p, @"^S[0-9C]$", RegexOptions.IgnoreCase)) return -10;
        if (p.Length == 1) return -5;
        int score = p.Length;
        if (DigitsOnly.IsMatch(p)) score += 20;
        else if (p.All(char.IsLetterOrDigit)) score += 5;
        if (p.Length < 4) score -= 10;
        return score;
    }

    /// <summary>Чистить штрихкод: пробіли, «!», лапки.</summary>
    public static string CleanBarcode(string? s) => (s ?? "").Trim().Trim('!', '"', '\'', ' ');

    /// <summary>Перший непорожній компонент поля (розділювач ^).</summary>
    public static string FirstNonEmptyComponent(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        foreach (var c in field.Split('\\')[0].Split('^'))
            if (!string.IsNullOrWhiteSpace(c)) return c.Trim();
        return "";
    }

    /// <summary>Код тесту із поля R-3 за замовчуванням: перший непорожній компонент, без «!», «|», пробілів.</summary>
    public static string DefaultCode(string field) => FirstNonEmptyComponent(field).Trim('!', '|', ' ');

    /// <summary>Чи виглядає рядок як штрихкод (≥ 4 символи, літери/цифри).</summary>
    public static bool LooksLikeBarcode(string? s)
        => !string.IsNullOrWhiteSpace(s) && s.Trim().Length >= 4 && s.Trim().All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');

    /// <summary>Лише цифри з рядка.</summary>
    public static string DigitsOf(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());

    /// <summary>Розбір дати/часу ASTM (yyyyMMddHHmmss / yyyyMMdd).</summary>
    public static DateTime? ParseTimestamp(string? ts) => Hl7Parser.ParseTimestamp(ts);

    /// <summary>Дата народження з поля P-8 (yyyyMMdd).</summary>
    public static DateTime? ParseBirthDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var d = DigitsOf(s);
        if (d.Length >= 8 && DateTime.TryParseExact(d[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return dt;
        return null;
    }

    /// <summary>Нормалізація статі: M/F/U.</summary>
    public static string? NormalizeGender(string? g)
    {
        if (string.IsNullOrWhiteSpace(g)) return null;
        var c = char.ToUpperInvariant(g.Trim()[0]);
        return c switch { 'M' or 'Ч' or '1' => "M", 'F' or 'W' or 'Ж' or '2' => "F", _ => "U" };
    }

    /// <summary>Пацієнт із запису P (поля 4..9).</summary>
    public static InboundPatientInfo? PatientFromP(AstmRecord? p)
    {
        if (p == null) return null;
        var name = p.Components(6);
        var info = new InboundPatientInfo
        {
            Id = FirstNonEmpty(p.Field(4), p.Field(3), p.Field(5)),
            LastName = name.Length > 0 ? name[0].Trim() : null,
            FirstName = name.Length > 1 ? name[1].Trim() : null,
            MiddleName = name.Length > 2 ? name[2].Trim() : null,
            BirthDate = ParseBirthDate(p.Field(8)),
            Gender = NormalizeGender(p.Field(9)),
        };
        if (string.IsNullOrEmpty(info.Id) && string.IsNullOrEmpty(info.LastName) && info.BirthDate == null) return null;
        return info;
    }

    public static string FirstNonEmpty(params string[] values)
    {
        foreach (var v in values) if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        return "";
    }

    /// <summary>Чи є проба контрольною (O-12 Action code = Q або O-16 містить QC/CONTROL).</summary>
    public static bool IsQcOrder(AstmRecord? o)
    {
        if (o == null) return false;
        var action = o.Field(12).Trim().ToUpperInvariant();
        if (action == "Q") return true;
        var spec = o.Field(16).ToUpperInvariant();
        return spec.Contains("QC") || spec.Contains("CONTROL");
    }
}

/// <summary>
/// Базовий парсер ASTM: визначає вид (Query / Results / Other), збирає штрихкоди Q-записів,
/// групує результати за O-записами; підкласи змінюють витягування штрихкоду/коду/значення.
/// Якщо текст виглядає як HL7 (MSH|) — делегує HL7_ORU-парсеру.
/// </summary>
public class AstmGenericParser : IAnalyzerMessageParser
{
    public virtual string Kind => "ASTM_GENERIC";

    public AnalyzerInboundMessage Parse(AnalyzerConfigDto cfg, string raw)
    {
        raw ??= "";
        try
        {
            if (Hl7Parser.LooksLikeHl7(raw)) return Hl7Fallback(cfg, raw);
            var trimmed = raw.Trim('\0', ' ', '\r', '\n');
            if (trimmed.Length == 1 && trimmed[0] == AstmControl.AckChar)
                return new AnalyzerInboundMessage { Kind = InboundMessageKind.Ack, Raw = raw };
            var msg = AstmParser.Parse(raw);
            var result = new AnalyzerInboundMessage { Raw = msg.Records.Count > 0 ? string.Join("\r", msg.Records.Select(r => r.RawText)) : raw };
            if (msg.Records.Count == 0) { result.Warnings.Add("Порожнє повідомлення"); return result; }
            return ParseCore(cfg, msg, result);
        }
        catch (Exception ex)
        {
            return AnalyzerInboundMessage.Other(raw, "Помилка розбору ASTM: " + ex.Message);
        }
    }

    protected virtual AnalyzerInboundMessage Hl7Fallback(AnalyzerConfigDto cfg, string raw) => new Hl7OruParser().Parse(cfg, raw);

    protected virtual AnalyzerInboundMessage ParseCore(AnalyzerConfigDto cfg, AstmMessage msg, AnalyzerInboundMessage result)
    {
        var queries = msg.OfType("Q").ToList();
        if (queries.Count > 0)
        {
            result.Kind = InboundMessageKind.Query;
            bool first = true;
            foreach (var q in queries)
            {
                var token = QueryToken(q);
                var (barcode, tail, _) = AstmParseHelpers.SplitBarcodeToken(token);
                barcode = CleanQueryBarcode(barcode, q);
                if (first)
                {
                    result.RawQueryToken = token;
                    result.SamplePosition = string.IsNullOrEmpty(tail) ? null : tail;
                    first = false;
                }
                if (barcode.Length > 0 && !result.Barcodes.Contains(barcode)) result.Barcodes.Add(barcode);
            }
            if (result.Barcodes.Count == 0) result.Warnings.Add("Запит без штрихкоду");
            return result;
        }

        if (msg.Has("R"))
        {
            result.Kind = InboundMessageKind.Results;
            result.Patient = AstmParseHelpers.PatientFromP(msg.First("P"));
            ParseResults(cfg, msg, result);
            if (result.Results.Count == 0) result.Warnings.Add("R-записи є, але результати не розпізнано");
            return result;
        }

        result.Kind = InboundMessageKind.Other;
        if (msg.Has("O") && !msg.Has("R")) result.Warnings.Add("Повідомлення містить замовлення без результатів");
        return result;
    }

    /// <summary>Токен штрихкоду із запису Q (поле 3 за замовчуванням).</summary>
    protected virtual string QueryToken(AstmRecord q) => q.Field(3);

    /// <summary>Додаткове очищення штрихкоду запиту.</summary>
    protected virtual string CleanQueryBarcode(string barcode, AstmRecord q) => barcode;

    /// <summary>Розбір результатів: за блоками O (кожен O → свій штрихкод); P-блоки — для fallback.</summary>
    protected virtual void ParseResults(AnalyzerConfigDto cfg, AstmMessage msg, AnalyzerInboundMessage result)
    {
        // Формуємо послідовність: поточний P, поточний O, R
        AstmRecord? currentP = null; AstmRecord? currentO = null;
        string barcode = "";
        foreach (var r in msg.Records)
        {
            switch (r.Type)
            {
                case "P": currentP = r; currentO = null; barcode = ""; break;
                case "O": currentO = r; barcode = OrderBarcode(r, currentP, msg); if (AstmParseHelpers.IsQcOrder(r)) result.IsQc = true; break;
                case "R":
                    if (barcode.Length == 0) barcode = OrderBarcode(currentO, currentP, msg);
                    if (barcode.Length > 0 && !result.Barcodes.Contains(barcode)) result.Barcodes.Add(barcode);
                    AddResult(r, barcode, currentO, result);
                    break;
            }
        }
    }

    /// <summary>Штрихкод для блоку результатів: O-3 → O-4 → P-4/5/3 (лише цифри, якщо коротко).</summary>
    protected virtual string OrderBarcode(AstmRecord? o, AstmRecord? p, AstmMessage msg)
    {
        if (o != null)
        {
            var (b, _, _) = AstmParseHelpers.SplitBarcodeToken(o.Field(3));
            if (AstmParseHelpers.LooksLikeBarcode(b)) return b;
            (b, _, _) = AstmParseHelpers.SplitBarcodeToken(o.Field(4));
            if (AstmParseHelpers.LooksLikeBarcode(b)) return b;
            if (b.Length > 0) return b;
        }
        return PatientBarcodeFallback(p);
    }

    protected static string PatientBarcodeFallback(AstmRecord? p)
    {
        if (p == null) return "";
        foreach (var f in new[] { 4, 5, 3, 6 })
        {
            var (b, _, _) = AstmParseHelpers.SplitBarcodeToken(p.Field(f));
            if (AstmParseHelpers.LooksLikeBarcode(b)) return b;
            var digits = AstmParseHelpers.DigitsOf(b);
            if (digits.Length >= 4) return digits;
        }
        return "";
    }

    protected virtual void AddResult(AstmRecord r, string barcode, AstmRecord? o, AnalyzerInboundMessage result)
    {
        var code = Code(r);
        if (code.Length == 0) return;
        var value = Value(r);
        var unit = Unit(r);
        var flags = Flags(r);
        var reference = r.Field(6).Trim();
        var measuredAt = AstmParseHelpers.ParseTimestamp(AstmParseHelpers.FirstNonEmpty(r.Field(13), r.Field(12)));
        result.AddResult(barcode, code, value, unit, flags, reference, measuredAt);
    }

    protected virtual string Code(AstmRecord r) => AstmParseHelpers.DefaultCode(r.Field(3));
    protected virtual string Value(AstmRecord r) => r.Field(4).Trim();
    protected virtual string Unit(AstmRecord r) => r.Field(5).Trim();
    protected virtual string Flags(AstmRecord r) => r.Field(7).Trim();
}

/// <summary>
/// Roche Cobas c111/c311/e411 (+ Prestige/BiOLiS, Beckman, DocuReader): штрихкод у O-3 вигляду
/// "seq^barcode^S1^SC", обрізання «!», код із «/» обрізається (напр. "GLU/2" → "GLU"), значення до «!».
/// </summary>
public class AstmCobasParser : AstmGenericParser
{
    public override string Kind => "ASTM_COBAS";

    protected override string CleanQueryBarcode(string barcode, AstmRecord q) => barcode.Trim('^', '!', ' ');

    protected override string Code(AstmRecord r)
    {
        var code = AstmParseHelpers.FirstNonEmptyComponent(r.Field(3));
        int slash = code.IndexOf('/');
        if (slash > 0) code = code[..slash];
        return code.Replace("^", "").Replace("/", "").Trim('|', '!', ' ');
    }

    protected override string Value(AstmRecord r)
    {
        var v = r.Field(4).Trim();
        int bang = v.IndexOf('!');
        if (bang >= 0) v = v[..bang];
        return v.Split('^')[0].Trim();
    }
}

/// <summary>
/// Sysmex XN/XS/XT/2000, CS-2500, CA-600: штрихкод у Q-3/O-3 як "^^  rack^pos^barcode^B" (компонент зі штрихкодом);
/// код — перший компонент після провідних ^ (напр. "^^^^WBC^1" → WBC); fallback — цифри з P-4.
/// </summary>
public class AstmSysmexParser : AstmGenericParser
{
    public override string Kind => "ASTM_SYSMEX";

    protected override string OrderBarcode(AstmRecord? o, AstmRecord? p, AstmMessage msg)
    {
        var b = base.OrderBarcode(o, p, msg);
        // legacy: LOCATE('P|1|||')+6 → поле P-5, лише цифри
        if (b.Length < 4 && p != null) b = AstmParseHelpers.DigitsOf(AstmParseHelpers.FirstNonEmpty(p.Field(5), p.Field(4), p.Field(3)));
        return b;
    }

    protected override string Value(AstmRecord r)
    {
        var v = r.Field(4).Trim();
        return v.Length > 64 ? v[..64] : v;
    }
}

/// <summary>Horiba ABX Pentra: як generic (Q-3 обрізання ^, код — перший компонент).</summary>
public sealed class AstmPentraParser : AstmGenericParser
{
    public override string Kind => "ASTM_PENTRA";
}

/// <summary>Radiometer ABL80/ABL9, AQT90: штрихкод береться з P-запису (P|1||||barcode або P|1||barcode), код без ^.</summary>
public sealed class AstmAblParser : AstmGenericParser
{
    public override string Kind => "ASTM_ABL";

    protected override string OrderBarcode(AstmRecord? o, AstmRecord? p, AstmMessage msg)
    {
        if (p != null)
        {
            foreach (var f in new[] { 6, 4, 5, 3 })
            {
                var (b, _, _) = AstmParseHelpers.SplitBarcodeToken(p.Field(f));
                if (AstmParseHelpers.LooksLikeBarcode(b)) return b;
            }
        }
        return base.OrderBarcode(o, p, msg);
    }

    protected override string Code(AstmRecord r) => r.Field(3).Trim().Trim('^').Split('^')[0].Trim();
}

/// <summary>Diagnostica Stago: Q-3 обрізання ^; O-3 — до ^; код — перший компонент.</summary>
public sealed class AstmStagoParser : AstmGenericParser
{
    public override string Kind => "ASTM_STAGO";
}

/// <summary>Tosoh AIA: Q-3 без ^; результати як generic.</summary>
public sealed class AstmTosohParser : AstmGenericParser
{
    public override string Kind => "ASTM_TOSOH";
    protected override string CleanQueryBarcode(string barcode, AstmRecord q) => barcode.Replace("^", "");
}

/// <summary>Snibe Maglumi: код — поле R-3 без провідних/кінцевих ^.</summary>
public sealed class AstmMaglumiParser : AstmGenericParser
{
    public override string Kind => "ASTM_MAGLUMI";
    protected override string Code(AstmRecord r) => r.Field(3).Trim().Trim('^').Split('^')[0].Trim();
}

/// <summary>Bio-Ksel 6000: кілька P-блоків; R|n|000x → код "n-000x" (номер запису-канал + код методики).</summary>
public sealed class AstmBioKselParser : AstmGenericParser
{
    public override string Kind => "ASTM_BIOKSEL";

    protected override void ParseResults(AnalyzerConfigDto cfg, AstmMessage msg, AnalyzerInboundMessage result)
    {
        AstmRecord? currentP = null; string barcode = "";
        foreach (var r in msg.Records)
        {
            switch (r.Type)
            {
                case "P": currentP = r; barcode = ""; break;
                case "O": barcode = OrderBarcode(r, currentP, msg); break;
                case "R":
                    if (barcode.Length == 0) barcode = PatientBarcodeFallback(currentP);
                    if (barcode.Length > 0 && !result.Barcodes.Contains(barcode)) result.Barcodes.Add(barcode);
                    var code = $"{r.Field(2).Trim()}-{AstmParseHelpers.DefaultCode(r.Field(3))}";
                    result.AddResult(barcode, code, r.Field(4), r.Field(5), r.Field(7), r.Field(6), AstmParseHelpers.ParseTimestamp(r.Field(13)));
                    break;
            }
        }
    }
}

/// <summary>Roche Urisys 1100 / Cobas u411: R|n|01^SG|value… — код — другий компонент (SG, pH, LEU…), значення до ^.</summary>
public sealed class AstmUrisysParser : AstmGenericParser
{
    public override string Kind => "ASTM_URISYS";

    protected override string Code(AstmRecord r)
    {
        var comps = r.Components(3);
        // "01^SG" → SG; "^^^SG" → SG
        for (int i = comps.Length - 1; i >= 0; i--)
        {
            var c = comps[i].Trim();
            if (c.Length > 0 && !c.All(char.IsDigit)) return c;
        }
        return AstmParseHelpers.DefaultCode(r.Field(3));
    }

    protected override string Value(AstmRecord r) => r.Field(4).Split('^')[0].Trim();
}

/// <summary>Mindray BS-серії (ASTM): Q може мати кілька записів; код — компонент між ^ (R|1|^Glucose^^F); значення без ^.</summary>
public sealed class AstmMindrayParser : AstmGenericParser
{
    public override string Kind => "ASTM_MINDRAY";

    protected override string OrderBarcode(AstmRecord? o, AstmRecord? p, AstmMessage msg)
    {
        if (o != null)
        {
            // MINDRAY: O|1|barcode|…; MINDRAY240 ASTM: O|1|rack^pos|barcode|…
            foreach (var f in new[] { 3, 4 })
            {
                var (b, _, _) = AstmParseHelpers.SplitBarcodeToken(o.Field(f));
                if (AstmParseHelpers.LooksLikeBarcode(b)) return b;
            }
        }
        return base.OrderBarcode(o, p, msg);
    }

    protected override string Code(AstmRecord r)
    {
        var comps = r.Components(3);
        // "^Glucose (GOD-POD Method)^^F" → Glucose…; "368^TSH_1^0^F" → перший непорожній
        if (comps.Length > 1 && string.IsNullOrWhiteSpace(comps[0]) && !string.IsNullOrWhiteSpace(comps[1])) return comps[1].Trim();
        return AstmParseHelpers.DefaultCode(r.Field(3));
    }

    protected override string Value(AstmRecord r) => r.Field(4).Trim().Trim('^').Split('^')[0].Trim();
}

/// <summary>Human HumaStar 100/300: пакетні результати P|n||barcode; R|1|code|…|value (значення у полі 8).</summary>
public sealed class AstmHumaStarParser : AstmGenericParser
{
    public override string Kind => "ASTM_HUMASTAR";

    protected override void ParseResults(AnalyzerConfigDto cfg, AstmMessage msg, AnalyzerInboundMessage result)
    {
        string barcode = "";
        foreach (var r in msg.Records)
        {
            switch (r.Type)
            {
                case "P":
                    barcode = AstmParseHelpers.CleanBarcode(AstmParseHelpers.FirstNonEmpty(r.Field(4), r.Field(3), r.Field(5)));
                    break;
                case "O":
                    if (barcode.Length == 0) barcode = AstmParseHelpers.SplitBarcodeToken(r.Field(3)).Barcode;
                    break;
                case "R":
                    if (barcode.Length > 0 && !result.Barcodes.Contains(barcode)) result.Barcodes.Add(barcode);
                    var code = r.Field(3).Trim();
                    var value = AstmParseHelpers.FirstNonEmpty(r.Field(8), r.Field(4));
                    result.AddResult(barcode, code, value, r.Field(5), r.Field(7), r.Field(6), AstmParseHelpers.ParseTimestamp(r.Field(13)));
                    break;
            }
        }
    }
}

/// <summary>Ortho Vitros: O-3 "barcode^3^1" → до ^; код "^^^1.0000+314+1.0" → символи 11..13 ("314").</summary>
public sealed class AstmVitrosParser : AstmGenericParser
{
    public override string Kind => "ASTM_VITROS";

    protected override string OrderBarcode(AstmRecord? o, AstmRecord? p, AstmMessage msg)
    {
        if (o != null)
        {
            var first = o.Field(3).Split('^')[0].Trim();
            if (first.Length > 0) return first;
        }
        return base.OrderBarcode(o, p, msg);
    }

    protected override string Code(AstmRecord r)
    {
        var f = r.Field(3);
        if (f.Contains('+'))
        {
            // "^^^1.0000+314+1.0": після першого '+' до наступного '+'
            var afterPlus = f[(f.IndexOf('+') + 1)..];
            int next = afterPlus.IndexOf('+');
            return (next > 0 ? afterPlus[..next] : afterPlus).Trim();
        }
        return AstmParseHelpers.DefaultCode(f);
    }
}

/// <summary>YHLO iFlash: O-4 — штрихкод; код — "368^TSH_1" (перші два компоненти); значення до ^.</summary>
public sealed class AstmIflashParser : AstmGenericParser
{
    public override string Kind => "ASTM_IFLASH";

    protected override string OrderBarcode(AstmRecord? o, AstmRecord? p, AstmMessage msg)
    {
        if (o != null)
        {
            var b = AstmParseHelpers.CleanBarcode(o.Field(4));
            if (b.Length > 0) return b;
        }
        return base.OrderBarcode(o, p, msg);
    }

    protected override string Code(AstmRecord r)
    {
        var comps = r.Components(3);
        return comps.Length >= 2 ? $"{comps[0].Trim()}^{comps[1].Trim()}" : AstmParseHelpers.DefaultCode(r.Field(3));
    }

    protected override string Value(AstmRecord r) => r.Field(4).Split('^')[0].Trim();
}

/// <summary>Nihon Kohden MEK-7300: штрихкод — перші 8 символів P-4; код — 4-й компонент R-3 ("^^2A01…^WBC^JC10" → WBC).</summary>
public sealed class AstmMek7300Parser : AstmGenericParser
{
    public override string Kind => "ASTM_MEK7300";

    protected override string OrderBarcode(AstmRecord? o, AstmRecord? p, AstmMessage msg)
    {
        if (p != null)
        {
            // legacy: LOCATE('P|1|||')+6 → поле P-5 ("10116418 0001"), перші 8 символів
            var f = AstmParseHelpers.FirstNonEmpty(p.Field(5), p.Field(4), p.Field(3));
            if (f.Length >= 8) f = f[..8];
            f = f.Split('^')[0].Trim();
            if (f.Length > 0) return f;
        }
        return base.OrderBarcode(o, p, msg);
    }

    protected override string Code(AstmRecord r)
    {
        var comps = r.Components(3);
        if (comps.Length >= 4) return comps[3].Trim();
        return AstmParseHelpers.DefaultCode(r.Field(3));
    }
}

/// <summary>Werfen ACL TOP: код — до 6 символів без ^ та /; одиниці — R-5.</summary>
public sealed class AstmAclTopParser : AstmGenericParser
{
    public override string Kind => "ASTM_ACLTOP";

    protected override string Code(AstmRecord r)
    {
        var code = AstmParseHelpers.DefaultCode(r.Field(3));
        int slash = code.IndexOf('/');
        if (slash > 0) code = code[..slash];
        return code;
    }
}

/// <summary>Урінальна мікроскопія UWAM (UriSed-подібна): значення до ^, одиниця /µl → «HPF/поле зр.».</summary>
public sealed class AstmUwamParser : AstmGenericParser
{
    public override string Kind => "ASTM_UWAM";
    protected override string Value(AstmRecord r) { var v = r.Field(4).Split('^')[0].Trim(); return v.Length > 64 ? v[..64] : v; }
    protected override string Unit(AstmRecord r)
    {
        var u = r.Field(5).Trim();
        if (u.Length > 16) u = u[..16];
        return u == "/µl" ? "HPF/поле зр." : u;
    }
}

/// <summary>Phadia 250: кілька O-блоків; код — перші два компоненти після провідних ^ ("tIgE^Total IgE"); значення до ^ та «!».</summary>
public sealed class AstmPhadiaParser : AstmGenericParser
{
    public override string Kind => "ASTM_PHADIA";

    protected override string OrderBarcode(AstmRecord? o, AstmRecord? p, AstmMessage msg)
    {
        if (o != null)
        {
            var first = o.Field(3).Split('^')[0].Trim().Trim('!');
            if (first.Length > 0) return first;
        }
        return base.OrderBarcode(o, p, msg);
    }

    protected override string Code(AstmRecord r)
    {
        var f = r.Field(3).Trim().TrimStart('^').Trim('!');
        var comps = f.Split('^');
        return comps.Length >= 2 ? $"{comps[0].Trim()}^{comps[1].Trim()}" : comps[0].Trim();
    }

    protected override string Value(AstmRecord r)
    {
        var v = r.Field(4).Trim();
        int bang = v.IndexOf('!');
        if (bang >= 0) v = v[..bang];
        return v.Split('^')[0].Trim();
    }
}

/// <summary>Erba XL: штрихкод із P-3; код — до 6 символів без ^ і /.</summary>
public sealed class AstmErbaParser : AstmGenericParser
{
    public override string Kind => "ASTM_ERBA";

    protected override string OrderBarcode(AstmRecord? o, AstmRecord? p, AstmMessage msg)
    {
        if (p != null)
        {
            var b = AstmParseHelpers.CleanBarcode(p.Field(3));
            if (AstmParseHelpers.LooksLikeBarcode(b)) return b;
        }
        return base.OrderBarcode(o, p, msg);
    }

    protected override string Code(AstmRecord r)
    {
        var code = AstmParseHelpers.DefaultCode(r.Field(3));
        int slash = code.IndexOf('/');
        if (slash > 0) code = code[..slash];
        return code;
    }
}
