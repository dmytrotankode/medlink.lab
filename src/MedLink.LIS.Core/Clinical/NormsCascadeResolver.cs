// =============================================================================
// MedLink LIS 4.0 — каскад референтних норм (перенесення семантики Simplex:
// dct_service_lab_norm + dct_service_lab_nv + dct_service_lab_attribute).
// Чистий клас без залежностей від EF/ASP.NET: приймає прості вхідні дані.
// =============================================================================
namespace MedLink.LIS.Core.Clinical;

/// <summary>Типи шарів каскаду та їх стандартні пріоритети.</summary>
public static class LayerTypes
{
    public const string Baseline = "BASELINE";          // 10
    public const string Demographic = "DEMOGRAPHIC";    // 40
    public const string ClinicalIcd10 = "CLINICAL_ICD10"; // 60
    public const string MenstrualPhase = "MENSTRUAL_PHASE"; // 80
    public const string Pregnancy = "PREGNANCY";        // 100

    public static int DefaultPriority(string layerType) => layerType?.ToUpperInvariant() switch
    {
        Pregnancy => 100,
        MenstrualPhase => 80,
        ClinicalIcd10 => 60,
        Demographic => 40,
        _ => 10
    };
}

/// <summary>Одиниці віку та конвертація у дні (DAYS=1, MONTHS=30.4375, YEARS=365.25).</summary>
public static class AgeUnits
{
    public const string Days = "DAYS";
    public const string Months = "MONTHS";
    public const string Years = "YEARS";

    public static double ToDays(double value, string? unit) => (unit ?? Years).ToUpperInvariant() switch
    {
        Days => value,
        Months => value * 30.4375,
        _ => value * 365.25
    };

    /// <summary>Вік у днях на момент <paramref name="at"/>.</summary>
    public static double AgeDays(DateTime birthDate, DateTime at)
    {
        var days = (at.Date - birthDate.Date).TotalDays;
        return days < 0 ? 0 : days;
    }

    /// <summary>Повні роки для відображення («45 р.»).</summary>
    public static int AgeYears(DateTime birthDate, DateTime at)
    {
        var years = at.Year - birthDate.Year;
        if (at.Month < birthDate.Month || (at.Month == birthDate.Month && at.Day < birthDate.Day)) years--;
        return Math.Max(0, years);
    }
}

/// <summary>Плоский опис шару норми (рядок lab_reference_layer).</summary>
public sealed class ReferenceLayerInput
{
    public string Id { get; init; } = "";
    public string TestCode { get; init; } = "";
    public string? MethodCode { get; init; }
    public string LayerType { get; init; } = LayerTypes.Baseline;
    public int PriorityOrder { get; init; } = 10;
    public string NormName { get; init; } = "";
    public string Gender { get; init; } = "ANY";
    public bool IsGender { get; init; }
    public string AgeUnit { get; init; } = AgeUnits.Years;
    public double AgeFrom { get; init; }
    public double AgeTo { get; init; } = 120;
    public bool IsAge { get; init; }
    public bool IsMenstrualPhase { get; init; }
    public string? MenstrualPhase { get; init; }
    public bool IsPregnancy { get; init; }
    public int? PregnancyWeekFrom { get; init; }
    public int? PregnancyWeekTo { get; init; }
    public string? Icd10Code { get; init; }
    public double? NormLow { get; init; }
    public double? NormHigh { get; init; }
    public double? CritLow { get; init; }
    public double? CritHigh { get; init; }
    public string? NormText { get; init; }
    public string? Unit { get; init; }
    public double? DeltaCheckMaxPct { get; init; }
}

/// <summary>Контекст пацієнта для підбору норми.</summary>
public sealed class PatientContext
{
    /// <summary>M | F | U</summary>
    public string Gender { get; init; } = "U";
    public double AgeDays { get; init; }
    public bool IsPregnant { get; init; }
    public int? PregnancyWeek { get; init; }
    /// <summary>FOLLICULAR | OVULATORY | LUTEAL | POSTMENOPAUSE</summary>
    public string? MenstrualPhase { get; init; }
    public string? Icd10Code { get; init; }
    public string? MethodCode { get; init; }
}

public sealed class CascadeAuditStep
{
    public string LayerId { get; init; } = "";
    public int Priority { get; init; }
    public string LayerType { get; init; } = "";
    public string LayerName { get; init; } = "";
    public bool IsMatched { get; init; }
    public string Reason { get; init; } = "";
}

public sealed class CascadeResolution
{
    public bool Found { get; init; }
    public ReferenceLayerInput? WinningLayer { get; init; }
    public double? NormLow { get; init; }
    public double? NormHigh { get; init; }
    public double? CritLow { get; init; }
    public double? CritHigh { get; init; }
    public string? Unit { get; init; }
    public string? NormText { get; init; }
    public double? DeltaCheckMaxPct { get; init; }
    public string ReferenceDisplay { get; init; } = "";
    public List<CascadeAuditStep> AuditTrace { get; init; } = new();
}

/// <summary>
/// Резолвер каскаду: шари сортуються за priorityOrder desc (далі — за специфічністю),
/// перший, що співпав, перемагає; BASELINE співпадає завжди.
/// Вік порівнюється у днях; верхня межа діапазону включає весь останній період
/// (напр. «16–120 років» = від 16 років включно до настання 121 року).
/// </summary>
public static class NormsCascadeResolver
{
    public static CascadeResolution Resolve(IEnumerable<ReferenceLayerInput> layers, PatientContext ctx)
    {
        var all = layers.ToList();
        var trace = new List<CascadeAuditStep>();

        // Фільтр за методикою: якщо вказано і є шари цієї методики — беремо лише їх.
        var candidates = all;
        if (!string.IsNullOrWhiteSpace(ctx.MethodCode))
        {
            var byMethod = all.Where(l => string.Equals(l.MethodCode, ctx.MethodCode, StringComparison.OrdinalIgnoreCase)).ToList();
            if (byMethod.Count > 0) candidates = byMethod;
        }

        var ordered = candidates
            .OrderByDescending(l => l.PriorityOrder)
            .ThenByDescending(l => l.IsGender && !string.Equals(l.Gender, "ANY", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(l => l.IsAge ? AgeUnits.ToDays(l.AgeTo, l.AgeUnit) - AgeUnits.ToDays(l.AgeFrom, l.AgeUnit) : double.MaxValue)
            .ToList();

        ReferenceLayerInput? winner = null;
        foreach (var layer in ordered)
        {
            var (match, reason) = Evaluate(layer, ctx);
            trace.Add(new CascadeAuditStep
            {
                LayerId = layer.Id,
                Priority = layer.PriorityOrder,
                LayerType = layer.LayerType,
                LayerName = layer.NormName,
                IsMatched = match,
                Reason = reason
            });
            if (match && winner == null) winner = layer;
        }

        if (winner == null)
        {
            return new CascadeResolution { Found = false, AuditTrace = trace, ReferenceDisplay = "" };
        }

        return new CascadeResolution
        {
            Found = true,
            WinningLayer = winner,
            NormLow = winner.NormLow,
            NormHigh = winner.NormHigh,
            CritLow = winner.CritLow,
            CritHigh = winner.CritHigh,
            Unit = winner.Unit,
            NormText = winner.NormText,
            DeltaCheckMaxPct = winner.DeltaCheckMaxPct,
            ReferenceDisplay = FormatReference(winner.NormLow, winner.NormHigh, winner.NormText),
            AuditTrace = trace
        };
    }

    /// <summary>Текст референсу для бланка: «4.10 – 5.90», «&lt; 41», «≥ 2.0» або текстова норма.</summary>
    public static string FormatReference(double? low, double? high, string? normText)
    {
        if (low.HasValue && high.HasValue) return $"{Fmt(low.Value)} – {Fmt(high.Value)}";
        if (high.HasValue) return $"< {Fmt(high.Value)}";
        if (low.HasValue) return $"> {Fmt(low.Value)}";
        return normText ?? "";
    }

    private static string Fmt(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static (bool, string) Evaluate(ReferenceLayerInput layer, PatientContext ctx)
    {
        var type = (layer.LayerType ?? LayerTypes.Baseline).ToUpperInvariant();
        var gender = (ctx.Gender ?? "U").ToUpperInvariant();

        if (type == LayerTypes.Baseline)
            return (true, "Універсальний базовий референс методики");

        // Загальні умови Simplex (is_gender / is_age) діють для всіх типів, окрім BASELINE.
        if (layer.IsGender && !string.Equals(layer.Gender, "ANY", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(layer.Gender, gender, StringComparison.OrdinalIgnoreCase))
            return (false, $"Стать '{gender}' не відповідає фільтру шару '{layer.Gender}'");

        if (layer.IsAge)
        {
            var from = AgeUnits.ToDays(layer.AgeFrom, layer.AgeUnit);
            var toExclusive = AgeUnits.ToDays(layer.AgeTo + 1, layer.AgeUnit);
            if (ctx.AgeDays < from || ctx.AgeDays >= toExclusive)
                return (false, $"Вік {Math.Floor(ctx.AgeDays)} дн. поза межами [{layer.AgeFrom}–{layer.AgeTo} {layer.AgeUnit}]");
        }

        switch (type)
        {
            case LayerTypes.Pregnancy:
                if (!ctx.IsPregnant) return (false, "Пацієнтка не вагітна");
                if (layer.PregnancyWeekFrom.HasValue || layer.PregnancyWeekTo.HasValue)
                {
                    if (!ctx.PregnancyWeek.HasValue) return (false, "Термін вагітності не вказано");
                    var w = ctx.PregnancyWeek.Value;
                    if ((layer.PregnancyWeekFrom.HasValue && w < layer.PregnancyWeekFrom.Value) ||
                        (layer.PregnancyWeekTo.HasValue && w > layer.PregnancyWeekTo.Value))
                        return (false, $"Термін {w} тиж. поза межами [{layer.PregnancyWeekFrom}–{layer.PregnancyWeekTo}] тиж.");
                }
                return (true, "Збіг за вагітністю та терміном");

            case LayerTypes.MenstrualPhase:
                if (gender != "F") return (false, "Застосовується лише до жінок");
                if (ctx.IsPregnant) return (false, "Перекрито пріоритетом вагітності");
                if (string.IsNullOrWhiteSpace(ctx.MenstrualPhase) ||
                    !string.Equals(layer.MenstrualPhase, ctx.MenstrualPhase, StringComparison.OrdinalIgnoreCase))
                    return (false, $"Фаза циклу '{ctx.MenstrualPhase ?? "не вказано"}' ≠ '{layer.MenstrualPhase}'");
                return (true, "Збіг за фазою циклу");

            case LayerTypes.ClinicalIcd10:
                if (string.IsNullOrWhiteSpace(ctx.Icd10Code) || string.IsNullOrWhiteSpace(layer.Icd10Code))
                    return (false, "Діагноз МКХ-10 не вказано");
                // Код шару «E11» покриває уточнені «E11.9».
                if (!ctx.Icd10Code.StartsWith(layer.Icd10Code, StringComparison.OrdinalIgnoreCase))
                    return (false, $"Діагноз '{ctx.Icd10Code}' не входить до когорти '{layer.Icd10Code}'");
                return (true, "Збіг за клінічною когортою МКХ-10");

            default: // DEMOGRAPHIC
                return (true, "Збіг за статтю та віком");
        }
    }
}
