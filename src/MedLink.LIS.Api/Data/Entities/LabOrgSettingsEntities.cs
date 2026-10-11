// [ЛІС] Лабораторні атрибути сутностей MedLink, що не мають відповідника в evomis:
// роль співробітника в ЛІС і вид підрозділу (лабораторія / пункт забору / клінічний / філія).
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

/// <summary>[ЛІС] lab_employee_settings — роль співробітника в ЛІС (1:1 з org_employee). В evomis роль може надаватися через sys_role/sys_user_profile.</summary>
[Table("lab_employee_settings")]
public class LabEmployeeSettings : AuditableEntity
{
    [Key, MaxLength(64)] public string EmployeeId { get; set; } = "";
    /// <summary>LAB_ADMIN | LAB_DOCTOR | LAB_TECHNICIAN | PHLEBOTOMIST | LOGISTICS_COURIER | REGISTRAR</summary>
    [MaxLength(32)] public string LabRole { get; set; } = "LAB_TECHNICIAN";
    /// <summary>Назва посади для бланків (в evomis посада — тип position_type_id).</summary>
    [MaxLength(256)] public string? PositionName { get; set; }
    [MaxLength(128)] public string? DigitalSignatureCertId { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(EmployeeId))] public OrgEmployee? Employee { get; set; }
}

/// <summary>[ЛІС] lab_department_settings — вид підрозділу для ЛІС (1:1 з org_department). Розширюється в FR-PRE-001 (пункти забору).</summary>
[Table("lab_department_settings")]
public class LabDepartmentSettings : AuditableEntity
{
    [Key, MaxLength(64)] public string DepartmentId { get; set; } = "";
    /// <summary>LABORATORY | COLLECTION_POINT | CLINICAL | BRANCH</summary>
    [MaxLength(32)] public string LabKind { get; set; } = "CLINICAL";
    [MaxLength(32)] public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(DepartmentId))] public OrgDepartment? Department { get; set; }
}
