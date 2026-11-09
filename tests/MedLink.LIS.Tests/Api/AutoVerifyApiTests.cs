// I-WS-autoverify-*: автоверифікація через API (розд. 4.0, FR-WS-006) — правила блокування, delta, ручна верифікація, lockout ВКЯ
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;
using static MedLink.LIS.Tests.Api.LabFlow;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class AutoVerifyApiTests
{
    private readonly LisApiFactory _f;
    public AutoVerifyApiTests(LisApiFactory f) => _f = f;

    private HttpClient Tech => _f.As(DemoDataSeeder.Technician);
    private HttpClient Doctor => _f.As(DemoDataSeeder.Doctor);

    /// <summary>Власний числовий показник без норм (флаг NORMAL), щоб правила перевірялись ізольовано від каскаду норм.</summary>
    private Task<string> NewTest(double? deltaPct = null, bool manual = false) => NewCustomTest(_f, deltaPct, manual);

    private async Task<FlowOrder> Order(string code, string? patientId = null) => await ReceivedOrder(_f, new[] { code }, patientId);

    private async Task<JsonNode> Row(FlowOrder o, string code) => await Json(await Tech.GetAsync($"/api/v1/lab/results/{o.Test(code)}"));

    private async Task<JsonNode> AutoVerify(params string[] ids) => await Json(await Tech.PostAsJsonAsync("/api/v1/lab/worklist/autoverify", new { orderTestIds = ids }));

    [Fact]
    public async Task Normal_result_is_auto_verified_and_released_immediately()
    {
        var code = await NewTest();
        var o = await Order(code);
        var row = await Enter(_f, DemoDataSeeder.Technician, o, code, 10);
        Assert.Equal("AUTO_VERIFIED", row["status"]!.GetValue<string>());
        Assert.True(row["isAutoVerified"]!.GetValue<bool>());
        Assert.Equal("NORMAL", row["flag"]!.GetValue<string>());
        Assert.NotNull(row["releasedAt"]);
        Assert.Null(row["autoVerifyBlockReason"]);
        // верифікований тест уже не потрапляє у повторну автоверифікацію
        var again = await AutoVerify(o.Test(code));
        Assert.Equal(0, again["verified"]!.GetValue<int>());
        Assert.Empty(again["blocked"]!.AsArray());
    }

    [Fact]
    public async Task Out_of_range_and_critical_results_are_blocked_with_reasons_and_stay_blocked_in_batch_retry()
    {
        var high = await Order("GLU");
        var h = await Enter(_f, DemoDataSeeder.Technician, high, "GLU", 8.0);
        Assert.Equal("HIGH", h["flag"]!.GetValue<string>());
        Assert.Equal("RESULTED", h["status"]!.GetValue<string>());
        Assert.False(h["isAutoVerified"]!.GetValue<bool>());
        Assert.Contains("поза нормою", h["autoVerifyBlockReason"]!.GetValue<string>());

        var crit = await Order("K");
        var c = await Enter(_f, DemoDataSeeder.Technician, crit, "K", 6.9);
        Assert.Equal("CRIT_HIGH", c["flag"]!.GetValue<string>());
        Assert.Equal("NEEDS_REVIEW", c["status"]!.GetValue<string>());
        Assert.Contains("Критичне", c["autoVerifyBlockReason"]!.GetValue<string>());

        var batch = await AutoVerify(high.Test("GLU"), crit.Test("K"));
        Assert.Equal(0, batch["verified"]!.GetValue<int>());
        var blocked = batch["blocked"]!.AsArray();
        Assert.Equal(2, blocked.Count);
        Assert.Contains(blocked, b => b!["testCode"]!.GetValue<string>() == "K" && b["reason"]!.GetValue<string>().Contains("Критичне"));
        Assert.Contains(blocked, b => b!["testCode"]!.GetValue<string>() == "GLU");
        // статуси не змінилися, випуск не відбувся
        Assert.Equal("RESULTED", (await Row(high, "GLU"))["status"]!.GetValue<string>());
        Assert.Equal("NEEDS_REVIEW", (await Row(crit, "K"))["status"]!.GetValue<string>());
        Assert.Null((await Row(crit, "K"))["releasedAt"]);
    }

    [Fact]
    public async Task Analyzer_flags_block_autoverification_unless_clean_and_batch_retry_keeps_them_blocked()
    {
        var code = await NewTest();
        var (connId, apiKey, _) = await NewConnector(_f);
        var analyzerId = await NewAnalyzer(_f, connId, ("XQ1", code, 1.0, 0.0));
        var con = ConnectorClient(_f, apiKey);
        async Task<JsonNode> Send(FlowOrder o, string flags)
        {
            var r = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/results", new { analyzerId, receivedAt = DateTime.UtcNow, results = new[] { new { barcode = o.Barcode, analyzerCode = "XQ1", value = "10", flags } } }));
            Assert.Equal(1, r["matched"]!.GetValue<int>());
            return await Row(o, code);
        }

        var flagged = await Order(code);
        var f1 = await Send(flagged, "H");
        Assert.Equal("RESULTED", f1["status"]!.GetValue<string>());
        Assert.Equal("NORMAL", f1["flag"]!.GetValue<string>()); // значення в нормі, блокує лише прапорець приладу
        Assert.Contains("Прапорці аналізатора: H", f1["autoVerifyBlockReason"]!.GetValue<string>());
        var retry = await AutoVerify(flagged.Test(code));
        Assert.Equal(0, retry["verified"]!.GetValue<int>());
        Assert.Contains("Прапорці аналізатора", retry["blocked"]![0]!["reason"]!.GetValue<string>());

        // «N» і порожні прапорці — чисті
        var clean = await Order(code);
        Assert.Equal("AUTO_VERIFIED", (await Send(clean, "N"))["status"]!.GetValue<string>());
        var empty = await Order(code);
        Assert.Equal("AUTO_VERIFIED", (await Send(empty, ""))["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Delta_check_over_threshold_sends_result_to_review_and_batch_retry_keeps_it_blocked()
    {
        var code = await NewTest(deltaPct: 20);
        var patient = await NewPatient(_f);
        var first = await Order(code, patient);
        var r1 = await Enter(_f, DemoDataSeeder.Technician, first, code, 10);
        Assert.Equal("AUTO_VERIFIED", r1["status"]!.GetValue<string>());
        Assert.Null(r1["deltaPercent"]); // попереднього результату немає — дельти немає

        // +15% — у межах порога 20% → автоверифікація, дельта показана
        var second = await Order(code, patient);
        var r2 = await Enter(_f, DemoDataSeeder.Technician, second, code, 11.5);
        Assert.Equal("AUTO_VERIFIED", r2["status"]!.GetValue<string>());
        Assert.Equal(15.0, r2["deltaPercent"]!.GetValue<double>());

        // порівняння — з останнім введеним результатом (11.5), +30.4% → сповіщення, ручна перевірка
        var third = await Order(code, patient);
        var r3 = await Enter(_f, DemoDataSeeder.Technician, third, code, 15);
        Assert.Equal(30.4, r3["deltaPercent"]!.GetValue<double>());
        Assert.True(r3["deltaAlert"]!.GetValue<bool>());
        Assert.Equal("NEEDS_REVIEW", r3["status"]!.GetValue<string>());
        Assert.Contains("Різка зміна", r3["autoVerifyBlockReason"]!.GetValue<string>());
        var retry = await AutoVerify(third.Test(code));
        Assert.Equal(0, retry["verified"]!.GetValue<int>());
        Assert.Contains("Різка зміна", retry["blocked"]![0]!["reason"]!.GetValue<string>());

        // інший пацієнт — свої попередні результати, дельти немає
        var other = await Order(code);
        Assert.Equal("AUTO_VERIFIED", (await Enter(_f, DemoDataSeeder.Technician, other, code, 50))["status"]!.GetValue<string>());
        // лікар може верифікувати вручну; лаборант — ні
        await Problem(await Tech.PostAsJsonAsync($"/api/v1/lab/results/{third.Test(code)}/verify", new { }), HttpStatusCode.Forbidden);
        var verified = await Json(await Doctor.PostAsJsonAsync($"/api/v1/lab/results/{third.Test(code)}/verify", new { }));
        Assert.Equal("VERIFIED", verified["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Test_requiring_manual_verification_is_never_auto_verified()
    {
        var code = await NewTest(manual: true);
        var o = await Order(code);
        var row = await Enter(_f, DemoDataSeeder.Technician, o, code, 5);
        Assert.Equal("RESULTED", row["status"]!.GetValue<string>());
        Assert.Contains("ручної верифікації", row["autoVerifyBlockReason"]!.GetValue<string>());
        var batch = await AutoVerify(o.Test(code));
        Assert.Equal(0, batch["verified"]!.GetValue<int>());
        Assert.Contains("ручної верифікації", batch["blocked"]![0]!["reason"]!.GetValue<string>());
        Assert.Equal("RESULTED", (await Row(o, code))["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Qc_lockout_blocks_autoverification_until_resolved_then_batch_retry_verifies()
    {
        var code = await NewTest();
        var admin = _f.As(DemoDataSeeder.Admin);
        var lockout = await Json(await admin.PostAsJsonAsync("/api/v1/lab/qc/lockouts", new { analyzerId = DemoDataSeeder.Mindray, testCode = code, reason = "Тестове блокування" }));
        var lockoutId = lockout["id"]!.GetValue<string>();

        var o = await Order(code);
        var row = await Enter(_f, DemoDataSeeder.Technician, o, code, 10, DemoDataSeeder.Mindray);
        Assert.Equal("RESULTED", row["status"]!.GetValue<string>());
        Assert.Contains("заблоковано контролем якості", row["autoVerifyBlockReason"]!.GetValue<string>());
        // поки блокування діє, повторна спроба також відмовляє
        Assert.Equal(0, (await AutoVerify(o.Test(code)))["verified"]!.GetValue<int>());
        // блокування стосується лише цього показника цього приладу: той самий результат без приладу — проходить
        var free = await Order(code);
        Assert.Equal("AUTO_VERIFIED", (await Enter(_f, DemoDataSeeder.Technician, free, code, 10))["status"]!.GetValue<string>());

        // лаборант не знімає блокування; лікар знімає з причиною й дією
        await Problem(await Tech.PostAsJsonAsync($"/api/v1/lab/qc/lockouts/{lockoutId}/resolve", new { cause = "x", action = "y" }), HttpStatusCode.Forbidden);
        await Json(await Doctor.PostAsJsonAsync($"/api/v1/lab/qc/lockouts/{lockoutId}/resolve", new { cause = "Перекалібровка", action = "Контроль у нормі" }));
        var retry = await AutoVerify(o.Test(code));
        Assert.Equal(1, retry["verified"]!.GetValue<int>());
        var after = await Row(o, code);
        Assert.Equal("AUTO_VERIFIED", after["status"]!.GetValue<string>());
        Assert.NotNull(after["releasedAt"]);
        Assert.Null(after["autoVerifyBlockReason"]);
    }

    [Fact]
    public async Task Autoverify_endpoint_is_role_protected_and_ignores_unknown_ids()
    {
        var code = await NewTest(manual: true);
        var o = await Order(code);
        await Enter(_f, DemoDataSeeder.Technician, o, code, 5);
        await Problem(await _f.As(DemoDataSeeder.Courier).PostAsJsonAsync("/api/v1/lab/worklist/autoverify", new { orderTestIds = new[] { o.Test(code) } }), HttpStatusCode.Forbidden);
        await Problem(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync("/api/v1/lab/worklist/autoverify", new { orderTestIds = new[] { o.Test(code) } }), HttpStatusCode.Forbidden);
        var unknown = await AutoVerify("00000000-0000-0000-0000-00000000dead");
        Assert.Equal(0, unknown["verified"]!.GetValue<int>());
        Assert.Empty(unknown["blocked"]!.AsArray());
    }
}
