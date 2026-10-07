// Внутрішній контроль якості: матеріали, цільові значення, результати, lockout
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace MedLink.LIS.Api.Data.Entities;

[Table("lab_qc_material")]
public class LabQcMaterial : GuidEntity
{
    [MaxLength(64)] public string AnalyzerId { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>LEVEL_1_LOW | LEVEL_2_NORMAL | LEVEL_3_HIGH</summary>
    [MaxLength(16)] public string Level { get; set; } = "LEVEL_2_NORMAL";
    [MaxLength(64)] public string LotNumber { get; set; } = "";
    [MaxLength(128)] public string? Manufacturer { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime? OpenedAt { get; set; }
    public int OpenStabilityDays { get; set; } = 30;
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(AnalyzerId))] public LabAnalyzer? Analyzer { get; set; }
    public List<LabQcTarget> Targets { get; set; } = new();
}

[Table("lab_qc_target")]
public class LabQcTarget : GuidEntity
{
    [MaxLength(64)] public string QcMaterialId { get; set; } = "";
    [MaxLength(64)] public string TestCode { get; set; } = "";
    public double TargetMean { get; set; }
    public double TargetSd { get; set; }
    [MaxLength(64)] public string? Unit { get; set; }
    /// <summary>Total allowable error, %</summary>
    public double? TeaPct { get; set; }

    [ForeignKey(nameof(QcMaterialId))] public LabQcMaterial? Material { get; set; }
}

[Table("lab_qc_result")]
public class LabQcResult : GuidEntity
{
    [MaxLength(64)] public string QcMaterialId { get; set; } = "";
    [MaxLength(64)] public string AnalyzerId { get; set; } = "";
    [MaxLength(64)] public string TestCode { get; set; } = "";
    public double MeasuredValue { get; set; }
    public double TargetMean { get; set; }
    public double TargetSd { get; set; }
    public double ZScore { get; set; }
    public string ViolatedRulesJson { get; set; } = "[]";
    public bool IsWarning { get; set; }
    public bool IsRejection { get; set; }
    public bool LockoutEnforced { get; set; }
    [MaxLength(64)] public string? LockoutId { get; set; }
    [MaxLength(64)] public string? ResolvedById { get; set; }
    public DateTime? ResolvedAt { get; set; }
    [MaxLength(512)] public string? ResolutionAction { get; set; }
    public DateTime RunAt { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? OperatorId { get; set; }

    [ForeignKey(nameof(QcMaterialId))] public LabQcMaterial? Material { get; set; }

    [NotMapped]
    public List<string> ViolatedRules
    {
        get => JsonSerializer.Deserialize<List<string>>(ViolatedRulesJson) ?? new();
        set => ViolatedRulesJson = JsonSerializer.Serialize(value);
    }
}

[Table("lab_analyzer_lockout")]
public class LabAnalyzerLockout : GuidEntity
{
    [MaxLength(64)] public string AnalyzerId { get; set; } = "";
    [MaxLength(64)] public string? TestCode { get; set; }
    [MaxLength(512)] public string Reason { get; set; } = "";
    [MaxLength(64)] public string? QcResultId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    [MaxLength(64)] public string? ResolvedById { get; set; }
    [MaxLength(512)] public string? Cause { get; set; }
    [MaxLength(512)] public string? Action { get; set; }
    [MaxLength(1024)] public string? Comment { get; set; }
    public string? ProtocolText { get; set; }

    [ForeignKey(nameof(AnalyzerId))] public LabAnalyzer? Analyzer { get; set; }
    [NotMapped] public bool IsActive => ResolvedAt == null;
}
