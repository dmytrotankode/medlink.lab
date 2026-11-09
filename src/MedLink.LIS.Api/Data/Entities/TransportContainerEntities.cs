// Транспортні контейнери (термобокси, сумки): типи тари, контейнер із місцями для проб, історія подій (FR-LOG-101…)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

/// <summary>Статуси транспортного контейнера.</summary>
public static class ContainerStatuses
{
    public const string Free = "FREE", Packing = "PACKING", Sealed = "SEALED", InTransit = "IN_TRANSIT",
        Received = "RECEIVED", Returned = "RETURNED", Lost = "LOST";
    /// <summary>Статуси, у яких проби фізично лежать у контейнері й не можуть бути покладені в інший.</summary>
    public static readonly string[] Active = { Packing, Sealed, InTransit };
}

/// <summary>Статуси місця (проби) в контейнері.</summary>
public static class ContainerItemStatuses
{
    public const string Packed = "PACKED", Received = "RECEIVED", Missing = "MISSING", Damaged = "DAMAGED";
}

/// <summary>Довідник типів транспортної тари.</summary>
[Table("lab_transport_container_type")]
public class LabTransportContainerType : GuidEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>Місткість — кількість місць (позицій) для проб.</summary>
    public int Capacity { get; set; }
    /// <summary>Температурний режим, °C. Якщо не задано — діє коридор холодового ланцюга з налаштувань (FR-LOG-001).</summary>
    public double? TempMinC { get; set; }
    public double? TempMaxC { get; set; }
    public bool IsReusable { get; set; }
    public bool IsActive { get; set; } = true;
}

[Table("lab_transport_container")]
public class LabTransportContainer : GuidEntity
{
    /// <summary>Штрихкод контейнера: TC + 8 цифр (лічильник lab_transport_container_barcode).</summary>
    [MaxLength(32)] public string Barcode { get; set; } = "";
    [MaxLength(64)] public string TypeId { get; set; } = "";
    /// <summary>FREE | PACKING | SEALED | IN_TRANSIT | RECEIVED | RETURNED | LOST</summary>
    [MaxLength(16)] public string Status { get; set; } = ContainerStatuses.Free;
    [MaxLength(64)] public string OriginDepartmentId { get; set; } = "";
    [MaxLength(64)] public string DestinationDepartmentId { get; set; } = "";
    /// <summary>Маніфест, сформований при запечатуванні.</summary>
    [MaxLength(64)] public string? ManifestId { get; set; }

    public DateTime? PackedAt { get; set; }
    [MaxLength(64)] public string? PackedById { get; set; }
    public DateTime? SealedAt { get; set; }
    [MaxLength(64)] public string? SealedById { get; set; }
    public double? TemperatureSeal { get; set; }
    public DateTime? DispatchedAt { get; set; }
    [MaxLength(64)] public string? DispatchedById { get; set; }
    public DateTime? ReceivedAt { get; set; }
    [MaxLength(64)] public string? ReceivedById { get; set; }
    public double? TemperatureReceipt { get; set; }
    public bool IsColdChainViolated { get; set; }
    public DateTime? ReturnedAt { get; set; }
    [MaxLength(64)] public string? ReturnedById { get; set; }
    public string? Notes { get; set; }
    /// <summary>Зайві штрихкоди при прийомі (відскановано, але не було в контейнері): JSON-масив рядків. Відсутні й пошкоджені видно за статусами місць.</summary>
    public string? ExtraBarcodesJson { get; set; }
    public int MissingCount { get; set; }
    public int ExtraCount { get; set; }
    public int DamagedCount { get; set; }

    [ForeignKey(nameof(TypeId))] public LabTransportContainerType? Type { get; set; }
    [ForeignKey(nameof(OriginDepartmentId))] public OrgDepartment? OriginDepartment { get; set; }
    [ForeignKey(nameof(DestinationDepartmentId))] public OrgDepartment? DestinationDepartment { get; set; }
    [ForeignKey(nameof(ManifestId))] public LabSampleLogistics? Manifest { get; set; }
    public List<LabTransportContainerItem> Items { get; set; } = new();
}

[Table("lab_transport_container_item")]
public class LabTransportContainerItem : GuidEntity
{
    [MaxLength(64)] public string ContainerId { get; set; } = "";
    [MaxLength(64)] public string SampleId { get; set; } = "";
    [MaxLength(32)] public string Barcode { get; set; } = "";
    /// <summary>Місце в контейнері, 1..місткість типу.</summary>
    public int Position { get; set; }
    /// <summary>PACKED | RECEIVED | MISSING | DAMAGED</summary>
    [MaxLength(16)] public string Status { get; set; } = ContainerItemStatuses.Packed;
    public DateTime PackedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? PackedById { get; set; }
    public DateTime? ReceivedAt { get; set; }

    [ForeignKey(nameof(ContainerId))] public LabTransportContainer? Container { get; set; }
    [ForeignKey(nameof(SampleId))] public LabOrderSample? Sample { get; set; }
}

/// <summary>Історія подій контейнера (укладка, запечатування, відправка, прийом, повернення).</summary>
[Table("lab_transport_container_event")]
public class LabTransportContainerEvent : GuidEntity
{
    [MaxLength(64)] public string ContainerId { get; set; } = "";
    /// <summary>CREATED | PACKED | UNPACKED | SEALED | DISPATCHED | RECEIVED | RETURNED | LOST | DELETED</summary>
    [MaxLength(16)] public string EventType { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? EmployeeId { get; set; }
    [MaxLength(32)] public string? SampleBarcode { get; set; }
    public int? Position { get; set; }
    public double? Temperature { get; set; }
    [MaxLength(1024)] public string? Details { get; set; }

    [ForeignKey(nameof(ContainerId))] public LabTransportContainer? Container { get; set; }
}
