// =============================================================================
// ehe_outbox — черга відправки в ЕСОЗ (transactional outbox). Один запис = одна ідемпотентна операція:
//   SUBMIT_DR   — Submit Diagnostic Report Package (POST) → опитування job;
//   COMPLETE_SR — Complete service_request (створюється воркером ЛИШЕ після того, як job SUBMIT_DR став processed);
//   CANCEL_DR   — Cancel Diagnostic Report Package (PATCH) з підписаним пакетом скасування.
// Стани: NEW → SENDING → WAITING_JOB → DONE; помилка: NEW (повтор з витримкою) | FAILED (потрібна дія) | DEAD (вичерпано спроби).
// =============================================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

public static class EhealthOutboxKinds
{
    public const string SubmitDr = "SUBMIT_DR", CompleteSr = "COMPLETE_SR", CancelDr = "CANCEL_DR";
    public static string Label(string k) => k switch { SubmitDr => "Відправка звіту", CompleteSr => "Погашення направлення", CancelDr => "Скасування звіту", _ => k };
}

public static class EhealthOutboxStatuses
{
    public const string New = "NEW", Sending = "SENDING", WaitingJob = "WAITING_JOB", Done = "DONE", Failed = "FAILED", Dead = "DEAD";
    public static readonly string[] Active = { New, Sending, WaitingJob };
    public static string Label(string s) => s switch
    {
        New => "У черзі", Sending => "Відправляється", WaitingJob => "Очікує завершення в ЕСОЗ", Done => "Виконано", Failed => "Потрібна дія", Dead => "Спроби вичерпано", _ => s
    };
}

/// <summary>Машинні коди причин зупинки (ehe_outbox.error_code).</summary>
public static class EhealthOutboxErrors
{
    public const string CancelledByUser = "CANCELLED_BY_USER", JobFailed = "JOB_FAILED", JobTimeout = "JOB_TIMEOUT", Recovered = "RECOVERED", MissingData = "MISSING_DATA";
}

[Table("ehe_outbox")]
public class EheOutbox : GuidEntity
{
    /// <summary>SUBMIT_DR | COMPLETE_SR | CANCEL_DR</summary>
    [MaxLength(16)] public string Kind { get; set; } = EhealthOutboxKinds.SubmitDr;
    /// <summary>Об'єкт, який відправляється: lab_ehealth_submission.id (для COMPLETE_SR — відправлення SUBMIT, яке погашається).</summary>
    [MaxLength(64)] public string EntityId { get; set; } = "";
    [MaxLength(64)] public string? OrderId { get; set; }
    /// <summary>Ключ ідемпотентності: повторне додавання з тим самим ключем не створює дубль (унікальний індекс).</summary>
    [MaxLength(200)] public string IdempotencyKey { get; set; } = "";
    /// <summary>Службові дані операції (JSON): patientId, reportId, serviceRequestId… Підписані дані — у lab_ehealth_submission.signed_data.</summary>
    public string Payload { get; set; } = "{}";
    [MaxLength(16)] public string Status { get; set; } = EhealthOutboxStatuses.New;
    public int Attempts { get; set; }
    public int PollAttempts { get; set; }
    public DateTime? NextAttemptAt { get; set; } = DateTime.UtcNow;
    [MaxLength(1024)] public string? LastError { get; set; }
    [MaxLength(32)] public string? ErrorCode { get; set; }
    [MaxLength(128)] public string? JobId { get; set; }
    [MaxLength(64)] public string? LockedBy { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? DoneAt { get; set; }
    /// <summary>Оновлення запису (updated).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
