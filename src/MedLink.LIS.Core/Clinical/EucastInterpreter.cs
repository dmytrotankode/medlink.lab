// =============================================================================
// MedLink LIS 4.0 — інтерпретація антибіотикограми за EUCAST (S/I/R) та
// детекція фенотипів стійкості (MRSA, VRE, ESBL, CRE)
// =============================================================================
namespace MedLink.LIS.Core.Clinical;

public sealed class EucastBreakpointInput
{
    public string OrganismCode { get; init; } = "";
    public string AntibioticCode { get; init; } = "";
    /// <summary>MIC ≤ S (мг/л)</summary>
    public double? MicSusceptibleLe { get; init; }
    /// <summary>MIC &gt; R (мг/л)</summary>
    public double? MicResistantGt { get; init; }
    /// <summary>Зона ≥ S (мм)</summary>
    public double? ZoneSusceptibleGe { get; init; }
    /// <summary>Зона &lt; R (мм)</summary>
    public double? ZoneResistantLt { get; init; }
    public bool IntrinsicResistance { get; init; }
}

public sealed class SusceptibilityObservation
{
    public string AntibioticCode { get; init; } = "";
    /// <summary>S | I | R</summary>
    public string Interpretation { get; init; } = "";
}

public sealed class ResistancePhenotype
{
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
}

public static class EucastInterpreter
{
    public const string Susceptible = "S";
    public const string IncreasedExposure = "I";
    public const string Resistant = "R";

    /// <summary>
    /// Інтерпретація за зоною (мм) або МІК (мг/л). Пріоритет — МІК, якщо вказано обидва.
    /// Повертає "" якщо breakpoint не дозволяє інтерпретувати.
    /// </summary>
    public static string Interpret(EucastBreakpointInput bp, double? zoneMm, double? mic)
    {
        if (bp.IntrinsicResistance) return Resistant;

        if (mic.HasValue && (bp.MicSusceptibleLe.HasValue || bp.MicResistantGt.HasValue))
        {
            if (bp.MicSusceptibleLe.HasValue && mic.Value <= bp.MicSusceptibleLe.Value) return Susceptible;
            if (bp.MicResistantGt.HasValue && mic.Value > bp.MicResistantGt.Value) return Resistant;
            if (bp.MicSusceptibleLe.HasValue && bp.MicResistantGt.HasValue) return IncreasedExposure;
            // лише одна межа задана
            return bp.MicSusceptibleLe.HasValue ? Resistant : Susceptible;
        }

        if (zoneMm.HasValue && (bp.ZoneSusceptibleGe.HasValue || bp.ZoneResistantLt.HasValue))
        {
            if (bp.ZoneSusceptibleGe.HasValue && zoneMm.Value >= bp.ZoneSusceptibleGe.Value) return Susceptible;
            if (bp.ZoneResistantLt.HasValue && zoneMm.Value < bp.ZoneResistantLt.Value) return Resistant;
            if (bp.ZoneSusceptibleGe.HasValue && bp.ZoneResistantLt.HasValue) return IncreasedExposure;
            return bp.ZoneSusceptibleGe.HasValue ? Resistant : Susceptible;
        }

        return "";
    }

    private static readonly string[] Enterobacterales = { "ECOL", "KPNE", "KOXY", "PMIR", "ENTC", "ECLO", "SMAR", "CFRE", "SALM", "SHIG", "PROT" };
    private static readonly string[] Cephalosporins3 = { "CTX", "CRO", "CAZ", "CPD", "FEP" };
    private static readonly string[] Carbapenems = { "MEM", "IPM", "ETP", "DOR" };

    /// <summary>
    /// Детекція фенотипів: MRSA (S. aureus, R до оксациліну/цефокситину), VRE (Enterococcus, R до ванкоміцину),
    /// ESBL (Enterobacterales, R до цефалоспоринів 3 покоління при S до карбапенемів),
    /// CRE (Enterobacterales, R до будь-якого карбапенему).
    /// </summary>
    public static List<ResistancePhenotype> DetectPhenotypes(string organismCode, string? organismLatinName,
        IEnumerable<SusceptibilityObservation> observations)
    {
        var obs = observations.ToList();
        var result = new List<ResistancePhenotype>();
        var code = (organismCode ?? "").ToUpperInvariant();
        var latin = (organismLatinName ?? "").ToLowerInvariant();

        bool R(string ab) => obs.Any(o => o.AntibioticCode.Equals(ab, StringComparison.OrdinalIgnoreCase) && o.Interpretation == Resistant);
        bool S(string ab) => obs.Any(o => o.AntibioticCode.Equals(ab, StringComparison.OrdinalIgnoreCase) && o.Interpretation == Susceptible);

        var isSaureus = code is "SAUR" or "MRSA" || latin.Contains("staphylococcus aureus");
        if (isSaureus && (R("FOX") || R("OXA") || code == "MRSA"))
            result.Add(new ResistancePhenotype { Code = "MRSA", Description = "Метицилін-резистентний Staphylococcus aureus — ізоляція пацієнта, глікопептиди/лінезолід" });

        var isEnterococcus = code.StartsWith("EFA") || latin.Contains("enterococcus");
        if (isEnterococcus && R("VAN"))
            result.Add(new ResistancePhenotype { Code = "VRE", Description = "Ванкоміцин-резистентний ентерокок" });

        var isEnterobacterales = Enterobacterales.Contains(code) || latin.Contains("escherichia") || latin.Contains("klebsiella")
                                 || latin.Contains("proteus") || latin.Contains("enterobacter") || latin.Contains("citrobacter") || latin.Contains("serratia");
        if (isEnterobacterales)
        {
            var carbapenemR = Carbapenems.Any(R);
            if (carbapenemR)
                result.Add(new ResistancePhenotype { Code = "CRE", Description = "Карбапенем-резистентні Enterobacterales (KPC/NDM/OXA-48) — критичне сповіщення інфекційного контролю" });
            if (Cephalosporins3.Any(R) && !carbapenemR && (Carbapenems.Any(S) || !Carbapenems.Any(ab => obs.Any(o => o.AntibioticCode == ab))))
                result.Add(new ResistancePhenotype { Code = "ESBL", Description = "Продуцент β-лактамаз розширеного спектра (ESBL) — препарати вибору: карбапенеми" });
        }

        var isPseudomonasOrAcineto = code is "PAER" or "ABAU" || latin.Contains("pseudomonas") || latin.Contains("acinetobacter");
        if (isPseudomonasOrAcineto && Carbapenems.Any(R))
            result.Add(new ResistancePhenotype { Code = "CR_NF", Description = "Карбапенем-резистентний неферментуючий збудник" });

        return result;
    }
}
