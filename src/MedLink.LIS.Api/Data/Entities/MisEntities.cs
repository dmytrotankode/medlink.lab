// =============================================================================
// [MedLink] — дзеркальні таблиці МІС MedLink (evomis) у тій самій структурі, що й в evomis
// (джерело: evomis/src/App.Data/Migrations/ApiDbContextModelSnapshot.cs). Відтворено підмножину
// колонок, потрібну ЛІС; назви таблиць/колонок і семантика збігаються. Ключі — uuid (у SQLite
// зберігаються як рядок GUID). Під час перенесення ці класи замінюються моделями evomis, а
// ЛІС-таблиці посилаються на реальні таблиці.
// [MedLink+] — колонки, які ЛІС додає до таблиці evomis (позначено в коментарі до властивості).
// =============================================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

/// <summary>[MedLink] cmn_enum_record — універсальний довідник переліків evomis (Gender, DepartmentType, OrgPositionType, PatientCardType…).</summary>
[Table("cmn_enum_record")]
public class CmnEnumRecord : GuidEntity
{
    public string? Caption { get; set; }
    public string? Code { get; set; }
    public string? EnumType { get; set; }
    [MaxLength(64)] public string? ParentId { get; set; }
    public int ValueType { get; set; }
}

/// <summary>[MedLink] cmn_person — фізична особа (ПІБ, дата народження, стать, ІПН, контакти).</summary>
[Table("cmn_person")]
public class CmnPerson : GuidEntity
{
    [MaxLength(100)] public string? LastName { get; set; }
    /// <summary>Ім'я.</summary>
    [MaxLength(100)] public string? Name { get; set; }
    /// <summary>По батькові.</summary>
    [MaxLength(100)] public string? MiddleName { get; set; }
    public DateTime? Birthday { get; set; }
    [MaxLength(64)] public string? GenderId { get; set; }
    /// <summary>РНОКПП (ІПН).</summary>
    [MaxLength(10)] public string? Ipn { get; set; }
    public bool NoIpn { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(129)] public string? Email { get; set; }
    /// <summary>Адреса.</summary>
    [MaxLength(250)] public string? Location { get; set; }
    [MaxLength(300)] public string? Caption { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    public bool SubscribedToNotifications { get; set; }

    [ForeignKey(nameof(GenderId))] public CmnEnumRecord? Gender { get; set; }

    public static string BuildCaption(string? last, string? name, string? middle) =>
        string.Join(" ", new[] { last, name, middle }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
}

/// <summary>[MedLink] org_organization — заклад (юридична особа).</summary>
[Table("org_organization")]
public class OrgOrganization : GuidEntity
{
    public string? Caption { get; set; }
    [MaxLength(400)] public string? Code { get; set; }
    public string? FullName { get; set; }
    public string? Description { get; set; }
}

/// <summary>[MedLink] org_department — підрозділ закладу. Лабораторні ознаки підрозділу — у [ЛІС] lab_department_settings.</summary>
[Table("org_department")]
public class OrgDepartment : GuidEntity
{
    public string? Caption { get; set; }
    public string? Code { get; set; }
    public string? FullName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    /// <summary>Адреса.</summary>
    public string? Location { get; set; }
    public string? ZipCode { get; set; }
    /// <summary>cmn_enum_record (DepartmentType): 01 Департамент, 02 Діагностичне, 03 Лікувальне, 04 Адміністративне, 05 Філія, 06 Підрозділ.</summary>
    [MaxLength(64)] public string DepartmentTypeId { get; set; } = "";
    [MaxLength(64)] public string OrganizationId { get; set; } = "";
    [MaxLength(64)] public string? ParentId { get; set; }

    [ForeignKey(nameof(DepartmentTypeId))] public CmnEnumRecord? DepartmentType { get; set; }
    [ForeignKey(nameof(OrganizationId))] public OrgOrganization? Organization { get; set; }
    public LabDepartmentSettings? LabSettings { get; set; }
}

/// <summary>[MedLink] org_employee — співробітник (ПІБ у caption та cmn_person). Лабораторна роль — у [ЛІС] lab_employee_settings.</summary>
[Table("org_employee")]
public class OrgEmployee : GuidEntity
{
    /// <summary>ПІБ співробітника (як в evomis).</summary>
    public string? Caption { get; set; }
    [MaxLength(64)] public string PersonId { get; set; } = "";
    [MaxLength(64)] public string OrganizationId { get; set; } = "";
    [MaxLength(64)] public string? DepartmentId { get; set; }
    [MaxLength(64)] public string? DepartmentSectionId { get; set; }
    /// <summary>cmn_enum_record (OrgPositionType): 0001 Інші, 0002 Лікарі, 0003 Лаборанти, 0005 Середній мед.персонал…</summary>
    [MaxLength(64)] public string PositionTypeId { get; set; } = "";
    public DateTime? WorkingStartDate { get; set; }
    public bool HasScheduleWithPartner { get; set; }

    [ForeignKey(nameof(PersonId))] public CmnPerson? Person { get; set; }
    [ForeignKey(nameof(DepartmentId))] public OrgDepartment? Department { get; set; }
    [ForeignKey(nameof(PositionTypeId))] public CmnEnumRecord? PositionType { get; set; }
    public LabEmployeeSettings? LabSettings { get; set; }

    [NotMapped] public string? LabRole => LabSettings?.LabRole;
    [NotMapped] public bool IsLabActive => LabSettings?.IsActive ?? true;
}

/// <summary>[MedLink+] mis_patient_card — медична карта пацієнта; персональні дані — у cmn_person (person_id).</summary>
[Table("mis_patient_card")]
public class MisPatientCard : GuidEntity
{
    [MaxLength(64)] public string PersonId { get; set; } = "";
    [MaxLength(64)] public string OrganizationId { get; set; } = "";
    [MaxLength(50)] public string? RegNumber { get; set; }
    public DateTime RegDate { get; set; } = DateTime.UtcNow;
    /// <summary>ПІБ пацієнта (як в evomis: caption картки = ПІБ особи).</summary>
    [MaxLength(300)] public string? Caption { get; set; }
    public DateTime? Birthday { get; set; }
    [MaxLength(64)] public string? GenderId { get; set; }
    [MaxLength(64)] public string DocumentTypeId { get; set; } = "";
    [MaxLength(64)] public string PatientCardTypeId { get; set; } = "";
    [MaxLength(64)] public string PrivacyRequestTypeId { get; set; } = "";
    [MaxLength(250)] public string? Location { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    /// <summary>[MedLink+] Прізвище латиницею (КМУ №55 від 27.01.2010) — для приладів без кирилиці (рішення Q-08).</summary>
    [MaxLength(100)] public string? LastNameLatin { get; set; }
    /// <summary>[MedLink+] Ім'я латиницею (КМУ №55).</summary>
    [MaxLength(100)] public string? FirstNameLatin { get; set; }

    [ForeignKey(nameof(PersonId))] public CmnPerson? Person { get; set; }

    /// <summary>Код статі M/F/U (cmn_enum_record Gender) — для каскаду норм і бланків.</summary>
    [NotMapped] public string Gender => MedLinkEnums.GenderCode(GenderId ?? Person?.GenderId);
}

/// <summary>[MedLink] ehe_service_catalog_service — національний каталог послуг eHealth (код послуги для е-направлення та DiagnosticReport).</summary>
[Table("ehe_service_catalog_service")]
public class EheServiceCatalogService : GuidEntity
{
    public string? Caption { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsComposition { get; set; }
    public bool RequestAllowed { get; set; } = true;
    [MaxLength(64)] public string MedicalReferralCategoryId { get; set; } = "";
    [MaxLength(64)] public string? EhealthId { get; set; }
    public DateTime InsertedAtInEhealth { get; set; } = DateTime.UtcNow;
}

/// <summary>[MedLink] org_organization_service — послуга прайсу закладу (назва, ціна, тривалість). В evomis таблиця без службових колонок.</summary>
[Table("org_organization_service")]
public class OrgOrganizationService
{
    [Key, MaxLength(64)] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? Caption { get; set; }
    [MaxLength(64)] public string OrganizationId { get; set; } = "";
    public decimal? Price { get; set; }
    /// <summary>Тривалість, хв (smallint).</summary>
    public short Duration { get; set; }
    [MaxLength(64)] public string ProfileId { get; set; } = MedLinkEnums.EmptyGuid;
}

/// <summary>[MedLink] ehe_incoming_medical_referral — вхідне е-направлення.</summary>
[Table("ehe_incoming_medical_referral")]
public class EheIncomingMedicalReferral : GuidEntity
{
    /// <summary>Номер е-направлення.</summary>
    public string? RegNumber { get; set; }
    public DateTime RegDate { get; set; } = DateTime.UtcNow;
    public string? Caption { get; set; }
    [MaxLength(64)] public string OrganizationId { get; set; } = "";
    [MaxLength(64)] public string? PatientCardId { get; set; }
    public string? PatientShortName { get; set; }
    [MaxLength(64)] public string? ServiceCatalogServiceId { get; set; }
    /// <summary>ehe_medical_referral_category (laboratory_procedure…).</summary>
    [MaxLength(64)] public string MedicalReferralCategoryId { get; set; } = "";
    /// <summary>Заклад-направник (ehe_external_organization).</summary>
    [MaxLength(64)] public string? RequesterId { get; set; }
    /// <summary>cmn_enum_record — статус направлення.</summary>
    [MaxLength(64)] public string StatusId { get; set; } = "";
    /// <summary>ehd_dictionary — пріоритет.</summary>
    [MaxLength(64)] public string PriorityId { get; set; } = "";
    public DateTime? ExpirationDate { get; set; }
    [MaxLength(64)] public string? EhealthId { get; set; }
    [MaxLength(2000)] public string? PatientInstruction { get; set; }
    /// <summary>Програма медичних гарантій (ehe_medical_service_program) — для автоматичного вибору платника НСЗУ.</summary>
    [MaxLength(64)] public string? MedicalServiceProgramId { get; set; }
    // --- обробка направлення виконавцем (як в evomis EhealthIncomingMedicalReferralService) ---
    /// <summary>cmn_enum_record MedicalReferralProcessingStatusInEhealth: new | in_progress | completed …</summary>
    [MaxLength(64)] public string? ProcessingStatusInEhealthId { get; set; }
    /// <summary>Взято в роботу (UseMedicalReferralInEhealth).</summary>
    public DateTime? TakeInWorkDate { get; set; }
    /// <summary>Погашено (CompleteMedicalReferralInEhealth).</summary>
    public DateTime? CompleteDateInEhealth { get; set; }
    /// <summary>Документ, яким погашено направлення (mis_diagnostic_report.id).</summary>
    [MaxLength(64)] public string? CompletedWithItemEntityId { get; set; }
    /// <summary>Тип документа погашення: DiagnosticReport | Encounter | Procedure.</summary>
    public string? CompletedWithItemEntityName { get; set; }

    [ForeignKey(nameof(PatientCardId))] public MisPatientCard? PatientCard { get; set; }
    [ForeignKey(nameof(StatusId))] public CmnEnumRecord? Status { get; set; }
    [ForeignKey(nameof(ProcessingStatusInEhealthId))] public CmnEnumRecord? ProcessingStatus { get; set; }
    [ForeignKey(nameof(ServiceCatalogServiceId))] public EheServiceCatalogService? ServiceCatalogService { get; set; }
}

/// <summary>[MedLink] ehe_paper_medical_referral — паперове направлення (ЛІС створює його при реєстрації замовлення за паперовим направленням).</summary>
[Table("ehe_paper_medical_referral")]
public class EhePaperMedicalReferral : GuidEntity
{
    /// <summary>Номер паперового направлення.</summary>
    public string? RegNumber { get; set; }
    public DateTime RegDate { get; set; } = DateTime.UtcNow;
    public string? Caption { get; set; }
    [MaxLength(64)] public string OrganizationId { get; set; } = "";
    [MaxLength(64)] public string PatientCardId { get; set; } = "";
    /// <summary>ПІБ лікаря-направника.</summary>
    public string? RequesterEmployeeName { get; set; }
    public string? RequesterLegalEntityName { get; set; }
    [MaxLength(50)] public string? RequesterLegalEntityEdrpou { get; set; }
    /// <summary>EhealthPaperMedicalReferralStatus: 0 PROJECT, 1 ACTIVE, 2 COMPLETED, 3 INACTIVE.</summary>
    public int Status { get; set; } = PaperReferralStatuses.Active;
    public DateTime? ProcessedDate { get; set; }
    [MaxLength(3000)] public string? Description { get; set; }

    [ForeignKey(nameof(PatientCardId))] public MisPatientCard? PatientCard { get; set; }
}

public static class PaperReferralStatuses
{
    public const int Project = 0, Active = 1, Completed = 2, Inactive = 3;
}

/// <summary>[MedLink] mis_diagnostic_report — медичний висновок (DiagnosticReport). ЛІС створює його при видачі замовлення; зв'язок — lab_order.diagnostic_report_id.</summary>
[Table("mis_diagnostic_report")]
public class MisDiagnosticReport : GuidEntity
{
    public string? RegNumber { get; set; }
    public DateTime RegDate { get; set; } = DateTime.UtcNow;
    public string? Caption { get; set; }
    /// <summary>Статус документа evomis (int).</summary>
    public int Status { get; set; }
    [MaxLength(64)] public string? PatientCardId { get; set; }
    [MaxLength(64)] public string OrganizationId { get; set; } = "";
    [MaxLength(64)] public string LegalEntityId { get; set; } = "";
    /// <summary>Місце надання послуг eHealth (ehe_division).</summary>
    [MaxLength(64)] public string DivisionId { get; set; } = "";
    [MaxLength(64)] public string DocumentTypeId { get; set; } = "";
    [MaxLength(64)] public string EhealthServiceCatalogServiceId { get; set; } = "";
    [MaxLength(64)] public string? EhealthIncomingMedicalReferralId { get; set; }
    [MaxLength(64)] public string? DiagnosticReportCategoryId { get; set; }
    [MaxLength(3000)] public string? Description { get; set; }
    public string? Comment { get; set; }
    public DateTime? EffectiveDateTimeStart { get; set; }
    public DateTime? EffectiveDateTimeEnd { get; set; }
    public DateTime? IssuedAt { get; set; }
    /// <summary>Виконавець (ehe_employee); у ЛІС до перенесення — також текстом у performer_string.</summary>
    [MaxLength(64)] public string? PerformerId { get; set; }
    public string? PerformerString { get; set; }
    public bool IsPerformerString { get; set; }
    [MaxLength(64)] public string? RecordedById { get; set; }
    [MaxLength(64)] public string? EhealthId { get; set; }
    public bool IsPrimarySource { get; set; } = true;
}

/// <summary>Ідентифікатори та коди переліків evomis (cmn_enum_record), засіяні з тими самими GUID, що й у evomis DbInitializer.</summary>
public static class MedLinkEnums
{
    public const string EmptyGuid = "00000000-0000-0000-0000-000000000000";

    public const string GenderFemale = "01f0eff5-a698-4873-a45b-ad034a386d08", GenderMale = "07b60329-7282-4262-9409-b24ea4440428", GenderUnknown = "4a27f64a-f484-4b8d-a0bd-17c242e2440e";
    public static string GenderCode(string? genderId) => genderId switch { GenderMale => "M", GenderFemale => "F", _ => "U" };
    public static string GenderId(string? code) => (code ?? "").Trim().ToUpperInvariant() switch { "M" => GenderMale, "F" => GenderFemale, _ => GenderUnknown };

    // DepartmentType
    public const string DeptTypeDiagnostic = "e535bab7-0b06-4d9a-a76d-73fd5f77ef0b", DeptTypeTreatment = "570a6df1-ed44-4ab6-8c60-47b82daca201",
        DeptTypeBranch = "1e17ba44-ec4a-4deb-bcc3-a006ea1bb138", DeptTypeUnit = "6b1501dd-8780-45ea-9d3e-445b8ae7875a";
    // OrgPositionType
    public const string PositionOther = "4fd18a0e-be63-42c3-8fa3-3f493679cd7d", PositionDoctors = "a40ae385-b6cb-467e-a8ef-7e35e111fd27",
        PositionLabTechs = "401fc1d1-064d-43a5-b0b1-3db57201abdd", PositionNurses = "0f284af5-ff01-4d12-93c1-410f4ec9f13f";
    // PrivacyRequestType
    public const string PrivacyUnknown = "64b66b71-581b-4c52-aff4-abf352fa6a6d";

    // MedicalReferralStatus (enum_type, коди як у evomis Constants.MedicalReferralStatus; GUID — власні, зіставляти за code)
    public const string ReferralStatusActive = "0c000000-0000-0000-0000-00000000e003", ReferralStatusCompleted = "0c000000-0000-0000-0000-00000000e004",
        ReferralStatusRecalled = "0c000000-0000-0000-0000-00000000e005";
    // MedicalReferralProcessingStatusInEhealth (коди як у evomis Constants)
    public const string ProcessingNew = "0c000000-0000-0000-0000-00000000e006", ProcessingInProgress = "0c000000-0000-0000-0000-00000000e007",
        ProcessingCompleted = "0c000000-0000-0000-0000-00000000e008";
}
