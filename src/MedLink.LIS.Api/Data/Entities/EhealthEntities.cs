// [ЛІС] Журнал обміну з ЕСОЗ (eHealth): кожен HTTP-запит/відповідь EhealthClient — у режимах MOCK і LIVE.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

[Table("lab_ehealth_exchange_log")]
public class LabEhealthExchangeLog : GuidEntity
{
    /// <summary>MOCK | LIVE</summary>
    [MaxLength(8)] public string Mode { get; set; } = "MOCK";
    [MaxLength(8)] public string Method { get; set; } = "GET";
    [MaxLength(512)] public string Url { get; set; } = "";
    public string? RequestJson { get; set; }
    public int? StatusCode { get; set; }
    public string? ResponseJson { get; set; }
    public int DurationMs { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
