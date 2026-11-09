// =============================================================================
// Доставка подій ЛІС → МІС (FR-MIS-026): outbox lab_mis_event_log + воркер + webhook (HTTP POST) на налаштовану адресу МІС.
// Режим MisDelivery:Mode = Disabled (типово) | Webhook. У режимі Disabled події чесно мають статус NOT_CONFIGURED (не «доставлено»)
// і відправляються автоматично, щойно доставку налаштовано.
// Запит: JSON {id, event, orderId, misReferralId, occurredAt, payload}; заголовки
//   X-MedLink-Event-Id (= ключ ідемпотентності, також Idempotency-Key), X-MedLink-Event, X-MedLink-Timestamp (Unix, с),
//   X-MedLink-Signature: sha256=<hex HMAC-SHA256(secret, "{timestamp}.{тіло}")>. Отримувач відкидає запити зі старим timestamp і повтори за Event-Id.
// 2xx → DELIVERED; 408/425/429/5xx і мережеві збої → повтор із експоненційною паузою до MaxAttempts, далі FAILED; інші 4xx → FAILED одразу.
// Секрет підпису — лише змінна середовища MisDelivery__Secret; у відповіді API й журнали не потрапляє.
// =============================================================================
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services.Notifications;

public static class MisDeliveryStatuses
{
    public const string Queued = "QUEUED", Sending = "SENDING", Delivered = "DELIVERED", Failed = "FAILED", NotConfigured = "NOT_CONFIGURED";
    public static readonly string[] All = { Queued, Sending, Delivered, Failed, NotConfigured };
    public static string Label(string s) => s switch
    {
        Queued => "У черзі", Sending => "Відправляється", Delivered => "Доставлено", Failed => "Помилка", NotConfigured => "Не налаштовано", _ => s
    };
    public static bool CanRetry(string s) => s is Failed or NotConfigured;
}

public sealed class MisDeliveryOptions
{
    /// <summary>Disabled (типово) | Webhook</summary>
    public string Mode { get; set; } = "Disabled";
    public string? Url { get; set; }
    /// <summary>Секрет підпису HMAC-SHA256. Лише змінна середовища / сховище секретів.</summary>
    public string? Secret { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public int MaxAttempts { get; set; } = 8;
    public int BackoffBaseSeconds { get; set; } = 30;
    public int BackoffMaxSeconds { get; set; } = 3600;
    public int SendingTimeoutMinutes { get; set; } = 5;
    public int PollIntervalSeconds { get; set; } = 5;
    public bool? WorkerEnabled { get; set; }

    public bool IsWebhook => string.Equals(Mode, "Webhook", StringComparison.OrdinalIgnoreCase);
    public bool HasValidUrl => Uri.TryCreate(Url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
    public bool IsConfigured => IsWebhook && HasValidUrl && !string.IsNullOrWhiteSpace(Secret);
    public TimeSpan RetryDelay(int attempt) => TimeSpan.FromSeconds(Math.Min((double)BackoffMaxSeconds, BackoffBaseSeconds * Math.Pow(2, Math.Max(0, attempt - 1))));
}

public static class MisSignature
{
    /// <summary>sha256=&lt;hex&gt; від HMAC-SHA256(secret, "{timestamp}.{body}").</summary>
    public static string Sign(string secret, string timestamp, string body)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();
    }
}

public sealed class MisDeliveryEngine
{
    public const string HttpClientName = "mis-delivery";
    private readonly IServiceScopeFactory _scopes;
    private readonly MisDeliveryOptions _options;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<MisDeliveryEngine> _log;
    public string WorkerId { get; } = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid().ToString("N")[..6]}";

    public MisDeliveryEngine(IServiceScopeFactory scopes, MisDeliveryOptions options, IHttpClientFactory http, ILogger<MisDeliveryEngine> log)
    { _scopes = scopes; _options = options; _http = http; _log = log; }

    public async Task<int> RunDueAsync(DateTime? now = null, CancellationToken ct = default)
    {
        var t = now ?? DateTime.UtcNow;
        List<string> ids;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
            await db.MisEvents.Where(e => e.Status == MisDeliveryStatuses.Sending && e.LockedUntil < t)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, MisDeliveryStatuses.Queued).SetProperty(e => e.LockedBy, (string?)null).SetProperty(e => e.LockedUntil, (DateTime?)null)
                    .SetProperty(e => e.NextAttemptAt, (DateTime?)t).SetProperty(e => e.LastError, "Відновлено після збою: відправка не завершилась"), ct);
            if (!_options.IsConfigured)
            {
                await db.MisEvents.Where(e => e.Status == MisDeliveryStatuses.Queued)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, MisDeliveryStatuses.NotConfigured).SetProperty(e => e.LastError, "Доставку подій у МІС не налаштовано").SetProperty(e => e.UpdatedAt, (DateTime?)t), ct);
                return 0;
            }
            await db.MisEvents.Where(e => e.Status == MisDeliveryStatuses.NotConfigured)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, MisDeliveryStatuses.Queued).SetProperty(e => e.LastError, (string?)null).SetProperty(e => e.NextAttemptAt, (DateTime?)t).SetProperty(e => e.UpdatedAt, (DateTime?)t), ct);
            ids = await db.MisEvents.AsNoTracking().Where(e => e.Status == MisDeliveryStatuses.Queued && (e.NextAttemptAt == null || e.NextAttemptAt <= t))
                .OrderBy(e => e.At).Take(100).Select(e => e.Id).ToListAsync(ct);
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
        var claimed = await db.MisEvents.Where(e => e.Id == id && e.Status == MisDeliveryStatuses.Queued && (e.NextAttemptAt == null || e.NextAttemptAt <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, MisDeliveryStatuses.Sending).SetProperty(e => e.LockedBy, (string?)WorkerId).SetProperty(e => e.LockedUntil, (DateTime?)until).SetProperty(e => e.UpdatedAt, (DateTime?)now), ct);
        if (claimed != 1) return false;
        var row = await db.MisEvents.FirstAsync(e => e.Id == id, ct);
        try { await SendAsync(row, now, ct); }
        catch (OperationCanceledException) { row.Status = MisDeliveryStatuses.Queued; }
        catch (Exception ex)
        {
            _log.LogError(ex, "Непередбачена помилка доставки події {Id} у МІС", id);
            Fail(row, true, "Внутрішня помилка: " + (ex.Message.Length > 300 ? ex.Message[..300] : ex.Message), now);
        }
        row.LockedBy = null; row.LockedUntil = null; row.UpdatedAt = now;
        await db.SaveChangesAsync(CancellationToken.None);
        return true;
    }

    private async Task SendAsync(LabMisEventLog row, DateTime now, CancellationToken ct)
    {
        var body = BuildBody(row);
        var ts = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeSeconds().ToString();
        row.Attempts++;
        try
        {
            using var http = _http.CreateClient(HttpClientName);
            http.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));
            using var req = new HttpRequestMessage(HttpMethod.Post, _options.Url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            req.Headers.TryAddWithoutValidation("X-MedLink-Event-Id", row.Id);
            req.Headers.TryAddWithoutValidation("Idempotency-Key", row.Id);
            req.Headers.TryAddWithoutValidation("X-MedLink-Event", row.Event);
            req.Headers.TryAddWithoutValidation("X-MedLink-Timestamp", ts);
            req.Headers.TryAddWithoutValidation("X-MedLink-Signature", MisSignature.Sign(_options.Secret!, ts, body));
            using var resp = await http.SendAsync(req, ct);
            var code = (int)resp.StatusCode;
            row.HttpStatus = code;
            if (resp.IsSuccessStatusCode) { row.Status = MisDeliveryStatuses.Delivered; row.DeliveredAt = now; row.LastError = null; row.NextAttemptAt = null; return; }
            Fail(row, code is 408 or 425 or 429 || code >= 500, $"МІС повернув помилку {code}", now);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            row.HttpStatus = null;
            Fail(row, true, ex is TaskCanceledException ? "МІС не відповів вчасно" : "МІС недоступний: " + (ex.Message.Length > 200 ? ex.Message[..200] : ex.Message), now);
        }
    }

    private void Fail(LabMisEventLog row, bool transient, string error, DateTime now)
    {
        if (row.Attempts < 1) row.Attempts = 1;
        row.LastError = error;
        if (transient && row.Attempts < Math.Max(1, _options.MaxAttempts)) { row.Status = MisDeliveryStatuses.Queued; row.NextAttemptAt = now + _options.RetryDelay(row.Attempts); }
        else { row.Status = MisDeliveryStatuses.Failed; row.NextAttemptAt = null; }
    }

    /// <summary>Тіло запиту (стабільне для повторів: ті самі id і occurredAt).</summary>
    public static string BuildBody(LabMisEventLog row)
    {
        JsonNode? payload = null;
        if (!string.IsNullOrWhiteSpace(row.Payload)) { try { payload = JsonNode.Parse(row.Payload); } catch (JsonException) { payload = JsonValue.Create(row.Payload); } }
        return new JsonObject
        {
            ["id"] = row.Id, ["event"] = row.Event, ["orderId"] = row.OrderId, ["misReferralId"] = row.MisReferralId,
            ["occurredAt"] = DateTime.SpecifyKind(row.At, DateTimeKind.Utc).ToString("o"), ["payload"] = payload
        }.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}

public sealed class MisDeliveryWorker : BackgroundService
{
    private readonly MisDeliveryEngine _engine;
    private readonly MisDeliveryOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<MisDeliveryWorker> _log;

    public MisDeliveryWorker(MisDeliveryEngine engine, MisDeliveryOptions options, IHostEnvironment env, ILogger<MisDeliveryWorker> log)
    { _engine = engine; _options = options; _env = env; _log = log; }

    public static bool IsEnabled(MisDeliveryOptions o, IHostEnvironment env) => o.WorkerEnabled ?? !env.IsEnvironment("Testing");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled(_options, _env)) return;
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch (OperationCanceledException) { return; }
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await _engine.RunDueAsync(null, stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Помилка проходу доставки подій у МІС"); }
            try { await Task.Delay(interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
