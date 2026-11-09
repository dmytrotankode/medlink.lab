// Архів виданих бланків результатів (FR-GAP-010/012): незмінні копії з хешем, версії «виправлено»
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

[Table("lab_report_archive")]
public class LabReportArchive : GuidEntity
{
    [MaxLength(64)] public string OrderId { get; set; } = "";
    /// <summary>Номер версії бланка в межах замовлення (1, 2…).</summary>
    public int VersionNo { get; set; } = 1;
    /// <summary>ISSUED — перша видача; AMENDED — виправлений бланк (замінює попередню версію).</summary>
    [MaxLength(16)] public string Kind { get; set; } = "ISSUED";
    [MaxLength(8)] public string Language { get; set; } = "uk";
    [MaxLength(16)] public string Variant { get; set; } = "final";
    public int? AmendsVersionNo { get; set; }
    [MaxLength(512)] public string? AmendReason { get; set; }
    /// <summary>SHA-256 (hex) вмісту: PDF, а якщо PDF недоступний — HTML.</summary>
    [MaxLength(64)] public string ContentHash { get; set; } = "";
    public string Html { get; set; } = "";
    public byte[]? Pdf { get; set; }
    [MaxLength(64)] public string? IssuedById { get; set; }
    public int DuplicatesPrinted { get; set; }

    [ForeignKey(nameof(OrderId))] public LabOrder? Order { get; set; }
}
