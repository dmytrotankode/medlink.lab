// Системні таблиці: аудит, налаштування лабораторії, нумератори, лічильники
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

[Table("lab_audit_log")]
public class LabAuditLog
{
    [Key, MaxLength(64)] public string Id { get; set; } = Guid.NewGuid().ToString();
    [MaxLength(64)] public string? UserId { get; set; }
    [MaxLength(256)] public string? UserName { get; set; }
    [MaxLength(64)] public string Action { get; set; } = "";
    [MaxLength(64)] public string Entity { get; set; } = "";
    [MaxLength(64)] public string? EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? Ip { get; set; }
    [MaxLength(512)] public string? Comment { get; set; }
}

[Table("lab_settings")]
public class LabSettings : GuidEntity
{
    [MaxLength(256)] public string Name { get; set; } = "Клініко-діагностична лабораторія MedLink";
    [MaxLength(16)] public string? Edrpou { get; set; }
    [MaxLength(512)] public string? Address { get; set; }
    [MaxLength(32)] public string? Phone { get; set; }
    [MaxLength(128)] public string? Email { get; set; }
    [MaxLength(128)] public string? LicenseNumber { get; set; }
    public string? LogoBase64 { get; set; }
    [MaxLength(256)] public string? DirectorName { get; set; }
    [MaxLength(128)] public string? WorkingHours { get; set; }
    [MaxLength(64)] public string OrderNumberMask { get; set; } = "{yyMM}-{000000}";
    [MaxLength(16)] public string? BarcodePrefix { get; set; }
    public string? ReportFooter { get; set; }
    [MaxLength(32)] public string? PanicPhone { get; set; }
    public double ColdChainMinC { get; set; } = 2.0;
    public double ColdChainMaxC { get; set; } = 8.0;
    // Принтер етикеток (друк виконує локальний print-агент коннектора; TCP/RAW 9100, без UDP)
    [MaxLength(128)] public string? LabelPrinterHost { get; set; }
    public int LabelPrinterPort { get; set; } = 9100;
    [MaxLength(128)] public string? LabelPrinterName { get; set; }
    /// <summary>Прапорці шаблонів бланка (JSON): {"final":true,"preliminary":true,"cito":true,"showPreviousValues":true,"showQr":true}</summary>
    public string ReportTemplatesJson { get; set; } = "{\"final\":true,\"preliminary\":true,\"cito\":true,\"showPreviousValues\":true,\"showQr\":true}";

    [NotMapped]
    public Dictionary<string, bool> ReportTemplates
    {
        get => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(ReportTemplatesJson) ?? new();
        set => ReportTemplatesJson = System.Text.Json.JsonSerializer.Serialize(value);
    }
}

[Table("lab_numerator")]
public class LabNumerator
{
    [Key, MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(128)] public string Name { get; set; } = "";
    [MaxLength(64)] public string Mask { get; set; } = "{yyMM}-{000000}";
    public long CurrentValue { get; set; }
    /// <summary>Ключ періоду (yyMM) — при зміні лічильник скидається.</summary>
    [MaxLength(16)] public string? PeriodKey { get; set; }
    public bool ResetByPeriod { get; set; } = true;
    public DateTime? ModifiedOn { get; set; }
}

/// <summary>Простий лічильник (аналог dct_counter Simplex), напр. lab_tube_barcode_ean8.</summary>
[Table("lab_counter")]
public class LabCounter
{
    [Key, MaxLength(64)] public string CounterCode { get; set; } = "";
    public long CounterValue { get; set; }
}
