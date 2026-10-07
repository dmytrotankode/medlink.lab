// Локальні копії структури таблиць evomis (підмножина колонок).
// У складі МІС ці DbSet вказують на реальні таблиці; ЛІС-таблиці мають FK на них.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

[Table("mis_patient_card")]
public class MisPatientCard : GuidEntity
{
    [MaxLength(128)] public string LastName { get; set; } = "";
    [MaxLength(128)] public string FirstName { get; set; } = "";
    [MaxLength(128)] public string? SecondName { get; set; }
    /// <summary>Транслітерація за КМУ №55 (2010) — для приладів без кирилиці.</summary>
    [MaxLength(128)] public string? LastNameLatin { get; set; }
    [MaxLength(128)] public string? FirstNameLatin { get; set; }
    public DateTime? BirthDate { get; set; }
    /// <summary>M | F | U</summary>
    [MaxLength(8)] public string Gender { get; set; } = "U";
    [MaxLength(32)] public string? Phone { get; set; }
    [MaxLength(128)] public string? Email { get; set; }
    /// <summary>РНОКПП (ІПН)</summary>
    [MaxLength(16)] public string? TaxId { get; set; }
    [MaxLength(512)] public string? Address { get; set; }
    [MaxLength(64)] public string? EhealthPatientId { get; set; }

    [NotMapped] public string FullName => string.Join(" ", new[] { LastName, FirstName, SecondName }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

[Table("mis_specimen")]
public class MisSpecimen : GuidEntity
{
    [MaxLength(64)] public string PatientId { get; set; } = "";
    [MaxLength(64)] public string Barcode { get; set; } = "";
    [MaxLength(128)] public string? SpecimenType { get; set; }
    public DateTime? CollectedOn { get; set; }
    [MaxLength(32)] public string Status { get; set; } = "available";
    [MaxLength(64)] public string? LabSampleId { get; set; }
}

[Table("mis_diagnostic_report")]
public class MisDiagnosticReport : GuidEntity
{
    [MaxLength(64)] public string ReportNumber { get; set; } = "";
    [MaxLength(64)] public string PatientId { get; set; } = "";
    [MaxLength(64)] public string? ReferralId { get; set; }
    [MaxLength(64)] public string? LabOrderId { get; set; }
    [MaxLength(512)] public string? ServiceName { get; set; }
    [MaxLength(64)] public string? PerformerDoctorId { get; set; }
    /// <summary>PRELIMINARY | FINAL | AMENDED | CANCELLED</summary>
    [MaxLength(32)] public string Status { get; set; } = "FINAL";
    public bool EhealthSynced { get; set; }
    public string? Conclusions { get; set; }
    public DateTime? SignedAt { get; set; }
}

[Table("org_employee")]
public class OrgEmployee : GuidEntity
{
    [MaxLength(256)] public string FullName { get; set; } = "";
    [MaxLength(256)] public string? PositionName { get; set; }
    [MaxLength(256)] public string? SpecialityName { get; set; }
    /// <summary>LAB_ADMIN | LAB_DOCTOR | LAB_TECHNICIAN | PHLEBOTOMIST | LOGISTICS_COURIER | REGISTRAR</summary>
    [MaxLength(32)] public string LabRole { get; set; } = "LAB_TECHNICIAN";
    [MaxLength(64)] public string? DepartmentId { get; set; }
    [MaxLength(32)] public string? Phone { get; set; }
    [MaxLength(128)] public string? Email { get; set; }
    [MaxLength(128)] public string? DigitalSignatureCertId { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(DepartmentId))] public OrgDepartment? Department { get; set; }
}

[Table("org_department")]
public class OrgDepartment : GuidEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>LABORATORY | COLLECTION_POINT | CLINICAL | BRANCH</summary>
    [MaxLength(32)] public string DepartmentType { get; set; } = "LABORATORY";
    [MaxLength(512)] public string? Address { get; set; }
    [MaxLength(32)] public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

[Table("ehe_incoming_medical_referral")]
public class EheIncomingMedicalReferral : GuidEntity
{
    [MaxLength(64)] public string ReferralCode { get; set; } = "";
    [MaxLength(64)] public string PatientId { get; set; } = "";
    [MaxLength(64)] public string? ServiceCode { get; set; }
    [MaxLength(512)] public string? ServiceName { get; set; }
    [MaxLength(64)] public string? Category { get; set; }
    [MaxLength(64)] public string? RequesterDoctorId { get; set; }
    [MaxLength(256)] public string? RequesterOrganization { get; set; }
    [MaxLength(32)] public string Status { get; set; } = "active";
}

[Table("dct_service")]
public class DctService : GuidEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(512)] public string Name { get; set; } = "";
    public decimal Price { get; set; }
    /// <summary>Посилання на лабораторний профіль (lab_test_profile).</summary>
    [MaxLength(64)] public string? LabProfileId { get; set; }
    public bool IsActive { get; set; } = true;
}
