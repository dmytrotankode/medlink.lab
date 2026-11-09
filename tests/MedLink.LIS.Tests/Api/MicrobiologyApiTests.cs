// I-MIC-*: мікробіологія (FR-MIC-001..008) — посів, ріст, ізоляти, антибіотикограма S/I/R за EUCAST, фенотипи, стани й ролі
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;
using static MedLink.LIS.Tests.Api.LabFlow;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class MicrobiologyApiTests
{
    private readonly LisApiFactory _f;
    public MicrobiologyApiTests(LisApiFactory f) => _f = f;

    private HttpClient Tech => _f.As(DemoDataSeeder.Technician);
    private HttpClient Doctor => _f.As(DemoDataSeeder.Doctor);

    private async Task<string> CultureOrderTestId()
    {
        var o = await ReceivedOrder(_f, new[] { "URINE_CULTURE" });
        return o.Test("URINE_CULTURE");
    }

    private async Task<string> NewCulture()
    {
        var c = await Json(await Tech.PostAsJsonAsync("/api/v1/lab/microbiology/cultures", new { orderTestId = await CultureOrderTestId(), specimenLocus = "Сеча", cultureMedium = "CLED" }));
        return c["id"]!.GetValue<string>();
    }

    private async Task<int> OrganismId(string code)
    {
        var list = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync("/api/v1/lab/dictionaries/organisms?pageSize=500"));
        return list["items"]!.AsArray().First(o => o!["code"]!.GetValue<string>() == code)!["id"]!.GetValue<int>();
    }

    private async Task<Dictionary<string, int>> Antibiotics()
    {
        var list = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync("/api/v1/lab/dictionaries/antibiotics?pageSize=500"));
        return list["items"]!.AsArray().ToDictionary(a => a!["code"]!.GetValue<string>(), a => a!["id"]!.GetValue<int>());
    }

    private async Task<JsonNode> Breakpoint(int organismId, int antibioticId)
    {
        var list = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync("/api/v1/lab/dictionaries/eucast-breakpoints?pageSize=1000"));
        return list["items"]!.AsArray().First(b => b!["organismId"]!.GetValue<int>() == organismId && b["antibioticId"]!.GetValue<int>() == antibioticId)!;
    }

    private async Task<string> IsolateOf(string cultureId, int organismId)
    {
        var c = await Json(await Tech.PostAsJsonAsync($"/api/v1/lab/microbiology/cultures/{cultureId}/isolates", new { organismId, quantitativeCount = "10^5 КУО/мл" }));
        return c["isolates"]![0]!["id"]!.GetValue<string>();
    }

    [Fact]
    public async Task Culture_create_validates_and_sets_incubating_state_with_role_checks()
    {
        var testId = await CultureOrderTestId();
        // реєстратор/медсестра не створюють посів
        await Problem(await _f.As(DemoDataSeeder.Nurse).PostAsJsonAsync("/api/v1/lab/microbiology/cultures", new { orderTestId = testId }), HttpStatusCode.Forbidden);
        await Problem(await Tech.PostAsJsonAsync("/api/v1/lab/microbiology/cultures", new { }), HttpStatusCode.BadRequest);
        await Problem(await Tech.PostAsJsonAsync("/api/v1/lab/microbiology/cultures", new { orderTestId = "no-such-test" }), HttpStatusCode.NotFound);

        var c = await Json(await Tech.PostAsJsonAsync("/api/v1/lab/microbiology/cultures", new { orderTestId = testId, specimenLocus = "Сеча (середня порція)" }));
        Assert.Equal("INCUBATING", c["status"]!.GetValue<string>());
        Assert.Equal("Blood Agar", c["cultureMedium"]!.GetValue<string>()); // середовище за замовчуванням
        Assert.Equal(48, c["incubationHoursRecommended"]!.GetValue<int>());
        Assert.Equal("Сеча (середня порція)", c["specimenLocus"]!.GetValue<string>());
        Assert.Empty(c["isolates"]!.AsArray());
        // другий посів на той самий тест → 409
        await Problem(await Tech.PostAsJsonAsync("/api/v1/lab/microbiology/cultures", new { orderTestId = testId }), HttpStatusCode.Conflict);
        // тест замовлення перейшов в аналіз
        var order = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"/api/v1/lab/orders/{c["orderId"]}"));
        Assert.Equal("IN_ANALYSIS", order["tests"]!.AsArray().First(t => t!["id"]!.GetValue<string>() == testId)!["status"]!.GetValue<string>());
        // у списку з фільтром за статусом
        var list = (await Json(await Tech.GetAsync("/api/v1/lab/microbiology/cultures?status=incubating"))).AsArray();
        Assert.Contains(list, x => x!["id"]!.GetValue<string>() == c["id"]!.GetValue<string>());
        Assert.All(list, x => Assert.Equal("INCUBATING", x!["status"]!.GetValue<string>()));
        await Problem(await Tech.GetAsync("/api/v1/lab/microbiology/cultures/nope"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Susceptibility_is_interpreted_by_eucast_breakpoints_for_zone_and_mic()
    {
        var ecol = await OrganismId("ECOL");
        var ab = await Antibiotics();
        var cultureId = await NewCulture();
        var isolateId = await IsolateOf(cultureId, ecol);
        var bp = await Breakpoint(ecol, ab["MEM"]);
        var zoneS = bp["zoneSusceptibleGe"]!.GetValue<double>();
        var zoneR = bp["zoneResistantLt"]!.GetValue<double>();

        async Task<JsonNode> Add(object req) => await Json(await Tech.PostAsJsonAsync($"/api/v1/lab/microbiology/isolates/{isolateId}/susceptibility", req));

        // межі зони: рівно на пороге S → S (≥), на 1 мм нижче порога R → R (<)
        var s = await Add(new { antibioticId = ab["MEM"], zoneMm = zoneS });
        Assert.Equal("S", s["interpretation"]!.GetValue<string>());
        Assert.True(s["breakpointFound"]!.GetValue<bool>());
        var r = await Add(new { antibioticId = ab["MEM"], zoneMm = zoneR - 1 });
        Assert.Equal("R", r["interpretation"]!.GetValue<string>());
        Assert.Equal(s["id"]!.GetValue<string>(), r["id"]!.GetValue<string>()); // повторне внесення оновлює той самий рядок
        var edge = await Add(new { antibioticId = ab["MEM"], zoneMm = zoneR }); // рівно на порозі R — це вже не R (R: зона < порога)
        Assert.NotEqual("R", edge["interpretation"]!.GetValue<string>());

        // МІК має пріоритет над зоною
        var micS = bp["micSusceptibleLe"]?.GetValue<double>();
        var micR = bp["micResistantGt"]?.GetValue<double>();
        if (micS.HasValue && micR.HasValue)
        {
            Assert.Equal("S", (await Add(new { antibioticId = ab["MEM"], method = "MIC", mic = micS.Value, zoneMm = zoneR - 5 }))["interpretation"]!.GetValue<string>());
            Assert.Equal("R", (await Add(new { antibioticId = ab["MEM"], method = "MIC", mic = micR.Value * 2 + 1 }))["interpretation"]!.GetValue<string>());
        }

        // ні зони, ні МІК → 400; невідомий антибіотик → 400
        await Problem(await Tech.PostAsJsonAsync($"/api/v1/lab/microbiology/isolates/{isolateId}/susceptibility", new { antibioticId = ab["MEM"] }), HttpStatusCode.BadRequest);
        await Problem(await Tech.PostAsJsonAsync($"/api/v1/lab/microbiology/isolates/{isolateId}/susceptibility", new { antibioticId = 999999, zoneMm = 20 }), HttpStatusCode.BadRequest);
        await Problem(await Tech.PostAsJsonAsync("/api/v1/lab/microbiology/isolates/nope/susceptibility", new { antibioticId = ab["MEM"], zoneMm = 20 }), HttpStatusCode.NotFound);
        // медсестра не вносить антибіотикограму
        await Problem(await _f.As(DemoDataSeeder.Nurse).PostAsJsonAsync($"/api/v1/lab/microbiology/isolates/{isolateId}/susceptibility", new { antibioticId = ab["MEM"], zoneMm = 20 }), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Manual_override_requires_reason_and_pair_without_breakpoint_needs_manual_interpretation()
    {
        var ab = await Antibiotics();
        // власний організм без порогових значень EUCAST
        var code = "T" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var org = await Json(await _f.As(DemoDataSeeder.Admin).PostAsJsonAsync("/api/v1/lab/dictionaries/organisms", new { code, latinName = "Testus " + code, kingdom = "BACTERIA" }));
        var cultureId = await NewCulture();
        var isolateId = await IsolateOf(cultureId, org["id"]!.GetValue<int>());
        var url = $"/api/v1/lab/microbiology/isolates/{isolateId}/susceptibility";

        await Problem(await Tech.PostAsJsonAsync(url, new { antibioticId = ab["MEM"], zoneMm = 20 }), HttpStatusCode.Conflict); // без breakpoint автоінтерпретації немає
        await Problem(await Tech.PostAsJsonAsync(url, new { antibioticId = ab["MEM"], overrideInterpretation = "S" }), HttpStatusCode.BadRequest); // без обґрунтування
        var ok = await Json(await Tech.PostAsJsonAsync(url, new { antibioticId = ab["MEM"], zoneMm = 20, overrideInterpretation = "s", overrideReason = "Інтерпретація за локальним протоколом" }));
        Assert.Equal("S", ok["interpretation"]!.GetValue<string>());
        Assert.False(ok["breakpointFound"]!.GetValue<bool>());
        var c = await Json(await Tech.GetAsync($"/api/v1/lab/microbiology/cultures/{cultureId}"));
        var row = c["isolates"]![0]!["susceptibilities"]![0]!;
        Assert.Equal("Інтерпретація за локальним протоколом", row["overrideReason"]!.GetValue<string>());
        // видалення результату антибіотикограми
        Assert.Equal(HttpStatusCode.NoContent, (await Tech.DeleteAsync($"/api/v1/lab/microbiology/susceptibility/{ok["id"]}")).StatusCode);
        Assert.Empty((await Json(await Tech.GetAsync($"/api/v1/lab/microbiology/cultures/{cultureId}")))["isolates"]![0]!["susceptibilities"]!.AsArray());
    }

    [Fact]
    public async Task Esbl_phenotype_is_detected_from_third_generation_cephalosporin_resistance_and_carbapenem_susceptibility()
    {
        var ecol = await OrganismId("ECOL");
        var ab = await Antibiotics();
        var cultureId = await NewCulture();
        var isolateId = await IsolateOf(cultureId, ecol);
        async Task<JsonNode> Add(string code, double zone) => await Json(await Tech.PostAsJsonAsync($"/api/v1/lab/microbiology/isolates/{isolateId}/susceptibility", new { antibioticId = ab[code], zoneMm = zone }));

        var ctx = await Add("CTX", 6);   // R
        Assert.Equal("R", ctx["interpretation"]!.GetValue<string>());
        var mem = await Add("MEM", 40);  // S
        Assert.Equal("S", mem["interpretation"]!.GetValue<string>());
        Assert.Contains("ESBL", mem["phenotypes"]!.AsArray().Select(p => p!.GetValue<string>()));
        var culture = await Json(await Tech.GetAsync($"/api/v1/lab/microbiology/cultures/{cultureId}"));
        Assert.Contains("ESBL", culture["isolates"]![0]!["phenotypes"]!.AsArray().Select(p => p!.GetValue<string>()));
        // звіт показує фенотип і антибіотикограму
        var html = await (await Tech.GetAsync($"/api/v1/lab/microbiology/cultures/{cultureId}/report")).Content.ReadAsStringAsync();
        Assert.Contains("Фенотип стійкості", html);
        Assert.Contains("ESBL", html);
        Assert.Contains("EUCAST", html);
        // карбапенем-резистентність → CRE, ESBL зникає
        var cre = await Add("MEM", 6);
        Assert.Contains("CRE", cre["phenotypes"]!.AsArray().Select(p => p!.GetValue<string>()));
        Assert.DoesNotContain("ESBL", cre["phenotypes"]!.AsArray().Select(p => p!.GetValue<string>()));
    }

    [Fact]
    public async Task Culture_lifecycle_incubating_preliminary_isolated_completed_with_role_and_state_guards()
    {
        var ecol = await OrganismId("ECOL");
        var ab = await Antibiotics();
        var cultureId = await NewCulture();
        var url = $"/api/v1/lab/microbiology/cultures/{cultureId}";

        // у INCUBATING антибіотикограма та завершення недоступні
        await Problem(await Doctor.PostAsJsonAsync($"{url}/transition", new { status = "COMPLETE" }), HttpStatusCode.Conflict); // зі стану INCUBATING не можна
        await Problem(await Tech.PostAsJsonAsync($"{url}/transition", new { status = "ISOLATE" }), HttpStatusCode.Conflict); // жодного ізоляту
        await Problem(await Tech.PostAsJsonAsync($"{url}/transition", new { status = "NO_SUCH_ACTION" }), HttpStatusCode.BadRequest);
        await Problem(await Tech.PostAsJsonAsync($"{url}/isolates", new { organismId = 987654 }), HttpStatusCode.BadRequest);

        // ріст + ізолят: INCUBATING → PRELIMINARY автоматично, GrowthDetected = true
        var withIsolate = await Json(await Tech.PostAsJsonAsync($"{url}/isolates", new { organismId = ecol, quantitativeCount = "10^6 КУО/мл" }));
        Assert.Equal("PRELIMINARY", withIsolate["status"]!.GetValue<string>());
        Assert.True(withIsolate["growthDetected"]!.GetValue<bool>());
        Assert.Equal(1, withIsolate["isolates"]![0]!["isolateNumber"]!.GetValue<int>());
        var isolateId = withIsolate["isolates"]![0]!["id"]!.GetValue<string>();
        await Json(await Tech.PostAsJsonAsync($"/api/v1/lab/microbiology/isolates/{isolateId}/susceptibility", new { antibioticId = ab["CIP"], zoneMm = 6 }));

        // лаборант не завершує посів (лише лікар/адмін)
        await Problem(await Tech.PostAsJsonAsync($"{url}/transition", new { status = "COMPLETE" }), HttpStatusCode.Forbidden);
        var isolated = await Json(await Tech.PostAsJsonAsync($"{url}/transition", new { status = "ISOLATE" }));
        Assert.Equal("ISOLATED", isolated["status"]!.GetValue<string>());
        // ще одна дія тим самим переходом недоступна
        await Problem(await Tech.PostAsJsonAsync($"{url}/transition", new { status = "ISOLATE" }), HttpStatusCode.Conflict);

        var completed = await Json(await Doctor.PostAsJsonAsync($"{url}/transition", new { status = "COMPLETE", comment = "Остаточний звіт" }));
        Assert.Equal("COMPLETED", completed["status"]!.GetValue<string>());
        // результат тесту сформовано в замовленні і відправлено на верифікацію лікарем (не автоверифіковано)
        var order = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"/api/v1/lab/orders/{completed["orderId"]}"));
        var test = order["tests"]!.AsArray().First(t => t!["id"]!.GetValue<string>() == completed["orderTestId"]!.GetValue<string>())!;
        Assert.Equal("RESULTED", test["status"]!.GetValue<string>());
        var res = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"/api/v1/lab/results/{completed["orderTestId"]}"));
        Assert.Contains("Escherichia", res["stringValue"]!.GetValue<string>());
        Assert.False(res["isAutoVerified"]!.GetValue<bool>());

        // завершений посів незмінний
        await Problem(await Tech.PutAsJsonAsync(url, new { cfuPerMl = "x" }), HttpStatusCode.Conflict);
        await Problem(await Tech.PostAsJsonAsync($"{url}/isolates", new { organismId = ecol }), HttpStatusCode.Conflict);
        await Problem(await Doctor.DeleteAsync(url), HttpStatusCode.Conflict);
        await Problem(await Tech.PostAsJsonAsync($"/api/v1/lab/microbiology/isolates/{isolateId}/susceptibility", new { antibioticId = ab["GEN"], zoneMm = 10 }), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Growth_without_isolates_cannot_complete_and_negative_culture_completes_with_no_growth_result()
    {
        // ріст відмічено, але ізолятів немає → завершити не можна
        var growth = await NewCulture();
        await Json(await Tech.PutAsJsonAsync($"/api/v1/lab/microbiology/cultures/{growth}", new { growthDetected = true, growthIntensity = "SCANTY" }));
        await Json(await Tech.PostAsJsonAsync($"/api/v1/lab/microbiology/cultures/{growth}/transition", new { status = "PRELIMINARY" }));
        await Problem(await Doctor.PostAsJsonAsync($"/api/v1/lab/microbiology/cultures/{growth}/transition", new { status = "COMPLETE" }), HttpStatusCode.Conflict);

        // негативний посів: PRELIMINARY → COMPLETED, результат «росту не виявлено», норма
        var neg = await NewCulture();
        await Json(await Tech.PutAsJsonAsync($"/api/v1/lab/microbiology/cultures/{neg}", new { growthDetected = false }));
        await Json(await Tech.PostAsJsonAsync($"/api/v1/lab/microbiology/cultures/{neg}/transition", new { status = "PRELIMINARY" }));
        var done = await Json(await Doctor.PostAsJsonAsync($"/api/v1/lab/microbiology/cultures/{neg}/transition", new { status = "COMPLETE" }));
        Assert.Equal("COMPLETED", done["status"]!.GetValue<string>());
        var res = await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"/api/v1/lab/results/{done["orderTestId"]}"));
        Assert.Equal("росту не виявлено", res["stringValue"]!.GetValue<string>());
        Assert.Equal("NORMAL", res["flag"]!.GetValue<string>());
    }

    [Fact]
    public async Task Delete_is_allowed_only_for_incubating_culture_and_only_for_doctor_or_admin()
    {
        var id = await NewCulture();
        await Problem(await Tech.DeleteAsync($"/api/v1/lab/microbiology/cultures/{id}"), HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.NoContent, (await Doctor.DeleteAsync($"/api/v1/lab/microbiology/cultures/{id}")).StatusCode);
        await Problem(await Tech.GetAsync($"/api/v1/lab/microbiology/cultures/{id}"), HttpStatusCode.NotFound);

        var withIso = await NewCulture();
        await IsolateOf(withIso, await OrganismId("ECOL"));
        await Problem(await Doctor.DeleteAsync($"/api/v1/lab/microbiology/cultures/{withIso}"), HttpStatusCode.Conflict); // вже PRELIMINARY
    }
}
