// =============================================================================
// MedLink LIS 4.0 — статистика серії ВКЯ: N, mean, SD, CV%, bias%
// =============================================================================
namespace MedLink.LIS.Core.Clinical;

public sealed class QcSeriesStatistics
{
    public int N { get; init; }
    public double Mean { get; init; }
    /// <summary>Вибіркове SD (n−1).</summary>
    public double Sd { get; init; }
    public double CvPct { get; init; }
    /// <summary>Зміщення відносно цільового середнього, %.</summary>
    public double? BiasPct { get; init; }
}

public static class QcStatistics
{
    public static QcSeriesStatistics Compute(IEnumerable<double> values, double? targetMean = null)
    {
        var list = values.ToList();
        var n = list.Count;
        if (n == 0) return new QcSeriesStatistics { N = 0 };
        var mean = list.Average();
        var sd = n > 1 ? Math.Sqrt(list.Sum(v => (v - mean) * (v - mean)) / (n - 1)) : 0.0;
        var cv = Math.Abs(mean) > 1e-12 ? sd / Math.Abs(mean) * 100.0 : 0.0;
        double? bias = targetMean.HasValue && Math.Abs(targetMean.Value) > 1e-12
            ? (mean - targetMean.Value) / Math.Abs(targetMean.Value) * 100.0
            : null;
        return new QcSeriesStatistics
        {
            N = n,
            Mean = Math.Round(mean, 4),
            Sd = Math.Round(sd, 4),
            CvPct = Math.Round(cv, 2),
            BiasPct = bias.HasValue ? Math.Round(bias.Value, 2) : null
        };
    }

    /// <summary>Перцентиль (лінійна інтерполяція), p у [0;100].</summary>
    public static double? Percentile(IEnumerable<double> values, double p)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return null;
        if (sorted.Count == 1) return sorted[0];
        var rank = (p / 100.0) * (sorted.Count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
    }

    public static double? Median(IEnumerable<double> values) => Percentile(values, 50);
}
