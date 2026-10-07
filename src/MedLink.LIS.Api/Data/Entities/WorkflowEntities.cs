// Робочий процес: замовлення, проби, тести, результати, історія, панічні дзвінки, батчі
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace MedLink.LIS.Api.Data.Entities;

public static class OrderStatuses
{
    public const string New = "NEW", Collected = "COLLECTED", InTransit = "IN_TRANSIT", Received = "RECEIVED",
        InProgress = "IN_PROGRESS", PartiallyCompleted = "PARTIALLY_COMPLETED", Completed = "COMPLETED", Released = "RELEASED", Cancelled = "CANCELLED", Rejected = "REJECTED";
    public static readonly string[] Terminal = { Cancelled, Rejected };
    /// <summary>Статуси, у яких триває робота з результатами.</summary>
    public static readonly string[] Working = { InProgress, PartiallyCompleted };
    public static readonly string[] All = { New, Collected, InTransit, Received, InProgress, PartiallyCompleted, Completed, Released, Cancelled, Rejected };
}

public static class SampleStatuses
{
    public const string Pending = "PENDING", Collected = "COLLECTED", InTransit = "IN_TRANSIT", Received = "RECEIVED",
        Processing = "PROCESSING", Stored = "STORED", Disposed = "DISPOSED", Rejected = "REJECTED";
}

public static class OrderTestStatuses
{
    public const string Pending = "PENDING", InAnalysis = "IN_ANALYSIS", Resulted = "RESULTED", NeedsReview = "NEEDS_REVIEW",
        AutoVerified = "AUTO_VERIFIED", Verified = "VERIFIED", Rejected = "REJECTED", Rerun = "RERUN",
        /// <summary>Відправлено на виконання в зовнішню лабораторію, очікується результат.</summary>
        SentOut = "SENT_OUT";
    public static readonly string[] Final = { AutoVerified, Verified, Rejected };
    public static readonly string[] VerifiedAny = { AutoVerified, Verified };
    public static readonly string[] AllowsResultEntry = { Pending, InAnalysis, Resulted, NeedsReview, Rerun, SentOut };
}

[Table("lab_order")]
public class LabOrder : GuidEntity
{
    [MaxLength(32)] public string OrderNumber { get; set; } = "";
    [MaxLength(64)] public string PatientId { get; set; } = "";
    [MaxLength(64)] public string? DoctorId { get; set; }
    [MaxLength(64)] public string? DepartmentId { get; set; }
    public DateTime OrderDatetime { get; set; } = DateTime.UtcNow;
    [MaxLength(32)] public string Status { get; set; } = OrderStatuses.New;
    public bool IsUrgentCito { get; set; }
    [MaxLength(64)] public string? EhealthReferralId { get; set; }
    public string? ClinicalNotes { get; set; }
    // Клінічний контекст для каскаду норм
    public bool IsPregnant { get; set; }
    public int? PregnancyWeek { get; set; }
    [MaxLength(32)] public string? MenstrualPhase { get; set; }
    [MaxLength(16)] public string? Icd10Code { get; set; }
    public decimal TotalPrice { get; set; }
    [MaxLength(64)] public string? CreatedById { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    [MaxLength(64)] public string? ReleasedById { get; set; }
    [MaxLength(128)] public string? VerifyToken { get; set; }
    [MaxLength(512)] public string? CancelReason { get; set; }
    [MaxLength(64)] public string? RepeatOfOrderId { get; set; }
    /// <summary>Заклад MedLink (org_organization) — для розмежування даних (RLS evomis).</summary>
    [MaxLength(64)] public string? OrganizationId { get; set; }
    /// <summary>Медичний висновок MedLink (mis_diagnostic_report), створений при видачі.</summary>
    [MaxLength(64)] public string? DiagnosticReportId { get; set; }

    [ForeignKey(nameof(PatientId))] public MisPatientCard? Patient { get; set; }
    [ForeignKey(nameof(DiagnosticReportId))] public MisDiagnosticReport? DiagnosticReport { get; set; }
    [ForeignKey(nameof(DoctorId))] public OrgEmployee? Doctor { get; set; }
    [ForeignKey(nameof(DepartmentId))] public OrgDepartment? Department { get; set; }
    [ForeignKey(nameof(EhealthReferralId))] public EheIncomingMedicalReferral? Referral { get; set; }
    public List<LabOrderSample> Samples { get; set; } = new();
    public List<LabOrderTest> Tests { get; set; } = new();
}

[Table("lab_order_sample")]
public class LabOrderSample : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(32)] public string Barcode { get; set; } = "";
    public int TubeTypeId { get; set; }
    public int BiomaterialTypeId { get; set; }
    /// <summary>group_numb Simplex — різні штрихкоди при однаковому tube_type_id</summary>
    public int GroupNumb { get; set; }
    /// <summary>Пояснення плану пробірок для медсестри: чому пробірка окрема (коди TubePlanReasons через кому).</summary>
    [MaxLength(128)] public string? PlanReasons { get; set; }
    // --- Преаналітична простежуваність (ISO 15189): батьківська/дочірня проба ---
    [MaxLength(64)] public string? ParentSampleId { get; set; }
    /// <summary>PRIMARY|ALIQUOT|CASSETTE|BLOCK|SLIDE|CULTURE_PLATE|DILUTION</summary>
    [MaxLength(16)] public string DerivationType { get; set; } = "PRIMARY";
    public int DerivationIndex { get; set; }
    [MaxLength(64)] public string? ContainerType { get; set; }
    [MaxLength(64)] public string? LabSectionId { get; set; }
    /// <summary>Поточний етап обробки за шаблоном робочого процесу секції.</summary>
    [MaxLength(32)] public string? CurrentStage { get; set; }
    [MaxLength(32)] public string Status { get; set; } = SampleStatuses.Pending;
    public DateTime? CollectedAt { get; set; }
    [MaxLength(64)] public string? CollectedById { get; set; }
    public DateTime? ReceivedAt { get; set; }
    [MaxLength(64)] public string? ReceivedById { get; set; }
    public double? VolumeMl { get; set; }
    public bool IsHemolyzed { get; set; }
    public bool IsLipemic { get; set; }
    public bool IsIcteric { get; set; }
    public bool IsClotted { get; set; }
    public bool IsInsufficientVolume { get; set; }
    [MaxLength(512)] public string? RejectReason { get; set; }
    public string? CollectChecklistJson { get; set; }

    [ForeignKey(nameof(OrderId))] public LabOrder? Order { get; set; }
    [ForeignKey(nameof(TubeTypeId))] public LabTubeType? TubeType { get; set; }
    [ForeignKey(nameof(BiomaterialTypeId))] public LabBiomaterialType? BiomaterialType { get; set; }
    [ForeignKey(nameof(ParentSampleId))] public LabOrderSample? ParentSample { get; set; }
    [ForeignKey(nameof(LabSectionId))] public LabSection? LabSection { get; set; }
}

[Table("lab_order_test")]
public class LabOrderTest : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(64)] public string? SampleId { get; set; }
    [MaxLength(64)] public string? ProfileId { get; set; }
    [MaxLength(64)] public string TestId { get; set; } = "";
    [MaxLength(64)] public string TestCode { get; set; } = "";
    [MaxLength(256)] public string TestName { get; set; } = "";
    [MaxLength(32)] public string Status { get; set; } = OrderTestStatuses.Pending;
    [MaxLength(64)] public string? AssignedAnalyzerId { get; set; }
    public bool IsReflex { get; set; }
    [MaxLength(64)] public string? ReflexFromTestId { get; set; }
    /// <summary>Reflex потребує іншого біоматеріалу — чекає підтвердження лаборанта.</summary>
    public bool ReflexNeedsConfirmation { get; set; }
    [MaxLength(512)] public string? RejectReason { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Прогресивна видача: результат відкрито пацієнту (після верифікації, якщо секція має autoReleaseVerified).</summary>
    public DateTime? ReleasedAt { get; set; }
    /// <summary>Лабораторія-виконавець (lab_performer); null — власна лабораторія.</summary>
    [MaxLength(64)] public string? PerformerId { get; set; }

    [ForeignKey(nameof(OrderId))] public LabOrder? Order { get; set; }
    [ForeignKey(nameof(SampleId))] public LabOrderSample? Sample { get; set; }
    [ForeignKey(nameof(PerformerId))] public LabPerformer? Performer { get; set; }
    [ForeignKey(nameof(ProfileId))] public LabTestProfile? Profile { get; set; }
    [ForeignKey(nameof(TestId))] public LabTestDefinition? Test { get; set; }
    [ForeignKey(nameof(AssignedAnalyzerId))] public LabAnalyzer? AssignedAnalyzer { get; set; }
    public LabTestResult? Result { get; set; }
}

[Table("lab_test_result")]
public class LabTestResult : GuidEntity
{
    [MaxLength(64)] public string OrderTestId { get; set; } = "";
    public double? NumericValue { get; set; }
    [MaxLength(512)] public string? StringValue { get; set; }
    [MaxLength(64)] public string? Unit { get; set; }
    public double? NormLow { get; set; }
    public double? NormHigh { get; set; }
    public double? CritLow { get; set; }
    public double? CritHigh { get; set; }
    [MaxLength(256)] public string ReferenceDisplay { get; set; } = "";
    /// <summary>NORMAL|LOW|HIGH|CRIT_LOW|CRIT_HIGH|ABNORMAL|NONE</summary>
    [MaxLength(16)] public string Flag { get; set; } = "NONE";
    [MaxLength(64)] public string? AppliedLayerId { get; set; }
    [MaxLength(256)] public string? AppliedLayerName { get; set; }
    public double? DeltaPercent { get; set; }
    public bool DeltaAlert { get; set; }
    public double? PreviousValue { get; set; }
    public DateTime? PreviousAt { get; set; }
    [MaxLength(64)] public string? AnalyzerId { get; set; }
    [MaxLength(64)] public string? AnalyzerFlags { get; set; }
    [MaxLength(64)] public string? RawMessageId { get; set; }
    public bool IsAutoVerified { get; set; }
    [MaxLength(512)] public string? AutoVerifyBlockReason { get; set; }
    [MaxLength(64)] public string? VerifiedById { get; set; }
    public DateTime? VerifiedAt { get; set; }
    [MaxLength(1024)] public string? VerificationComment { get; set; }
    [MaxLength(1024)] public string? OperatorComment { get; set; }
    /// <summary>Структурований/вільний текст висновку (патогістологія, цитологія).</summary>
    public string? ReportText { get; set; }
    [MaxLength(64)] public string? EnteredById { get; set; }
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? MeasuredAt { get; set; }
    public int Version { get; set; } = 1;
    /// <summary>Лабораторія, що виконала дослідження (для результатів зовнішніх лабораторій).</summary>
    [MaxLength(64)] public string? PerformerId { get; set; }
    /// <summary>Номер/посилання на бланк зовнішньої лабораторії.</summary>
    [MaxLength(128)] public string? ExternalReference { get; set; }

    [ForeignKey(nameof(OrderTestId))] public LabOrderTest? OrderTest { get; set; }
    [ForeignKey(nameof(AnalyzerId))] public LabAnalyzer? Analyzer { get; set; }
}

[Table("lab_result_history")]
public class LabResultHistory : GuidEntity
{
    [MaxLength(64)] public string OrderTestId { get; set; } = "";
    [MaxLength(64)] public string ResultId { get; set; } = "";
    public int Version { get; set; }
    /// <summary>ENTERED | CORRECTED | VERIFIED | AUTO_VERIFIED | REJECTED | REOPENED | RERUN</summary>
    [MaxLength(32)] public string Action { get; set; } = "ENTERED";
    public double? NumericValue { get; set; }
    [MaxLength(512)] public string? StringValue { get; set; }
    [MaxLength(16)] public string? Flag { get; set; }
    [MaxLength(64)] public string? ActorId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    [MaxLength(1024)] public string? Comment { get; set; }
    public string? SnapshotJson { get; set; }
}

[Table("lab_panic_call")]
public class LabPanicCall : GuidEntity
{
    [MaxLength(64)] public string ResultId { get; set; } = "";
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(256)] public string PatientName { get; set; } = "";
    [MaxLength(64)] public string TestCode { get; set; } = "";
    [MaxLength(64)] public string Value { get; set; } = "";
    [MaxLength(256)] public string DoctorNotifiedName { get; set; } = "";
    [MaxLength(32)] public string Phone { get; set; } = "";
    [MaxLength(256)] public string? Department { get; set; }
    public bool ReadbackConfirmed { get; set; }
    [MaxLength(64)] public string NotifiedById { get; set; } = "";
    public DateTime NotifiedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(1024)] public string? Comments { get; set; }
}

[Table("lab_worklist_batch")]
public class LabWorklistBatch : GuidEntity
{
    [MaxLength(64)] public string BatchCode { get; set; } = "";
    [MaxLength(64)] public string? AnalyzerId { get; set; }
    /// <summary>OPEN | SENT | COMPLETED</summary>
    [MaxLength(16)] public string Status { get; set; } = "OPEN";
    [MaxLength(64)] public string? CreatedById { get; set; }
    public string ItemsJson { get; set; } = "[]";

    [NotMapped]
    public List<string> Items
    {
        get => JsonSerializer.Deserialize<List<string>>(ItemsJson) ?? new();
        set => ItemsJson = JsonSerializer.Serialize(value);
    }
}

[Table("lab_unmatched_result")]
public class LabUnmatchedResult : GuidEntity
{
    [MaxLength(64)] public string? AnalyzerId { get; set; }
    [MaxLength(64)] public string Barcode { get; set; } = "";
    [MaxLength(64)] public string AnalyzerCode { get; set; } = "";
    [MaxLength(128)] public string Value { get; set; } = "";
    [MaxLength(64)] public string? Unit { get; set; }
    [MaxLength(64)] public string? Flags { get; set; }
    public DateTime? MeasuredAt { get; set; }
    [MaxLength(64)] public string? RawMessageId { get; set; }
    [MaxLength(512)] public string Reason { get; set; } = "";
    public bool IsLinked { get; set; }
    [MaxLength(64)] public string? LinkedOrderTestId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

[Table("lab_patient_notification")]
public class LabPatientNotification : GuidEntity
{
    [MaxLength(64)] public string PatientId { get; set; } = "";
    [MaxLength(64)] public string? OrderId { get; set; }
    /// <summary>SMS | EMAIL | PUSH | VIBER</summary>
    [MaxLength(16)] public string Channel { get; set; } = "SMS";
    public string Payload { get; set; } = "";
    /// <summary>QUEUED | SENT | FAILED</summary>
    [MaxLength(16)] public string Status { get; set; } = "QUEUED";
    public DateTime? SentAt { get; set; }
}
