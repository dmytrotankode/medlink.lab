// Екран «Сповіщення»: черга сповіщень пацієнтам, події для МІС, стан каналів. Секрети (паролі, ключі, підпис) у відповідях не повертаються.
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services.Notifications;

public sealed class NotificationAdminService
{
    private static readonly string[] RetryRoles = { LabRoles.Admin, LabRoles.Registrar, LabRoles.Doctor };
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly NotificationDispatcher _dispatcher;
    private readonly MisDeliveryEngine _mis;
    private readonly MisDeliveryOptions _misOptions;
    private readonly NotifyOptions _notify;
    private readonly IAuditService _audit;

    public NotificationAdminService(LisDbContext db, IRolePolicy policy, NotificationDispatcher dispatcher, MisDeliveryEngine mis, MisDeliveryOptions misOptions, NotifyOptions notify, IAuditService audit)
    { _db = db; _policy = policy; _dispatcher = dispatcher; _mis = mis; _misOptions = misOptions; _notify = notify; _audit = audit; }

    // ------------------------------------------------------------------ сповіщення пацієнтам
    public async Task<object> ListAsync(string? status, string? channel, string? search, DateTime? from, DateTime? to, PagingQuery paging)
    {
        var q = _db.Notifications.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(n => n.Status == status);
        if (!string.IsNullOrWhiteSpace(channel)) q = q.Where(n => n.Channel == channel);
        if (from != null) q = q.Where(n => n.CreatedOn >= from);
        if (to != null) q = q.Where(n => n.CreatedOn < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var patientIds = _db.Patients.AsNoTracking().Where(p => p.Person != null && p.Person.Caption != null && p.Person.Caption.Contains(s)).Select(p => p.Id);
            var orderIds = _db.Orders.AsNoTracking().Where(o => o.OrderNumber.Contains(s)).Select(o => o.Id);
            q = q.Where(n => patientIds.Contains(n.PatientId) || (n.OrderId != null && orderIds.Contains(n.OrderId)) || n.PatientId == s);
        }
        var total = await q.CountAsync();
        var page = paging.SafePage; var size = paging.SafePageSize;
        var rows = await q.OrderByDescending(n => n.CreatedOn).Skip((page - 1) * size).Take(size).ToListAsync();
        var pids = rows.Select(r => r.PatientId).Distinct().ToList();
        var names = await _db.Patients.AsNoTracking().Where(p => pids.Contains(p.Id)).Select(p => new { p.Id, Name = p.Person != null ? p.Person.Caption : null }).ToDictionaryAsync(x => x.Id, x => x.Name);
        var oids = rows.Where(r => r.OrderId != null).Select(r => r.OrderId!).Distinct().ToList();
        var numbers = await _db.Orders.AsNoTracking().Where(o => oids.Contains(o.Id)).Select(o => new { o.Id, o.OrderNumber }).ToDictionaryAsync(x => x.Id, x => x.OrderNumber);
        var chState = await _dispatcher.ChannelStateAsync(_db);
        // «Повторити» має сенс, лише якщо канал налаштовано й увімкнено (інакше запис знову стане «Не налаштовано»)
        bool Retryable(string channel, string status) => NotifyStatuses.CanRetry(status) && (status != NotifyStatuses.NotConfigured || (chState.TryGetValue(channel, out var cs) && cs.Configured && cs.Enabled));
        return new
        {
            items = rows.Select(n => new
            {
                n.Id, n.PatientId, patientName = names.GetValueOrDefault(n.PatientId), n.OrderId, orderNumber = n.OrderId != null ? numbers.GetValueOrDefault(n.OrderId) : null,
                n.Channel, channelLabel = NotifyChannels.Label(n.Channel), n.Status, statusLabel = NotifyStatuses.Label(n.Status),
                n.ReasonCode, reasonLabel = NotifyReasons.Label(n.ReasonCode), n.Attempts, n.NextAttemptAt, n.LastError, n.RecipientMasked, n.SentAt, n.CreatedOn,
                canRetry = Retryable(n.Channel, n.Status)
            }),
            total, page, pageSize = size
        };
    }

    public async Task<object> SummaryAsync()
    {
        var byStatus = await _db.Notifications.AsNoTracking().GroupBy(n => n.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync();
        var dict = byStatus.ToDictionary(x => x.status, x => x.count);
        return new { total = dict.Values.Sum(), byStatus = NotifyStatuses.All.Select(s => new { status = s, label = NotifyStatuses.Label(s), count = dict.GetValueOrDefault(s) }) };
    }

    public async Task<object> RetryAsync(string id)
    {
        _policy.Require("Повтор сповіщення", RetryRoles);
        var row = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id) ?? throw new NotFoundException("Сповіщення не знайдено");
        if (!NotifyStatuses.CanRetry(row.Status)) throw new ConflictException($"Повторити можна лише сповіщення зі статусом «Помилка», «Пропущено» або «Не налаштовано» (зараз: «{NotifyStatuses.Label(row.Status)}»)");
        if (row.Status == NotifyStatuses.NotConfigured)
        {
            var st = await _dispatcher.ChannelStateAsync(_db);
            if (!st.TryGetValue(row.Channel, out var cs) || !cs.Configured || !cs.Enabled) throw new ConflictException("Канал не налаштовано або вимкнено — повторити не можна. Спочатку налаштуйте канал.");
        }
        var now = DateTime.UtcNow;
        row.Status = NotifyStatuses.Queued; row.Attempts = 0; row.NextAttemptAt = now; row.LastError = null; row.ReasonCode = null; row.UpdatedAt = now;
        _audit.Log("NOTIFY_RETRY", "lab_patient_notification", row.Id, null, new { row.Channel });
        await _db.SaveChangesAsync();
        return new { row.Id, row.Status, statusLabel = NotifyStatuses.Label(row.Status) };
    }

    public async Task<object> RunNowAsync()
    {
        _policy.Require("Обробка черги сповіщень", LabRoles.Admin);
        var notifications = await _dispatcher.RunDueAsync();
        var events = await _mis.RunDueAsync();
        return new { notifications, misEvents = events };
    }

    // ------------------------------------------------------------------ канали
    public async Task<object> ChannelsAsync()
    {
        var state = await _dispatcher.ChannelStateAsync(_db);
        var counts = await _db.Notifications.AsNoTracking().GroupBy(n => n.Channel).Select(g => new { channel = g.Key, count = g.Count(), pending = g.Count(n => n.Status == NotifyStatuses.NotConfigured) }).ToListAsync();
        return NotifyChannels.All.Select(c =>
        {
            var (configured, enabled) = state[c];
            var st = !configured ? "NOT_CONFIGURED" : !enabled ? "OFF" : "READY";
            var cnt = counts.FirstOrDefault(x => x.channel == c);
            return new
            {
                channel = c, label = NotifyChannels.Label(c), configured, enabled, state = st,
                stateLabel = st switch { "READY" => "Працює", "OFF" => "Вимкнено адміністратором", _ => "Не налаштовано" },
                hint = configured ? null : "Параметри підключення задаються адміністратором сервера в конфігурації (секрети — лише у змінних середовища).",
                configKeys = configured ? Array.Empty<string>() : NotifyChannels.ConfigKeys(c),
                waiting = cnt?.pending ?? 0
            };
        }).ToList();
    }

    public async Task<object> SetChannelAsync(string channel, bool enabled)
    {
        _policy.Require("Налаштування каналу сповіщень", LabRoles.Admin);
        channel = (channel ?? "").ToUpperInvariant();
        if (!NotifyChannels.All.Contains(channel)) throw new NotFoundException("Канал не знайдено");
        var row = await _db.NotifyChannelSettings.FirstOrDefaultAsync(c => c.Channel == channel);
        if (row == null) _db.NotifyChannelSettings.Add(row = new LabNotifyChannel { Channel = channel });
        row.IsEnabled = enabled;
        _audit.Log("NOTIFY_CHANNEL", "lab_notify_channel", row.Id, null, new { channel, enabled });
        await _db.SaveChangesAsync();
        return new { channel, enabled };
    }

    // ------------------------------------------------------------------ події для МІС
    public async Task<object> MisListAsync(string? status, string? eventName, string? search, DateTime? from, DateTime? to, PagingQuery paging)
    {
        var q = _db.MisEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.Status == status);
        if (!string.IsNullOrWhiteSpace(eventName)) q = q.Where(e => e.Event == eventName);
        if (from != null) q = q.Where(e => e.At >= from);
        if (to != null) q = q.Where(e => e.At < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var orderIds = _db.Orders.AsNoTracking().Where(o => o.OrderNumber.Contains(s)).Select(o => o.Id);
            q = q.Where(e => (e.OrderId != null && orderIds.Contains(e.OrderId)) || (e.MisReferralId != null && e.MisReferralId.Contains(s)));
        }
        var total = await q.CountAsync();
        var page = paging.SafePage; var size = paging.SafePageSize;
        var rows = await q.OrderByDescending(e => e.At).Skip((page - 1) * size).Take(size).ToListAsync();
        var oids = rows.Where(r => r.OrderId != null).Select(r => r.OrderId!).Distinct().ToList();
        var numbers = await _db.Orders.AsNoTracking().Where(o => oids.Contains(o.Id)).Select(o => new { o.Id, o.OrderNumber }).ToDictionaryAsync(x => x.Id, x => x.OrderNumber);
        return new
        {
            items = rows.Select(e => new
            {
                e.Id, e.OrderId, orderNumber = e.OrderId != null ? numbers.GetValueOrDefault(e.OrderId) : null, e.MisReferralId, e.Event, eventLabel = MisEvents.Label(e.Event),
                e.Status, statusLabel = MisDeliveryStatuses.Label(e.Status), e.Attempts, e.NextAttemptAt, e.DeliveredAt, e.HttpStatus, e.LastError, e.At,
                canRetry = MisDeliveryStatuses.CanRetry(e.Status) && (e.Status != MisDeliveryStatuses.NotConfigured || _misOptions.IsConfigured)
            }),
            total, page, pageSize = size
        };
    }

    public async Task<object> MisStatusAsync()
    {
        var byStatus = await _db.MisEvents.AsNoTracking().GroupBy(e => e.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync();
        var dict = byStatus.ToDictionary(x => x.status, x => x.count);
        string? host = null;
        if (_misOptions.HasValidUrl) host = new Uri(_misOptions.Url!).Authority; // лише хост: шлях і параметри можуть містити токени
        var state = _misOptions.IsConfigured ? "READY" : "NOT_CONFIGURED";
        return new
        {
            mode = _misOptions.IsWebhook ? "Webhook" : "Disabled", configured = _misOptions.IsConfigured, state,
            stateLabel = state == "READY" ? "Доставка працює" : "Доставку подій у МІС не налаштовано",
            host, signed = !string.IsNullOrWhiteSpace(_misOptions.Secret),
            configKeys = _misOptions.IsConfigured ? Array.Empty<string>() : new[] { "MisDelivery:Mode", "MisDelivery:Url", "MisDelivery:Secret" },
            total = dict.Values.Sum(), byStatus = MisDeliveryStatuses.All.Select(s => new { status = s, label = MisDeliveryStatuses.Label(s), count = dict.GetValueOrDefault(s) })
        };
    }

    public async Task<object> MisRetryAsync(string id)
    {
        _policy.Require("Повтор доставки події", RetryRoles);
        var row = await _db.MisEvents.FirstOrDefaultAsync(e => e.Id == id) ?? throw new NotFoundException("Подію не знайдено");
        if (!MisDeliveryStatuses.CanRetry(row.Status)) throw new ConflictException($"Повторити можна лише події зі статусом «Помилка» або «Не налаштовано» (зараз: «{MisDeliveryStatuses.Label(row.Status)}»)");
        var now = DateTime.UtcNow;
        row.Status = MisDeliveryStatuses.Queued; row.Attempts = 0; row.NextAttemptAt = now; row.LastError = null; row.UpdatedAt = now;
        _audit.Log("MIS_EVENT_RETRY", "lab_mis_event_log", row.Id, null, new { row.Event });
        await _db.SaveChangesAsync();
        return new { row.Id, row.Status, statusLabel = MisDeliveryStatuses.Label(row.Status) };
    }
}
