// Логістика (холодовий ланцюг), біобанк (штативи 8×12), реагенти (лоти)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

[Table("lab_sample_logistics")]
public class LabSampleLogistics : GuidEntity
{
    [MaxLength(64)] public string ManifestNumber { get; set; } = "";
    [MaxLength(64)] public string OriginDepartmentId { get; set; } = "";
    [MaxLength(64)] public string DestinationDepartmentId { get; set; } = "";
    [MaxLength(128)] public string? CourierName { get; set; }
    [MaxLength(32)] public string? CourierPhone { get; set; }
    public DateTime? DispatchedAt { get; set; }
    [MaxLength(64)] public string? DispatchedById { get; set; }
    public double? TemperatureDispatch { get; set; }
    public DateTime? ReceivedAt { get; set; }
    [MaxLength(64)] public string? ReceivedById { get; set; }
    public double? TemperatureReceipt { get; set; }
    public bool IsColdChainViolated { get; set; }
    /// <summary>CREATED | DISPATCHED | IN_TRANSIT | RECEIVED | REJECTED</summary>
    [MaxLength(16)] public string Status { get; set; } = "CREATED";
    public string? Notes { get; set; }

    [ForeignKey(nameof(OriginDepartmentId))] public OrgDepartment? OriginDepartment { get; set; }
    [ForeignKey(nameof(DestinationDepartmentId))] public OrgDepartment? DestinationDepartment { get; set; }
    public List<LabSampleLogisticsItem> Items { get; set; } = new();
}

[Table("lab_sample_logistics_item")]
public class LabSampleLogisticsItem : GuidEntity
{
    [MaxLength(64)] public string LogisticsId { get; set; } = "";
    [MaxLength(64)] public string SampleId { get; set; } = "";
    [MaxLength(32)] public string Barcode { get; set; } = "";
    /// <summary>EN_ROUTE | RECEIVED | MISSING</summary>
    [MaxLength(16)] public string Status { get; set; } = "EN_ROUTE";

    [ForeignKey(nameof(LogisticsId))] public LabSampleLogistics? Logistics { get; set; }
    [ForeignKey(nameof(SampleId))] public LabOrderSample? Sample { get; set; }
}

[Table("lab_archive_rack")]
public class LabArchiveRack : GuidEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    [MaxLength(64)] public string? RoomNumber { get; set; }
    [MaxLength(128)] public string? FreezerName { get; set; }
    [MaxLength(32)] public string? ShelfNumber { get; set; }
    public double TemperatureCelsius { get; set; } = -20;
    public int RowsCount { get; set; } = 8;
    public int ColsCount { get; set; } = 12;
    public bool IsActive { get; set; } = true;

    public List<LabArchiveCell> Cells { get; set; } = new();
}

[Table("lab_archive_cell")]
public class LabArchiveCell : GuidEntity
{
    [MaxLength(64)] public string RackId { get; set; } = "";
    /// <summary>1..RowsCount (A=1)</summary>
    public int RowNum { get; set; }
    public int ColNum { get; set; }
    [MaxLength(64)] public string? SampleId { get; set; }
    [MaxLength(32)] public string? Barcode { get; set; }
    public DateTime? StoredAt { get; set; }
    [MaxLength(64)] public string? StoredById { get; set; }
    public DateTime? ExpiryAt { get; set; }
    public bool IsDisposed { get; set; }
    public DateTime? DisposedAt { get; set; }
    [MaxLength(256)] public string? DisposalReason { get; set; }

    [ForeignKey(nameof(RackId))] public LabArchiveRack? Rack { get; set; }
    [ForeignKey(nameof(SampleId))] public LabOrderSample? Sample { get; set; }

    [NotMapped] public string Coordinate => $"{(char)('A' + RowNum - 1)}{ColNum:00}";
}

[Table("lab_reagent_lot")]
public class LabReagentLot : GuidEntity
{
    [MaxLength(64)] public string? AnalyzerId { get; set; }
    [MaxLength(64)] public string? TestCode { get; set; }
    [MaxLength(256)] public string ReagentName { get; set; } = "";
    [MaxLength(64)] public string LotNumber { get; set; } = "";
    [MaxLength(128)] public string? Manufacturer { get; set; }
    public int TestsInitial { get; set; }
    public int TestsRemaining { get; set; }
    public int MinimumTests { get; set; } = 50;
    public DateTime ExpiryDate { get; set; }
    public DateTime? OpenedAt { get; set; }
    public int? OnboardStabilityDays { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(AnalyzerId))] public LabAnalyzer? Analyzer { get; set; }
}
