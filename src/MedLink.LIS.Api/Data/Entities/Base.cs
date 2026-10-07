// Базові сутності: службові колонки як в evomis — created_on/created_by/modified_on/modified_by (uuid користувача)
// та record_state (2 — активний, 4 — видалений) замість прапорця видалення.
using System.ComponentModel.DataAnnotations;

namespace MedLink.LIS.Api.Data.Entities;

/// <summary>Значення record_state evomis (Core.Data RecordState). ЛІС використовує лише Active/Deleted.</summary>
public static class RecordStates
{
    public const int Active = 2;
    public const int Deleted = 4;
}

public abstract class AuditableEntity
{
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    /// <summary>uuid співробітника/користувача, що створив запис (системний — нульовий GUID).</summary>
    [MaxLength(64)] public string CreatedBy { get; set; } = MedLinkEnums.EmptyGuid;
    public DateTime? ModifiedOn { get; set; }
    [MaxLength(64)] public string ModifiedBy { get; set; } = MedLinkEnums.EmptyGuid;
    public int RecordState { get; set; } = RecordStates.Active;
}

/// <summary>Сутність із ключем uuid (у SQLite — рядок GUID).</summary>
public abstract class GuidEntity : AuditableEntity
{
    [Key, MaxLength(64)] public string Id { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>Довідник ЛІС із цілочисельним ключем.</summary>
public abstract class IntEntity : AuditableEntity
{
    [Key] public int Id { get; set; }
}
