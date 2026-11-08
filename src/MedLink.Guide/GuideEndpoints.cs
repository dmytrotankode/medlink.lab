// Маршрути API модуля Guide (minimal API). Плеєр — відкриті; конструктор — лише редактори (GuideOptions.CheckEditor).
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MedLink.Guide;

public static class GuideEndpoints
{
    public static IEndpointRouteBuilder MapGuide(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetRequiredService<GuideOptions>();
        var root = app.MapGroup(options.RoutePrefix).WithTags("Guide");
        root.AddEndpointFilter(ErrorFilter);

        var open = root;
        var edit = root.MapGroup("");
        edit.AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            var user = http.RequestServices.GetRequiredService<IGuideUserResolver>().Resolve(http);
            if (!http.RequestServices.GetRequiredService<GuideOptions>().CheckEditor(user))
                return Results.Problem(statusCode: 403, title: "Заборонено", detail: "Для конструктора інструкцій потрібна роль редактора");
            return await next(ctx);
        });

        // ---------------- Плеєр (відкрито)
        open.MapGet("/apps/{appCode}/catalog", async (HttpContext h, GuideService s, string appCode, string? role, string? route) =>
            Results.Json(await s.CatalogAsync(appCode, role, route, U(h)), GuideJson.Options));
        open.MapGet("/scenarios/{id}/play", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.PlayAsync(id, U(h)), GuideJson.Options));
        open.MapGet("/media/{id}", async (HttpContext h, GuideService s, string id) =>
        {
            var m = await s.GetMediaAsync(id);
            var mime = (m.Mime ?? "").ToLowerInvariant();
            var safe = mime.StartsWith("image/") && !mime.Contains("svg") || mime.StartsWith("audio/") || mime.StartsWith("video/");
            h.Response.Headers["X-Content-Type-Options"] = "nosniff";
            h.Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
            return Results.File(m.Content, safe ? mime : "application/octet-stream", safe ? null : (m.FileName ?? "file"));
        });
        open.MapPost("/progress", async (HttpContext h, GuideService s) =>
            Results.Json(await s.SaveProgressAsync(await Body<ProgressDto>(h), U(h)), GuideJson.Options));
        open.MapGet("/progress/me", async (HttpContext h, GuideService s) =>
            Results.Json(await s.MyProgressAsync(U(h)), GuideJson.Options));
        open.MapPost("/anchor-reports", async (HttpContext h, GuideService s) =>
            Results.Json(await s.ReportAnchorAsync(await Body<AnchorReportDto>(h), U(h)), GuideJson.Options));

        // ---------------- Конструктор (редактори)
        edit.MapPost("/apps", async (HttpContext h, GuideService s) =>
            Results.Json(await s.UpsertAppAsync(await Body<AppDto>(h), U(h)), GuideJson.Options));
        edit.MapGet("/apps/{appCode}/tree", async (HttpContext h, GuideService s, string appCode) =>
            Results.Json(await s.TreeAsync(appCode, U(h)), GuideJson.Options));

        edit.MapPost("/folders", async (HttpContext h, GuideService s) =>
            Results.Json(await s.CreateFolderAsync(await Body<FolderDto>(h), U(h)), GuideJson.Options, statusCode: 201));
        edit.MapPut("/folders/{id}", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.UpdateFolderAsync(id, await Body<FolderDto>(h), U(h)), GuideJson.Options));
        edit.MapDelete("/folders/{id}", async (HttpContext h, GuideService s, string id, bool? cascade) =>
        {
            await s.DeleteFolderAsync(id, cascade == true, U(h));
            return Results.NoContent();
        });
        edit.MapPost("/folders/{id}/move", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.MoveFolderAsync(id, await Body<MoveFolderDto>(h), U(h)), GuideJson.Options));

        edit.MapPost("/scenarios", async (HttpContext h, GuideService s) =>
            Results.Json(await s.CreateScenarioAsync(await Body<ScenarioDto>(h), U(h)), GuideJson.Options, statusCode: 201));
        edit.MapGet("/scenarios/{id}", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.GetScenarioAsync(id, U(h)), GuideJson.Options));
        edit.MapPut("/scenarios/{id}", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.UpdateScenarioAsync(id, await Body<ScenarioDto>(h), U(h)), GuideJson.Options));
        edit.MapDelete("/scenarios/{id}", async (HttpContext h, GuideService s, string id) =>
        {
            await s.DeleteScenarioAsync(id, U(h));
            return Results.NoContent();
        });
        edit.MapPost("/scenarios/{id}/duplicate", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.DuplicateScenarioAsync(id, U(h)), GuideJson.Options, statusCode: 201));
        edit.MapPut("/scenarios/{id}/steps", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.ReplaceStepsAsync(id, await Body<List<StepDto>>(h), U(h)), GuideJson.Options));
        edit.MapPut("/scenarios/{id}/variables", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.ReplaceVariablesAsync(id, await Body<List<VariableDto>>(h), U(h)), GuideJson.Options));
        edit.MapPost("/scenarios/{id}/publish", async (HttpContext h, GuideService s, string id) =>
        {
            var body = h.Request.ContentLength is > 0 ? await Body<PublishDto>(h) : new PublishDto();
            return Results.Json(await s.PublishAsync(id, body.Note, U(h)), GuideJson.Options);
        });
        edit.MapPost("/scenarios/{id}/unpublish", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.UnpublishAsync(id, U(h)), GuideJson.Options));
        edit.MapGet("/scenarios/{id}/export", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.ExportAsync(id, U(h)), GuideJson.Options));
        edit.MapPost("/apps/{appCode}/import", async (HttpContext h, GuideService s, string appCode) =>
            Results.Json(await s.ImportAsync(appCode, await Body<GuideBundle>(h), U(h)), GuideJson.Options));

        edit.MapGet("/apps/{appCode}/anchors", async (HttpContext h, GuideService s, string appCode) =>
            Results.Json(await s.ListAnchorsAsync(appCode, U(h)), GuideJson.Options));
        edit.MapPut("/anchors/{id}", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.UpdateAnchorAsync(id, await Body<AnchorDto>(h), U(h)), GuideJson.Options));
        edit.MapGet("/apps/{appCode}/anchors/health", async (HttpContext h, GuideService s, string appCode) =>
            Results.Json(await s.AnchorHealthAsync(appCode, U(h)), GuideJson.Options));
        edit.MapPost("/anchor-reports/{id}/accept", async (HttpContext h, GuideService s, string id) =>
            Results.Json(await s.AcceptReportAsync(id, U(h)), GuideJson.Options));

        edit.MapPost("/media", async (HttpContext h, GuideService s) =>
            Results.Json(await s.UploadMediaAsync(await Body<MediaUploadDto>(h), U(h),
                h.RequestServices.GetRequiredService<GuideOptions>().MaxMediaBytes), GuideJson.Options, statusCode: 201));

        return app;
    }

    private static GuideUser U(HttpContext h) => h.RequestServices.GetRequiredService<IGuideUserResolver>().Resolve(h);

    private static async Task<T> Body<T>(HttpContext h) where T : new()
    {
        try
        {
            return await h.Request.ReadFromJsonAsync<T>(GuideJson.Options) ?? throw new GuideException(400, "Порожнє тіло запиту");
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new GuideException(400, "Некоректний JSON: " + ex.Message);
        }
    }

    private static async ValueTask<object?> ErrorFilter(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        try { return await next(ctx); }
        catch (GuideException ex)
        {
            return Results.Problem(statusCode: ex.Status, title: ex.Status switch { 400 => "Помилка валідації", 403 => "Заборонено", 404 => "Не знайдено", 409 => "Конфлікт", _ => "Помилка" }, detail: ex.Message);
        }
    }
}
