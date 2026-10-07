using MedLink.LIS.Core.Clinical;

namespace MedLink.LIS.Tests.Clinical;

public class NormsCascadeResolverTests
{
    // Каскад GLU з add_layered_norms_to_db.py
    private static List<ReferenceLayerInput> GluLayers() => new()
    {
        new() { Id = "BASE", TestCode = "GLU", MethodCode = "HEX_IFCC", LayerType = "BASELINE", PriorityOrder = 10, NormName = "База", NormLow = 4.10, NormHigh = 5.90, CritLow = 2.5, CritHigh = 25, Unit = "ммоль/л", DeltaCheckMaxPct = 25 },
        new() { Id = "CHILD", TestCode = "GLU", MethodCode = "HEX_IFCC", LayerType = "DEMOGRAPHIC", PriorityOrder = 40, NormName = "Діти", Gender = "ANY", IsAge = true, AgeUnit = "YEARS", AgeFrom = 0, AgeTo = 14, NormLow = 3.3, NormHigh = 5.6 },
        new() { Id = "ADULT_M", TestCode = "GLU", MethodCode = "HEX_IFCC", LayerType = "DEMOGRAPHIC", PriorityOrder = 40, NormName = "Чоловіки", Gender = "M", IsGender = true, IsAge = true, AgeUnit = "YEARS", AgeFrom = 15, AgeTo = 64, NormLow = 4.1, NormHigh = 5.9 },
        new() { Id = "ADULT_F", TestCode = "GLU", MethodCode = "HEX_IFCC", LayerType = "DEMOGRAPHIC", PriorityOrder = 40, NormName = "Жінки", Gender = "F", IsGender = true, IsAge = true, AgeUnit = "YEARS", AgeFrom = 15, AgeTo = 64, NormLow = 4.1, NormHigh = 5.9 },
        new() { Id = "ELDERLY", TestCode = "GLU", MethodCode = "HEX_IFCC", LayerType = "DEMOGRAPHIC", PriorityOrder = 40, NormName = "65+", Gender = "ANY", IsAge = true, AgeUnit = "YEARS", AgeFrom = 65, AgeTo = 120, NormLow = 4.4, NormHigh = 6.4 },
        new() { Id = "LUTEAL", TestCode = "GLU", MethodCode = "HEX_IFCC", LayerType = "MENSTRUAL_PHASE", PriorityOrder = 80, NormName = "Лютеїнова", Gender = "F", IsGender = true, IsAge = true, AgeUnit = "YEARS", AgeFrom = 15, AgeTo = 55, IsMenstrualPhase = true, MenstrualPhase = "LUTEAL", NormLow = 3.8, NormHigh = 5.7 },
        new() { Id = "PREG_T2", TestCode = "GLU", MethodCode = "HEX_IFCC", LayerType = "PREGNANCY", PriorityOrder = 100, NormName = "Вагітні T2", Gender = "F", IsGender = true, IsAge = true, AgeUnit = "YEARS", AgeFrom = 15, AgeTo = 50, IsPregnancy = true, PregnancyWeekFrom = 14, PregnancyWeekTo = 27, NormLow = 3.5, NormHigh = 5.3 },
        new() { Id = "DIAB", TestCode = "GLU", MethodCode = "HEX_IFCC", LayerType = "CLINICAL_ICD10", PriorityOrder = 60, NormName = "ЦД E11", Gender = "ANY", IsAge = true, AgeUnit = "YEARS", AgeFrom = 18, AgeTo = 120, Icd10Code = "E11", NormLow = 4.0, NormHigh = 7.2 },
        new() { Id = "GOD_BASE", TestCode = "GLU", MethodCode = "GOD_PAP", LayerType = "BASELINE", PriorityOrder = 10, NormName = "GOD-PAP база", NormLow = 3.89, NormHigh = 5.83 },
    };

    private static double Years(double y) => y * 365.25;

    [Fact]
    public void Pregnant_woman_in_second_trimester_gets_pregnancy_layer()
    {
        var res = NormsCascadeResolver.Resolve(GluLayers(), new PatientContext { Gender = "F", AgeDays = Years(30), IsPregnant = true, PregnancyWeek = 20, MethodCode = "HEX_IFCC" });
        Assert.True(res.Found);
        Assert.Equal("PREG_T2", res.WinningLayer!.Id);
        Assert.Equal(5.3, res.NormHigh);
        Assert.Contains(res.AuditTrace, s => s.LayerId == "LUTEAL" && !s.IsMatched);
    }

    [Fact]
    public void Pregnant_outside_week_range_falls_to_demographic()
    {
        var res = NormsCascadeResolver.Resolve(GluLayers(), new PatientContext { Gender = "F", AgeDays = Years(30), IsPregnant = true, PregnancyWeek = 8 });
        Assert.Equal("ADULT_F", res.WinningLayer!.Id);
    }

    [Fact]
    public void Menstrual_phase_layer_wins_for_woman_when_not_pregnant()
    {
        var res = NormsCascadeResolver.Resolve(GluLayers(), new PatientContext { Gender = "F", AgeDays = Years(28), MenstrualPhase = "LUTEAL" });
        Assert.Equal("LUTEAL", res.WinningLayer!.Id);
    }

    [Fact]
    public void Menstrual_phase_is_ignored_for_men()
    {
        var res = NormsCascadeResolver.Resolve(GluLayers(), new PatientContext { Gender = "M", AgeDays = Years(28), MenstrualPhase = "LUTEAL" });
        Assert.Equal("ADULT_M", res.WinningLayer!.Id);
    }

    [Fact]
    public void Icd10_cohort_layer_matches_by_prefix()
    {
        var res = NormsCascadeResolver.Resolve(GluLayers(), new PatientContext { Gender = "M", AgeDays = Years(50), Icd10Code = "E11.9" });
        Assert.Equal("DIAB", res.WinningLayer!.Id);
        Assert.Equal(7.2, res.NormHigh);
    }

    [Fact]
    public void Age_is_compared_in_days_for_newborn_layer()
    {
        var layers = new List<ReferenceLayerInput>
        {
            new() { Id = "NB", TestCode = "HGB", LayerType = "DEMOGRAPHIC", PriorityOrder = 40, IsAge = true, AgeUnit = "DAYS", AgeFrom = 0, AgeTo = 14, NormLow = 135, NormHigh = 215 },
            new() { Id = "INF", TestCode = "HGB", LayerType = "DEMOGRAPHIC", PriorityOrder = 40, IsAge = true, AgeUnit = "DAYS", AgeFrom = 15, AgeTo = 60, NormLow = 100, NormHigh = 180 },
            new() { Id = "BASE", TestCode = "HGB", LayerType = "BASELINE", PriorityOrder = 10, NormLow = 120, NormHigh = 160 },
        };
        Assert.Equal("NB", NormsCascadeResolver.Resolve(layers, new PatientContext { Gender = "F", AgeDays = 5 }).WinningLayer!.Id);
        Assert.Equal("INF", NormsCascadeResolver.Resolve(layers, new PatientContext { Gender = "F", AgeDays = 30 }).WinningLayer!.Id);
        Assert.Equal("BASE", NormsCascadeResolver.Resolve(layers, new PatientContext { Gender = "F", AgeDays = 400 }).WinningLayer!.Id);
    }

    [Fact]
    public void Child_and_elderly_age_slices()
    {
        Assert.Equal("CHILD", NormsCascadeResolver.Resolve(GluLayers(), new PatientContext { Gender = "M", AgeDays = Years(7) }).WinningLayer!.Id);
        Assert.Equal("ELDERLY", NormsCascadeResolver.Resolve(GluLayers(), new PatientContext { Gender = "F", AgeDays = Years(77) }).WinningLayer!.Id);
    }

    [Fact]
    public void Upper_age_bound_includes_whole_last_year()
    {
        // 14 років 11 місяців → все ще «діти 0–14»
        Assert.Equal("CHILD", NormsCascadeResolver.Resolve(GluLayers(), new PatientContext { Gender = "M", AgeDays = Years(14) + 300 }).WinningLayer!.Id);
    }

    [Fact]
    public void Baseline_fallback_when_nothing_else_matches()
    {
        // стать невідома → гендерні шари не підходять; вік 30 → не дитина/не літній
        var res = NormsCascadeResolver.Resolve(GluLayers(), new PatientContext { Gender = "U", AgeDays = Years(30) });
        Assert.Equal("BASE", res.WinningLayer!.Id);
        Assert.Equal("4.1 – 5.9", res.ReferenceDisplay);
    }

    [Fact]
    public void Method_filter_selects_layers_of_requested_method()
    {
        var res = NormsCascadeResolver.Resolve(GluLayers(), new PatientContext { Gender = "M", AgeDays = Years(30), MethodCode = "GOD_PAP" });
        Assert.Equal("GOD_BASE", res.WinningLayer!.Id);
    }

    [Fact]
    public void Unknown_method_falls_back_to_all_layers()
    {
        var res = NormsCascadeResolver.Resolve(GluLayers(), new PatientContext { Gender = "M", AgeDays = Years(30), MethodCode = "NOPE" });
        Assert.Equal("ADULT_M", res.WinningLayer!.Id);
    }

    [Fact]
    public void No_layers_returns_not_found()
    {
        var res = NormsCascadeResolver.Resolve(new List<ReferenceLayerInput>(), new PatientContext());
        Assert.False(res.Found);
        Assert.Null(res.WinningLayer);
    }

    [Fact]
    public void Age_unit_conversion()
    {
        Assert.Equal(30.4375, AgeUnits.ToDays(1, "MONTHS"));
        Assert.Equal(365.25, AgeUnits.ToDays(1, "YEARS"));
        Assert.Equal(7, AgeUnits.ToDays(7, "DAYS"));
        Assert.Equal(44, AgeUnits.AgeYears(new DateTime(1982, 4, 12), new DateTime(2026, 10, 7)));
    }
}
