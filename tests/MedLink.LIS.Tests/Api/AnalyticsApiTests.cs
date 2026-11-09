// I-TAT-*: аналітика (FR-TAT-001..006) — TAT за етапами, медіана/P90, фільтри, SLA, межі періоду, обсяги, XLSX; I-AUDIT / I-HIST
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ClosedXML.Excel;
using MedLink.LIS.Api.Data.Seed;
using static MedLink.LIS.Tests.Api.LabFlow;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class AnalyticsApiTests
{
    private readonly LisApiFactory _f;
    public AnalyticsApiTests(LisApiFactory f) => _f = f;

    private HttpClient Admin => _f.As(DemoDataSeeder.Admin);

    /// <summary>Замовлення в минулому: час замовлення й забору задаємо, решта етапів — «зараз» (результат введено лаборантом, автоверифікація).</summary>
    private async Task<FlowOrder> HistoricOrder(DateTime orderedAt, int collectAfterMin, bool cito, bool withResult = true)
    {
        var admin = Admin;
        var pid = await NewPatient(_f);
        var o = await Json(await admin.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid, doctorId = DemoDataSeeder.Doctor, departmentId = DemoDataSeeder.Dept, isUrgentCito = cito, orderDatetime = orderedAt, testIds = new[] { "GLU" } }));
        var barcode = o["samples"]![0]!["barcode"]!.GetValue<string>();
        var orderId = o["id"]!.GetValue<string>();
        if (withResult)
        {
            await Json(await admin.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/collect", new { volumeMl = 4.0, collectedAt = orderedAt.AddMinutes(collectAfterMin) }));
            await Json(await admin.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/receive", new { }));
        }
        var detail = await Json(await admin.GetAsync($"/api/v1/lab/orders/{orderId}"));
        var order = new FlowOrder(orderId, pid, new List<string> { barcode }, detail["tests"]!.AsArray().ToDictionary(t => t!["testCode"]!.GetValue<string>(), t => t!["id"]!.GetValue<string>()));
        if (withResult) await Enter(_f, DemoDataSeeder.Technician, order, "GLU", 5.2);
        return order;
    }

    [Fact]
    public async Task Tat_computes_stage_median_p90_totals_sla_and_cito_filter_in_isolated_window()
    {
        var day = new DateTime(2018, 6, 10, 8, 0, 0, DateTimeKind.Utc);
        foreach (var delay in new[] { 10, 20, 30, 40, 100 }) await HistoricOrder(day, delay, cito: false);
        foreach (var delay in new[] { 5, 15 }) await HistoricOrder(day, delay, cito: true);
        await HistoricOrder(day, 0, cito: false, withResult: false); // замовлення без забору — у вибірці, але без завершення

        var q = "from=2018-06-10T00:00:00Z&to=2018-06-10T23:59:59Z";
        var all = await Json(await Admin.GetAsync($"/api/v1/lab/analytics/tat?{q}"));
        Assert.Equal(8, all["orders"]!.GetValue<int>());
        Assert.Equal(7, all["completed"]!.GetValue<int>());
        var st0 = all["stages"]!.AsArray().First(s => s!["name"]!.GetValue<string>() == "order→collected")!;
        Assert.Equal(7, st0["n"]!.GetValue<int>());
        Assert.Equal(20.0, st0["medianMin"]!.GetValue<double>()); // [5,10,15,20,30,40,100]
        // етап «прийом → результат» і «результат → верифікація» — секунди (результат введено одразу, автоверифікація)
        Assert.True(all["stages"]!.AsArray().First(s => s!["name"]!.GetValue<string>() == "received→resulted")!["medianMin"]!.GetValue<double>() < 5);
        Assert.True(all["stages"]!.AsArray().First(s => s!["name"]!.GetValue<string>() == "resulted→verified")!["medianMin"]!.GetValue<double>() < 5);
        // замовлення 2018 року давно прострочені: кожне завершене порушує SLA, а перелік порушень містить їх номери
        Assert.Equal(7, all["slaViolations"]!.GetValue<int>());
        Assert.Equal(7, all["slaViolationOrders"]!.AsArray().Count);
        Assert.All(all["slaViolationOrders"]!.AsArray(), v => Assert.True(v!["overMin"]!.GetValue<double>() > 0));
        Assert.Equal(5, all["waterfall"]!.AsArray().Count);

        // тільки планові: [10,20,30,40,100] → медіана 30, P90 = 40 + 0.6·(100−40) = 76
        var routine = await Json(await Admin.GetAsync($"/api/v1/lab/analytics/tat?{q}&cito=false"));
        Assert.Equal(6, routine["orders"]!.GetValue<int>()); // 5 з результатом + 1 без забору
        Assert.Equal(5, routine["routine"]!["n"]!.GetValue<int>());
        Assert.Equal(0, routine["cito"]!["n"]!.GetValue<int>());
        var rst0 = routine["stages"]!.AsArray().First(s => s!["name"]!.GetValue<string>() == "order→collected")!;
        Assert.Equal(30.0, rst0["medianMin"]!.GetValue<double>());
        Assert.Equal(76.0, rst0["p90Min"]!.GetValue<double>());

        // тільки CITO: [5,15] → медіана 10
        var cito = await Json(await Admin.GetAsync($"/api/v1/lab/analytics/tat?{q}&cito=true"));
        Assert.Equal(2, cito["orders"]!.GetValue<int>());
        Assert.Equal(2, cito["cito"]!["n"]!.GetValue<int>());
        Assert.Equal(10.0, cito["stages"]!.AsArray().First(s => s!["name"]!.GetValue<string>() == "order→collected")!["medianMin"]!.GetValue<double>());
        Assert.All(cito["slaViolationOrders"]!.AsArray(), v => Assert.True(v!["slaHours"]!.GetValue<int>() <= 2)); // для CITO норма ≤ 2 год
    }

    [Fact]
    public async Task Tat_period_edges_are_inclusive_and_date_only_end_covers_the_whole_day()
    {
        var edge = new DateTime(2018, 7, 20, 15, 30, 0, DateTimeKind.Utc);
        await HistoricOrder(edge, 10, cito: false);
        // «До» — дата без часу (так її надсилає вебінтерфейс): весь день включено
        var dateOnly = await Json(await Admin.GetAsync("/api/v1/lab/analytics/tat?from=2018-07-20&to=2018-07-20"));
        Assert.Equal(1, dateOnly["orders"]!.GetValue<int>());
        // точні межі включно
        var exact = await Json(await Admin.GetAsync("/api/v1/lab/analytics/tat?from=2018-07-20T15:30:00Z&to=2018-07-20T15:30:00Z"));
        Assert.Equal(1, exact["orders"]!.GetValue<int>());
        // секундою після / секундою до — поза періодом
        Assert.Equal(0, (await Json(await Admin.GetAsync("/api/v1/lab/analytics/tat?from=2018-07-20T15:30:01Z&to=2018-07-21T00:00:00Z")))["orders"]!.GetValue<int>());
        Assert.Equal(0, (await Json(await Admin.GetAsync("/api/v1/lab/analytics/tat?from=2018-07-19T00:00:00Z&to=2018-07-20T15:29:59Z")))["orders"]!.GetValue<int>());
        // порожній період і перевернутий період — без помилок і без даних
        var empty = await Json(await Admin.GetAsync("/api/v1/lab/analytics/tat?from=2001-01-01&to=2001-01-02"));
        Assert.Equal(0, empty["orders"]!.GetValue<int>());
        Assert.Null(empty["medianMin"]);
        Assert.Equal(0, (await Json(await Admin.GetAsync("/api/v1/lab/analytics/tat?from=2018-07-21&to=2018-07-19")))["orders"]!.GetValue<int>());
        // нерозбірлива дата → 400
        await Problem(await Admin.GetAsync("/api/v1/lab/analytics/tat?from=not-a-date"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Tat_excludes_cancelled_orders_and_filters_by_profile()
    {
        var day = new DateTime(2018, 8, 5, 9, 0, 0, DateTimeKind.Utc);
        var kept = await HistoricOrder(day, 15, cito: false);
        var cancelled = await HistoricOrder(day, 25, cito: false, withResult: false);
        await Json(await Admin.PostAsJsonAsync($"/api/v1/lab/orders/{cancelled.OrderId}/cancel", new { reason = "помилкове замовлення" }));
        var profileOrder = await Json(await Admin.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = await NewPatient(_f), orderDatetime = day, profileIds = new[] { "PROF_LIPID" } }));

        var all = await Json(await Admin.GetAsync("/api/v1/lab/analytics/tat?from=2018-08-05&to=2018-08-05"));
        Assert.Equal(2, all["orders"]!.GetValue<int>()); // скасоване не враховується
        var byProfile = await Json(await Admin.GetAsync("/api/v1/lab/analytics/tat?from=2018-08-05&to=2018-08-05&profileId=PROF_LIPID"));
        Assert.Equal(1, byProfile["orders"]!.GetValue<int>());
        Assert.Equal(1, byProfile["byProfile"]!.AsArray().Count);
        Assert.Equal(0, byProfile["byProfile"]![0]!["completed"]!.GetValue<int>());
        Assert.NotNull(profileOrder["id"]);
        Assert.NotNull(kept.OrderId);
    }

    [Fact]
    public async Task Volume_groups_tests_by_dimension_and_totals_match()
    {
        var day = new DateTime(2018, 9, 12, 7, 0, 0, DateTimeKind.Utc);
        await HistoricOrder(day, 5, cito: false);
        await HistoricOrder(day, 5, cito: true);
        var q = "from=2018-09-12&to=2018-09-12";
        foreach (var g in new[] { "profile", "analyzer", "employee", "day", "test", "department" })
        {
            var v = await Json(await Admin.GetAsync($"/api/v1/lab/analytics/volume?{q}&groupBy={g}"));
            Assert.Equal(2, v["totalTests"]!.GetValue<int>());
            Assert.Equal(2, v["totalOrders"]!.GetValue<int>());
            Assert.Equal(2, v["rows"]!.AsArray().Sum(r => r!["tests"]!.GetValue<int>())); // розбивка не втрачає й не дублює
        }
        var byTest = await Json(await Admin.GetAsync($"/api/v1/lab/analytics/volume?{q}&groupBy=test"));
        Assert.Equal("GLU", byTest["rows"]![0]!["key"]!.GetValue<string>());
        Assert.Equal(1, byTest["rows"]![0]!["cito"]!.GetValue<int>());
        Assert.Equal(2, byTest["rows"]![0]!["autoVerified"]!.GetValue<int>());
        var byDay = await Json(await Admin.GetAsync($"/api/v1/lab/analytics/volume?{q}&groupBy=day"));
        Assert.Equal("2018-09-12", byDay["rows"]![0]!["key"]!.GetValue<string>());
        var byEmp = await Json(await Admin.GetAsync($"/api/v1/lab/analytics/volume?{q}&groupBy=employee"));
        Assert.Equal("Автоверифікація", byEmp["rows"]![0]!["key"]!.GetValue<string>());
    }

    [Fact]
    public async Task Xlsx_export_contains_rows_for_the_period_and_has_spreadsheet_content_type()
    {
        var day = new DateTime(2018, 10, 3, 10, 0, 0, DateTimeKind.Utc);
        var o1 = await HistoricOrder(day, 10, cito: false);
        await HistoricOrder(day, 20, cito: true);
        foreach (var (report, sheet, expectedRows) in new[] { ("orders", "Замовлення", 2), ("tat", "Терміни виконання", 2), ("volume", "Обсяги", 1) })
        {
            var r = await Admin.GetAsync($"/api/v1/lab/analytics/export.xlsx?report={report}&from=2018-10-03&to=2018-10-03");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", r.Content.Headers.ContentType!.MediaType);
            using var wb = new XLWorkbook(await r.Content.ReadAsStreamAsync());
            var ws = wb.Worksheet(sheet);
            Assert.Equal(expectedRows, ws.RowsUsed().Count() - 1); // мінус заголовок
        }
        // у звіті «замовлення» є номери наших замовлень
        var orders = await Admin.GetAsync("/api/v1/lab/analytics/export.xlsx?report=orders&from=2018-10-03&to=2018-10-03");
        using var wb2 = new XLWorkbook(await orders.Content.ReadAsStreamAsync());
        var numbers = wb2.Worksheet("Замовлення").Column(1).CellsUsed().Select(c => c.GetString()).ToList();
        var detail = await Json(await Admin.GetAsync($"/api/v1/lab/orders/{o1.OrderId}"));
        Assert.Contains(detail["orderNumber"]!.GetValue<string>(), numbers);
        var qc = await Admin.GetAsync("/api/v1/lab/analytics/export.xlsx?report=qc&from=2018-10-03&to=2018-10-03");
        Assert.Equal(HttpStatusCode.OK, qc.StatusCode);
    }

    [Fact]
    public async Task Audit_records_every_mutation_with_actor_and_result_history_keeps_versions()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var o = await ReceivedOrder(_f, new[] { "GLU" });
        await Enter(_f, DemoDataSeeder.Technician, o, "GLU", 8.0); // вище норми → ручна верифікація, тому виправлення дозволене
        await Enter(_f, DemoDataSeeder.Technician, o, "GLU", 5.3); // виправлення → нова версія

        // I-HIST-versions
        var hist = (await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test("GLU")}/history"))).AsArray();
        Assert.Equal(3, hist.Count); // ENTERED(v1), SUPERSEDED(v1), CORRECTED(v2)
        Assert.Contains(hist, h => h!["action"]!.GetValue<string>() == "ENTERED");
        Assert.Contains(hist, h => h!["action"]!.GetValue<string>() == "CORRECTED" && h["version"]!.GetValue<int>() == 2);
        Assert.Contains(hist, h => h!["action"]!.GetValue<string>() == "SUPERSEDED" && h["numericValue"]!.GetValue<double>() == 8.0);
        await Problem(await Admin.GetAsync("/api/v1/lab/results/nope/history"), HttpStatusCode.NotFound);

        // I-AUDIT-every-mutation: створення замовлення, введення, виправлення — з виконавцем
        var audit = await Json(await Admin.GetAsync($"/api/v1/lab/audit?entity=lab_test_result&userId={DemoDataSeeder.Technician}&pageSize=200"));
        var items = (audit["items"] ?? audit).AsArray();
        Assert.Contains(items, a => a!["action"]!.GetValue<string>() == "RESULT_ENTERED");
        Assert.Contains(items, a => a!["action"]!.GetValue<string>() == "RESULT_CORRECTED");
        Assert.All(items, a => Assert.Equal(DemoDataSeeder.Technician, a!["userId"]!.GetValue<string>()));
        var created = await Json(await Admin.GetAsync($"/api/v1/lab/audit?entityId={o.OrderId}&pageSize=50"));
        Assert.Contains((created["items"] ?? created).AsArray(), a => a!["action"]!.GetValue<string>() == "CREATE" || a["action"]!.GetValue<string>() == "ORDER_CREATED");
        // фільтр за періодом із датою-без-часу включає сьогоднішні записи
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var todays = await Json(await Admin.GetAsync($"/api/v1/lab/audit?action=RESULT_CORRECTED&from={today}&to={today}&pageSize=200"));
        Assert.Contains((todays["items"] ?? todays).AsArray(), a => a!["entityId"] != null);
        Assert.NotNull(tech);
    }
}
