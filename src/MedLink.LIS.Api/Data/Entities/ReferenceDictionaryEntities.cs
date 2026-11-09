// Довідники, що замінюють вільний текст: одиниці виміру (та перерахунок SI ↔ традиційні), вікові групи, причини браку та відмови,
// а також службовий журнал застосованих стартових наборів даних (щоб сід не повертав те, що адміністратор видалив).
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

/// <summary>Одиниця виміру. <see cref="Name"/> — запис, який бачить користувач і який зберігається в полі «одиниця» показника та норми (напр. «ммоль/л»).</summary>
[Table("lab_unit")]
public class LabUnit : IntEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    /// <summary>Скорочений запис для результатів і бланків (ммоль/л, мг/дл, Од/л…). Для безрозмірних величин (pH, питома вага, якісні) — порожній рядок (код NONE).</summary>
    [MaxLength(64)] public string Name { get; set; } = "";
    [MaxLength(256)] public string? FullName { get; set; }
    /// <summary>Величина: CONC_SUBSTANCE (кількість речовини), CONC_MASS (маса), ENZYME (активність), CELLS, PERCENT, RATIO, TIME, PRESSURE, DIMENSIONLESS, OTHER.</summary>
    [MaxLength(32)] public string Dimension { get; set; } = "OTHER";
    /// <summary>SI | TRADITIONAL | OTHER — система одиниць.</summary>
    [MaxLength(16)] public string SystemKind { get; set; } = "OTHER";
    public int SortOrder { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

/// <summary>Правило перерахунку результату показника з однієї одиниці в іншу: до = від × Factor + Offset.</summary>
[Table("lab_unit_conversion")]
public class LabUnitConversion : GuidEntity
{
    [MaxLength(64)] public string TestCode { get; set; } = "";
    public int FromUnitId { get; set; }
    public int ToUnitId { get; set; }
    public double Factor { get; set; } = 1;
    public double Offset { get; set; }
    [MaxLength(256)] public string? Note { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(FromUnitId))] public LabUnit? FromUnit { get; set; }
    [ForeignKey(nameof(ToUnitId))] public LabUnit? ToUnit { get; set; }
}

/// <summary>Вікова група для референтних норм (новонароджені, немовлята, діти, підлітки, дорослі, особи похилого віку).</summary>
[Table("lab_age_group")]
public class LabAgeGroup : IntEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>Межі включно, в цілих одиницях <see cref="AgeUnit"/> (як у шарі норми).</summary>
    public double AgeFrom { get; set; }
    public double AgeTo { get; set; } = 120;
    /// <summary>DAYS | MONTHS | YEARS</summary>
    [MaxLength(8)] public string AgeUnit { get; set; } = "YEARS";
    public int SortOrder { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

/// <summary>Причина браку (відбракування) проби/результату.</summary>
[Table("lab_rejection_reason")]
public class LabRejectionReason : IntEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>COLLECTION (забір) | SAMPLE_QUALITY (якість матеріалу) | IDENTIFICATION (ідентифікація) | TRANSPORT (транспортування) | ANALYTIC (аналітика) | OTHER.</summary>
    [MaxLength(32)] public string Category { get; set; } = "OTHER";
    /// <summary>Чи потрібен повторний забір за замовчуванням.</summary>
    public bool RequiresRecollection { get; set; } = true;
    public int SortOrder { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

/// <summary>Причина відмови/скасування замовлення.</summary>
[Table("lab_cancel_reason")]
public class LabCancelReason : IntEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>PATIENT (пацієнт) | DOCTOR (лікар) | LABORATORY (лабораторія) | PAYMENT (оплата) | ADMIN (адміністративна) | OTHER.</summary>
    [MaxLength(32)] public string Category { get; set; } = "OTHER";
    public int SortOrder { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

/// <summary>Журнал застосованих стартових наборів даних: набір застосовується один раз на базу, тож видалене/змінене адміністратором не повертається.</summary>
[Table("lab_seed_marker")]
public class LabSeedMarker
{
    [Key, MaxLength(64)] public string Code { get; set; } = "";
    public DateTime AppliedOn { get; set; } = DateTime.UtcNow;
    [MaxLength(256)] public string? Note { get; set; }
}
