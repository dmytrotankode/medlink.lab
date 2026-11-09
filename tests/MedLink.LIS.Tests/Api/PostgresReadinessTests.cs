// Перевірки готовності до PostgreSQL: пошук без урахування регістру кирилиці, українське сортування, паралельні лічильники,
// версіоновані скрипти схеми, параметри підключення. Тести, що залежать від провайдера, виконуються лише на PostgreSQL
// (LIS_TEST_PG); на SQLite вони перевіряють лише спільну поведінку.
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Seed;
using MedLink.LIS.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class PostgresReadinessTests
{
    private readonly LisApiFactory _f;
    public PostgresReadinessTests(LisApiFactory f) => _f = f;

    private bool IsPg()
    {
        using var scope = _f.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<LisDbContext>().Database.IsNpgsql();
    }

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private async Task<string> CreatePatient(string last, string first)
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var dto = await Json(await reg.PostAsJsonAsync("/api/v1/lab/patients", new { lastName = last, firstName = first, gender = "F", birthDate = "1991-01-01" }));
        return dto["id"]!.GetValue<string>();
    }

    private async Task<List<string>> SearchIds(string q)
    {
        var found = await Json(await _f.As(DemoDataSeeder.Registrar).GetAsync($"/api/v1/lab/patients?search={Uri.EscapeDataString(q)}&pageSize=100"));
        return found["items"]!.AsArray().Select(p => p!["id"]!.GetValue<string>()).ToList();
    }

    [Fact]
    public async Task Search_ignores_case_of_cyrillic_on_postgres_and_escapes_wildcards()
    {
        var id = await CreatePatient("Ґаврилюк-Пошук", "Мар'яна");
        Assert.Contains(id, await SearchIds("Ґаврилюк-Пошук")); // точний регістр — на будь-якому провайдері
        // % та _ — звичайні символи, а не шаблони: «%» не має знаходити всіх
        Assert.DoesNotContain(id, await SearchIds("%"));
        Assert.DoesNotContain(id, await SearchIds("Ґ_врилюк"));
        // Літеральні _ та % у даних знаходяться за точним введенням (ESCAPE '\')
        var special = await CreatePatient("Підкреслення_100%", "Тест");
        Assert.Contains(special, await SearchIds("Підкреслення_100%"));
        if (!IsPg()) return;
        foreach (var q in new[] { "ґаврилюк-пошук", "ҐАВРИЛЮК-ПОШУК", "аврилюк-П", "МАР'ЯНА" })
            Assert.Contains(id, await SearchIds(q));
        Assert.Contains(special, await SearchIds("підкреслення_100%"));
    }

    [Fact]
    public async Task Patients_are_sorted_in_ukrainian_alphabet_on_postgres()
    {
        if (!IsPg()) return;
        // Порядок українського алфавіту: Г < Ґ < Д < І < Ї < Й (колація БД en_US ставить Ґ першим, а Ї перед І)
        var names = new[] { "Йосипенко", "Їжак", "Іваненко", "Дубина", "Ґудзик", "Гюго" };
        foreach (var n in names) await CreatePatient(n, "Сортування");
        var found = await Json(await _f.As(DemoDataSeeder.Registrar).GetAsync($"/api/v1/lab/patients?search={Uri.EscapeDataString("Сортування")}&pageSize=100"));
        var caps = found["items"]!.AsArray().Select(p => p!["fullName"]!.GetValue<string>().Split(' ')[0]).ToList();
        Assert.Equal(new[] { "Гюго", "Ґудзик", "Дубина", "Іваненко", "Їжак", "Йосипенко" }, caps);
    }

    [Fact]
    public async Task Counters_and_numerators_do_not_duplicate_under_parallel_calls()
    {
        var code = "test_par_" + Guid.NewGuid().ToString("N")[..8];
        const int workers = 16, perWorker = 12;
        var counters = await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(async () =>
        {
            var list = new List<long>();
            for (var i = 0; i < perWorker; i++)
            {
                using var scope = _f.Services.CreateScope();
                list.Add(await scope.ServiceProvider.GetRequiredService<INumeratorService>().NextCounterAsync(code));
            }
            return list;
        })));
        var all = counters.SelectMany(x => x).OrderBy(x => x).ToList();
        Assert.Equal(workers * perWorker, all.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, workers * perWorker).Select(x => (long)x), all); // без пропусків

        var numeratorCode = "test_num_" + Guid.NewGuid().ToString("N")[..8];
        var at = new DateTime(2035, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        var numbers = await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(async () =>
        {
            var list = new List<string>();
            for (var i = 0; i < perWorker; i++)
            {
                using var scope = _f.Services.CreateScope();
                list.Add(await new NumeratorService(scope.ServiceProvider.GetRequiredService<LisDbContext>()).NextFormattedAsync(numeratorCode, "{yyMM}-{000000}", at));
            }
            return list;
        })));
        var nums = numbers.SelectMany(x => x).ToList();
        Assert.Equal(workers * perWorker, nums.Distinct().Count());
        Assert.Contains("3503-000192", nums);
        Assert.DoesNotContain("3503-000193", nums);

        // перехід періоду: перший номер нового місяця — 000001
        using var s = _f.Services.CreateScope();
        var next = await new NumeratorService(s.ServiceProvider.GetRequiredService<LisDbContext>()).NextFormattedAsync(numeratorCode, "{yyMM}-{000000}", new DateTime(2035, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal("3504-000001", next);
    }

    [Fact]
    public async Task Tube_barcodes_are_unique_under_parallel_calls()
    {
        var codes = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
        {
            var list = new List<string>();
            for (var i = 0; i < 8; i++)
            {
                using var scope = _f.Services.CreateScope();
                list.Add(await scope.ServiceProvider.GetRequiredService<INumeratorService>().NextTubeBarcodeAsync());
            }
            return list;
        })));
        var all = codes.SelectMany(x => x).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public async Task Postgres_schema_is_versioned_and_matches_model()
    {
        if (!IsPg()) return;
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var expected = PgSchemaMigrator.LoadScripts().Select(s => s.Version).ToList();
        var applied = await db.Database.SqlQueryRaw<int>("SELECT version AS \"Value\" FROM lab_schema_version ORDER BY version").ToListAsync();
        Assert.Equal(expected, applied);
        Assert.Empty(await SchemaUpgrader.DetectDriftAsync(db));
        // повторний запуск ідемпотентний
        Assert.Empty(await PgSchemaMigrator.ApplyAsync(db));
        Assert.DoesNotContain(await SchemaUpgrader.UpgradeAsync(db), x => x.StartsWith("ПОМИЛКА"));
    }

    [Fact]
    public void Upgrade_scripts_are_numbered_sequentially()
    {
        var scripts = PgSchemaMigrator.LoadScripts();
        Assert.NotEmpty(scripts);
        Assert.Equal(Enumerable.Range(1, scripts.Count), scripts.Select(s => s.Version));
    }

    [Fact]
    public void Connection_string_gets_pool_and_timeout_defaults_without_overriding_explicit_values()
    {
        var cs = PgConnectionOptions.Normalize("Host=db;Database=x;Username=u;Password=p;Maximum Pool Size=7;Command Timeout=5");
        Assert.Contains("Maximum Pool Size=7", cs);
        Assert.DoesNotContain("Maximum Pool Size=40", cs);
        Assert.Contains("Command Timeout=5", cs);
        Assert.DoesNotContain("Command Timeout=60", cs);
        Assert.Contains("Timeout=15", cs);
        Assert.Contains("Keepalive=30", cs);
        Assert.Contains("Minimum Pool Size=2", cs);
        Assert.Equal(cs, PgConnectionOptions.Normalize(cs)); // ідемпотентно
    }
}

/// <summary>
/// Режим дат Npgsql має бути ввімкнено ДО першого використання Npgsql у процесі тестів: фабрика тестів скидає схему PostgreSQL
/// (ResetPostgres) ще до виконання Program.cs, а Npgsql запам'ятовує перемикач при першому зверненні.
/// </summary>
internal static class NpgsqlTestSwitches
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Init() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
}
