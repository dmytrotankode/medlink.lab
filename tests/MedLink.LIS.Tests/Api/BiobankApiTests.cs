// I-BIO-*: біобанк (FR-BIO-001..006) — штативи, розміщення, пошук, утилізація, терміни, права ролей
using System.Net;
using System.Net.Http.Json;
using MedLink.LIS.Api.Data.Seed;
using static MedLink.LIS.Tests.Api.LabFlow;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class BiobankApiTests
{
    private readonly LisApiFactory _f;
    public BiobankApiTests(LisApiFactory f) => _f = f;

    private async Task<string> NewRack(string? code = null, int rows = 8, int cols = 12)
    {
        var rack = await Json(await _f.As(DemoDataSeeder.Technician).PostAsJsonAsync("/api/v1/lab/biobank/racks",
            new { code = code ?? "R-" + Guid.NewGuid().ToString("N")[..8], name = "Тестовий штатив", temperatureCelsius = -20, rowsCount = rows, colsCount = cols }));
        return rack["id"]!.GetValue<string>();
    }

    [Fact]
    public async Task Rack_create_validates_code_size_role_and_reports_capacity()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var code = "R-" + Guid.NewGuid().ToString("N")[..8];
        var rack = await Json(await tech.PostAsJsonAsync("/api/v1/lab/biobank/racks", new { code, name = "Штатив 4×5", rowsCount = 4, colsCount = 5, temperatureCelsius = -80 }));
        Assert.Equal(20, rack["capacity"]!.GetValue<int>());
        Assert.Equal(0, rack["occupied"]!.GetValue<int>());
        // дубль коду → 409, порожній код / завеликий розмір → 400
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/racks", new { code, name = "дубль" }), HttpStatusCode.Conflict);
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/racks", new { code = "", name = "x" }), HttpStatusCode.BadRequest);
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/racks", new { code = code + "X", name = "x", rowsCount = 27, colsCount = 12 }), HttpStatusCode.BadRequest);
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/racks", new { code = code + "Y", name = "x", rowsCount = 8, colsCount = 51 }), HttpStatusCode.BadRequest);
        // медсестра і реєстратор не працюють із біобанком
        await Problem(await _f.As(DemoDataSeeder.Nurse).PostAsJsonAsync("/api/v1/lab/biobank/racks", new { code = code + "N", name = "x" }), HttpStatusCode.Forbidden);
        await Problem(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync("/api/v1/lab/biobank/racks", new { code = code + "R", name = "x" }), HttpStatusCode.Forbidden);
        // сітка комірок відповідає розміру штатива
        var cells = await Json(await tech.GetAsync($"/api/v1/lab/biobank/racks/{rack["id"]}/cells"));
        Assert.Equal(20, cells["cells"]!.AsArray().Count);
        Assert.Equal("A01", cells["cells"]![0]!["coordinate"]!.GetValue<string>());
        Assert.Equal("D05", cells["cells"]![19]!["coordinate"]!.GetValue<string>());
        // невідомий штатив → 404
        await Problem(await tech.GetAsync("/api/v1/lab/biobank/racks/nope"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Place_search_remove_cycle_changes_sample_status_and_frees_the_cell()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var rackId = await NewRack();
        var o1 = await ReceivedOrder(_f, new[] { "GLU" });
        var o2 = await ReceivedOrder(_f, new[] { "GLU" });

        var placed = await Json(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "B", col = 5, barcode = o1.Barcode }));
        Assert.Equal("B05", placed["coordinate"]!.GetValue<string>());
        // термін зберігання за замовчуванням ≈ 180 діб
        var days = (placed["expiryAt"]!.GetValue<DateTime>() - placed["storedAt"]!.GetValue<DateTime>()).TotalDays;
        Assert.InRange(days, 179.9, 180.1);
        var sample = await Json(await tech.GetAsync($"/api/v1/lab/samples/{o1.Barcode}"));
        Assert.Equal("STORED", sample["status"]!.GetValue<string>());

        // пошук за штрихкодом дає координату, штатив і замовлення
        var found = await Json(await tech.GetAsync($"/api/v1/lab/biobank/search?barcode={o1.Barcode}"));
        Assert.Equal("B05", found["coordinate"]!.GetValue<string>());
        Assert.Equal(rackId, found["rackId"]!.GetValue<string>());
        Assert.False(found["isExpired"]!.GetValue<bool>());
        Assert.Equal("STORED", found["sampleStatus"]!.GetValue<string>());
        await Problem(await tech.GetAsync("/api/v1/lab/biobank/search?barcode=00000000"), HttpStatusCode.NotFound);

        // та сама пробірка вдруге → 409; зайнята комірка → 409; невідома пробірка → 404; поза межами → 400; порожній рядок → 400
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "C", col = 1, barcode = o1.Barcode }), HttpStatusCode.Conflict);
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "b", col = 5, barcode = o2.Barcode }), HttpStatusCode.Conflict);
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "C", col = 1, barcode = "00000000" }), HttpStatusCode.NotFound);
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "I", col = 1, barcode = o2.Barcode }), HttpStatusCode.BadRequest);
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "A", col = 13, barcode = o2.Barcode }), HttpStatusCode.BadRequest);
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "", col = 1, barcode = o2.Barcode }), HttpStatusCode.BadRequest);
        // медсестра не розміщує
        await Problem(await _f.As(DemoDataSeeder.Nurse).PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "A", col = 1, barcode = o2.Barcode }), HttpStatusCode.Forbidden);

        var rack = await Json(await tech.GetAsync($"/api/v1/lab/biobank/racks/{rackId}"));
        Assert.Equal(1, rack["occupied"]!.GetValue<int>());

        // вилучення: медсестра — 403; лаборант — ок, статус пробірки Disposed, комірка вільна
        var cellId = placed["id"]!.GetValue<string>();
        await Problem(await _f.As(DemoDataSeeder.Nurse).PostAsJsonAsync($"/api/v1/lab/biobank/cells/{cellId}/remove", new { reason = "x" }), HttpStatusCode.Forbidden);
        var removed = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/biobank/cells/{cellId}/remove", new { reason = "Термін зберігання минув" }));
        Assert.True(removed["isDisposed"]!.GetValue<bool>());
        Assert.Equal("Термін зберігання минув", removed["disposalReason"]!.GetValue<string>());
        Assert.Equal("DISPOSED", (await Json(await tech.GetAsync($"/api/v1/lab/samples/{o1.Barcode}")))["status"]!.GetValue<string>());
        await Problem(await tech.GetAsync($"/api/v1/lab/biobank/search?barcode={o1.Barcode}"), HttpStatusCode.NotFound);
        await Problem(await tech.PostAsJsonAsync($"/api/v1/lab/biobank/cells/{cellId}/remove", new { reason = "повтор" }), HttpStatusCode.Conflict);
        Assert.Equal(0, (await Json(await tech.GetAsync($"/api/v1/lab/biobank/racks/{rackId}")))["occupied"]!.GetValue<int>());
        // звільнена комірка приймає іншу пробірку
        var again = await Json(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "B", col = 5, barcode = o2.Barcode }));
        Assert.Equal("B05", again["coordinate"]!.GetValue<string>());
        // утилізована пробірка не повертається в біобанк
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "A", col = 1, barcode = o1.Barcode }), HttpStatusCode.Conflict);
        // аудит фіксує розміщення й вилучення
        var audit = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"/api/v1/lab/audit?entity=lab_archive_cell&entityId={cellId}"));
        var actions = (audit["items"] ?? audit).AsArray().Select(a => a!["action"]!.GetValue<string>()).ToList();
        Assert.Contains("PLACE", actions);
        Assert.Contains("REMOVE", actions);
    }

    [Fact]
    public async Task Place_requires_received_sample()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var rackId = await NewRack();
        var pid = await NewPatient(_f);
        var order = await Json(await _f.As(DemoDataSeeder.Admin).PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid, testIds = new[] { "GLU" } }));
        var barcode = order["samples"]![0]!["barcode"]!.GetValue<string>();
        // ще не забрана пробірка (PENDING) не може потрапити в біобанк
        await Problem(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "A", col = 1, barcode }), HttpStatusCode.Conflict);
        Assert.Equal(0, (await Json(await tech.GetAsync($"/api/v1/lab/biobank/racks/{rackId}")))["occupied"]!.GetValue<int>());
    }

    [Fact]
    public async Task Expiry_is_tracked_listed_and_editable()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var rackId = await NewRack();
        var o = await ReceivedOrder(_f, new[] { "GLU" });
        var placed = await Json(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "A", col = 2, barcode = o.Barcode, expiryAt = DateTime.UtcNow.AddDays(-1) }));
        var cellId = placed["id"]!.GetValue<string>();

        var expired = (await Json(await tech.GetAsync("/api/v1/lab/biobank/expired"))).AsArray();
        Assert.Contains(expired, c => c!["barcode"]!.GetValue<string>() == o.Barcode && c["coordinate"]!.GetValue<string>() == "A02");
        Assert.Equal(1, (await Json(await tech.GetAsync($"/api/v1/lab/biobank/racks/{rackId}")))["expiredCount"]!.GetValue<int>());
        Assert.True((await Json(await tech.GetAsync($"/api/v1/lab/biobank/search?barcode={o.Barcode}")))["isExpired"]!.GetValue<bool>());

        // продовження терміну прибирає пробірку зі списку прострочених
        await Json(await tech.PutAsJsonAsync($"/api/v1/lab/biobank/cells/{cellId}", new { expiryAt = DateTime.UtcNow.AddDays(30) }));
        Assert.DoesNotContain((await Json(await tech.GetAsync("/api/v1/lab/biobank/expired"))).AsArray(), c => c!["barcode"]!.GetValue<string>() == o.Barcode);
        await Problem(await _f.As(DemoDataSeeder.Courier).PutAsJsonAsync($"/api/v1/lab/biobank/cells/{cellId}", new { expiryAt = DateTime.UtcNow.AddDays(99) }), HttpStatusCode.Forbidden);
        await Problem(await tech.PutAsJsonAsync("/api/v1/lab/biobank/cells/nope", new { expiryAt = DateTime.UtcNow }), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rack_and_cell_deletion_are_guarded_by_occupancy_and_role()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var doctor = _f.As(DemoDataSeeder.Doctor);
        var rackId = await NewRack(rows: 4, cols: 4);
        var o = await ReceivedOrder(_f, new[] { "GLU" });
        var placed = await Json(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "D", col = 4, barcode = o.Barcode }));
        var cellId = placed["id"]!.GetValue<string>();

        // не можна зменшити штатив нижче зайнятої комірки D04 і не можна видалити непорожній
        await Problem(await tech.PutAsJsonAsync($"/api/v1/lab/biobank/racks/{rackId}", new { code = "SHRINK", name = "x", rowsCount = 3, colsCount = 4 }), HttpStatusCode.Conflict);
        await Problem(await doctor.DeleteAsync($"/api/v1/lab/biobank/racks/{rackId}"), HttpStatusCode.Conflict);
        await Problem(await doctor.DeleteAsync($"/api/v1/lab/biobank/cells/{cellId}"), HttpStatusCode.Conflict);

        await Json(await tech.PostAsJsonAsync($"/api/v1/lab/biobank/cells/{cellId}/remove", new { reason = "очищення" }));
        // лаборант не має права видаляти штатив/комірку (лише адмін і лікар)
        await Problem(await tech.DeleteAsync($"/api/v1/lab/biobank/cells/{cellId}"), HttpStatusCode.Forbidden);
        await Problem(await tech.DeleteAsync($"/api/v1/lab/biobank/racks/{rackId}"), HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.NoContent, (await doctor.DeleteAsync($"/api/v1/lab/biobank/cells/{cellId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await doctor.DeleteAsync($"/api/v1/lab/biobank/racks/{rackId}")).StatusCode);
        await Problem(await tech.GetAsync($"/api/v1/lab/biobank/racks/{rackId}"), HttpStatusCode.NotFound);
    }
}
