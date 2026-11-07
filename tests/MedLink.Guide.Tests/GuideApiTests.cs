// Інтеграційні тести API Guide: мінімальний WebApplication на TestServer + SQLite in-memory (без WebApplicationFactory).
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MedLink.Guide.Tests;

public sealed class GuideHost : IAsyncDisposable
{
    public WebApplication App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    private SqliteConnection _conn = null!;

    public static async Task<GuideHost> StartAsync(bool anonymousEditing = true)
    {
        var h = new GuideHost { _conn = new SqliteConnection("DataSource=:memory:") };
        await h._conn.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGuide(o =>
        {
            o.ConfigureDb = db => db.UseSqlite(h._conn);
            o.AllowAnonymousEditing = anonymousEditing;
        });
        h.App = builder.Build();
        h.App.MapGuide();
        await h.App.Services.EnsureGuideDatabaseAsync();
        await h.App.StartAsync();
        h.Client = h.App.GetTestClient();
        return h;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
        await _conn.DisposeAsync();
    }

    public const string Base = "/api/v1/guide";

    public async Task<JsonNode> Json(HttpResponseMessage r, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == expected, $"{(int)r.StatusCode} {r.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!;
    }

    public Task<HttpResponseMessage> Post(string url, object? body = null, string? roles = null, string? tenant = null) => Send(HttpMethod.Post, url, body, roles, tenant);
    public Task<HttpResponseMessage> Put(string url, object? body = null) => Send(HttpMethod.Put, url, body);
    public Task<HttpResponseMessage> Get(string url, string? roles = null, string? tenant = null) => Send(HttpMethod.Get, url, null, roles, tenant);
    public Task<HttpResponseMessage> Delete(string url) => Send(HttpMethod.Delete, url);

    private Task<HttpResponseMessage> Send(HttpMethod m, string url, object? body = null, string? roles = null, string? tenant = null)
    {
        var req = new HttpRequestMessage(m, Base + url);
        if (body != null) req.Content = JsonContent.Create(body, options: GuideJson.Options);
        if (roles != null) req.Headers.Add("X-MedLink-Roles", roles);
        if (tenant != null) req.Headers.Add("X-MedLink-Tenant-Id", tenant);
        return Client.SendAsync(req);
    }

    public async Task<string> CreateApp(string code = "lab") { await Json(await Post("/apps", new { code, caption = code })); return code; }
    public async Task<string> CreateFolder(string app, string caption, string? parentId = null) =>
        (await Json(await Post("/folders", new { appCode = app, caption, parentId }), HttpStatusCode.Created))["id"]!.GetValue<string>();

    public static object Step(string title, string? anchorCaption = null, string? anchorCode = null, string? anchorId = null) => new
    {
        stepType = "info", title, body = "Текст " + title, route = "/laboratory/orders",
        anchorId,
        anchor = anchorCaption == null ? null : new
        {
            code = anchorCode, caption = anchorCaption, routePattern = "/laboratory/orders",
            strategies = new[] { new { type = "css", value = "[data-testid=\"x\"]", weight = 100 } },
            fingerprint = new { tag = "div" }
        }
    };

    public async Task<string> CreateScenario(string app, string caption, string? folderId = null, string[]? roles = null, string? tenant = null, string? code = null) =>
        (await Json(await Post("/scenarios", new { appCode = app, caption, folderId, requiredRoles = roles ?? Array.Empty<string>(), tenantId = tenant, code }), HttpStatusCode.Created))["id"]!.GetValue<string>();
}

public class GuideApiTests
{
    private const string TenantA = "11111111-1111-1111-1111-111111111111";
    private const string TenantB = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task Catalog_filters_by_status_role_tenant_and_hides_empty_folders()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var f1 = await h.CreateFolder(app, "Загальне");
        var f2 = await h.CreateFolder(app, "Порожнє");
        var sub = await h.CreateFolder(app, "Вкладене", f1);

        var pub = await h.CreateScenario(app, "Для всіх", sub);
        var admin = await h.CreateScenario(app, "Лише адмін", f1, new[] { "admin" });
        var tenantB = await h.CreateScenario(app, "Чужий tenant", f1);
        var draft = await h.CreateScenario(app, "Чернетка", f1);
        foreach (var id in new[] { pub, admin, tenantB })
        {
            await h.Json(await h.Put($"/scenarios/{id}/steps", new[] { GuideHost.Step("s1", "a-" + id[..6]) }));
            await h.Json(await h.Post($"/scenarios/{id}/publish", new { note = "v1" }));
        }
        // Призначаємо tenant після публікації (далі сценарій видимий лише tenant B)
        await h.Json(await h.Put($"/scenarios/{tenantB}", new { caption = "Чужий tenant", folderId = f1, tenantId = TenantB }));

        var cat = await h.Json(await h.Get($"/apps/{app}/catalog", tenant: TenantA));
        var titles = cat["scenarios"]!.AsArray().Select(x => x!["caption"]!.GetValue<string>()).ToList();
        Assert.Equal(new[] { "Для всіх" }, titles);
        var folders = cat["folders"]!.AsArray().Select(x => x!["caption"]!.GetValue<string>()).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "Вкладене", "Загальне" }, folders); // «Порожнє» приховано, предок збережено

        var catAdmin = await h.Json(await h.Get($"/apps/{app}/catalog", roles: "admin", tenant: TenantB));
        var t2 = catAdmin["scenarios"]!.AsArray().Select(x => x!["caption"]!.GetValue<string>()).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "Для всіх", "Лише адмін", "Чужий tenant" }, t2);

        var catRole = await h.Json(await h.Get($"/apps/{app}/catalog?role=admin", tenant: TenantA));
        Assert.Equal(2, catRole["scenarios"]!.AsArray().Count);

        var tree = await h.Json(await h.Get($"/apps/{app}/tree", tenant: TenantA));
        Assert.Equal(1, tree["counts"]!["draft"]!.GetValue<int>()); // draft; чужий tenant не враховано
        Assert.Equal(2, tree["counts"]!["published"]!.GetValue<int>());
    }

    [Fact]
    public async Task Catalog_marks_relevant_scenarios_for_route()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var id = await h.CreateScenario(app, "На сторінці", null);
        await h.Json(await h.Put($"/scenarios/{id}/steps", new[] { GuideHost.Step("s1", "a") }));
        await h.Json(await h.Post($"/scenarios/{id}/publish"));
        var cat = await h.Json(await h.Get($"/apps/{app}/catalog?route=/laboratory/orders?x=1"));
        Assert.True(cat["scenarios"]![0]!["relevantHere"]!.GetValue<bool>());
        var other = await h.Json(await h.Get($"/apps/{app}/catalog?route=/laboratory/qc"));
        Assert.False(other["scenarios"]![0]!["relevantHere"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ReplaceSteps_upserts_anchors_keeps_ids_and_removes_missing()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var sid = await h.CreateScenario(app, "Кроки", code: "t1");

        var r1 = await h.Json(await h.Put($"/scenarios/{sid}/steps", new[]
        {
            GuideHost.Step("Один", "Якір A", "anc.a"),
            GuideHost.Step("Два", "Якір A (повторно)", "anc.a"), // той самий код → той самий якір
            GuideHost.Step("Три", "Якір B", "anc.b"),
        }));
        var steps = r1["steps"]!.AsArray();
        Assert.Equal(3, steps.Count);
        Assert.Equal(steps[0]!["anchorId"]!.GetValue<string>(), steps[1]!["anchorId"]!.GetValue<string>());
        Assert.NotEqual(steps[0]!["anchorId"]!.GetValue<string>(), steps[2]!["anchorId"]!.GetValue<string>());
        var firstId = steps[0]!["id"]!.GetValue<string>();
        var anchorA = steps[0]!["anchorId"]!.GetValue<string>();

        // Другий PUT: переставлено, перший крок із id, другий видалено, додано новий
        var r2 = await h.Json(await h.Put($"/scenarios/{sid}/steps", new object[]
        {
            GuideHost.Step("Три (новий порядок)", anchorId: steps[2]!["anchorId"]!.GetValue<string>()),
            new { id = firstId, stepType = "click", title = "Один (змінено)", anchorId = anchorA, expect = "click" },
            GuideHost.Step("Новий", "Якір C", "anc.c"),
        }));
        var s2 = r2["steps"]!.AsArray();
        Assert.Equal(3, s2.Count);
        Assert.Equal("Три (новий порядок)", s2[0]!["title"]!.GetValue<string>());
        Assert.Equal(firstId, s2[1]!["id"]!.GetValue<string>());
        Assert.Equal("click", s2[1]!["stepType"]!.GetValue<string>());
        Assert.Equal(anchorA, s2[1]!["anchor"]!["id"]!.GetValue<string>());
        Assert.Equal(new[] { 0, 1, 2 }, s2.Select(x => x!["sortOrder"]!.GetValue<int>()).ToArray());

        var anchors = await h.Json(await h.Get($"/apps/{app}/anchors"));
        Assert.Equal(3, anchors.AsArray().Count); // A, B, C — без дублів

        // Невідомий anchorId → 400
        var bad = await h.Put($"/scenarios/{sid}/steps", new[] { GuideHost.Step("x", anchorId: Guid.NewGuid().ToString()) });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Publish_creates_versioned_snapshots_and_play_serves_published_version()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var sid = await h.CreateScenario(app, "Версії");

        // Без кроків публікувати не можна; без публікації play → 404
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Post($"/scenarios/{sid}/publish")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await h.Get($"/scenarios/{sid}/play")).StatusCode);

        await h.Json(await h.Put($"/scenarios/{sid}/variables", new[] { new { name = "num", kind = "random_number", defaultValue = "1" } }));
        var st = await h.Json(await h.Put($"/scenarios/{sid}/steps", new[] { GuideHost.Step("v1-крок", "Якір", "anc.v") }));
        var pub1 = await h.Json(await h.Post($"/scenarios/{sid}/publish", new { note = "перша" }));
        Assert.Equal(1, pub1["publishedVersion"]!.GetValue<int>());
        Assert.Equal("published", pub1["status"]!.GetValue<string>());

        // Правка чернетки не змінює опубліковане
        await h.Json(await h.Put($"/scenarios/{sid}/steps", new[]
        {
            GuideHost.Step("v2-крок A", anchorId: st["steps"]![0]!["anchorId"]!.GetValue<string>()),
            GuideHost.Step("v2-крок B", "Якір 2", "anc.v2")
        }));
        var play1 = await h.Json(await h.Get($"/scenarios/{sid}/play"));
        Assert.Equal(1, play1["versionNo"]!.GetValue<int>());
        Assert.Equal("v1-крок", play1["steps"]![0]!["title"]!.GetValue<string>());
        Assert.Single(play1["steps"]!.AsArray());
        Assert.Equal("num", play1["variables"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("random_number", play1["variables"]![0]!["kind"]!.GetValue<string>());

        var pub2 = await h.Json(await h.Post($"/scenarios/{sid}/publish"));
        Assert.Equal(2, pub2["publishedVersion"]!.GetValue<int>());
        var play2 = await h.Json(await h.Get($"/scenarios/{sid}/play"));
        Assert.Equal(2, play2["versionNo"]!.GetValue<int>());
        Assert.Equal(2, play2["steps"]!.AsArray().Count);

        // Зняття з публікації ховає з каталогу та play
        await h.Json(await h.Post($"/scenarios/{sid}/unpublish"));
        Assert.Equal(HttpStatusCode.NotFound, (await h.Get($"/scenarios/{sid}/play")).StatusCode);
        var cat = await h.Json(await h.Get($"/apps/{app}/catalog"));
        Assert.Empty(cat["scenarios"]!.AsArray());
    }

    [Fact]
    public async Task AnchorReport_healed_then_accept_updates_anchor_and_play_uses_live_anchor()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var sid = await h.CreateScenario(app, "Здоров'я");
        var st = await h.Json(await h.Put($"/scenarios/{sid}/steps", new[] { GuideHost.Step("крок", "Кнопка", "anc.btn") }));
        await h.Json(await h.Post($"/scenarios/{sid}/publish"));
        var anchorId = st["steps"]![0]!["anchorId"]!.GetValue<string>();

        // ok не створює звіту, лише статус
        var ok = await h.Json(await h.Post("/anchor-reports", new { anchorId, outcome = "ok", how = "testid" }));
        Assert.Equal("ok", ok["healthStatus"]!.GetValue<string>());
        Assert.Null(ok["reportId"]);

        var healed = await h.Json(await h.Post("/anchor-reports", new
        {
            anchorId, scenarioId = sid, outcome = "healed", how = "fuzzy", confidence = 0.82m, route = "/laboratory/orders",
            newStrategies = new[] { new { type = "css", value = "[data-testid=\"x-new\"]", weight = 100 } },
            newFingerprint = new { tag = "button", text = "Нова кнопка" }
        }));
        Assert.Equal("healed", healed["healthStatus"]!.GetValue<string>());
        var reportId = healed["reportId"]!.GetValue<string>();

        var health = await h.Json(await h.Get($"/apps/{app}/anchors/health"));
        Assert.Single(health.AsArray());
        Assert.Equal(reportId, health[0]!["reports"]![0]!["id"]!.GetValue<string>());
        Assert.Contains(sid, health[0]!["scenarioIds"]!.AsArray().Select(x => x!.GetValue<string>()));

        var accepted = await h.Json(await h.Post($"/anchor-reports/{reportId}/accept"));
        Assert.Equal("ok", accepted["healthStatus"]!.GetValue<string>());
        Assert.Contains("x-new", accepted["strategies"]![0]!["value"]!.GetValue<string>());
        Assert.Empty((await h.Json(await h.Get($"/apps/{app}/anchors/health"))).AsArray());

        // Плеєр бачить виправлений якір без перепублікації
        var play = await h.Json(await h.Get($"/scenarios/{sid}/play"));
        Assert.Contains("x-new", play["steps"]![0]!["anchor"]!["strategies"]![0]!["value"]!.GetValue<string>());

        // missing → broken
        var miss = await h.Json(await h.Post("/anchor-reports", new { anchorId, outcome = "missing" }));
        Assert.Equal("broken", miss["healthStatus"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Post("/anchor-reports", new { anchorId, outcome = "zzz" })).StatusCode);
    }

    [Fact]
    public async Task Export_import_roundtrip_is_idempotent()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp("lab");
        var folder = await h.CreateFolder(app, "Розділ");
        var sid = await h.CreateScenario(app, "Експорт", folder, code: "exp.one");
        await h.Json(await h.Put($"/scenarios/{sid}/variables", new[] { new { name = "v", kind = "fixed", defaultValue = "42" } }));
        await h.Json(await h.Put($"/scenarios/{sid}/steps", new[]
        {
            GuideHost.Step("A", "Якір 1", "anc.1"),
            GuideHost.Step("B", "Якір 2")
        }));
        await h.Json(await h.Post($"/scenarios/{sid}/publish"));

        var bundle = await h.Json(await h.Get($"/scenarios/{sid}/export"));
        Assert.Equal("lab", bundle["app"]!["code"]!.GetValue<string>());
        Assert.Equal("exp.one", bundle["scenarios"]![0]!["code"]!.GetValue<string>());
        Assert.True(bundle["scenarios"]![0]!["publish"]!.GetValue<bool>());

        // Імпорт у іншу систему
        var r1 = await h.Json(await h.Post("/apps/evomis/import", bundle));
        Assert.Equal(1, r1["scenariosCreated"]!.GetValue<int>());
        Assert.Equal(1, r1["foldersCreated"]!.GetValue<int>());
        Assert.Equal(1, r1["scenariosPublished"]!.GetValue<int>());

        // Повтор без змін: нічого не створює й не перепубліковує
        var r2 = await h.Json(await h.Post("/apps/evomis/import", bundle));
        Assert.Equal(0, r2["scenariosCreated"]!.GetValue<int>());
        Assert.Equal(1, r2["scenariosUpdated"]!.GetValue<int>());
        Assert.Equal(0, r2["scenariosPublished"]!.GetValue<int>());
        Assert.Equal(0, r2["foldersCreated"]!.GetValue<int>());

        var tree = await h.Json(await h.Get("/apps/evomis/tree"));
        Assert.Single(tree["scenarios"]!.AsArray());
        Assert.Equal(2, tree["scenarios"]![0]!["stepCount"]!.GetValue<int>());
        var anchors = await h.Json(await h.Get("/apps/evomis/anchors"));
        Assert.Equal(2, anchors.AsArray().Count);

        var imported = tree["scenarios"]![0]!["id"]!.GetValue<string>();
        var play = await h.Json(await h.Get($"/scenarios/{imported}/play"));
        Assert.Equal("A", play["steps"]![0]!["title"]!.GetValue<string>());
        Assert.Equal("42", play["variables"]![0]!["defaultValue"]!.GetValue<string>());

        // Зміна в бандлі → оновлення і нова версія
        bundle["scenarios"]![0]!["steps"]![0]!["title"] = "A (оновлено)";
        var r3 = await h.Json(await h.Post("/apps/evomis/import", bundle));
        Assert.Equal(1, r3["scenariosPublished"]!.GetValue<int>());
        var play3 = await h.Json(await h.Get($"/scenarios/{imported}/play"));
        Assert.Equal(2, play3["versionNo"]!.GetValue<int>());
        Assert.Equal("A (оновлено)", play3["steps"]![0]!["title"]!.GetValue<string>());
    }

    [Fact]
    public async Task Bundled_seed_loads_idempotently()
    {
        await using var h = await GuideHost.StartAsync();
        Assert.True(await h.App.Services.SeedGuideBundlesAsync() >= 1);
        var first = await h.Json(await h.Get("/apps/lab/catalog"));
        await h.App.Services.SeedGuideBundlesAsync();
        var cat = await h.Json(await h.Get("/apps/lab/catalog"));
        // повторний сід не створює дублікатів (кількість у каталозі стабільна); у бандлах є і зразок, і інструкції процесів ЛІС
        Assert.Equal(first["scenarios"]!.AsArray().Count, cat["scenarios"]!.AsArray().Count);
        Assert.Equal(first["folders"]!.AsArray().Count, cat["folders"]!.AsArray().Count);
        Assert.True(cat["scenarios"]!.AsArray().Count >= 9);
        var play = await h.Json(await h.Get($"/scenarios/{cat["scenarios"]![0]!["id"]!.GetValue<string>()}/play"));
        Assert.Equal(1, play["versionNo"]!.GetValue<int>());
    }

    [Fact]
    public async Task SoftDelete_hides_folders_and_scenarios_and_protects_non_empty_folders()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var f = await h.CreateFolder(app, "Папка");
        var child = await h.CreateFolder(app, "Дочірня", f);
        var sid = await h.CreateScenario(app, "У папці", child);

        Assert.Equal(HttpStatusCode.Conflict, (await h.Delete($"/folders/{f}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await h.Delete($"/folders/{f}?cascade=true")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await h.Get($"/scenarios/{sid}")).StatusCode);
        var tree = await h.Json(await h.Get($"/apps/{app}/tree"));
        Assert.Empty(tree["folders"]!.AsArray());

        // Фізично записи лишилися з record_state = 4
        using var scope = h.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuideDbContext>();
        Assert.Equal(2, await db.Folders.IgnoreQueryFilters().CountAsync(x => x.RecordState == RecordStates.Deleted));
        Assert.Equal(RecordStates.Deleted, (await db.Scenarios.IgnoreQueryFilters().FirstAsync(x => x.Id == sid)).RecordState);

        var s2 = await h.CreateScenario(app, "Окрема");
        Assert.Equal(HttpStatusCode.NoContent, (await h.Delete($"/scenarios/{s2}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await h.Get($"/scenarios/{s2}")).StatusCode);
    }

    [Fact]
    public async Task Folder_move_prevents_cycles()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var a = await h.CreateFolder(app, "A");
        var b = await h.CreateFolder(app, "B", a);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Post($"/folders/{a}/move", new { parentId = b })).StatusCode);
        var moved = await h.Json(await h.Post($"/folders/{b}/move", new { parentId = (string?)null, sortOrder = 5 }));
        Assert.Null(moved["parentId"]?.GetValue<string>());
        Assert.Equal(5, moved["sortOrder"]!.GetValue<int>());
    }

    [Fact]
    public async Task Duplicate_copies_steps_and_variables_as_draft()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var sid = await h.CreateScenario(app, "Оригінал", code: "orig");
        await h.Json(await h.Put($"/scenarios/{sid}/steps", new[] { GuideHost.Step("S", "Якір", "anc.d") }));
        await h.Json(await h.Put($"/scenarios/{sid}/variables", new[] { new { name = "x", kind = "fixed", defaultValue = "1" } }));
        await h.Json(await h.Post($"/scenarios/{sid}/publish"));
        var copy = await h.Json(await h.Post($"/scenarios/{sid}/duplicate"), HttpStatusCode.Created);
        Assert.Equal("Оригінал (копія)", copy["caption"]!.GetValue<string>());
        Assert.Equal("draft", copy["status"]!.GetValue<string>());
        Assert.Single(copy["steps"]!.AsArray());
        Assert.Single(copy["variables"]!.AsArray());
        Assert.NotEqual(sid, copy["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Editor_endpoints_require_editor_role_but_player_is_open()
    {
        await using var h = await GuideHost.StartAsync(anonymousEditing: false);
        var denied = await h.Post("/apps", new { code = "lab" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var ok = await h.Post("/apps", new { code = "lab" }, roles: "guide_editor");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        // Плеєр відкритий
        Assert.Equal(HttpStatusCode.OK, (await h.Get("/apps/lab/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Get("/apps/lab/tree")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await h.Get("/apps/lab/tree", roles: "admin")).StatusCode);
    }

    [Fact]
    public async Task Media_and_progress_roundtrip()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var sid = await h.CreateScenario(app, "Прогрес");
        await h.Json(await h.Put($"/scenarios/{sid}/steps", new[] { GuideHost.Step("S", "Якір") }));
        await h.Json(await h.Post($"/scenarios/{sid}/publish"));

        var bytes = new byte[] { 1, 2, 3, 4 };
        var media = await h.Json(await h.Post("/media", new { kind = "audio", mime = "audio/webm", fileName = "a.webm", base64 = Convert.ToBase64String(bytes), transcript = "привіт", durationMs = 1200 }), HttpStatusCode.Created);
        var resp = await h.Get("/media/" + media["id"]!.GetValue<string>());
        Assert.Equal("audio/webm", resp.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, await resp.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Post("/media", new { kind = "audio", mime = "x", base64 = "***" })).StatusCode);

        var emp = "33333333-3333-3333-3333-333333333333";
        async Task<JsonNode> Prog(string status, int step)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, GuideHost.Base + "/progress") { Content = JsonContent.Create(new { scenarioId = sid, status, lastStep = step }, options: GuideJson.Options) };
            req.Headers.Add("X-MedLink-Employee-Id", emp);
            return await h.Json(await h.Client.SendAsync(req));
        }
        var p1 = await Prog("started", 1);
        var p2 = await Prog("started", 2);
        Assert.Equal(p1["id"]!.GetValue<string>(), p2["id"]!.GetValue<string>()); // upsert
        var p3 = await Prog("completed", 3);
        Assert.NotNull(p3["completedOn"]);
        var meReq = new HttpRequestMessage(HttpMethod.Get, GuideHost.Base + "/progress/me");
        meReq.Headers.Add("X-MedLink-Employee-Id", emp);
        var me = await h.Json(await h.Client.SendAsync(meReq));
        Assert.Single(me.AsArray());
        Assert.Equal("completed", me[0]!["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("/laboratory/orders/:id", "/laboratory/orders/123", true)]
    [InlineData("/laboratory/orders", "/laboratory/orders/", true)]
    [InlineData("/laboratory/orders", "/laboratory/orders/123", false)]
    [InlineData("*", "/anything/here", true)]
    [InlineData("/a/*", "/a/b/c?x=1#h", true)]
    [InlineData(null, "/a", false)]
    public void RouteMatches_supports_params_wildcards_and_trailing_slash(string? pattern, string route, bool expected) =>
        Assert.Equal(expected, GuideService.RouteMatches(pattern, route));
}
