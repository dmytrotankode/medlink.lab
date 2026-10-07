// =============================================================================
// MedLink LIS 4.0 — delta-check: порівняння з попереднім результатом пацієнта
// =============================================================================
namespace MedLink.LIS.Core.Clinical;

public sealed class DeltaCheckResult
{
    public bool HasPrevious { get; init; }
    public double? DeltaPercent { get; init; }
    public bool IsAlert { get; init; }
    public double? PreviousValue { get; init; }
    public DateTime? PreviousAt { get; init; }
    public string Reason { get; init; } = "";
}

public static class DeltaCheckEvaluator
{
    /// <summary>
    /// Попередній результат враховується лише у вікні <paramref name="windowHours"/> годин до <paramref name="now"/>.
    /// |Δ%| &gt; maxPct → alert. Δ% = (поточне − попереднє) / |попереднє| × 100.
    /// </summary>
    public static DeltaCheckResult Evaluate(double current, double? previous, DateTime? previousAt, DateTime now,
        int windowHours, double? maxPct)
    {
        if (!previous.HasValue)
            return new DeltaCheckResult { HasPrevious = false, Reason = "Попередній результат відсутній" };

        if (previousAt.HasValue && windowHours > 0 && (now - previousAt.Value).TotalHours > windowHours)
            return new DeltaCheckResult { HasPrevious = false, PreviousValue = previous, PreviousAt = previousAt,
                Reason = $"Попередній результат старіший за {windowHours} год." };

        if (Math.Abs(previous.Value) < 1e-12)
            return new DeltaCheckResult { HasPrevious = true, PreviousValue = previous, PreviousAt = previousAt,
                DeltaPercent = null, IsAlert = false, Reason = "Попереднє значення дорівнює нулю — Δ% не обчислюється" };

        var delta = Math.Round((current - previous.Value) / Math.Abs(previous.Value) * 100.0, 1);
        var alert = maxPct.HasValue && maxPct.Value > 0 && Math.Abs(delta) > maxPct.Value;
        return new DeltaCheckResult
        {
            HasPrevious = true,
            PreviousValue = previous,
            PreviousAt = previousAt,
            DeltaPercent = delta,
            IsAlert = alert,
            Reason = alert ? $"Відхилення {delta:+0.0;-0.0}% перевищує поріг {maxPct}%" : "У межах допустимого відхилення"
        };
    }
}
