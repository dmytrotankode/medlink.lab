// FR-VAL-002/003 (S-06): панічні дзвінки та заборона видачі; FR-GAP-031: журнал калібрувань, статуси, попередження/блокування результатів
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class CalibrationPanicApiTests
{
    private const string Patient = "0b000000-0000-0000-0000-000000000003";
    private const string Base = "/api/v1/lab";
    private readonly LisApiFactory _f;
    public CalibrationPanicApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static async Task<JsonNode> Problem(HttpResponseMessage r, HttpStatusCode status, string? code = null)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"очікувано {(int)status}, отримано {(int)r.StatusCode}: {text}");
        var node = JsonNode.Parse(text)!;
        Assert.NotNull(node["title"]);
        if (code != null) Assert.Equal(code, node["code"]?.GetValue<string>());
        return node;
    }

    /// <summary>Замовлення з одним показником: створено, забрано, прийнято в лабораторії. Повертає (orderId, orderTestId).</summary>
    private async Task<(string orderId, string testId)> ReceivedOrder(string testCode)
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var order = await Json(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync($"{Base}/orders", new { patientId = Patient, doctorId = DemoDataSeeder.Doctor, departmentId = DemoDataSeeder.CollectionPoint, testIds = new[] { testCode } }));
        foreach (var s in order["samples"]!.AsArray())
        {
            var barcode = s!["barcode"]!.GetValue<string>();
            await Json(await admin.PostAsJsonAsync($"{Base}/samples/{barcode}/collect", new { checklist = new { idVerified = true, fasting = true, orderOfDraw = true, mixing = true }, volumeMl = 3.0 }));
            await Json(await admin.PostAsJsonAsync($"{Base}/samples/{barcode}/receive", new { }));
        }
        var orderId = order["id"]!.GetValue<string>();
        var detail = await Json(await admin.GetAsync($"{Base}/orders/{orderId}"));
        var testId = detail["tests"]!.AsArray().First(t => t!["testCode"]!.GetValue<string>() == testCode)!["id"]!.GetValue<string>();
        return (orderId, testId);
    }

    private async Task SetBlock(bool block) => await Json(await _f.As(DemoDataSeeder.Admin).PutAsJsonAsync($"{Base}/settings/lab", new { blockOnExpiredCalibration = block }));

    // ------------------------------------------------------------------ паніка

    [Fact]
    public async Task Release_with_critical_result_requires_registered_panic_call()
    {
        var doctor = _f.As(DemoDataSeeder.Doctor);
        var tech = _f.As(DemoDataSeeder.Technician);
        var (orderId, testId) = await ReceivedOrder("K");

        var row = await Json(await tech.PutAsJsonAsync($"{Base}/results/{testId}", new { numericValue = 7.9 }));
        Assert.Equal("CRIT_HIGH", row["flag"]!.GetValue<string>());
        var resultId = row["resultId"]!.GetValue<string>();
        await Json(await doctor.PostAsJsonAsync($"{Base}/results/{testId}/verify", new { comment = "Гіперкаліємія, потрібен дзвінок" }));

        // Дзвінка немає: у списку очікуваних, видача заборонена з зрозумілим повідомленням
        var pending = await Json(await doctor.GetAsync($"{Base}/panic/pending"));
        Assert.Contains(pending.AsArray(), p => p!["orderTestId"]!.GetValue<string>() == testId);
        var blocked = await Problem(await doctor.PostAsJsonAsync($"{Base}/orders/{orderId}/release", new { }), HttpStatusCode.Conflict, "PANIC_CALL_REQUIRED");
        var detail = blocked["detail"]!.GetValue<string>();
        Assert.Contains("K", detail);
        Assert.Contains("Дзвінок", detail);
        Assert.Contains("Валідація та критичні значення", detail);
        Assert.Equal("COMPLETED", (await Json(await doctor.GetAsync($"{Base}/orders/{orderId}")))["status"]!.GetValue<string>());

        // Реєстрація дзвінка: валідація
        await Problem(await doctor.PostAsJsonAsync($"{Base}/panic-calls", new { resultId, phone = "+380501112233" }), HttpStatusCode.BadRequest);
        await Problem(await doctor.PostAsJsonAsync($"{Base}/panic-calls", new { resultId, doctorName = "Лікар" }), HttpStatusCode.BadRequest);
        await Problem(await doctor.PostAsJsonAsync($"{Base}/panic-calls", new { resultId = "no-such", doctorName = "Лікар", phone = "1" }), HttpStatusCode.NotFound);
        await Problem(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync($"{Base}/panic-calls", new { resultId, doctorName = "Лікар", phone = "1" }), HttpStatusCode.Forbidden);
        // і досі заблоковано
        await Problem(await doctor.PostAsJsonAsync($"{Base}/orders/{orderId}/release", new { }), HttpStatusCode.Conflict, "PANIC_CALL_REQUIRED");

        // read-back не підтверджено — лише попередження, запис дозволений (FR-VAL-003), видача відкривається
        var call = await Json(await tech.PostAsJsonAsync($"{Base}/panic-calls", new { resultId, doctorName = "Іванчук М.П.", phone = "+380501234567", department = "Терапія", readbackConfirmed = false, comments = "Не вдалося повторити значення" }));
        Assert.Equal(resultId, call["resultId"]!.GetValue<string>());
        Assert.False(call["readbackConfirmed"]!.GetValue<bool>());
        Assert.Equal(orderId, call["orderId"]!.GetValue<string>());
        var pendingAfter = await Json(await doctor.GetAsync($"{Base}/panic/pending"));
        Assert.DoesNotContain(pendingAfter.AsArray(), p => p!["orderTestId"]!.GetValue<string>() == testId);
        var list = await Json(await doctor.GetAsync($"{Base}/panic-calls"));
        Assert.Contains(list.AsArray(), c => c!["id"]!.GetValue<string>() == call["id"]!.GetValue<string>());

        // оновлення (підтвердження read-back) і видача
        var upd = await Json(await doctor.PutAsJsonAsync($"{Base}/panic-calls/{call["id"]!.GetValue<string>()}", new { resultId, readbackConfirmed = true }));
        Assert.True(upd["readbackConfirmed"]!.GetValue<bool>());
        var released = await Json(await doctor.PostAsJsonAsync($"{Base}/orders/{orderId}/release", new { }));
        Assert.Equal("RELEASED", released["status"]!.GetValue<string>());

        // аудит реєстрації дзвінка
        var audit = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"{Base}/audit?entity=lab_panic_call"));
        Assert.Contains("lab_panic_call", audit.ToJsonString());
    }

    [Fact]
    public async Task Release_without_critical_results_is_not_blocked_by_panic_rule()
    {
        var (orderId, testId) = await ReceivedOrder("GLU");
        var row = await Json(await _f.As(DemoDataSeeder.Technician).PutAsJsonAsync($"{Base}/results/{testId}", new { numericValue = 5.1 }));
        Assert.Equal("AUTO_VERIFIED", row["status"]!.GetValue<string>());
        var released = await Json(await _f.As(DemoDataSeeder.Doctor).PostAsJsonAsync($"{Base}/orders/{orderId}/release", new { }));
        Assert.Equal("RELEASED", released["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Panic_call_delete_is_doctor_only_and_blocks_release_again()
    {
        var doctor = _f.As(DemoDataSeeder.Doctor);
        var (orderId, testId) = await ReceivedOrder("K");
        var row = await Json(await _f.As(DemoDataSeeder.Technician).PutAsJsonAsync($"{Base}/results/{testId}", new { numericValue = 1.9 }));
        Assert.Equal("CRIT_LOW", row["flag"]!.GetValue<string>());
        await Json(await doctor.PostAsJsonAsync($"{Base}/results/{testId}/verify", new { comment = "Гіпокаліємія" }));
        var call = await Json(await doctor.PostAsJsonAsync($"{Base}/panic-calls", new { resultId = row["resultId"]!.GetValue<string>(), doctorName = "Петренко", phone = "+380671112233", readbackConfirmed = true }));
        var id = call["id"]!.GetValue<string>();
        await Problem(await _f.As(DemoDataSeeder.Technician).DeleteAsync($"{Base}/panic-calls/{id}"), HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.NoContent, (await doctor.DeleteAsync($"{Base}/panic-calls/{id}")).StatusCode);
        await Problem(await doctor.PostAsJsonAsync($"{Base}/orders/{orderId}/release", new { }), HttpStatusCode.Conflict, "PANIC_CALL_REQUIRED");
    }

    // ------------------------------------------------------------------ калібрування

    private static object Cal(string analyzer, string? test, string lot, DateTime? performed = null, int? validDays = 30, bool accepted = true, string? reason = null, string? reagentLotId = null)
        => new { analyzerId = analyzer, testCode = test, calibratorLot = lot, calibratorName = "Мультикалібратор", performedAt = performed, validDays, accepted, rejectReason = reason, reagentLotId, calibrationValue = 1.02, unit = "ум. од." };

    [Fact]
    public async Task Calibration_journal_crud_validation_roles_and_statuses()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var doctor = _f.As(DemoDataSeeder.Doctor);
        const string test = "UREA";

        // права: реєстратор не вносить калібрування
        await Problem(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Mindray, test, "CAL-1")), HttpStatusCode.Forbidden);
        // валідація
        await Problem(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal("no-analyzer", test, "CAL-1")), HttpStatusCode.BadRequest);
        await Problem(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Mindray, "NO_SUCH_TEST", "CAL-1")), HttpStatusCode.BadRequest);
        await Problem(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Mindray, test, "")), HttpStatusCode.BadRequest);
        await Problem(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Mindray, test, "CAL-1", validDays: null)), HttpStatusCode.BadRequest);
        await Problem(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Mindray, test, "CAL-1", accepted: false)), HttpStatusCode.BadRequest);
        await Problem(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Mindray, test, "CAL-1", performed: DateTime.UtcNow.AddDays(2))), HttpStatusCode.BadRequest);
        await Problem(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Mindray, test, "CAL-1", reagentLotId: "no-lot")), HttpStatusCode.BadRequest);

        // лот реагенту іншого аналізатора не підходить; свого — підходить
        var foreign = await Json(await tech.PostAsJsonAsync($"{Base}/reagents/lots", new { analyzerId = DemoDataSeeder.Sysmex, reagentName = "Чужий реагент", lotNumber = "FOREIGN-1", testsInitial = 100, expiryDate = DateTime.UtcNow.AddMonths(6) }));
        await Problem(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Mindray, test, "CAL-1", reagentLotId: foreign["id"]!.GetValue<string>())), HttpStatusCode.BadRequest);
        var lot = await Json(await tech.PostAsJsonAsync($"{Base}/reagents/lots", new { analyzerId = DemoDataSeeder.Mindray, testCode = test, reagentName = "Urea UV", lotNumber = "UREA-LOT-CAL", testsInitial = 200, expiryDate = DateTime.UtcNow.AddMonths(6) }));
        var lotId = lot["id"]!.GetValue<string>();

        var created = await Json(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Mindray, test, "CAL-LOT-77", reagentLotId: lotId)));
        var id = created["id"]!.GetValue<string>();
        Assert.Equal("VALID", created["status"]!.GetValue<string>());
        Assert.Equal("Дійсне", created["statusText"]!.GetValue<string>());
        Assert.Equal("UREA-LOT-CAL", created["reagentLotNumber"]!.GetValue<string>());
        Assert.Equal(DemoDataSeeder.Technician, created["performedById"]!.GetValue<string>());
        Assert.True(created["daysLeft"]!.GetValue<int>() >= 29);
        Assert.Equal(created["validUntil"]!.GetValue<DateTime>(), created["nextDueAt"]!.GetValue<DateTime>());

        // журнал, фільтри, зведення, перевірка
        var list = await Json(await tech.GetAsync($"{Base}/calibrations?analyzerId={DemoDataSeeder.Mindray}&testCode={test}"));
        Assert.Contains(list.AsArray(), c => c!["id"]!.GetValue<string>() == id);
        Assert.Empty((await Json(await tech.GetAsync($"{Base}/calibrations?analyzerId={DemoDataSeeder.Mindray}&testCode={test}&status=EXPIRED"))).AsArray());
        var summary = await Json(await tech.GetAsync($"{Base}/calibrations/summary?analyzerId={DemoDataSeeder.Mindray}"));
        Assert.Contains(summary.AsArray(), c => c!["id"]!.GetValue<string>() == id);
        var check = await Json(await tech.GetAsync($"{Base}/calibrations/check?analyzerId={DemoDataSeeder.Mindray}&testCode={test}"));
        Assert.Equal("VALID", check["status"]!.GetValue<string>());
        Assert.False(check["isProblem"]!.GetValue<bool>());

        // Лот реагенту став неактивним → калібрування недійсне
        await Json(await tech.PutAsJsonAsync($"{Base}/reagents/lots/{lotId}", new { analyzerId = DemoDataSeeder.Mindray, testCode = test, reagentName = "Urea UV", lotNumber = "UREA-LOT-CAL", testsInitial = 200, expiryDate = DateTime.UtcNow.AddMonths(6), isActive = false }));
        var lotBad = await Json(await tech.GetAsync($"{Base}/calibrations/check?analyzerId={DemoDataSeeder.Mindray}&testCode={test}"));
        Assert.Equal("LOT_INVALID", lotBad["status"]!.GetValue<string>());
        Assert.True(lotBad["isProblem"]!.GetValue<bool>());

        // редагування: лише коментар і планова дата; видалення — лікар/адмін з причиною
        var upd = await Json(await tech.PutAsJsonAsync($"{Base}/calibrations/{id}", new { comment = "Уточнено лаборантом" }));
        Assert.Equal("Уточнено лаборантом", upd["comment"]!.GetValue<string>());
        await Problem(await tech.DeleteAsync($"{Base}/calibrations/{id}?reason=помилка"), HttpStatusCode.Forbidden);
        await Problem(await doctor.DeleteAsync($"{Base}/calibrations/{id}"), HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.NoContent, (await doctor.DeleteAsync($"{Base}/calibrations/{id}?reason=Внесено помилково")).StatusCode);
        await Problem(await doctor.GetAsync($"{Base}/calibrations/{id}"), HttpStatusCode.NotFound);
        Assert.Equal("NONE", (await Json(await tech.GetAsync($"{Base}/calibrations/check?analyzerId={DemoDataSeeder.Mindray}&testCode={test}")))["status"]!.GetValue<string>());

        // аудит: створення і видалення
        var audit = (await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"{Base}/audit?entity=lab_calibration"))).ToJsonString();
        Assert.Contains("lab_calibration", audit);
        Assert.Contains("SOFT_DELETE", audit);
    }

    [Fact]
    public async Task Calibration_status_expired_rejected_expiring_and_test_scope()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        const string test = "ALB";
        string Chk(JsonNode n) => n["status"]!.GetValue<string>();
        async Task<JsonNode> Check(string t) => await Json(await tech.GetAsync($"{Base}/calibrations/check?analyzerId={DemoDataSeeder.Sysmex}&testCode={t}"));

        Assert.Equal("NONE", Chk(await Check(test)));
        // 40 днів тому з терміном 30 → протерміноване
        var expired = await Json(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Sysmex, test, "OLD", performed: DateTime.UtcNow.AddDays(-40), validDays: 30)));
        Assert.Equal("EXPIRED", expired["status"]!.GetValue<string>());
        var c1 = await Check(test);
        Assert.Equal("EXPIRED", Chk(c1));
        Assert.True(c1["isProblem"]!.GetValue<bool>());
        Assert.False(c1["blocking"]!.GetValue<bool>()); // за замовчуванням лише попередження
        Assert.Contains("протерміноване", c1["message"]!.GetValue<string>());
        // «загальне» калібрування приладу (без показника) діє на показники без власних записів
        await Json(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Sysmex, null, "GEN", validDays: 90)));
        Assert.Equal("VALID", Chk(await Check("CRP")));
        // але для ALB діє власний (протермінований) запис
        Assert.Equal("EXPIRED", Chk(await Check(test)));
        // свіже калібрування показника виправляє стан; спливаюче — нагадування
        await Json(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Sysmex, test, "NEW", validDays: 5)));
        Assert.Equal("EXPIRING", Chk(await Check(test)));
        // відхилене калібрування робить показник проблемним, навіть якщо попереднє ще діє
        await Json(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Sysmex, test, "BAD", validDays: 30, accepted: false, reason: "Не пройшов критерії приймання")));
        var rej = await Check(test);
        Assert.Equal("REJECTED", Chk(rej));
        Assert.True(rej["isProblem"]!.GetValue<bool>());
        // фільтр за статусом
        var onlyRej = await Json(await tech.GetAsync($"{Base}/calibrations?analyzerId={DemoDataSeeder.Sysmex}&status=REJECTED"));
        Assert.All(onlyRej.AsArray(), c => Assert.Equal("REJECTED", c!["status"]!.GetValue<string>()));
        Assert.NotEmpty(onlyRej.AsArray());

        // прибирання, щоб не впливати на інші тести
        var doctor = _f.As(DemoDataSeeder.Doctor);
        foreach (var c in (await Json(await tech.GetAsync($"{Base}/calibrations?analyzerId={DemoDataSeeder.Sysmex}"))).AsArray())
            await doctor.DeleteAsync($"{Base}/calibrations/{c!["id"]!.GetValue<string>()}?reason=Очищення тесту");
        Assert.Equal("NONE", Chk(await Check(test)));
    }

    [Fact]
    public async Task Expired_calibration_warns_blocks_verification_and_manual_entry_by_setting()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var doctor = _f.As(DemoDataSeeder.Doctor);
        const string test = "FE";
        string calId = "";
        try
        {
            await SetBlock(false);
            var (_, ok) = await ReceivedOrder("CA");
            // без записів у журналі обмежень немає
            var clean = await Json(await tech.PutAsJsonAsync($"{Base}/results/{ok}", new { numericValue = 2.3, analyzerId = DemoDataSeeder.Mindray }));
            Assert.Null(clean["calibrationWarning"]);

            var cal = await Json(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Mindray, test, "EXP-1", performed: DateTime.UtcNow.AddDays(-60), validDays: 30)));
            calId = cal["id"]!.GetValue<string>();

            // 1) лише попередження: результат прийнято, але не автоверифікується
            var (_, t1) = await ReceivedOrder(test);
            var row = await Json(await tech.PutAsJsonAsync($"{Base}/results/{t1}", new { numericValue = 15.0, analyzerId = DemoDataSeeder.Mindray }));
            Assert.NotNull(row["calibrationWarning"]);
            Assert.Contains("протерміноване", row["calibrationWarning"]!.GetValue<string>());
            Assert.Equal("NEEDS_REVIEW", row["status"]!.GetValue<string>());
            Assert.NotNull(row["autoVerifyBlockReason"]);

            // 2) блокування увімкнено: ручне введення заборонено, верифікація — лише з override і коментарем
            await SetBlock(true);
            var (_, t2) = await ReceivedOrder(test);
            await Problem(await tech.PutAsJsonAsync($"{Base}/results/{t2}", new { numericValue = 15.0, analyzerId = DemoDataSeeder.Mindray }), HttpStatusCode.Conflict, "CALIBRATION_EXPIRED");

            await Problem(await doctor.PostAsJsonAsync($"{Base}/results/{t1}/verify", new { comment = "ок" }), HttpStatusCode.Conflict, "CALIBRATION_EXPIRED");
            await Problem(await doctor.PostAsJsonAsync($"{Base}/results/{t1}/verify", new { @override = true }), HttpStatusCode.BadRequest);
            var verified = await Json(await doctor.PostAsJsonAsync($"{Base}/results/{t1}/verify", new { @override = true, comment = "Калібрування виконано, запис у журнал буде внесено" }));
            Assert.Equal("VERIFIED", verified["status"]!.GetValue<string>());

            // 3) нове дійсне калібрування знімає обмеження
            await Json(await tech.PostAsJsonAsync($"{Base}/calibrations", Cal(DemoDataSeeder.Mindray, test, "FRESH", validDays: 60)));
            var (_, t3) = await ReceivedOrder(test);
            var fine = await Json(await tech.PutAsJsonAsync($"{Base}/results/{t3}", new { numericValue = 15.0, analyzerId = DemoDataSeeder.Mindray }));
            Assert.Null(fine["calibrationWarning"]);

            // аудит попередження
            var audit = (await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"{Base}/audit?entity=lab_order_test"))).ToJsonString();
            Assert.Contains("CALIBRATION_WARNING", audit);
        }
        finally
        {
            await SetBlock(false);
            foreach (var c in (await Json(await tech.GetAsync($"{Base}/calibrations?analyzerId={DemoDataSeeder.Mindray}&testCode={test}"))).AsArray())
                await doctor.DeleteAsync($"{Base}/calibrations/{c!["id"]!.GetValue<string>()}?reason=Очищення тесту");
        }
        Assert.NotEqual("", calId);
    }
}
