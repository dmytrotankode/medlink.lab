// Спільні засоби тестів інтеграції з ЕСОЗ: реєстр мок-ЕСОЗ, віртуальний годинник для воркера, повний шлях замовлення до видачі.
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Data.Seed;
using MedLink.LIS.Api.Services.Ehealth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedLink.LIS.Tests.Api;

/// <summary>Фабрика для тестів ЕСОЗ із довільною конфігурацією секції Ehealth (режим, швидкі витримки тощо).</summary>
public class EhealthFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"medlink-lis-esoz-{Guid.NewGuid():N}.db");
    private readonly string _guidePath = Path.Combine(Path.GetTempPath(), $"medlink-guide-esoz-{Guid.NewGuid():N}.db");
    private readonly Dictionary<string, string?> _extra;
    public EhealthFactory(params (string Key, string Value)[] extra) => _extra = extra.ToDictionary(x => x.Key, x => (string?)x.Value);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            var d = new Dictionary<string, string?>
            {
                ["Database:SqlitePath"] = _dbPath, ["Database:GuideSqlitePath"] = _guidePath, ["Lab:SeedDemoData"] = "true", ["Lab:PublicBaseUrl"] = "http://localhost:5055", ["Logging:LogLevel:Default"] = "Warning",
                ["Ehealth:Http:RetryBaseDelayMs"] = "1", ["Ehealth:Http:SyncJobPollDelaysMs:0"] = "1", ["Ehealth:Http:SyncJobWaitSeconds"] = "5", ["Ehealth:Outbox:BackoffJitter"] = "0"
            };
            foreach (var kv in _extra) d[kv.Key] = kv.Value;
            cfg.AddInMemoryCollection(d);
        });
    }

    public HttpClient As(string employeeId)
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Add("X-MedLink-Employee-Id", employeeId);
        return c;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_dbPath); File.Delete(_guidePath); } catch { /* ignore */ }
    }
}

/// <summary>Режим Mock із типовими швидкими налаштуваннями (воркер вимкнено, тест керує рушієм).</summary>
public sealed class EhealthMockFactory : EhealthFactory { public EhealthMockFactory() : base(("Ehealth:Mode", "Mock")) { } }

/// <summary>Режим Replay: відповіді ЕСОЗ беруться з Fixtures/ehealth/*.json.</summary>
public sealed class EhealthReplayFactory : EhealthFactory { public EhealthReplayFactory() : base(("Ehealth:Mode", "Replay")) { } }

public static class EhealthKit
{
    public const string Patient = "0b000000-0000-0000-0000-000000000006";

    public static EhealthMockState Mock(this WebApplicationFactory<Program> f) => f.Services.GetRequiredService<EhealthMockState>();
    public static EhealthOutboxEngine Engine(this WebApplicationFactory<Program> f) => f.Services.GetRequiredService<EhealthOutboxEngine>();

    public static async Task<T> Db<T>(this WebApplicationFactory<Program> f, Func<LisDbContext, Task<T>> fn)
    {
        using var scope = f.Services.CreateScope();
        return await fn(scope.ServiceProvider.GetRequiredService<LisDbContext>());
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, StrongBox<DateTime>> Clocks = new();

    /// <summary>Віртуальний годинник воркера фабрики: монотонний між викликами Pump (кожен прохід — +10 хв).</summary>
    public static DateTime Now(this WebApplicationFactory<Program> f) => Clocks.GetOrCreateValue(f).Value is var v && v > DateTime.UtcNow ? v : DateTime.UtcNow;

    /// <summary>Проганяє чергу з віртуальним годинником, доки є що виконувати. Повертає кількість проходів із роботою.</summary>
    public static async Task<int> Pump(this WebApplicationFactory<Program> f, int maxRounds = 20)
    {
        var box = Clocks.GetOrCreateValue(f);
        var t = f.Now();
        var worked = 0;
        for (var i = 0; i < maxRounds; i++)
        {
            t = t.AddMinutes(10);
            box.Value = t;
            if (await f.Engine().RunDueAsync(t) > 0) worked++;
            // продовжуємо, доки в черзі є задачі, що чекають (витримка повтору, опитування job)
            if (!await f.Db(db => db.EhealthOutbox.AnyAsync(x => x.Status == EhealthOutboxStatuses.New || x.Status == EhealthOutboxStatuses.WaitingJob)))
            {
                box.Value = default; // черга порожня: годинник знову іде за реальним часом, щоб не накопичувати зсув між тестами
                break;
            }
        }
        return worked;
    }

    public static string S(JsonNode? n) => n!.GetValue<string>();

    public static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    /// <summary>Е-направлення в реєстрі мок-ЕСОЗ (без запису в дзеркало БД ЛІС).</summary>
    public static EhealthMockState.MockServiceRequest RegisterReferral(this WebApplicationFactory<Program> f, string number, string? programId = null, string? patientId = Patient, string serviceCode = "LAB-BIOCHEM")
        => f.Mock().Register(number, patientId, serviceCode, programId);

    /// <summary>Входить в ЄСОЗ через двійник проксі (посилання -> тестовий код -> обмін коду) і повертає тестовий сертифікат КЕП.</summary>
    public static async Task<JsonNode> LoginAsync(HttpClient doc)
    {
        var link = await Json(await doc.PostAsync("/api/v1/lab/ehealth/session/link", null));
        var s = await Json(await doc.PostAsJsonAsync("/api/v1/lab/ehealth/session/callback", new { code = S(link["code"]) }));
        Assert.Equal("L2", S(s["level"]));
        return s["testCertificate"]!;
    }

    /// <summary>Створює замовлення за е-направленням (ГЛЮ), проводить результат і видає його. Повертає id замовлення.</summary>
    public static async Task<string> ReleasedOrderAsync(this WebApplicationFactory<Program> f, Func<string, HttpClient> client, string referralNumber, string testId = "GLU", string patient = Patient)
    {
        var reg = client(DemoDataSeeder.Registrar); var tech = client(DemoDataSeeder.Technician); var doctor = client(DemoDataSeeder.Doctor);
        var o = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = patient, ehealthReferralNumber = referralNumber, testIds = new[] { testId } }));
        var orderId = S(o["id"]);
        var barcode = S(o["samples"]![0]!["barcode"]);
        await Json(await tech.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/collect", new { volumeMl = 4.0 }));
        await Json(await tech.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/receive", new { }));
        var tid = S(o["tests"]![0]!["id"]);
        var row = await Json(await tech.PutAsJsonAsync($"/api/v1/lab/results/{tid}", new { numericValue = 5.0 }));
        if (S(row["status"]) != "AUTO_VERIFIED") await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/results/{tid}/verify", new { }));
        await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/release", new { }));
        return orderId;
    }

    /// <summary>Prepare → тестовий підпис → submit. Повертає відповідь submit.</summary>
    public static async Task<JsonNode> SignAndSubmitAsync(HttpClient doc, string orderId, JsonNode cert)
    {
        await Json(await doc.PostAsync($"/api/v1/lab/orders/{orderId}/ehealth/prepare", null));
        var signed = await Json(await doc.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/ehealth/mock-sign", new { kind = "submit" }));
        return await Json(await doc.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/ehealth/submit", new { signedData = S(signed["signedData"]), signer = cert }));
    }
}
