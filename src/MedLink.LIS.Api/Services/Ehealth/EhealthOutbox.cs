// =============================================================================
// Outbox відправки в ЕСОЗ: черга ehe_outbox + рушій (EhealthOutboxEngine) + фоновий воркер.
// Порядок: SUBMIT_DR → (job processed) → окрема задача COMPLETE_SR; виправлення: CANCEL_DR (підписаний пакет) → новий пакет.
// Надійність:
//   • ідемпотентність — унікальний ключ (idempotency_key); повторна постановка не створює дубль;
//   • блокування запису — атомарний UPDATE … WHERE id=@id AND status IN (NEW, WAITING_JOB) (працює однаково в SQLite і PostgreSQL),
//     тож два воркери не візьмуть один запис; LockedUntil + RecoverStuck повертають у чергу записи SENDING після збою;
//   • повтор після невизначеного результату — перед повторною відправкою пакета перевіряється GET diagnostic_reports/{id};
//   • класифікація помилок: transient → повтор з експоненційною витримкою; auth/permission/validation/conflict → FAILED (потрібна дія).
// =============================================================================
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services.Ehealth;

public static class EhealthBackoff
{
    /// <summary>Витримка перед повтором відправки: base·2^(attempt-1), не більше max, ± jitter.</summary>
    public static TimeSpan RetryDelay(int attempt, EhealthOutboxOptions o, Random? rng = null)
    {
        var secs = Math.Min((double)o.BackoffMaxSeconds, o.BackoffBaseSeconds * Math.Pow(2, Math.Max(0, attempt - 1)));
        if (o.BackoffJitter > 0) secs *= 1 + ((rng ?? Random.Shared).NextDouble() * 2 - 1) * o.BackoffJitter;
        return TimeSpan.FromSeconds(Math.Max(0, secs));
    }

    /// <summary>Пауза між опитуваннями job (ТВ 1.1.4): 2/5/10/30 с, далі кожні PollMaxSeconds.</summary>
    public static TimeSpan PollDelay(int pollAttempt, EhealthOutboxOptions o)
    {
        var d = o.PollDelaysSeconds;
        return TimeSpan.FromSeconds(pollAttempt < d.Length ? d[pollAttempt] : o.PollMaxSeconds);
    }
}

/// <summary>Додавання задач у чергу і ручні дії над ними (повторити / скасувати).</summary>
public sealed class EhealthOutboxService
{
    private readonly LisDbContext _db;
    public EhealthOutboxService(LisDbContext db) => _db = db;

    /// <summary>Ставить задачу в чергу. Той самий ключ → повертається наявний запис (завершений — без змін; зупинений — відновлюється).</summary>
    public async Task<EheOutbox> EnqueueAsync(string kind, string entityId, string? orderId, string idempotencyKey, object payload, DateTime? now = null)
    {
        var t = now ?? DateTime.UtcNow;
        var existing = _db.EhealthOutbox.Local.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey)
                       ?? await _db.EhealthOutbox.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey);
        if (existing != null)
        {
            if (existing.Status is EhealthOutboxStatuses.Failed or EhealthOutboxStatuses.Dead) Revive(existing, t);
            return existing;
        }
        var row = new EheOutbox
        {
            Kind = kind, EntityId = entityId, OrderId = orderId, IdempotencyKey = idempotencyKey, NextAttemptAt = t, UpdatedAt = t,
            Payload = System.Text.Json.JsonSerializer.Serialize(payload, Infrastructure.JsonDefaults.Options)
        };
        _db.EhealthOutbox.Add(row);
        return row;
    }

    public static void Revive(EheOutbox r, DateTime now)
    {
        r.Status = EhealthOutboxStatuses.New; r.Attempts = 0; r.PollAttempts = 0; r.NextAttemptAt = now; r.LastError = null; r.ErrorCode = null;
        r.LockedBy = null; r.LockedUntil = null; r.UpdatedAt = now;
    }

    public static bool CanRetry(EheOutbox r) => r.Status is EhealthOutboxStatuses.Failed or EhealthOutboxStatuses.Dead;
    public static bool CanCancel(EheOutbox r) => (r.Status == EhealthOutboxStatuses.New || r.Status is EhealthOutboxStatuses.Failed or EhealthOutboxStatuses.Dead) && r.ErrorCode != EhealthOutboxErrors.CancelledByUser;

    public async Task<EheOutbox> RetryAsync(string id)
    {
        var r = await _db.EhealthOutbox.FirstOrDefaultAsync(x => x.Id == id) ?? throw new Infrastructure.NotFoundException("Задачу відправки в ЕСОЗ не знайдено");
        if (r.Kind == EhealthOutboxKinds.CompleteSr && await _db.EhealthSubmissions.AnyAsync(s => s.Id == r.EntityId && (s.Status == EhealthSubmitStatuses.Annulled || s.Status == EhealthSubmitStatuses.Completed)))
            throw new Infrastructure.ConflictException("Погашення вже не потрібне: звіт анульовано або направлення погашено");
        if (!CanRetry(r)) throw new Infrastructure.ConflictException($"Повторити можна лише задачу в стані «{EhealthOutboxStatuses.Label(EhealthOutboxStatuses.Failed)}» або «{EhealthOutboxStatuses.Label(EhealthOutboxStatuses.Dead)}»; зараз: «{EhealthOutboxStatuses.Label(r.Status)}»");
        Revive(r, DateTime.UtcNow);
        await SyncSubmissionAsync(r, EhealthSubmitStatuses.Processing, null);
        await _db.SaveChangesAsync();
        return r;
    }

    public async Task<EheOutbox> CancelAsync(string id)
    {
        var r = await _db.EhealthOutbox.FirstOrDefaultAsync(x => x.Id == id) ?? throw new Infrastructure.NotFoundException("Задачу відправки в ЕСОЗ не знайдено");
        if (!CanCancel(r)) throw new Infrastructure.ConflictException($"Скасувати можна задачу, яка ще не передана в ЕСОЗ (у черзі або зупинена); зараз: «{EhealthOutboxStatuses.Label(r.Status)}»");
        r.Status = EhealthOutboxStatuses.Dead; r.ErrorCode = EhealthOutboxErrors.CancelledByUser; r.LastError = "Скасовано користувачем"; r.NextAttemptAt = null; r.UpdatedAt = DateTime.UtcNow;
        // підписаний пакет лишається збереженим: відправку можна відновити кнопкою «Повторити»
        await SyncSubmissionAsync(r, r.Kind == EhealthOutboxKinds.SubmitDr ? EhealthSubmitStatuses.Signed : null, "Скасовано користувачем");
        await _db.SaveChangesAsync();
        return r;
    }

    private async Task SyncSubmissionAsync(EheOutbox r, string? status, string? error)
    {
        var sub = await _db.EhealthSubmissions.FirstOrDefaultAsync(s => s.Id == r.EntityId);
        if (sub == null || status == null) return;
        if (r.Kind == EhealthOutboxKinds.CompleteSr) return; // статус REGISTERED лишається, поки погашення не виконано
        sub.Status = status; sub.LastError = error; sub.NextAttemptAt = null;
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == sub.OrderId);
        if (order != null) order.EhealthStatus = status;
    }
}

/// <summary>Рушій виконання черги. Singleton; кожна задача виконується у власному scope (власний DbContext і клієнт).</summary>
public sealed class EhealthOutboxEngine
{
    private readonly IServiceScopeFactory _scopes;
    private readonly EhealthOptions _options;
    private readonly ILogger<EhealthOutboxEngine> _log;
    public string WorkerId { get; } = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid().ToString("N")[..6]}";

    public EhealthOutboxEngine(IServiceScopeFactory scopes, EhealthOptions options, ILogger<EhealthOutboxEngine> log) { _scopes = scopes; _options = options; _log = log; }

    /// <summary>Повертає у чергу записи SENDING, у яких вичерпано LockedUntil (воркер упав під час виконання).</summary>
    public async Task<int> RecoverStuckAsync(DateTime now, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var msg = $"Відновлено після збою: запис довше {_options.Outbox.SendingTimeoutMinutes} хв був у стані відправки";
        var withJob = await db.EhealthOutbox.Where(x => x.Status == EhealthOutboxStatuses.Sending && x.LockedUntil < now && x.JobId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, EhealthOutboxStatuses.WaitingJob).SetProperty(x => x.LockedBy, (string?)null).SetProperty(x => x.LockedUntil, (DateTime?)null)
                .SetProperty(x => x.NextAttemptAt, (DateTime?)now).SetProperty(x => x.ErrorCode, EhealthOutboxErrors.Recovered).SetProperty(x => x.LastError, msg).SetProperty(x => x.UpdatedAt, now), ct);
        var noJob = await db.EhealthOutbox.Where(x => x.Status == EhealthOutboxStatuses.Sending && x.LockedUntil < now && x.JobId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, EhealthOutboxStatuses.New).SetProperty(x => x.LockedBy, (string?)null).SetProperty(x => x.LockedUntil, (DateTime?)null)
                .SetProperty(x => x.NextAttemptAt, (DateTime?)now).SetProperty(x => x.ErrorCode, EhealthOutboxErrors.Recovered).SetProperty(x => x.LastError, msg).SetProperty(x => x.UpdatedAt, now), ct);
        return withJob + noJob;
    }

    /// <summary>Один прохід: відновлення покинутих записів, вибірка задач, термін яких настав, виконання з обмеженням паралелізму.</summary>
    public async Task<int> RunDueAsync(DateTime? now = null, CancellationToken ct = default)
    {
        var t = now ?? DateTime.UtcNow;
        await RecoverStuckAsync(t, ct);
        List<string> ids;
        var parallel = Math.Max(1, _options.Outbox.MaxParallel);
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
            if (db.Database.IsSqlite()) parallel = 1; // SQLite — один писач: паралельні задачі лише дають «database is locked» (на PostgreSQL працює MaxParallel)
            ids = await db.EhealthOutbox.AsNoTracking()
                .Where(x => (x.Status == EhealthOutboxStatuses.New || x.Status == EhealthOutboxStatuses.WaitingJob) && (x.NextAttemptAt == null || x.NextAttemptAt <= t))
                .OrderBy(x => x.NextAttemptAt).Take(100).Select(x => x.Id).ToListAsync(ct);
        }
        if (ids.Count == 0) return 0;
        var gate = new SemaphoreSlim(parallel);
        var done = 0;
        await Task.WhenAll(ids.Select(async id =>
        {
            await gate.WaitAsync(ct);
            try { if (await ProcessOneAsync(id, t, ct)) Interlocked.Increment(ref done); }
            finally { gate.Release(); }
        }));
        return done;
    }

    /// <summary>Займає запис (атомарно) і виконує один крок. false — запис уже зайнято іншим воркером або ще не настав час.</summary>
    public async Task<bool> ProcessOneAsync(string id, DateTime now, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        if (!await TryClaimAsync(db, id, now, WorkerId, ct)) return false;
        var row = await db.EhealthOutbox.FirstAsync(x => x.Id == id, ct);
        var client = scope.ServiceProvider.GetRequiredService<IEhealthClient>();
        try
        {
            var step = new Step(db, client, row, now, _options, scope.ServiceProvider.GetRequiredService<EhealthOutboxService>());
            switch (row.Kind)
            {
                case EhealthOutboxKinds.SubmitDr: await step.SubmitAsync(ct); break;
                case EhealthOutboxKinds.CompleteSr: await step.CompleteAsync(ct); break;
                case EhealthOutboxKinds.CancelDr: await step.CancelAsync(ct); break;
                default: step.Fail("CONFIG", $"Невідомий тип задачі {row.Kind}"); break;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.LogError(ex, "Непередбачена помилка виконання задачі ЕСОЗ {Id}", id);
            row.Attempts = Math.Max(row.Attempts, 1);
            row.LastError = "Внутрішня помилка: " + (ex.Message.Length > 300 ? ex.Message[..300] : ex.Message); row.ErrorCode = "UNEXPECTED";
            row.Status = row.Attempts >= _options.Outbox.MaxAttempts ? EhealthOutboxStatuses.Dead : EhealthOutboxStatuses.New;
            row.NextAttemptAt = now + EhealthBackoff.RetryDelay(row.Attempts, _options.Outbox);
        }
        row.LockedBy = null; row.LockedUntil = null; row.UpdatedAt = now;
        await db.SaveChangesAsync(CancellationToken.None);
        return true;
    }

    /// <summary>Атомарне блокування запису: рівно один виклик отримає true (UPDATE із перевіркою стану).</summary>
    public async Task<bool> TryClaimAsync(LisDbContext db, string id, DateTime now, string worker, CancellationToken ct = default)
    {
        var until = now.AddMinutes(Math.Max(1, _options.Outbox.SendingTimeoutMinutes));
        var rows = await db.EhealthOutbox
            .Where(x => x.Id == id && (x.Status == EhealthOutboxStatuses.New || x.Status == EhealthOutboxStatuses.WaitingJob) && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, EhealthOutboxStatuses.Sending).SetProperty(x => x.LockedBy, (string?)worker).SetProperty(x => x.LockedUntil, (DateTime?)until).SetProperty(x => x.UpdatedAt, now), ct);
        return rows == 1;
    }

    // ------------------------------------------------------------------ кроки
    private sealed class Step
    {
        private readonly LisDbContext _db;
        private readonly IEhealthClient _client;
        private readonly EheOutbox _row;
        private readonly DateTime _now;
        private readonly EhealthOptions _o;
        private readonly JsonObject _payload;

        public Step(LisDbContext db, IEhealthClient client, EheOutbox row, DateTime now, EhealthOptions o, EhealthOutboxService _)
        {
            _db = db; _client = client; _row = row; _now = now; _o = o;
            _payload = (JsonNode.Parse(row.Payload) as JsonObject) ?? new JsonObject();
        }

        private string P(string key) => _payload[key]?.GetValue<string>() ?? "";
        private string? PN(string key) => _payload[key]?.GetValue<string>();

        public void Fail(string code, string message, string? submissionStatus = null, LabEhealthSubmission? sub = null, LabOrder? order = null)
        {
            _row.Status = EhealthOutboxStatuses.Failed; _row.ErrorCode = code; _row.LastError = Trim(message); _row.NextAttemptAt = null;
            Mirror(sub, order, submissionStatus, message);
        }

        private static string Trim(string s) => s.Length > 1000 ? s[..1000] : s;

        private void Mirror(LabEhealthSubmission? sub, LabOrder? order, string? status, string? error, DateTime? next = null)
        {
            if (sub == null) return;
            sub.Attempts = Math.Max(sub.Attempts, _row.Attempts);
            sub.LastError = error == null ? null : Trim(error); sub.NextAttemptAt = next;
            if (status != null) { sub.Status = status; if (order != null) order.EhealthStatus = status; }
        }

        private async Task<(LabEhealthSubmission? sub, LabOrder? order)> LoadAsync(string submissionId)
        {
            var sub = await _db.EhealthSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId);
            var order = sub == null ? null : await _db.Orders.FirstOrDefaultAsync(o => o.Id == sub.OrderId);
            return (sub, order);
        }

        /// <summary>Помилку обміну → стан задачі (повтор з витримкою / зупинка з потрібною дією).</summary>
        private async Task HandleErrorAsync(EhealthException ex, LabEhealthSubmission? sub, LabOrder? order, bool polling, Func<Task<bool>>? alreadyDone = null)
        {
            var msg = ex.Message + (ex.Invalid.Count > 0 ? " [" + string.Join("; ", ex.Invalid.Take(5)) + "]" : "");
            _row.ErrorCode = ex.Code; _row.LastError = Trim(msg);
            switch (ex.Kind)
            {
                case EhealthErrorKind.Transient:
                {
                    var tooOld = (_now - _row.CreatedOn) > TimeSpan.FromHours(_o.Outbox.JobDeadlineHours);
                    var count = polling ? ++_row.PollAttempts : _row.Attempts;
                    if ((!polling && count >= _o.Outbox.MaxAttempts) || tooOld)
                    {
                        _row.Status = EhealthOutboxStatuses.Dead; _row.NextAttemptAt = null;
                        Mirror(sub, order, EhealthSubmitStatuses.Error, "Спроби вичерпано: " + msg);
                    }
                    else
                    {
                        var next = _now + (polling ? EhealthBackoff.PollDelay(count, _o.Outbox) : EhealthBackoff.RetryDelay(count, _o.Outbox));
                        _row.Status = polling ? EhealthOutboxStatuses.WaitingJob : EhealthOutboxStatuses.New; _row.NextAttemptAt = next;
                        if (_row.Kind != EhealthOutboxKinds.CompleteSr) Mirror(sub, order, EhealthSubmitStatuses.Error, msg, next);
                        else Mirror(sub, order, null, msg, next);
                    }
                    break;
                }
                case EhealthErrorKind.Auth:
                    Fail("AUTH", msg, _row.Kind == EhealthOutboxKinds.CompleteSr ? null : EhealthSubmitStatuses.AuthRequired, sub, order); break;
                case EhealthErrorKind.Conflict when alreadyDone != null && await alreadyDone():
                    break;
                case EhealthErrorKind.Disabled or EhealthErrorKind.Config:
                    Fail(ex.Code, msg, _row.Kind == EhealthOutboxKinds.CompleteSr ? null : EhealthSubmitStatuses.Error, sub, order); break;
                default:
                    Fail(ex.Code, msg, _row.Kind == EhealthOutboxKinds.CompleteSr ? null : EhealthSubmitStatuses.Rejected, sub, order); break;
            }
        }

        private void Waiting(string jobId, LabEhealthSubmission? sub, LabOrder? order, string? status)
        {
            _row.JobId = jobId; _row.Status = EhealthOutboxStatuses.WaitingJob; _row.PollAttempts = 0; _row.ErrorCode = null; _row.LastError = null;
            _row.NextAttemptAt = _now + EhealthBackoff.PollDelay(0, _o.Outbox);
            if (sub != null) { sub.JobId = jobId; sub.Attempts = Math.Max(sub.Attempts, _row.Attempts); }
            Mirror(sub, order, status, null);
            if (sub != null) sub.JobId = jobId;
        }

        /// <summary>Опитує job. Повертає null, якщо результат ще не відомий (стан задачі вже оновлено).</summary>
        private async Task<EhealthJob?> PollAsync(LabEhealthSubmission? sub, LabOrder? order)
        {
            EhealthJob job;
            try { job = await _client.GetJobAsync(_row.JobId!); }
            catch (EhealthException ex) { await HandleErrorAsync(ex, sub, order, polling: true); return null; }
            if (job.IsPending)
            {
                _row.PollAttempts++;
                if ((_now - _row.CreatedOn) > TimeSpan.FromHours(_o.Outbox.JobDeadlineHours))
                    Fail(EhealthOutboxErrors.JobTimeout, $"Job {_row.JobId} не завершився за {_o.Outbox.JobDeadlineHours} год", _row.Kind == EhealthOutboxKinds.CompleteSr ? null : EhealthSubmitStatuses.Error, sub, order);
                else { _row.Status = EhealthOutboxStatuses.WaitingJob; _row.NextAttemptAt = _now + EhealthBackoff.PollDelay(_row.PollAttempts, _o.Outbox); }
                return null;
            }
            return job;
        }

        // --------------------------------------------------------------- SUBMIT_DR
        public async Task SubmitAsync(CancellationToken ct)
        {
            var (sub, order) = await LoadAsync(_row.EntityId);
            if (sub == null || string.IsNullOrEmpty(sub.SignedData)) { Fail(EhealthOutboxErrors.MissingData, "Немає підписаного пакета для відправки"); return; }
            var patientId = P("patientId"); var reportId = P("reportId");

            if (_row.JobId == null)
            {
                _row.Attempts++;
                try
                {
                    // повтор після невизначеного результату: звіт міг бути вже зареєстрований
                    if ((_row.Attempts > 1 || sub.Attempts > 0 || _row.ErrorCode == EhealthOutboxErrors.Recovered) && await _client.GetDiagnosticReportAsync(patientId, reportId, ct) is { Status: "final" })
                    { await RegisteredAsync(sub, order); return; }
                    var job = await _client.SubmitDiagnosticReportPackageAsync(patientId, sub.SignedData!, ct);
                    if (job.IsProcessed && string.IsNullOrEmpty(job.Id)) { await RegisteredAsync(sub, order); return; }
                    Waiting(job.Id, sub, order, EhealthSubmitStatuses.Processing);
                }
                catch (EhealthException ex)
                {
                    await HandleErrorAsync(ex, sub, order, polling: false, alreadyDone: async () =>
                    {
                        // 409: дубль id — перевіряємо, чи звіт уже є в ЕСОЗ
                        try { if (await _client.GetDiagnosticReportAsync(patientId, reportId, ct) is { Status: "final" }) { await RegisteredAsync(sub, order); return true; } }
                        catch (EhealthException) { /* лишаємо як конфлікт */ }
                        Fail(ex.Code, ex.Message, EhealthSubmitStatuses.Rejected, sub, order);
                        return true;
                    });
                }
                return;
            }

            var res = await PollAsync(sub, order);
            if (res == null) return;
            if (res.IsProcessed) await RegisteredAsync(sub, order);
            else
            {
                var msg = $"ЕСОЗ відхилила пакет: {res.Error ?? res.RawStatus}" + (res.Invalid is { Count: > 0 } ? " [" + string.Join("; ", res.Invalid.Take(5)) + "]" : "");
                Fail(EhealthOutboxErrors.JobFailed, msg, EhealthSubmitStatuses.Rejected, sub, order);
                _row.JobId = null; // повтор відправляє пакет заново (після перевірки, що звіту в ЕСОЗ немає)
            }
        }

        private async Task RegisteredAsync(LabEhealthSubmission sub, LabOrder? order)
        {
            var reportId = P("reportId");
            sub.EhealthReportId = reportId; sub.CompletedAt = _now;
            var report = order?.DiagnosticReportId == null ? null : await _db.DiagnosticReports.FirstOrDefaultAsync(r => r.Id == order.DiagnosticReportId);
            if (report != null) report.EhealthId = reportId;
            _row.Status = EhealthOutboxStatuses.Done; _row.DoneAt = _now; _row.NextAttemptAt = null; _row.ErrorCode = null; _row.LastError = null;
            Mirror(sub, order, EhealthSubmitStatuses.Registered, null);
            if (PN("serviceRequestId") is { Length: > 0 } sr)
            {
                // Погашення направлення — ОКРЕМА задача, лише після того, як звіт існує в ЕСОЗ
                var svc = new EhealthOutboxService(_db);
                await svc.EnqueueAsync(EhealthOutboxKinds.CompleteSr, sub.Id, sub.OrderId, $"COMPLETE_SR:{sr}:{reportId}",
                    new { serviceRequestId = sr, reportId, patientId = P("patientId"), referralId = PN("referralId"), submissionId = sub.Id }, _now);
            }
        }

        // --------------------------------------------------------------- COMPLETE_SR
        public async Task CompleteAsync(CancellationToken ct)
        {
            var (sub, order) = await LoadAsync(_row.EntityId);
            var srId = P("serviceRequestId"); var reportId = P("reportId");
            if (sub == null || srId == "" || reportId == "") { Fail(EhealthOutboxErrors.MissingData, "Немає даних для погашення направлення"); return; }
            if (_row.JobId == null)
            {
                _row.Attempts++;
                try
                {
                    var job = await _client.CompleteServiceRequestAsync(srId, "diagnostic_report", reportId, null, "default", ct);
                    if (job.IsProcessed && string.IsNullOrEmpty(job.Id)) { await CompletedAsync(sub, order); return; }
                    Waiting(job.Id, sub, order, null);
                    sub.Attempts = Math.Max(sub.Attempts, _row.Attempts);
                }
                catch (EhealthException ex) { await HandleErrorAsync(ex, sub, order, polling: false); }
                return;
            }
            var res = await PollAsync(sub, order);
            if (res == null) return;
            if (res.IsProcessed) await CompletedAsync(sub, order);
            else
            {
                Fail(EhealthOutboxErrors.JobFailed, $"ЕСОЗ не погасила направлення: {res.Error ?? res.RawStatus}", null, sub, order);
                sub.LastError = _row.LastError; _row.JobId = null;
            }
        }

        private async Task CompletedAsync(LabEhealthSubmission sub, LabOrder? order)
        {
            var refId = PN("referralId") ?? order?.EhealthReferralId;
            var r = refId == null ? null : await _db.Referrals.FirstOrDefaultAsync(x => x.Id == refId);
            if (r != null)
            {
                r.StatusId = MedLinkEnums.ReferralStatusCompleted; r.ProcessingStatusInEhealthId = MedLinkEnums.ProcessingCompleted; r.EhealthStatus = "completed";
                r.CompleteDateInEhealth = _now; r.CompletedWithItemEntityId = order?.DiagnosticReportId; r.CompletedWithItemEntityName = "DiagnosticReport";
            }
            _row.Status = EhealthOutboxStatuses.Done; _row.DoneAt = _now; _row.NextAttemptAt = null; _row.ErrorCode = null; _row.LastError = null;
            Mirror(sub, order, EhealthSubmitStatuses.Completed, null);
        }

        // --------------------------------------------------------------- CANCEL_DR
        public async Task CancelAsync(CancellationToken ct)
        {
            var (annul, order) = await LoadAsync(_row.EntityId);
            if (annul == null || string.IsNullOrEmpty(annul.SignedData)) { Fail(EhealthOutboxErrors.MissingData, "Немає підписаного пакета скасування"); return; }
            var patientId = P("patientId");
            if (_row.JobId == null)
            {
                _row.Attempts++;
                try
                {
                    var job = await _client.CancelDiagnosticReportPackageAsync(patientId, annul.SignedData!, ct);
                    if (job.IsProcessed && string.IsNullOrEmpty(job.Id)) { await CancelledAsync(annul, order); return; }
                    Waiting(job.Id, annul, null, EhealthSubmitStatuses.Processing);
                }
                catch (EhealthException ex) { await HandleErrorAsync(ex, annul, null, polling: false); }
                return;
            }
            var res = await PollAsync(annul, null);
            if (res == null) return;
            if (res.IsProcessed) await CancelledAsync(annul, order);
            else
            {
                Fail(EhealthOutboxErrors.JobFailed, $"ЕСОЗ не скасувала звіт: {res.Error ?? res.RawStatus}", EhealthSubmitStatuses.Rejected, annul, null);
                _row.JobId = null;
            }
        }

        private async Task CancelledAsync(LabEhealthSubmission annul, LabOrder? order)
        {
            var origId = PN("submissionId");
            var orig = origId == null ? null : await _db.EhealthSubmissions.FirstOrDefaultAsync(s => s.Id == origId);
            if (orig != null) orig.Status = EhealthSubmitStatuses.Annulled;
            _row.Status = EhealthOutboxStatuses.Done; _row.DoneAt = _now; _row.NextAttemptAt = null; _row.ErrorCode = null; _row.LastError = null;
            Mirror(annul, null, EhealthSubmitStatuses.Annulled, null);
            annul.CompletedAt = _now;
            if (order != null)
            {
                // Виправлення: новий висновок (копія) → новий пакет з новими ідентифікаторами і новим підписом
                var old = order.DiagnosticReportId == null ? null : await _db.DiagnosticReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == order.DiagnosticReportId);
                if (old != null)
                {
                    var copy = new MisDiagnosticReport
                    {
                        RegNumber = old.RegNumber + "-1", RegDate = _now, Caption = old.Caption, Status = 0, PatientCardId = old.PatientCardId, OrganizationId = old.OrganizationId,
                        LegalEntityId = old.LegalEntityId, DivisionId = old.DivisionId, DocumentTypeId = old.DocumentTypeId, EhealthServiceCatalogServiceId = old.EhealthServiceCatalogServiceId,
                        EhealthIncomingMedicalReferralId = old.EhealthIncomingMedicalReferralId, Description = old.Description, EffectiveDateTimeStart = old.EffectiveDateTimeStart,
                        EffectiveDateTimeEnd = old.EffectiveDateTimeEnd, IssuedAt = _now, PerformerString = old.PerformerString, IsPerformerString = true, IsPrimarySource = true,
                        Comment = "Виправлений звіт після анулювання: " + PN("reason")
                    };
                    _db.DiagnosticReports.Add(copy);
                    order.DiagnosticReportId = copy.Id;
                }
                order.EhealthStatus = EhealthSubmitStatuses.Annulled;
            }
        }
    }
}

/// <summary>Фоновий воркер: періодично запускає EhealthOutboxEngine.RunDueAsync. У середовищі Testing вимкнений (тести керують рушієм вручну).</summary>
public sealed class EhealthOutboxWorker : BackgroundService
{
    private readonly EhealthOutboxEngine _engine;
    private readonly EhealthOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<EhealthOutboxWorker> _log;

    public EhealthOutboxWorker(EhealthOutboxEngine engine, EhealthOptions options, IHostEnvironment env, ILogger<EhealthOutboxWorker> log) { _engine = engine; _options = options; _env = env; _log = log; }

    public static bool IsEnabled(EhealthOptions o, IHostEnvironment env) => o.Outbox.WorkerEnabled ?? !env.IsEnvironment("Testing");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled(_options, _env)) return;
        try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); } catch (OperationCanceledException) { return; }
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.Outbox.PollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await _engine.RunDueAsync(null, stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Помилка проходу outbox ЕСОЗ"); }
            try { await Task.Delay(interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
