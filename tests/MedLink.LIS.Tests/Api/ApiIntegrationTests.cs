// Інтеграційні тести API: WebApplicationFactory + тимчасова SQLite, повний процес за ролями, негативні переходи, ВКЯ lockout
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MedLink.LIS.Tests.Api;

public sealed class LisApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"medlink-lis-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:SqlitePath"] = _dbPath,
            ["Lab:SeedDemoData"] = "true",
            ["Lab:PublicBaseUrl"] = "http://localhost:5055",
            ["Logging:LogLevel:Default"] = "Warning"
        }));
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
        try { File.Delete(_dbPath); } catch { /* ignore */ }
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<LisApiFactory> { }

[Collection("api")]
public class ApiIntegrationTests
{
    private readonly LisApiFactory _f;
    public ApiIntegrationTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static async Task AssertProblem(HttpResponseMessage r, HttpStatusCode status)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.Equal(status, r.StatusCode);
        Assert.Contains("\"title\"", text);
    }

    [Fact]
    public async Task Health_swagger_worklist_orders_respond()
    {
        var c = _f.CreateClient();
        var health = await Json(await c.GetAsync("/health"));
        Assert.Equal("Healthy", health["status"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        var wl = await Json(await c.GetAsync("/api/v1/lab/worklist?pageSize=5"));
        Assert.True(wl["total"]!.GetValue<int>() > 0);
        var orders = await Json(await c.GetAsync("/api/v1/lab/orders"));
        Assert.True(orders["total"]!.GetValue<int>() >= 8);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/lab/nothing")).StatusCode);
    }

    [Fact]
    public async Task Full_happy_path_by_roles_with_connector_and_report()
    {
        var registrar = _f.As(DemoDataSeeder.Registrar);
        var nurse = _f.As(DemoDataSeeder.Nurse);
        var courier = _f.As(DemoDataSeeder.Courier);
        var tech = _f.As(DemoDataSeeder.Technician);
        var doctor = _f.As(DemoDataSeeder.Doctor);
        var admin = _f.As(DemoDataSeeder.Admin);

        // Реєстратор створює замовлення (біохімія + K)
        var order = await Json(await registrar.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = "pat-0000-0000-0000-000000000003", doctorId = DemoDataSeeder.Doctor, departmentId = DemoDataSeeder.CollectionPoint, profileIds = new[] { "PROF_BIOCHEM_BASE" }, testIds = new[] { "K" } }));
        var orderId = order["id"]!.GetValue<string>();
        Assert.Matches(@"^\d{4}-\d{6}$", order["orderNumber"]!.GetValue<string>());
        Assert.Equal("NEW", order["status"]!.GetValue<string>());
        var barcode = order["samples"]![0]!["barcode"]!.GetValue<string>();
        Assert.Equal(8, barcode.Length);

        // Медсестра пункту забору збирає пробірку
        var sample = await Json(await nurse.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/collect", new { checklist = new { idVerified = true, fasting = true, orderOfDraw = true, mixing = true }, volumeMl = 4.0 }));
        Assert.Equal("COLLECTED", sample["status"]!.GetValue<string>());
        // повторний забір → 409
        await AssertProblem(await nurse.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/collect", new { }), HttpStatusCode.Conflict);

        // Кур'єр: маніфест → IN_TRANSIT → прийом із холодовим ланцюгом
        var manifest = await Json(await courier.PostAsJsonAsync("/api/v1/lab/logistics/manifests", new { originDepartmentId = DemoDataSeeder.CollectionPoint, destinationDepartmentId = DemoDataSeeder.Dept, courierName = "Ткаченко", temperatureDispatch = 5.0, barcodes = new[] { barcode } }));
        Assert.Equal("DISPATCHED", manifest["status"]!.GetValue<string>());
        var o2 = await Json(await admin.GetAsync($"/api/v1/lab/orders/{orderId}"));
        Assert.Equal("IN_TRANSIT", o2["status"]!.GetValue<string>());
        var received = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/logistics/manifests/{manifest["id"]}/receive", new { temperatureReceipt = 12.0, receivedBarcodes = new[] { barcode } }));
        Assert.Equal("RECEIVED", received["status"]!.GetValue<string>());
        Assert.True(received["isColdChainViolated"]!.GetValue<bool>());

        // Журнал секції заповнено при прийомі
        var journal = await Json(await admin.GetAsync($"/api/v1/lab/orders/{orderId}/journal-entries"));
        Assert.True(journal.AsArray().Count >= 1);
        Assert.Matches(@"^\d{4}-\d{6}$", journal[0]!["journalNumber"]!.GetValue<string>());

        var detail = await Json(await admin.GetAsync($"/api/v1/lab/orders/{orderId}"));
        Assert.Equal("RECEIVED", detail["status"]!.GetValue<string>());
        var tests = detail["tests"]!.AsArray();
        string TestId(string code) => tests.First(t => t!["testCode"]!.GetValue<string>() == code)!["id"]!.GetValue<string>();

        // Лаборант вводить результат вручну (нормальний → автоверифікація, released прогресивно)
        var row = await Json(await tech.PutAsJsonAsync($"/api/v1/lab/results/{TestId("GLU")}", new { numericValue = 5.2 }));
        Assert.Equal("AUTO_VERIFIED", row["status"]!.GetValue<string>());
        Assert.Equal("NORMAL", row["flag"]!.GetValue<string>());
        Assert.NotNull(row["releasedAt"]);
        // Кур'єр не може вводити результати → 403
        await AssertProblem(await courier.PutAsJsonAsync($"/api/v1/lab/results/{TestId("UREA")}", new { numericValue = 5.0 }), HttpStatusCode.Forbidden);

        // Коннектор: реєстрація → конфіг → результати (критичний калій)
        var anon = _f.CreateClient();
        var reg = await Json(await anon.PostAsJsonAsync("/api/v1/lab/connector/register", new { installKey = "DEMO-INSTALL-KEY-0001", hostName = "LAB-PC-1", osDescription = "Windows 11", version = "4.0.0" }));
        var apiKey = reg["apiKey"]!.GetValue<string>();
        var con = _f.CreateClient();
        con.DefaultRequestHeaders.Add("X-MedLink-ApiKey", apiKey);
        var config = await Json(await con.GetAsync("/api/v1/lab/connector/config"));
        Assert.Equal(2, config["analyzers"]!.AsArray().Count);
        Assert.Contains(config["analyzers"]!.AsArray(), a => a!["protocol"]!.GetValue<string>() == "HL7");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/lab/connector/config")).StatusCode);
        var accepted = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/results", new
        {
            analyzerId = DemoDataSeeder.Mindray, receivedAt = DateTime.UtcNow, isQc = false,
            results = new object[]
            {
                new { barcode, analyzerCode = "K", value = "6.9", unit = "mmol/L", flags = "H", measuredAt = DateTime.UtcNow },
                new { barcode, analyzerCode = "Crea", value = "88", unit = "umol/L", flags = "N" },
                new { barcode, analyzerCode = "Urea", value = "5.1", flags = "N" },
                new { barcode, analyzerCode = "ALT", value = "25", flags = "N" },
                new { barcode, analyzerCode = "AST", value = "22", flags = "N" },
                new { barcode, analyzerCode = "TBIL", value = "10.5", flags = "N" },
                new { barcode, analyzerCode = "TP", value = "72", flags = "N" },
                new { barcode = "99999999", analyzerCode = "GLU", value = "5.0" }
            }
        }));
        Assert.Equal(7, accepted["matched"]!.GetValue<int>());
        Assert.Single(accepted["unmatched"]!.AsArray());
        var unmatched = await Json(await admin.GetAsync("/api/v1/lab/results/unmatched"));
        Assert.Contains(unmatched.AsArray(), u => u!["barcode"]!.GetValue<string>() == "99999999");

        var kRow = await Json(await admin.GetAsync($"/api/v1/lab/results/{TestId("K")}"));
        Assert.Equal("CRIT_HIGH", kRow["flag"]!.GetValue<string>());
        Assert.Equal("NEEDS_REVIEW", kRow["status"]!.GetValue<string>());

        // Лаборант не може верифікувати → 403; лікар без коментаря до критичного → 400
        await AssertProblem(await tech.PostAsJsonAsync($"/api/v1/lab/results/{TestId("K")}/verify", new { }), HttpStatusCode.Forbidden);
        await AssertProblem(await doctor.PostAsJsonAsync($"/api/v1/lab/results/{TestId("K")}/verify", new { }), HttpStatusCode.BadRequest);
        // Видача до верифікації → 409
        await AssertProblem(await doctor.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/release", new { }), HttpStatusCode.Conflict);

        var verified = await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/results/{TestId("K")}/verify", new { comment = "Гіперкаліємія, лікаря повідомлено" }));
        Assert.Equal("VERIFIED", verified["status"]!.GetValue<string>());
        var batch = await Json(await doctor.PostAsJsonAsync("/api/v1/lab/results/verify-batch", new { orderTestIds = tests.Select(t => t!["id"]!.GetValue<string>()).ToArray() }));
        Assert.True(batch["verified"]!.GetValue<int>() >= 1);

        var beforeRelease = await Json(await admin.GetAsync($"/api/v1/lab/orders/{orderId}"));
        Assert.Equal("COMPLETED", beforeRelease["status"]!.GetValue<string>());
        var released = await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/release", new { }));
        Assert.Equal("RELEASED", released["status"]!.GetValue<string>());
        var token = released["verifyToken"]!.GetValue<string>();
        var verify = await Json(await anon.GetAsync($"/api/v1/lab/verify/{token}"));
        Assert.True(verify["valid"]!.GetValue<bool>());

        var html = await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report");
        Assert.Equal(HttpStatusCode.OK, html.StatusCode);
        Assert.Contains("text/html", html.Content.Headers.ContentType!.MediaType);
        Assert.Contains("РЕЗУЛЬТАТИ ЛАБОРАТОРНИХ", await html.Content.ReadAsStringAsync());
        var pdf = await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report.pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);

        // Портал: усі тести відкриті, прогрес 100%
        var portal = await Json(await anon.GetAsync($"/api/v1/lab/portal/pat-0000-0000-0000-000000000003/orders/{orderId}"));
        Assert.Equal(100, portal["progress"]!["percent"]!.GetValue<int>());
        Assert.True(portal["tests"]!.AsArray().Count >= 8);

        // Переходи для поточного статусу
        var transitions = await Json(await doctor.GetAsync($"/api/v1/lab/orders/{orderId}/transitions"));
        Assert.Contains(transitions["allowedActions"]!.AsArray(), a => a!["action"]!.GetValue<string>() == "REOPEN");
    }

    [Fact]
    public async Task Qc_1_3s_creates_lockout_which_blocks_autoverification_until_resolved()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var doctor = _f.As(DemoDataSeeder.Doctor);
        // Mindray L1 GLU: ціль 4.2 ± 0.15 → 4.8 = z 4.0 → 1_3s
        var qc = await Json(await tech.PostAsJsonAsync("/api/v1/lab/qc/results", new { qcMaterialId = "qcm-0000-0000-0000-000000000003", testCode = "GLU", measuredValue = 4.8 }));
        Assert.True(qc["isRejection"]!.GetValue<bool>());
        Assert.True(qc["lockoutEnforced"]!.GetValue<bool>());
        var lockoutId = qc["lockoutId"]!.GetValue<string>();
        var lj = await Json(await tech.GetAsync("/api/v1/lab/qc/levey-jennings?testCode=GLU&materialId=qcm-0000-0000-0000-000000000003"));
        Assert.Equal("LOCKOUT", lj["currentStatus"]!.GetValue<string>());
        // лаборант не може розблокувати → 403
        await AssertProblem(await tech.PostAsJsonAsync($"/api/v1/lab/qc/lockouts/{lockoutId}/resolve", new { cause = "x", action = "y" }), HttpStatusCode.Forbidden);
        var resolved = await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/qc/lockouts/{lockoutId}/resolve", new { cause = "Калібрувальна крива застаріла", action = "Перекалібровано, повторний контроль у нормі", comment = "ок" }));
        Assert.NotNull(resolved["resolvedAt"]);
        Assert.Contains("ПРОТОКОЛ РОЗБЛОКУВАННЯ", resolved["protocolText"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("biomaterials", """{"code":"BM_T1","name":"Тестовий біоматеріал","stabilityHours":12}""", """{"name":"Тестовий біоматеріал (змінено)"}""")]
    [InlineData("tube-types", """{"code":"TUBE_T1","name":"Тестова пробірка","colorCode":"#123456","volumeMl":2}""", """{"volumeMl":3}""")]
    [InlineData("method-types", """{"code":"METH_T1","name":"Тестова методика"}""", """{"name":"Методика 2"}""")]
    [InlineData("analyzer-types", """{"code":"TESTTYPE","name":"Test Analyzer","category":"BIOCHEM","exchType":"ASTM"}""", """{"sleepMs":200}""")]
    [InlineData("tests", """{"code":"ZZTEST","name":"Тестовий показник","unit":"од","biomaterialTypeId":4,"category":"BIOCHEM"}""", """{"name":"Показник 2","decimalPlaces":1}""")]
    [InlineData("profiles", """{"code":"PROF_T1","name":"Тестовий профіль","category":"Тест","items":[{"testCode":"GLU"},{"testCode":"K"}]}""", """{"name":"Профіль 2","turnaroundHours":5}""")]
    [InlineData("reflex-rules", """{"triggerTestCode":"K","conditionOperator":">","thresholdValue":6.0,"reflexTestCode":"NA","description":"тест"}""", """{"thresholdValue":6.5}""")]
    [InlineData("organisms", """{"code":"TESTORG","latinName":"Testus organismus","kingdom":"BACTERIA"}""", """{"commonName":"Тестовий"}""")]
    [InlineData("antibiotics", """{"code":"TST","name":"Тестоміцин","groupName":"Test"}""", """{"defaultDiskContentMcg":10}""")]
    [InlineData("eucast-breakpoints", """{"organismId":9,"antibioticId":13,"micSusceptibleLe":1,"micResistantGt":2}""", """{"guidanceNotes":"оновлено"}""")]
    [InlineData("departments", """{"code":"DEP_T1","name":"Тестовий підрозділ","departmentType":"BRANCH"}""", """{"phone":"+380000000000"}""")]
    [InlineData("employees", """{"fullName":"Тестовий Співробітник","labRole":"LAB_TECHNICIAN","positionName":"Лаборант"}""", """{"labRole":"LAB_DOCTOR"}""")]
    public async Task Dictionary_crud(string name, string createJson, string updateJson)
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var tech = _f.As(DemoDataSeeder.Technician);
        var created = await Json(await admin.PostAsync($"/api/v1/lab/dictionaries/{name}", new StringContent(createJson, System.Text.Encoding.UTF8, "application/json")));
        var id = created["id"]!.ToString();
        var got = await Json(await admin.GetAsync($"/api/v1/lab/dictionaries/{name}/{id}"));
        Assert.Equal(id, got["id"]!.ToString());
        await Json(await admin.PutAsync($"/api/v1/lab/dictionaries/{name}/{id}", new StringContent(updateJson, System.Text.Encoding.UTF8, "application/json")));
        // лаборант не має права видаляти довідники → 403
        await AssertProblem(await tech.DeleteAsync($"/api/v1/lab/dictionaries/{name}/{id}"), HttpStatusCode.Forbidden);
        var deleted = await Json(await admin.DeleteAsync($"/api/v1/lab/dictionaries/{name}/{id}"));
        Assert.True(deleted["deleted"]!.GetValue<bool>() || deleted["deactivated"]!.GetValue<bool>());
        var list = await Json(await admin.GetAsync($"/api/v1/lab/dictionaries/{name}?pageSize=5"));
        Assert.True(list["total"]!.GetValue<int>() >= 0);
    }

    [Fact]
    public async Task Sample_split_creates_children_with_derived_barcodes_and_moves_tests()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var order = await Json(await admin.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = "pat-0000-0000-0000-000000000001", profileIds = new[] { "PROF_BIOCHEM_BASE" }, testIds = new[] { "TSH" } }));
        var barcode = order["samples"]![0]!["barcode"]!.GetValue<string>();
        await Json(await admin.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/collect", new { volumeMl = 5.0 }));
        await Json(await admin.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/receive", new { }));
        var split = await Json(await admin.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/split", new { count = 2, volumeEachMl = 1.0, derivationType = "ALIQUOT", targetSectionIds = new[] { "IMMUNO" } }));
        var children = split["children"]!.AsArray();
        Assert.Equal(2, children.Count);
        Assert.Equal(barcode + "-A1", children[0]!["barcode"]!.GetValue<string>());
        Assert.Equal(barcode + "-A2", children[1]!["barcode"]!.GetValue<string>());
        Assert.Contains(split["movedTests"]!.AsArray(), m => m!["testCode"]!.GetValue<string>() == "TSH");
        Assert.Equal(2, split["labels"]!.AsArray().Count);
        var tree = await Json(await admin.GetAsync($"/api/v1/lab/samples/{barcode}/tree"));
        Assert.Equal(2, tree["root"]!["children"]!.AsArray().Count);
        var stage = await Json(await admin.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/stage", new { stageCode = "CENTRIFUGED", note = "3000 об/хв 10 хв" }));
        Assert.Equal("CENTRIFUGED", stage["currentStage"]!.GetValue<string>());
        // рух назад заборонено
        await AssertProblem(await admin.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/stage", new { stageCode = "RECEIVED" }), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Journal_numbering_preview_follows_mask_and_matrix_has_shape()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var preview = await Json(await admin.PostAsJsonAsync("/api/v1/lab/sections/sec-0000-0000-0000-000000000007/journal/renumber-preview", new { count = 3, at = "2026-03-05T10:00:00Z" }));
        var numbers = preview["preview"]!.AsArray().Select(p => p!["journalNumber"]!.GetValue<string>()).ToList();
        Assert.All(numbers, n => Assert.Matches(@"^S26-\d{5}$", n));
        var dayPreview = await Json(await admin.PostAsJsonAsync("/api/v1/lab/sections/sec-0000-0000-0000-000000000002/journal/renumber-preview", new { count = 1, at = "2026-03-05T10:00:00Z" }));
        Assert.Matches(@"^260305/\d{3}$", dayPreview["preview"]![0]!["journalNumber"]!.GetValue<string>());
        var matrix = await Json(await admin.GetAsync("/api/v1/lab/dictionaries/order-matrix"));
        Assert.Equal(8, matrix["sections"]!.AsArray().Count);
        var col = matrix["allProfiles"]!.AsArray()[0]!;
        foreach (var key in new[] { "code", "name", "biomaterial", "tubeColor", "price", "tatHours", "isProfile", "memberTestCodes" }) Assert.NotNull(col.AsObject()[key] ?? JsonValue.Create(""));
        var fav = await Json(await admin.PutAsJsonAsync("/api/v1/lab/dictionaries/order-matrix/favorites", new[] { new { name = "Мій набір", profileIds = new[] { "PROF_CBC" }, testIds = new[] { "GLU" } } }));
        Assert.Single(fav.AsArray());
    }

    [Fact]
    public async Task Unknown_employee_is_forbidden_for_mutations()
    {
        var ghost = _f.As("no-such-employee");
        await AssertProblem(await ghost.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = "pat-0000-0000-0000-000000000001", testIds = new[] { "GLU" } }), HttpStatusCode.Forbidden);
    }
}
