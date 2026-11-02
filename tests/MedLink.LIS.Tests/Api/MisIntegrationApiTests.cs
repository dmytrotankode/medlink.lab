// ТЗ розд. 21 і 23: направлення МІС з обраними показниками, маршрутизація по приладах, розподіл (вхідні направлення),
// довідник «Послуга ЕСОЗ × аналізатори», каталог для МІС, підпис і відправка звіту в ЕСОЗ, робота за направленнями, звіти
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Data.Seed;
using MedLink.LIS.Core.Clinical;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class MisIntegrationApiTests
{
    private const string Patient = "0b000000-0000-0000-0000-000000000005";
    private const string Bc780 = "0a2a0000-0000-0000-0000-000000000003", Bc20s = "0a2a0000-0000-0000-0000-000000000004", Sysmex = DemoDataSeeder.Sysmex;
    private readonly LisApiFactory _f;
    public MisIntegrationApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static async Task<string> Problem(HttpResponseMessage r, HttpStatusCode status)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"очікувалось {(int)status}, отримано {(int)r.StatusCode}: {text}");
        return JsonNode.Parse(text)!["code"]?.GetValue<string>() ?? "";
    }

    private static string S(JsonNode? n) => n!.GetValue<string>();

    private async Task<T> Db<T>(Func<LisDbContext, Task<T>> f)
    {
        using var scope = _f.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<LisDbContext>());
    }

    private static object Item(string code, params string[] indicators) => new { interventionCode = code, indicators };

    private Task<HttpResponseMessage> Refer(object body) => _f.As(DemoDataSeeder.Doctor).PostAsJsonAsync("/api/v1/lab/mis/referrals", body);

    private static IEnumerable<JsonNode> Tests(JsonNode referral) => referral["items"]!.AsArray().SelectMany(i => i!["tests"]!.AsArray()).Select(t => t!);

    [Fact]
    public async Task Demo_inbox_shows_package_cito_and_needs_selection_referrals()
    {
        var inbox = await Json(await _f.As(DemoDataSeeder.Technician).GetAsync("/api/v1/lab/distribution/inbox?state=all"));
        var items = inbox["items"]!.AsArray();
        var postop = items.First(i => S(i!["referralNumber"]) == "НП-2611-0108")!;
        Assert.Equal("PROPOSED", S(postop["distributionStatus"]));
        Assert.Equal("PKG-POSTOP-1", S(postop["packageCode"]));
        Assert.Equal("NEEDS_SELECTION", S(items.First(i => S(i!["referralNumber"]) == "0003-2026-D2X7")!["distributionStatus"]));

        // CITO-пакет ВРІТ: розподіл без підтвердження, газоаналізатор і експрес-гематологія
        var icu = await Json(await _f.As(DemoDataSeeder.Technician).GetAsync("/api/v1/lab/mis/referrals/НП-2611-0107"));
        Assert.Equal("CONFIRMED", S(icu["distributionStatus"]));
        Assert.All(Tests(icu), t => Assert.NotEqual("PROPOSED", S(t["routingStatus"])));
        Assert.Equal(Bc20s, S(Tests(icu).First(t => S(t["code"]) == "HGB")["analyzerId"]));
        Assert.Contains(icu["events"]!.AsArray(), e => S(e!["event"]) == MisEvents.Received);

        // Е-направлення: онкомаркер без обладнання, не обрані показники ЗАК видно як «·»
        var eh = await Json(await _f.As(DemoDataSeeder.Technician).GetAsync("/api/v1/lab/mis/referrals/0002-2026-K4P9"));
        Assert.Equal("EHEALTH", S(eh["source"]));
        Assert.Equal(RoutingStatuses.NoEquipment, S(Tests(eh).First(t => S(t["code"]) == "CA125")["routingStatus"]));
        Assert.Contains(eh["items"]!.AsArray().First(i => S(i!["interventionCode"]) == "DEMO-LAB-CBC")!["notOrdered"]!.AsArray(), x => S(x) == "MCV");
    }

    [Fact]
    public async Task Referral_validates_indicators_and_rejects_duplicates()
    {
        var id = Guid.NewGuid().ToString();
        // Обов'язковий показник ЗАК (HGB) не обрано → 422
        Assert.Equal("required-indicator-missing", await Problem(await Refer(new { misReferralId = id, context = "AMBULATORY", source = "POLYCLINIC", patientId = Patient, doctorId = DemoDataSeeder.Doctor,
            items = new[] { Item("DEMO-LAB-CBC", "WBC", "RBC", "HCT", "PLT") } }), HttpStatusCode.UnprocessableEntity));
        // Показник не зі складу послуги → 422
        Assert.Equal("unknown-indicator", await Problem(await Refer(new { misReferralId = id, context = "AMBULATORY", source = "POLYCLINIC", patientId = Patient, doctorId = DemoDataSeeder.Doctor,
            items = new[] { Item("DEMO-LAB-CBC", "WBC", "RBC", "HGB", "HCT", "PLT", "TSH") } }), HttpStatusCode.UnprocessableEntity));
        // Внутрішнє направлення з неспівставленою інтервенцією → 422; патоморфологія — окремий модуль
        Assert.Equal("intervention-unmapped", await Problem(await Refer(new { misReferralId = id, context = "AMBULATORY", source = "POLYCLINIC", patientId = Patient, doctorId = DemoDataSeeder.Doctor,
            items = new[] { Item("DEMO-LAB-AMH") } }), HttpStatusCode.UnprocessableEntity));

        var created = await Json(await Refer(new { misReferralId = id, referralNumber = "НП-T-1", context = "AMBULATORY", source = "POLYCLINIC", patientId = Patient, doctorId = DemoDataSeeder.Doctor,
            items = new[] { Item("DEMO-LAB-CBC", "WBC", "RBC", "HGB", "HCT", "PLT") } }));
        Assert.Equal("PROPOSED", S(created["distributionStatus"]));
        var cbc = created["items"]!.AsArray()[0]!;
        Assert.Equal(5, cbc["tests"]!.AsArray().Count);
        Assert.Contains(cbc["notOrdered"]!.AsArray(), x => S(x) == "NEUT_ABS");
        Assert.All(cbc["tests"]!.AsArray(), t => Assert.Equal(RoutingStatuses.Proposed, S(t!["routingStatus"])));
        // Планове ЗАК: пріоритет 1 — BC-780
        Assert.Equal(Bc780, S(cbc["tests"]!.AsArray().First(t => S(t!["code"]) == "HGB")!["analyzerId"]));

        Assert.Equal("duplicate-referral", await Problem(await Refer(new { misReferralId = id, context = "AMBULATORY", source = "POLYCLINIC", patientId = Patient, doctorId = DemoDataSeeder.Doctor,
            items = new[] { Item("DEMO-LAB-CBC", "WBC", "RBC", "HGB", "HCT", "PLT") } }), HttpStatusCode.Conflict));

        // Скасування до початку роботи
        var cancelled = await Json(await _f.As(DemoDataSeeder.Doctor).PostAsJsonAsync($"/api/v1/lab/mis/referrals/{id}/cancel", new { reason = "Помилкове призначення" }));
        Assert.Equal(OrderStatuses.Cancelled, S(cancelled["status"]));
    }

    [Fact]
    public async Task Distribution_change_auto_and_confirm_enqueue_tests()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var id = Guid.NewGuid().ToString();
        var created = await Json(await Refer(new { misReferralId = id, context = "INPATIENT", patientId = Patient, doctorId = DemoDataSeeder.Doctor, department = "Хірургія", ward = "3", bed = "2",
            items = new[] { Item("DEMO-LAB-CBC", "WBC", "RBC", "HGB", "HCT", "PLT"), Item("DEMO-LAB-TM", "CA125") } }));
        var orderId = S(created["orderId"]);
        var hgb = Tests(created).First(t => S(t["code"]) == "HGB");
        var ca125 = Tests(created).First(t => S(t["code"]) == "CA125");
        Assert.Equal(RoutingStatuses.NoEquipment, S(ca125["routingStatus"]));

        // Прилад, що не виконує показник, — відмова; кандидат — розщеплення послуги з попередженням
        var bad = await tech.PutAsJsonAsync($"/api/v1/lab/distribution/{orderId}", new[] { new { orderTestId = S(hgb["orderTestId"]), analyzerId = DemoDataSeeder.Mindray } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var changed = await Json(await tech.PutAsJsonAsync($"/api/v1/lab/distribution/{orderId}", new[] { new { orderTestId = S(hgb["orderTestId"]), analyzerId = Bc20s, reason = "Експрес" } }));
        var cbcItem = changed["items"]!.AsArray().First(i => S(i!["interventionCode"]) == "DEMO-LAB-CBC")!;
        Assert.True(cbcItem["split"]!.GetValue<bool>());
        Assert.Contains(cbcItem["tests"]!.AsArray().First(t => S(t!["code"]) == "HGB")!["candidates"]!.AsArray(), c => S(c!["analyzerId"]) == Sysmex);

        // Авторозподіл скидає ручні зміни
        var auto = await Json(await tech.PostAsync($"/api/v1/lab/distribution/{orderId}/auto", null));
        Assert.False(auto["items"]!.AsArray().First(i => S(i!["interventionCode"]) == "DEMO-LAB-CBC")!["split"]!.GetValue<bool>());

        // Підтвердження: CA125 — send-out; решта — у черги приладів, подія RECEIVED для МІС
        var confirmed = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/distribution/{orderId}/confirm",
            new { noEquipment = new[] { new { orderTestId = S(ca125["orderTestId"]), action = "SENDOUT" } } }));
        Assert.Equal("CONFIRMED", S(confirmed["proposal"]!["distributionStatus"]));
        var after = await Json(await tech.GetAsync($"/api/v1/lab/mis/referrals/{orderId}"));
        Assert.All(Tests(after).Where(t => S(t["code"]) != "CA125"), t => Assert.Equal(RoutingStatuses.Routed, S(t["routingStatus"])));
        Assert.Equal(RoutingStatuses.SendOut, S(Tests(after).First(t => S(t["code"]) == "CA125")["routingStatus"]));
        Assert.Contains(after["events"]!.AsArray(), e => S(e!["event"]) == MisEvents.Received);
        var queue = await Json(await tech.GetAsync($"/api/v1/lab/analyzers/{Bc780}/queue"));
        Assert.Contains(S(hgb["orderTestId"]), queue.ToJsonString());
    }

    [Fact]
    public async Task Needs_selection_requires_indicators_before_confirm()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var id = Guid.NewGuid().ToString();
        var created = await Json(await Refer(new { misReferralId = id, context = "AMBULATORY", source = "PARTNER", partnerName = "АЗПСМ № 3", patientId = Patient, items = new[] { Item("DEMO-LAB-COAG") } }));
        Assert.Equal("NEEDS_SELECTION", S(created["distributionStatus"]));
        Assert.NotEmpty(created["warnings"]!.AsArray());
        var orderId = S(created["orderId"]);
        Assert.Equal(HttpStatusCode.Conflict, (await tech.PostAsJsonAsync($"/api/v1/lab/distribution/{orderId}/confirm", new { })).StatusCode);

        var itemId = S(created["items"]!.AsArray()[0]!["itemId"]);
        await Json(await tech.PutAsJsonAsync($"/api/v1/lab/mis/referrals/{orderId}/items/{itemId}/indicators", new { indicators = new[] { "PT", "INR", "APTT" }, selectedBy = "REGISTRAR" }));
        var proposal = await Json(await tech.GetAsync($"/api/v1/lab/distribution/{orderId}"));
        Assert.Equal("PROPOSED", S(proposal["distributionStatus"]));
        await Json(await tech.PostAsJsonAsync($"/api/v1/lab/distribution/{orderId}/confirm", new { }));
    }

    [Fact]
    public async Task Cito_routing_prefers_fastest_analyzer_and_skips_long_offline()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var routine = await Json(await tech.PostAsJsonAsync("/api/v1/lab/routing/preview", new { profileId = "PROF_CBC", testCodes = new[] { "HGB" }, cito = false }));
        var cito = await Json(await tech.PostAsJsonAsync("/api/v1/lab/routing/preview", new { profileId = "PROF_CBC", testCodes = new[] { "HGB" }, cito = true }));
        Assert.Equal(Bc780, S(routine["tests"]![0]!["analyzerId"]));
        Assert.Equal(Bc20s, S(cito["tests"]![0]!["analyzerId"]));
        // Respons 940 без зв'язку понад 30 хв — недоступний кандидат
        var glu = await Json(await tech.PostAsJsonAsync("/api/v1/lab/routing/preview", new { testCodes = new[] { "GLU" } }));
        var respons = glu["tests"]![0]!["candidates"]!.AsArray().First(c => S(c!["analyzerId"]) == "0a2a0000-0000-0000-0000-000000000006")!;
        Assert.False(respons["available"]!.GetValue<bool>());
        Assert.Equal("немає зв'язку", S(respons["reason"]));
    }

    [Fact]
    public async Task Service_matrix_card_and_catalog_for_mis()
    {
        var doc = _f.As(DemoDataSeeder.Doctor);
        var matrix = await Json(await doc.GetAsync("/api/v1/lab/analyzer-services/matrix"));
        var cbcRow = matrix["rows"]!.AsArray().First(r => S(r!["serviceCode"]) == "DEMO-LAB-CBC")!;
        Assert.Equal(1, cbcRow["cells"]![Bc780]!["priority"]!.GetValue<int>());
        Assert.Equal("NO_EQUIPMENT", S(matrix["rows"]!.AsArray().First(r => S(r!["serviceCode"]) == "DEMO-LAB-TM")!["readiness"]));

        var card = await Json(await doc.GetAsync("/api/v1/lab/analyzer-services/DEMO-LAB-CBC"));
        Assert.Contains(card["indicators"]!.AsArray(), i => S(i!["code"]) == "HGB" && i["isRequired"]!.GetValue<bool>());

        // Вимкнення приладу в послузі — спершу кількість зачеплених тестів (dryRun), без змін
        var dry = await Json(await doc.PutAsJsonAsync("/api/v1/lab/analyzer-services/DEMO-LAB-CBC",
            new { dryRun = true, analyzers = new[] { new { analyzerId = Bc780, isEnabled = false, priority = 1 } } }));
        Assert.True(dry["dryRun"]!.GetValue<bool>());
        // Новий показник на приладі без коду приладу → 422 (FR-EQP-006)
        Assert.Equal("analyzer-code-missing", await Problem(await doc.PutAsJsonAsync("/api/v1/lab/analyzer-services/DEMO-LAB-CBC",
            new { @params = new[] { new { analyzerId = Bc20s, testCode = "RET", isEnabled = true } } }), HttpStatusCode.UnprocessableEntity));
        // Реєстратор не редагує довідник
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.As(DemoDataSeeder.Registrar).PutAsJsonAsync("/api/v1/lab/analyzer-services/DEMO-LAB-CBC", new { dryRun = true })).StatusCode);

        // Каталог для МІС з ETag
        var first = await doc.GetAsync("/api/v1/lab/mis/catalog");
        var catalog = await Json(first);
        Assert.Contains("DEMO-LAB-CBC", catalog.ToJsonString());
        using var again = new HttpRequestMessage(HttpMethod.Get, "/api/v1/lab/mis/catalog");
        again.Headers.TryAddWithoutValidation("If-None-Match", first.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NotModified, (await doc.SendAsync(again)).StatusCode);
        var packages = await Json(await doc.GetAsync("/api/v1/lab/mis/catalog/packages"));
        Assert.Contains(packages.AsArray(), p => S(p!["code"]) == "PKG-POSTOP-ICU");
    }

    [Fact]
    public async Task Ehealth_session_two_factor_sign_submit_retry_and_annul()
    {
        var doc = _f.As(DemoDataSeeder.Doctor);
        await doc.DeleteAsync("/api/v1/lab/ehealth/session");
        var orderId = await Db(db => db.Orders.Where(o => o.PatientId == "0b000000-0000-0000-0000-000000000001" && o.Status == OrderStatuses.Released && o.DiagnosticReportId != null
                                                         && o.Tests.Any(t => t.Profile!.Code == "PROF_CBC") && o.EhealthStatus == EhealthSubmitStatuses.NotSent)
            .OrderBy(o => o.OrderDatetime).Select(o => o.Id).FirstAsync());

        Assert.Equal("L0", S((await Json(await doc.GetAsync("/api/v1/lab/ehealth/session")))["level"]));
        Assert.Equal("ehealth-auth-required", await Problem(await doc.PostAsync($"/api/v1/lab/orders/{orderId}/ehealth/prepare", null), HttpStatusCode.Unauthorized));
        Assert.Equal("L1", S((await Json(await doc.PostAsJsonAsync("/api/v1/lab/ehealth/session", new { login = "doctor@clinic.ua", password = "secret" })))["level"]));
        Assert.Equal("ehealth-strong-auth-required", await Problem(await doc.PostAsync($"/api/v1/lab/orders/{orderId}/ehealth/prepare", null), HttpStatusCode.Forbidden));

        // 2FA вимкнена → 409; після увімкнення невірний код → 422, вірний → L2
        Assert.Equal("ehealth-totp-disabled", await Problem(await doc.PostAsJsonAsync("/api/v1/lab/ehealth/session/confirm", new { method = "TOTP", code = "123456" }), HttpStatusCode.Conflict));
        await Json(await doc.PostAsync("/api/v1/lab/ehealth/session/totp", null));
        Assert.Equal("ehealth-invalid-code", await Problem(await doc.PostAsJsonAsync("/api/v1/lab/ehealth/session/confirm", new { method = "TOTP", code = "000000" }), HttpStatusCode.UnprocessableEntity));
        Assert.Equal("L2", S((await Json(await doc.PostAsJsonAsync("/api/v1/lab/ehealth/session/confirm", new { method = "TOTP", code = "123456" })))["level"]));

        var prepared = await Json(await doc.PostAsync($"/api/v1/lab/orders/{orderId}/ehealth/prepare", null));
        Assert.Equal(EhealthSubmitStatuses.ReadyToSign, S(prepared["ehealthStatus"]));
        Assert.Equal(HttpStatusCode.BadRequest, (await doc.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/ehealth/submit", new { })).StatusCode);

        // Мережева помилка → ERROR, підписаний пакет збережено; без сесії повтор → AUTH_REQUIRED
        var status = await Json(await doc.GetAsync("/api/v1/lab/ehealth/session"));
        var cert = status["testCertificate"]!;
        var failed = await Json(await doc.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/ehealth/submit", new { signedData = "MIIDEMO==", signer = cert, simulateNetworkError = true }));
        Assert.Equal(EhealthSubmitStatuses.Error, S(failed["ehealthStatus"]));
        await doc.DeleteAsync("/api/v1/lab/ehealth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, (await doc.PostAsync($"/api/v1/lab/orders/{orderId}/ehealth/retry", null)).StatusCode);
        Assert.Equal(EhealthSubmitStatuses.AuthRequired, S((await Json(await doc.GetAsync($"/api/v1/lab/orders/{orderId}/ehealth/status")))["status"]));

        // Вхід через КЕП: чужий ДРФО → 422; власний тестовий сертифікат → L2; повтор без повторного підпису
        await Json(await doc.PostAsJsonAsync("/api/v1/lab/ehealth/session", new { login = "doctor@clinic.ua", password = "secret" }));
        Assert.Equal("signer-mismatch", await Problem(await doc.PostAsJsonAsync("/api/v1/lab/ehealth/session/confirm",
            new { method = "KEP", signer = new { drfo = "0000000000", name = "Інша особа" } }), HttpStatusCode.UnprocessableEntity));
        await Json(await doc.PostAsJsonAsync("/api/v1/lab/ehealth/session/confirm", new { method = "KEP", signer = cert }));
        var sent = await Json(await doc.PostAsync($"/api/v1/lab/orders/{orderId}/ehealth/retry", null));
        Assert.Equal(EhealthSubmitStatuses.Registered, S(sent["ehealthStatus"]));
        Assert.NotNull(sent["submission"]!["ehealthReportId"]);

        var annulled = await Json(await doc.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/ehealth/annul", new { signedData = "MIIANNUL==", signer = cert, reason = "Помилка в одиницях" }));
        Assert.Equal(EhealthSubmitStatuses.Annulled, S(annulled["ehealthStatus"]));
        await doc.DeleteAsync("/api/v1/lab/ehealth/session");
    }

    [Fact]
    public async Task Work_views_reports_and_cumulative_export()
    {
        var doc = _f.As(DemoDataSeeder.Doctor);
        var summary = await Json(await doc.GetAsync("/api/v1/lab/work/summary?status=all"));
        Assert.True(summary["noEquipment"]!.GetValue<int>() >= 1);
        Assert.Contains(summary["analyzers"]!.AsArray(), a => S(a!["id"]) == Bc780);
        await Json(await doc.GetAsync("/api/v1/lab/work/by-service?status=all"));
        await Json(await doc.GetAsync("/api/v1/lab/work/by-patient?status=all"));

        // Кумулятивний звіт: HGB 62 г/л з BC-20S — критичне, стрілки ↓↓
        var cum = await Json(await doc.GetAsync("/api/v1/lab/reports/cumulative/0b000000-0000-0000-0000-000000000001?group=analyzer"));
        Assert.Contains(cum["criticals"]!.AsArray(), c => S(c!["testCode"]) == "HGB" && S(c["arrow"]) == "↓↓");
        Assert.Contains(cum["groups"]!.AsArray(), g => g!["analyzer"] != null && S(g["analyzer"]) == "Mindray BC-20S");

        var load = await Json(await doc.GetAsync("/api/v1/lab/reports/load"));
        Assert.NotEmpty(load["rows"]!.AsArray());
        var xlsx = await doc.GetAsync("/api/v1/lab/reports/sources?format=xlsx");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType!.MediaType);
        var pdf = await (await doc.GetAsync("/api/v1/lab/reports/unperformed?format=pdf")).Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(await Db(db => db.ReportRuns.AnyAsync(r => r.ReportType == "cumulative")));
    }

    [Fact]
    public void Plausibility_flags_impossible_values_and_ratios()
    {
        Assert.False(PlausibilityChecker.Check("HGB", 950, new Dictionary<string, double>()).IsPlausible);
        Assert.True(PlausibilityChecker.Check("HGB", 120, new Dictionary<string, double> { ["RBC"] = 4.2, ["HCT"] = 36 }).IsPlausible);
        // MCH = 120 / 0.9 = 133 пг — неправдоподібне співвідношення
        Assert.False(PlausibilityChecker.Check("HGB", 120, new Dictionary<string, double> { ["RBC"] = 0.9 }).IsPlausible);
    }
}
