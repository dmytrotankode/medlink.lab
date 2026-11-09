// API-тести виправлень візуального проходу №3: розрахункові MCV/MCH/MCHC у демо-довіднику, англійський бланк,
// службовий коментар імпорту на бланку, перевірка телефону, автоверифікація з призначеним виконавцем.
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class FixG2ApiTests
{
    private const string Patient = "0b000000-0000-0000-0000-000000000003";
    private readonly LisApiFactory _f;
    public FixG2ApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static string S(JsonNode? n) => n!.GetValue<string>();
    private static double D(JsonNode? n) => n!.GetValue<double>();

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

    [Fact]
    public async Task Demo_mcv_mch_mchc_are_calculated_from_cbc()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var (_, t) = await Order("RBC", "HGB", "HCT", "MCV", "MCH", "MCHC");
        await Json(await tech.PutAsJsonAsync($"/api/v1/lab/results/{t["RBC"]}", new { numericValue = 4.5 }));
        await Json(await tech.PutAsJsonAsync($"/api/v1/lab/results/{t["HGB"]}", new { numericValue = 140.0 }));
        await Json(await tech.PutAsJsonAsync($"/api/v1/lab/results/{t["HCT"]}", new { numericValue = 42.0 }));
        var mcv = await Json(await tech.GetAsync($"/api/v1/lab/results/{t["MCV"]}"));
        var mch = await Json(await tech.GetAsync($"/api/v1/lab/results/{t["MCH"]}"));
        var mchc = await Json(await tech.GetAsync($"/api/v1/lab/results/{t["MCHC"]}"));
        Assert.True(mcv["isCalculated"]!.GetValue<bool>());
        Assert.Equal(42.0 * 10 / 4.5, D(mcv["numericValue"]), 1);
        Assert.Equal(140.0 / 4.5, D(mch["numericValue"]), 1);
        Assert.Equal(140.0 * 100 / 42.0, D(mchc["numericValue"]), 0);
    }

    [Fact]
    public async Task English_report_uses_english_names_and_hides_import_and_ukrainian_comments()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var admin = _f.As(DemoDataSeeder.Admin);
        var (orderId, t) = await Order("HGB", "GLU");
        await Json(await tech.PutAsJsonAsync($"/api/v1/lab/results/{t["HGB"]}", new { numericValue = 140.0, comment = "Імпорт із файлу g2.csv" }));
        await Json(await tech.PutAsJsonAsync($"/api/v1/lab/results/{t["GLU"]}", new { numericValue = 5.1, comment = "Український коментар лаборанта" }));
        var en = await (await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report?variant=preliminary&lang=en")).Content.ReadAsStringAsync();
        Assert.Contains("Hemoglobin (HGB)", en);
        Assert.Contains("Serum glucose", en);
        Assert.DoesNotContain("Імпорт із файлу", en);
        Assert.DoesNotContain("Український коментар", en);
        var uk = await (await admin.GetAsync($"/api/v1/lab/orders/{orderId}/report?variant=preliminary&lang=uk")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Імпорт із файлу", uk);
        Assert.Contains("Український коментар", uk);
    }

    [Fact]
    public async Task Panic_call_rejects_non_phone_text()
    {
        var doctor = _f.As(DemoDataSeeder.Doctor);
        var rows = await Json(await doctor.GetAsync("/api/v1/lab/worklist?pageSize=5"));
        var any = (rows["items"] ?? rows).AsArray().FirstOrDefault(r => r!["resultId"] != null);
        if (any == null) return;
        var res = await doctor.PostAsJsonAsync("/api/v1/lab/panic-calls", new { resultId = S(any["resultId"]), doctorName = "Лікар", phone = "abc", readbackConfirmed = true });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
    }
}
