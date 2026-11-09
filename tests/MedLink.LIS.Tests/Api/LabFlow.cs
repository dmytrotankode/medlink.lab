// Спільні помічники API-тестів: JSON-відповіді, проблеми, швидке створення пацієнта/замовлення з прийнятою пробіркою
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;

namespace MedLink.LIS.Tests.Api;

public sealed record FlowOrder(string OrderId, string PatientId, List<string> Barcodes, Dictionary<string, string> TestIds)
{
    public string Barcode => Barcodes[0];
    public string Test(string code) => TestIds[code];
}

public static class LabFlow
{
    public static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    public static async Task Problem(HttpResponseMessage r, HttpStatusCode status)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"очікувався {(int)status}, отримано {(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        Assert.Contains("\"title\"", text);
    }

    /// <summary>Новий пацієнт (унікальне ПІБ), щоб delta-check не чіпав чужі результати.</summary>
    public static async Task<string> NewPatient(LisApiFactory f, string? prefix = null)
    {
        var admin = f.As(DemoDataSeeder.Admin);
        var s = Guid.NewGuid().ToString("N")[..6];
        var p = await Json(await admin.PostAsJsonAsync("/api/v1/lab/patients", new { lastName = $"{prefix ?? "Тестенко"}{s}", firstName = "Тест", birthDate = "1985-05-05", gender = "M" }));
        return p["id"]!.GetValue<string>();
    }

    /// <summary>Замовлення з забраними й прийнятими пробірками (адмін виконує всі кроки).</summary>
    public static async Task<FlowOrder> ReceivedOrder(LisApiFactory f, string[] tests, string? patientId = null, bool cito = false, DateTime? orderedAt = null, string[]? profiles = null)
    {
        var admin = f.As(DemoDataSeeder.Admin);
        patientId ??= await NewPatient(f);
        var o = await Json(await admin.PostAsJsonAsync("/api/v1/lab/orders", new
        {
            patientId, doctorId = DemoDataSeeder.Doctor, departmentId = DemoDataSeeder.Dept, isUrgentCito = cito, orderDatetime = orderedAt,
            profileIds = profiles ?? Array.Empty<string>(), testIds = tests
        }));
        var orderId = o["id"]!.GetValue<string>();
        var barcodes = o["samples"]!.AsArray().Select(s => s!["barcode"]!.GetValue<string>()).ToList();
        foreach (var b in barcodes)
        {
            await Json(await admin.PostAsJsonAsync($"/api/v1/lab/samples/{b}/collect", new { volumeMl = 4.0 }));
            await Json(await admin.PostAsJsonAsync($"/api/v1/lab/samples/{b}/receive", new { }));
        }
        var detail = await Json(await admin.GetAsync($"/api/v1/lab/orders/{orderId}"));
        var map = detail["tests"]!.AsArray().ToDictionary(t => t!["testCode"]!.GetValue<string>(), t => t!["id"]!.GetValue<string>());
        return new FlowOrder(orderId, patientId, barcodes, map);
    }

    public static async Task<JsonNode> Enter(LisApiFactory f, string employee, FlowOrder o, string code, double value, string? analyzerId = null)
        => await Json(await f.As(employee).PutAsJsonAsync($"/api/v1/lab/results/{o.Test(code)}", new { numericValue = value, analyzerId }));

    public static async Task<string> NewConnectorKey(LisApiFactory f, string installName)
    {
        var admin = f.As(DemoDataSeeder.Admin);
        var created = await Json(await admin.PostAsJsonAsync("/api/v1/lab/connectors", new { name = installName }));
        var reg = await Json(await f.CreateClient().PostAsJsonAsync("/api/v1/lab/connector/register", new { installKey = created["installKey"]!.GetValue<string>(), hostName = "TEST-PC", osDescription = "test", version = "4.0.0" }));
        return reg["apiKey"]!.GetValue<string>();
    }

    public static HttpClient Connector(LisApiFactory f, string apiKey)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Add("X-MedLink-ApiKey", apiKey);
        return c;
    }
}
