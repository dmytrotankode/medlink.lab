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
            await WriteAsync(context, ex.StatusCode, ex.Title, ex.Message, ex.Errors, ex.Code);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Помилка збереження у БД");
            // PostgreSQL суворіше за SQLite (довжина varchar, NOT NULL, FK): повідомлення українською без технічних подробиць СУБД
            var (status, title, detail) = (ex.InnerException as Npgsql.PostgresException) switch
            {
                { SqlState: "23505" } => (409, "Дублікат", "Запис із таким значенням уже існує"),
                { SqlState: "23503" } => (409, "Порушення зв'язку", "Запис пов'язаний з іншими даними або посилається на неіснуючий запис"),
                { SqlState: "23502" } pg => (400, "Не заповнено поле", $"Обов'язкове поле «{pg.ColumnName}» не заповнено"),
                { SqlState: "22001" } => (400, "Занадто довге значення", "Одне зі значень перевищує допустиму довжину поля"),
                { SqlState: "40001" or "40P01" } => (409, "Конфлікт одночасного запису", "Дані одночасно змінено іншим користувачем — повторіть дію"),
                _ => (409, "Помилка збереження", "Порушення цілісності даних: " + (ex.InnerException?.Message ?? ex.Message))
            };
            await WriteAsync(context, status, title, detail, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Необроблена помилка {Path}", context.Request.Path);
            await WriteAsync(context, 500, "Внутрішня помилка сервера", ex.Message, null);
        }
    }

    private static async Task WriteAsync(HttpContext ctx, int status, string title, string detail, IDictionary<string, string[]>? errors, string? code = null)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json; charset=utf-8";
        var pd = new ProblemDetails
        {
            Type = code == null ? $"https://httpstatuses.com/{status}" : $"https://medlink.ua/problems/{code}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = ctx.Request.Path
        };
        if (errors != null) pd.Extensions["errors"] = errors;
        if (code != null) pd.Extensions["code"] = code;
        pd.Extensions["traceId"] = ctx.TraceIdentifier;
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(pd, JsonOptions));
    }
}
