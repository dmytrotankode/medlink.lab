// Калібрування аналізаторів (FR-GAP-031): журнал за приладом і показником, лот калібратора, лот реагенту, термін дії
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

[Table("lab_calibration")]
public class LabCalibration : GuidEntity
{
    [MaxLength(64)] public string AnalyzerId { get; set; } = "";
    /// <summary>Код показника; null — калібрування діє для всіх показників приладу.</summary>
    [MaxLength(64)] public string? TestCode { get; set; }
    /// <summary>Лот реагенту, під який виконано калібрування (зв'язок з обліком реагентів).</summary>
    [MaxLength(64)] public string? ReagentLotId { get; set; }
    [MaxLength(256)] public string? CalibratorName { get; set; }
    [MaxLength(64)] public string CalibratorLot { get; set; } = "";
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Отримане значення (коефіцієнт/відповідь калібрування).</summary>
    public double? CalibrationValue { get; set; }
    [MaxLength(64)] public string? Unit { get; set; }
    /// <summary>Калібрування прийнято (критерії приймання виконано).</summary>
    public bool Accepted { get; set; } = true;
    [MaxLength(512)] public string? RejectReason { get; set; }
    /// <summary>Кінець терміну дії калібрування.</summary>
    public DateTime ValidUntil { get; set; }
    /// <summary>Планова дата наступного калібрування (за замовчуванням — кінець терміну дії).</summary>
    public DateTime NextDueAt { get; set; }
    [MaxLength(64)] public string? PerformedById { get; set; }
    [MaxLength(1024)] public string? Comment { get; set; }

    [ForeignKey(nameof(AnalyzerId))] public LabAnalyzer? Analyzer { get; set; }
    [ForeignKey(nameof(ReagentLotId))] public LabReagentLot? ReagentLot { get; set; }
}
