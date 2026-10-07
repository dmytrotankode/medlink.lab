// =============================================================================
// MedLink LIS 4.0 — прапорці результату (NORMAL/LOW/HIGH/CRIT_LOW/CRIT_HIGH/ABNORMAL/NONE)
// =============================================================================
namespace MedLink.LIS.Core.Clinical;

public static class ResultFlags
{
    public const string Normal = "NORMAL";
    public const string Low = "LOW";
    public const string High = "HIGH";
    public const string CritLow = "CRIT_LOW";
    public const string CritHigh = "CRIT_HIGH";
    public const string Abnormal = "ABNORMAL";
    public const string None = "NONE";

    public static bool IsCritical(string? flag) => flag == CritLow || flag == CritHigh;
    public static bool IsAbnormal(string? flag) => flag is Low or High or CritLow or CritHigh or Abnormal;
}

public static class ResultFlagger
{
    /// <summary>
    /// v&lt;=critLow → CRIT_LOW; v&gt;=critHigh → CRIT_HIGH; v&lt;normLow → LOW; v&gt;normHigh → HIGH; інакше NORMAL.
    /// Якщо жодної межі немає — NONE.
    /// </summary>
    public static string Flag(double value, double? normLow, double? normHigh, double? critLow, double? critHigh)
    {
        if (!normLow.HasValue && !normHigh.HasValue && !critLow.HasValue && !critHigh.HasValue)
            return ResultFlags.None;
        if (critLow.HasValue && value <= critLow.Value) return ResultFlags.CritLow;
        if (critHigh.HasValue && value >= critHigh.Value) return ResultFlags.CritHigh;
        if (normLow.HasValue && value < normLow.Value) return ResultFlags.Low;
        if (normHigh.HasValue && value > normHigh.Value) return ResultFlags.High;
        return ResultFlags.Normal;
    }

    /// <summary>Текстовий результат: збіг із normText → NORMAL, інакше ABNORMAL; без normText → NONE.</summary>
    public static string FlagText(string? value, string? normText)
    {
        if (string.IsNullOrWhiteSpace(normText)) return ResultFlags.None;
        if (string.IsNullOrWhiteSpace(value)) return ResultFlags.None;
        var norm = Normalize(normText);
        var val = Normalize(value);
        // normText може містити кілька допустимих варіантів через «;» або «/»
        var options = norm.Split(new[] { ';', '/', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim());
        return options.Any(o => o == val) ? ResultFlags.Normal : ResultFlags.Abnormal;
    }

    private static string Normalize(string s) => s.Trim().ToLowerInvariant().Replace('ё', 'е');
}
