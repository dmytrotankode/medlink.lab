// Режим Replay: відповіді проксі MedLink беруться з Fixtures/ehealth/*.json (форма за SDK МІС і прикладами OpenAPI). Той самий код клієнта й outbox, що й з реальним проксі —
// інша лише «мережа». Перевіряє повний цикл за фікстурами, відхилений job (422) і новий пакет, а також відсутність фікстури.
using System.Net;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Data.Seed;
using MedLink.LIS.Api.Services.Ehealth;
using Microsoft.Extensions.DependencyInjection;
using static MedLink.LIS.Tests.Api.EhealthKit;

namespace MedLink.LIS.Tests.Api;

// Collection "api": БД довідника Guide спільна для всіх фабрик (шлях читається до конфігурації хоста), тому класи з хостом виконуються послідовно
[Collection("api")]
public class EhealthReplayApiTests : IClassFixture<EhealthReplayFactory>
{
    private readonly EhealthReplayFactory _f;
    public EhealthReplayApiTests(EhealthReplayFactory f) => _f = f;

    private List<string> Calls() => _f.Services.GetRequiredService<EhealthReplayState>().CallLog().ToList();

    [Fact]
    public async Task Replay_mode_reports_its_name_and_serves_search_and_error_fixtures()
    {
        Assert.Equal("Replay", _f.Services.GetRequiredService<EhealthOptions>().Mode);
        var reg = _f.As(DemoDataSeeder.Registrar);
        var found = await Json(await reg.GetAsync("/api/v1/lab/referrals/ehealth/lookup?number=AX654654T"));
        Assert.Equal("ESOZ", S(found["source"]));
        Assert.Equal("AX654654T", S(found["number"]));
        Assert.True(found["canUse"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NotFound, (await reg.GetAsync("/api/v1/lab/referrals/ehealth/lookup?number=NOT-FOUND")).StatusCode);
        var bad = await reg.GetAsync("/api/v1/lab/referrals/ehealth/lookup?number=BAD");
        Assert.True((int)bad.StatusCode >= 400, "422 від ЕСОЗ не має перетворитися на успіх");
        Assert.Contains("GET /patients/service_requests?requisition=AX654654T", Calls().First(x => x.Contains("AX654654T")).Replace("&status=active&page=1&page_size=50", ""));
        var log = await Json(await _f.As(DemoDataSeeder.Doctor).GetAsync("/api/v1/lab/ehealth/exchange-log?take=50"));
        Assert.Equal("REPLAY", S(log["mode"]));
    }

    [Fact]
    public async Task Replay_full_cycle_then_rejected_job_and_new_package()
    {
        var doc = _f.As(DemoDataSeeder.Doctor);
        var cert = await LoginAsync(doc);

        // 1. направлення AX654654T: пакет → job (pending → 303 processed) → окреме погашення → job processed
        var order1 = await _f.ReleasedOrderAsync(_f.As, "AX654654T");
        await SignAndSubmitAsync(doc, order1, cert);
        await _f.Pump();
        Assert.Equal(EhealthSubmitStatuses.Completed, S((await Json(await doc.GetAsync($"/api/v1/lab/orders/{order1}/ehealth/status")))["status"]));
        var calls = Calls();
        var iPost = calls.FindIndex(x => x.StartsWith("POST /patients/0b000000-0000-0000-0000-000000000006/diagnostic_report_package"));
        var iJob1 = calls.FindIndex(x => x == "GET /jobs/NBXk9EyErUZv1RhXgyvgg");
        var iDone = calls.FindIndex(x => x == "PATCH /patients/service_requests/90a9e15b-b71b-4caf-8f2e-ff247e8a5600/actions/complete");
        var iJob2 = calls.FindIndex(x => x == "GET /jobs/Ck3wPZ8hQTq6RjS2mNvXa1");
        Assert.True(iPost >= 0 && iJob1 > iPost && iDone > iJob1 && iJob2 > iDone, string.Join(" | ", calls));
        Assert.Equal(2, calls.Count(x => x == "GET /jobs/NBXk9EyErUZv1RhXgyvgg")); // pending, потім processed

        // 2. направлення AX654655T: перший пакет — job failed (422), задача зупиняється; новий пакет проходить
        var order2 = await _f.ReleasedOrderAsync(_f.As, "AX654655T");
        await SignAndSubmitAsync(doc, order2, cert);
        await _f.Pump();
        var st = await Json(await doc.GetAsync($"/api/v1/lab/orders/{order2}/ehealth/status"));
        Assert.Equal(EhealthSubmitStatuses.Rejected, S(st["status"]));
        var task = st["outbox"]![0]!;
        Assert.Equal("FAILED", S(task["status"]));
        Assert.Contains("Issued date must be in the past", S(task["lastError"]));
        Assert.True(task["canRetry"]!.GetValue<bool>());
        Assert.DoesNotContain(Calls(), x => x.EndsWith("/service_requests/5b3f1c7e-0d84-4f0e-9a52-1c2d9d6f7a10/actions/complete"));

        await SignAndSubmitAsync(doc, order2, cert);
        await _f.Pump();
        Assert.Equal(EhealthSubmitStatuses.Completed, S((await Json(await doc.GetAsync($"/api/v1/lab/orders/{order2}/ehealth/status")))["status"]));
        Assert.Contains("GET /jobs/Fq3rYt8LmZp2VxA6Hc1Wd", Calls());
        Assert.Contains("GET /jobs/Pk7sBn4TgQe9JuR5Mz0Lw", Calls());
        Assert.Contains("PATCH /patients/service_requests/5b3f1c7e-0d84-4f0e-9a52-1c2d9d6f7a10/actions/complete", Calls());
    }
}
