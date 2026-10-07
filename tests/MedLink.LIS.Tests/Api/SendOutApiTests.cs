// Направлення у зовнішні лабораторії (send-out): маршрут за замовчуванням → окрема пробірка → черга → реєстр →
// відправка → приймання → результат (без автоверифікації) → бланк із позначкою виконавця; PDF-бланк; імпорт файлу; ручна маршрутизація
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class SendOutApiTests
{
    private const string Patient = "0b000000-0000-0000-0000-000000000004";
    private const string Synevo = "0f0f0000-0000-0000-0000-000000000002";
    private readonly LisApiFactory _f;
    public SendOutApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static string S(JsonNode? n) => n!.GetValue<string>();

    private async Task<JsonNode> CreateCollectedOrder(params string[] tests)
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var nurse = _f.As(DemoDataSeeder.Nurse);
        var order = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, testIds = tests }));
        var tech = _f.As(DemoDataSeeder.Technician);
        foreach (var s in order["samples"]!.AsArray())
        {
            await Json(await nurse.PostAsJsonAsync($"/api/v1/lab/samples/{S(s!["barcode"])}/collect", new { checklist = new { idVerified = true }, volumeMl = 4.0 }));
            await Json(await tech.PostAsJsonAsync($"/api/v1/lab/samples/{S(s["barcode"])}/receive", new { }));
        }
        return await Json(await tech.GetAsync($"/api/v1/lab/orders/{S(order["id"])}"));
    }

    [Fact]
    public async Task Full_send_out_cycle_with_manual_result_and_report_note()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var doctor = _f.As(DemoDataSeeder.Doctor);

        // 1. Маршрут за замовчуванням: VITD → Сінево, окрема пробірка
        var order = await CreateCollectedOrder("VITD", "GLU");
        var orderId = S(order["id"]);
        var vitd = order["tests"]!.AsArray().First(t => S(t!["testCode"]) == "VITD")!;
        var glu = order["tests"]!.AsArray().First(t => S(t!["testCode"]) == "GLU")!;
        Assert.Equal(Synevo, S(vitd["performerId"]));
        Assert.Null(glu["performerId"]);
        Assert.NotEqual(S(vitd["sampleId"]), S(glu["sampleId"]));
        var sendOutTube = order["samples"]!.AsArray().First(s => S(s!["id"]) == S(vitd["sampleId"]))!;
        Assert.Contains("COMPATIBILITY_GROUP", sendOutTube["planReasons"]!.AsArray().Select(r => S(r)));

        // 2. Черга до відправки
        var queue = (await Json(await tech.GetAsync($"/api/v1/lab/send-out/queue?performerId={Synevo}"))).AsArray();
        var q = queue.First(x => S(x!["orderTestId"]) == S(vitd["id"]))!;
        Assert.True(q["sampleReady"]!.GetValue<bool>());
        Assert.Equal("SY-1043", S(q["externalCode"]));

        // 3. Реєстр → відправка → приймання
        var so = await Json(await tech.PostAsJsonAsync("/api/v1/lab/send-out", new { performerId = Synevo, orderTestIds = new[] { S(vitd["id"]) }, courierName = "Кур'єр Сінево" }));
        Assert.Matches(@"^SO-\d{4}-\d{4}$", S(so["number"]));
        Assert.Equal("CREATED", S(so["status"]));
        var soId = S(so["id"]);
        await Assert.ThrowsAnyAsync<Exception>(async () => await Json(await tech.PostAsJsonAsync("/api/v1/lab/send-out", new { performerId = Synevo, orderTestIds = new[] { S(vitd["id"]) } }))); // двічі не можна
        so = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/send-out/{soId}/dispatch", new { temperature = 4.5 }));
        Assert.Equal("DISPATCHED", S(so["status"]));
        var item = so["items"]![0]!;
        Assert.Equal("SENT_OUT", S(item["testStatus"]));
        Assert.NotNull(item["dueAt"]);
        so = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/send-out/{soId}/accept", new { externalBatchNumber = "SYN-778812", externalOrderNumbers = new Dictionary<string, string> { [S(item["id"])] = "SY-55501" } }));
        Assert.Equal("ACCEPTED", S(so["status"]));
        Assert.Equal("SY-55501", S(so["items"]![0]!["externalOrderNumber"]));

        // 4. Результат зовнішньої лабораторії: без автоверифікації, з референсом виконавця
        var row = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/send-out/items/{S(item["id"])}/result", new { numericValue = 18.4, referenceText = "30 – 100" }));
        Assert.Equal("RESULTED", S(row["status"]));
        so = await Json(await tech.GetAsync($"/api/v1/lab/send-out/{soId}"));
        Assert.Equal("COMPLETED", S(so["status"]));
        Assert.Equal("30 – 100", S(so["items"]![0]!["result"]!["referenceDisplay"]));

        // 5. PDF-бланк зовнішньої лабораторії
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4 demo synevo blank");
        using (var form = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(pdf); file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "file", "synevo_SY-55501.pdf");
            var att = await Json(await tech.PostAsync($"/api/v1/lab/orders/{orderId}/attachments?sendOutId={soId}", form));
            Assert.Equal(64, S(att["sha256"]).Length);
        }
        var list = (await Json(await tech.GetAsync($"/api/v1/lab/orders/{orderId}/attachments"))).AsArray();
        Assert.Equal("ТОВ «Сінево Україна»", S(list[0]!["performerName"]));
        Assert.Equal(pdf, await (await tech.GetAsync(S(list[0]!["downloadUrl"]))).Content.ReadAsByteArrayAsync());

        // 6. Верифікація нашим лікарем, бланк із позначкою виконавця
        await Json(await tech.PutAsJsonAsync($"/api/v1/lab/results/{S(glu["id"])}", new { numericValue = 5.1 }));
        await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/results/{S(vitd["id"])}/verify", new { comment = "Дефіцит вітаміну D" }));
        var html = await (await doctor.GetAsync($"/api/v1/lab/orders/{orderId}/report?variant=preliminary")).Content.ReadAsStringAsync();
        Assert.Contains("Сінево Україна", html);
        Assert.Contains(" *", html);
    }

    [Fact]
    public async Task Rejection_by_external_lab_returns_test_to_queue_and_import_matches_rows()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var order = await CreateCollectedOrder("AMH", "VITD");
        var ids = order["tests"]!.AsArray().Select(t => S(t!["id"])).ToArray();
        var so = await Json(await tech.PostAsJsonAsync("/api/v1/lab/send-out", new { performerId = Synevo, orderTestIds = ids }));
        var soId = S(so["id"]);
        so = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/send-out/{soId}/dispatch", new { }));
        var amh = so["items"]!.AsArray().First(i => S(i!["testCode"]) == "AMH")!;
        var vitd = so["items"]!.AsArray().First(i => S(i!["testCode"]) == "VITD")!;

        // Відмова за АМГ (гемоліз) — тест повертається в чергу
        so = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/send-out/items/{S(amh["id"])}/reject", new { reason = "Гемоліз зразка", returnToQueue = true }));
        Assert.Equal("REJECTED", S(so["items"]!.AsArray().First(i => S(i!["id"]) == S(amh["id"]))!["status"]));
        var queue = (await Json(await tech.GetAsync("/api/v1/lab/send-out/queue"))).AsArray();
        Assert.Contains(queue, x => S(x!["orderTestId"]) == S(amh["orderTestId"]));

        // Імпорт CSV: штрихкод + код у прайсі Сінево
        var csv = $"barcode;testCode;value;unit\n{S(vitd["barcode"])};SY-1043;42,5;нг/мл\n00000000;SY-1043;1;x\n";
        using var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "synevo.csv" } };
        var dry = await Json(await tech.PostAsync($"/api/v1/lab/send-out/{soId}/import?dryRun=true", form));
        Assert.Equal(1, dry["matched"]!.GetValue<int>());
        Assert.Equal(1, dry["errors"]!.GetValue<int>());
        using var form2 = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "synevo.csv" } };
        var applied = await Json(await tech.PostAsync($"/api/v1/lab/send-out/{soId}/import?dryRun=false", form2));
        Assert.Equal(1, applied["applied"]!.GetValue<int>());
        so = await Json(await tech.GetAsync($"/api/v1/lab/send-out/{soId}"));
        Assert.Equal("COMPLETED", S(so["status"])); // АМГ відхилено, VITD отримано
        Assert.Equal(42.5, so["items"]!.AsArray().First(i => S(i!["testCode"]) == "VITD")!["result"]!["numericValue"]!.GetValue<double>());
    }

    [Fact]
    public async Task Manual_routing_is_limited_to_performer_price_list()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var order = await Json(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, testIds = new[] { "TSH", "GLU" } }));
        var tsh = order["tests"]!.AsArray().First(t => S(t!["testCode"]) == "TSH")!;
        var glu = order["tests"]!.AsArray().First(t => S(t!["testCode"]) == "GLU")!;
        Assert.Null(tsh["performerId"]); // TSH у прайсі Сінево, але не маршрут за замовчуванням
        var routed = await Json(await tech.PutAsJsonAsync($"/api/v1/lab/order-tests/{S(tsh["id"])}/performer", new { performerId = "SYNEVO", reason = "Аналізатор імунохімії на ТО" }));
        Assert.Equal(Synevo, S(routed["performerId"]));
        var bad = await tech.PutAsJsonAsync($"/api/v1/lab/order-tests/{S(glu["id"])}/performer", new { performerId = "SYNEVO" });
        Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);
        var back = await Json(await tech.PutAsJsonAsync($"/api/v1/lab/order-tests/{S(tsh["id"])}/performer", new { performerId = (string?)null }));
        Assert.Null(back["performerId"]);
        var performers = (await Json(await tech.GetAsync("/api/v1/lab/performers"))).AsArray();
        Assert.Contains(performers, p => S(p!["code"]) == "TERRALAB" && S(p["medlinkProvider"]) == "TERRALAB");
    }
}
