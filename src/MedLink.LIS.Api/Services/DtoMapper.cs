// Мапінг сутностей → DTO (з allowedActions за машиною станів та роллю)
using System.Globalization;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Core.Clinical;

namespace MedLink.LIS.Api.Services;

public static class DtoMapper
{
    public static PatientDto ToDto(MisPatientCard p, DateTime? at = null) => MedLinkPeople.ToDto(p, at);

    public static string AgeGender(MisPatientCard? p, DateTime at) => p == null ? "" : AgeGender(p.Birthday, p.Gender, at);

    public static string AgeGender(DateTime? birthday, string? genderCode, DateTime at)
    {
        var g = genderCode switch { "M" => "Ч", "F" => "Ж", _ => "—" };
        if (!birthday.HasValue) return g;
        var days = AgeUnits.AgeDays(birthday.Value, at);
        string age = days < 60 ? $"{(int)days} дн." : days < 730 ? $"{(int)(days / 30.4375)} міс." : $"{AgeUnits.AgeYears(birthday.Value, at)} р.";
        return $"{age}, {g}";
    }

    public static string FormatValue(double? numeric, string? text, int decimals)
    {
        if (numeric.HasValue) return numeric.Value.ToString("F" + Math.Clamp(decimals, 0, 6), CultureInfo.InvariantCulture);
        return text ?? "";
    }

    public static ResultDto ToDto(LabTestResult r, int decimals = 2, string? verifiedByName = null) => new()
    {
        Id = r.Id, NumericValue = r.NumericValue, StringValue = r.StringValue, DisplayValue = FormatValue(r.NumericValue, r.StringValue, decimals),
        Unit = r.Unit, NormLow = r.NormLow, NormHigh = r.NormHigh, CritLow = r.CritLow, CritHigh = r.CritHigh, ReferenceDisplay = r.ReferenceDisplay,
        Flag = r.Flag, AppliedLayerId = r.AppliedLayerId, AppliedLayerName = r.AppliedLayerName, DeltaPercent = r.DeltaPercent, DeltaAlert = r.DeltaAlert,
        PreviousValue = r.PreviousValue, PreviousAt = r.PreviousAt, AnalyzerId = r.AnalyzerId, AnalyzerFlags = r.AnalyzerFlags, RawMessageId = r.RawMessageId,
        IsAutoVerified = r.IsAutoVerified, AutoVerifyBlockReason = r.AutoVerifyBlockReason, VerifiedById = r.VerifiedById, VerifiedByName = verifiedByName,
        VerifiedAt = r.VerifiedAt, VerificationComment = r.VerificationComment, OperatorComment = r.OperatorComment, ReportText = r.ReportText, EnteredById = r.EnteredById,
        EnteredAt = r.EnteredAt, Version = r.Version
    };

    public static OrderTestDto ToDto(LabOrderTest t, IRolePolicy policy) => new()
    {
        Id = t.Id, OrderId = t.OrderId, SampleId = t.SampleId, Barcode = t.Sample?.Barcode, ProfileId = t.ProfileId, ProfileCode = t.Profile?.Code,
        TestId = t.TestId, TestCode = t.TestCode, TestName = t.TestName, Unit = t.Test?.Unit, ResultType = t.Test?.ResultType ?? "NUMERIC",
        DropdownOptions = t.Test?.ResultType == "DROPDOWN" ? t.Test.DropdownOptions : null,
        Status = t.Status, AssignedAnalyzerId = t.AssignedAnalyzerId, AnalyzerName = t.AssignedAnalyzer?.Name ?? t.Result?.Analyzer?.Name,
        IsReflex = t.IsReflex, ReflexFromTestId = t.ReflexFromTestId, ReflexNeedsConfirmation = t.ReflexNeedsConfirmation, RejectReason = t.RejectReason,
        DisplayOrder = t.DisplayOrder, ReleasedAt = t.ReleasedAt, LabSectionId = t.Test?.LabSectionId, LabSectionName = t.Test?.LabSection?.Name,
        PerformerId = t.PerformerId, PerformerName = t.Performer?.Name,
        Result = t.Result == null ? null : ToDto(t.Result, t.Test?.DecimalPlaces ?? 2),
        AllowedActions = policy.AllowedActions(LisEntities.OrderTest, t.Status)
    };

    public static SampleDto ToDto(LabOrderSample s, IEnumerable<LabOrderTest> tests, IRolePolicy policy) => new()
    {
        Id = s.Id, OrderId = s.OrderId, Barcode = s.Barcode, TubeTypeId = s.TubeTypeId, TubeTypeName = s.TubeType?.Name, TubeColor = s.TubeType?.ColorCode,
        BiomaterialTypeId = s.BiomaterialTypeId, BiomaterialName = s.BiomaterialType?.Name, GroupNumb = s.GroupNumb, Status = s.Status,
        ParentSampleId = s.ParentSampleId, DerivationType = s.DerivationType, DerivationIndex = s.DerivationIndex, ContainerType = s.ContainerType, LabSectionId = s.LabSectionId, CurrentStage = s.CurrentStage,
        CollectedAt = s.CollectedAt, CollectedById = s.CollectedById, ReceivedAt = s.ReceivedAt, VolumeMl = s.VolumeMl,
        IsHemolyzed = s.IsHemolyzed, IsLipemic = s.IsLipemic, IsIcteric = s.IsIcteric, IsClotted = s.IsClotted, IsInsufficientVolume = s.IsInsufficientVolume,
        RejectReason = s.RejectReason, TestCodes = tests.Where(t => t.SampleId == s.Id).Select(t => t.TestCode).ToList(),
        OrderOfDrawIndex = s.TubeType?.OrderOfDrawIndex ?? 99,
        PlannedVolumeMl = tests.Where(t => t.SampleId == s.Id && t.Test?.RequiredVolumeMl > 0).Sum(t => t.Test!.RequiredVolumeMl) is double v && v > 0 ? Math.Round(v, 3) : null,
        CapacityMl = s.TubeType == null ? null : TubePlanService.CapacityOf(s.TubeType),
        PlanReasons = string.IsNullOrEmpty(s.PlanReasons) ? new() : s.PlanReasons.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
        AllowedActions = policy.AllowedActions(LisEntities.Sample, s.Status)
    };

    public static OrderDto ToDto(LabOrder o, IRolePolicy policy) => new()
    {
        Id = o.Id, OrderNumber = o.OrderNumber, PatientId = o.PatientId, Patient = o.Patient == null ? null : ToDto(o.Patient, o.OrderDatetime),
        DoctorId = o.DoctorId, DoctorName = o.Doctor?.Caption, DepartmentId = o.DepartmentId, DepartmentName = o.Department?.Caption,
        OrderDatetime = o.OrderDatetime, Status = o.Status, IsUrgentCito = o.IsUrgentCito, EhealthReferralId = o.EhealthReferralId, ReferralType = o.ReferralType, ReferralTypeName = ReferralTypes.Label(o.ReferralType), ReferralNumber = o.Referral?.RegNumber ?? o.PaperReferral?.RegNumber ?? o.ReferrerNumber, ReferralStatus = o.Referral?.Status?.Caption, ReferrerOrganizationName = o.ReferrerOrganizationName, ReferrerDoctorName = o.ReferrerDoctorName ?? (o.ReferralType == ReferralTypes.Internal ? o.Doctor?.Caption : null), ClinicalNotes = o.ClinicalNotes,
        IsPregnant = o.IsPregnant, PregnancyWeek = o.PregnancyWeek, MenstrualPhase = o.MenstrualPhase, Icd10Code = o.Icd10Code, TotalPrice = o.TotalPrice,
        CreatedById = o.CreatedById, CreatedOn = o.CreatedOn, CompletedAt = o.CompletedAt, ReleasedAt = o.ReleasedAt, VerifyToken = o.VerifyToken, CancelReason = o.CancelReason,
        Samples = o.Samples.OrderBy(s => s.GroupNumb).Select(s => ToDto(s, o.Tests, policy)).ToList(),
        Tests = o.Tests.OrderBy(t => t.DisplayOrder).ThenBy(t => t.CreatedOn).Select(t => ToDto(t, policy)).ToList(),
        AllowedActions = policy.AllowedActions(LisEntities.Order, o.Status)
    };

    public static WorklistRowDto ToWorklistRow(LabOrderTest t, bool isLockedOut, IRolePolicy policy, string? journalNumber = null)
    {
        var r = t.Result;
        var decimals = t.Test?.DecimalPlaces ?? 2;
        return new WorklistRowDto
        {
            OrderTestId = t.Id, ResultId = r?.Id, OrderId = t.OrderId, OrderNumber = t.Order?.OrderNumber ?? "", Barcode = t.Sample?.Barcode,
            PatientId = t.Order?.PatientId ?? "", PatientName = t.Order?.Patient?.Caption ?? "", PatientAgeGender = AgeGender(t.Order?.Patient, t.Order?.OrderDatetime ?? DateTime.UtcNow),
            TestCode = t.TestCode, TestName = t.TestName,
            Value = r == null ? null : FormatValue(r.NumericValue, r.StringValue, decimals), NumericValue = r?.NumericValue, StringValue = r?.StringValue,
            Unit = r?.Unit ?? t.Test?.Unit, NormLow = r?.NormLow, NormHigh = r?.NormHigh, ReferenceDisplay = r?.ReferenceDisplay, Flag = r?.Flag ?? "NONE",
            DeltaPercent = r?.DeltaPercent, DeltaAlert = r?.DeltaAlert ?? false, Status = t.Status, OrderStatus = t.Order?.Status ?? "",
            AnalyzerId = r?.AnalyzerId ?? t.AssignedAnalyzerId, AnalyzerName = r?.Analyzer?.Name ?? t.AssignedAnalyzer?.Name,
            LabSectionId = t.Test?.LabSectionId, LabSectionName = t.Test?.LabSection?.Name, JournalNumber = journalNumber, ReleasedAt = t.ReleasedAt,
            IsCito = t.Order?.IsUrgentCito ?? false, IsAutoVerified = r?.IsAutoVerified ?? false, IsLockedOut = isLockedOut, IsReflex = t.IsReflex,
            AutoVerifyBlockReason = r?.AutoVerifyBlockReason, EnteredAt = r?.EnteredAt, VerifiedAt = r?.VerifiedAt, OrderDatetime = t.Order?.OrderDatetime ?? DateTime.UtcNow,
            AllowedActions = policy.AllowedActions(LisEntities.OrderTest, t.Status)
        };
    }
}
