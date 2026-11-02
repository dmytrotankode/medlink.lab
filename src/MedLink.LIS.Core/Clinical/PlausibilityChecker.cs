// =============================================================================
// MedLink LIS — контроль правдоподобності результату (ТЗ 23.7, FR-RPT-017/018, S-60): фізіологічні межі показника
// і співвідношення гематологічних показників (MCH = HGB/RBC, MCHC = HGB/HCT). Неправдоподібне значення блокує автоверифікацію.
// =============================================================================
namespace MedLink.LIS.Core.Clinical;

public sealed class PlausibilityResult
{
    public bool IsPlausible => Reasons.Count == 0;
    public List<string> Reasons { get; } = new();
    public string Summary => string.Join("; ", Reasons);
}

public static class PlausibilityChecker
{
    /// <summary>Фізіологічні межі (поза ними значення несумісне з життям або є помилкою одиниць/введення), одиниці довідника ЛІС.</summary>
    public static readonly IReadOnlyDictionary<string, (double Min, double Max, string Unit)> Limits = new Dictionary<string, (double, double, string)>(StringComparer.OrdinalIgnoreCase)
    {
        ["HGB"] = (20, 260, "г/л"), ["RBC"] = (0.5, 10, "10^12/л"), ["HCT"] = (5, 80, "%"), ["WBC"] = (0, 600, "10^9/л"), ["PLT"] = (0, 3000, "10^9/л"),
        ["MCV"] = (40, 160, "фл"), ["MCH"] = (10, 60, "пг"), ["MCHC"] = (200, 450, "г/л"), ["K"] = (1, 10, "ммоль/л"), ["NA"] = (90, 200, "ммоль/л"),
        ["CL"] = (50, 160, "ммоль/л"), ["GLU"] = (0.3, 80, "ммоль/л"), ["PH"] = (6.5, 8.0, ""), ["PROT_TOT"] = (20, 150, "г/л"), ["ALB"] = (5, 70, "г/л"),
        ["CREAT"] = (5, 3000, "мкмоль/л"), ["UREA"] = (0.3, 150, "ммоль/л"), ["ICA"] = (0.2, 3, "ммоль/л"), ["INR"] = (0.5, 15, ""), ["FIBRINOGEN"] = (0.1, 15, "г/л")
    };

    /// <summary>Перевіряє значення показника з урахуванням інших результатів тієї ж проби/замовлення (code → value).</summary>
    public static PlausibilityResult Check(string testCode, double value, IReadOnlyDictionary<string, double> siblings)
    {
        var r = new PlausibilityResult();
        if (Limits.TryGetValue(testCode, out var lim) && (value < lim.Min || value > lim.Max))
            r.Reasons.Add($"Неправдоподібне значення {testCode} {Fmt(value)} {lim.Unit} (фізіологічні межі {Fmt(lim.Min)}–{Fmt(lim.Max)}) — перевірте одиниці та введення");
        var all = new Dictionary<string, double>(siblings, StringComparer.OrdinalIgnoreCase) { [testCode] = value };
        if (all.TryGetValue("HGB", out var hgb) && all.TryGetValue("RBC", out var rbc) && rbc > 0 && (testCode is "HGB" or "RBC"))
        {
            var mch = hgb / rbc;
            if (mch < 10 || mch > 60) r.Reasons.Add($"Неправдоподібне співвідношення HGB/RBC (MCH {Fmt(mch)} пг; можлива перестановка значень)");
        }
        if (all.TryGetValue("HGB", out hgb) && all.TryGetValue("HCT", out var hct) && hct > 0 && (testCode is "HGB" or "HCT"))
        {
            var mchc = hgb / hct * 100;
            if (mchc < 200 || mchc > 450) r.Reasons.Add($"Неправдоподібне співвідношення HGB/HCT (MCHC {Fmt(mchc)} г/л)");
        }
        return r;
    }

    private static string Fmt(double v) => v.ToString(Math.Abs(v) >= 100 ? "0" : "0.##", System.Globalization.CultureInfo.GetCultureInfo("uk-UA"));
}
