// =============================================================================
// MedLink LIS 4.0 — reflex-правила (TSH>4→FT4, PSA>4→FPSA, GLU>11→HBA1C …)
// =============================================================================
namespace MedLink.LIS.Core.Clinical;

public sealed class ReflexRuleInput
{
    public string Id { get; init; } = "";
    public string TriggerTestCode { get; init; } = "";
    /// <summary>&lt;, &lt;=, &gt;, &gt;=, ==, OUT_OF_RANGE, CRITICAL</summary>
    public string ConditionOperator { get; init; } = ">";
    public double? ThresholdValue { get; init; }
    public string ReflexTestCode { get; init; } = "";
    public bool AutoApprove { get; init; }
    public bool RequiresSameSample { get; init; } = true;
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class ReflexTrigger
{
    public ReflexRuleInput Rule { get; init; } = new();
    public string Reason { get; init; } = "";
}

public static class ReflexRuleEngine
{
    /// <summary>
    /// Повертає правила, що спрацювали для результату тесту <paramref name="testCode"/>.
    /// Тести, що вже є у замовленні (<paramref name="existingTestCodes"/>), не дублюються.
    /// </summary>
    public static List<ReflexTrigger> Evaluate(IEnumerable<ReflexRuleInput> rules, string testCode, double? value, string flag,
        IEnumerable<string>? existingTestCodes = null)
    {
        var existing = new HashSet<string>(existingTestCodes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var triggered = new List<ReflexTrigger>();
        foreach (var rule in rules.Where(r => r.IsActive && string.Equals(r.TriggerTestCode, testCode, StringComparison.OrdinalIgnoreCase)))
        {
            if (existing.Contains(rule.ReflexTestCode)) continue;
            if (Matches(rule, value, flag, out var reason))
                triggered.Add(new ReflexTrigger { Rule = rule, Reason = reason });
        }
        return triggered;
    }

    public static bool Matches(ReflexRuleInput rule, double? value, string flag, out string reason)
    {
        reason = "";
        var op = (rule.ConditionOperator ?? "").Trim().ToUpperInvariant();
        switch (op)
        {
            case "OUT_OF_RANGE":
                if (ResultFlags.IsAbnormal(flag)) { reason = $"{rule.TriggerTestCode} поза нормою ({flag})"; return true; }
                return false;
            case "CRITICAL":
                if (ResultFlags.IsCritical(flag)) { reason = $"{rule.TriggerTestCode} критичне ({flag})"; return true; }
                return false;
        }
        if (!value.HasValue || !rule.ThresholdValue.HasValue) return false;
        var v = value.Value; var t = rule.ThresholdValue.Value;
        var ok = op switch
        {
            "<" => v < t,
            "<=" => v <= t,
            ">" => v > t,
            ">=" => v >= t,
            "==" or "=" => Math.Abs(v - t) < 1e-9,
            _ => false
        };
        if (ok) reason = $"{rule.TriggerTestCode} = {v} {op} {t} → додано {rule.ReflexTestCode}";
        return ok;
    }
}
