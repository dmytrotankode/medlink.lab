// =============================================================================
// MedLink LIS 4.0 — План пробірок (FR-PRE-004): правила окремих/об'єднаних пробірок.
// Алгоритм: групування за (біоматеріал, тип тари, група сумісності) → тести з вимогою
// окремої пробірки отримують власну → first-fit у порядку призначення з контролем
// сумарного об'єму та ліміту тестів на пробірку → упорядкування за порядком забору.
// =============================================================================
namespace MedLink.LIS.Core.Preanalytics;

/// <summary>Тест, що потребує місця в пробірці, з його правилами тари.</summary>
public sealed class TubePlanTest
{
    public string TestId { get; init; } = "";
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public int BiomaterialTypeId { get; init; }
    public int TubeTypeId { get; init; }
    /// <summary>Тест завжди в окремій пробірці (посів крові, стерильний посів сечі тощо).</summary>
    public bool RequiresSeparateTube { get; init; }
    /// <summary>Ліміт тестів у пробірці, де є цей тест (null — без обмеження).</summary>
    public int? MaxTestsPerTube { get; init; }
    /// <summary>Об'єм матеріалу, що споживає тест, мл (null/0 — не враховується).</summary>
    public double? RequiredVolumeMl { get; init; }
    /// <summary>Група сумісності: в одну пробірку потрапляють лише тести з однаковою групою (null = загальна).</summary>
    public string? CompatibilityGroup { get; init; }
}

/// <summary>Параметри типу тари, що впливають на об'єднання.</summary>
public sealed class TubePlanTubeType
{
    public int Id { get; init; }
    public int OrderOfDrawIndex { get; init; } = 99;
    /// <summary>Корисний об'єм матеріалу в пробірці, мл (null/0 — об'єм не контролюється).</summary>
    public double? CapacityMl { get; init; }
    public int? MaxTestsPerTube { get; init; }
}

/// <summary>Наявна пробірка замовлення, до якої можна доплюсувати тести (дозамовлення).</summary>
public sealed class TubePlanExistingTube
{
    public string SampleId { get; init; } = "";
    public int BiomaterialTypeId { get; init; }
    public int TubeTypeId { get; init; }
    public int GroupNumb { get; init; }
    public List<TubePlanTest> Tests { get; init; } = new();
}

public static class TubePlanReasons
{
    public const string SeparateRequired = "SEPARATE_REQUIRED";
    public const string VolumeSplit = "VOLUME_SPLIT";
    public const string TestLimitSplit = "TEST_LIMIT_SPLIT";
    public const string CompatibilityGroup = "COMPATIBILITY_GROUP";
    public const string OverCapacity = "OVER_CAPACITY";
}

public sealed class PlannedTube
{
    /// <summary>Id наявної пробірки, якщо тести додано до неї; null — нова пробірка.</summary>
    public string? ExistingSampleId { get; init; }
    public int BiomaterialTypeId { get; init; }
    public int TubeTypeId { get; init; }
    public string? CompatibilityGroup { get; init; }
    public int OrderOfDrawIndex { get; init; }
    public double? CapacityMl { get; init; }
    public int? MaxTests { get; internal set; }
    public bool IsSeparate { get; internal set; }
    public List<TubePlanTest> ExistingTests { get; init; } = new();
    public List<TubePlanTest> NewTests { get; } = new();
    /// <summary>Коди причин (TubePlanReasons.*), чому пробірка виділена окремо.</summary>
    public List<string> Reasons { get; } = new();

    public IEnumerable<TubePlanTest> AllTests => ExistingTests.Concat(NewTests);
    public int TestCount => ExistingTests.Count + NewTests.Count;
    public double UsedVolumeMl => AllTests.Sum(t => t.RequiredVolumeMl is > 0 ? t.RequiredVolumeMl.Value : 0);
    public bool IsOverCapacity => CapacityMl is > 0 && UsedVolumeMl > CapacityMl.Value + 1e-9;
}

public sealed class TubePlan
{
    public List<PlannedTube> Tubes { get; init; } = new();
    /// <summary>Попередження для медсестри/реєстратора (людською мовою).</summary>
    public List<string> Warnings { get; init; } = new();
}

public static class TubePlanner
{
    public static TubePlan Plan(IEnumerable<TubePlanTest> tests, IReadOnlyDictionary<int, TubePlanTubeType> tubeTypes, IEnumerable<TubePlanExistingTube>? existing = null)
    {
        var plan = new TubePlan();
        var tubes = new List<PlannedTube>();
        foreach (var ex in existing ?? Enumerable.Empty<TubePlanExistingTube>())
        {
            var tt = Type(tubeTypes, ex.TubeTypeId);
            var t = new PlannedTube
            {
                ExistingSampleId = ex.SampleId, BiomaterialTypeId = ex.BiomaterialTypeId, TubeTypeId = ex.TubeTypeId,
                CompatibilityGroup = Norm(ex.Tests.FirstOrDefault()?.CompatibilityGroup), OrderOfDrawIndex = tt.OrderOfDrawIndex,
                CapacityMl = tt.CapacityMl, ExistingTests = ex.Tests.ToList()
            };
            t.IsSeparate = ex.Tests.Any(x => x.RequiresSeparateTube);
            t.MaxTests = Min(tt.MaxTestsPerTube, ex.Tests.Select(x => x.MaxTestsPerTube));
            tubes.Add(t);
        }

        foreach (var test in tests)
        {
            var tt = Type(tubeTypes, test.TubeTypeId);
            var group = Norm(test.CompatibilityGroup);
            var vol = test.RequiredVolumeMl is > 0 ? test.RequiredVolumeMl.Value : 0;
            var overCapacityAlone = tt.CapacityMl is > 0 && vol > tt.CapacityMl.Value + 1e-9;

            PlannedTube? target = null;
            string? splitReason = null;
            if (!test.RequiresSeparateTube && !overCapacityAlone)
            {
                foreach (var t in tubes.Where(t => t.BiomaterialTypeId == test.BiomaterialTypeId && t.TubeTypeId == test.TubeTypeId && t.CompatibilityGroup == group && !t.IsSeparate))
                {
                    var limit = Min(t.MaxTests, new[] { test.MaxTestsPerTube });
                    if (limit.HasValue && t.TestCount + 1 > limit.Value) { splitReason ??= TubePlanReasons.TestLimitSplit; continue; }
                    if (t.CapacityMl is > 0 && t.UsedVolumeMl + vol > t.CapacityMl.Value + 1e-9) { splitReason ??= TubePlanReasons.VolumeSplit; continue; }
                    target = t;
                    break;
                }
            }

            if (target == null)
            {
                target = new PlannedTube
                {
                    BiomaterialTypeId = test.BiomaterialTypeId, TubeTypeId = test.TubeTypeId, CompatibilityGroup = group,
                    OrderOfDrawIndex = tt.OrderOfDrawIndex, CapacityMl = tt.CapacityMl, IsSeparate = test.RequiresSeparateTube,
                    MaxTests = tt.MaxTestsPerTube
                };
                if (test.RequiresSeparateTube) target.Reasons.Add(TubePlanReasons.SeparateRequired);
                else if (splitReason != null) target.Reasons.Add(splitReason);
                if (group != null) target.Reasons.Add(TubePlanReasons.CompatibilityGroup);
                if (overCapacityAlone)
                {
                    target.Reasons.Add(TubePlanReasons.OverCapacity);
                    plan.Warnings.Add($"Тест {test.Code} потребує {vol:0.##} мл, а корисний об'єм тари — {tt.CapacityMl:0.##} мл: потрібна більша тара або повторний забір");
                }
                tubes.Add(target);
            }
            target.NewTests.Add(test);
            target.MaxTests = Min(target.MaxTests, new[] { test.MaxTestsPerTube });
        }

        plan.Tubes.AddRange(tubes.Where(t => t.ExistingSampleId != null || t.NewTests.Count > 0)
            .OrderBy(t => t.OrderOfDrawIndex).ThenBy(t => t.TubeTypeId).ThenBy(t => t.BiomaterialTypeId));
        return plan;
    }

    private static TubePlanTubeType Type(IReadOnlyDictionary<int, TubePlanTubeType> types, int id) =>
        types.TryGetValue(id, out var t) ? t : new TubePlanTubeType { Id = id };

    private static string? Norm(string? group) => string.IsNullOrWhiteSpace(group) ? null : group.Trim().ToUpperInvariant();

    private static int? Min(int? a, IEnumerable<int?> rest)
    {
        var r = a is > 0 ? a : null;
        foreach (var v in rest)
            if (v is > 0) r = r.HasValue ? Math.Min(r.Value, v.Value) : v;
        return r;
    }
}
