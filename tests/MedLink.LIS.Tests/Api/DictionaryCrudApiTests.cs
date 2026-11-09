// Універсальні довідники /dictionaries/{name}: CRUD, конфлікт коду, архів, імпорт із dryRun, ліміт, експорт, правила повторного тесту (дублі/цикли).
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class DictionaryCrudApiTests
{
    private const string Base = "/api/v1/lab/dictionaries";
    private readonly LisApiFactory _f;
    public DictionaryCrudApiTests(LisApiFactory f) => _f = f;

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

    private HttpClient Admin => _f.As(DemoDataSeeder.Admin);

    private static StringContent Text(string body) => new(body, Encoding.UTF8, "text/plain");

    private async Task<JsonNode> Import(string name, string body, bool dryRun, HttpClient? client = null) =>
        await Json(await (client ?? Admin).PostAsync($"{Base}/import?name={name}&dryRun={dryRun.ToString().ToLowerInvariant()}", Text(body)));

    // ------------------------------------------------------------------ CRUD: створення, конфлікт коду, архів, видалення
    [Theory]
    [InlineData("method-types", "T_MT_1")]
    [InlineData("biomaterials", "T_BM_1")]
    [InlineData("tube-types", "T_TT_1")]
    [InlineData("organisms", "T_ORG_1")]
    [InlineData("antibiotics", "T_ABX_1")]
    [InlineData("departments", "T_DEP_1")]
    public async Task Dictionary_crud_cycle(string name, string code)
    {
        var created = await Json(await Admin.PostAsJsonAsync($"{Base}/{name}", new { code, name = "Тестовий запис " + code, latinName = "Test sp.", commonName = "Тест" }));
        var id = created["id"]!.ToString();
        // конфлікт коду (відділення перевіряються окремо — перевірка в OrgDictionaryService)
        await AssertProblem(await Admin.PostAsJsonAsync($"{Base}/{name}", new { code, name = "Дубль" }), HttpStatusCode.Conflict);
        var updated = await Json(await Admin.PutAsJsonAsync($"{Base}/{name}/{id}", new { isActive = false }));
        Assert.False(updated["isActive"]!.GetValue<bool>());
        var list = await Json(await Admin.GetAsync($"{Base}/{name}?isActive=false&pageSize=500"));
        Assert.Contains(list["items"]!.AsArray(), x => x!["code"]?.ToString() == code);
        var del = await Json(await Admin.DeleteAsync($"{Base}/{name}/{id}"));
        Assert.True(del["deleted"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Technician_cannot_create_and_cannot_delete()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        await AssertProblem(await tech.PostAsJsonAsync($"{Base}/method-types", new { code = "T_MT_TECH", name = "Не повинно створитись" }), HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.OK, (await tech.GetAsync($"{Base}/method-types")).StatusCode); // читання — усім ролям
        var doctor = _f.As(DemoDataSeeder.Doctor);
        var created = await Json(await doctor.PostAsJsonAsync($"{Base}/method-types", new { code = "T_MT_DOC", name = "Лікар створює" }));
        await AssertProblem(await doctor.DeleteAsync($"{Base}/method-types/{created["id"]}"), HttpStatusCode.Forbidden); // видалення — лише адміністратор
    }

    [Fact]
    public async Task Unknown_dictionary_is_404()
    {
        await AssertProblem(await Admin.GetAsync($"{Base}/nonexistent"), HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------ імпорт
    [Fact]
    public async Task Import_dry_run_reports_create_update_skip_error_and_writes_nothing()
    {
        await Json(await Admin.PostAsJsonAsync($"{Base}/method-types", new { code = "T_IMP_A", name = "Стара назва" }));
        var csv = "code;name;isActive\r\nT_IMP_A;Нова назва;true\r\nT_IMP_B;Створюваний;true\r\nT_IMP_B;Дубль у файлі;true\r\n;Без коду;true\r\nT_IMP_A;Нова назва;true\r\n";
        var dry = await Import("method-types", csv, true);
        Assert.True(dry["dryRun"]!.GetValue<bool>());
        Assert.Equal(1, dry["create"]!.GetValue<int>());
        Assert.Equal(1, dry["update"]!.GetValue<int>());
        Assert.Equal(3, dry["skip"]!.GetValue<int>()); // дубль у файлі, без коду, дубль T_IMP_A
        Assert.All(dry["rows"]!.AsArray().Where(r => r!["action"]!.ToString() == "SKIP"), r => Assert.False(string.IsNullOrWhiteSpace(r!["reason"]!.ToString())));
        // dryRun нічого не записав
        var after = await Json(await Admin.GetAsync($"{Base}/method-types/T_IMP_A"));
        Assert.Equal("Стара назва", after["name"]!.ToString());
        await AssertProblem(await Admin.GetAsync($"{Base}/method-types/T_IMP_B"), HttpStatusCode.NotFound);

        var real = await Import("method-types", csv, false);
        Assert.Equal(1, real["created"]!.GetValue<int>());
        Assert.Equal(1, real["updated"]!.GetValue<int>());
        Assert.Equal("Нова назва", (await Json(await Admin.GetAsync($"{Base}/method-types/T_IMP_A")))["name"]!.ToString());
        // повторний імпорт: усе без змін
        var again = await Import("method-types", "code;name\r\nT_IMP_A;Нова назва\r\nT_IMP_B;Створюваний\r\n", false);
        Assert.Equal(0, again["create"]!.GetValue<int>());
        Assert.Equal(0, again["update"]!.GetValue<int>());
        Assert.Equal(2, again["skip"]!.GetValue<int>());
    }

    [Fact]
    public async Task Import_rejects_more_than_5000_rows_with_clear_message()
    {
        var sb = new StringBuilder("code;name\r\n");
        for (var i = 0; i < 5001; i++) sb.Append($"X{i};Рядок {i}\r\n");
        var r = await Admin.PostAsync($"{Base}/import?name=method-types&dryRun=true", Text(sb.ToString()));
        await AssertProblem(r, HttpStatusCode.BadRequest, "5000");
    }

    [Fact]
    public async Task Import_handles_quoted_csv_values_and_admin_only()
    {
        var csv = "code;name\r\nT_Q_1;\"Назва; з крапкою з комою і \"\"лапками\"\"\"\r\n";
        await Import("method-types", csv, false);
        Assert.Equal("Назва; з крапкою з комою і \"лапками\"", (await Json(await Admin.GetAsync($"{Base}/method-types/T_Q_1")))["name"]!.ToString());
        var doctor = _f.As(DemoDataSeeder.Doctor);
        await AssertProblem(await doctor.PostAsync($"{Base}/import?name=method-types&dryRun=true", Text(csv)), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Import_reflex_rules_by_key_does_not_create_duplicates()
    {
        var csv = "triggerTestCode;conditionOperator;thresholdValue;reflexTestCode;description\r\nGLU;>;11;HBA1C;Імпорт перший\r\n";
        var first = await Import("reflex-rules", csv, false);
        Assert.Equal(1, first["create"]!.GetValue<int>() + first["update"]!.GetValue<int>() + first["skip"]!.GetValue<int>());
        var again = await Import("reflex-rules", csv.Replace("Імпорт перший", "Імпорт оновлений"), false);
        Assert.Equal(0, again["create"]!.GetValue<int>());
        var rules = await Json(await Admin.GetAsync($"{Base}/reflex-rules?pageSize=500"));
        Assert.Single(rules["items"]!.AsArray().Where(r => r!["triggerTestCode"]!.ToString() == "GLU" && r["reflexTestCode"]!.ToString() == "HBA1C" && r["thresholdValue"]!.GetValue<double>() == 11));
    }

    [Fact]
    public async Task Import_eucast_by_codes_updates_existing_pair()
    {
        var organisms = await Json(await Admin.GetAsync($"{Base}/organisms?pageSize=500"));
        var antibiotics = await Json(await Admin.GetAsync($"{Base}/antibiotics?pageSize=500"));
        var org = organisms["items"]![0]!["code"]!.ToString(); var abx = antibiotics["items"]![antibiotics["items"]!.AsArray().Count - 1]!["code"]!.ToString();
        var csv1 = $"organismCode;antibioticCode;eucastVersion;micSusceptibleLe;micResistantGt\r\n{org};{abx};vTEST_1;1;2\r\n";
        var one = await Import("eucast-breakpoints", csv1, false);
        Assert.Equal(1, one["create"]!.GetValue<int>());
        var two = await Import("eucast-breakpoints", csv1.Replace(";1;2", ";0,5;4"), false);
        Assert.Equal(0, two["create"]!.GetValue<int>());
        Assert.Equal(1, two["update"]!.GetValue<int>());
        var all = await Json(await Admin.GetAsync($"{Base}/eucast-breakpoints?search=vTEST_1&pageSize=500"));
        var row = Assert.Single(all["items"]!.AsArray());
        Assert.Equal(4, row!["micResistantGt"]!.GetValue<double>());
    }

    [Fact]
    public async Task Import_employees_by_email_does_not_duplicate()
    {
        var csv = "fullName;labRole;email;position\r\nІмпортов Іван Петрович;LAB_TECHNICIAN;import.ivan@example.test;Лаборант\r\n";
        var one = await Import("employees", csv, false);
        Assert.Equal(1, one["create"]!.GetValue<int>());
        var two = await Import("employees", csv.Replace("Лаборант", "Старший лаборант"), false);
        Assert.Equal(0, two["create"]!.GetValue<int>());
        Assert.Equal(1, two["update"]!.GetValue<int>());
        var list = await Json(await Admin.GetAsync($"{Base}/employees?search=import.ivan&pageSize=500"));
        Assert.Single(list["items"]!.AsArray());
    }

    // ------------------------------------------------------------------ експорт
    [Theory]
    [InlineData("biomaterials")]
    [InlineData("tests")]
    [InlineData("profiles")]
    [InlineData("eucast-breakpoints")]
    [InlineData("employees")]
    [InlineData("reflex-rules")]
    public async Task Export_csv_and_json_for_every_role(string name)
    {
        var csv = await _f.As(DemoDataSeeder.Technician).GetAsync($"{Base}/{name}/export?format=csv");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Contains("csv", csv.Content.Headers.ContentType!.MediaType);
        var bytes = await csv.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray()); // BOM для Excel
        var text = Encoding.UTF8.GetString(bytes);
        Assert.True(text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length >= 2, "є заголовок і хоча б один запис");
        var json = JsonNode.Parse(await (await Admin.GetAsync($"{Base}/{name}/export?format=json")).Content.ReadAsStringAsync())!.AsArray();
        Assert.NotEmpty(json);
        await AssertProblem(await Admin.GetAsync($"{Base}/{name}/export?format=xml"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Export_then_import_round_trip_is_idempotent_for_tests()
    {
        var csv = await (await Admin.GetAsync($"{Base}/tests/export?format=csv")).Content.ReadAsStringAsync();
        var rep = await Import("tests", csv.TrimStart('﻿'), true);
        Assert.Equal(0, rep["create"]!.GetValue<int>());
        Assert.Equal(0, rep["error"]!.GetValue<int>());
    }

    // ------------------------------------------------------------------ правила повторного тесту
    [Fact]
    public async Task Reflex_rules_reject_self_duplicate_and_cycles()
    {
        await AssertProblem(await Admin.PostAsJsonAsync($"{Base}/reflex-rules", new { triggerTestCode = "K", conditionOperator = ">", thresholdValue = 5.5, reflexTestCode = "K" }), HttpStatusCode.BadRequest, "сам себе");
        var a = await Json(await Admin.PostAsJsonAsync($"{Base}/reflex-rules", new { triggerTestCode = "NA", conditionOperator = ">", thresholdValue = 150.0, reflexTestCode = "K", description = "NA → K" }));
        await AssertProblem(await Admin.PostAsJsonAsync($"{Base}/reflex-rules", new { triggerTestCode = "NA", conditionOperator = ">", thresholdValue = 150.0, reflexTestCode = "K" }), HttpStatusCode.Conflict, "вже існує");
        await AssertProblem(await Admin.PostAsJsonAsync($"{Base}/reflex-rules", new { triggerTestCode = "K", conditionOperator = ">", thresholdValue = 6.0, reflexTestCode = "NA" }), HttpStatusCode.Conflict, "цикл");
        await AssertProblem(await Admin.PostAsJsonAsync($"{Base}/reflex-rules", new { triggerTestCode = "K", conditionOperator = ">", reflexTestCode = "CL" }), HttpStatusCode.BadRequest, "поріг");
        await Admin.DeleteAsync($"{Base}/reflex-rules/{a["id"]}");
    }

    [Fact]
    public async Task Eucast_breakpoint_archive_and_conflict()
    {
        var organisms = await Json(await Admin.GetAsync($"{Base}/organisms?pageSize=500"));
        var antibiotics = await Json(await Admin.GetAsync($"{Base}/antibiotics?pageSize=500"));
        var orgId = organisms["items"]![1]!["id"]!.GetValue<int>(); var abxId = antibiotics["items"]![1]!["id"]!.GetValue<int>();
        var body = new { organismId = orgId, antibioticId = abxId, eucastVersion = "vARCH_1", micSusceptibleLe = 1.0, micResistantGt = 2.0 };
        var created = await Json(await Admin.PostAsJsonAsync($"{Base}/eucast-breakpoints", body));
        await AssertProblem(await Admin.PostAsJsonAsync($"{Base}/eucast-breakpoints", body), HttpStatusCode.Conflict);
        await Json(await Admin.PutAsJsonAsync($"{Base}/eucast-breakpoints/{created["id"]}", new { isActive = false }));
        var active = await Json(await Admin.GetAsync($"{Base}/eucast-breakpoints?isActive=true&search=vARCH_1&pageSize=500"));
        Assert.Empty(active["items"]!.AsArray());
        var archived = await Json(await Admin.GetAsync($"{Base}/eucast-breakpoints?isActive=false&search=vARCH_1&pageSize=500"));
        Assert.Single(archived["items"]!.AsArray());
    }
}
