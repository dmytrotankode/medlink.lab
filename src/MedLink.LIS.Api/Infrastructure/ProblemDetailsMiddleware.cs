// Глобальна обробка винятків у форматі RFC 7807
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Infrastructure;

public sealed class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (LisException ex)
        {
            await WriteAsync(context, ex.StatusCode, ex.Title, ex.Message, ex.Errors);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Помилка збереження у БД");
            await WriteAsync(context, 409, "Помилка збереження", "Порушення цілісності даних: " + (ex.InnerException?.Message ?? ex.Message), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Необроблена помилка {Path}", context.Request.Path);
            await WriteAsync(context, 500, "Внутрішня помилка сервера", ex.Message, null);
        }
    }

    private static async Task WriteAsync(HttpContext ctx, int status, string title, string detail, IDictionary<string, string[]>? errors)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json; charset=utf-8";
        var pd = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = ctx.Request.Path
        };
        if (errors != null) pd.Extensions["errors"] = errors;
        pd.Extensions["traceId"] = ctx.TraceIdentifier;
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(pd, JsonOptions));
    }
}
