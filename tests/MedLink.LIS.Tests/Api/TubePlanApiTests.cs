// FR-PRE-004 через API: попередній план, створення замовлення за правилами тари, дозамовлення, оновлення схеми старої БД
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Seed;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class TubePlanApiTests
{
    private const string Patient = "pat-0000-0000-0000-000000000003";
    private readonly LisApiFactory _f;
    public TubePlanApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static string[] Codes(JsonNode tube) => tube["tests"]!.AsArray().Select(t => t!["code"]!.GetValue<string>()).ToArray();

    [Fact]
    public async Task Preview_puts_urine_culture_into_separate_container()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var plan = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders/tube-plan", new { profileIds = new[] { "PROF_URINE" }, testIds = new[] { "URINE_CULTURE" } }));
        var tubes = plan["tubes"]!.AsArray();
        Assert.Equal(2, tubes.Count);
        Assert.Equal(2, plan["newTubesCount"]!.GetValue<int>());
        var sep = tubes.Single(t => t!["isSeparate"]!.GetValue<bool>())!;
        Assert.Equal(new[] { "URINE_CULTURE" }, Codes(sep));
        Assert.Contains("SEPARATE_REQUIRED", sep["reasons"]!.AsArray().Select(r => r!.GetValue<string>()));
        Assert.NotEmpty(sep["reasonTexts"]!.AsArray());
    }

    [Fact]
    public async Task Order_creation_follows_plan_and_add_on_reuses_existing_tube()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var order = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, profileIds = new[] { "PROF_URINE", "PROF_BIOCHEM_BASE" }, testIds = new[] { "URINE_CULTURE" } }));
        var samples = order["samples"]!.AsArray();
        Assert.Equal(3, samples.Count); // сироватка + сеча + окремий стерильний контейнер
        var culture = samples.Single(s => s!["testCodes"]!.AsArray().Any(c => c!.GetValue<string>() == "URINE_CULTURE"))!;
        Assert.Single(culture["testCodes"]!.AsArray());
        Assert.Contains("SEPARATE_REQUIRED", culture["planReasons"]!.AsArray().Select(r => r!.GetValue<string>()));
        var serum = samples.Single(s => s!["testCodes"]!.AsArray().Any(c => c!.GetValue<string>() == "GLU"))!;
        Assert.Equal(0.35, serum["plannedVolumeMl"]!.GetValue<double>(), 3);
        Assert.Equal(1.2, serum["capacityMl"]!.GetValue<double>(), 3);
        // Порядок забору: сироватка (3) раніше за сечу (99), groupNumb відповідає порядку
        var ordered = samples.OrderBy(s => s!["groupNumb"]!.GetValue<int>()).Select(s => s!["orderOfDrawIndex"]!.GetValue<int>()).ToList();
        Assert.Equal(ordered.OrderBy(x => x), ordered);

        var orderId = order["id"]!.GetValue<string>();
        var preview = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders/tube-plan", new { orderId, testIds = new[] { "CRP", "U_PH" } }));
        var crp = preview["tubes"]!.AsArray().Single(t => Codes(t!).Contains("CRP"))!;
        Assert.Equal(serum["id"]!.GetValue<string>(), crp["existingSampleId"]!.GetValue<string>());
        Assert.Equal(0, preview["newTubesCount"]!.GetValue<int>()); // U_PH уже в замовленні, CRP додається до сироватки

        var after = await Json(await reg.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/tests", new { testIds = new[] { "CRP", "TSH" } }));
        Assert.Equal(3, after["samples"]!.AsArray().Count);
        var serumAfter = after["samples"]!.AsArray().Single(s => s!["id"]!.GetValue<string>() == serum["id"]!.GetValue<string>())!;
        Assert.Contains("TSH", serumAfter["testCodes"]!.AsArray().Select(c => c!.GetValue<string>()));
    }

    [Fact]
    public async Task Serum_volume_overflow_creates_second_tube()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var serumTests = new[] { "GLU", "CREAT", "UREA", "ALT", "AST", "BIL_TOT", "PROT_TOT", "ALB", "GGT", "ALP", "AMYL", "CHOL", "HDL", "LDL", "TG", "URIC", "CRP", "K", "NA", "CL", "CA", "FE", "TROP_I", "TSH", "FT4", "PSA", "FPSA" };
        var plan = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders/tube-plan", new { testIds = serumTests }));
        var tubes = plan["tubes"]!.AsArray();
        Assert.Equal(2, tubes.Count);
        Assert.All(tubes, t => Assert.True(t!["usedVolumeMl"]!.GetValue<double>() <= 1.2 + 1e-9));
        Assert.Contains("VOLUME_SPLIT", tubes[1]!["reasons"]!.AsArray().Select(r => r!.GetValue<string>()));
        Assert.Equal(serumTests.Length, tubes.Sum(t => Codes(t!).Length));
    }

    [Fact]
    public async Task Rules_are_editable_in_test_dictionary()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        await Json(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests", new { code = "ZZ_SEP_TUBE", name = "Тест окремої пробірки", unit = "од", biomaterialTypeId = 4, tubeTypeId = 7, category = "BIOCHEM", requiresSeparateTube = false }));
        await Json(await admin.PutAsJsonAsync("/api/v1/lab/dictionaries/tests/ZZ_SEP_TUBE", new { requiresSeparateTube = true, requiredVolumeMl = 0.3, tubeCompatibilityGroup = "SPECIAL" }));
        var got = await Json(await admin.GetAsync("/api/v1/lab/dictionaries/tests/ZZ_SEP_TUBE"));
        Assert.True(got["requiresSeparateTube"]!.GetValue<bool>());
        var plan = await Json(await admin.PostAsJsonAsync("/api/v1/lab/orders/tube-plan", new { testIds = new[] { "GLU", "ZZ_SEP_TUBE" } }));
        Assert.Equal(2, plan["tubes"]!.AsArray().Count);
    }

    [Fact]
    public async Task Schema_upgrader_adds_missing_columns_and_tables_to_old_database()
    {
        var path = Path.Combine(Path.GetTempPath(), $"medlink-lis-upgrade-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LisDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        try
        {
            await using (var db = new LisDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                // Імітуємо БД попередньої версії
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE lab_test_definition DROP COLUMN requires_separate_tube");
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE lab_tube_type DROP COLUMN usable_volume_ml");
                await db.Database.ExecuteSqlRawAsync("DROP TABLE lab_reagent_lot");
            }
            await using (var db = new LisDbContext(options))
            {
                var applied = await SchemaUpgrader.UpgradeAsync(db);
                Assert.Contains(applied, s => s.Contains("requires_separate_tube") && s.Contains("NOT NULL DEFAULT 0"));
                Assert.Contains(applied, s => s.Contains("usable_volume_ml"));
                Assert.Contains(applied, s => s.Contains("CREATE TABLE \"lab_reagent_lot\""));
                Assert.Equal(0, await db.Tests.CountAsync(t => t.RequiresSeparateTube));
                Assert.Equal(0, await db.ReagentLots.CountAsync());
                Assert.Empty(await SchemaUpgrader.UpgradeAsync(db)); // ідемпотентно
            }
        }
        finally { try { File.Delete(path); } catch { /* ignore */ } }
    }
}
