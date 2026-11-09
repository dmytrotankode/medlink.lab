// Ліміти пакетних операцій (413), версія коннектора (FR-CON-008), mutex (FR-CON-007), перенаправлення замовлення на інший прилад (S-56)
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class LimitsConnectorRerouteTests
{
    private const string Patient = "0b000000-0000-0000-0000-000000000004";
    private readonly LisApiFactory _f;
    public LimitsConnectorRerouteTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }
    private static string S(JsonNode? n) => n!.GetValue<string>();

    [Fact]
    public async Task Order_batch_over_200_returns_413_with_ukrainian_message()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var big = Enumerable.Range(0, 201).Select(_ => new { patientId = Patient, testIds = new[] { "GLU" } }).ToList();
        var r = await reg.PostAsJsonAsync("/api/v1/lab/orders/batch", big);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);
        var body = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.Contains("не більше 200", S(body["detail"]));
        var ok = await reg.PostAsJsonAsync("/api/v1/lab/orders/batch", big.Take(2).ToList());
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    [Fact]
    public async Task Results_import_over_5000_rows_returns_413()
    {
        var tech = _f.As(DemoDataSeeder.Technician);
        var sb = new StringBuilder("barcode;testCode;value\n");
        for (var i = 0; i < 5001; i++) sb.Append('X').Append(i).Append(";GLU;5,1\n");
        using var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes(sb.ToString())), "file", "big.csv" } };
        var r = await tech.PostAsync("/api/v1/lab/import/results?dryRun=true", form);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);
        Assert.Contains("не більше 5000", S(JsonNode.Parse(await r.Content.ReadAsStringAsync())!["detail"]));
    }

    [Fact]
    public async Task Connector_heartbeat_warns_about_outdated_version()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var created = await Json(await admin.PostAsJsonAsync("/api/v1/lab/connectors", new { name = "Старий ПК", pollIntervalSec = 30, heartbeatIntervalSec = 30 }));
        var anon = _f.CreateClient();
        var reg = await Json(await anon.PostAsJsonAsync("/api/v1/lab/connector/register", new { installKey = S(created["installKey"]), hostName = "OLD-PC", osDescription = "Windows 10", version = "3.9.0" }));
        var con = _f.CreateClient();
        con.DefaultRequestHeaders.Add("X-MedLink-ApiKey", S(reg["apiKey"]));

        var hb = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/heartbeat", new { version = "3.9.0", uptimeSec = 10, bufferedCount = 0, analyzers = Array.Empty<object>() }));
        Assert.True(hb["versionOutdated"]!.GetValue<bool>());
        Assert.Equal("4.0.0", S(hb["minVersion"]));
        Assert.Contains("застаріла", S(hb["warning"]));
        var card = await Json(await admin.GetAsync($"/api/v1/lab/connectors/{S(created["id"])}"));
        Assert.True(card["versionOutdated"]!.GetValue<bool>());

        var hb2 = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/heartbeat", new { version = "4.1.2", uptimeSec = 20, bufferedCount = 0, analyzers = Array.Empty<object>() }));
        Assert.False(hb2["versionOutdated"]!.GetValue<bool>());
        Assert.Null(hb2["warning"]);
        var card2 = await Json(await admin.GetAsync($"/api/v1/lab/connectors/{S(created["id"])}"));
        Assert.False(card2["versionOutdated"]!.GetValue<bool>());
    }

    [Fact]
    public void Single_instance_guard_blocks_second_instance()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ml-guard-" + Guid.NewGuid().ToString("N"));
        var port = 50000 + Random.Shared.Next(1, 9000);
        Assert.True(MedLink.LabConnector.Services.SingleInstanceGuard.TryAcquire(dir, port, out var first, out _));
        try
        {
            Assert.False(MedLink.LabConnector.Services.SingleInstanceGuard.TryAcquire(dir, port, out var second, out var err));
            Assert.Null(second);
            Assert.Contains("уже запущено", err);
        }
        finally { first!.Dispose(); }
        Assert.True(MedLink.LabConnector.Services.SingleInstanceGuard.TryAcquire(dir, port, out var again, out _));
        again!.Dispose();
    }

    [Fact]
    public async Task Order_can_be_rerouted_to_another_analyzer_with_reason_audit_and_roles()
    {
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
            if (!await db.AnalyzerParameters.AnyAsync(p => p.AnalyzerId == DemoDataSeeder.Sysmex && p.TestCode == "GLU"))
            {
                db.AnalyzerParameters.Add(new LabAnalyzerParameterMap { AnalyzerId = DemoDataSeeder.Sysmex, AnalyzerCode = "GLU", TestCode = "GLU", IsEnabled = true, Priority = 5 });
                await db.SaveChangesAsync();
            }
        }
        var reg = _f.As(DemoDataSeeder.Registrar);
        var tech = _f.As(DemoDataSeeder.Technician);
        var order = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, testIds = new[] { "GLU" } }));
        var orderId = S(order["id"]);
        var test = order["tests"]!.AsArray().Single(t => S(t!["testCode"]) == "GLU")!;
        var from = test["assignedAnalyzerId"]?.GetValue<string>();
        var target = from == DemoDataSeeder.Sysmex ? DemoDataSeeder.Mindray : DemoDataSeeder.Sysmex;

        var cands = await Json(await tech.GetAsync($"/api/v1/lab/order-tests/{S(test["id"])}/analyzer-candidates"));
        Assert.Contains(cands["candidates"]!.AsArray(), c => S(c!["analyzerId"]) == target);

        Assert.Equal(HttpStatusCode.Forbidden, (await reg.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/reroute", new { analyzerId = target, reason = "тест" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await tech.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/reroute", new { analyzerId = target, reason = "" })).StatusCode);

        var res = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/reroute", new { analyzerId = target, reason = "Прилад перевантажено" }));
        Assert.Single(res["moved"]!.AsArray());
        var after = await Json(await tech.GetAsync($"/api/v1/lab/orders/{orderId}"));
        Assert.Equal(target, S(after["tests"]!.AsArray().Single(t => S(t!["testCode"]) == "GLU")!["assignedAnalyzerId"]));

        var log = await Json(await tech.GetAsync($"/api/v1/lab/routing/log?orderId={orderId}"));
        Assert.Contains(log.AsArray(), e => e!["reason"]?.GetValue<string>() == "Прилад перевантажено" && S(e["analyzerId"]) == target);

        var again = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/reroute", new { analyzerId = target, reason = "ще раз" }));
        Assert.Empty(again["moved"]!.AsArray());
        Assert.Single(again["skipped"]!.AsArray());
    }
}
