// =============================================================================
// ТЗ розд. 21 (ред. 4.1) і 23 (ред. 4.3): інтеграція з МІС — словник інтервенцій ЕСОЗ, співставлення з послугами ЛІС,
// послуги обладнання, позиції направлення й обрані лікарем показники, маршрутизація/розподіл у черги приладів,
// події для МІС, пакети послуг, сесія ЕСОЗ і відправка DiagnosticReport з підписом КЕП, агрегати й журнал звітів.
// =============================================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

/// <summary>Контур направлення (FR-MIS, розд. 21.1): зовнішнє — амбулаторне е-направлення ЕСОЗ; внутрішнє — призначення стаціонару/поліклініки МІС.</summary>
public static class ReferralContexts
{
    public const string Ambulatory = "AMBULATORY", Inpatient = "INPATIENT";
    public static readonly string[] All = { Ambulatory, Inpatient };
    public static string Label(string? c) => c switch { Ambulatory => "Амбулаторія", Inpatient => "Стаціонар", _ => "—" };
}

/// <summary>Джерело направлення для розрізів і звітів (розд. 23.5): стаціонар, поліклініка (внутрішнє), е-направлення ЕСОЗ, клініка-партнер, інше.</summary>
public static class ReferralSources
{
    public const string Inpatient = "INPATIENT", Polyclinic = "POLYCLINIC", Ehealth = "EHEALTH", Partner = "PARTNER", Other = "OTHER";
    public static readonly string[] All = { Inpatient, Polyclinic, Ehealth, Partner, Other };
    public static string Label(string s) => s switch
    {
        Inpatient => "Стаціонар", Polyclinic => "Поліклініка", Ehealth => "Е-направлення ЕСОЗ", Partner => "Клініка-партнер", _ => "Інше"
    };

    public static string Of(LabOrder o) =>
        o.ReferralType == ReferralTypes.Ehealth ? Ehealth
        : o.ReferralType == ReferralTypes.ExternalClinic ? Partner
        : o.ReferralContext == ReferralContexts.Inpatient ? Inpatient
        : o.ReferralType == ReferralTypes.Internal ? Polyclinic
        : Other;
}

/// <summary>Режим вибору показників на співставленні (FR-MIS-011).</summary>
public static class SelectionModes
{
    public const string Fixed = "FIXED", Selectable = "SELECTABLE", SelectableAny = "SELECTABLE_ANY";
    public static readonly string[] All = { Fixed, Selectable, SelectableAny };
    public static string Label(string m) => m switch
    {
        Fixed => "Фіксований склад", Selectable => "Лікар обирає (є обов'язкові)", _ => "Лікар обирає довільно"
    };
}

/// <summary>Хто обрав показники позиції (FR-MIS-022).</summary>
public static class SelectionSources
{
    public const string ReferringDoctor = "REFERRING_DOCTOR", Registrar = "REGISTRAR", LabDoctor = "LAB_DOCTOR", Fixed = "FIXED";
}

/// <summary>Готовність інтервенції до маршрутизації (FR-MIS-014).</summary>
public static class Readiness
{
    public const string Unmapped = "UNMAPPED", NoEquipment = "NO_EQUIPMENT", Partial = "PARTIAL", Ready = "READY";
    public static string Label(string r) => r switch
    {
        Unmapped => "Не співставлено", NoEquipment => "Немає обладнання", Partial => "Частково", _ => "Готово"
    };
}

/// <summary>Стан маршрутизації тесту (FR-RTE-001, FR-DST-008).</summary>
public static class RoutingStatuses
{
    public const string Proposed = "PROPOSED", Routed = "ROUTED", NoEquipment = "NO_EQUIPMENT", SendOut = "SENDOUT", Manual = "MANUAL";
}

/// <summary>Тип запису журналу маршрутизації (розд. 23.8).</summary>
public static class RoutingEvents
{
    public const string Propose = "PROPOSE", Change = "CHANGE", Confirm = "CONFIRM", Reroute = "REROUTE", Route = "ROUTE";
}

/// <summary>Події ЛІС → МІС (FR-MIS-026, FR-PAT, FR-SVA-011/015, FR-PKG-010).</summary>
public static class MisEvents
{
    public const string Received = "RECEIVED", Collected = "COLLECTED", ResultReady = "RESULT_READY", Completed = "COMPLETED", Cancelled = "CANCELLED",
        CriticalResult = "CRITICAL_RESULT", ItemIndicatorsChanged = "ITEM_INDICATORS_CHANGED", CatalogChanged = "CATALOG_CHANGED",
        Rejected = "REJECTED", Query = "QUERY", StageChanged = "STAGE_CHANGED", AdditionalStudyOrdered = "ADDITIONAL_STUDY_ORDERED",
        ResultPreliminary = "RESULT_PRELIMINARY", ResultFinal = "RESULT_FINAL", Addendum = "ADDENDUM";

    public static string Label(string e) => e switch
    {
        Received => "Прийнято в роботу", Collected => "Матеріал отримано", ResultReady => "Результат готовий", Completed => "Направлення виконано", Cancelled => "Скасовано",
        CriticalResult => "Критичний результат", ItemIndicatorsChanged => "Змінено показники", CatalogChanged => "Змінено каталог", Rejected => "Відхилено", Query => "Запит лікаря",
        StageChanged => "Змінено етап", AdditionalStudyOrdered => "Призначено додаткове дослідження", ResultPreliminary => "Попередній результат", ResultFinal => "Остаточний результат", Addendum => "Доповнення", _ => e
    };
}

/// <summary>Статуси відправки DiagnosticReport в ЕСОЗ (розд. 21.13).</summary>
public static class EhealthSubmitStatuses
{
    public const string NotSent = "NOT_SENT", AuthRequired = "AUTH_REQUIRED", ReadyToSign = "READY_TO_SIGN", Signed = "SIGNED", Processing = "PROCESSING",
        Registered = "REGISTERED", Completed = "COMPLETED", Rejected = "REJECTED", Error = "ERROR", Annulled = "ANNULLED";
    public static readonly string[] CanSubmit = { NotSent, AuthRequired, Error, ReadyToSign, Signed, Rejected };
    public static string Label(string s) => s switch
    {
        NotSent => "ЕСОЗ: не надіслано", AuthRequired => "Потрібен вхід в ЕСОЗ", ReadyToSign => "Очікує підпис", Signed => "Підписано",
        Processing => "Обробляється в ЕСОЗ", Registered => "Звіт зареєстровано", Completed => "ЕСОЗ: надіслано, направлення погашено",
        Rejected => "Відхилено", Error => "Помилка відправки", Annulled => "Анульовано", _ => s
    };
}

/// <summary>Тип приладу (FR-SVA-006) і стан експлуатації (FR-SVA-005).</summary>
public static class AnalyzerKinds
{
    public const string Analyzer = "ANALYZER", Manual = "MANUAL";
}

public static class AnalyzerServiceStatuses
{
    public const string Active = "ACTIVE", Maintenance = "MAINTENANCE", Retired = "RETIRED";
}

/// <summary>[MedLink] Дзеркало словника інтервенцій ЕСОЗ (FR-MIS-001). Лише читання, синхронізація з ЕСОЗ/МІС.</summary>
[Table("lab_ehealth_intervention")]
public class LabEhealthIntervention : GuidEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(128)] public string CodeSystem { get; set; } = "eHealth/ICPC2/process";
    [MaxLength(512)] public string Name { get; set; } = "";
    [MaxLength(64)] public string Category { get; set; } = "laboratory_procedure";
    /// <summary>Група послуг МІС (напр. 21729 «Гістологія; шкіра») — для патоморфології (розд. 22.10).</summary>
    [MaxLength(32)] public string? ServiceGroup { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(32)] public string? SourceVersion { get; set; }
    public DateTime? SyncedOn { get; set; }
}

/// <summary>[ЛІС] Співставлення інтервенції ЕСОЗ з послугою ЛІС (профілем) або набором показників (FR-MIS-010…015).</summary>
[Table("lab_intervention_map")]
public class LabInterventionMap : GuidEntity
{
    [MaxLength(64)] public string InterventionId { get; set; } = "";
    /// <summary>Профіль (послуга ЛІС); null — набір окремих показників (lab_intervention_map_test).</summary>
    [MaxLength(64)] public string? ProfileId { get; set; }
    [MaxLength(16)] public string IndicatorSelectionMode { get; set; } = SelectionModes.Selectable;
    public int MinSelected { get; set; } = 1;
    /// <summary>Обчислена готовність на момент збереження (перераховується при змінах обладнання).</summary>
    [MaxLength(16)] public string Readiness { get; set; } = Entities.Readiness.Unmapped;
    /// <summary>Відділення патоморфології: позиція виконується модулем «Патоморфологія» (розд. 22), без маршрутизації.</summary>
    public bool IsPathology { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(512)] public string? Note { get; set; }

    [ForeignKey(nameof(InterventionId))] public LabEhealthIntervention? Intervention { get; set; }
    [ForeignKey(nameof(ProfileId))] public LabTestProfile? Profile { get; set; }
    public List<LabInterventionMapTest> Tests { get; set; } = new();
}

/// <summary>[ЛІС] Набір показників співставлення без профілю.</summary>
[Table("lab_intervention_map_test")]
public class LabInterventionMapTest : GuidEntity
{
    [MaxLength(64)] public string MapId { get; set; } = "";
    [MaxLength(64)] public string TestId { get; set; } = "";
    public bool IsDefault { get; set; } = true;
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }

    [ForeignKey(nameof(MapId))] public LabInterventionMap? Map { get; set; }
    [ForeignKey(nameof(TestId))] public LabTestDefinition? Test { get; set; }
}

/// <summary>[ЛІС] Послуга, яку виконує прилад (FR-EQP-001/002). Рівень показника — lab_analyzer_parameter_map.</summary>
[Table("lab_analyzer_service")]
public class LabAnalyzerService : GuidEntity
{
    [MaxLength(64)] public string AnalyzerId { get; set; } = "";
    [MaxLength(64)] public string ProfileId { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    /// <summary>1 — основний, 2 — резервний…</summary>
    public int Priority { get; set; } = 1;
    public int AvgTatMinutes { get; set; } = 30;
    public int? BiomaterialTypeId { get; set; }
    [MaxLength(512)] public string? Note { get; set; }

    [ForeignKey(nameof(AnalyzerId))] public LabAnalyzer? Analyzer { get; set; }
    [ForeignKey(nameof(ProfileId))] public LabTestProfile? Profile { get; set; }
}

/// <summary>[ЛІС] Позиція направлення: інтервенція ЕСОЗ → послуга ЛІС; обрані показники — lab_order_item_indicator (FR-REG-012).</summary>
[Table("lab_order_item")]
public class LabOrderItem : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(64)] public string? InterventionId { get; set; }
    [MaxLength(64)] public string? InterventionCode { get; set; }
    [MaxLength(512)] public string? InterventionName { get; set; }
    [MaxLength(64)] public string? ProfileId { get; set; }
    /// <summary>REFERRING_DOCTOR | REGISTRAR | LAB_DOCTOR | FIXED</summary>
    [MaxLength(32)] public string SelectionSource { get; set; } = SelectionSources.ReferringDoctor;
    [MaxLength(16)] public string SelectionMode { get; set; } = SelectionModes.Selectable;
    /// <summary>ACTIVE | CANCELLED | DONE | UNMAPPED</summary>
    [MaxLength(16)] public string Status { get; set; } = "ACTIVE";
    /// <summary>Ідентифікатор ServiceRequest ЕСОЗ (групове е-направлення — окремий на кожну позицію, FR-MIS-021).</summary>
    [MaxLength(64)] public string? EhealthServiceRequestId { get; set; }
    /// <summary>Номер позиції в пакеті МІС (розд. 23.8).</summary>
    public int? PackageItemNo { get; set; }
    public bool IsCito { get; set; }
    [MaxLength(1024)] public string? Comment { get; set; }
    public int DisplayOrder { get; set; }

    [ForeignKey(nameof(OrderId))] public LabOrder? Order { get; set; }
    [ForeignKey(nameof(InterventionId))] public LabEhealthIntervention? Intervention { get; set; }
    [ForeignKey(nameof(ProfileId))] public LabTestProfile? Profile { get; set; }
    public List<LabOrderItemIndicator> Indicators { get; set; } = new();
}

/// <summary>[ЛІС] Показник позиції: обраний лікарем (з тестом) або не обраний (для відображення «не замовлено лікарем»).</summary>
[Table("lab_order_item_indicator")]
public class LabOrderItemIndicator : GuidEntity
{
    [MaxLength(64)] public string ItemId { get; set; } = "";
    [MaxLength(64)] public string TestId { get; set; } = "";
    [MaxLength(64)] public string TestCode { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool IsSelected { get; set; } = true;
    /// <summary>Додано автоматично як складова розрахункового показника (FR-MIS-032).</summary>
    public bool IsAutoAdded { get; set; }
    [MaxLength(32)] public string? SelectedBy { get; set; }
    public DateTime? SelectedOn { get; set; }
    [MaxLength(64)] public string? OrderTestId { get; set; }

    [ForeignKey(nameof(ItemId))] public LabOrderItem? Item { get; set; }
}

/// <summary>[ЛІС] Журнал маршрутизації та розподілу (FR-RTE-001, FR-DST-012).</summary>
[Table("lab_routing_log")]
public class LabRoutingLog : GuidEntity
{
    [MaxLength(64)] public string OrderTestId { get; set; } = "";
    [MaxLength(64)] public string? OrderId { get; set; }
    [MaxLength(64)] public string? TestCode { get; set; }
    /// <summary>PROPOSE | CHANGE | CONFIRM | REROUTE | ROUTE</summary>
    [MaxLength(16)] public string Event { get; set; } = RoutingEvents.Route;
    /// <summary>Кандидати: [{analyzerId, name, priority, queue, available, reason}]</summary>
    public string? CandidatesJson { get; set; }
    [MaxLength(64)] public string? FromAnalyzerId { get; set; }
    [MaxLength(64)] public string? AnalyzerId { get; set; }
    [MaxLength(64)] public string? Rule { get; set; }
    /// <summary>AUTO | MANUAL</summary>
    [MaxLength(16)] public string RoutedBy { get; set; } = "AUTO";
    [MaxLength(512)] public string? Reason { get; set; }
    [MaxLength(64)] public string? ActorId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

/// <summary>[ЛІС] Вихідні події для МІС з повторами (FR-MIS-026).</summary>
[Table("lab_mis_event_log")]
public class LabMisEventLog : GuidEntity
{
    [MaxLength(64)] public string? OrderId { get; set; }
    [MaxLength(64)] public string? MisReferralId { get; set; }
    [MaxLength(64)] public string Event { get; set; } = "";
    public string? Payload { get; set; }
    /// <summary>QUEUED | SENDING | DELIVERED | FAILED | NOT_CONFIGURED (MisDeliveryStatuses). Id запису = ключ ідемпотентності доставки.</summary>
    [MaxLength(16)] public string Status { get; set; } = "QUEUED";
    public int Attempts { get; set; }
    [MaxLength(1024)] public string? LastError { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    /// <summary>HTTP-код останньої відповіді МІС.</summary>
    public int? HttpStatus { get; set; }
    [MaxLength(96)] public string? LockedBy { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>[ЛІС, дзеркало МІС] Пакет послуг (FR-PKG-001): набір послуг ЕСОЗ із типовими показниками.</summary>
[Table("lab_service_package")]
public class LabServicePackage : GuidEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>INPATIENT | AMBULATORY | ANY</summary>
    [MaxLength(16)] public string Context { get; set; } = "ANY";
    [MaxLength(256)] public string? Purpose { get; set; }
    public bool IsCito { get; set; }
    /// <summary>[{"serviceCode":"DEMO-LAB-CBC","indicators":["WBC",…]}]</summary>
    public string ItemsJson { get; set; } = "[]";
    [MaxLength(32)] public string? SourceVersion { get; set; }
    public DateTime? SyncedOn { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>[ЛІС] Сесія ЕСОЗ користувача (FR-SGN-002…004). Пароль не зберігається; токен — у зашифрованому вигляді.</summary>
[Table("lab_ehealth_session")]
public class LabEhealthSession : GuidEntity
{
    [MaxLength(64)] public string EmployeeId { get; set; } = "";
    [MaxLength(128)] public string Login { get; set; } = "";
    /// <summary>L1 — пароль; L2 — підтверджена особа (КЕП/2FA).</summary>
    [MaxLength(4)] public string AuthLevel { get; set; } = "L1";
    /// <summary>PASSWORD | PASSWORD+KEP | PASSWORD+TOTP</summary>
    [MaxLength(32)] public string AuthMethod { get; set; } = "PASSWORD";
    public string? TokenEnc { get; set; }
    public bool TotpEnabled { get; set; }
    [MaxLength(64)] public string? CertSerial { get; set; }
    [MaxLength(256)] public string? SignerName { get; set; }
    [MaxLength(16)] public string? SignerDrfo { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastActivityOn { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
}

/// <summary>[ЛІС] Відправка DiagnosticReport в ЕСОЗ (FR-SGN-005…010): підписаний пакет, завдання, результат.</summary>
[Table("lab_ehealth_submission")]
public class LabEhealthSubmission : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(64)] public string? DiagnosticReportId { get; set; }
    [MaxLength(32)] public string Status { get; set; } = EhealthSubmitStatuses.ReadyToSign;
    /// <summary>SUBMIT | ANNUL</summary>
    [MaxLength(16)] public string Kind { get; set; } = "SUBMIT";
    public string? PackageJson { get; set; }
    [MaxLength(128)] public string? PackageHash { get; set; }
    public string? SignedData { get; set; }
    [MaxLength(64)] public string? SignedBy { get; set; }
    [MaxLength(256)] public string? SignedByName { get; set; }
    public DateTime? SignedOn { get; set; }
    [MaxLength(64)] public string? CertSerial { get; set; }
    [MaxLength(64)] public string? JobId { get; set; }
    [MaxLength(64)] public string? EhealthReportId { get; set; }
    public int Attempts { get; set; }
    [MaxLength(1024)] public string? LastError { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(OrderId))] public LabOrder? Order { get; set; }
}

/// <summary>[ЛІС] Денні агрегати для звітів (FR-RPT-011).</summary>
[Table("lab_stat_daily")]
public class LabStatDaily : GuidEntity
{
    public DateTime Day { get; set; }
    [MaxLength(64)] public string? AnalyzerId { get; set; }
    [MaxLength(64)] public string? ProfileId { get; set; }
    [MaxLength(64)] public string? TestCode { get; set; }
    [MaxLength(16)] public string Source { get; set; } = ReferralSources.Other;
    [MaxLength(64)] public string? PackageCode { get; set; }
    [MaxLength(64)] public string? DepartmentId { get; set; }
    public bool IsCito { get; set; }
    public int Tests { get; set; }
    public int Reruns { get; set; }
    public int Rejected { get; set; }
    public double? TatP50 { get; set; }
    public double? TatP90 { get; set; }
}

/// <summary>[ЛІС] Журнал формування звітів (FR-RPT-012) — для звітів з персональними даними це журнал доступу.</summary>
[Table("lab_report_run")]
public class LabReportRun : GuidEntity
{
    [MaxLength(32)] public string ReportType { get; set; } = "";
    public string? ParamsJson { get; set; }
    [MaxLength(64)] public string? PatientId { get; set; }
    [MaxLength(64)] public string? RunBy { get; set; }
    public DateTime RunAt { get; set; } = DateTime.UtcNow;
    [MaxLength(8)] public string Format { get; set; } = "json";
    public int Rows { get; set; }
}
