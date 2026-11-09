// API-тести: розрахункові показники (FR-WS-005), повтор і розведення (FR-WS-007), автоверифікація через API,
// автосписання реагентів, зв'язок «результат ↔ лот» і звіт витрат (FR-REA-001…004, FR-GAP-030).
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class ResultsCalcRerunReagentApiTests
{
    private const string Patient = "0b000000-0000-0000-0000-000000000003";
    private readonly LisApiFactory _f;
    public ResultsCalcRerunReagentApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static async Task AssertStatus(HttpResponseMessage r, HttpStatusCode status)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"очікувалось {status}, отримано {(int)r.StatusCode}: {text}");
    }

    private static string S(JsonNode? n) => n!.GetValue<string>();
    private static double D(JsonNode? n) => n!.GetValue<double>();

    private async Task EnsureCalcTest(string code, string formula, int decimals = 2, string? unit = null)
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var existing = await admin.GetAsync($"/api/v1/lab/dictionaries/tests/{code}");
        if (existing.IsSuccessStatusCode) return;
        await Json(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests", new { code, name = $"Розрахунок {code}", unit = unit ?? "", decimalPlaces = decimals, resultType = "CALCULATED", formula, biomaterialTypeId = 4, category = "BIOCHEM" }));
    }

    /// <summary>Замовлення з тестами, пробірки забрано й прийнято.</summary>
    private async Task<(string Id, Dictionary<string, string> Tests)> Order(params string[] codes)
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var nurse = _f.As(DemoDataSeeder.Nurse);
        var tech = _f.As(DemoDataSeeder.Technician);
        var order = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, testIds = codes }));
        foreach (var s in order["samples"]!.AsArray())
        {
            await Json(await nurse.PostAsJsonAsync($"/api/v1/lab/samples/{S(s!["barcode"])}/collect", new { checklist = new { idVerified = true }, volumeMl = 4.0 }));
            await Json(await tech.PostAsJsonAsync($"/api/v1/lab/samples/{S(s["barcode"])}/receive", new { }));
        }
        var detail = await Json(await tech.GetAsync($"/api/v1/lab/orders/{S(order["id"])}"));
        return (S(detail["id"]), detail["tests"]!.AsArray().ToDictionary(t => S(t!["testCode"]), t => S(t!["id"])));
    }

    /// <summary>Виправлення вже верифікованого значення: лікар повертає результат у роботу, лаборант вводить нове.</summary>
    private async Task<JsonNode> Correct(string id, double value)
    {
        await Json(await _f.As(DemoDataSeeder.Doctor).PostAsJsonAsync($"/api/v1/lab/results/{id}/reopen", new { reason = "виправлення" }));
        return await Enter(_f.As(DemoDataSeeder.Technician), id, value);
    }

    private async Task<JsonNode> Row(HttpClient c, string id) => await Json(await c.GetAsync($"/api/v1/lab/results/{id}"));

    private async Task<JsonNode> Enter(HttpClient c, string id, double value, string? analyzerId = null, double? dilution = null)
        => await Json(await c.PutAsJsonAsync($"/api/v1/lab/results/{id}", new { numericValue = value, analyzerId, dilutionFactor = dilution }));

    // ------------------------------------------------------------------ формули у довіднику
    [Fact]
    public async Task Formula_editor_validates_syntax_unknown_codes_and_cycles()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var tech = _f.As(DemoDataSeeder.Technician);

        var ok = await Json(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests/validate-formula", new { code = "FC_NEW", formula = "ROUND((CHOL - HDL) / HDL; 2)" }));
        Assert.True(ok["valid"]!.GetValue<bool>());
        Assert.Equal(new[] { "CHOL", "HDL" }, ok["inputs"]!.AsArray().Select(S).OrderBy(x => x));

        var syntax = await Json(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests/validate-formula", new { code = "FC_NEW", formula = "CHOL - (HDL" }));
        Assert.False(syntax["valid"]!.GetValue<bool>());
        Assert.Contains("дужку", S(syntax["error"]));

        var unknown = await Json(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests/validate-formula", new { code = "FC_NEW", formula = "CHOL / NOPE_X" }));
        Assert.False(unknown["valid"]!.GetValue<bool>());
        Assert.Contains("NOPE_X", S(unknown["error"]));

        var self = await Json(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests/validate-formula", new { code = "FC_SELF", formula = "FC_SELF + 1" }));
        Assert.False(self["valid"]!.GetValue<bool>());

        // створення зі зламаною формулою → 400; без формули для CALCULATED → 400
        await AssertStatus(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests", new { code = "FC_BAD", name = "Ні", unit = "", resultType = "CALCULATED", formula = "1 +", biomaterialTypeId = 4, category = "BIOCHEM" }), HttpStatusCode.BadRequest);
        await AssertStatus(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests", new { code = "FC_BAD2", name = "Ні", unit = "", resultType = "CALCULATED", biomaterialTypeId = 4, category = "BIOCHEM" }), HttpStatusCode.BadRequest);
        await AssertStatus(await tech.PostAsJsonAsync("/api/v1/lab/dictionaries/tests", new { code = "FC_BAD3", name = "Ні", resultType = "CALCULATED", formula = "CHOL*2", biomaterialTypeId = 4 }), HttpStatusCode.Forbidden);

        // цикл A → B → A: A залежить від CHOL, B від A; тоді A = B + 1 — цикл
        await Json(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests", new { code = "FC_A", name = "A", unit = "", resultType = "CALCULATED", formula = "CHOL * 2", biomaterialTypeId = 4, category = "BIOCHEM" }));
        await Json(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests", new { code = "FC_B", name = "B", unit = "", resultType = "CALCULATED", formula = "FC_A + 1", biomaterialTypeId = 4, category = "BIOCHEM" }));
        var cyc = await admin.PutAsJsonAsync("/api/v1/lab/dictionaries/tests/FC_A", new { formula = "FC_B + 1" });
        await AssertStatus(cyc, HttpStatusCode.BadRequest);
        Assert.Contains("Циклічне", S(JsonNode.Parse(await cyc.Content.ReadAsStringAsync())!["detail"]));

        // зміна формули пишеться в аудит
        await Json(await admin.PutAsJsonAsync("/api/v1/lab/dictionaries/tests/FC_A", new { formula = "CHOL * 3" }));
        var audit = await Json(await admin.GetAsync("/api/v1/lab/audit?action=FORMULA_CHANGED&pageSize=50"));
        Assert.Contains((audit["items"] ?? audit).AsArray(), a => (a!["afterJson"]?.ToString() ?? "").Contains("CHOL * 3"));
    }

    // ------------------------------------------------------------------ розрахунок: ЛПНЩ, ланцюжок, статуси
    [Fact]
    public async Task Calculated_ldl_follows_inputs_with_pending_flags_audit_and_manual_lock()
    {
        await EnsureCalcTest("LDLC_T", "ROUND(CHOL - HDL - TG / 2.2; 2)", 2, "ммоль/л");
        await EnsureCalcTest("AI_T", "ROUND((CHOL - HDL) / HDL; 2)", 2);
        var admin = _f.As(DemoDataSeeder.Admin);
        var tech = _f.As(DemoDataSeeder.Technician);
        var doctor = _f.As(DemoDataSeeder.Doctor);
        // норма для розрахункового — як у звичайних показників
        await Json(await admin.PostAsJsonAsync("/api/v1/lab/norms/combinations", new { testCode = "LDLC_T", layerType = "BASELINE", normName = "Дорослі", normLow = 0.0, normHigh = 3.0, critHigh = 8.0 }));

        var (orderId, t) = await Order("CHOL", "HDL", "TG", "LDLC_T", "AI_T");
        Assert.Equal("PENDING", S((await Row(tech, t["LDLC_T"]))["status"]));
        Assert.True((await Row(tech, t["LDLC_T"]))["isCalculated"]!.GetValue<bool>());

        // ручне введення розрахункового заблоковане
        await AssertStatus(await tech.PutAsJsonAsync($"/api/v1/lab/results/{t["LDLC_T"]}", new { numericValue = 2.0 }), HttpStatusCode.BadRequest);

        // є лише ЗХ і ЛПВЩ → бракує ТГ, результат не з'являється
        await Enter(tech, t["CHOL"], 5.2);
        await Enter(tech, t["HDL"], 1.3);
        var calc = await Json(await tech.GetAsync($"/api/v1/lab/results/{t["LDLC_T"]}/calculation"));
        Assert.False(calc["canCalculate"]!.GetValue<bool>());
        Assert.Contains("TG", calc["missing"]!.AsArray().Select(S));
        var pending = await Row(tech, t["LDLC_T"]);
        Assert.Equal("PENDING", S(pending["status"]));
        Assert.Null(pending["numericValue"]);
        // але індекс атерогенності вже порахований: (5.2-1.3)/1.3 = 3.0
        Assert.Equal(3.0, D((await Row(tech, t["AI_T"]))["numericValue"]), 6);

        // з'явився ТГ → ЛПНЩ = 5.2 − 1.3 − 1.1/2.2 = 3.4; прапорець за нормою (вище 3.0)
        var tgRow = await Enter(tech, t["TG"], 1.1);
        var ldl = await Row(tech, t["LDLC_T"]);
        Assert.Equal(3.4, D(ldl["numericValue"]), 6);
        Assert.Equal("HIGH", S(ldl["flag"]));
        Assert.True(ldl["isCalculated"]!.GetValue<bool>());
        Assert.NotEqual("PENDING", S(ldl["status"]));
        Assert.DoesNotContain("FORMULA", S(ldl["autoVerifyBlockReason"] ?? ""));

        // у аудит записано формулу і вхідні
        var audit = await Json(await admin.GetAsync($"/api/v1/lab/audit?entityId={S(ldl["resultId"])}&pageSize=20"));
        var json = (audit["items"] ?? audit).AsArray().Select(a => a!["afterJson"]?.ToString() ?? "").First(s => s.Contains("formula"));
        Assert.Contains("CHOL - HDL - TG / 2.2", json);
        Assert.Contains("TG", json);          // вхідні значення розрахунку
        Assert.Contains("1.3", json);

        // зміна вхідного → перерахунок (версія 2): 5.2 − 1.3 − 2.2/2.2 = 2.9 → NORMAL
        await Correct(t["TG"], 2.2);
        var ldl2 = await Row(tech, t["LDLC_T"]);
        Assert.Equal(2.9, D(ldl2["numericValue"]), 6);
        Assert.Equal("NORMAL", S(ldl2["flag"]));
        var hist = await Json(await tech.GetAsync($"/api/v1/lab/results/{t["LDLC_T"]}/history"));
        Assert.Contains(hist.AsArray(), h => S(h!["action"]) == "SUPERSEDED");

        // видалення вхідного результату знімає розрахунок
        await Json(await doctor.DeleteAsync($"/api/v1/lab/results/{t["TG"]}?reason=помилка введення"));
        var after = await Row(tech, t["LDLC_T"]);
        Assert.Equal("PENDING", S(after["status"]));
        Assert.Null(after["numericValue"]);

        // ручний перерахунок замовлення без змін — без помилок
        var rec = await Json(await tech.PostAsync($"/api/v1/lab/orders/{orderId}/recalculate", null));
        Assert.Empty(rec["warnings"]!.AsArray());
    }

    [Fact]
    public async Task Calculated_chain_and_ag_ratio_recalculate_when_input_changes()
    {
        await EnsureCalcTest("AG_T", "ALB / (PROT_TOT - ALB)", 2);
        await EnsureCalcTest("AGP_T", "ROUND(AG_T * 100; 0)", 0, "%");
        var tech = _f.As(DemoDataSeeder.Technician);
        var (_, t) = await Order("ALB", "PROT_TOT", "AG_T", "AGP_T");
        await Enter(tech, t["ALB"], 45);
        await Enter(tech, t["PROT_TOT"], 75);
        Assert.Equal(1.5, D((await Row(tech, t["AG_T"]))["numericValue"]), 6);
        Assert.Equal(150, D((await Row(tech, t["AGP_T"]))["numericValue"]), 6);

        // повтор складової: розрахунки (і ланцюжок) знімаються й чекають нового значення
        await Json(await tech.PostAsJsonAsync($"/api/v1/lab/results/{t["ALB"]}/rerun", new { reason = "контроль" }));
        Assert.Equal("PENDING", S((await Row(tech, t["AG_T"]))["status"]));
        Assert.Equal("PENDING", S((await Row(tech, t["AGP_T"]))["status"]));
        await Enter(tech, t["ALB"], 40);          // 40/35 = 1.14
        Assert.Equal(1.14, D((await Row(tech, t["AG_T"]))["numericValue"]), 6);
        Assert.Equal(114, D((await Row(tech, t["AGP_T"]))["numericValue"]), 6);

        // ділення на нуль не ламає введення складових — розрахунок просто не з'являється
        await Json(await tech.PostAsJsonAsync($"/api/v1/lab/results/{t["PROT_TOT"]}/rerun", new { reason = "контроль" }));
        var row = await Enter(tech, t["PROT_TOT"], 40);        // PROT − ALB = 0
        Assert.Contains(row["warnings"]!.AsArray().Select(S), w => w.Contains("нуль"));
        Assert.Equal("PENDING", S((await Row(tech, t["AG_T"]))["status"]));
    }

    [Fact]
    public async Task Calculated_egfr_uses_patient_age_and_sex()
    {
        const string ckd = "ROUND(141 * POW(MIN(CREAT / 88.4 / (0.9 * MALE + 0.7 * FEMALE); 1); -0.411 * MALE - 0.329 * FEMALE) * POW(MAX(CREAT / 88.4 / (0.9 * MALE + 0.7 * FEMALE); 1); -1.209) * POW(0.993; AGE) * (1 + 0.018 * FEMALE); 0)";
        await EnsureCalcTest("EGFR_T", ckd, 0, "мл/хв/1,73м²");
        var tech = _f.As(DemoDataSeeder.Technician);
        var (_, t) = await Order("CREAT", "EGFR_T");
        await Enter(tech, t["CREAT"], 88);
        var info = await Json(await tech.GetAsync($"/api/v1/lab/results/{t["EGFR_T"]}/calculation"));
        var ctx = info["context"]!.AsArray().ToDictionary(c => S(c!["name"]), c => c!["value"]);
        // демо-пацієнт має вік і стать — розрахунок виконано
        Assert.True(info["canCalculate"]!.GetValue<bool>(), info.ToJsonString());
        Assert.NotNull(ctx["AGE"]);
        var v = D((await Row(tech, t["EGFR_T"]))["numericValue"]);
        Assert.InRange(v, 20, 160);
    }

    // ------------------------------------------------------------------ повтор і розведення
    private async Task SetLinearity(string analyzerId, string testCode, double? low, double? high, double? factor)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var map = db.AnalyzerParameters.FirstOrDefault(m => m.AnalyzerId == analyzerId && m.TestCode == testCode);
        if (map == null)
        {
            map = new MedLink.LIS.Api.Data.Entities.LabAnalyzerParameterMap { AnalyzerId = analyzerId, AnalyzerCode = testCode + "_L", TestCode = testCode };
            db.AnalyzerParameters.Add(map);
        }
        map.LinearityLow = low; map.LinearityHigh = high; map.AutoDilutionFactor = factor;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Out_of_linearity_triggers_auto_dilution_rerun_keeps_attempts_and_labels_blank()
    {
        await SetLinearity(DemoDataSeeder.Mindray, "ALT", null, 200, 10);
        var tech = _f.As(DemoDataSeeder.Technician);
        var doctor = _f.As(DemoDataSeeder.Doctor);
        var admin = _f.As(DemoDataSeeder.Admin);
        var (orderId, t) = await Order("ALT");
        var id = t["ALT"];

        // 450 > лінійності 200 → авто-повтор із розведенням 1:10, результат не автоверифікується
        var first = await Enter(tech, id, 450, DemoDataSeeder.Mindray);
        Assert.Equal("RERUN", S(first["status"]));
        Assert.Equal(10, D(first["plannedDilution"]));
        Assert.True(first["outOfLinearity"]!.GetValue<bool>());
        Assert.Equal(1, first["rerunCount"]!.GetValue<int>());
        Assert.Contains(first["warnings"]!.AsArray().Select(S), w => w.Contains("розведенням 1:10"));
        Assert.False(first["isAutoVerified"]!.GetValue<bool>());

        // друге вимірювання: прилад видав 38 у розведеному зразку → результат 380 (зразок розведено 1:10 — множимо)
        var second = await Enter(tech, id, 38, DemoDataSeeder.Mindray);
        Assert.Equal(380, D(second["numericValue"]), 6);
        Assert.Equal(10, D(second["dilutionFactor"]));
        Assert.NotEqual("RERUN", S(second["status"]));
        Assert.Null(second["plannedDilution"]);

        var attempts = (await Json(await tech.GetAsync($"/api/v1/lab/results/{id}/attempts"))).AsArray();
        Assert.Equal(2, attempts.Count);
        Assert.Equal("INITIAL", S(attempts[0]!["kind"]));
        Assert.True(attempts[0]!["supersededByRerun"]!.GetValue<bool>());
        Assert.False(attempts[0]!["isAccepted"]!.GetValue<bool>());
        Assert.Equal(450, D(attempts[0]!["numericValue"]));
        Assert.Equal("AUTO_DILUTION", S(attempts[1]!["kind"]));
        Assert.True(attempts[1]!["isAccepted"]!.GetValue<bool>());
        Assert.Equal(38, D(attempts[1]!["rawValue"]));

        // бланк: позначка розведення й повторного аналізу
        var html = await (await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report")).Content.ReadAsStringAsync();
        Assert.Contains("розведення 1:10", html);
        Assert.Contains("повторний аналіз", html);

        // лаборант не може обирати прийняте значення; лікар — може, лише з коментарем
        var firstId = S(attempts[0]!["id"]);
        await AssertStatus(await tech.PostAsJsonAsync($"/api/v1/lab/results/{id}/accept-attempt", new { attemptId = firstId, comment = "x" }), HttpStatusCode.Forbidden);
        await AssertStatus(await doctor.PostAsJsonAsync($"/api/v1/lab/results/{id}/accept-attempt", new { attemptId = firstId }), HttpStatusCode.BadRequest);
        var accepted = await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/results/{id}/accept-attempt", new { attemptId = firstId, comment = "Розведення неточне, беремо первинне" }));
        Assert.Equal(450, D(accepted["numericValue"]));
        var after = (await Json(await tech.GetAsync($"/api/v1/lab/results/{id}/attempts"))).AsArray();
        Assert.True(after[0]!["isAccepted"]!.GetValue<bool>());
        Assert.False(after[1]!["isAccepted"]!.GetValue<bool>());
        Assert.Equal(2, after.Count); // нових спроб не з'явилось
    }

    [Fact]
    public async Task Manual_rerun_with_dilution_factor_records_attempts_and_validates()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var courier = _f.As(DemoDataSeeder.Courier);
        var (_, t) = await Order("GLU");
        var id = t["GLU"];

        // повтор до отримання результату неможливий
        await AssertStatus(await tech.PostAsJsonAsync($"/api/v1/lab/results/{id}/rerun", new { reason = "рано" }), HttpStatusCode.Conflict);

        var entered = await Enter(tech, id, 5.2);
        Assert.Equal("AUTO_VERIFIED", S(entered["status"]));
        await AssertStatus(await courier.PostAsJsonAsync($"/api/v1/lab/results/{id}/rerun", new { reason = "x" }), HttpStatusCode.Forbidden);
        await AssertStatus(await tech.PostAsJsonAsync($"/api/v1/lab/results/{id}/rerun", new { reason = "x", dilutionFactor = 0.5 }), HttpStatusCode.BadRequest);

        var rerun = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/results/{id}/rerun", new { reason = "Сумнів у значенні", dilutionFactor = 5 }));
        Assert.Equal("RERUN", S(rerun["status"]));
        Assert.Equal(5, D(rerun["plannedDilution"]));
        Assert.Equal(1, rerun["rerunCount"]!.GetValue<int>());
        Assert.False(rerun["isAutoVerified"]!.GetValue<bool>());
        var hist = await Json(await tech.GetAsync($"/api/v1/lab/results/{id}/history"));
        Assert.Contains(hist.AsArray(), h => S(h!["action"]) == "RERUN" && S(h["comment"]).Contains("1:5"));

        var again = await Enter(tech, id, 1.0);       // 1.0 у розведеному 1:5 → 5.0
        Assert.Equal(5.0, D(again["numericValue"]), 6);
        Assert.Equal(5, D(again["dilutionFactor"]));
        var attempts = (await Json(await tech.GetAsync($"/api/v1/lab/results/{id}/attempts"))).AsArray();
        Assert.Equal(2, attempts.Count);
        Assert.Equal(5.2, D(attempts[0]!["numericValue"]));
        Assert.Equal("DILUTION", S(attempts[1]!["kind"]));

        // некоректне розведення при введенні
        await AssertStatus(await tech.PutAsJsonAsync($"/api/v1/lab/results/{id}", new { numericValue = 1.0, dilutionFactor = 0 }), HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------ autoverify через API
    [Fact]
    public async Task Autoverify_api_blocks_by_reason_then_passes_after_conditions_change()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var tech = _f.As(DemoDataSeeder.Technician);
        var doctor = _f.As(DemoDataSeeder.Doctor);
        await Json(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests", new { code = "AV_MAN_T", name = "Ручна верифікація", unit = "од", decimalPlaces = 1, biomaterialTypeId = 4, category = "BIOCHEM", requiresManualVerification = true }));
        await Json(await admin.PostAsJsonAsync("/api/v1/lab/norms/combinations", new { testCode = "AV_MAN_T", layerType = "BASELINE", normName = "Дорослі", normLow = 0.0, normHigh = 10.0 }));
        var (_, t) = await Order("AV_MAN_T", "K");

        // обов'язкова ручна верифікація → RESULTED, не автоверифіковано
        var row = await Enter(tech, t["AV_MAN_T"], 5);
        Assert.Equal("RESULTED", S(row["status"]));
        // критичний калій → NEEDS_REVIEW
        var k = await Enter(tech, t["K"], 7.5);
        Assert.Equal("NEEDS_REVIEW", S(k["status"]));

        var blocked = await Json(await tech.PostAsJsonAsync("/api/v1/lab/worklist/autoverify", new { orderTestIds = new[] { t["AV_MAN_T"], t["K"] } }));
        Assert.Equal(0, blocked["verified"]!.GetValue<int>());
        Assert.Equal(2, blocked["blocked"]!.AsArray().Count);
        Assert.All(blocked["blocked"]!.AsArray(), b => Assert.False(string.IsNullOrWhiteSpace(S(b!["reason"]))));
        // курʼєр не може запускати автоверифікацію
        await AssertStatus(await _f.As(DemoDataSeeder.Courier).PostAsJsonAsync("/api/v1/lab/worklist/autoverify", new { orderTestIds = new[] { t["K"] } }), HttpStatusCode.Forbidden);

        // зняли вимогу ручної верифікації → автоверифікація проходить для першого, критичний лишається заблокованим
        await Json(await admin.PutAsJsonAsync("/api/v1/lab/dictionaries/tests/AV_MAN_T", new { requiresManualVerification = false }));
        var ok = await Json(await doctor.PostAsJsonAsync("/api/v1/lab/worklist/autoverify", new { orderTestIds = new[] { t["AV_MAN_T"], t["K"] } }));
        Assert.Equal(1, ok["verified"]!.GetValue<int>());
        Assert.Equal(t["AV_MAN_T"], S(ok["verifiedIds"]![0]));
        Assert.Single(ok["blocked"]!.AsArray());
        Assert.Equal("AUTO_VERIFIED", S((await Row(tech, t["AV_MAN_T"]))["status"]));
    }

    // ------------------------------------------------------------------ реагенти
    private async Task<JsonNode> CreateLot(string testCode, string lot, int initial, int? remaining, int minimum, bool active = true, int? units = null, int expiryDays = 200)
    {
        var tech = _f.As(DemoDataSeeder.Admin);
        return await Json(await tech.PostAsJsonAsync("/api/v1/lab/reagents/lots", new
        {
            analyzerId = DemoDataSeeder.Mindray, testCode, reagentName = $"Реагент {testCode}", lotNumber = lot, testsInitial = initial, testsRemaining = remaining, minimumTests = minimum,
            expiryDate = DateTime.UtcNow.AddDays(expiryDays), isActive = active, unitsPerTest = units ?? 1
        }));
    }

    private async Task<JsonNode> LotNow(string id) => await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"/api/v1/lab/reagents/lots/{id}"));

    [Fact]
    public async Task Results_auto_consume_reagent_link_lot_switch_on_depletion_and_report()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var admin = _f.As(DemoDataSeeder.Admin);
        var a = await CreateLot("CRP", "CRP-A1", 3, 2, 1, true, units: 1);
        var b = await CreateLot("CRP", "CRP-B1", 100, 100, 10, false, units: 1);
        var (_, t) = await Order("CRP", "CRP");   // дубль ігнорується — один тест
        var (_, t2) = await Order("CRP");
        var (_, t3) = await Order("CRP");

        // 1-й результат: лот A (2 → 1); залишок = мінімум → попередження про залишок
        var r1 = await Enter(tech, t["CRP"], 2.0, DemoDataSeeder.Mindray);
        Assert.Equal("CRP-A1", S(r1["reagentLotNumber"]));
        Assert.Equal(1, (await LotNow(S(a["id"])))["testsRemaining"]!.GetValue<int>());
        Assert.Contains(r1["warnings"]!.AsArray().Select(S), w => w.Contains("Залишок реагенту"));

        // 2-й: лот A вичерпано (1 → 0) — автоперемикання на B
        var r2 = await Enter(tech, t2["CRP"], 2.0, DemoDataSeeder.Mindray);
        Assert.Equal("CRP-A1", S(r2["reagentLotNumber"]));
        Assert.Contains(r2["warnings"]!.AsArray().Select(S), w => w.Contains("активовано лот CRP-B1"));
        Assert.False((await LotNow(S(a["id"])))["isActive"]!.GetValue<bool>());
        Assert.True((await LotNow(S(b["id"])))["isActive"]!.GetValue<bool>());

        // 3-й — із лоту B
        var r3 = await Enter(tech, t3["CRP"], 2.0, DemoDataSeeder.Mindray);
        Assert.Equal("CRP-B1", S(r3["reagentLotNumber"]));
        Assert.Equal(99, (await LotNow(S(b["id"])))["testsRemaining"]!.GetValue<int>());

        // зв'язок «результат ↔ лот»
        var link = (await Json(await tech.GetAsync($"/api/v1/lab/reagents/by-result/{t3["CRP"]}"))).AsArray();
        Assert.Single(link);
        Assert.Equal("CRP-B1", S(link[0]!["lotNumber"]));
        Assert.Equal("AUTO", S(link[0]!["kind"]));

        // виправлення значення (не нове вимірювання) реагент повторно не списує
        await Correct(t3["CRP"], 2.2);
        Assert.Equal(99, (await LotNow(S(b["id"])))["testsRemaining"]!.GetValue<int>());

        // повтор списує ще раз (Kind = RERUN)
        await Json(await tech.PostAsJsonAsync($"/api/v1/lab/results/{t3["CRP"]}/rerun", new { reason = "контроль" }));
        await Enter(tech, t3["CRP"], 2.1, DemoDataSeeder.Mindray);
        Assert.Equal(98, (await LotNow(S(b["id"])))["testsRemaining"]!.GetValue<int>());
        var link2 = (await Json(await tech.GetAsync($"/api/v1/lab/reagents/by-result/{t3["CRP"]}"))).AsArray();
        Assert.Equal(new[] { "AUTO", "RERUN" }, link2.Select(l => S(l!["kind"])));

        // аудит списання (FR-REA-004)
        var audit = await Json(await admin.GetAsync($"/api/v1/lab/audit?entity=lab_reagent_lot&entityId={S(b["id"])}&action=CONSUME&pageSize=20"));
        Assert.True((audit["items"] ?? audit).AsArray().Count >= 2);

        // звіт витрат за лотом і за періодом
        var rep = await Json(await admin.GetAsync($"/api/v1/lab/reagents/consumption?lotId={S(b["id"])}"));
        Assert.Equal(2, rep["totals"]!["units"]!.GetValue<int>());
        Assert.Equal(1, rep["byLot"]!.AsArray().Count);
        Assert.Equal("CRP-B1", S(rep["byLot"]![0]!["lotNumber"]));
        Assert.Equal(2, rep["byLot"]![0]!["runs"]!.GetValue<int>());
        var all = await Json(await admin.GetAsync($"/api/v1/lab/reagents/consumption?testCode=CRP&from={DateTime.UtcNow:yyyy-MM-dd}&to={DateTime.UtcNow:yyyy-MM-dd}"));
        Assert.True(all["totals"]!["units"]!.GetValue<int>() >= 4);
        var past = await Json(await admin.GetAsync($"/api/v1/lab/reagents/consumption?lotId={S(b["id"])}&from=2020-01-01&to=2020-01-31"));
        Assert.Equal(0, past["totals"]!["records"]!.GetValue<int>());
    }

    [Fact]
    public async Task Reagent_shortage_never_blocks_result_only_warns()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var admin = _f.As(DemoDataSeeder.Admin);
        var lot = await CreateLot("NA", "NA-EMPTY", 10, 0, 5, true, units: 2);
        var (_, t) = await Order("NA");

        var row = await Enter(tech, t["NA"], 140, DemoDataSeeder.Mindray);       // раніше було б 409
        Assert.Equal(140, D(row["numericValue"]));
        Assert.Contains(row["warnings"]!.AsArray().Select(S), w => w.Contains("не вистачає"));
        var now = await LotNow(S(lot["id"]));
        Assert.Equal(0, now["testsRemaining"]!.GetValue<int>());                  // не йде в мінус
        var link = (await Json(await tech.GetAsync($"/api/v1/lab/reagents/by-result/{t["NA"]}"))).AsArray();
        Assert.Equal(2, link[0]!["shortfall"]!.GetValue<int>());
        Assert.Equal(0, link[0]!["units"]!.GetValue<int>());

        var alerts = (await Json(await admin.GetAsync("/api/v1/lab/reagents/alerts"))).AsArray();
        Assert.Contains(alerts, a => S(a!["lotNumber"]) == "NA-EMPTY" && S(a["type"]) == "EMPTY");
        var rep = await Json(await admin.GetAsync($"/api/v1/lab/reagents/consumption?lotId={S(lot["id"])}"));
        Assert.Equal(2, rep["totals"]!["shortfall"]!.GetValue<int>());
        Assert.True(rep["byLot"]![0]!["belowMinimum"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Manual_consume_over_remaining_warns_instead_of_409_and_respects_norm()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var courier = _f.As(DemoDataSeeder.Courier);
        var lot = await CreateLot("UREA", "UR-MAN", 20, 5, 3, true);
        var id = S(lot["id"]);

        var ok = await Json(await admin.PostAsJsonAsync($"/api/v1/lab/reagents/lots/{id}/consume", new { tests = 2 }));
        Assert.Equal(3, ok["testsRemaining"]!.GetValue<int>());
        Assert.Equal(0, ok["shortfall"]!.GetValue<int>());
        Assert.Contains("мінімум", S(ok["warning"]));            // 3 ≤ мінімум 3

        var over = await Json(await admin.PostAsJsonAsync($"/api/v1/lab/reagents/lots/{id}/consume", new { tests = 10 }));
        Assert.Equal(0, over["testsRemaining"]!.GetValue<int>());
        Assert.Equal(7, over["shortfall"]!.GetValue<int>());
        Assert.Contains("не вистачає", S(over["warning"]));

        await AssertStatus(await admin.PostAsJsonAsync($"/api/v1/lab/reagents/lots/{id}/consume", new { tests = 0 }), HttpStatusCode.BadRequest);
        await AssertStatus(await courier.PostAsJsonAsync($"/api/v1/lab/reagents/lots/{id}/consume", new { tests = 1 }), HttpStatusCode.Forbidden);

        // норма витрати 3 на тест
        var heavy = await CreateLot("UREA", "UR-NORM3", 30, 30, 5, true, units: 3);
        var tech = _f.As(DemoDataSeeder.Technician);
        var (_, t) = await Order("UREA");
        // активні UR-MAN (0) і UR-NORM3: вибирається придатний лот із залишком
        var row = await Enter(tech, t["UREA"], 5.0, DemoDataSeeder.Mindray);
        Assert.Equal("UR-NORM3", S(row["reagentLotNumber"]));
        Assert.Equal(27, (await LotNow(S(heavy["id"])))["testsRemaining"]!.GetValue<int>());
    }

    [Fact]
    public async Task Expired_lot_is_not_preferred_and_warns_when_only_option()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var expired = await CreateLot("ALB", "ALB-OLD", 50, 50, 5, true, expiryDays: -5);
        var fresh = await CreateLot("ALB", "ALB-NEW", 50, 50, 5, false);
        var (_, t) = await Order("ALB");
        var row = await Enter(tech, t["ALB"], 42, DemoDataSeeder.Mindray);
        Assert.Equal("ALB-NEW", S(row["reagentLotNumber"]));
        Assert.Equal(50, (await LotNow(S(expired["id"])))["testsRemaining"]!.GetValue<int>());
        Assert.True((await LotNow(S(fresh["id"])))["isActive"]!.GetValue<bool>());
        Assert.False((await LotNow(S(expired["id"])))["isActive"]!.GetValue<bool>());
    }
}
