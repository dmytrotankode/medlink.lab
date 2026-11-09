// Тести наборів інструкцій (презентацій): створення, розкриття розділу, каталог, імпорт sets, soft-delete, дооснащення БД.
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MedLink.Guide.Tests;

public class GuideSetsTests
{
    private static async Task<string> Published(GuideHost h, string app, string caption, string? folderId = null, string? code = null, int? est = null)
    {
        var id = await h.CreateScenario(app, caption, folderId, code: code);
        if (est != null) await h.Json(await h.Put($"/scenarios/{id}", new { caption, folderId, code, estMinutes = est }));
        await h.Json(await h.Put($"/scenarios/{id}/steps", new[] { GuideHost.Step("s1", "a-" + id[..6]) }));
        await h.Json(await h.Post($"/scenarios/{id}/publish"));
        return id;
    }

    private static async Task<string> NewSet(GuideHost h, string app, string caption, object[] items, string? code = null, string? color = "#2e7d32") =>
        (await h.Json(await h.Post("/sets", new { appCode = app, caption, code, color, items }), HttpStatusCode.Created))["id"]!.GetValue<string>();

    private static string[] Captions(JsonNode? arr) => arr!.AsArray().Select(x => x!["caption"]!.GetValue<string>()).ToArray();

    [Fact]
    public async Task Create_set_returns_items_and_validates_input()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var f = await h.CreateFolder(app, "Розділ");
        var s1 = await Published(h, app, "Одна");

        var created = await h.Json(await h.Post("/sets", new { appCode = app, caption = "Старт", code = "start", color = "#1565c0", items = new object[] { new { scenarioId = s1 }, new { folderId = f } } }), HttpStatusCode.Created);
        Assert.Equal("#1565c0", created["color"]!.GetValue<string>());
        Assert.Equal(2, created["items"]!.AsArray().Count);

        Assert.Equal(HttpStatusCode.BadRequest, (await h.Post("/sets", new { appCode = app, caption = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Post("/sets", new { appCode = app, caption = "X", color = "red" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await h.Post("/sets", new { appCode = app, caption = "Дубль", code = "start" })).StatusCode);
        var id = created["id"]!.GetValue<string>();
        // елемент має бути або інструкцією, або розділом
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Put($"/sets/{id}/items", new object[] { new { scenarioId = s1, folderId = f } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await h.Put($"/sets/{id}/items", new object[] { new { scenarioId = Guid.NewGuid().ToString() } })).StatusCode);

        var upd = await h.Json(await h.Put($"/sets/{id}", new { caption = "Старт 2", code = "start", color = "#000", sortOrder = 3 }));
        Assert.Equal("Старт 2", upd["caption"]!.GetValue<string>());
        var list = await h.Json(await h.Get($"/apps/{app}/sets"));
        Assert.Single(list.AsArray());
    }

    [Fact]
    public async Task Folder_item_expands_into_published_scenarios_with_subfolders_and_keeps_order()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var f = await h.CreateFolder(app, "Корінь");
        var sub = await h.CreateFolder(app, "Під", f);
        var a = await Published(h, app, "A у корені", f, est: 3);
        var b = await Published(h, app, "B у підрозділі", sub, est: 4);
        var outside = await Published(h, app, "Окрема", est: 5);
        await h.CreateScenario(app, "Чернетка у корені", f); // не опублікована — не потрапляє

        var set = await NewSet(h, app, "Презентація", new object[] { new { scenarioId = outside }, new { folderId = f } }, code: "pres");
        var cat = await h.Json(await h.Get($"/apps/{app}/catalog"));
        var sets = cat["sets"]!.AsArray();
        Assert.Single(sets);
        Assert.Equal(new[] { "Окрема", "A у корені", "B у підрозділі" }, Captions(sets[0]!["scenarios"]));
        Assert.Equal(12, sets[0]!["estMinutes"]!.GetValue<int>());
        Assert.Equal("A у корені", sets[0]!["scenarios"]![1]!["caption"]!.GetValue<string>());
        Assert.Equal("Корінь", sets[0]!["scenarios"]![1]!["folder"]!.GetValue<string>());

        // Зміна порядку: розділ першим
        await h.Json(await h.Put($"/sets/{set}/items", new object[] { new { folderId = f }, new { scenarioId = outside } }));
        var cat2 = await h.Json(await h.Get($"/apps/{app}/catalog"));
        Assert.Equal(new[] { "A у корені", "B у підрозділі", "Окрема" }, Captions(cat2["sets"]![0]!["scenarios"]));
    }

    [Fact]
    public async Task Catalog_set_counts_available_and_unavailable_by_role_including_folder_items()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var f = await h.CreateFolder(app, "Розділ");
        async Task<string> PublishedFor(string caption, string[]? roles)
        {
            var id = await h.CreateScenario(app, caption, f, roles: roles);
            await h.Json(await h.Put($"/scenarios/{id}/steps", new[] { GuideHost.Step("s1", "a-" + id[..6]) }));
            await h.Json(await h.Post($"/scenarios/{id}/publish"));
            return id;
        }
        await PublishedFor("Для всіх", null);
        await PublishedFor("Лише лікар", new[] { "LAB_DOCTOR" });
        await PublishedFor("Лише адмін", new[] { "LAB_ADMIN" });
        await NewSet(h, app, "Огляд", new object[] { new { folderId = f } }, code: "overview");

        var tech = (await h.Json(await h.Get($"/apps/{app}/catalog", roles: "LAB_TECHNICIAN")))["sets"]![0]!;
        Assert.Equal(1, tech["scenarioCount"]!.GetValue<int>());
        Assert.Equal(3, tech["totalCount"]!.GetValue<int>());
        Assert.Equal(2, tech["unavailableCount"]!.GetValue<int>());

        var admin = (await h.Json(await h.Get($"/apps/{app}/catalog", roles: "LAB_ADMIN,LAB_DOCTOR")))["sets"]![0]!;
        Assert.Equal(3, admin["scenarioCount"]!.GetValue<int>());
        Assert.Equal(0, admin["unavailableCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task Catalog_marks_folders_and_scenarios_with_set_codes_and_colors()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var f = await h.CreateFolder(app, "Папка");
        var sub = await h.CreateFolder(app, "Підпапка", f);
        var a = await Published(h, app, "У підпапці", sub);
        var lone = await Published(h, app, "Без набору");
        await NewSet(h, app, "Синій", new object[] { new { folderId = f } }, code: "blue", color: "#1565c0");
        await NewSet(h, app, "Зелений", new object[] { new { scenarioId = a } }, code: "green", color: "#2e7d32");

        var cat = await h.Json(await h.Get($"/apps/{app}/catalog"));
        var sc = cat["scenarios"]!.AsArray().First(x => x!["id"]!.GetValue<string>() == a)!;
        Assert.Equal(new[] { "blue", "green" }, sc["setCodes"]!.AsArray().Select(x => x!.GetValue<string>()).OrderBy(x => x).ToArray());
        Assert.Contains(sc["sets"]!.AsArray(), x => x!["color"]!.GetValue<string>() == "#1565c0");
        var none = cat["scenarios"]!.AsArray().First(x => x!["id"]!.GetValue<string>() == lone)!;
        Assert.Empty(none["setCodes"]!.AsArray());
        var fol = cat["folders"]!.AsArray().First(x => x!["id"]!.GetValue<string>() == sub)!; // підпапка успадковує набір папки
        Assert.Equal(new[] { "blue" }, fol["setCodes"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
    }

    [Fact]
    public async Task Set_without_published_scenarios_or_for_other_tenant_is_hidden()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var draft = await h.CreateScenario(app, "Чернетка");
        var pub = await Published(h, app, "Опублікована");
        await NewSet(h, app, "Порожній", new object[] { new { scenarioId = draft } }, code: "empty");
        var t = "33333333-3333-3333-3333-333333333333";
        var tenantSet = (await h.Json(await h.Post("/sets", new { appCode = app, caption = "Чужий", code = "alien", tenantId = t, items = new object[] { new { scenarioId = pub } } }), HttpStatusCode.Created))["id"]!.GetValue<string>();

        var cat = await h.Json(await h.Get($"/apps/{app}/catalog"));
        Assert.Empty(cat["sets"]!.AsArray());
        var catT = await h.Json(await h.Get($"/apps/{app}/catalog", tenant: t));
        Assert.Single(catT["sets"]!.AsArray());
        Assert.NotNull(tenantSet);
    }

    [Fact]
    public async Task Import_sets_is_idempotent_and_updates_by_code()
    {
        await using var h = await GuideHost.StartAsync();
        JsonObject Bundle(string caption, string[] scenarioCodes) => new()
        {
            ["app"] = new JsonObject { ["code"] = "lab", ["caption"] = "ЛІС" },
            ["folders"] = new JsonArray(new JsonObject { ["code"] = "fo", ["caption"] = "Розділ" }),
            ["scenarios"] = new JsonArray(
                new JsonObject { ["code"] = "i.one", ["caption"] = "Перша", ["folderCode"] = "fo", ["publish"] = true, ["steps"] = new JsonArray(new JsonObject { ["title"] = "s", ["anchor"] = new JsonObject { ["code"] = "an1", ["caption"] = "A", ["strategies"] = new JsonArray(new JsonObject { ["type"] = "css", ["value"] = "#a", ["weight"] = 100 }) } }) },
                new JsonObject { ["code"] = "i.two", ["caption"] = "Друга", ["publish"] = true, ["steps"] = new JsonArray(new JsonObject { ["title"] = "s", ["anchor"] = new JsonObject { ["code"] = "an2", ["caption"] = "B", ["strategies"] = new JsonArray(new JsonObject { ["type"] = "css", ["value"] = "#b", ["weight"] = 100 }) } }) }),
            ["sets"] = new JsonArray(new JsonObject
            {
                ["code"] = "demo", ["caption"] = caption, ["color"] = "#ef6c00", ["sortOrder"] = 1,
                ["items"] = new JsonArray(new[] { new JsonObject { ["folderCode"] = "fo" } }.Concat(scenarioCodes.Select(c => new JsonObject { ["scenarioCode"] = c })).Concat(new[] { new JsonObject { ["scenarioCode"] = "unknown" } }).ToArray())
            })
        };

        var r1 = await h.Json(await h.Post("/apps/lab/import", Bundle("Демо", new[] { "i.two" })));
        Assert.Equal(1, r1["setsCreated"]!.GetValue<int>());
        var r2 = await h.Json(await h.Post("/apps/lab/import", Bundle("Демо", new[] { "i.two" })));
        Assert.Equal(0, r2["setsCreated"]!.GetValue<int>());
        Assert.Equal(1, r2["setsUpdated"]!.GetValue<int>());

        var sets = await h.Json(await h.Get("/apps/lab/sets"));
        Assert.Single(sets.AsArray());
        Assert.Equal(2, sets[0]!["items"]!.AsArray().Count); // невідомий код пропущено, дублів немає

        // Зміна складу і назви — той самий набір
        await h.Json(await h.Post("/apps/lab/import", Bundle("Демо 2", Array.Empty<string>())));
        var sets2 = await h.Json(await h.Get("/apps/lab/sets"));
        Assert.Single(sets2.AsArray());
        Assert.Equal("Демо 2", sets2[0]!["caption"]!.GetValue<string>());
        Assert.Single(sets2[0]!["items"]!.AsArray());
        Assert.Equal(sets[0]!["id"]!.GetValue<string>(), sets2[0]!["id"]!.GetValue<string>());

        var cat = await h.Json(await h.Get("/apps/lab/catalog"));
        Assert.Equal(new[] { "Перша" }, Captions(cat["sets"]![0]!["scenarios"]));
    }

    [Fact]
    public async Task Soft_deleted_set_disappears_from_catalog_and_list_but_stays_in_db()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        var s = await Published(h, app, "Одна");
        var set = await NewSet(h, app, "На видалення", new object[] { new { scenarioId = s } });
        Assert.Single((await h.Json(await h.Get($"/apps/{app}/catalog")))["sets"]!.AsArray());

        Assert.Equal(HttpStatusCode.NoContent, (await h.Delete($"/sets/{set}")).StatusCode);
        Assert.Empty((await h.Json(await h.Get($"/apps/{app}/catalog")))["sets"]!.AsArray());
        Assert.Empty((await h.Json(await h.Get($"/apps/{app}/sets"))).AsArray());
        Assert.Equal(HttpStatusCode.NotFound, (await h.Delete($"/sets/{set}")).StatusCode);

        using var scope = h.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuideDbContext>();
        var row = await db.Sets.IgnoreQueryFilters().SingleAsync(x => x.Id == set);
        Assert.Equal(RecordStates.Deleted, row.RecordState);

        // Видалена інструкція зникає з розкритого набору
        var s2 = await Published(h, app, "Друга");
        var set2 = await NewSet(h, app, "Два", new object[] { new { scenarioId = s }, new { scenarioId = s2 } });
        await h.Delete($"/scenarios/{s}");
        var cat = await h.Json(await h.Get($"/apps/{app}/catalog"));
        Assert.Equal(new[] { "Друга" }, Captions(cat["sets"]![0]!["scenarios"]));
        Assert.NotNull(set2);
    }

    [Fact]
    public async Task Ensure_database_adds_set_tables_to_existing_sqlite_db()
    {
        await using var h = await GuideHost.StartAsync();
        var app = await h.CreateApp();
        using (var scope = h.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GuideDbContext>();
            await db.Database.ExecuteSqlRawAsync("DROP TABLE gd_set_item");
            await db.Database.ExecuteSqlRawAsync("DROP TABLE gd_set");
        }
        await h.App.Services.EnsureGuideDatabaseAsync();
        await h.App.Services.EnsureGuideDatabaseAsync(); // повторно — без помилок
        var s = await Published(h, app, "Одна");
        await NewSet(h, app, "Після оновлення", new object[] { new { scenarioId = s } });
        Assert.Single((await h.Json(await h.Get($"/apps/{app}/catalog")))["sets"]!.AsArray());
    }
}
