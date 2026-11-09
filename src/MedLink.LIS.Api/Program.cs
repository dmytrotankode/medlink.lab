// =============================================================================
// MedLink LIS 4.0 — ASP.NET Core 8 Web API (PostgreSQL — основна СУБД; SQLite — лише dev/тестовий провайдер; EF Core 8,
// без автентифікації користувачів)
// =============================================================================
using System.Text.Json.Serialization;
using MedLink.Guide;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Seed;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services;
using MedLink.LIS.Api.Services.Parsing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- База даних: SQLite (Database:SqlitePath, типово App_Data/medlink_lis.db відносно content root) ---
// Шлях обчислюється ліниво з IConfiguration контейнера, щоб перевизначення (WebApplicationFactory у тестах) застосовувались
static string ResolveSqlitePath(IConfiguration cfg, IHostEnvironment env)
{
    var path = cfg["Database:SqlitePath"] ?? "App_Data/medlink_lis.db";
    if (!Path.IsPathRooted(path)) path = Path.Combine(env.ContentRootPath, path);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    return path;
}
// Database:Provider = Postgres (production, типово в appsettings.json; Database:ConnectionString) | Sqlite (dev/тести; appsettings.Development.json).
// Дати зберігаються як є (timestamp without time zone, значення — UTC; legacy-режим Npgsql).
// УВАГА: провайдер визначається ЛІНИВО (у момент створення контексту) з остаточної конфігурації: перевизначення з тестової фабрики
// (WebApplicationFactory.ConfigureAppConfiguration) застосовуються лише під час Build() і недоступні в коді верхнього рівня.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true); // до першого використання Npgsql; для SQLite нешкідливо
IConfiguration? finalConfig = null; // = app.Configuration після Build()
IConfiguration Cfg() => finalConfig ?? builder.Configuration;
bool UsePostgres()
{
    var name = Cfg()["Database:Provider"];
    if (string.IsNullOrWhiteSpace(name) || name.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)) return false;
    if (name.Equals("Postgres", StringComparison.OrdinalIgnoreCase)) return true;
    throw new InvalidOperationException($"Database:Provider = «{name}» не підтримується (Postgres | Sqlite)");
}
// Налаштування текстових запитів PostgreSQL (ILIKE, українська колація): уточнюються при старті після перевірки кластера
var pgQuery = new PgQuerySettings { Collation = builder.Configuration["Database:Collation"] ?? "uk-x-icu" };
if (string.IsNullOrWhiteSpace(pgQuery.Collation)) pgQuery.Collation = null;
string PgConnectionString(IConfiguration cfg) => PgConnectionOptions.Normalize(
    cfg["Database:ConnectionString"] is { Length: > 0 } cs ? cs : args.Contains("--export-pg-ddl") ? "Host=localhost" : throw new InvalidOperationException("Database:ConnectionString не задано (Database:Provider = Postgres). Для локальної розробки задайте Database:Provider = Sqlite."));
builder.Services.AddDbContext<LisDbContext>((sp, o) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    finalConfig ??= cfg;
    if (UsePostgres()) o.UseNpgsql(PgConnectionString(cfg), npg => npg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)) // повтор при тимчасових збоях з'єднання (explicit-транзакцій у коді немає)
         .AddInterceptors(new PgQueryInterceptor(pgQuery));
    else o.UseSqlite($"Data Source={ResolveSqlitePath(cfg, sp.GetRequiredService<IHostEnvironment>())}");
});

// --- MedLink Guide: інтерактивні покрокові інструкції (окрема бібліотека/схема guide, API /api/v1/guide) ---
// На SQLite — окремий файл App_Data/medlink_guide.db; на PostgreSQL — та сама БД, схема "guide" (db/postgres/guide).
var guideDbPath = Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Database:GuideSqlitePath"] ?? "App_Data/medlink_guide.db");
builder.Services.AddGuide(builder.Configuration, o =>
{
    o.ConfigureDb = db =>
    {
        if (UsePostgres()) db.UseNpgsql(PgConnectionString(Cfg())).AddInterceptors(new PgQueryInterceptor(pgQuery));
        else { Directory.CreateDirectory(Path.GetDirectoryName(guideDbPath)!); db.UseSqlite($"Data Source={guideDbPath}"); }
    };
    o.AllowAnonymousEditing = true; // у ЛІС немає автентифікації (її надає evomis); розмежування — через Guide:EditorRoles у evomis
});
// --- MVC / JSON / ProblemDetails ---
static string LocalizeModelKey(string key)
{
    var k = (key ?? "").TrimStart('$', '.');
    return k is "req" or "request" or "dto" or "body" ? "" : k;
}

static string LocalizeModelError(string? message)
{
    var m = message ?? "";
    if (m.Contains("could not be converted to System.Decimal") || m.Contains("System.Double") || m.Contains("System.Int"))
        return "Невірний формат числа: введіть число, наприклад 550 або 550,50";
    if (m.Contains("System.DateTime") || m.Contains("System.DateOnly")) return "Невірний формат дати";
    if (m.Contains("System.Boolean")) return "Очікується значення «так» або «ні»";
    if (m.Contains("is required") || m.Contains("field is required")) return "Не заповнено обов’язкове поле";
    if (m.Length == 0 || !m.Any(c => c >= 'Ѐ' && c <= 'ӿ')) return "Некоректне значення";
    return m;
}

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = ctx =>
        {
            // Тексти стандартного зв'язування моделі (англійські, з технічними шляхами) замінюються українськими:
            // користувач не має бачити «The JSON value could not be converted to System.Decimal. Path: $.amount».
            var errors = ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
                .GroupBy(kv => LocalizeModelKey(kv.Key))
                .ToDictionary(g => g.Key, g => g.SelectMany(kv => kv.Value!.Errors.Select(e => LocalizeModelError(e.ErrorMessage))).Distinct().ToArray());
            var pd = new ProblemDetails { Type = "https://httpstatuses.com/400", Title = "Помилка валідації", Status = 400, Detail = "Перевірте введені значення", Instance = ctx.HttpContext.Request.Path };
            pd.Extensions["errors"] = errors;
            return new BadRequestObjectResult(pd) { ContentTypes = { "application/problem+json" } };
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MedLink LIS 4.0 API",
        Version = "v1",
        Description = "Лабораторна інформаційна система для МІС MedLink (evomis): замовлення, проби, робочий лист, каскад норм Simplex, ВКЯ (Вестгард), коннектор аналізаторів, біобанк, мікробіологія (EUCAST), аналітика TAT. Без автентифікації користувачів — контекст співробітника у заголовку X-MedLink-Employee-Id."
    });
    c.OperationFilter<SwaggerHeaderFilter>();
    var xml = Path.Combine(AppContext.BaseDirectory, "MedLink.LIS.Api.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml, true);
    c.CustomSchemaIds(t => t.FullName!.Replace("+", "."));
});
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("Content-Disposition", "X-MedLink-Pdf-Fallback")));

// --- Сервіси домену ---
builder.Services.AddScoped<ICurrentEmployee, CurrentEmployee>();
builder.Services.AddScoped<IRolePolicy, RolePolicy>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<INumeratorService, NumeratorService>();
builder.Services.AddScoped<OrderStateService>();
builder.Services.AddScoped<TubePlanService>();
builder.Services.AddScoped<OrgDictionaryService>();
builder.Services.AddScoped<SendOutService>();
// --- ЕСОЗ (eHealth): режими Mock | Replay | Live, клієнт, outbox-воркер (Services/Ehealth/EhealthServiceCollectionExtensions.cs) ---
MedLink.LIS.Api.Services.Ehealth.EhealthServiceCollectionExtensions.AddEhealthIntegration(builder);
builder.Services.AddScoped<ReferralService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<RoutingService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<MisCatalogService>();
builder.Services.AddScoped<MisReferralService>();
builder.Services.AddScoped<WorkReportsService>();
builder.Services.AddScoped<PathologyService>();
builder.Services.AddScoped<SampleService>();
builder.Services.AddScoped<ResultPipelineService>();
builder.Services.AddScoped<WorklistService>();
builder.Services.AddScoped<QcService>();
builder.Services.AddScoped<CalibrationService>();
builder.Services.AddScoped<ConnectorService>();
builder.Services.AddScoped<ConnectorApiKeyFilter>();
builder.Services.AddScoped<AnalyzerService>();
builder.Services.AddScoped<LogisticsService>();
builder.Services.AddScoped<BiobankService>();
builder.Services.AddScoped<QualityRegistryService>();
builder.Services.AddScoped<ReagentService>();
builder.Services.AddScoped<ReagentConsumptionService>();
builder.Services.AddScoped<MicrobiologyService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<PatientService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<DictionaryService>();
builder.Services.AddScoped<SectionJournalService>();
builder.Services.AddScoped<ProgressiveReleaseService>();
builder.Services.AddScoped<SampleProcessingService>();
builder.Services.AddScoped<OrderMatrixService>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<MisCatalogSeeder>();
builder.Services.AddScoped<BiochemCatalogSeeder>();
builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.AddScoped<PathologyDemoSeeder>();
// Парсер повідомлень аналізаторів: локальна реалізація; замінюється на MedLink.LIS.Core.Protocols однією реєстрацією
builder.Services.AddSingleton<IAnalyzerMessageParser, AnalyzerMessageParserFallback>();
MedLink.LIS.Api.Services.Notifications.NotificationsServiceCollectionExtensions.AddNotificationsAndMisDelivery(builder.Services);
builder.Services.AddSingleton<RetentionCleanupService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RetentionCleanupService>());

var app = builder.Build();
finalConfig = app.Configuration;

// --- Експорт DDL PostgreSQL для перенесення в MedLink: --export-pg-ddl <файл> (без запуску сервера) ---
var ddlArg = Array.IndexOf(args, "--export-pg-ddl");
if (ddlArg >= 0)
{
    using var ddlScope = app.Services.CreateScope();
    var target = ddlArg + 1 < args.Length ? args[ddlArg + 1] : "medlink_lis_schema.sql";
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
    File.WriteAllText(target, PostgresDdlGenerator.Generate(ddlScope.ServiceProvider.GetRequiredService<LisDbContext>()), new System.Text.UTF8Encoding(false));
    Console.WriteLine($"DDL PostgreSQL збережено: {Path.GetFullPath(target)}");
    return;
}

// --- Створення схеми та сід (ідемпотентно, лише порожні таблиці) ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
    var legacyBackup = await SchemaUpgrader.BackupIfIncompatibleAsync(db);
    if (legacyBackup != null) app.Logger.LogWarning("БД попередньої структури (до вирівнювання з MedLink) збережено як {Backup}; створюється нова БД", legacyBackup);
    await db.Database.EnsureCreatedAsync();
    foreach (var change in await SchemaUpgrader.UpgradeAsync(db)) app.Logger.LogWarning("Оновлення схеми БД: {Sql}", change);
    if (db.Database.IsNpgsql())
    {
        // Версіоновані скрипти PostgreSQL (db/postgres/upgrades): індекси-вирази, trigram-пошук тощо; один екземпляр за раз (advisory lock)
        var pgWarnings = new List<string>();
        foreach (var script in await PgSchemaMigrator.ApplyAsync(db, pgWarnings)) app.Logger.LogWarning("Застосовано скрипт схеми PostgreSQL: {Script}", script);
        foreach (var w in pgWarnings) app.Logger.LogWarning("Схема PostgreSQL: {Warning}", w);
        var drift = await SchemaUpgrader.DetectDriftAsync(db);
        if (drift.Count > 0) app.Logger.LogWarning("Розбіжності типів колонок БД і моделі ({Count}): {Drift}", drift.Count, string.Join("; ", drift.Take(10)));
        // Українська колація сортування: лише якщо вона є в кластері (ICU); інакше сортування за колацією БД
        if (pgQuery.Collation != null)
        {
            var have = await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_collation WHERE collname = {0}", pgQuery.Collation).ToListAsync();
            if (have.FirstOrDefault() == 0) { app.Logger.LogWarning("Колація {Collation} відсутня в кластері PostgreSQL — сортування за колацією БД", pgQuery.Collation); pgQuery.Collation = null; }
        }
    }
    if (db.Database.IsSqlite()) await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await scope.ServiceProvider.GetRequiredService<SeedService>().SeedAsync();
    await DictionaryRuntime.SeedAndApplyAsync(db);
    await scope.ServiceProvider.GetRequiredService<MisCatalogSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<BiochemCatalogSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<MisCatalogService>().RecomputeReadinessAsync();
    if (app.Configuration.GetValue<bool?>("Lab:SeedDemoData") ?? true)
    {
        try { await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(); }
        catch (Exception ex) { app.Logger.LogError(ex, "Помилка створення демо-даних"); }
        using var pathScope = app.Services.CreateScope();
        try { await pathScope.ServiceProvider.GetRequiredService<PathologyDemoSeeder>().SeedAsync(); }
        catch (Exception ex) { app.Logger.LogError(ex, "Помилка створення демо-даних патоморфології"); }
    }
}

// --- Перевірка виробничої конфігурації: гучне попередження, без падіння ---
var configWarnings = StartupConfigCheck.Evaluate(app.Configuration, app.Environment);
StartupConfigCheck.LogWarnings(app.Logger, configWarnings);

await app.Services.EnsureGuideDatabaseAsync();
await app.Services.SeedGuideBundlesAsync();

// Розгортання в підпапці за зворотним проксі (напр. PathBase=/lab)
var pathBase = app.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase)) app.UsePathBase(pathBase.TrimEnd('/'));
app.UseMiddleware<ProblemDetailsMiddleware>();
app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "MedLink LIS 4.0 API v1"); c.RoutePrefix = "swagger"; c.DocumentTitle = "MedLink LIS 4.0 — API"; });
app.UseMiddleware<CurrentEmployeeMiddleware>();
app.UseRouting();
app.MapControllers();
app.MapGuide();

// Health
app.MapGet("/health", async (LisDbContext db, MedLink.LIS.Api.Services.Notifications.NotifyOptions notify, MedLink.LIS.Api.Services.Notifications.MisDeliveryOptions mis) =>
{
    var ok = await db.Database.CanConnectAsync();
    var integrations = new
    {
        auth = AuthModes.Resolve(app.Configuration),
        notifyEmail = notify.Email.IsConfigured, notifySms = notify.Sms.IsConfigured, notifyViber = notify.Viber.IsConfigured, notifyTelegram = notify.Telegram.IsConfigured,
        misDelivery = mis.IsConfigured ? "Webhook" : "Disabled"
    };
    return Results.Json(new
    {
        status = ok ? "Healthy" : "Unhealthy", db = ok ? "ok" : "error", version = "4.0.0", utc = DateTime.UtcNow,
        configWarnings = configWarnings.Select(w => new { w.Code, w.Message }), integrations
    }, statusCode: ok ? 200 : 503);
});

// Публічна сторінка верифікації бланка за QR (без ПІБ): /verify/{token}
app.MapGet("/verify/{token}", async (string token, OrderService orders) =>
{
    var result = await orders.VerifyByTokenAsync(token);
    var ok = result != null;
    var html = $"<!doctype html><html lang=\"uk\"><head><meta charset=\"utf-8\"><title>Перевірка бланка</title><style>body{{font-family:Arial,sans-serif;background:#f4f6f9;margin:0}}.c{{max-width:520px;margin:12vh auto;background:#fff;padding:28px 36px;border-radius:8px;box-shadow:0 2px 12px rgba(0,0,0,.08)}}h1{{color:{(ok ? "#15803d" : "#b91c1c")};font-size:20px}}</style></head><body><div class=\"c\"><h1>{(ok ? "Бланк автентичний ✔" : "Бланк не знайдено ✖")}</h1>" +
               (ok ? $"<p>Номер замовлення: <b>{System.Net.WebUtility.HtmlEncode(result!.GetType().GetProperty("orderNumber")!.GetValue(result)?.ToString())}</b><br>Видано: {result.GetType().GetProperty("releasedAt")!.GetValue(result)}<br>Лабораторія: {System.Net.WebUtility.HtmlEncode(result.GetType().GetProperty("lab")!.GetValue(result)?.ToString())}</p>" : "<p>Токен недійсний або результати ще не видано.</p>") +
               "</div></body></html>";
    return Results.Content(html, "text/html; charset=utf-8", statusCode: ok ? 200 : 404);
});

// SPA fallback: усе, що не /api, не /swagger і не файл → wwwroot/index.html; невідомі /api → 404 ProblemDetails
app.MapFallback(async ctx =>
{
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        ctx.Response.StatusCode = 404;
        ctx.Response.ContentType = "application/problem+json; charset=utf-8";
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails { Type = "https://httpstatuses.com/404", Title = "Не знайдено", Status = 404, Detail = $"Маршрут {ctx.Request.Path} не існує", Instance = ctx.Request.Path });
        return;
    }
    var index = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
    if (File.Exists(index)) { ctx.Response.ContentType = "text/html; charset=utf-8"; await ctx.Response.SendFileAsync(index); }
    else { ctx.Response.StatusCode = 404; await ctx.Response.WriteAsync("Фронтенд не зібрано (wwwroot/index.html відсутній)"); }
});

app.Logger.LogInformation("MedLink LIS 4.0 API запущено. Swagger: /swagger · БД: {Db}", UsePostgres() ? "PostgreSQL" : "SQLite (dev) " + ResolveSqlitePath(app.Configuration, app.Environment));
app.Run();

/// <summary>Точка входу (для WebApplicationFactory в інтеграційних тестах).</summary>
public partial class Program { }
