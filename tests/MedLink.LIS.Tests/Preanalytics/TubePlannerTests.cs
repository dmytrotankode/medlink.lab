// План пробірок FR-PRE-004: окремі пробірки, розбиття за об'ємом і лімітом тестів, групи сумісності, дозамовлення
using MedLink.LIS.Core.Preanalytics;

namespace MedLink.LIS.Tests.Preanalytics;

public class TubePlannerTests
{
    private const int Serum = 7, Citrate = 1, Urine = 12, Slide = 11;
    private const int BmSerum = 4, BmPlasma = 5, BmUrine = 7;

    private static readonly Dictionary<int, TubePlanTubeType> Types = new()
    {
        [Serum] = new() { Id = Serum, OrderOfDrawIndex = 3, CapacityMl = 1.2 },
        [Citrate] = new() { Id = Citrate, OrderOfDrawIndex = 2, CapacityMl = 1.2 },
        [Urine] = new() { Id = Urine, OrderOfDrawIndex = 99, CapacityMl = 50 },
        [Slide] = new() { Id = Slide, OrderOfDrawIndex = 99, MaxTestsPerTube = 1 }
    };

    private static TubePlanTest T(string code, int bm, int tube, double? vol = null, bool separate = false, int? max = null, string? group = null) =>
        new() { TestId = "id-" + code, Code = code, Name = code, BiomaterialTypeId = bm, TubeTypeId = tube, RequiredVolumeMl = vol, RequiresSeparateTube = separate, MaxTestsPerTube = max, CompatibilityGroup = group };

    [Fact]
    public void Same_biomaterial_and_tube_are_merged_and_sorted_by_order_of_draw()
    {
        var plan = TubePlanner.Plan(new[] { T("GLU", BmSerum, Serum, 0.05), T("PT", BmPlasma, Citrate, 0.1), T("ALT", BmSerum, Serum, 0.05) }, Types);
        Assert.Equal(2, plan.Tubes.Count);
        Assert.Equal(Citrate, plan.Tubes[0].TubeTypeId); // цитрат забирається раніше сироватки
        Assert.Equal(new[] { "GLU", "ALT" }, plan.Tubes[1].NewTests.Select(t => t.Code));
        Assert.Empty(plan.Tubes[1].Reasons);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Separate_tube_test_never_shares()
    {
        var plan = TubePlanner.Plan(new[] { T("U_PH", BmUrine, Urine), T("URINE_CULTURE", BmUrine, Urine, separate: true), T("U_PRO", BmUrine, Urine) }, Types);
        Assert.Equal(2, plan.Tubes.Count);
        var sep = Assert.Single(plan.Tubes, t => t.IsSeparate);
        Assert.Equal("URINE_CULTURE", Assert.Single(sep.NewTests).Code);
        Assert.Contains(TubePlanReasons.SeparateRequired, sep.Reasons);
        Assert.Equal(2, plan.Tubes.Single(t => !t.IsSeparate).TestCount);
    }

    [Fact]
    public void Volume_overflow_splits_into_next_tube()
    {
        var tests = Enumerable.Range(1, 10).Select(i => T("B" + i, BmSerum, Serum, 0.15)).ToList(); // 1.5 мл > 1.2
        var plan = TubePlanner.Plan(tests, Types);
        Assert.Equal(2, plan.Tubes.Count);
        Assert.Equal(8, plan.Tubes[0].TestCount);
        Assert.Equal(1.2, plan.Tubes[0].UsedVolumeMl, 3);
        Assert.Contains(TubePlanReasons.VolumeSplit, plan.Tubes[1].Reasons);
        Assert.All(plan.Tubes, t => Assert.False(t.IsOverCapacity));
    }

    [Fact]
    public void Smaller_test_fills_earlier_tube_first_fit()
    {
        var plan = TubePlanner.Plan(new[] { T("A", BmSerum, Serum, 1.0), T("B", BmSerum, Serum, 0.5), T("C", BmSerum, Serum, 0.2) }, Types);
        Assert.Equal(2, plan.Tubes.Count);
        Assert.Equal(new[] { "A", "C" }, plan.Tubes[0].NewTests.Select(t => t.Code));
    }

    [Fact]
    public void Test_limit_from_tube_type_and_from_test()
    {
        var slides = TubePlanner.Plan(new[] { T("HISTO", 8, Slide), T("CYTO", 8, Slide) }, Types);
        Assert.Equal(2, slides.Tubes.Count);
        Assert.Contains(TubePlanReasons.TestLimitSplit, slides.Tubes[1].Reasons);

        var limited = TubePlanner.Plan(new[] { T("X1", BmSerum, Serum), T("X2", BmSerum, Serum, max: 2), T("X3", BmSerum, Serum) }, Types);
        Assert.Equal(new[] { 2, 1 }, limited.Tubes.Select(t => t.TestCount));
    }

    [Fact]
    public void Compatibility_groups_are_isolated()
    {
        var plan = TubePlanner.Plan(new[] { T("GLU", BmSerum, Serum), T("TSH", BmSerum, Serum, group: "immuno"), T("FT4", BmSerum, Serum, group: "IMMUNO "), T("ALT", BmSerum, Serum) }, Types);
        Assert.Equal(2, plan.Tubes.Count);
        var imm = plan.Tubes.Single(t => t.CompatibilityGroup == "IMMUNO");
        Assert.Equal(new[] { "TSH", "FT4" }, imm.NewTests.Select(t => t.Code));
        Assert.Contains(TubePlanReasons.CompatibilityGroup, imm.Reasons);
    }

    [Fact]
    public void Test_larger_than_tube_gets_own_tube_with_warning()
    {
        var plan = TubePlanner.Plan(new[] { T("BIG", BmSerum, Serum, 2.0), T("GLU", BmSerum, Serum, 0.05) }, Types);
        Assert.Equal(2, plan.Tubes.Count);
        Assert.Contains(TubePlanReasons.OverCapacity, plan.Tubes.Single(t => t.NewTests[0].Code == "BIG").Reasons);
        Assert.Single(plan.Warnings);
    }

    [Fact]
    public void Add_on_uses_existing_tube_when_rules_allow()
    {
        var existing = new[]
        {
            new TubePlanExistingTube { SampleId = "s1", BiomaterialTypeId = BmSerum, TubeTypeId = Serum, GroupNumb = 1, Tests = new() { T("GLU", BmSerum, Serum, 1.0) } },
            new TubePlanExistingTube { SampleId = "s2", BiomaterialTypeId = BmUrine, TubeTypeId = Urine, GroupNumb = 2, Tests = new() { T("URINE_CULTURE", BmUrine, Urine, separate: true) } }
        };
        var plan = TubePlanner.Plan(new[] { T("ALT", BmSerum, Serum, 0.1), T("CRP", BmSerum, Serum, 0.5), T("U_PH", BmUrine, Urine) }, Types, existing);
        Assert.Equal(new[] { "ALT" }, plan.Tubes.Single(t => t.ExistingSampleId == "s1").NewTests.Select(t => t.Code));
        Assert.Contains(plan.Tubes, t => t.ExistingSampleId == null && t.NewTests.Single().Code == "CRP");
        Assert.Empty(plan.Tubes.Single(t => t.ExistingSampleId == "s2").NewTests); // окрема пробірка посіву не приймає нових тестів
        Assert.Contains(plan.Tubes, t => t.ExistingSampleId == null && t.NewTests.Single().Code == "U_PH");
    }
}
