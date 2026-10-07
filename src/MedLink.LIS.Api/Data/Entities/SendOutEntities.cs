// =============================================================================
// [ЛІС] Лабораторії-виконавці та направлення у зовнішні (референс-) лабораторії (send-out):
// власна лабораторія або зовнішня (Сінево тощо) виконує тест; відправка партіями-реєстрами,
// трекінг статусів і термінів, отримання результатів вручну / файлом (PDF-бланк + значення).
// Режим API зарезервовано для майбутніх інтеграцій; провайдери MedLink (TerraLab, SimplexMed)
// позначаються medlink_provider і в ЛІС не дублюються (обмін з ними веде evomis).
// =============================================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

public static class PerformerKinds
{
    public const string Internal = "INTERNAL", External = "EXTERNAL";
}

public static class ExchangeModes
{
    /// <summary>Ручний ввід значень / завантаження бланка.</summary>
    public const string Manual = "MANUAL";
    /// <summary>Файл результатів (CSV/XLSX/PDF) з порталу лабораторії.</summary>
    public const string File = "FILE";
    /// <summary>Електронний обмін (майбутня інтеграція).</summary>
    public const string Api = "API";
    public static readonly string[] All = { Manual, File, Api };
}

/// <summary>[ЛІС] lab_performer — лабораторія-виконавець досліджень (власна або зовнішня).</summary>
[Table("lab_performer")]
public class LabPerformer : GuidEntity
{
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>INTERNAL | EXTERNAL</summary>
    [MaxLength(16)] public string Kind { get; set; } = PerformerKinds.External;
    /// <summary>MANUAL | FILE | API</summary>
    [MaxLength(16)] public string ExchangeMode { get; set; } = ExchangeModes.Manual;
    /// <summary>Провайдер, з яким уже інтегровано MedLink (TERRALAB | SIMPLEXMED) — довідково, обмін веде evomis.</summary>
    [MaxLength(32)] public string? MedlinkProvider { get; set; }
    [MaxLength(16)] public string? Edrpou { get; set; }
    [MaxLength(128)] public string? LicenseNumber { get; set; }
    [MaxLength(512)] public string? Address { get; set; }
    [MaxLength(64)] public string? Phone { get; set; }
    [MaxLength(128)] public string? Email { get; set; }
    [MaxLength(128)] public string? ContractNumber { get; set; }
    public DateTime? ContractDate { get; set; }
    /// <summary>Типовий термін виконання, год (якщо не задано для тесту).</summary>
    public int DefaultTatHours { get; set; } = 72;
    /// <summary>Примітка на бланку результатів, напр. «Дослідження виконано: ТОВ «Сінево Україна», ліцензія …».</summary>
    [MaxLength(512)] public string? ReportNote { get; set; }
    /// <summary>Підрозділ-отримувач MedLink (org_department) для логістики, якщо є.</summary>
    [MaxLength(64)] public string? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;

    public List<LabPerformerTest> Tests { get; set; } = new();
}

/// <summary>[ЛІС] lab_performer_test — які показники виконує зовнішня лабораторія: код/назва в її прайсі, вартість, термін, маршрут за замовчуванням.</summary>
[Table("lab_performer_test")]
public class LabPerformerTest : GuidEntity
{
    [MaxLength(64)] public string PerformerId { get; set; } = "";
    [MaxLength(64)] public string TestId { get; set; } = "";
    [MaxLength(64)] public string? ExternalCode { get; set; }
    [MaxLength(256)] public string? ExternalName { get; set; }
    /// <summary>Собівартість для лабораторії (вартість у прайсі виконавця).</summary>
    public decimal? Cost { get; set; }
    public int? TatHours { get; set; }
    /// <summary>Маршрутизувати цей показник до виконавця автоматично при створенні замовлення.</summary>
    public bool IsDefaultRoute { get; set; }
    /// <summary>Вимоги до біоматеріалу/тари виконавця (окрема пробірка, заморожування…).</summary>
    [MaxLength(512)] public string? SpecimenRequirements { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(PerformerId))] public LabPerformer? Performer { get; set; }
    [ForeignKey(nameof(TestId))] public LabTestDefinition? Test { get; set; }
}

public static class SendOutStatuses
{
    /// <summary>Реєстр сформовано, проби ще в лабораторії.</summary>
    public const string Created = "CREATED";
    public const string Dispatched = "DISPATCHED";
    /// <summary>Зовнішня лабораторія підтвердила приймання проб.</summary>
    public const string Accepted = "ACCEPTED";
    /// <summary>Отримано частину результатів.</summary>
    public const string Partial = "PARTIAL";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
}

public static class SendOutItemStatuses
{
    public const string Queued = "QUEUED", Sent = "SENT", Accepted = "ACCEPTED", Resulted = "RESULTED", Rejected = "REJECTED", Recalled = "RECALLED";
    public static readonly string[] Open = { Queued, Sent, Accepted };
}

/// <summary>[ЛІС] lab_send_out — реєстр (накладна) відправки проб у зовнішню лабораторію.</summary>
[Table("lab_send_out")]
public class LabSendOut : GuidEntity
{
    [MaxLength(32)] public string Number { get; set; } = "";
    [MaxLength(64)] public string PerformerId { get; set; } = "";
    [MaxLength(16)] public string Status { get; set; } = SendOutStatuses.Created;
    /// <summary>Номер замовлення/накладної в системі зовнішньої лабораторії.</summary>
    [MaxLength(64)] public string? ExternalBatchNumber { get; set; }
    [MaxLength(128)] public string? CourierName { get; set; }
    public double? TemperatureDispatch { get; set; }
    public DateTime? DispatchedAt { get; set; }
    [MaxLength(64)] public string? DispatchedById { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }

    [ForeignKey(nameof(PerformerId))] public LabPerformer? Performer { get; set; }
    public List<LabSendOutItem> Items { get; set; } = new();
}

/// <summary>[ЛІС] lab_send_out_item — тест замовлення, відправлений на виконання зовнішній лабораторії.</summary>
[Table("lab_send_out_item")]
public class LabSendOutItem : GuidEntity
{
    [MaxLength(64)] public string SendOutId { get; set; } = "";
    [MaxLength(64)] public string OrderTestId { get; set; } = "";
    [MaxLength(64)] public string? SampleId { get; set; }
    /// <summary>Номер замовлення/проби в зовнішній лабораторії (для звірки результатів).</summary>
    [MaxLength(64)] public string? ExternalOrderNumber { get; set; }
    [MaxLength(64)] public string? ExternalCode { get; set; }
    [MaxLength(16)] public string Status { get; set; } = SendOutItemStatuses.Queued;
    /// <summary>Очікуваний термін результату (відправка + TAT виконавця).</summary>
    public DateTime? DueAt { get; set; }
    public DateTime? ResultReceivedAt { get; set; }
    [MaxLength(512)] public string? RejectReason { get; set; }

    [ForeignKey(nameof(SendOutId))] public LabSendOut? SendOut { get; set; }
    [ForeignKey(nameof(OrderTestId))] public LabOrderTest? OrderTest { get; set; }
    [ForeignKey(nameof(SampleId))] public LabOrderSample? Sample { get; set; }
}

/// <summary>[ЛІС] lab_order_attachment — файли до замовлення: бланк зовнішньої лабораторії (PDF), скани. Незмінні, з SHA-256.</summary>
[Table("lab_order_attachment")]
public class LabOrderAttachment : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    [MaxLength(64)] public string? SendOutId { get; set; }
    [MaxLength(64)] public string? PerformerId { get; set; }
    /// <summary>EXTERNAL_REPORT | SCAN | OTHER</summary>
    [MaxLength(32)] public string Kind { get; set; } = "EXTERNAL_REPORT";
    [MaxLength(256)] public string FileName { get; set; } = "";
    [MaxLength(128)] public string ContentType { get; set; } = "application/pdf";
    public long SizeBytes { get; set; }
    [MaxLength(64)] public string Sha256 { get; set; } = "";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    /// <summary>Показувати пацієнту в порталі разом із результатами.</summary>
    public bool VisibleToPatient { get; set; } = true;

    [ForeignKey(nameof(OrderId))] public LabOrder? Order { get; set; }
}
