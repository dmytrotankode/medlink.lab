// Цілісність біохімічного каталогу та довідників-сутностей: одиниці виміру (перерахунок), вікові групи, причини браку та відмови,
// ідемпотентність стартового набору на наявних базах.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class BiochemCatalogTests
{
    private const string Base = "/api/v1/lab";
    private const string Patient = "0b000000-0000-0000-0000-000000000003";
    private readonly LisApiFactory _f;
    public BiochemCatalogTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static async Task AssertProblem(HttpResponseMessage r, HttpStatusCode status)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"очікувалось {(int)status}, отримано {(int)r.StatusCode}: {text}");
    }

    private async Task<List<JsonNode>> ListAsync(string name)
    {
        var res = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"{Base}/dictionaries/{name}?pageSize=500"));
        return res["items"]!.AsArray().Select(x => x!).ToList();
    }

    /// <summary>Показники каталогу без тимчасових, створених іншими тестами (код починається з ZZ).</summary>
    private async Task<List<JsonNode>> CatalogTestsAsync() => (await ListAsync("tests")).Where(t => { var c = t["code"]!.GetValue<string>(); return !c.StartsWith("ZZ") && !System.Text.RegularExpressions.Regex.IsMatch(c, "^(AV|DT)[0-9A-F]{5,6}$"); }).ToList();

    // ------------------------------------------------------------------ цілісність каталогу
    /// <summary>Показники, для яких немає надійно визначеного коду LOINC (залишені порожніми свідомо).</summary>
    private static readonly string[] NoLoinc = { "PTI", "ICA", "HCO3", "ASLO", "AMYL_P", "CHOL_NHDL", "ATH_IDX", "HOMA" };
    /// <summary>Категорії, результат яких — звіт/текст (гістологія, цитологія, посів): норми та одиниці не застосовуються.</summary>
    private static readonly string[] ReportCategories = { "PATHOLOGY", "CYTOLOGY", "MICRO" };

    [Fact]
    public async Task Catalog_every_test_has_unit_method_tube_norm_and_codes_are_unique()
    {
        var tests = await CatalogTestsAsync();
        var units = (await ListAsync("units")).ToDictionary(u => u["id"]!.GetValue<int>());
        var layers = (await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"{Base}/norms/combinations"))).AsArray();
        var layersByTest = layers.GroupBy(l => l!["testCode"]!.GetValue<string>()).ToDictionary(g => g.Key, g => g.ToList());

        Assert.True(tests.Count >= 110, $"у каталозі {tests.Count} показників");
        var codes = tests.Select(t => t["code"]!.GetValue<string>().ToUpperInvariant()).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());

        var problems = new List<string>();
        foreach (var t in tests)
        {
            var code = t["code"]!.GetValue<string>();
            var category = t["category"]!.GetValue<string>();
            var unitId = t["unitId"]?.GetValue<int>();
            if (unitId == null || !units.TryGetValue(unitId.Value, out var unit)) problems.Add($"{code}: немає одиниці з довідника");
            else if ((t["unit"]?.GetValue<string>() ?? "") != (unit["name"]?.GetValue<string>() ?? "")) problems.Add($"{code}: текст одиниці не збігається з довідником");
            if (t["methodId"] == null) problems.Add($"{code}: немає методики");
            if (t["tubeTypeId"] == null) problems.Add($"{code}: немає пробірки");
            if (t["biomaterialTypeId"] == null) problems.Add($"{code}: немає біоматеріалу");
            if (!ReportCategories.Contains(category) && !layersByTest.ContainsKey(code)) problems.Add($"{code}: немає жодної норми");
            if (category == "BIOCHEM" && string.IsNullOrWhiteSpace(t["loincCode"]?.GetValue<string>()) && !NoLoinc.Contains(code)) problems.Add($"{code}: немає LOINC");
            var methodCode = t["methodCode"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(methodCode) && layersByTest.TryGetValue(code, out var ls) && !ls.Any(l => l!["methodCode"]?.GetValue<string>() == methodCode))
                problems.Add($"{code}: жодна норма не відповідає коду методики {methodCode} (каскад їх проігнорує)");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task Catalog_biochemistry_contains_required_analytes_and_norms_are_marked_as_starter()
    {
        var tests = (await ListAsync("tests")).Select(t => t["code"]!.GetValue<string>()).ToHashSet();
        foreach (var code in new[] { "MG", "PHOS", "LDH", "CK", "CKMB", "TRF", "RF", "ASLO", "CYSC", "INS", "CPEP", "TIBC", "GLU_F", "BIL_IND", "U_ALB", "U_PRO24" })
            Assert.Contains(code, tests);

        var layers = (await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"{Base}/norms/combinations"))).AsArray();
        var catalogCodes = (await CatalogTestsAsync()).Select(t => t["code"]!.GetValue<string>()).ToHashSet();
        var unmarked = layers.Where(l => catalogCodes.Contains(l!["testCode"]!.GetValue<string>()) && !(l!["source"]?.GetValue<string>() ?? "").Contains("потребує затвердження")).Select(l => l!["id"]!.GetValue<string>()).ToList();
        Assert.True(unmarked.Count == 0, $"норми без позначки «потребує затвердження»: {unmarked.Count}");
        // кожна норма має коректні межі
        foreach (var l in layers)
        {
            var lo = l!["normLow"]?.GetValue<double>(); var hi = l["normHigh"]?.GetValue<double>();
            if (lo.HasValue && hi.HasValue) Assert.True(lo <= hi, $"норма {l["id"]}: нижня межа більша за верхню");
            Assert.True(l["ageFrom"]!.GetValue<double>() <= l["ageTo"]!.GetValue<double>(), $"норма {l["id"]}: вік від > до");
        }
    }

    [Fact]
    public async Task Catalog_tubes_biomaterials_and_methods_are_consistent()
    {
        var tubes = await ListAsync("tube-types");
        Assert.Contains(tubes, t => t["code"]!.GetValue<string>() == "FLUORIDE");
        Assert.Contains(tubes, t => t["code"]!.GetValue<string>() == "K2_EDTA"); // лілова ЕДТА вже була
        foreach (var name in new[] { "tube-types", "biomaterials", "method-types", "units", "age-groups", "rejection-reasons", "cancel-reasons" })
        {
            var codes = (await ListAsync(name)).Select(x => x["code"]!.GetValue<string>().ToUpperInvariant()).ToList();
            Assert.True(codes.Count == codes.Distinct().Count(), $"дублі кодів у довіднику {name}");
        }
        var tests = await CatalogTestsAsync();
        var byCode = tests.ToDictionary(t => t["code"]!.GetValue<string>());
        var tubeById = tubes.ToDictionary(t => t["id"]!.GetValue<int>(), t => t["code"]!.GetValue<string>());
        Assert.Equal("FLUORIDE", tubeById[byCode["GLU_F"]["tubeTypeId"]!.GetValue<int>()]);
        Assert.Equal("URINE_24H", tubeById[byCode["U_PRO24"]["tubeTypeId"]!.GetValue<int>()]);
        Assert.Equal("SERUM_GEL", tubeById[byCode["MG"]["tubeTypeId"]!.GetValue<int>()]);
        // сечові показники — у контейнері для сечі, біохімія сироватки — не в контейнері для сечі
        foreach (var t in tests.Where(t => t["category"]!.GetValue<string>() == "BIOCHEM" && t["biomaterialType"]?["code"]?.GetValue<string>() == "BM_04"))
        {
            var tid = t["tubeTypeId"]?.GetValue<int>();
            Assert.True(tid.HasValue && tubeById.ContainsKey(tid.Value), $"{t["code"]}: пробірка не визначена");
            Assert.StartsWith("SERUM", tubeById[tid!.Value]);
        }
    }

    // ------------------------------------------------------------------ норми за віком та статтю
    private async Task<JsonNode> CascadeAsync(string testCode, string gender, double age, string ageUnit = "YEARS") =>
        await Json(await _f.As(DemoDataSeeder.Admin).PostAsJsonAsync($"{Base}/norms/resolve-cascade", new { testCode, gender, age, ageUnit }));

    [Theory]
    [InlineData("CREAT", "M", 3.0, "YEARS", 18.0, 35.0)]     // діти 1–3 роки
    [InlineData("CREAT", "F", 20.0, "DAYS", 27.0, 88.0)]     // новонароджена
    [InlineData("CREAT", "M", 40.0, "YEARS", 62.0, 115.0)]   // дорослий чоловік — норма, що була раніше
    [InlineData("BIL_TOT", "F", 3.0, "DAYS", 0.0, 205.0)]    // новонароджені 0–6 діб
    [InlineData("CK", "M", 30.0, "YEARS", 39.0, 308.0)]
    [InlineData("CK", "F", 30.0, "YEARS", 26.0, 192.0)]
    [InlineData("MG", "F", 8.0, "MONTHS", 0.7, 0.95)]
    [InlineData("GLU_F", "M", 5.0, "DAYS", 2.8, 4.4)]
    [InlineData("PHOS", "M", 14.0, "YEARS", 0.95, 1.75)]
    [InlineData("ALP", "F", 15.0, "YEARS", 50.0, 254.0)]
    public async Task Cascade_picks_pediatric_and_sex_specific_norms(string test, string gender, double age, string unit, double low, double high)
    {
        var r = await CascadeAsync(test, gender, age, unit);
        Assert.True(r["found"]!.GetValue<bool>());
        Assert.Equal(low, r["normLow"]!.GetValue<double>(), 3);
        Assert.Equal(high, r["normHigh"]!.GetValue<double>(), 3);
    }

    // ------------------------------------------------------------------ одиниці виміру
    [Fact]
    public async Task Units_seeded_with_keys_and_linked_to_tests_and_layers()
    {
        var units = await ListAsync("units");
        foreach (var code in new[] { "MMOL_L", "MG_DL", "UMOL_L", "G_L", "U_L", "PERCENT", "NONE" })
            Assert.Contains(units, u => u["code"]!.GetValue<string>() == code);
        var layers = (await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"{Base}/norms/combinations"))).AsArray();
        Assert.All(layers, l => Assert.NotNull(l!["unitId"]));
        // pH і питома вага (раніше порожній текст) прив'язані до «без одиниць»
        var none = units.Single(u => u["code"]!.GetValue<string>() == "NONE")["id"]!.GetValue<int>();
        var ph = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"{Base}/dictionaries/tests/U_PH"));
        Assert.Equal(none, ph["unitId"]!.GetValue<int>());
        Assert.Equal("", ph["unit"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("GLU", 100.0, "MG_DL", "MMOL_L", 5.55)]
    [InlineData("GLU", 5.55, "MMOL_L", "MG_DL", 100.0)]          // зворотний напрям
    [InlineData("CREAT", 1.0, "MG_DL", "UMOL_L", 88.42)]
    [InlineData("CHOL", 200.0, "MG_DL", "MMOL_L", 5.172)]
    [InlineData("BIL_TOT", 1.0, "MG_DL", "UMOL_L", 17.104)]
    [InlineData("UREA", 30.0, "MG_DL", "MMOL_L", 4.995)]
    [InlineData("CA", 10.0, "MG_DL", "MMOL_L", 2.495)]
    [InlineData("HBA1C", 6.5, "PERCENT", "MMOL_MOL", 47.5385)]  // зі зсувом
    [InlineData("HBA1C", 48.0, "MMOL_MOL", "PERCENT", 6.5352)]
    [InlineData("ALB", 4.0, "G_DL", "G_L", 40.0)]
    public async Task Units_convert_between_traditional_and_si(string test, double value, string from, string to, double expected)
    {
        var r = await Json(await _f.CreateClient().GetAsync($"{Base}/dictionaries/units/convert?testCode={test}&value={value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&from={from}&to={to}"));
        Assert.Equal(expected, r["result"]!.GetValue<double>(), 2);
    }

    [Fact]
    public async Task Units_convert_without_rule_is_validation_error_and_conversions_listed()
    {
        await AssertProblem(await _f.CreateClient().GetAsync($"{Base}/dictionaries/units/convert?testCode=GLU&value=1&from=UMOL_L&to=U_L"), HttpStatusCode.BadRequest);
        await AssertProblem(await _f.CreateClient().GetAsync($"{Base}/dictionaries/units/convert?testCode=NO_SUCH&value=1&from=MG_DL&to=MMOL_L"), HttpStatusCode.NotFound);
        var list = (await Json(await _f.CreateClient().GetAsync($"{Base}/dictionaries/units/conversions?testCode=GLU"))).AsArray();
        Assert.Contains(list, c => c!["from"]!.GetValue<string>() == "MG_DL" && c["to"]!.GetValue<string>() == "MMOL_L");
    }

    [Fact]
    public async Task Units_crud_conflicts_and_test_unit_sync()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var created = await Json(await admin.PostAsJsonAsync($"{Base}/dictionaries/units", new { code = "t_unit_x", name = "тест/х", dimension = "OTHER", systemKind = "OTHER" }));
        var unitId = created["id"]!.GetValue<int>();
        Assert.Equal("T_UNIT_X", created["code"]!.GetValue<string>());
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/dictionaries/units", new { code = "T_UNIT_X", name = "інша" }), HttpStatusCode.Conflict);
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/dictionaries/units", new { code = "T_UNIT_Y", name = "тест/х" }), HttpStatusCode.Conflict);

        // показник з unitId отримує текст одиниці з довідника
        var test = await Json(await admin.PostAsJsonAsync($"{Base}/dictionaries/tests", new { code = "ZZUNIT", name = "Показник одиниці", unitId, biomaterialTypeId = 4, category = "BIOCHEM" }));
        Assert.Equal("тест/х", test["unit"]!.GetValue<string>());
        // вільний текст нової одиниці не губиться: створюється запис довідника
        var upd = await Json(await admin.PutAsJsonAsync($"{Base}/dictionaries/tests/ZZUNIT", new { unit = "умовн.од." }));
        Assert.Equal("умовн.од.", upd["unit"]!.GetValue<string>());
        Assert.NotEqual(unitId, upd["unitId"]!.GetValue<int>());
        Assert.Contains(await ListAsync("units"), u => u["name"]!.GetValue<string>() == "умовн.од.");
        // повернення за unitId
        var back = await Json(await admin.PutAsJsonAsync($"{Base}/dictionaries/tests/ZZUNIT", new { unitId }));
        Assert.Equal("тест/х", back["unit"]!.GetValue<string>());
        // одиниця використовується → деактивація замість видалення
        var del = await Json(await admin.DeleteAsync($"{Base}/dictionaries/units/{unitId}"));
        Assert.True(del["deactivated"]!.GetValue<bool>());
        // ролі: лаборант не створює одиниці
        await AssertProblem(await _f.As(DemoDataSeeder.Technician).PostAsJsonAsync($"{Base}/dictionaries/units", new { code = "T_ZZ", name = "зз" }), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unit_conversion_rules_crud_validates_pairs()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var units = await ListAsync("units");
        int Id(string code) => units.Single(u => u["code"]!.GetValue<string>() == code)["id"]!.GetValue<int>();
        var rule = await Json(await admin.PostAsJsonAsync($"{Base}/dictionaries/unit-conversions", new { testCode = "PSA", fromUnitId = Id("NG_ML"), toUnitId = Id("UG_L"), factor = 1.0, note = "нг/мл = мкг/л" }));
        Assert.NotNull(rule["id"]);
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/dictionaries/unit-conversions", new { testCode = "PSA", fromUnitId = Id("NG_ML"), toUnitId = Id("UG_L"), factor = 1.0 }), HttpStatusCode.Conflict);
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/dictionaries/unit-conversions", new { testCode = "PSA", fromUnitId = Id("NG_ML"), toUnitId = Id("NG_ML"), factor = 1.0 }), HttpStatusCode.BadRequest);
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/dictionaries/unit-conversions", new { testCode = "PSA", fromUnitId = Id("NG_ML"), toUnitId = Id("MG_L"), factor = 0 }), HttpStatusCode.BadRequest);
        var r = await Json(await _f.CreateClient().GetAsync($"{Base}/dictionaries/units/convert?testCode=PSA&value=4&from=NG_ML&to=UG_L"));
        Assert.Equal(4.0, r["result"]!.GetValue<double>(), 3);
    }

    // ------------------------------------------------------------------ вікові групи
    [Fact]
    public async Task Age_groups_seeded_and_crud()
    {
        var groups = await ListAsync("age-groups");
        Assert.True(groups.Count >= 10);
        var nb = groups.Single(g => g["code"]!.GetValue<string>() == "NB_0_6D");
        Assert.Equal("DAYS", nb["ageUnit"]!.GetValue<string>());
        // у кожної дитячої норми з групою межі віку збігаються з межами групи
        var layers = (await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"{Base}/norms/combinations"))).AsArray().Where(l => l!["ageGroupId"] != null).ToList();
        Assert.True(layers.Count > 50);
        var byId = groups.ToDictionary(g => g["id"]!.GetValue<int>());
        foreach (var l in layers)
        {
            var g = byId[l!["ageGroupId"]!.GetValue<int>()];
            Assert.Equal(g["ageFrom"]!.GetValue<double>(), l["ageFrom"]!.GetValue<double>());
            Assert.Equal(g["ageTo"]!.GetValue<double>(), l["ageTo"]!.GetValue<double>());
            Assert.Equal(g["ageUnit"]!.GetValue<string>(), l["ageUnit"]!.GetValue<string>());
        }
        var admin = _f.As(DemoDataSeeder.Admin);
        var created = await Json(await admin.PostAsJsonAsync($"{Base}/dictionaries/age-groups", new { code = "t_group", name = "Тестова група", ageFrom = 2, ageTo = 5, ageUnit = "YEARS" }));
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/dictionaries/age-groups", new { code = "T_GROUP", name = "ще", ageFrom = 1, ageTo = 2, ageUnit = "YEARS" }), HttpStatusCode.Conflict);
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/dictionaries/age-groups", new { code = "T_BAD", name = "погана", ageFrom = 9, ageTo = 2, ageUnit = "YEARS" }), HttpStatusCode.BadRequest);
        var del = await Json(await admin.DeleteAsync($"{Base}/dictionaries/age-groups/{created["id"]}"));
        Assert.True(del["deleted"]!.GetValue<bool>());
    }

    // ------------------------------------------------------------------ причини браку та відмови
    private async Task<string> CollectedTubeAsync()
    {
        var order = await Json(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync($"{Base}/orders",
            new { patientId = Patient, doctorId = DemoDataSeeder.Doctor, departmentId = DemoDataSeeder.CollectionPoint, profileIds = new[] { "PROF_BIOCHEM_BASE" } }));
        var barcode = order["samples"]![0]!["barcode"]!.GetValue<string>();
        await Json(await _f.As(DemoDataSeeder.Nurse).PostAsJsonAsync($"{Base}/samples/{barcode}/collect",
            new { checklist = new { idVerified = true, fasting = true, orderOfDraw = true, mixing = true }, volumeMl = 4.0 }));
        return barcode;
    }

    [Fact]
    public async Task Reasons_seeded_with_categories()
    {
        var rej = await ListAsync("rejection-reasons");
        var can = await ListAsync("cancel-reasons");
        Assert.True(rej.Count >= 15 && can.Count >= 10);
        Assert.Contains(rej, r => r["code"]!.GetValue<string>() == "HEMOLYSIS" && r["category"]!.GetValue<string>() == "SAMPLE_QUALITY");
        Assert.Contains(can, r => r["code"]!.GetValue<string>() == "PATIENT_REFUSED" && r["category"]!.GetValue<string>() == "PATIENT");
        Assert.All(rej.Concat(can), r => Assert.False(string.IsNullOrWhiteSpace(r["name"]!.GetValue<string>())));
    }

    [Theory]
    [InlineData("HEMOLYSIS", "Гемоліз", "HEMOLYSIS")]                 // за кодом
    [InlineData("згусток у пробірці", "Згусток у пробірці", "CLOT")]   // сумісний текст, що збігається з назвою
    [InlineData("Особлива причина лабораторії", "Особлива причина лабораторії", null)] // довільний текст приймається як є
    public async Task Sample_reject_accepts_code_and_compatible_text(string input, string expectedText, string? expectedCode)
    {
        var barcode = await CollectedTubeAsync();
        await Json(await _f.As(DemoDataSeeder.Admin).PostAsJsonAsync($"{Base}/samples/{barcode}/reject", new { reason = input, createRepeatOrder = false }));
        var sample = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"{Base}/samples/{barcode}"));
        Assert.Equal(expectedText, sample["rejectReason"]!.GetValue<string>());
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var row = await db.Samples.AsNoTracking().SingleAsync(s => s.Barcode == barcode);
        Assert.Equal(expectedCode, row.RejectReasonCode);
    }

    [Fact]
    public async Task Order_cancel_accepts_code_and_free_text()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        async Task<string> NewOrder() => (await Json(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync($"{Base}/orders",
            new { patientId = Patient, doctorId = DemoDataSeeder.Doctor, departmentId = DemoDataSeeder.CollectionPoint, testIds = new[] { "K" } })))["id"]!.GetValue<string>();
        var o1 = await NewOrder();
        await Json(await admin.PostAsJsonAsync($"{Base}/orders/{o1}/cancel", new { reason = "PATIENT_REFUSED" }));
        var o2 = await NewOrder();
        await Json(await admin.PostAsJsonAsync($"{Base}/orders/{o2}/cancel", new { reason = "Пацієнт передумав" }));
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var a = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == o1);
        var b = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == o2);
        Assert.Equal(("Відмова пацієнта", "PATIENT_REFUSED"), (a.CancelReason, a.CancelReasonCode));
        Assert.Equal(("Пацієнт передумав", (string?)null), (b.CancelReason, b.CancelReasonCode));
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/orders/{o1}/cancel", new { reason = "" }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reasons_crud_uppercases_code_blocks_duplicates_and_soft_deletes_used()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var created = await Json(await admin.PostAsJsonAsync($"{Base}/dictionaries/rejection-reasons", new { code = "t_reason", name = "Тестова причина", category = "OTHER" }));
        Assert.Equal("T_REASON", created["code"]!.GetValue<string>());
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/dictionaries/rejection-reasons", new { code = "T_REASON", name = "дубль" }), HttpStatusCode.Conflict);
        await Json(await admin.PutAsJsonAsync($"{Base}/dictionaries/rejection-reasons/{created["id"]}", new { name = "Тестова причина 2" }));
        // використана у відбракуванні → лише деактивація
        var barcode = await CollectedTubeAsync();
        await Json(await _f.As(DemoDataSeeder.Admin).PostAsJsonAsync($"{Base}/samples/{barcode}/reject", new { reason = "T_REASON", createRepeatOrder = false }));
        var del = await Json(await admin.DeleteAsync($"{Base}/dictionaries/rejection-reasons/{created["id"]}"));
        Assert.True(del["deactivated"]!.GetValue<bool>());
        // неактивна причина більше не підставляється за кодом, але текст приймається як є
        var b2 = await CollectedTubeAsync();
        await Json(await _f.As(DemoDataSeeder.Admin).PostAsJsonAsync($"{Base}/samples/{b2}/reject", new { reason = "T_REASON", createRepeatOrder = false }));
        Assert.Equal("T_REASON", (await Json(await admin.GetAsync($"{Base}/samples/{b2}")))["rejectReason"]!.GetValue<string>());

        var c = await Json(await admin.PostAsJsonAsync($"{Base}/dictionaries/cancel-reasons", new { code = "t_cancel", name = "Тест скасування", category = "ADMIN" }));
        var d = await Json(await admin.DeleteAsync($"{Base}/dictionaries/cancel-reasons/{c["id"]}"));
        Assert.True(d["deleted"]!.GetValue<bool>());
    }

    // ------------------------------------------------------------------ імпорт нових довідників
    [Fact]
    public async Task Reasons_import_dry_run_marks_create_and_update()
    {
        var csv = "code;name;category\nHEMOLYSIS;Гемоліз (уточнено);SAMPLE_QUALITY\nIMP_NEW;Імпортована причина;OTHER\n";
        var res = await _f.As(DemoDataSeeder.Admin).PostAsync($"{Base}/dictionaries/import?name=rejection-reasons&dryRun=true", new StringContent(csv, System.Text.Encoding.UTF8, "text/csv"));
        var json = await Json(res);
        var actions = json["rows"]!.AsArray().ToDictionary(r => r!["key"]!.GetValue<string>(), r => r!["action"]!.GetValue<string>());
        Assert.Equal("UPDATE", actions["HEMOLYSIS"]);
        Assert.Equal("CREATE", actions["IMP_NEW"]);
    }
}

/// <summary>Стартовий набір на наявних базах: повторний запуск нічого не змінює, ручні зміни адміністратора не перезаписуються й не повертаються.</summary>
[Collection("api")]
public class BiochemSeedIdempotencyTests
{
    private readonly LisApiFactory _f;
    public BiochemSeedIdempotencyTests(LisApiFactory f) => _f = f;

    private async Task<(int tests, int layers, int units, int groups, int reasons, int conversions)> CountsAsync(LisDbContext db) =>
        (await db.Tests.CountAsync(), await db.ReferenceLayers.CountAsync(), await db.Units.CountAsync(), await db.AgeGroups.CountAsync(),
         await db.RejectionReasons.CountAsync() + await db.CancelReasons.CountAsync(), await db.UnitConversions.CountAsync());

    [Fact]
    public async Task Repeated_seed_is_noop_and_legacy_database_keeps_manual_changes()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<BiochemCatalogSeeder>();
        var baseline = await CountsAsync(db);
        Assert.Equal(2, await db.SeedMarkers.CountAsync());
        var orig = (await db.Tests.AsNoTracking().Where(t => t.Code == "CREAT" || t.Code == "MG").ToListAsync()).ToDictionary(t => t.Code);
        var origLayer = await db.ReferenceLayers.AsNoTracking().FirstAsync(l => l.TestCode == "MG" && l.LayerType == "BASELINE");
        var origFl = (await db.Units.AsNoTracking().SingleAsync(u => u.Code == "FL")).Name;
        try
        {

        await seeder.SeedAsync();   // 1) повторний запуск з маркерами
        Assert.Equal(baseline, await CountsAsync(db));

        // 2) «стара» база без маркерів + ручні зміни адміністратора
        var creat = await db.Tests.SingleAsync(t => t.Code == "CREAT");
        var mg = await db.Tests.SingleAsync(t => t.Code == "MG");
        var methodBefore = creat.MethodId;
        creat.MethodId = 2;                       // адміністратор обрав іншу методику
        mg.LoincCode = "CUSTOM-1";                // власний код
        mg.Name = "Магній (змінена назва)";
        var layer = await db.ReferenceLayers.FirstAsync(l => l.TestCode == "MG" && l.LayerType == "BASELINE");
        layer.NormHigh = 1.2; layer.Source = "Затверджено лікарем-лаборантом"; // затверджена норма
        var removed = await db.Units.SingleAsync(u => u.Code == "FL");
        removed.Name = "фл (змінено)";
        db.SeedMarkers.RemoveRange(db.SeedMarkers);
        await db.SaveChangesAsync();

        await seeder.SeedAsync();
        db.ChangeTracker.Clear();
        var after = await CountsAsync(db);
        Assert.Equal(baseline.tests, after.tests);
        Assert.Equal(baseline.layers, after.layers);
        Assert.Equal(baseline.groups, after.groups);
        Assert.Equal(baseline.reasons, after.reasons);
        Assert.Equal(baseline.conversions, after.conversions);
        Assert.Equal(2, await db.SeedMarkers.CountAsync());
        Assert.Equal(2, (await db.Tests.SingleAsync(t => t.Code == "CREAT")).MethodId);
        Assert.NotEqual(methodBefore, 2);
        var mg2 = await db.Tests.SingleAsync(t => t.Code == "MG");
        Assert.Equal("CUSTOM-1", mg2.LoincCode);
        Assert.Equal("Магній (змінена назва)", mg2.Name);
        var layer2 = await db.ReferenceLayers.SingleAsync(l => l.Id == layer.Id);
        Assert.Equal(1.2, layer2.NormHigh);
        Assert.Equal("Затверджено лікарем-лаборантом", layer2.Source);
        Assert.Equal("фл (змінено)", (await db.Units.SingleAsync(u => u.Code == "FL")).Name);
        Assert.Equal(baseline.units, after.units); // дублікатів одиниць не створено
        }
        finally
        {
            // повернути спільну БД тестів у вихідний стан
            db.ChangeTracker.Clear();
            var c = await db.Tests.SingleAsync(t => t.Code == "CREAT"); c.MethodId = orig["CREAT"].MethodId;
            var m = await db.Tests.SingleAsync(t => t.Code == "MG"); m.LoincCode = orig["MG"].LoincCode; m.Name = orig["MG"].Name;
            var l = await db.ReferenceLayers.SingleAsync(x => x.Id == origLayer.Id); l.NormHigh = origLayer.NormHigh; l.Source = origLayer.Source;
            (await db.Units.SingleAsync(u => u.Code == "FL")).Name = origFl;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Free_text_units_migrate_without_loss()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        // рядки зі «старої» бази: вільний текст одиниці без UnitId
        var origNa = await db.Tests.AsNoTracking().SingleAsync(x => x.Code == "NA");
        var t = await db.Tests.SingleAsync(x => x.Code == "K");
        t.UnitId = null; t.Unit = "ммоль/л";
        var t2 = await db.Tests.SingleAsync(x => x.Code == "NA");
        t2.UnitId = null; t2.Unit = "унікальна/одиниця";
        var layers = await db.ReferenceLayers.Where(l => l.TestCode == "K").ToListAsync();
        foreach (var l in layers) l.UnitId = null;
        await db.SaveChangesAsync();

        var changed = await MedLink.LIS.Api.Services.UnitCatalog.LinkAllAsync(db);
        Assert.True(changed >= 2 + layers.Count);
        db.ChangeTracker.Clear();
        var k = await db.Tests.SingleAsync(x => x.Code == "K");
        Assert.Equal("MMOL_L", (await db.Units.SingleAsync(u => u.Id == k.UnitId)).Code);   // збіг із довідником — без дубліката
        var na = await db.Tests.SingleAsync(x => x.Code == "NA");
        var custom = await db.Units.SingleAsync(u => u.Id == na.UnitId);
        Assert.Equal("унікальна/одиниця", custom.Name);                                      // нова одиниця створена, текст збережено
        Assert.StartsWith("CUSTOM_", custom.Code);
        Assert.All(await db.ReferenceLayers.Where(l => l.TestCode == "K").ToListAsync(), l => Assert.Equal(k.UnitId, l.UnitId));
        // повернути спільну БД у вихідний стан
        na.UnitId = origNa.UnitId; na.Unit = origNa.Unit;
        custom.IsActive = false;
        await db.SaveChangesAsync();
    }
}
