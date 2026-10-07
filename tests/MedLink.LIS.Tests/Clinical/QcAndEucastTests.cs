using MedLink.LIS.Core.Clinical;

namespace MedLink.LIS.Tests.Clinical;

public class WestgardEvaluatorTests
{
    private const double Mean = 7.2, Sd = 0.3;
    private static double Z(double z) => Mean + z * Sd;

    [Fact]
    public void In_control_point_has_no_rules()
    {
        var r = WestgardEvaluator.Evaluate(new[] { Z(0.5), Z(-0.3), Z(1.0) }, Mean, Sd);
        Assert.Empty(r.ViolatedRules);
        Assert.Equal("OK", r.Status);
    }

    [Fact]
    public void Rule_1_2s_is_warning_only()
    {
        var r = WestgardEvaluator.Evaluate(new[] { Z(0.5), Z(2.5) }, Mean, Sd);
        Assert.Contains("1_2s", r.ViolatedRules);
        Assert.True(r.IsWarning);
        Assert.False(r.IsRejection);
    }

    [Fact]
    public void Rule_1_3s_rejects()
    {
        var r = WestgardEvaluator.Evaluate(new[] { Z(0.1), 8.3 }, Mean, Sd);
        Assert.Contains("1_3s", r.ViolatedRules);
        Assert.True(r.IsRejection);
        Assert.Equal(3.667, r.ZScore);
    }

    [Fact]
    public void Rule_2_2s_two_consecutive_same_side()
    {
        var r = WestgardEvaluator.Evaluate(new[] { Z(0), Z(2.3), Z(2.4) }, Mean, Sd);
        Assert.Contains("2_2s", r.ViolatedRules);
        Assert.True(r.IsRejection);
        // протилежні сторони — не 2_2s
        var r2 = WestgardEvaluator.Evaluate(new[] { Z(0), Z(-2.3), Z(2.4) }, Mean, Sd);
        Assert.DoesNotContain("2_2s", r2.ViolatedRules);
    }

    [Fact]
    public void Rule_R_4s_range_between_consecutive()
    {
        var r = WestgardEvaluator.Evaluate(new[] { Z(0), Z(-2.2), Z(2.3) }, Mean, Sd);
        Assert.Contains("R_4s", r.ViolatedRules);
        Assert.True(r.IsRejection);
    }

    [Fact]
    public void Rule_4_1s_four_consecutive_beyond_1sd()
    {
        var r = WestgardEvaluator.Evaluate(new[] { Z(0), Z(1.2), Z(1.5), Z(1.1), Z(1.3) }, Mean, Sd);
        Assert.Contains("4_1s", r.ViolatedRules);
        Assert.DoesNotContain("1_2s", r.ViolatedRules);
        Assert.True(r.IsRejection);
    }

    [Fact]
    public void Rule_10x_ten_consecutive_same_side_of_mean()
    {
        var series = Enumerable.Range(0, 10).Select(i => Z(0.2 + (i % 3) * 0.1)).ToArray();
        var r = WestgardEvaluator.Evaluate(series, Mean, Sd);
        Assert.Contains("10_x", r.ViolatedRules);
        Assert.True(r.IsRejection);
        var nine = series.Take(9).ToArray();
        Assert.DoesNotContain("10_x", WestgardEvaluator.Evaluate(nine, Mean, Sd).ViolatedRules);
    }

    [Fact]
    public void Window_is_limited_to_last_10_points()
    {
        var series = new List<double> { Z(-3.5) }; // стара точка поза вікном
        series.AddRange(Enumerable.Range(0, 10).Select(i => Z(i % 2 == 0 ? 0.3 : -0.3)));
        var r = WestgardEvaluator.Evaluate(series, Mean, Sd);
        Assert.Empty(r.ViolatedRules);
    }

    [Fact]
    public void Invalid_sd_throws() => Assert.Throws<ArgumentException>(() => WestgardEvaluator.Evaluate(new[] { 1.0 }, 1.0, 0));
}

public class QcStatisticsTests
{
    [Fact]
    public void Computes_n_mean_sd_cv_bias()
    {
        var s = QcStatistics.Compute(new[] { 7.0, 7.2, 7.4, 7.1, 7.3 }, 7.0);
        Assert.Equal(5, s.N);
        Assert.Equal(7.2, s.Mean);
        Assert.Equal(0.1581, s.Sd);
        Assert.Equal(2.2, s.CvPct);
        Assert.Equal(2.86, s.BiasPct);
    }

    [Fact]
    public void Empty_series() { var s = QcStatistics.Compute(Array.Empty<double>()); Assert.Equal(0, s.N); }

    [Fact]
    public void Single_value_has_zero_sd() => Assert.Equal(0, QcStatistics.Compute(new[] { 5.0 }).Sd);
}

public class EucastInterpreterTests
{
    private static readonly EucastBreakpointInput EcolCip = new() { OrganismCode = "ECOL", AntibioticCode = "CIP", MicSusceptibleLe = 0.25, MicResistantGt = 0.5, ZoneSusceptibleGe = 25, ZoneResistantLt = 22 };

    [Theory]
    [InlineData(27, "S")]
    [InlineData(25, "S")]
    [InlineData(23, "I")]
    [InlineData(21, "R")]
    public void Zone_interpretation(double zone, string expected) => Assert.Equal(expected, EucastInterpreter.Interpret(EcolCip, zone, null));

    [Theory]
    [InlineData(0.125, "S")]
    [InlineData(0.25, "S")]
    [InlineData(0.5, "I")]
    [InlineData(1.0, "R")]
    public void Mic_interpretation(double mic, string expected) => Assert.Equal(expected, EucastInterpreter.Interpret(EcolCip, null, mic));

    [Fact]
    public void Mic_takes_priority_over_zone() => Assert.Equal("R", EucastInterpreter.Interpret(EcolCip, 30, 2.0));

    [Fact]
    public void Intrinsic_resistance_is_always_R() => Assert.Equal("R", EucastInterpreter.Interpret(new EucastBreakpointInput { IntrinsicResistance = true }, 40, null));

    [Fact]
    public void No_breakpoint_data_gives_empty() => Assert.Equal("", EucastInterpreter.Interpret(new EucastBreakpointInput(), 20, null));

    [Fact]
    public void Mrsa_detected_by_cefoxitin_resistance()
    {
        var ph = EucastInterpreter.DetectPhenotypes("SAUR", "Staphylococcus aureus", new[] { new SusceptibilityObservation { AntibioticCode = "FOX", Interpretation = "R" } });
        Assert.Contains(ph, p => p.Code == "MRSA");
    }

    [Fact]
    public void Esbl_detected_for_e_coli_resistant_to_3rd_gen_cephalosporins_but_susceptible_to_carbapenems()
    {
        var ph = EucastInterpreter.DetectPhenotypes("ECOL", "Escherichia coli", new[]
        {
            new SusceptibilityObservation { AntibioticCode = "CTX", Interpretation = "R" },
            new SusceptibilityObservation { AntibioticCode = "MEM", Interpretation = "S" }
        });
        Assert.Contains(ph, p => p.Code == "ESBL");
        Assert.DoesNotContain(ph, p => p.Code == "CRE");
    }

    [Fact]
    public void Cre_detected_for_carbapenem_resistance()
    {
        var ph = EucastInterpreter.DetectPhenotypes("KPNE", "Klebsiella pneumoniae", new[] { new SusceptibilityObservation { AntibioticCode = "MEM", Interpretation = "R" } });
        Assert.Contains(ph, p => p.Code == "CRE");
    }

    [Fact]
    public void Vre_detected() => Assert.Contains(EucastInterpreter.DetectPhenotypes("EFAL", "Enterococcus faecalis", new[] { new SusceptibilityObservation { AntibioticCode = "VAN", Interpretation = "R" } }), p => p.Code == "VRE");
}
