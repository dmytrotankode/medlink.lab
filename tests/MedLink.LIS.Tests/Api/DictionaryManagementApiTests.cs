// Ведення довідників: патоморфологія (архів, цитологія і тарифи в БД), синоніми, виробничий календар, номенклатура реагентів,
// прайс-листи/пакети/платники/клієнти/тара (архів і видалення), масове оновлення цін, версії шаблонів бланків.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;
using MedLink.LIS.Api.Services;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class DictionaryManagementApiTests
{
    private const string Dict = "/api/v1/lab/dictionaries";
    private const string Lab = "/api/v1/lab";
    private readonly LisApiFactory _f;
    public DictionaryManagementApiTests(LisApiFactory f) => _f = f;

    private HttpClient Admin => _f.As(DemoDataSeeder.Admin);

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static async Task AssertProblem(HttpResponseMessage r, HttpStatusCode status, string? contains = null)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"очікувалось {(int)status}, отримано {(int)r.StatusCode}: {text}");
        if (contains != null) Assert.Contains(contains, JsonNode.Parse(text)!["detail"]!.GetValue<string>());
    }

    private static StringContent Text(string body) => new(body, Encoding.UTF8, "text/plain");

    // ------------------------------------------------------------------ патоморфологія: повне ведення з архівом
    [Theory]
    [InlineData("path-methods", "T_PM_1", "Тестове забарвлення")]
    [InlineData("path-material-types", "T_PMT_1", "Тестовий тип матеріалу")]
    [InlineData("path-reject-reasons", "T_PRR_1", "Тестова причина відмови")]
    [InlineData("path-rooms", "T_ROOM_1", "Тестове приміщення")]
    public async Task Pathology_dictionary_create_edit_archive_restore(string name, string code, string title)
    {
        await Admin.GetAsync($"{Lab}/pathology/rooms"); // типові приміщення створюються при першому зверненні — до тестового запису
        var created = await Json(await Admin.PostAsJsonAsync($"{Dict}/{name}", new { code, name = title, slaDays = 4, price = 100, basePrice = 50 }));
        Assert.True(created["isActive"]!.GetValue<bool>());
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/{name}", new { code, name = "Дубль" }), HttpStatusCode.Conflict);
        var id = created["id"]!.ToString();
        var upd = await Json(await Admin.PutAsJsonAsync($"{Dict}/{name}/{id}", new { name = title + " (змінено)" }));
        Assert.EndsWith("(змінено)", upd["name"]!.ToString());
        // видалення завжди архівує: історія справ не втрачається
        var del = await Json(await Admin.DeleteAsync($"{Dict}/{name}/{id}"));
        Assert.True(del["deactivated"]!.GetValue<bool>());
        var archived = await Json(await Admin.GetAsync($"{Dict}/{name}?isActive=false&search={code}&pageSize=500"));
        Assert.Single(archived["items"]!.AsArray());
        var restored = await Json(await Admin.PutAsJsonAsync($"{Dict}/{name}/{id}", new { isActive = true }));
        Assert.True(restored["isActive"]!.GetValue<bool>());
        await Json(await Admin.DeleteAsync($"{Dict}/{name}/{id}")); // повертаємо в архів, щоб не зіпсувати спільні дані інших тестів
    }

    [Fact]
    public async Task Pathology_fixative_template_and_ihc_range_rules()
    {
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/path-fixatives", new { name = "Т_ФІКС", minHours = 48, maxHours = 6 }), HttpStatusCode.BadRequest, "перевищує");
        var fx = await Json(await Admin.PostAsJsonAsync($"{Dict}/path-fixatives", new { name = "Т_ФІКС", minHours = 6, maxHours = 48 }));
        Assert.Equal("Т_ФІКС", fx["name"]!.ToString());
        var tpl = await Json(await Admin.PostAsJsonAsync($"{Dict}/path-templates", new { name = "Т_ШАБЛОН", macro = "Макро", micro = "Мікро" }));
        await Json(await Admin.PutAsJsonAsync($"{Dict}/path-templates/{tpl["id"]}", new { micro = "Мікро 2" }));
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/path-ihc-rules", new { topoFrom = "C50", topoTo = "C18", morphCodes = "8500/3", panel = "ER, PR" }), HttpStatusCode.BadRequest, "діапазону");
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/path-ihc-rules", new { topoFrom = "X50", topoTo = "C59", morphCodes = "8500/3", panel = "ER, PR" }), HttpStatusCode.BadRequest, "C00");
        var rule = await Json(await Admin.PostAsJsonAsync($"{Dict}/path-ihc-rules", new { topoFrom = "C50", topoTo = "C50.9", morphCodes = "8500/3", panel = "ER, PR, HER2", why = "Тест" }));
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/path-ihc-rules", new { topoFrom = "C50", topoTo = "C50.9", morphCodes = "8500/3", panel = "ER, PR, HER2" }), HttpStatusCode.Conflict);
        var dict = await Json(await Admin.GetAsync($"{Lab}/pathology/dictionaries"));
        Assert.Contains(dict["reflex"]!.AsArray(), r => r!["panel"]!.ToString() == "ER, PR, HER2");
        await Json(await Admin.DeleteAsync($"{Dict}/path-ihc-rules/{rule["id"]}"));
        dict = await Json(await Admin.GetAsync($"{Lab}/pathology/dictionaries"));
        Assert.DoesNotContain(dict["reflex"]!.AsArray(), r => r!["panel"]!.ToString() == "ER, PR, HER2"); // архівне правило не застосовується
    }

    [Fact]
    public async Task Pathology_cytology_catalog_and_prices_come_from_database()
    {
        var seeded = await Json(await Admin.GetAsync($"{Dict}/path-cytology?pageSize=500"));
        Assert.True(seeded["total"]!.GetValue<int>() >= 18, "цитологічні довідники винесені з коду в БД");
        var cat = await Json(await Admin.PostAsJsonAsync($"{Dict}/path-cytology", new { kind = "CATEGORY", code = "T_CAT", name = "Тестова категорія", isHigh = true, materials = "" }));
        var dict = await Json(await Admin.GetAsync($"{Lab}/pathology/dictionaries"));
        var found = dict["cytology"]!["categories"]!.AsArray().Single(c => c!["code"]!.ToString() == "T_CAT");
        Assert.True(found!["high"]!.GetValue<bool>());
        await Json(await Admin.DeleteAsync($"{Dict}/path-cytology/{cat["id"]}"));
        dict = await Json(await Admin.GetAsync($"{Lab}/pathology/dictionaries"));
        Assert.DoesNotContain(dict["cytology"]!["categories"]!.AsArray(), c => c!["code"]!.ToString() == "T_CAT");
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/path-cytology", new { kind = "UNKNOWN", code = "X", name = "X" }), HttpStatusCode.BadRequest);

        // тарифи
        var prices = await Json(await Admin.GetAsync($"{Dict}/path-prices?pageSize=500"));
        var first = prices["items"]!.AsArray().Single(p => p!["code"]!.ToString() == "FIRST_BLOCK");
        var old = first!["amount"]!.GetValue<decimal>();
        await AssertProblem(await Admin.PutAsJsonAsync($"{Dict}/path-prices/{first["id"]}", new { amount = -1 }), HttpStatusCode.BadRequest);
        await Json(await Admin.PutAsJsonAsync($"{Dict}/path-prices/{first["id"]}", new { amount = 555 }));
        dict = await Json(await Admin.GetAsync($"{Lab}/pathology/dictionaries"));
        Assert.Equal(555m, dict["prices"]!["firstBlock"]!.GetValue<decimal>());
        Assert.Equal(555m + 140m, PathologyService.GrossPrice(2));
        await Json(await Admin.PutAsJsonAsync($"{Dict}/path-prices/{first["id"]}", new { amount = old }));
        Assert.Equal(old, (await Json(await Admin.GetAsync($"{Lab}/pathology/dictionaries")))["prices"]!["firstBlock"]!.GetValue<decimal>());
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/path-prices", new { code = "NOPE", name = "Невідомий", amount = 1 }), HttpStatusCode.BadRequest, "Невідомий тариф");
    }

    // ------------------------------------------------------------------ синоніми й календар
    [Fact]
    public async Task Test_synonyms_are_found_by_search_and_validated()
    {
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/test-synonyms", new { testCode = "NOPE_TEST", synonym = "Щось" }), HttpStatusCode.BadRequest, "Показник не знайдено");
        var syn = await Json(await Admin.PostAsJsonAsync($"{Dict}/test-synonyms", new { testCode = "hba1c", synonym = "Глікований гемоглобін ТЕСТ", language = "uk" }));
        Assert.Equal("HBA1C", syn["testCode"]!.ToString());
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/test-synonyms", new { testCode = "HBA1C", synonym = "глікований гемоглобін тест" }), HttpStatusCode.Conflict);
        var found = await Json(await Admin.GetAsync($"{Dict}/tests?search=глікований гемоглобін тест&pageSize=50"));
        Assert.Contains(found["items"]!.AsArray(), t => t!["code"]!.ToString() == "HBA1C");
        var matrix = await Json(await Admin.GetAsync($"{Dict}/order-matrix"));
        Assert.Contains("Глікований гемоглобін ТЕСТ", matrix.ToJsonString(new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        await Json(await Admin.DeleteAsync($"{Dict}/test-synonyms/{syn["id"]}"));
        var gone = await Json(await Admin.GetAsync($"{Dict}/tests?search=глікований гемоглобін тест&pageSize=50"));
        Assert.Empty(gone["items"]!.AsArray());
    }

    [Fact]
    public async Task Work_calendar_shifts_sla_workdays_and_validates_kind()
    {
        var wed = new DateTime(2036, 6, 4, 0, 0, 0, DateTimeKind.Utc); // середа
        Assert.Equal(DayOfWeek.Wednesday, wed.DayOfWeek);
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/work-calendar", new { day = "2036-06-07", kind = "HOLIDAY", name = "Субота" }), HttpStatusCode.BadRequest, "Субота й неділя");
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/work-calendar", new { day = "2036-06-05", kind = "WORKDAY", name = "Четвер" }), HttpStatusCode.BadRequest, "суботу або неділю");
        var holiday = await Json(await Admin.PostAsJsonAsync($"{Dict}/work-calendar", new { day = "2036-06-04", kind = "HOLIDAY", name = "Тестове свято" }));
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/work-calendar", new { day = "2036-06-04", kind = "HOLIDAY", name = "Дубль" }), HttpStatusCode.Conflict);
        var work = await Json(await Admin.PostAsJsonAsync($"{Dict}/work-calendar", new { day = "2036-06-07", kind = "WORKDAY", name = "Робоча субота" }));
        try
        {
            var tue = wed.AddDays(-1);
            Assert.Equal(DayOfWeek.Thursday, PathologyService.AddWorkdays(tue, 1).DayOfWeek); // середа — свято
            Assert.False(WorkCalendar.IsWorkday(wed));
            Assert.True(WorkCalendar.IsWorkday(new DateTime(2036, 6, 7))); // робоча субота
            Assert.Equal(DayOfWeek.Saturday, PathologyService.AddWorkdays(new DateTime(2036, 6, 6), 1).DayOfWeek); // п'ятниця + 1 = робоча субота
        }
        finally
        {
            await Admin.DeleteAsync($"{Dict}/work-calendar/{holiday["id"]}");
            await Admin.DeleteAsync($"{Dict}/work-calendar/{work["id"]}");
        }
        Assert.True(WorkCalendar.IsWorkday(wed));
        Assert.False(WorkCalendar.IsWorkday(new DateTime(2036, 6, 7)));
    }

    // ------------------------------------------------------------------ реагенти
    [Fact]
    public async Task Reagent_nomenclature_is_separate_from_lots_and_archived_when_used()
    {
        var item = await Json(await Admin.PostAsJsonAsync($"{Dict}/reagent-items", new { code = "T_RGT_1", name = "Тестовий реагент глюкози", manufacturer = "ТестВиробник", testCode = "GLU", unit = "тест", minimumTests = 30 }));
        await AssertProblem(await Admin.PostAsJsonAsync($"{Dict}/reagent-items", new { code = "T_RGT_1", name = "Дубль" }), HttpStatusCode.Conflict);
        var itemId = item["id"]!.ToString();
        // лот за номенклатурою: назва й виробник підставляються
        var lot = await Json(await _f.As(DemoDataSeeder.Technician).PostAsJsonAsync($"{Lab}/reagents/lots", new { reagentItemId = itemId, lotNumber = "LOT-T-1", testsInitial = 100, expiryDate = DateTime.UtcNow.AddYears(1) }));
        Assert.Equal("Тестовий реагент глюкози", lot["reagentName"]!.ToString());
        Assert.Equal("ТестВиробник", lot["manufacturer"]!.ToString());
        Assert.Equal(itemId, lot["reagentItemId"]!.ToString());
        // позиція використовується лотом → видалення архівує
        var del = await Json(await Admin.DeleteAsync($"{Dict}/reagent-items/{itemId}"));
        Assert.True(del["deactivated"]!.GetValue<bool>());
        var unused = await Json(await Admin.PostAsJsonAsync($"{Dict}/reagent-items", new { code = "T_RGT_2", name = "Не використаний" }));
        Assert.True((await Json(await Admin.DeleteAsync($"{Dict}/reagent-items/{unused["id"]}")))["deleted"]!.GetValue<bool>());
        await AssertProblem(await _f.As(DemoDataSeeder.Technician).PostAsJsonAsync($"{Lab}/reagents/lots", new { reagentItemId = "NO_SUCH", lotNumber = "L", testsInitial = 1, expiryDate = DateTime.UtcNow.AddYears(1) }), HttpStatusCode.BadRequest, "номенклатури");
    }

    // ------------------------------------------------------------------ прайси, пакети, платники
    [Fact]
    public async Task Price_list_packages_mass_update_and_delete_or_archive()
    {
        var created = await Json(await Admin.PostAsJsonAsync($"{Lab}/price-lists", new { code = "T_PL_1", name = "Тестовий прайс", isActive = true, items = new Dictionary<string, decimal> { ["PROF_CBC"] = 100m } }));
        var id = created["id"]!.ToString();
        // пакети
        await AssertProblem(await Admin.PostAsJsonAsync($"{Lab}/price-lists/{id}/packages", new { code = "T_PK_1", name = "Пакет", price = 50, members = new[] { "PROF_CBC" } }), HttpStatusCode.BadRequest, "дві");
        var pk = await Json(await Admin.PostAsJsonAsync($"{Lab}/price-lists/{id}/packages", new { code = "t_pk_1", name = "Пакет тест", price = 150, members = new[] { "PROF_CBC", "PROF_BIOCHEM_BASE" } }));
        Assert.Equal("T_PK_1", pk["code"]!.ToString());
        await AssertProblem(await Admin.PostAsJsonAsync($"{Lab}/price-lists/{id}/packages", new { code = "T_PK_1", name = "Дубль", price = 1, members = new[] { "PROF_CBC", "PROF_BIOCHEM_BASE" } }), HttpStatusCode.Conflict);
        var upd = await Json(await Admin.PutAsJsonAsync($"{Lab}/price-lists/{id}/packages/{pk["id"]}", new { price = 160 }));
        Assert.Equal(160m, upd["price"]!.GetValue<decimal>());
        Assert.True((await Json(await Admin.DeleteAsync($"{Lab}/price-lists/{id}/packages/{pk["id"]}")))["deleted"]!.GetValue<bool>());

        // масове оновлення цін
        var csv = "code;price\r\nPROF_CBC;120,50\r\nPROF_BIOCHEM_BASE;300\r\nPROF_CBC;999\r\nNO_SUCH;10\r\nPROF_COAG;-5\r\n;1\r\n";
        var dry = await Json(await Admin.PostAsync($"{Lab}/price-lists/{id}/import-prices?dryRun=true", Text(csv)));
        Assert.Equal(1, dry["create"]!.GetValue<int>());
        Assert.Equal(1, dry["update"]!.GetValue<int>());
        Assert.Equal(2, dry["skip"]!.GetValue<int>());
        Assert.Equal(2, dry["error"]!.GetValue<int>());
        var before = (await Json(await Admin.GetAsync($"{Lab}/price-lists"))).AsArray().Single(l => l!["id"]!.ToString() == id)!;
        Assert.Equal(1, before["itemsCount"]!.GetValue<int>());
        await Json(await Admin.PostAsync($"{Lab}/price-lists/{id}/import-prices?dryRun=false", Text(csv)));
        var after = (await Json(await Admin.GetAsync($"{Lab}/price-lists"))).AsArray().Single(l => l!["id"]!.ToString() == id)!;
        Assert.Equal(2, after["itemsCount"]!.GetValue<int>());
        Assert.Contains(after["items"]!.AsArray(), i => i!["price"]!.GetValue<decimal>() == 120.5m);

        // платник на цьому прайсі → прайс не видаляється, а архівується
        var payer = await Json(await Admin.PostAsJsonAsync($"{Lab}/payers", new { code = "T_PAY_1", name = "Тестовий платник", kind = "ENTERPRISE", priceListId = id, coveragePct = 100 }));
        var arch = await Json(await Admin.DeleteAsync($"{Lab}/price-lists/{id}"));
        Assert.True(arch["deactivated"]!.GetValue<bool>());
        // платник без замовлень → видаляється; після цього порожній прайс теж
        Assert.True((await Json(await Admin.DeleteAsync($"{Lab}/payers/{payer["id"]}")))["deleted"]!.GetValue<bool>());
        Assert.True((await Json(await Admin.DeleteAsync($"{Lab}/price-lists/{id}")))["deleted"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Price_import_limit_and_default_list_protected_and_patient_payer_protected()
    {
        var lists = await Json(await Admin.GetAsync($"{Lab}/price-lists"));
        var def = lists.AsArray().FirstOrDefault(l => l!["isDefault"]!.GetValue<bool>());
        if (def != null) await AssertProblem(await Admin.DeleteAsync($"{Lab}/price-lists/{def["id"]}"), HttpStatusCode.Conflict, "за замовчуванням");
        var anyList = lists.AsArray()[0]!["id"]!.ToString();
        var sb = new StringBuilder("code;price\r\n");
        for (var i = 0; i < 5001; i++) sb.Append($"C{i};1\r\n");
        await AssertProblem(await Admin.PostAsync($"{Lab}/price-lists/{anyList}/import-prices?dryRun=true", Text(sb.ToString())), HttpStatusCode.BadRequest, "5000");
        await AssertProblem(await Admin.DeleteAsync($"{Lab}/payers/PATIENT"), HttpStatusCode.Conflict, "Пацієнт");
        await AssertProblem(await _f.As(DemoDataSeeder.Registrar).DeleteAsync($"{Lab}/payers/PATIENT"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Client_created_from_clients_screen_and_archived_or_removed()
    {
        var c = await Json(await Admin.PostAsJsonAsync($"{Lab}/clients", new { code = "T_CLI_1", name = "Клініка-клієнт тест", edrpou = "12345678" }));
        Assert.Equal("CLINIC", c["kind"]!.ToString());
        var list = await Json(await Admin.GetAsync($"{Lab}/clients"));
        Assert.Contains(list.AsArray(), x => x!["code"]!.ToString() == "T_CLI_1");
        await Json(await Admin.PutAsJsonAsync($"{Lab}/clients/{c["id"]}", new { code = "T_CLI_1", name = "Клініка-клієнт (змінено)", kind = "INSURANCE" }));
        var updated = (await Json(await Admin.GetAsync($"{Lab}/clients"))).AsArray().Single(x => x!["code"]!.ToString() == "T_CLI_1")!;
        Assert.Equal("Клініка-клієнт (змінено)", updated["name"]!.ToString()); // вид залишився CLINIC
        Assert.True((await Json(await Admin.DeleteAsync($"{Lab}/clients/{c["id"]}")))["deleted"]!.GetValue<bool>());
        await AssertProblem(await _f.As(DemoDataSeeder.Technician).PostAsJsonAsync($"{Lab}/clients", new { code = "T_CLI_2", name = "Заборонено" }), HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ транспортна тара
    [Fact]
    public async Task Container_type_archived_when_used_deleted_when_free()
    {
        var courier = _f.As(DemoDataSeeder.Courier);
        var free = await Json(await Admin.PostAsJsonAsync($"{Lab}/logistics/container-types", new { code = "T_CT_FREE", name = "Вільний тип", capacity = 6 }));
        Assert.True((await Json(await Admin.DeleteAsync($"{Lab}/logistics/container-types/{free["id"]}")))["deleted"]!.GetValue<bool>());
        var used = await Json(await Admin.PostAsJsonAsync($"{Lab}/logistics/container-types", new { code = "T_CT_USED", name = "Використаний тип", capacity = 6 }));
        await Json(await courier.PostAsJsonAsync($"{Lab}/logistics/containers", new { typeId = used["id"]!.ToString(), originDepartmentId = DemoDataSeeder.CollectionPoint, destinationDepartmentId = DemoDataSeeder.Dept }));
        var res = await Json(await Admin.DeleteAsync($"{Lab}/logistics/container-types/{used["id"]}"));
        Assert.True(res["deactivated"]!.GetValue<bool>());
        var active = await Json(await Admin.GetAsync($"{Lab}/logistics/container-types"));
        Assert.DoesNotContain(active.AsArray(), t => t!["code"]!.ToString() == "T_CT_USED");
        await AssertProblem(await _f.As(DemoDataSeeder.Technician).DeleteAsync($"{Lab}/logistics/container-types/{used["id"]}"), HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ шаблони бланків
    [Fact]
    public async Task Report_templates_versions_activate_and_edit_requisites()
    {
        var list = await Json(await _f.As(DemoDataSeeder.Technician).GetAsync($"{Lab}/report-templates"));
        Assert.True(list.AsArray().Count(t => t!["isActive"]!.GetValue<bool>()) >= 3); // є активна версія кожного варіанта
        var active = list.AsArray().First(t => t!["variant"]!.ToString() == "final" && t["isActive"]!.GetValue<bool>())!;
        var v2 = await Json(await Admin.PostAsJsonAsync($"{Lab}/report-templates", new { variant = "final", name = "Версія 2", title = "РЕЗУЛЬТАТИ ДОСЛІДЖЕНЬ (ТЕСТ)", headerNote = "Акредитація ТЕСТ", footerText = "Підвал ТЕСТ", languages = "uk,en" }));
        Assert.Equal(active["version"]!.GetValue<int>() + 1, v2["version"]!.GetValue<int>());
        Assert.False(v2["isActive"]!.GetValue<bool>());
        await AssertProblem(await Admin.PostAsJsonAsync($"{Lab}/report-templates", new { variant = "nope", name = "X" }), HttpStatusCode.BadRequest);
        await AssertProblem(await Admin.PutAsJsonAsync($"{Lab}/report-templates/{v2["id"]}", new { languages = "uk,de" }), HttpStatusCode.BadRequest, "uk, en");
        await AssertProblem(await _f.As(DemoDataSeeder.Doctor).PostAsJsonAsync($"{Lab}/report-templates", new { variant = "final", name = "Не лікар" }), HttpStatusCode.Forbidden);
        await AssertProblem(await Admin.DeleteAsync($"{Lab}/report-templates/{active["id"]}"), HttpStatusCode.Conflict, "Активну");
        // бланк використовує активну версію
        var orderId = await ReportableOrderIdAsync();
        await Json(await Admin.PostAsync($"{Lab}/report-templates/{v2["id"]}/activate", null));
        var versions = await Json(await Admin.GetAsync($"{Lab}/report-templates?variant=final"));
        Assert.Single(versions.AsArray().Where(t => t!["isActive"]!.GetValue<bool>()));
        if (orderId != null)
        {
            var html = await (await Admin.GetAsync($"{Lab}/orders/{orderId}/report")).Content.ReadAsStringAsync();
            Assert.Contains("РЕЗУЛЬТАТИ ДОСЛІДЖЕНЬ (ТЕСТ)", html);
            Assert.Contains("Акредитація ТЕСТ", html);
            Assert.Contains("Підвал ТЕСТ", html);
            // без власного заголовка при мові «en» бланк двомовний
            await Json(await Admin.PutAsJsonAsync($"{Lab}/report-templates/{v2["id"]}", new { title = "" }));
            html = await (await Admin.GetAsync($"{Lab}/orders/{orderId}/report")).Content.ReadAsStringAsync();
            Assert.Contains("LABORATORY TEST RESULTS", html);
            Assert.Contains("РЕЗУЛЬТАТИ ЛАБОРАТОРНИХ ДОСЛІДЖЕНЬ", html);
        }
        // повернення старої версії; тестова версія видаляється
        await Json(await Admin.PostAsync($"{Lab}/report-templates/{active["id"]}/activate", null));
        Assert.True((await Json(await Admin.DeleteAsync($"{Lab}/report-templates/{v2["id"]}")))["deleted"]!.GetValue<bool>());
        if (orderId != null)
        {
            var html = await (await Admin.GetAsync($"{Lab}/orders/{orderId}/report")).Content.ReadAsStringAsync();
            Assert.Contains("РЕЗУЛЬТАТИ ЛАБОРАТОРНИХ ДОСЛІДЖЕНЬ", html);
        }
    }

    private async Task<string?> ReportableOrderIdAsync()
    {
        var orders = await Json(await Admin.GetAsync($"{Lab}/orders?pageSize=100"));
        foreach (var o in orders["items"]!.AsArray())
        {
            var r = await Admin.GetAsync($"{Lab}/orders/{o!["id"]}/report");
            if (r.IsSuccessStatusCode) return o["id"]!.ToString();
        }
        return null;
    }
}
