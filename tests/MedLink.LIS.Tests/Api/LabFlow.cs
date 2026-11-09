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

    /// <summary>Нова інсталяція коннектора, зареєстрована за install key → (id, apiKey).</summary>
    public static async Task<(string Id, string ApiKey, string InstallKey)> NewConnector(LisApiFactory f, string? name = null)
    {
        var admin = f.As(DemoDataSeeder.Admin);
        var created = await Json(await admin.PostAsJsonAsync("/api/v1/lab/connectors", new { name = name ?? "Тест-" + Guid.NewGuid().ToString("N")[..6] }));
        var installKey = created["installKey"]!.GetValue<string>();
        var reg = await Json(await f.CreateClient().PostAsJsonAsync("/api/v1/lab/connector/register", new { installKey, hostName = "TEST-PC", osDescription = "test", version = "4.0.0" }));
        return (created["id"]!.GetValue<string>(), reg["apiKey"]!.GetValue<string>(), installKey);
    }

    public static async Task<string> NewConnectorKey(LisApiFactory f, string installName) => (await NewConnector(f, installName)).ApiKey;

    /// <summary>Аналізатор на заданому коннекторі (тип — як у демо-Mindray) з мапою «код приладу → код тесту».</summary>
    public static async Task<string> NewAnalyzer(LisApiFactory f, string connectorId, params (string AnalyzerCode, string TestCode, double Factor, double Offset)[] map)
    {
        var admin = f.As(DemoDataSeeder.Admin);
        var mindray = await Json(await admin.GetAsync($"/api/v1/lab/analyzers/{DemoDataSeeder.Mindray}"));
        var a = await Json(await admin.PostAsJsonAsync("/api/v1/lab/analyzers", new
        {
            code = "AN" + Guid.NewGuid().ToString("N")[..8].ToUpper(), name = "Тестовий аналізатор", analyzerTypeId = mindray["analyzerTypeId"]!.GetValue<int>(),
            connectorId, connectionMode = "TCP", tcpHost = "127.0.0.1", tcpPort = 5100,
            parameterMap = map.Select(m => new { analyzerCode = m.AnalyzerCode, testCode = m.TestCode, factor = m.Factor, offset = m.Offset }).ToArray()
        }));
        return a["id"]!.GetValue<string>();
    }

    public static async Task<string> NewCustomTest(LisApiFactory f, double? deltaPct = null, bool manual = false)
    {
        var code = "AV" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        await Json(await f.As(DemoDataSeeder.Admin).PostAsJsonAsync("/api/v1/lab/dictionaries/tests", new
        {
            code, name = "Тестовий показник " + code, unit = "од", biomaterialTypeId = 4, category = "BIOCHEM", decimalPlaces = 1,
            deltaCheckMaxPct = deltaPct, deltaCheckHours = 72, requiresManualVerification = manual
        }));
        // без шару норми результат має флаг NONE і за правилами завжди йде на ручну верифікацію — задаємо норму 0–100
        await Json(await f.As(DemoDataSeeder.Admin).PostAsJsonAsync("/api/v1/lab/norms/combinations", new { testCode = code, layerType = "BASELINE", normName = "Тестова норма", normLow = 0.0, normHigh = 100.0 }));
        return code;
    }

    public static HttpClient ConnectorClient(LisApiFactory f, string apiKey)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Add("X-MedLink-ApiKey", apiKey);
        return c;
    }
}
