// =============================================================================
// MedLink LIS 4.0 — правила Вестгарда для карт Леві-Дженнінгса
// =============================================================================
namespace MedLink.LIS.Core.Clinical;

public static class WestgardRules
{
    public const string R12s = "1_2s";
    public const string R13s = "1_3s";
    public const string R22s = "2_2s";
    public const string R4s = "R_4s";
    public const string R41s = "4_1s";
    public const string R10x = "10_x";

    public static readonly string[] RejectionRules = { R13s, R22s, R4s, R41s, R10x };
    public static bool IsRejection(string rule) => RejectionRules.Contains(rule);
}

public sealed class WestgardResult
{
    public double ZScore { get; init; }
    public List<string> ViolatedRules { get; init; } = new();
    public bool IsWarning { get; init; }
    public bool IsRejection { get; init; }
    /// <summary>OK | WARNING | REJECTION</summary>
    public string Status => IsRejection ? "REJECTION" : IsWarning ? "WARNING" : "OK";
}

public static class WestgardEvaluator
{
    /// <summary>
    /// Оцінює останню точку серії. <paramref name="series"/> — хронологічно впорядковані значення
    /// одного матеріалу+тесту (вікно до 10 точок), останній елемент — поточний результат.
    /// z = (x − mean) / sd.
    /// </summary>
    public static WestgardResult Evaluate(IReadOnlyList<double> series, double mean, double sd)
    {
        if (series == null || series.Count == 0) throw new ArgumentException("Серія порожня", nameof(series));
        if (sd <= 0) throw new ArgumentException("SD має бути > 0", nameof(sd));

        var z = series.Select(v => (v - mean) / sd).ToList();
        var window = z.Count > 10 ? z.Skip(z.Count - 10).ToList() : z;
        var cur = window[^1];
        var rules = new List<string>();

        if (Math.Abs(cur) > 2) rules.Add(WestgardRules.R12s);
        if (Math.Abs(cur) > 3) rules.Add(WestgardRules.R13s);

        if (window.Count >= 2)
        {
            var prev = window[^2];
            if ((cur > 2 && prev > 2) || (cur < -2 && prev < -2)) rules.Add(WestgardRules.R22s);
            if (Math.Abs(cur - prev) > 4) rules.Add(WestgardRules.R4s);
        }
        if (window.Count >= 4)
        {
            var last4 = window.Skip(window.Count - 4).ToList();
            if (last4.All(v => v > 1) || last4.All(v => v < -1)) rules.Add(WestgardRules.R41s);
        }
        if (window.Count >= 10)
        {
            var last10 = window.Skip(window.Count - 10).ToList();
            if (last10.All(v => v > 0) || last10.All(v => v < 0)) rules.Add(WestgardRules.R10x);
        }

        var rejection = rules.Any(WestgardRules.IsRejection);
        return new WestgardResult
        {
            ZScore = Math.Round(cur, 3),
            ViolatedRules = rules,
            IsWarning = rules.Contains(WestgardRules.R12s) && !rejection,
            IsRejection = rejection
        };
    }

    /// <summary>Пояснення правила українською (для протоколу розблокування).</summary>
    public static string Describe(string rule) => rule switch
    {
        WestgardRules.R12s => "1(2s): одне значення за межами ±2SD — попередження",
        WestgardRules.R13s => "1(3s): одне значення за межами ±3SD — випадкова помилка",
        WestgardRules.R22s => "2(2s): два поспіль значення за межами ±2SD з одного боку — систематична помилка",
        WestgardRules.R4s => "R(4s): різниця двох поспіль значень перевищує 4SD — випадкова помилка",
        WestgardRules.R41s => "4(1s): чотири поспіль значення за межами ±1SD з одного боку — зсув",
        WestgardRules.R10x => "10(x): десять поспіль значень по один бік від середнього — зсув середнього",
        _ => rule
    };
}
