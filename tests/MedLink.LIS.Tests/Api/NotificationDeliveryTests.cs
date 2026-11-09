// Сповіщення пацієнтам (відправник, повтори, згода, канали) і доставка подій у МІС (webhook, HMAC, повтори); перевірка конфігурації й Auth:Mode.
// Провайдери підміняються фейками (SMTP-транспорт і IHttpClientFactory) — реальних відправок немає.
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Data.Seed;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MedLink.LIS.Tests.Api;

public sealed class FakeSmtp : ISmtpTransport
{
    public List<MailMessage> Sent { get; } = new();
    public Func<MailMessage, Exception?>? Fail { get; set; }
    public Task SendAsync(NotifyEmailOptions options, MailMessage message, CancellationToken ct)
    {
        var ex = Fail?.Invoke(message);
        if (ex != null) throw ex;
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

public sealed record FakeCall(string Client, string Url, string Body, Dictionary<string, string> Headers);

public sealed class FakeHttpFactory : IHttpClientFactory
{
    public List<FakeCall> Calls { get; } = new();
    public Func<FakeCall, HttpStatusCode> Respond { get; set; } = _ => HttpStatusCode.OK;
    public HttpClient CreateClient(string name) => new(new Handler(this, name));

    private sealed class Handler : HttpMessageHandler
    {
        private readonly FakeHttpFactory _f; private readonly string _name;
        public Handler(FakeHttpFactory f, string name) { _f = f; _name = name; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
            var call = new FakeCall(_name, request.RequestUri!.ToString(), body, headers);
            _f.Calls.Add(call);
            var code = _f.Respond(call);
            return new HttpResponseMessage(code) { Content = new StringContent("{\"id\":\"gw-1\"}") };
        }
    }
}

public sealed class NotifyFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"medlink-notify-test-{Guid.NewGuid():N}.db");
    private readonly string _guidePath = Path.Combine(Path.GetTempPath(), $"medlink-notify-guide-{Guid.NewGuid():N}.db");
    public const string MisSecret = "test-hmac-secret-value-123";
    public FakeSmtp Smtp { get; } = new();
    public FakeHttpFactory Http { get; } = new();
    /// <summary>Сповіщення, створені сідером (видача демо-замовлень), до першого очищення черги тестами.</summary>
    public List<LabPatientNotification>? SeedSnapshot { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:SqlitePath"] = _dbPath, ["Database:GuideSqlitePath"] = _guidePath, ["Lab:SeedDemoData"] = "true", ["Lab:PublicBaseUrl"] = "https://lab.example.org", ["Auth:Mode"] = "Header",
            ["Logging:LogLevel:Default"] = "Warning",
            ["Notify:MaxAttempts"] = "3", ["Notify:BackoffBaseSeconds"] = "10",
            ["Notify:Email:Host"] = "smtp.test", ["Notify:Email:From"] = "lab@example.org",
            ["Notify:Sms:Url"] = "https://sms.test/send", ["Notify:Sms:ApiKey"] = "sms-key-secret",
            ["MisDelivery:Mode"] = "Webhook", ["MisDelivery:Url"] = "https://mis.test/hook?token=abc", ["MisDelivery:Secret"] = MisSecret,
            ["MisDelivery:MaxAttempts"] = "3", ["MisDelivery:BackoffBaseSeconds"] = "10"
        }));
        builder.ConfigureServices(s =>
        {
            s.AddSingleton<ISmtpTransport>(Smtp);
            s.AddSingleton<IHttpClientFactory>(Http);
        });
    }

    public HttpClient As(string employeeId)
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Add("X-MedLink-Employee-Id", employeeId);
        return c;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_dbPath); File.Delete(_guidePath); } catch { /* ignore */ }
    }
}

[Collection("api")] // спільна колекція з іншими API-тестами: усі фабрики пишуть в один файл довідника Guide, паралельний старт дає «database is locked»
public class NotificationDeliveryTests
{
    private const string P1 = "0b000000-0000-0000-0000-000000000001", P2 = "0b000000-0000-0000-0000-000000000002";
    private readonly NotifyFactory _f;
    public NotificationDeliveryTests(NotifyFactory f)
    {
        _f = f;
        lock (f)
        {
            if (f.SeedSnapshot != null) return;
            using var scope = f.Services.CreateScope();
            f.SeedSnapshot = scope.ServiceProvider.GetRequiredService<LisDbContext>().Notifications.AsNoTracking().ToList();
        }
    }

    private NotificationDispatcher Dispatcher => _f.Services.GetRequiredService<NotificationDispatcher>();
    private MisDeliveryEngine Mis => _f.Services.GetRequiredService<MisDeliveryEngine>();

    private async Task<T> Db<T>(Func<LisDbContext, Task<T>> f)
    {
        using var scope = _f.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<LisDbContext>());
    }

    /// <summary>Чистий стан: порожні черги, фейки скинуто, пацієнт із заданими контактами й згодою.</summary>
    private async Task Reset(string patientId, bool consent, string? email, string? phone)
    {
        _f.Smtp.Sent.Clear(); _f.Smtp.Fail = null; _f.Http.Calls.Clear(); _f.Http.Respond = _ => HttpStatusCode.OK;
        await Db(async db =>
        {
            await db.Notifications.ExecuteDeleteAsync();
            await db.MisEvents.ExecuteDeleteAsync();
            await db.NotifyChannelSettings.ExecuteDeleteAsync();
            var person = (await db.Patients.Include(p => p.Person).FirstAsync(p => p.Id == patientId)).Person!;
            person.SubscribedToNotifications = consent; person.Email = email; person.Phone = phone;
            await db.SaveChangesAsync();
            return 0;
        });
    }

    private async Task<string> Enqueue(string patientId, string channel, string key, string template = NotifyTemplates.ResultReady)
    {
        return await Db(async db =>
        {
            var n = await NotifyQueue.EnqueueAsync(db, patientId, null, channel, template, $"/portal/{patientId}/orders/o1", key, DateTime.UtcNow.AddMinutes(-1));
            await db.SaveChangesAsync();
            return n!.Id;
        });
    }

    private Task<LabPatientNotification> Row(string id) => Db(db => db.Notifications.AsNoTracking().FirstAsync(n => n.Id == id));

    // ------------------------------------------------------------------ сповіщення пацієнтам
    [Fact]
    public async Task Email_is_sent_with_link_only_and_masked_recipient()
    {
        await Reset(P1, true, "patient.one@example.org", "+380501112233");
        var id = await Enqueue(P1, NotifyChannels.Email, "k-email-1");
        await Dispatcher.RunDueAsync();
        var row = await Row(id);
        Assert.Equal(NotifyStatuses.Sent, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.NotNull(row.SentAt);
        Assert.Equal("p***@example.org", row.RecipientMasked);
        var mail = Assert.Single(_f.Smtp.Sent);
        Assert.Equal("patient.one@example.org", mail.To[0].Address);
        Assert.Contains("https://lab.example.org/portal/" + P1 + "/orders/o1", mail.Body);
        Assert.DoesNotContain("WBC", mail.Body);
    }

    [Fact]
    public async Task Sms_text_has_link_only_and_goes_through_gateway_with_key()
    {
        await Reset(P1, true, null, "+38 (050) 111-22-33");
        var id = await Enqueue(P1, NotifyChannels.Sms, "k-sms-1", NotifyTemplates.ResultPartial);
        await Dispatcher.RunDueAsync();
        var row = await Row(id);
        Assert.Equal(NotifyStatuses.Sent, row.Status);
        Assert.Equal("gw-1", row.ProviderMessageId);
        var call = Assert.Single(_f.Http.Calls);
        Assert.Equal("notify", call.Client);
        var body = JsonNode.Parse(call.Body)!;
        Assert.Equal("+380501112233", body["to"]!.GetValue<string>());
        Assert.Contains("https://lab.example.org/portal/", body["text"]!.GetValue<string>());
        Assert.Equal("Bearer sms-key-secret", call.Headers["Authorization"]);
        Assert.Equal(row.Id, call.Headers["Idempotency-Key"]);
        Assert.DoesNotContain("sms-key-secret", row.LastError ?? "");
    }

    [Fact]
    public async Task No_consent_or_no_contact_is_skipped_not_sent()
    {
        await Reset(P1, false, "patient.one@example.org", null);
        var noConsent = await Enqueue(P1, NotifyChannels.Email, "k-nc");
        await Dispatcher.RunDueAsync();
        var r1 = await Row(noConsent);
        Assert.Equal(NotifyStatuses.Skipped, r1.Status);
        Assert.Equal(NotifyReasons.NoConsent, r1.ReasonCode);
        Assert.Empty(_f.Smtp.Sent);

        await Reset(P1, true, null, null);
        var noContact = await Enqueue(P1, NotifyChannels.Sms, "k-nct");
        await Dispatcher.RunDueAsync();
        Assert.Equal(NotifyReasons.NoContact, (await Row(noContact)).ReasonCode);
        Assert.Empty(_f.Http.Calls);
    }

    [Fact]
    public async Task Unconfigured_channel_is_honest_and_sends_automatically_after_it_becomes_ready()
    {
        await Reset(P1, true, null, "+380501112233");
        var id = await Enqueue(P1, NotifyChannels.Viber, "k-viber"); // Viber у конфігурації порожній
        await Dispatcher.RunDueAsync();
        var row = await Row(id);
        Assert.Equal(NotifyStatuses.NotConfigured, row.Status);
        Assert.Equal(NotifyReasons.NotConfigured, row.ReasonCode);
        Assert.Null(row.SentAt);
        Assert.Empty(_f.Http.Calls);
    }

    [Fact]
    public async Task Transient_errors_retry_with_exponential_backoff_then_fail()
    {
        await Reset(P1, true, null, "+380501112233");
        _f.Http.Respond = _ => HttpStatusCode.ServiceUnavailable;
        var id = await Enqueue(P1, NotifyChannels.Sms, "k-retry");
        var t0 = DateTime.UtcNow;
        await Dispatcher.RunDueAsync(t0);
        var a1 = await Row(id);
        Assert.Equal(NotifyStatuses.Queued, a1.Status);
        Assert.Equal(1, a1.Attempts);
        Assert.InRange((a1.NextAttemptAt!.Value - t0).TotalSeconds, 9.5, 10.5); // 10 с

        Assert.Equal(0, await Dispatcher.RunDueAsync(t0.AddSeconds(5))); // ще не час
        await Dispatcher.RunDueAsync(t0.AddSeconds(11));
        var a2 = await Row(id);
        Assert.Equal(2, a2.Attempts);
        Assert.InRange((a2.NextAttemptAt!.Value - t0.AddSeconds(11)).TotalSeconds, 19.5, 20.5); // 20 с

        await Dispatcher.RunDueAsync(t0.AddMinutes(5));
        var a3 = await Row(id);
        Assert.Equal(NotifyStatuses.Failed, a3.Status); // MaxAttempts = 3
        Assert.Equal(3, a3.Attempts);
        Assert.Contains("503", a3.LastError);
        Assert.Equal(3, _f.Http.Calls.Count);
    }

    [Fact]
    public async Task Permanent_error_fails_at_once_and_manual_retry_requeues_and_sends()
    {
        await Reset(P1, true, null, "+380501112233");
        _f.Http.Respond = _ => HttpStatusCode.BadRequest;
        var id = await Enqueue(P1, NotifyChannels.Sms, "k-perm");
        await Dispatcher.RunDueAsync();
        Assert.Equal(NotifyStatuses.Failed, (await Row(id)).Status);
        Assert.Single(_f.Http.Calls);

        _f.Http.Respond = _ => HttpStatusCode.OK;
        var reg = _f.As(DemoDataSeeder.Registrar);
        var resp = await reg.PostAsync($"/api/v1/lab/notifications/{id}/retry", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(NotifyStatuses.Queued, (await Row(id)).Status);
        await Dispatcher.RunDueAsync();
        Assert.Equal(NotifyStatuses.Sent, (await Row(id)).Status);

        // надіслане повторити не можна
        Assert.Equal(HttpStatusCode.Conflict, (await reg.PostAsync($"/api/v1/lab/notifications/{id}/retry", null)).StatusCode);
        // кур'єр не має права
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.As(DemoDataSeeder.Courier).PostAsync($"/api/v1/lab/notifications/{id}/retry", null)).StatusCode);
    }

    [Fact]
    public async Task Smtp_failure_classification()
    {
        await Reset(P1, true, "patient.one@example.org", null);
        _f.Smtp.Fail = _ => new SmtpException(SmtpStatusCode.MailboxUnavailable, "550 no such user");
        var perm = await Enqueue(P1, NotifyChannels.Email, "k-smtp-perm");
        await Dispatcher.RunDueAsync();
        Assert.Equal(NotifyStatuses.Failed, (await Row(perm)).Status);

        await Reset(P1, true, "patient.one@example.org", null);
        _f.Smtp.Fail = _ => new SmtpException(SmtpStatusCode.ServiceNotAvailable, "421 try later");
        var temp = await Enqueue(P1, NotifyChannels.Email, "k-smtp-temp");
        await Dispatcher.RunDueAsync();
        Assert.Equal(NotifyStatuses.Queued, (await Row(temp)).Status);
    }

    [Fact]
    public async Task Queueing_is_idempotent_and_concurrent_runs_send_once()
    {
        await Reset(P1, true, null, "+380501112233");
        var first = await Enqueue(P1, NotifyChannels.Sms, "k-idem");
        var again = await Db(async db => await NotifyQueue.EnqueueAsync(db, P1, null, NotifyChannels.Sms, NotifyTemplates.ResultReady, "/x", "k-idem"));
        Assert.Null(again);
        Assert.Equal(1, await Db(db => db.Notifications.CountAsync(n => n.IdempotencyKey == "k-idem")));

        await Task.WhenAll(Dispatcher.RunDueAsync(), Dispatcher.RunDueAsync(), Dispatcher.RunDueAsync());
        Assert.Equal(NotifyStatuses.Sent, (await Row(first)).Status);
        Assert.Single(_f.Http.Calls);
    }

    [Fact]
    public async Task Admin_can_switch_channel_off_and_back_and_status_hides_secrets()
    {
        await Reset(P1, true, null, "+380501112233");
        var admin = _f.As(DemoDataSeeder.Admin);
        var channels = await admin.GetStringAsync("/api/v1/lab/notifications/channels");
        Assert.DoesNotContain("sms-key-secret", channels);
        var arr = JsonNode.Parse(channels)!.AsArray();
        Assert.Equal("READY", arr.First(c => c!["channel"]!.GetValue<string>() == "SMS")!["state"]!.GetValue<string>());
        Assert.Equal("NOT_CONFIGURED", arr.First(c => c!["channel"]!.GetValue<string>() == "TELEGRAM")!["state"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.Forbidden, (await _f.As(DemoDataSeeder.Registrar).PutAsJsonAsync("/api/v1/lab/notifications/channels/SMS", new { enabled = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/lab/notifications/channels/SMS", new { enabled = false })).StatusCode);
        var id = await Enqueue(P1, NotifyChannels.Sms, "k-off");
        await Dispatcher.RunDueAsync();
        var off = await Row(id);
        Assert.Equal(NotifyStatuses.NotConfigured, off.Status);
        Assert.Equal(NotifyReasons.ChannelOff, off.ReasonCode);
        Assert.Empty(_f.Http.Calls);

        await admin.PutAsJsonAsync("/api/v1/lab/notifications/channels/SMS", new { enabled = true });
        await Dispatcher.RunDueAsync(); // NOT_CONFIGURED → QUEUED → SENT
        Assert.Equal(NotifyStatuses.Sent, (await Row(id)).Status);
    }

    [Fact]
    public async Task List_filters_and_summary()
    {
        await Reset(P1, true, "patient.one@example.org", "+380501112233");
        var a = await Enqueue(P1, NotifyChannels.Email, "k-l1");
        await Enqueue(P1, NotifyChannels.Telegram, "k-l2");
        await Dispatcher.RunDueAsync();
        var admin = _f.As(DemoDataSeeder.Admin);
        var all = JsonNode.Parse(await admin.GetStringAsync("/api/v1/lab/notifications"))!;
        Assert.Equal(2, all["total"]!.GetValue<int>());
        var sent = JsonNode.Parse(await admin.GetStringAsync("/api/v1/lab/notifications?status=SENT"))!;
        Assert.Equal(a, sent["items"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("Надіслано", sent["items"]![0]!["statusLabel"]!.GetValue<string>());
        var tg = JsonNode.Parse(await admin.GetStringAsync("/api/v1/lab/notifications?channel=TELEGRAM"))!;
        Assert.Equal("NOT_CONFIGURED", tg["items"]![0]!["status"]!.GetValue<string>());
        var summary = JsonNode.Parse(await admin.GetStringAsync("/api/v1/lab/notifications/summary"))!;
        Assert.Equal(2, summary["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Release_and_partial_release_queue_notifications_with_template_and_key()
    {
        var rows = _f.SeedSnapshot!.Where(n => n.TemplateCode == NotifyTemplates.ResultReady).ToList();
        Assert.NotEmpty(rows); // демо-замовлення видано через OrderService.ReleaseAsync
        Assert.All(rows, r => { Assert.StartsWith("result-ready:", r.IdempotencyKey); Assert.StartsWith("/portal/", r.Payload); Assert.DoesNotContain("№", r.Payload); });
    }

    [Fact]
    public void Mask_and_templates_do_not_leak()
    {
        Assert.Equal("a***@x.org", NotifyMask.Recipient(NotifyChannels.Email, "alice@x.org"));
        Assert.Equal("******2233", NotifyMask.Recipient(NotifyChannels.Sms, "+380501112233"));
        Assert.Equal("+380501112233", NotifyMask.NormalizePhone("+38 (050) 111-22-33"));
        var (_, sms) = NotifyTemplates.Render(NotifyTemplates.ResultReady, NotifyChannels.Sms, "https://l/p");
        Assert.Equal("Лабораторія: результати готові. Перегляд: https://l/p", sms);
    }

    // ------------------------------------------------------------------ доставка подій у МІС
    private async Task<string> MisEvent(string ev = "RESULT_READY")
        => await Db(async db =>
        {
            var e = new LabMisEventLog { Event = ev, OrderId = "o-1", MisReferralId = "ref-1", Payload = "{\"a\":1}" };
            db.MisEvents.Add(e);
            await db.SaveChangesAsync();
            return e.Id;
        });

    private Task<LabMisEventLog> MisRow(string id) => Db(db => db.MisEvents.AsNoTracking().FirstAsync(e => e.Id == id));

    [Fact]
    public async Task Mis_event_is_delivered_with_valid_hmac_and_idempotency_key()
    {
        await Reset(P1, true, null, null);
        var id = await MisEvent();
        await Mis.RunDueAsync();
        var row = await MisRow(id);
        Assert.Equal(MisDeliveryStatuses.Delivered, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Equal(200, row.HttpStatus);
        Assert.NotNull(row.DeliveredAt);
        var call = Assert.Single(_f.Http.Calls.Where(c => c.Client == MisDeliveryEngine.HttpClientName));
        Assert.Equal(id, call.Headers["X-MedLink-Event-Id"]);
        Assert.Equal(id, call.Headers["Idempotency-Key"]);
        Assert.Equal("RESULT_READY", call.Headers["X-MedLink-Event"]);
        Assert.Equal(MisSignature.Sign(NotifyFactory.MisSecret, call.Headers["X-MedLink-Timestamp"], call.Body), call.Headers["X-MedLink-Signature"]);
        var body = JsonNode.Parse(call.Body)!;
        Assert.Equal(id, body["id"]!.GetValue<string>());
        Assert.Equal(1, body["payload"]!["a"]!.GetValue<int>());
        Assert.DoesNotContain(NotifyFactory.MisSecret, call.Body);
    }

    [Fact]
    public async Task Mis_delivery_retries_transient_and_fails_permanent_and_manual_retry_works()
    {
        await Reset(P1, true, null, null);
        _f.Http.Respond = _ => HttpStatusCode.InternalServerError;
        var id = await MisEvent();
        var t0 = DateTime.UtcNow;
        await Mis.RunDueAsync(t0);
        var r1 = await MisRow(id);
        Assert.Equal(MisDeliveryStatuses.Queued, r1.Status);
        Assert.Equal(500, r1.HttpStatus);
        Assert.InRange((r1.NextAttemptAt!.Value - t0).TotalSeconds, 9.5, 10.5);
        Assert.Equal(0, await Mis.RunDueAsync(t0.AddSeconds(3)));
        _f.Http.Respond = _ => HttpStatusCode.OK;
        await Mis.RunDueAsync(t0.AddSeconds(11));
        var r2 = await MisRow(id);
        Assert.Equal(MisDeliveryStatuses.Delivered, r2.Status);
        Assert.Equal(2, r2.Attempts);
        Assert.Equal(2, _f.Http.Calls.Select(c => c.Headers["X-MedLink-Event-Id"]).Count(x => x == id)); // той самий ключ ідемпотентності

        _f.Http.Respond = _ => HttpStatusCode.UnprocessableEntity;
        var bad = await MisEvent("COMPLETED");
        await Mis.RunDueAsync();
        Assert.Equal(MisDeliveryStatuses.Failed, (await MisRow(bad)).Status);

        _f.Http.Respond = _ => HttpStatusCode.OK;
        var doc = _f.As(DemoDataSeeder.Doctor);
        Assert.Equal(HttpStatusCode.OK, (await doc.PostAsync($"/api/v1/lab/mis-events/{bad}/retry", null)).StatusCode);
        await Mis.RunDueAsync();
        Assert.Equal(MisDeliveryStatuses.Delivered, (await MisRow(bad)).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await doc.PostAsync($"/api/v1/lab/mis-events/{bad}/retry", null)).StatusCode);
    }

    [Fact]
    public async Task Mis_status_endpoint_shows_host_only_no_secret_or_query()
    {
        await Reset(P1, true, null, null);
        var json = await _f.As(DemoDataSeeder.Registrar).GetStringAsync("/api/v1/lab/mis-events/status");
        Assert.DoesNotContain(NotifyFactory.MisSecret, json);
        Assert.DoesNotContain("token=abc", json);
        var n = JsonNode.Parse(json)!;
        Assert.Equal("mis.test", n["host"]!.GetValue<string>());
        Assert.Equal("READY", n["state"]!.GetValue<string>());
        var list = JsonNode.Parse(await _f.As(DemoDataSeeder.Registrar).GetStringAsync("/api/v1/lab/mis-events?status=DELIVERED"))!;
        Assert.NotNull(list["items"]);
    }
}

/// <summary>Режим за замовчуванням: канали й доставка в МІС не налаштовані — чесні статуси, жодної імітації успіху; Auth:Mode і попередження конфігурації.</summary>
[Collection("api")]
public class DisabledIntegrationsTests
{
    private readonly LisApiFactory _f;
    public DisabledIntegrationsTests(LisApiFactory f) => _f = f;

    [Fact]
    public async Task Default_config_marks_everything_not_configured_and_health_warns()
    {
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
            db.MisEvents.Add(new LabMisEventLog { Event = "COMPLETED", OrderId = "x" });
            await db.SaveChangesAsync();
        }
        await _f.Services.GetRequiredService<NotificationDispatcher>().RunDueAsync();
        await _f.Services.GetRequiredService<MisDeliveryEngine>().RunDueAsync();
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
            Assert.DoesNotContain(await db.Notifications.AsNoTracking().ToListAsync(), n => n.Status == NotifyStatuses.Sent);
            Assert.Contains(await db.Notifications.AsNoTracking().ToListAsync(), n => n.Status is NotifyStatuses.NotConfigured or NotifyStatuses.Skipped);
            Assert.Equal(MisDeliveryStatuses.NotConfigured, (await db.MisEvents.AsNoTracking().FirstAsync(e => e.OrderId == "x")).Status);
        }
        var health = JsonNode.Parse(await _f.CreateClient().GetStringAsync("/health"))!;
        var codes = health["configWarnings"]!.AsArray().Select(w => w!["code"]!.GetValue<string>() ?? w["Code"]!.GetValue<string>()).ToList();
        Assert.Contains("VERIFY_SECRET", codes);
        Assert.Contains("AUTH_HEADER", codes);
        Assert.Equal("Header", health["integrations"]!["auth"]!.GetValue<string>());
        Assert.Equal("Disabled", health["integrations"]!["misDelivery"]!.GetValue<string>());
        Assert.False(health["integrations"]!["notifySms"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Auth_mode_none_ignores_employee_header()
    {
        using var none = _f.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Mode"] = "None" })));
        var client = none.CreateClient();
        client.DefaultRequestHeaders.Add("X-MedLink-Employee-Id", DemoDataSeeder.Courier); // у режимі Header це 403 для запуску черги
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/lab/notifications/run", null)).StatusCode); // діє співробітник за замовчуванням (адміністратор)
        var health = JsonNode.Parse(await client.GetStringAsync("/health"))!;
        Assert.Contains(health["configWarnings"]!.AsArray(), w => (w!["code"] ?? w["Code"])!.GetValue<string>() == "AUTH_NONE");

        Assert.Equal(HttpStatusCode.Forbidden, (await _f.As(DemoDataSeeder.Courier).PostAsync("/api/v1/lab/notifications/run", null)).StatusCode);
    }
}

public class StartupConfigCheckTests
{
    private static IConfiguration Cfg(params (string, string?)[] kv) => new ConfigurationBuilder().AddInMemoryCollection(kv.ToDictionary(x => x.Item1, x => x.Item2)).Build();
    private sealed class Env : IHostEnvironment
    {
        public Env(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "t";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public void Development_is_silent()
        => Assert.Empty(StartupConfigCheck.Evaluate(Cfg(("Lab:VerifySecret", "change-me")), new Env("Development")));

    [Fact]
    public void Production_placeholders_and_localhost_warn_loudly()
    {
        var codes = StartupConfigCheck.Evaluate(Cfg(("Lab:VerifySecret", "change-me-medlink-lis-verify-secret"), ("Lab:PublicBaseUrl", "http://localhost:5055")), new Env("Production")).Select(w => w.Code).ToList();
        Assert.Contains("VERIFY_SECRET", codes);
        Assert.Contains("PUBLIC_BASE_URL", codes);
        Assert.Contains("AUTH_NONE", codes);
    }

    [Fact]
    public void Production_good_values_leave_only_auth_warning()
    {
        var w = StartupConfigCheck.Evaluate(Cfg(("Lab:VerifySecret", "k8Qz1vN7xP0aLm3RtYw5HbJd9GfCs2Ue"), ("Lab:PublicBaseUrl", "https://lab.hospital.ua"), ("Auth:Mode", "Header")), new Env("Production"));
        Assert.Equal(new[] { "AUTH_HEADER" }, w.Select(x => x.Code).ToArray());
    }

    [Fact]
    public void Auth_mode_defaults_to_none_and_unknown_is_safe()
    {
        Assert.Equal(AuthModes.None, AuthModes.Resolve(Cfg()));
        Assert.Equal(AuthModes.None, AuthModes.Resolve(Cfg(("Auth:Mode", "Oidc"))));
        Assert.Equal(AuthModes.Header, AuthModes.Resolve(Cfg(("Auth:Mode", "header"))));
    }

    [Fact]
    public void Mis_signature_is_stable_and_secret_dependent()
    {
        var a = MisSignature.Sign("s1", "1700000000", "{}");
        Assert.StartsWith("sha256=", a);
        Assert.Equal(a, MisSignature.Sign("s1", "1700000000", "{}"));
        Assert.NotEqual(a, MisSignature.Sign("s2", "1700000000", "{}"));
        Assert.NotEqual(a, MisSignature.Sign("s1", "1700000001", "{}"));
    }
}
