// I-IMP-*: імпорт результатів із файлу (FR-IMP-001..004) — формати CSV/XLSX/XML, dryRun, підтвердження, помилки рядків, ролі
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using ClosedXML.Excel;
using MedLink.LIS.Api.Data.Seed;
using static MedLink.LIS.Tests.Api.LabFlow;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class ImportApiTests
{
    private readonly LisApiFactory _f;
    public ImportApiTests(LisApiFactory f) => _f = f;

    private HttpClient Tech => _f.As(DemoDataSeeder.Technician);

    private static MultipartFormDataContent File(string name, string text) => new() { { new ByteArrayContent(new UTF8Encoding(false).GetBytes(text)), "file", name } };
    private static MultipartFormDataContent File(string name, byte[] bytes) => new() { { new ByteArrayContent(bytes), "file", name } };

    private async Task<JsonNode> Row(FlowOrder o, string code) => await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"/api/v1/lab/results/{o.Test(code)}"));

    [Fact]
    public async Task DryRun_validates_every_row_without_changing_data_and_apply_requires_explicit_confirmation()
    {
        var o = await ReceivedOrder(_f, new[] { "GLU", "UREA" });
        var verified = await ReceivedOrder(_f, new[] { "GLU" });
        await Enter(_f, DemoDataSeeder.Technician, verified, "GLU", 5.0); // вже автоверифіковано → SKIP
        var csv = "barcode;testCode;value;unit\n" +
                  $"{o.Barcode};GLU;5,2;ммоль/л\n" +                 // ok (кома)
                  $"{o.Barcode};UREA;4.1;ммоль/л\n" +                // ok
                  $"{o.Barcode};NOPE;1;x\n" +                        // тесту немає в замовленні
                  "00000000;GLU;5.0;x\n" +                           // невідомий штрихкод
                  $"{o.Barcode};GLU;;x\n" +                          // порожнє значення
                  $"{verified.Barcode};GLU;5.5;x\n";                 // уже верифіковано

        // за замовчуванням (без dryRun) — безпечний попередній перегляд
        var preview = await Json(await Tech.PostAsync("/api/v1/lab/import/results", File("r.csv", csv)));
        Assert.True(preview["dryRun"]!.GetValue<bool>());
        Assert.Equal(6, preview["total"]!.GetValue<int>());
        Assert.Equal(2, preview["ready"]!.GetValue<int>());
        Assert.Equal(3, preview["errors"]!.GetValue<int>());
        Assert.Equal(1, preview["skipped"]!.GetValue<int>());
        var rows = preview["rows"]!.AsArray();
        Assert.Equal(new[] { "READY", "READY", "ERROR", "ERROR", "ERROR", "SKIP" }, rows.Select(r => r!["status"]!.GetValue<string>()).ToArray());
        Assert.Contains("NOPE", rows[2]!["message"]!.GetValue<string>());
        Assert.Contains("Штрихкод", rows[3]!["message"]!.GetValue<string>());
        Assert.Contains("обов'язков", rows[4]!["message"]!.GetValue<string>());
        Assert.Contains("верифіковано", rows[5]!["message"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(rows[0]!["orderNumber"]!.GetValue<string>())); // рядок показує номер замовлення
        Assert.Equal(2, rows[0]!["line"]!.GetValue<int>()); // нумерація рядків файлу (1 — заголовок)
        // даних не змінено
        Assert.Equal("PENDING", (await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"/api/v1/lab/orders/{o.OrderId}")))["tests"]!.AsArray().First(t => t!["testCode"]!.GetValue<string>() == "GLU")!["status"]!.GetValue<string>());
        Assert.Null((await Row(o, "GLU"))["numericValue"]);

        // явне dryRun=false застосовує лише готові рядки
        var applied = await Json(await Tech.PostAsync("/api/v1/lab/import/results?dryRun=false", File("r.csv", csv)));
        Assert.False(applied["dryRun"]!.GetValue<bool>());
        Assert.Equal(2, applied["applied"]!.GetValue<int>());
        Assert.Equal(3, applied["errors"]!.GetValue<int>());
        Assert.Equal(new[] { "APPLIED", "APPLIED", "ERROR", "ERROR", "ERROR", "SKIP" }, applied["rows"]!.AsArray().Select(r => r!["status"]!.GetValue<string>()).ToArray());
        var glu = await Row(o, "GLU");
        Assert.Equal(5.2, glu["numericValue"]!.GetValue<double>());
        Assert.Equal("AUTO_VERIFIED", glu["status"]!.GetValue<string>()); // імпорт проходить той самий конвеєр, що й ручне введення
        Assert.Equal(4.1, (await Row(o, "UREA"))["numericValue"]!.GetValue<double>());
        var hist = (await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"/api/v1/lab/results/{o.Test("GLU")}/history"))).AsArray();
        Assert.Contains(hist, h => (h!["comment"]?.GetValue<string>() ?? "").Contains("Імпорт із файлу r.csv"));
        Assert.Equal(5.0, (await Row(verified, "GLU"))["numericValue"]!.GetValue<double>()); // верифікований не перезаписано
        // аудит імпорту
        Assert.Contains("IMPORT", await _f.As(DemoDataSeeder.Admin).GetStringAsync("/api/v1/lab/audit?action=IMPORT&pageSize=20"));
    }

    [Fact]
    public async Task Existing_unverified_result_is_marked_overwrite_and_replaced_with_new_version()
    {
        var o = await ReceivedOrder(_f, new[] { "GLU" });
        await Enter(_f, DemoDataSeeder.Technician, o, "GLU", 8.0); // вище норми — чекає верифікації (RESULTED)
        var csv = $"barcode,testCode,value\n{o.Barcode},glu,8.4\n";
        var preview = await Json(await Tech.PostAsync("/api/v1/lab/import/results?dryRun=true", File("o.csv", csv)));
        Assert.Equal("OVERWRITE", preview["rows"]![0]!["status"]!.GetValue<string>());
        Assert.Equal(1, preview["ready"]!.GetValue<int>());
        await Json(await Tech.PostAsync("/api/v1/lab/import/results?dryRun=false", File("o.csv", csv)));
        var row = await Row(o, "GLU");
        Assert.Equal(8.4, row["numericValue"]!.GetValue<double>());
        var hist = (await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"/api/v1/lab/results/{o.Test("GLU")}/history"))).AsArray();
        Assert.Contains(hist, h => h!["action"]!.GetValue<string>() == "CORRECTED" && h["version"]!.GetValue<int>() == 2);
    }

    [Fact]
    public async Task Headerless_csv_tab_separator_and_ukrainian_headers_are_understood()
    {
        var a = await ReceivedOrder(_f, new[] { "GLU" });
        var b = await ReceivedOrder(_f, new[] { "GLU" });
        var c = await ReceivedOrder(_f, new[] { "GLU" });
        // без заголовка: перший стовпець — штрихкод (цифри)
        var noHeader = await Json(await Tech.PostAsync("/api/v1/lab/import/results?dryRun=true", File("n.csv", $"{a.Barcode},GLU,5.1,ммоль/л\n")));
        Assert.Equal("READY", noHeader["rows"]![0]!["status"]!.GetValue<string>());
        Assert.Equal(1, noHeader["total"]!.GetValue<int>());
        // табуляція + українські заголовки
        var tab = await Json(await Tech.PostAsync("/api/v1/lab/import/results?dryRun=false", File("t.csv", $"штрихкод\tтест\tрезультат\tод\n{b.Barcode}\tGLU\t5.3\tммоль/л\n")));
        Assert.Equal(1, tab["applied"]!.GetValue<int>());
        Assert.Equal(5.3, (await Row(b, "GLU"))["numericValue"]!.GetValue<double>());
        // порожні рядки ігноруються, лапки знімаються
        var quoted = await Json(await Tech.PostAsync("/api/v1/lab/import/results?dryRun=false", File("q.csv", $"\"barcode\";\"testCode\";\"value\"\n\n\"{c.Barcode}\";\"GLU\";\"5.4\"\n\n")));
        Assert.Equal(1, quoted["total"]!.GetValue<int>());
        Assert.Equal(5.4, (await Row(c, "GLU"))["numericValue"]!.GetValue<double>());
    }

    [Fact]
    public async Task Xlsx_and_xml_files_are_imported_like_csv()
    {
        var x = await ReceivedOrder(_f, new[] { "GLU" });
        var y = await ReceivedOrder(_f, new[] { "GLU" });
        using var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("res");
            ws.Cell(1, 1).Value = "barcode"; ws.Cell(1, 2).Value = "testCode"; ws.Cell(1, 3).Value = "value"; ws.Cell(1, 4).Value = "unit";
            ws.Cell(2, 1).Value = x.Barcode; ws.Cell(2, 2).Value = "GLU"; ws.Cell(2, 3).Value = "5.6"; ws.Cell(2, 4).Value = "ммоль/л";
            wb.SaveAs(ms);
        }
        var xlsx = await Json(await Tech.PostAsync("/api/v1/lab/import/results?dryRun=false", File("r.xlsx", ms.ToArray())));
        Assert.Equal(1, xlsx["applied"]!.GetValue<int>());
        Assert.Equal(5.6, (await Row(x, "GLU"))["numericValue"]!.GetValue<double>());

        var xml = $"<results><result barcode=\"{y.Barcode}\" testCode=\"GLU\" value=\"4.9\" unit=\"ммоль/л\"/></results>";
        var imported = await Json(await Tech.PostAsync("/api/v1/lab/import/results?dryRun=false", File("r.xml", xml)));
        Assert.Equal(1, imported["applied"]!.GetValue<int>());
        Assert.Equal(4.9, (await Row(y, "GLU"))["numericValue"]!.GetValue<double>());
        // зламаний XML — помилка клієнта, не 500
        var broken = await Tech.PostAsync("/api/v1/lab/import/results?dryRun=true", File("b.xml", "<results><result"));
        Assert.True((int)broken.StatusCode is >= 400 and < 500, $"зламаний XML дав {(int)broken.StatusCode}");
    }

    [Fact]
    public async Task Empty_file_missing_file_and_unauthorised_roles_are_rejected()
    {
        await Problem(await Tech.PostAsync("/api/v1/lab/import/results", File("e.csv", "")), HttpStatusCode.BadRequest);
        await Problem(await Tech.PostAsync("/api/v1/lab/import/results", File("h.csv", "barcode;testCode;value\n")), HttpStatusCode.BadRequest); // лише заголовок
        await Problem(await Tech.PostAsync("/api/v1/lab/import/results", new MultipartFormDataContent { { new StringContent("x"), "other" } }), HttpStatusCode.BadRequest);
        var o = await ReceivedOrder(_f, new[] { "GLU" });
        var csv = $"barcode;testCode;value\n{o.Barcode};GLU;5.0\n";
        await Problem(await _f.As(DemoDataSeeder.Nurse).PostAsync("/api/v1/lab/import/results?dryRun=false", File("r.csv", csv)), HttpStatusCode.Forbidden);
        await Problem(await _f.As(DemoDataSeeder.Registrar).PostAsync("/api/v1/lab/import/results?dryRun=false", File("r.csv", csv)), HttpStatusCode.Forbidden);
        await Problem(await _f.As(DemoDataSeeder.Courier).PostAsync("/api/v1/lab/import/results", File("r.csv", csv)), HttpStatusCode.Forbidden);
        Assert.Null((await Row(o, "GLU"))["numericValue"]); // заборонені ролі нічого не змінили
    }

    [Fact]
    public async Task Apply_reports_pipeline_errors_per_row_when_sample_is_not_received_yet()
    {
        var pid = await NewPatient(_f);
        var order = await Json(await _f.As(DemoDataSeeder.Admin).PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid, testIds = new[] { "GLU" } }));
        var barcode = order["samples"]![0]!["barcode"]!.GetValue<string>();
        var good = await ReceivedOrder(_f, new[] { "GLU" });
        var csv = $"barcode;testCode;value\n{barcode};GLU;5.0\n{good.Barcode};GLU;5.1\n";
        var res = await Json(await Tech.PostAsync("/api/v1/lab/import/results?dryRun=false", File("p.csv", csv)));
        // проба ще не прийнята: рядок стає ERROR з поясненням, але інші рядки файлу застосовано
        Assert.Equal("ERROR", res["rows"]![0]!["status"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(res["rows"]![0]!["message"]!.GetValue<string>()));
        Assert.Equal("APPLIED", res["rows"]![1]!["status"]!.GetValue<string>());
        Assert.Equal(1, res["applied"]!.GetValue<int>());
    }
}
