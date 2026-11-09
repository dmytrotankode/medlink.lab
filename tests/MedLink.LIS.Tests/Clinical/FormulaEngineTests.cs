using MedLink.LIS.Core.Clinical;

namespace MedLink.LIS.Tests.Clinical;

public class FormulaEngineTests
{
    private static double? Calc(string formula, params (string Code, double Value)[] inputs)
    {
        var parsed = FormulaEngine.Parse(formula);
        Assert.True(parsed.IsValid, parsed.Error);
        var map = inputs.ToDictionary(i => i.Code.ToUpperInvariant(), i => i.Value);
        var res = FormulaEngine.Evaluate(parsed, c => map.TryGetValue(c, out var v) ? v : null);
        return res.Value;
    }

    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("-2 ^ 2", -4)]            // унарний мінус слабший за степінь
    [InlineData("2 ^ 3 ^ 2", 512)]        // степінь правоасоціативний
    [InlineData("10 / 4", 2.5)]
    [InlineData("7 - 2 - 1", 4)]
    [InlineData("ROUND(2.346; 2)", 2.35)]
    [InlineData("ROUND(2.5)", 3)]
    [InlineData("MAX(1, 5, 3) - MIN(4, 2)", 3)]
    [InlineData("SQRT(16) + ABS(-3)", 7)]
    [InlineData("POW(2, 10)", 1024)]
    [InlineData("2 × 3 ÷ 4", 1.5)]
    public void Arithmetic_and_functions(string formula, double expected) => Assert.Equal(expected, Calc(formula)!.Value, 9);

    [Fact]
    public void Indirect_ldl_friedewald()
    {
        // ЛПНЩ = ЗХ − ЛПВЩ − ТГ/2.2
        Assert.Equal(3.4, Calc("ROUND(CHOL - HDL - TG / 2.2; 2)", ("CHOL", 5.2), ("HDL", 1.3), ("TG", 1.1))!.Value, 9);
    }

    [Fact]
    public void Atherogenic_index_and_ag_ratio()
    {
        Assert.Equal(3.0, Calc("ROUND((CHOL - HDL) / HDL; 2)", ("CHOL", 5.2), ("HDL", 1.3))!.Value, 9);
        Assert.Equal(1.5, Calc("ALB / (PROT_TOT - ALB)", ("ALB", 45), ("PROT_TOT", 75))!.Value, 9);
    }

    [Fact]
    public void Ckd_epi_uses_age_and_sex_context()
    {
        const string f = "141 * POW(MIN(CREAT / 88.4 / (0.9 * MALE + 0.7 * FEMALE); 1); -0.411 * MALE - 0.329 * FEMALE) * POW(MAX(CREAT / 88.4 / (0.9 * MALE + 0.7 * FEMALE); 1); -1.209) * POW(0.993; AGE) * (1 + 0.018 * FEMALE)";
        var parsed = FormulaEngine.Parse(f);
        Assert.True(parsed.IsValid, parsed.Error);
        Assert.Equal(new[] { "CREAT" }, parsed.Inputs);
        Assert.Equal(new[] { "AGE", "FEMALE", "MALE" }, parsed.ContextVariables.OrderBy(x => x));
        var egfr = Calc(f, ("CREAT", 88.4), ("AGE", 40), ("MALE", 1), ("FEMALE", 0))!.Value;   // 1.0 мг/дл, чоловік 40 р.
        Assert.InRange(egfr, 92, 96);
    }

    [Fact]
    public void Missing_inputs_are_reported_not_calculated()
    {
        var parsed = FormulaEngine.Parse("HGB / RBC * 10");
        var res = FormulaEngine.Evaluate(parsed, c => c == "HGB" ? 130 : null);
        Assert.Null(res.Value);
        Assert.Equal(new[] { "RBC" }, res.Missing);
        Assert.Null(res.Error);
    }

    [Fact]
    public void Division_by_zero_and_domain_errors_are_errors()
    {
        Assert.Contains("нуль", FormulaEngine.Evaluate(FormulaEngine.Parse("A / B"), c => c == "A" ? 1 : 0).Error);
        Assert.NotNull(FormulaEngine.Evaluate(FormulaEngine.Parse("SQRT(A)"), _ => -4).Error);
        Assert.NotNull(FormulaEngine.Evaluate(FormulaEngine.Parse("LN(A)"), _ => 0).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1 +")]
    [InlineData("(1 + 2")]
    [InlineData("1 + 2)")]
    [InlineData("FOO(1)")]                // невідома функція — жодних довільних викликів
    [InlineData("ROUND()")]
    [InlineData("POW(2)")]
    [InlineData("1,5 + 2")]               // кома — лише роздільник аргументів
    [InlineData("HGB $ RBC")]
    [InlineData("2 3")]
    [InlineData("1abc + 2")]
    public void Syntax_errors_are_reported_with_message(string formula)
    {
        var p = FormulaEngine.Parse(formula);
        Assert.False(p.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(p.Error));
    }

    [Fact]
    public void Dangerous_input_is_not_executed()
    {
        foreach (var f in new[] { "System.Environment.Exit(1)", "Process.Start(\"x\")", "1; DROP TABLE x", "eval(1)", "__import__('os')" })
            Assert.False(FormulaEngine.Parse(f).IsValid);
        Assert.False(FormulaEngine.Parse(new string('1', FormulaEngine.MaxLength + 1)).IsValid);
        Assert.False(FormulaEngine.Parse(new string('(', 200) + "1" + new string(')', 200)).IsValid); // глибина
    }

    [Fact]
    public void Codes_are_case_insensitive_and_distinct()
    {
        var p = FormulaEngine.Parse("hgb / Rbc + HGB");
        Assert.Equal(new[] { "HGB", "RBC" }, p.Inputs.OrderBy(x => x));
    }

    [Fact]
    public void Cycle_detection_finds_chain()
    {
        var graph = new Dictionary<string, string[]> { ["B"] = new[] { "C" }, ["C"] = new[] { "A" } };
        var cycle = FormulaEngine.FindCycle("A", new[] { "B" }, c => graph.GetValueOrDefault(c, Array.Empty<string>()));
        Assert.Equal(new[] { "A", "B", "C", "A" }, cycle);
        Assert.Null(FormulaEngine.FindCycle("A", new[] { "B" }, c => c == "B" ? new[] { "D" } : Array.Empty<string>()));
    }
}
