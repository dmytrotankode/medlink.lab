// DTO відповідей та запитів основного процесу (camelCase JSON)
using MedLink.LIS.Api.Domain;

namespace MedLink.LIS.Api.Models;

public sealed class PatientDto
{
    public string Id { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? SecondName { get; set; }
    public string FullName { get; set; } = "";
    public string? LastNameLatin { get; set; }
    public string? FirstNameLatin { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? AgeYears { get; set; }
    public string Gender { get; set; } = "U";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
}

public sealed class ResultDto
{
    public string Id { get; set; } = "";
    public double? NumericValue { get; set; }
    public string? StringValue { get; set; }
    public string? DisplayValue { get; set; }
    public string? Unit { get; set; }
    public double? NormLow { get; set; }
    public double? NormHigh { get; set; }
    public double? CritLow { get; set; }
    public double? CritHigh { get; set; }
    public string ReferenceDisplay { get; set; } = "";
    public string Flag { get; set; } = "NONE";
    public string? AppliedLayerId { get; set; }
    public string? AppliedLayerName { get; set; }
    public double? DeltaPercent { get; set; }
    public bool DeltaAlert { get; set; }
    public double? PreviousValue { get; set; }
    public DateTime? PreviousAt { get; set; }
    public string? AnalyzerId { get; set; }
    public string? AnalyzerFlags { get; set; }
    public string? RawMessageId { get; set; }
    public bool IsAutoVerified { get; set; }
    public string? AutoVerifyBlockReason { get; set; }
    public string? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerificationComment { get; set; }
    public string? OperatorComment { get; set; }
    public string? ReportText { get; set; }
    public string? EnteredById { get; set; }
    public DateTime EnteredAt { get; set; }
    public int Version { get; set; }
}

public sealed class OrderTestDto
{
    public string Id { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string? SampleId { get; set; }
    public string? Barcode { get; set; }
    public string? ProfileId { get; set; }
    public string? ProfileCode { get; set; }
    public string TestId { get; set; } = "";
    public string TestCode { get; set; } = "";
    public string TestName { get; set; } = "";
    public string? Unit { get; set; }
    public string ResultType { get; set; } = "NUMERIC";
    public List<string>? DropdownOptions { get; set; }
    public string Status { get; set; } = "";
    public string? AssignedAnalyzerId { get; set; }
    public string? AnalyzerName { get; set; }
    public bool IsReflex { get; set; }
    public string? ReflexFromTestId { get; set; }
    public bool ReflexNeedsConfirmation { get; set; }
    public string? RejectReason { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string? LabSectionId { get; set; }
    public string? LabSectionName { get; set; }
    public string? JournalNumber { get; set; }
    public ResultDto? Result { get; set; }
    public List<AllowedActionDto> AllowedActions { get; set; } = new();
}

public sealed class SampleDto
{
    public string Id { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string Barcode { get; set; } = "";
    public int TubeTypeId { get; set; }
    public string? TubeTypeName { get; set; }
    public string? TubeColor { get; set; }
    public int BiomaterialTypeId { get; set; }
    public string? BiomaterialName { get; set; }
    public int GroupNumb { get; set; }
    /// <summary>Порядок забору (CLSI) типу тари.</summary>
    public int OrderOfDrawIndex { get; set; }
    /// <summary>Сумарний об'єм матеріалу, потрібний тестам пробірки, мл (FR-PRE-004).</summary>
    public double? PlannedVolumeMl { get; set; }
    public double? CapacityMl { get; set; }
    /// <summary>Коди причин окремої пробірки (TubePlanReasons).</summary>
    public List<string> PlanReasons { get; set; } = new();
    public string? ParentSampleId { get; set; }
    public string DerivationType { get; set; } = "PRIMARY";
    public int DerivationIndex { get; set; }
    public string? ContainerType { get; set; }
    public string? LabSectionId { get; set; }
    public string? CurrentStage { get; set; }
    public string Status { get; set; } = "";
    public DateTime? CollectedAt { get; set; }
    public string? CollectedById { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public double? VolumeMl { get; set; }
    public bool IsHemolyzed { get; set; }
    public bool IsLipemic { get; set; }
    public bool IsIcteric { get; set; }
    public bool IsClotted { get; set; }
    public bool IsInsufficientVolume { get; set; }
    public string? RejectReason { get; set; }
    public List<string> TestCodes { get; set; } = new();
    public List<AllowedActionDto> AllowedActions { get; set; } = new();
}

public sealed class OrderDto
{
    public string Id { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public string PatientId { get; set; } = "";
    public PatientDto? Patient { get; set; }
    public string? DoctorId { get; set; }
    public string? DoctorName { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime OrderDatetime { get; set; }
    public string Status { get; set; } = "";
    public bool IsUrgentCito { get; set; }
    public string? EhealthReferralId { get; set; }
    public string? ClinicalNotes { get; set; }
    public bool IsPregnant { get; set; }
    public int? PregnancyWeek { get; set; }
    public string? MenstrualPhase { get; set; }
    public string? Icd10Code { get; set; }
    public decimal TotalPrice { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string? VerifyToken { get; set; }
    public string? CancelReason { get; set; }
    public List<SampleDto> Samples { get; set; } = new();
    public List<OrderTestDto> Tests { get; set; } = new();
    public List<AllowedActionDto> AllowedActions { get; set; } = new();
}

public sealed class OrderListItemDto
{
    public string Id { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public string PatientId { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string? PatientAgeGender { get; set; }
    public DateTime OrderDatetime { get; set; }
    public string Status { get; set; } = "";
    public bool IsUrgentCito { get; set; }
    public string? DepartmentName { get; set; }
    public string? DoctorName { get; set; }
    public int TestsTotal { get; set; }
    public int TestsVerified { get; set; }
    public int SamplesCount { get; set; }
    public bool HasCritical { get; set; }
    public decimal TotalPrice { get; set; }
    public List<AllowedActionDto> AllowedActions { get; set; } = new();
}

public sealed class WorklistRowDto
{
    public string OrderTestId { get; set; } = "";
    public string? ResultId { get; set; }
    public string OrderId { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public string? Barcode { get; set; }
    public string PatientId { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string PatientAgeGender { get; set; } = "";
    public string TestCode { get; set; } = "";
    public string TestName { get; set; } = "";
    public string? Value { get; set; }
    public double? NumericValue { get; set; }
    public string? StringValue { get; set; }
    public string? Unit { get; set; }
    public double? NormLow { get; set; }
    public double? NormHigh { get; set; }
    public string? ReferenceDisplay { get; set; }
    public string Flag { get; set; } = "NONE";
    public double? DeltaPercent { get; set; }
    public bool DeltaAlert { get; set; }
    public string Status { get; set; } = "";
    public string OrderStatus { get; set; } = "";
    public string? AnalyzerId { get; set; }
    public string? AnalyzerName { get; set; }
    public string? LabSectionId { get; set; }
    public string? LabSectionName { get; set; }
    public string? JournalNumber { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public bool IsCito { get; set; }
    public bool IsAutoVerified { get; set; }
    public bool IsLockedOut { get; set; }
    public bool IsReflex { get; set; }
    public string? AutoVerifyBlockReason { get; set; }
    public DateTime? EnteredAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime OrderDatetime { get; set; }
    public List<AllowedActionDto> AllowedActions { get; set; } = new();
}

// ---------------------------------------------------------------- requests
public sealed class NewPatientRequest
{
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? SecondName { get; set; }
    public DateTime? BirthDate { get; set; }
    public string Gender { get; set; } = "U";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
}

public sealed class CreateOrderRequest
{
    public string? PatientId { get; set; }
    public NewPatientRequest? NewPatient { get; set; }
    public string? DoctorId { get; set; }
    public string? DepartmentId { get; set; }
    public bool IsUrgentCito { get; set; }
    public string? EhealthReferralId { get; set; }
    public string? ClinicalNotes { get; set; }
    public bool IsPregnant { get; set; }
    public int? PregnancyWeek { get; set; }
    public string? MenstrualPhase { get; set; }
    public string? Icd10Code { get; set; }
    /// <summary>Ідентифікатори або коди профілів.</summary>
    public List<string> ProfileIds { get; set; } = new();
    /// <summary>Ідентифікатори або коди тестів.</summary>
    public List<string> TestIds { get; set; } = new();
    public DateTime? OrderDatetime { get; set; }
}

public sealed class UpdateOrderRequest
{
    public string? DoctorId { get; set; }
    public string? DepartmentId { get; set; }
    public bool? IsUrgentCito { get; set; }
    public string? EhealthReferralId { get; set; }
    public string? ClinicalNotes { get; set; }
    public bool? IsPregnant { get; set; }
    public int? PregnancyWeek { get; set; }
    public string? MenstrualPhase { get; set; }
    public string? Icd10Code { get; set; }
}

public sealed class AddTestsRequest
{
    public List<string> TestIds { get; set; } = new();
    public List<string> ProfileIds { get; set; } = new();
}

public sealed class AddSampleRequest
{
    public int TubeTypeId { get; set; }
    public int BiomaterialTypeId { get; set; }
    /// <summary>Тести, які перенести на нову пробірку (необов'язково).</summary>
    public List<string> OrderTestIds { get; set; } = new();
}

public sealed class ReasonRequest
{
    public string Reason { get; set; } = "";
}

public sealed class CollectChecklist
{
    public bool IdVerified { get; set; } = true;
    public bool Fasting { get; set; } = true;
    public bool OrderOfDraw { get; set; } = true;
    public bool Mixing { get; set; } = true;
}

public sealed class CollectSampleRequest
{
    public CollectChecklist? Checklist { get; set; }
    public double? VolumeMl { get; set; }
    public DateTime? CollectedAt { get; set; }
}

public sealed class ReceiveSampleRequest
{
    public bool IsHemolyzed { get; set; }
    public bool IsLipemic { get; set; }
    public bool IsIcteric { get; set; }
    public bool IsClotted { get; set; }
    public bool IsInsufficientVolume { get; set; }
    public string? Notes { get; set; }
}

public sealed class SampleFlagsRequest
{
    public bool? IsHemolyzed { get; set; }
    public bool? IsLipemic { get; set; }
    public bool? IsIcteric { get; set; }
    public bool? IsClotted { get; set; }
    public bool? IsInsufficientVolume { get; set; }
    public double? VolumeMl { get; set; }
    public int? TubeTypeId { get; set; }
    public int? BiomaterialTypeId { get; set; }
}

public sealed class RejectSampleRequest
{
    public string Reason { get; set; } = "";
    public bool CreateRepeatOrder { get; set; }
}

public sealed class ResultInputRequest
{
    public double? NumericValue { get; set; }
    public string? StringValue { get; set; }
    /// <summary>Текст висновку для патогістології/цитології (resultType REPORT).</summary>
    public string? ReportText { get; set; }
    public string? Comment { get; set; }
    public string? Unit { get; set; }
    public string? AnalyzerId { get; set; }
}

public sealed class VerifyRequest
{
    public string? Comment { get; set; }
    public bool Override { get; set; }
}

public sealed class VerifyBatchRequest
{
    public List<string> OrderTestIds { get; set; } = new();
    public string? Comment { get; set; }
}

public sealed class AutoVerifyRequest
{
    public List<string>? OrderTestIds { get; set; }
}

public sealed class AssignAnalyzerRequest
{
    public string? AnalyzerId { get; set; }
}

public sealed class TransitionRequest
{
    public string Status { get; set; } = "";
    public string? Comment { get; set; }
}

// ------------------------------------------------------------------ план пробірок (FR-PRE-004)
public sealed class TubePlanRequest
{
    public List<string> ProfileIds { get; set; } = new();
    public List<string> TestIds { get; set; } = new();
    /// <summary>Для дозамовлення: план з урахуванням наявних пробірок замовлення.</summary>
    public string? OrderId { get; set; }
}

public sealed class TubePlanTestDto
{
    public string TestId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public double? RequiredVolumeMl { get; set; }
    /// <summary>false — тест уже був у цій пробірці (дозамовлення).</summary>
    public bool IsNew { get; set; } = true;
}

public sealed class TubePlanItemDto
{
    public int Index { get; set; }
    public string? ExistingSampleId { get; set; }
    public string? ExistingBarcode { get; set; }
    public int TubeTypeId { get; set; }
    public string? TubeCode { get; set; }
    public string? TubeName { get; set; }
    public string? ColorCode { get; set; }
    public string? Anticoagulant { get; set; }
    public int InversionsCount { get; set; }
    public int OrderOfDrawIndex { get; set; }
    public int BiomaterialTypeId { get; set; }
    public string? BiomaterialName { get; set; }
    public string? CompatibilityGroup { get; set; }
    public bool IsSeparate { get; set; }
    public double? CapacityMl { get; set; }
    public double UsedVolumeMl { get; set; }
    public int? MaxTests { get; set; }
    public List<string> Reasons { get; set; } = new();
    public List<string> ReasonTexts { get; set; } = new();
    public List<TubePlanTestDto> Tests { get; set; } = new();
}

public sealed class TubePlanDto
{
    public List<TubePlanItemDto> Tubes { get; set; } = new();
    public int NewTubesCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}
