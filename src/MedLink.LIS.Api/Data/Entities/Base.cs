// Базові сутності: службові колонки evomis (created_on/created_by/modified_on/modified_by/is_deleted)
using System.ComponentModel.DataAnnotations;

namespace MedLink.LIS.Api.Data.Entities;

public abstract class AuditableEntity
{
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    [MaxLength(64)] public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Сутність із ключем-GUID у вигляді рядка (як uuid в evomis).</summary>
public abstract class GuidEntity : AuditableEntity
{
    [Key, MaxLength(64)] public string Id { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>Довідник із цілочисельним ключем.</summary>
public abstract class IntEntity : AuditableEntity
{
    [Key] public int Id { get; set; }
}
