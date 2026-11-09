// Бланк en/двомовний (FR-PRN-002), архів виданих бланків з хешем, виправлений бланк і дублікат (FR-GAP-010/012),
// біобанк S-11 (розміщення → видача на повтор → списання з актом), імпорт результатів S-14 (dryRun з помилковим рядком)
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class ReportArchiveBiobankImportTests
{
    private const string Patient = "0b000000-0000-0000-0000-000000000004";
    private readonly LisApiFactory _f;
    public ReportArchiveBiobankImportTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }
    private static string S(JsonNode? n) => n!.GetValue<string>();

    /// <summary>Замовлення з одним показником: створено → забрано → прийнято в лабораторії (без результату).</summary>
    private async Task<JsonNode> ReceivedOrder(string testCode = "GLU")
    {
        var reg = _f.As(DemoDataSeeder.Registrar); var nurse = _f.As(DemoDataSeeder.Nurse); var tech = _f.As(DemoDataSeeder.Technician);
        var order = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, testIds = new[] { testCode } }));
        foreach (var s in order["samples"]!.AsArray())
        {
            await Json(await nurse.PostAsJsonAsync($"/api/v1/lab/samples/{S(s!["barcode"])}/collect", new { checklist = new { idVerified = true }, volumeMl = 4.0 }));
            await Json(await tech.PostAsJsonAsync($"/api/v1/lab/samples/{S(s["barcode"])}/receive", new { }));
        }
        return await Json(await tech.GetAsync($"/api/v1/lab/orders/{S(order["id"])}"));
    }

    private async Task<string> ReleasedOrder()
    {
        var order = await ReceivedOrder();
        var tech = _f.As(DemoDataSeeder.Technician); var doctor = _f.As(DemoDataSeeder.Doctor);
        var testId = S(order["tests"]![0]!["id"]);
        await Json(await tech.PutAsJsonAsync($"/api/v1/lab/results/{testId}", new { numericValue = 5.2 }));
        await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/orders/{S(order["id"])}/release", new { }));
        return S(order["id"]);
    }

    [Fact]
    public async Task Report_english_and_bilingual_use_name_en_with_fallback()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
            var glu = await db.Tests.FirstAsync(t => t.Code == "GLU");
            glu.NameEn = "Glucose (serum)";
            await db.SaveChangesAsync();
        }
        var orderId = await ReleasedOrder();
        var uk = await admin.GetStringAsync($"/api/v1/lab/orders/{orderId}/report");
        Assert.Contains("РЕЗУЛЬТАТИ ЛАБОРАТОРНИХ ДОСЛІДЖЕНЬ", uk);
        Assert.Contains("lang=\"uk\"", uk);

        var en = await admin.GetStringAsync($"/api/v1/lab/orders/{orderId}/report?lang=en");
        Assert.Contains("LABORATORY TEST RESULTS", en);
        Assert.Contains("Glucose (serum)", en);
        Assert.Contains("Reference range", en);
        Assert.Contains("lang=\"en\"", en);
        Assert.DoesNotContain("РЕЗУЛЬТАТИ ЛАБОРАТОРНИХ", en);

        var both = await admin.GetStringAsync($"/api/v1/lab/orders/{orderId}/report?lang=both");
        Assert.Contains("РЕЗУЛЬТАТИ ЛАБОРАТОРНИХ ДОСЛІДЖЕНЬ / LABORATORY TEST RESULTS", both);
        Assert.Contains(" / Glucose (serum)", both);

        // запасний варіант: порожня англійська назва → основна назва
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
            (await db.Tests.FirstAsync(t => t.Code == "GLU")).NameEn = null;
            await db.SaveChangesAsync();
        }
        var fallback = await admin.GetStringAsync($"/api/v1/lab/orders/{orderId}/report?lang=en");
        Assert.DoesNotContain("Glucose (serum)", fallback);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report?lang=xx")).StatusCode);
        var pdf = await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report.pdf?lang=en");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);

        // мова за замовчуванням — з налаштувань лабораторії
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/lab/settings/lab", new { reportLanguage = "en" })).StatusCode);
        Assert.Contains("LABORATORY TEST RESULTS", await admin.GetStringAsync($"/api/v1/lab/orders/{orderId}/report"));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/v1/lab/settings/lab", new { reportLanguage = "de" })).StatusCode);
        await admin.PutAsJsonAsync("/api/v1/lab/settings/lab", new { reportLanguage = "uk" });
    }

    [Fact]
    public async Task Release_archives_report_amend_creates_new_version_and_duplicate_is_marked()
    {
        var admin = _f.As(DemoDataSeeder.Admin); var doctor = _f.As(DemoDataSeeder.Doctor);
        var orderId = await ReleasedOrder();

        var list = await Json(await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report/archive"));
        Assert.Single(list.AsArray());
        var v1 = list[0]!;
        Assert.Equal("ISSUED", S(v1["kind"]));
        Assert.Equal(64, S(v1["contentHash"]).Length);
        Assert.True(v1["hasPdf"]!.GetValue<bool>());

        // PDF із архіву — байт у байт, хеш збігається з перевіркою цілісності
        var pdfResp = await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report/archive/1.pdf");
        Assert.Equal(HttpStatusCode.OK, pdfResp.StatusCode);
        Assert.Equal(S(v1["contentHash"]), pdfResp.Headers.GetValues("X-MedLink-Content-Hash").Single());
        var pdfBytes = await pdfResp.Content.ReadAsByteArrayAsync();
        Assert.Equal(S(v1["contentHash"]), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdfBytes)).ToLowerInvariant());
        Assert.True((await Json(await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report/archive/1/verify")))["valid"]!.GetValue<bool>());

        // дублікат: позначка «ДУБЛІКАТ» + лічильник друку
        var dup = await admin.GetStringAsync($"/api/v1/lab/orders/{orderId}/report/archive/1/duplicate");
        Assert.Contains("ДУБЛІКАТ", dup);
        Assert.Contains("РЕЗУЛЬТАТИ ЛАБОРАТОРНИХ", dup);
        var list2 = await Json(await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report/archive"));
        Assert.Equal(1, list2[0]!["duplicatesPrinted"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report/archive/9/duplicate")).StatusCode);

        // виправлення: повторне відкриття з причиною → нова видача → версія 2 «ВИПРАВЛЕНО»
        await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/reopen", new { reason = "Виправлено помилку введення" }));
        await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/release", new { }));
        var list3 = await Json(await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report/archive"));
        Assert.Equal(2, list3.AsArray().Count);
        var v2 = list3[1]!;
        Assert.Equal("AMENDED", S(v2["kind"]));
        Assert.Equal(1, v2["amendsVersionNo"]!.GetValue<int>());
        Assert.Equal("Виправлено помилку введення", S(v2["amendReason"]));
        Assert.NotEqual(S(v1["contentHash"]), S(v2["contentHash"]));

        var live = await admin.GetStringAsync($"/api/v1/lab/orders/{orderId}/report");
        Assert.Contains("ВИПРАВЛЕНО", live);
        Assert.Contains("Замінює бланк версії 1", live);
        // перша версія в архіві лишилась без змін
        Assert.DoesNotContain("Замінює бланк", await admin.GetStringAsync($"/api/v1/lab/orders/{orderId}/report/archive/1/duplicate"));
    }

    [Fact]
    public async Task Biobank_cycle_place_retrieve_for_rerun_dispose_with_act()
    {
        var tech = _f.As(DemoDataSeeder.Technician); var reg = _f.As(DemoDataSeeder.Registrar);
        var order = await ReceivedOrder("GLU");
        var barcode = S(order["samples"]![0]!["barcode"]);
        var rack = await Json(await tech.PostAsJsonAsync("/api/v1/lab/biobank/racks", new { code = "T-" + Guid.NewGuid().ToString("N")[..6], name = "Тестовий штатив", temperatureCelsius = -20, rowsCount = 8, colsCount = 12 }));
        var rackId = S(rack["id"]);

        // роль: реєстратор не розміщує зразки
        Assert.Equal(HttpStatusCode.Forbidden, (await reg.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "A", col = 1, barcode })).StatusCode);
        var placed = await Json(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "C", col = 5, barcode }));
        var cellId = S(placed["id"]);
        Assert.Equal("C05", S(placed["coordinate"]));
        Assert.Equal("STORED", S((await Json(await tech.GetAsync($"/api/v1/lab/biobank/search?barcode={barcode}")))["sampleStatus"]));
        // повторне розміщення тієї ж пробірки і зайнята комірка → 409
        Assert.Equal(HttpStatusCode.Conflict, (await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "D", col = 1, barcode })).StatusCode);

        // видача на повтор: без причини 400; з причиною STORED → PROCESSING, комірка вільна
        Assert.Equal(HttpStatusCode.BadRequest, (await tech.PostAsJsonAsync($"/api/v1/lab/biobank/cells/{cellId}/retrieve", new { reason = "" })).StatusCode);
        var retrieved = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/biobank/cells/{cellId}/retrieve", new { reason = "Повторне визначення глюкози" }));
        Assert.Equal("PROCESSING", S(retrieved["sampleStatus"]));
        Assert.Equal(HttpStatusCode.NotFound, (await tech.GetAsync($"/api/v1/lab/biobank/search?barcode={barcode}")).StatusCode);
        Assert.Equal("PROCESSING", S((await Json(await tech.GetAsync($"/api/v1/lab/orders/{S(order["id"])}")))["samples"]![0]!["status"]));

        // знову на зберігання, потім списання: без причини 400, з причиною DISPOSED + акт
        var placed2 = await Json(await tech.PostAsJsonAsync("/api/v1/lab/biobank/cells/place", new { rackId, row = "C", col = 5, barcode }));
        var cell2 = S(placed2["id"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await tech.PostAsJsonAsync($"/api/v1/lab/biobank/cells/{cell2}/remove", new { reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await tech.GetAsync($"/api/v1/lab/biobank/cells/{cell2}/disposal-act")).StatusCode); // ще не списано
        var removed = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/biobank/cells/{cell2}/remove", new { reason = "Закінчився строк зберігання" }));
        Assert.True(removed["isDisposed"]!.GetValue<bool>());
        Assert.Contains(barcode, S(removed["actNumber"]));
        Assert.Equal("DISPOSED", S((await Json(await tech.GetAsync($"/api/v1/lab/orders/{S(order["id"])}")))["samples"]![0]!["status"]));
        var act = await tech.GetStringAsync($"/api/v1/lab/biobank/cells/{cell2}/disposal-act");
        Assert.Contains("Акт списання біоматеріалу", act);
        Assert.Contains("Закінчився строк зберігання", act);
        Assert.Contains(barcode, act);
        // повторне списання порожньої комірки → 409
        Assert.Equal(HttpStatusCode.Conflict, (await tech.PostAsJsonAsync($"/api/v1/lab/biobank/cells/{cell2}/remove", new { reason = "ще раз" })).StatusCode);
    }

    [Fact]
    public async Task Results_import_dry_run_reports_error_row_and_apply_saves_valid_rows()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var order = await ReceivedOrder("GLU");
        var barcode = S(order["samples"]![0]!["barcode"]);
        var csv = $"barcode;testCode;value;unit\n{barcode};GLU;5,3;mmol/L\n00000000;GLU;4,1;mmol/L\n";

        MultipartFormDataContent Form() => new() { { new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "res.csv" } };
        var dry = await Json(await tech.PostAsync("/api/v1/lab/import/results?dryRun=true", Form()));
        Assert.True(dry["dryRun"]!.GetValue<bool>());
        Assert.Equal(2, dry["total"]!.GetValue<int>());
        Assert.Equal(1, dry["ready"]!.GetValue<int>());
        Assert.Equal(1, dry["errors"]!.GetValue<int>());
        Assert.Contains(dry["rows"]!.AsArray(), r => S(r!["status"]) == "ERROR" && S(r["message"]).Contains("Штрихкод не знайдено"));
        // dryRun нічого не зберігає
        var before = await Json(await tech.GetAsync($"/api/v1/lab/orders/{S(order["id"])}"));
        Assert.Equal("PENDING", S(before["tests"]![0]!["status"]));

        var applied = await Json(await tech.PostAsync("/api/v1/lab/import/results?dryRun=false", Form()));
        Assert.Equal(1, applied["applied"]!.GetValue<int>());
        var after = await Json(await tech.GetAsync($"/api/v1/lab/orders/{S(order["id"])}"));
        Assert.NotEqual("PENDING", S(after["tests"]![0]!["status"]));
        // кур'єр не має права імпортувати
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.As(DemoDataSeeder.Courier).PostAsync("/api/v1/lab/import/results?dryRun=true", Form())).StatusCode);
    }
}
