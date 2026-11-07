// Підключення модуля до хоста: AddGuide (DI), MapGuide (маршрути), EnsureGuideDatabaseAsync (схема БД), сід.
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MedLink.Guide;

public static class GuideExtensions
{
    /// <summary>Реєструє модуль. Провайдер БД передає хост через <see cref="GuideOptions.ConfigureDb"/>.</summary>
    public static IServiceCollection AddGuide(this IServiceCollection services, Action<GuideOptions> configure) =>
        services.AddGuide(null, configure);

    /// <summary>Те саме + читає секцію <c>Guide</c> з конфігурації (RoutePrefix, Schema, AllowAnonymousEditing, EditorRoles, ...), потім застосовує <paramref name="configure"/>.</summary>
    public static IServiceCollection AddGuide(this IServiceCollection services, IConfiguration? configuration, Action<GuideOptions>? configure)
    {
        var options = new GuideOptions();
        configuration?.GetSection("Guide").Bind(options);
        configure?.Invoke(options);
        if (options.ConfigureDb == null) throw new InvalidOperationException("Guide: не задано ConfigureDb (UseSqlite/UseNpgsql).");

        services.AddSingleton(options);
        services.TryAddSingleton<IGuideUserResolver, HeaderGuideUserResolver>();
        services.AddDbContext<GuideDbContext>((sp, o) => sp.GetRequiredService<GuideOptions>().ConfigureDb!(o));
        services.AddScoped<GuideService>();
        return services;
    }

    /// <summary>Створює таблиці Guide, якщо їх немає (ідемпотентно). SQLite — зі скрипта EF; PostgreSQL — із вбудованого 001_guide_schema.sql.</summary>
    public static async Task EnsureGuideDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuideDbContext>();
        var provider = db.Database.ProviderName ?? "";
        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            var asm = typeof(GuideExtensions).Assembly;
            using var stream = asm.GetManifestResourceStream("MedLink.Guide.Sql.001_guide_schema.sql") ?? throw new InvalidOperationException("Немає вбудованого SQL схеми Guide");
            using var reader = new StreamReader(stream);
            await db.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync(ct), ct);
            return;
        }
        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var conn = db.Database.GetDbConnection();
            var wasClosed = conn.State != System.Data.ConnectionState.Open;
            if (wasClosed) await conn.OpenAsync(ct);
            try
            {
                using var check = conn.CreateCommand();
                check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='gd_app'";
                if (Convert.ToInt32(await check.ExecuteScalarAsync(ct)) > 0) return;
                await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript(), ct);
            }
            finally { if (wasClosed) await conn.CloseAsync(); }
            return;
        }
        await db.Database.EnsureCreatedAsync(ct);
    }

    /// <summary>Завантажує вбудовані бандли Seed/*.json (ідемпотентно за кодами). Повертає кількість оброблених бандлів.</summary>
    public static async Task<int> SeedGuideBundlesAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<GuideService>();
        var asm = typeof(GuideExtensions).Assembly;
        var n = 0;
        foreach (var name in asm.GetManifestResourceNames().Where(x => x.StartsWith("MedLink.Guide.Seed.", StringComparison.Ordinal) && x.EndsWith(".json")).OrderBy(x => x))
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            var bundle = await JsonSerializer.DeserializeAsync<GuideBundle>(stream, GuideJson.Options, ct);
            if (bundle?.App?.Code == null) continue;
            await svc.ImportAsync(bundle.App.Code, bundle, new GuideUser());
            n++;
        }
        return n;
    }
}
