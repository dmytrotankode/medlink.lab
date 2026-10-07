using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Kestrel URLs to listen on all interfaces at port 5055
builder.WebHost.UseUrls("http://0.0.0.0:5055");

// 2. Add controllers and OpenAPI / Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MedLink LIS 3.0 Web API (.NET 8 Core)",
        Version = "v1",
        Description = "Laboratory Information System REST API for MedLink MIS (evomis) platform with SQLite & Delphi Cascade Architecture"
    });
});

// 3. Add DbContext with local SQLite database (prototype: sample DB copied into App_Data)
var repoRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".."));
var appData = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appData);
var dbPath = Path.Combine(appData, "medlink_lab_local.db");
var sampleDb = Path.Combine(repoRoot, "db", "medlink_lab_local.sample.db");
if (!File.Exists(dbPath) && File.Exists(sampleDb)) File.Copy(sampleDb, dbPath);
builder.Services.AddDbContext<MedLinkLabDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// 4. Register Delphi Multi-Layer Cascade Resolver Service
builder.Services.AddScoped<ILabNormsCascadeService, LabNormsCascadeService>();

// 5. Add CORS policy for web frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Enable CORS
app.UseCors("AllowAll");

// Enable standard static files for Swagger UI embedded assets (js/css)
app.UseStaticFiles();

// Enable Swagger and Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MedLink LIS 3.0 API v1");
    c.RoutePrefix = "swagger";
});

// Custom Physical File Provider for serving MedLink Vue frontend & documentation
var rootDir = Path.Combine(repoRoot, "src", "MedLink.LIS.Web.legacy-prototype");
if (Directory.Exists(rootDir))
{
    var fileProvider = new PhysicalFileProvider(rootDir);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider,
        RequestPath = ""
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        RequestPath = "",
        ServeUnknownFileTypes = true
    });
}

// Redirect root to frontend
app.MapGet("/", () => Results.Redirect("/run_prototype.html"));

app.UseAuthorization();
app.MapControllers();

Console.WriteLine("================================================================================");
Console.WriteLine("MedLink LIS 3.0 .NET 8 Core Web API started successfully!");
Console.WriteLine("REST API & Swagger UI: http://localhost:5055/swagger");
Console.WriteLine("Vue Quasar Frontend:   http://localhost:5055/run_prototype.html");
Console.WriteLine("Master Specification:  (docs/analysis)");
Console.WriteLine($"Database:              {dbPath}");
Console.WriteLine("================================================================================");

app.Run();
