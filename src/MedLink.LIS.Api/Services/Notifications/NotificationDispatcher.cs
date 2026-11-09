// =============================================================================
// Відправник сповіщень пацієнтам: черга lab_patient_notification → провайдер каналу.
// Статуси: QUEUED → SENDING → SENT | FAILED | SKIPPED | NOT_CONFIGURED.
//   • канал не налаштований або вимкнений → NOT_CONFIGURED (не імітація успіху; запис відправиться автоматично, щойно канал стане готовим);
//   • немає згоди пацієнта (SubscribedToNotifications) або контакту → SKIPPED із причиною;
//   • тимчасова помилка → повтор з експоненційною паузою до MaxAttempts, далі FAILED; остаточна помилка → FAILED одразу;
//   • ідемпотентність: унікальний IdempotencyKey при постановці + атомарне блокування запису (UPDATE … WHERE status IN …) при відправці.
// =============================================================================
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services.Notifications;

/// <summary>Постановка сповіщення в чергу з ідемпотентністю та вибором каналу за контактами пацієнта.</summary>
public static class NotifyQueue
{
    /// <summary>Типовий канал: пошта, якщо є e-mail, інакше SMS.</summary>
    public static string PreferredChannel(CmnPerson? person) => !string.IsNullOrWhiteSpace(person?.Email) ? NotifyChannels.Email : NotifyChannels.Sms;

    /// <summary>Додає запис (без SaveChanges). Повторний виклик з тим самим ключем нічого не створює і повертає null.</summary>
    public static async Task<LabPatientNotification?> EnqueueAsync(LisDbContext db, string patientId, string? orderId, string channel, string templateCode, string link, string idempotencyKey, DateTime? now = null)
    {
        var t = now ?? DateTime.UtcNow;
        if (db.Notifications.Local.Any(n => n.IdempotencyKey == idempotencyKey) || await db.Notifications.AsNoTracking().AnyAsync(n => n.IdempotencyKey == idempotencyKey)) return null;
        // Не плодимо однакові повідомлення: для замовлення й каналу вже є невідправлене — нове не потрібне;
        // підсумкове «результат готовий» замінює невідправлені часткові.
        if (orderId != null)
        {
            var pendingStates = new[] { NotifyStatuses.Queued, NotifyStatuses.NotConfigured };
            var pending = await db.Notifications.Where(n => n.OrderId == orderId && n.Channel == channel && pendingStates.Contains(n.Status)).ToListAsync();
            if (pending.Any(n => n.TemplateCode == templateCode || (templateCode == NotifyTemplates.ResultPartial && n.TemplateCode == NotifyTemplates.ResultReady))) return null;
            foreach (var n in pending.Where(n => templateCode == NotifyTemplates.ResultReady && n.TemplateCode == NotifyTemplates.ResultPartial))
            { n.Status = NotifyStatuses.Skipped; n.ReasonCode = NotifyReasons.Superseded; n.UpdatedAt = t; }
        }
        var row = new LabPatientNotification
        {
            PatientId = patientId, OrderId = orderId, Channel = channel, Payload = link, TemplateCode = templateCode, IdempotencyKey = idempotencyKey,
            Status = NotifyStatuses.Queued, NextAttemptAt = t, CreatedOn = t, UpdatedAt = t
        };
        db.Notifications.Add(row);
        return row;
    }
}

public sealed class NotificationDispatcher
{
    private readonly IServiceScopeFactory _scopes;
    private readonly NotifyOptions _options;
    private readonly Dictionary<string, INotifyChannelProvider> _providers;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationDispatcher> _log;
    public string WorkerId { get; } = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid().ToString("N")[..6]}";

    public NotificationDispatcher(IServiceScopeFactory scopes, NotifyOptions options, IEnumerable<INotifyChannelProvider> providers, IConfiguration config, ILogger<NotificationDispatcher> log)
    {
        _scopes = scopes; _options = options; _config = config; _log = log;
        _providers = providers.ToDictionary(p => p.Channel, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Канали, яким дозволено відправляти: налаштовані в конфігурації й не вимкнені адміністратором. Ключ — канал, значення — (налаштований, увімкнений).</summary>
    public async Task<Dictionary<string, (bool Configured, bool Enabled)>> ChannelStateAsync(LisDbContext db, CancellationToken ct = default)
    {
        var off = await db.NotifyChannelSettings.AsNoTracking().Where(c => !c.IsEnabled).Select(c => c.Channel).ToListAsync(ct);
        return NotifyChannels.All.ToDictionary(c => c, c => (_providers.TryGetValue(c, out var p) && p.IsConfigured, !off.Contains(c)));
    }

    public async Task<int> RecoverStuckAsync(DateTime now, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        return await db.Notifications.Where(n => n.Status == NotifyStatuses.Sending && n.LockedUntil < now)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, NotifyStatuses.Queued).SetProperty(n => n.LockedBy, (string?)null).SetProperty(n => n.LockedUntil, (DateTime?)null)
                .SetProperty(n => n.NextAttemptAt, (DateTime?)now).SetProperty(n => n.LastError, "Відновлено після збою: відправка не завершилась").SetProperty(n => n.UpdatedAt, (DateTime?)now), ct);
    }

    /// <summary>Один прохід: звіряє канали, переводить недоступні в NOT_CONFIGURED, відправляє записи, термін яких настав. Повертає кількість оброблених.</summary>
    public async Task<int> RunDueAsync(DateTime? now = null, CancellationToken ct = default)
    {
        var t = now ?? DateTime.UtcNow;
        await RecoverStuckAsync(t, ct);
        List<string> ids;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
            var state = await ChannelStateAsync(db, ct);
            var ready = state.Where(kv => kv.Value.Configured && kv.Value.Enabled).Select(kv => kv.Key).ToList();
            var off = state.Where(kv => kv.Value.Configured && !kv.Value.Enabled).Select(kv => kv.Key).ToList();
            // недоступний канал — чесний статус, а не «відправлено»
            await db.Notifications.Where(n => n.Status == NotifyStatuses.Queued && !ready.Contains(n.Channel) && off.Contains(n.Channel))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, NotifyStatuses.NotConfigured).SetProperty(n => n.ReasonCode, NotifyReasons.ChannelOff).SetProperty(n => n.UpdatedAt, (DateTime?)t), ct);
            await db.Notifications.Where(n => n.Status == NotifyStatuses.Queued && !ready.Contains(n.Channel) && !off.Contains(n.Channel))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, NotifyStatuses.NotConfigured).SetProperty(n => n.ReasonCode, NotifyReasons.NotConfigured).SetProperty(n => n.UpdatedAt, (DateTime?)t), ct);
            // канал став готовим → раніше відкладені записи повертаються в чергу
            await db.Notifications.Where(n => n.Status == NotifyStatuses.NotConfigured && ready.Contains(n.Channel))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, NotifyStatuses.Queued).SetProperty(n => n.ReasonCode, (string?)null).SetProperty(n => n.NextAttemptAt, (DateTime?)t).SetProperty(n => n.UpdatedAt, (DateTime?)t), ct);
            ids = await db.Notifications.AsNoTracking()
                .Where(n => n.Status == NotifyStatuses.Queued && ready.Contains(n.Channel) && (n.NextAttemptAt == null || n.NextAttemptAt <= t))
                .OrderBy(n => n.CreatedOn).Take(100).Select(n => n.Id).ToListAsync(ct);
        }
        var done = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            if (await ProcessOneAsync(id, t, ct)) done++;
        }
        return done;
    }

    private async Task<bool> ProcessOneAsync(string id, DateTime now, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var until = now.AddMinutes(Math.Max(1, _options.SendingTimeoutMinutes));
        var claimed = await db.Notifications.Where(n => n.Id == id && n.Status == NotifyStatuses.Queued && (n.NextAttemptAt == null || n.NextAttemptAt <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, NotifyStatuses.Sending).SetProperty(n => n.LockedBy, (string?)WorkerId).SetProperty(n => n.LockedUntil, (DateTime?)until).SetProperty(n => n.UpdatedAt, (DateTime?)now), ct);
        if (claimed != 1) return false;
        var row = await db.Notifications.FirstAsync(n => n.Id == id, ct);
        try
        {
            await DeliverAsync(db, row, now, ct);
        }
        catch (OperationCanceledException) { row.Status = NotifyStatuses.Queued; }
        catch (Exception ex)
        {
            _log.LogError(ex, "Непередбачена помилка відправки сповіщення {Id}", id);
            Fail(row, true, "Внутрішня помилка: " + (ex.Message.Length > 300 ? ex.Message[..300] : ex.Message), now);
        }
        row.LockedBy = null; row.LockedUntil = null; row.UpdatedAt = now;
        await db.SaveChangesAsync(CancellationToken.None);
        return true;
    }

    private async Task DeliverAsync(LisDbContext db, LabPatientNotification row, DateTime now, CancellationToken ct)
    {
        var patient = await db.Patients.AsNoTracking().Include(p => p.Person).FirstOrDefaultAsync(p => p.Id == row.PatientId, ct);
        if (patient?.Person == null) { Skip(row, NotifyReasons.PatientMissing); return; }
        if (!patient.Person.SubscribedToNotifications) { Skip(row, NotifyReasons.NoConsent); return; }
        if (!_providers.TryGetValue(row.Channel, out var provider)) { Skip(row, NotifyReasons.Unsupported); return; }
        var raw = row.Channel == NotifyChannels.Email ? patient.Person.Email?.Trim() : patient.Person.Phone;
        var recipient = row.Channel == NotifyChannels.Email ? raw : (string.IsNullOrWhiteSpace(raw) ? null : NotifyMask.NormalizePhone(raw));
        if (string.IsNullOrWhiteSpace(recipient)) { Skip(row, NotifyReasons.NoContact); return; }
        row.RecipientMasked = NotifyMask.Recipient(row.Channel, recipient);

        var link = row.Payload.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? row.Payload : (_config["Lab:PublicBaseUrl"] ?? "").TrimEnd('/') + row.Payload;
        var (subject, body) = row.TemplateCode == null
            ? ("Повідомлення лабораторії", row.Payload) // старі записи без шаблону
            : NotifyTemplates.Render(row.TemplateCode, row.Channel, link);
        var result = await provider.SendAsync(new NotifyMessage(row.Id, row.Channel, recipient, subject, body), ct);
        row.Attempts++;
        if (result.Ok)
        {
            row.Status = NotifyStatuses.Sent; row.SentAt = now; row.LastError = null; row.ReasonCode = null; row.ProviderMessageId = result.ProviderMessageId; row.NextAttemptAt = null;
            return;
        }
        Fail(row, result.Transient, result.Error ?? "Помилка відправки", now);
    }

    private void Fail(LabPatientNotification row, bool transient, string error, DateTime now)
    {
        if (row.Attempts < 1) row.Attempts = 1;
        row.LastError = error;
        if (transient && row.Attempts < Math.Max(1, _options.MaxAttempts))
        {
            row.Status = NotifyStatuses.Queued;
            row.NextAttemptAt = now + _options.RetryDelay(row.Attempts);
        }
        else { row.Status = NotifyStatuses.Failed; row.NextAttemptAt = null; }
    }

    private static void Skip(LabPatientNotification row, string reason)
    {
        row.Status = NotifyStatuses.Skipped; row.ReasonCode = reason; row.LastError = NotifyReasons.Label(reason); row.NextAttemptAt = null;
    }
}

public sealed class NotificationWorker : BackgroundService
{
    private readonly NotificationDispatcher _dispatcher;
    private readonly NotifyOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<NotificationWorker> _log;

    public NotificationWorker(NotificationDispatcher dispatcher, NotifyOptions options, IHostEnvironment env, ILogger<NotificationWorker> log)
    { _dispatcher = dispatcher; _options = options; _env = env; _log = log; }

    public static bool IsEnabled(NotifyOptions o, IHostEnvironment env) => o.WorkerEnabled ?? !env.IsEnvironment("Testing");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled(_options, _env)) return;
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch (OperationCanceledException) { return; }
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await _dispatcher.RunDueAsync(null, stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Помилка проходу відправника сповіщень"); }
            try { await Task.Delay(interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
