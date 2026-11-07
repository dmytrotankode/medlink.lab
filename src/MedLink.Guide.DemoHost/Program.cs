// Автономний демо-хост MedLink Guide: dotnet run --project src/MedLink.Guide.DemoHost → http://localhost:5066
using MedLink.Guide;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "guide_demo.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddGuide(builder.Configuration, o =>
{
    o.ConfigureDb = db => db.UseSqlite($"Data Source={dbPath}");
    o.AllowAnonymousEditing = true;
});

var app = builder.Build();
await app.Services.EnsureGuideDatabaseAsync();
app.MapGuide();

// SDK та демо-сторінка зі спільної папки MedLink.Guide.Web (../MedLink.Guide.Web)
var web = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "MedLink.Guide.Web"));
var files = new PhysicalFileProvider(web);
app.UseStaticFiles(new StaticFileOptions { FileProvider = files, RequestPath = "" });
app.MapGet("/", ctx => { ctx.Response.Redirect("/demo/index.html?api=1"); return Task.CompletedTask; });
app.Run("http://localhost:5066");
