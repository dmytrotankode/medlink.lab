// Згоди пацієнтів (FR-GAP-003), реєстр СОП (FR-GAP-041), допуски персоналу (FR-GAP-040)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

/// <summary>Згода пацієнта: обробка персональних даних або отримання результатів певним каналом. Один запис на пару (пацієнт, вид); історія змін — в аудиті.</summary>
[Table("lab_patient_consent")]
public class LabPatientConsent : GuidEntity
{
    [MaxLength(64)] public string PatientId { get; set; } = "";
    /// <summary>DATA_PROCESSING | RESULTS_PHONE | RESULTS_SMS | RESULTS_EMAIL | RESULTS_VIBER</summary>
    [MaxLength(32)] public string Kind { get; set; } = "";
    public bool IsGranted { get; set; }
    public DateTime? GrantedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    /// <summary>PAPER | ELECTRONIC | VERBAL</summary>
    [MaxLength(16)] public string Source { get; set; } = "PAPER";
    [MaxLength(512)] public string? Note { get; set; }
    [MaxLength(64)] public string? RecordedById { get; set; }
}

/// <summary>Стандартна операційна процедура (СОП) із версіями; активна версія одна на код.</summary>
[Table("lab_sop")]
public class LabSop : GuidEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Title { get; set; } = "";
    [MaxLength(32)] public string Version { get; set; } = "1.0";
    /// <summary>DRAFT | ACTIVE | ARCHIVED</summary>
    [MaxLength(16)] public string Status { get; set; } = "DRAFT";
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? ReviewDueAt { get; set; }
    [MaxLength(512)] public string? DocumentUrl { get; set; }
    [MaxLength(2000)] public string? Description { get; set; }
    /// <summary>Де застосовується: TEST | ANALYZER | SECTION | PROCESS та код об'єкта — для посилання з екранів процесів.</summary>
    [MaxLength(16)] public string? TargetKind { get; set; }
    [MaxLength(64)] public string? TargetCode { get; set; }
    [MaxLength(64)] public string? ApprovedById { get; set; }
}

/// <summary>Допуск співробітника до методики/приладу/розділу з датою оцінки компетентності.</summary>
[Table("lab_staff_competency")]
public class LabStaffCompetency : GuidEntity
{
    [MaxLength(64)] public string EmployeeId { get; set; } = "";
    /// <summary>TEST | ANALYZER | SECTION | PROCEDURE</summary>
    [MaxLength(16)] public string Kind { get; set; } = "TEST";
    [MaxLength(64)] public string TargetCode { get; set; } = "";
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ValidUntil { get; set; }
    [MaxLength(64)] public string? AssessorId { get; set; }
    [MaxLength(512)] public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
}
