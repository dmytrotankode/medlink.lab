// =============================================================================
// MedLink LIS 4.0 — Turnaround Time: етапи order→collected→received→resulted→verified→released
// =============================================================================
namespace MedLink.LIS.Core.Clinical;

public sealed class TatTimestamps
{
    public DateTime OrderedAt { get; init; }
    public DateTime? CollectedAt { get; init; }
    public DateTime? ReceivedAt { get; init; }
    public DateTime? ResultedAt { get; init; }
    public DateTime? VerifiedAt { get; init; }
    public DateTime? ReleasedAt { get; init; }
}

public sealed class TatStage
{
    public string Name { get; init; } = "";
    public double? Minutes { get; init; }
}

public sealed class TatBreakdown
{
    public List<TatStage> Stages { get; init; } = new();
    /// <summary>Хвилини від замовлення до верифікації (або видачі, якщо є).</summary>
    public double? TotalMinutes { get; init; }
}

public static class TatCalculator
{
    public static readonly string[] StageNames = { "order→collected", "collected→received", "received→resulted", "resulted→verified", "verified→released" };

    public static TatBreakdown Compute(TatTimestamps t)
    {
        var stages = new List<TatStage>
        {
            new() { Name = StageNames[0], Minutes = Diff(t.OrderedAt, t.CollectedAt) },
            new() { Name = StageNames[1], Minutes = Diff(t.CollectedAt, t.ReceivedAt) },
            new() { Name = StageNames[2], Minutes = Diff(t.ReceivedAt, t.ResultedAt) },
            new() { Name = StageNames[3], Minutes = Diff(t.ResultedAt, t.VerifiedAt) },
            new() { Name = StageNames[4], Minutes = Diff(t.VerifiedAt, t.ReleasedAt) },
        };
        var end = t.ReleasedAt ?? t.VerifiedAt;
        return new TatBreakdown { Stages = stages, TotalMinutes = Diff(t.OrderedAt, end) };
    }

    private static double? Diff(DateTime? from, DateTime? to)
    {
        if (!from.HasValue || !to.HasValue) return null;
        var m = (to.Value - from.Value).TotalMinutes;
        return Math.Round(m < 0 ? 0 : m, 1);
    }

    public static double? Median(IEnumerable<double> values) => QcStatistics.Median(values);
    public static double? P90(IEnumerable<double> values) => QcStatistics.Percentile(values, 90);
}
