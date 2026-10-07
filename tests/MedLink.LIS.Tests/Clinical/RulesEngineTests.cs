using MedLink.LIS.Core.Clinical;

namespace MedLink.LIS.Tests.Clinical;

public class ResultFlaggerTests
{
    [Theory]
    [InlineData(5.0, "NORMAL")]
    [InlineData(3.9, "LOW")]
    [InlineData(6.5, "HIGH")]
    [InlineData(2.5, "CRIT_LOW")]
    [InlineData(2.0, "CRIT_LOW")]
    [InlineData(25.0, "CRIT_HIGH")]
    [InlineData(30.0, "CRIT_HIGH")]
    public void Numeric_flags(double value, string expected) => Assert.Equal(expected, ResultFlagger.Flag(value, 4.1, 5.9, 2.5, 25.0));

    [Fact]
    public void No_limits_gives_none() => Assert.Equal("NONE", ResultFlagger.Flag(1, null, null, null, null));

    [Fact]
    public void Only_upper_limit() { Assert.Equal("HIGH", ResultFlagger.Flag(45, 0, 41, null, 500)); Assert.Equal("NORMAL", ResultFlagger.Flag(20, 0, 41, null, 500)); }

    [Fact]
    public void Text_result_matches_norm_text_options()
    {
        Assert.Equal("NORMAL", ResultFlagger.FlagText("Не виявлено", "не виявлено;negative"));
        Assert.Equal("NORMAL", ResultFlagger.FlagText("negative", "не виявлено;negative"));
        Assert.Equal("ABNORMAL", ResultFlagger.FlagText("+++", "не виявлено;negative"));
        Assert.Equal("NONE", ResultFlagger.FlagText("щось", null));
    }
}

public class DeltaCheckEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Alert_when_delta_exceeds_threshold()
    {
        var r = DeltaCheckEvaluator.Evaluate(7.9, 5.1, Now.AddDays(-5), Now, 168, 20);
        Assert.True(r.HasPrevious);
        Assert.Equal(54.9, r.DeltaPercent);
        Assert.True(r.IsAlert);
    }

    [Fact]
    public void No_alert_within_threshold()
    {
        var r = DeltaCheckEvaluator.Evaluate(5.5, 5.1, Now.AddHours(-10), Now, 72, 20);
        Assert.False(r.IsAlert);
        Assert.Equal(7.8, r.DeltaPercent);
    }

    [Fact]
    public void Previous_outside_window_is_ignored()
    {
        var r = DeltaCheckEvaluator.Evaluate(9.0, 5.0, Now.AddDays(-10), Now, 72, 20);
        Assert.False(r.HasPrevious);
        Assert.False(r.IsAlert);
    }

    [Fact]
    public void No_previous_value()
    {
        var r = DeltaCheckEvaluator.Evaluate(9.0, null, null, Now, 72, 20);
        Assert.False(r.HasPrevious);
        Assert.Null(r.DeltaPercent);
    }

    [Fact]
    public void Zero_previous_does_not_divide() => Assert.Null(DeltaCheckEvaluator.Evaluate(1, 0, Now, Now, 72, 20).DeltaPercent);
}

public class AutoVerificationEngineTests
{
    [Fact]
    public void Normal_clean_result_is_approved()
    {
        var d = AutoVerificationEngine.Evaluate(new AutoVerificationInput { Flag = "NORMAL", AnalyzerFlags = "N" });
        Assert.True(d.Approved);
    }

    [Fact]
    public void Critical_value_always_blocks() => Assert.False(AutoVerificationEngine.Evaluate(new AutoVerificationInput { Flag = "CRIT_HIGH" }).Approved);

    [Fact]
    public void Abnormal_blocks() => Assert.False(AutoVerificationEngine.Evaluate(new AutoVerificationInput { Flag = "HIGH" }).Approved);

    [Fact]
    public void Delta_alert_blocks()
    {
        var d = AutoVerificationEngine.Evaluate(new AutoVerificationInput { Flag = "NORMAL", DeltaAlert = true });
        Assert.False(d.Approved);
        Assert.Contains(d.BlockReasons, r => r.Contains("delta"));
    }

    [Fact]
    public void Lockout_blocks()
    {
        var d = AutoVerificationEngine.Evaluate(new AutoVerificationInput { Flag = "NORMAL", HasActiveLockout = true });
        Assert.False(d.Approved);
        Assert.Contains(d.BlockReasons, r => r.Contains("lockout"));
    }

    [Fact]
    public void Manual_verification_required_blocks() => Assert.False(AutoVerificationEngine.Evaluate(new AutoVerificationInput { Flag = "NORMAL", RequiresManualVerification = true }).Approved);

    [Fact]
    public void Analyzer_flags_block() => Assert.False(AutoVerificationEngine.Evaluate(new AutoVerificationInput { Flag = "NORMAL", AnalyzerFlags = "H*" }).Approved);
}

public class ReflexRuleEngineTests
{
    private static readonly List<ReflexRuleInput> Rules = new()
    {
        new() { Id = "r1", TriggerTestCode = "TSH", ConditionOperator = ">", ThresholdValue = 4.0, ReflexTestCode = "FT4" },
        new() { Id = "r2", TriggerTestCode = "PSA", ConditionOperator = ">", ThresholdValue = 4.0, ReflexTestCode = "FPSA" },
        new() { Id = "r3", TriggerTestCode = "GLU", ConditionOperator = ">", ThresholdValue = 11.0, ReflexTestCode = "HBA1C", RequiresSameSample = false },
        new() { Id = "r4", TriggerTestCode = "K", ConditionOperator = "CRITICAL", ReflexTestCode = "NA" },
    };

    [Fact]
    public void Tsh_above_threshold_triggers_ft4()
    {
        var t = ReflexRuleEngine.Evaluate(Rules, "TSH", 6.2, "HIGH");
        Assert.Single(t);
        Assert.Equal("FT4", t[0].Rule.ReflexTestCode);
    }

    [Fact]
    public void Below_threshold_does_not_trigger() => Assert.Empty(ReflexRuleEngine.Evaluate(Rules, "TSH", 2.0, "NORMAL"));

    [Fact]
    public void Existing_test_in_order_is_not_duplicated() => Assert.Empty(ReflexRuleEngine.Evaluate(Rules, "TSH", 6.2, "HIGH", new[] { "TSH", "FT4" }));

    [Fact]
    public void Critical_operator_uses_flag()
    {
        Assert.Single(ReflexRuleEngine.Evaluate(Rules, "K", 6.8, "CRIT_HIGH"));
        Assert.Empty(ReflexRuleEngine.Evaluate(Rules, "K", 5.5, "HIGH"));
    }

    [Fact]
    public void Glucose_reflex_requires_other_sample()
    {
        var t = ReflexRuleEngine.Evaluate(Rules, "GLU", 12.5, "HIGH");
        Assert.Single(t);
        Assert.False(t[0].Rule.RequiresSameSample);
    }
}

public class TatCalculatorTests
{
    [Fact]
    public void Stages_and_total()
    {
        var t0 = new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);
        var tat = TatCalculator.Compute(new TatTimestamps { OrderedAt = t0, CollectedAt = t0.AddMinutes(10), ReceivedAt = t0.AddMinutes(70), ResultedAt = t0.AddMinutes(130), VerifiedAt = t0.AddMinutes(160), ReleasedAt = t0.AddMinutes(180) });
        Assert.Equal(5, tat.Stages.Count);
        Assert.Equal(10, tat.Stages[0].Minutes);
        Assert.Equal(60, tat.Stages[1].Minutes);
        Assert.Equal(180, tat.TotalMinutes);
    }

    [Fact]
    public void Missing_stage_gives_null()
    {
        var tat = TatCalculator.Compute(new TatTimestamps { OrderedAt = DateTime.UtcNow });
        Assert.All(tat.Stages, s => Assert.Null(s.Minutes));
        Assert.Null(tat.TotalMinutes);
    }

    [Fact]
    public void Median_and_p90()
    {
        var values = new double[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        Assert.Equal(55, TatCalculator.Median(values));
        Assert.Equal(91, TatCalculator.P90(values));
    }
}
