// =============================================================================
// MedLink LIS 4.0 — ASP.NET Core 8 Web API (SQLite, EF Core 8, без автентифікації користувачів)
// =============================================================================
using System.Text.Json.Serialization;
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
var sqlitePath = builder.Configuration["Database:SqlitePath"] ?? "App_Data/medlink_lis.db";
if (!Path.IsPathRooted(sqlitePath)) sqlitePath = Path.Combine(builder.Environment.ContentRootPath, sqlitePath);
Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);
builder.Services.AddDbContext<LisDbContext>(o => o.UseSqlite($"Data Source={sqlitePath}"));

// --- MVC / JSON / ProblemDetails ---
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
            var errors = ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Некоректне значення" : e.ErrorMessage).ToArray());
            var pd = new ProblemDetails { Type = "https://httpstatuses.com/400", Title = "Помилка валідації", Status = 400, Detail = "Перевірте поля запиту", Instance = ctx.HttpContext.Request.Path };
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
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<SampleService>();
builder.Services.AddScoped<ResultPipelineService>();
builder.Services.AddScoped<WorklistService>();
builder.Services.AddScoped<QcService>();
builder.Services.AddScoped<ConnectorService>();
builder.Services.AddScoped<ConnectorApiKeyFilter>();
builder.Services.AddScoped<AnalyzerService>();
builder.Services.AddScoped<LogisticsService>();
builder.Services.AddScoped<BiobankService>();
builder.Services.AddScoped<ReagentService>();
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
builder.Services.AddScoped<DemoDataSeeder>();
// Парсер повідомлень аналізаторів: локальна реалізація; замінюється на MedLink.LIS.Core.Protocols однією реєстрацією
builder.Services.AddSingleton<IAnalyzerMessageParser, AnalyzerMessageParserFallback>();
builder.Services.AddSingleton<RetentionCleanupService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RetentionCleanupService>());

var app = builder.Build();

// --- Створення схеми та сід (ідемпотентно, лише порожні таблиці) ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await scope.ServiceProvider.GetRequiredService<SeedService>().SeedAsync();
    if (app.Configuration.GetValue<bool?>("Lab:SeedDemoData") ?? true)
    {
        try { await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(); }
        catch (Exception ex) { app.Logger.LogError(ex, "Помилка створення демо-даних"); }
    }
}

app.UseMiddleware<ProblemDetailsMiddleware>();
app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "MedLink LIS 4.0 API v1"); c.RoutePrefix = "swagger"; c.DocumentTitle = "MedLink LIS 4.0 — API"; });
app.UseMiddleware<CurrentEmployeeMiddleware>();
app.UseRouting();
app.MapControllers();

// Health
app.MapGet("/health", async (LisDbContext db) =>
{
    var ok = await db.Database.CanConnectAsync();
    return Results.Json(new { status = ok ? "Healthy" : "Unhealthy", db = ok ? "ok" : "error", version = "4.0.0", utc = DateTime.UtcNow }, statusCode: ok ? 200 : 503);
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

app.Logger.LogInformation("MedLink LIS 4.0 API запущено. Swagger: /swagger · БД: {Db}", sqlitePath);
app.Run();

/// <summary>Точка входу (для WebApplicationFactory в інтеграційних тестах).</summary>
public partial class Program { }
