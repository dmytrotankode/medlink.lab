// =============================================================================
// MedLink LIS 4.0 — автоверифікація результатів
// =============================================================================
namespace MedLink.LIS.Core.Clinical;

public sealed class AutoVerificationInput
{
    public string Flag { get; init; } = ResultFlags.None;
    public string? AnalyzerFlags { get; init; }
    public bool DeltaAlert { get; init; }
    public bool RequiresManualVerification { get; init; }
    public bool HasActiveLockout { get; init; }
    public bool HasValue { get; init; } = true;
}

public sealed class AutoVerificationDecision
{
    public bool Approved { get; init; }
    public List<string> BlockReasons { get; init; } = new();
    public string Summary => Approved ? "Автоверифіковано" : string.Join("; ", BlockReasons);
}

public static class AutoVerificationEngine
{
    /// <summary>
    /// AUTO_VERIFIED лише якщо одночасно: flag==NORMAL, прапорці приладу порожні або «N»,
    /// deltaAlert==false, тест не вимагає ручної верифікації, немає активного lockout.
    /// Критичне значення завжди блокує.
    /// </summary>
    public static AutoVerificationDecision Evaluate(AutoVerificationInput input)
    {
        var reasons = new List<string>();
        if (!input.HasValue) reasons.Add("Результат порожній");
        if (ResultFlags.IsCritical(input.Flag)) reasons.Add("Критичне (панічне) значення — потрібен лікар-лаборант");
        else if (input.Flag != ResultFlags.Normal) reasons.Add($"Результат поза нормою ({input.Flag})");
        if (!AnalyzerFlagsAreClean(input.AnalyzerFlags)) reasons.Add($"Прапорці аналізатора: {input.AnalyzerFlags}");
        if (input.DeltaAlert) reasons.Add("Спрацював delta-check");
        if (input.RequiresManualVerification) reasons.Add("Тест вимагає ручної верифікації");
        if (input.HasActiveLockout) reasons.Add("Активний lockout аналізатора (ВКЯ)");
        return new AutoVerificationDecision { Approved = reasons.Count == 0, BlockReasons = reasons };
    }

    public static bool AnalyzerFlagsAreClean(string? flags)
    {
        if (string.IsNullOrWhiteSpace(flags)) return true;
        var f = flags.Trim().ToUpperInvariant();
        return f == "N" || f == "F" || f == "N^F" || f == "NORMAL";
    }
}
