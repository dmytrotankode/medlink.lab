// Поточний співробітник: Auth:Mode=None (Lab:DefaultEmployeeId) | Header (X-MedLink-Employee-Id) | Oidc (токен Identity MedLink → співробітник МІС)
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Services.MedLinkBridge;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Infrastructure;

public interface ICurrentEmployee
{
    string EmployeeId { get; }
    string? FullName { get; }
    string? LabRole { get; }
    string? DepartmentId { get; }
    string? Ip { get; }
    bool IsResolved { get; }
    void Set(OrgEmployee? employee, string requestedId, string? ip);
}

public sealed class CurrentEmployee : ICurrentEmployee
{
    public string EmployeeId { get; private set; } = "";
    public string? FullName { get; private set; }
    public string? LabRole { get; private set; }
    public string? DepartmentId { get; private set; }
    public string? Ip { get; private set; }
    public bool IsResolved { get; private set; }

    public void Set(OrgEmployee? employee, string requestedId, string? ip)
    {
        EmployeeId = employee?.Id ?? requestedId;
        FullName = employee?.Caption;
        LabRole = employee?.LabRole;
        DepartmentId = employee?.DepartmentId;
        Ip = ip;
        IsResolved = employee != null;
    }
}

public sealed class CurrentEmployeeMiddleware
{
    public const string HeaderName = "X-MedLink-Employee-Id";
    /// <summary>Маршрути без входу співробітника в режимі Oidc (задаються префіксами; додаткові — Auth:PublicPaths).</summary>
    private static readonly string[] DefaultPublicPrefixes =
    {
        "/api/v1/lab/auth/",       // налаштування входу для SPA (без секретів)
        "/api/v1/lab/connector/",  // коннектор аналізаторів: окремий X-MedLink-ApiKey
        "/api/v1/lab/portal/"      // кабінет пацієнта: власна схема доступу (docs/AUTH_PLAN.md п.5)
    };

    private readonly RequestDelegate _next;

    public CurrentEmployeeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICurrentEmployee current, LisDbContext db, IConfiguration config)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var mode = AuthModes.Resolve(config);
        if (mode == AuthModes.Oidc)
        {
            await InvokeOidcAsync(context, current, db, config, ip);
            return;
        }

        // Auth:Mode=None — заголовок ігнорується, діє Lab:DefaultEmployeeId; Auth:Mode=Header — співробітник із заголовка (довіра до проксі МІС).
        var requested = mode == AuthModes.Header ? context.Request.Headers[HeaderName].FirstOrDefault() : null;
        if (string.IsNullOrWhiteSpace(requested)) requested = config["Lab:DefaultEmployeeId"] ?? "";
        OrgEmployee? employee = null;
        if (!string.IsNullOrWhiteSpace(requested) && context.Request.Path.StartsWithSegments("/api"))
            employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == requested);
        current.Set(employee, requested, ip);
        db.CurrentUserId = current.EmployeeId;
        await _next(context);
    }

    /// <summary>Auth:Mode=Oidc: токен Identity (JWT Bearer / reference) → sub → співробітник МІС → роль ЛІС. Заголовок співробітника не діє.</summary>
    private async Task InvokeOidcAsync(HttpContext context, ICurrentEmployee current, LisDbContext db, IConfiguration config, string? ip)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api") || IsPublic(path, config))
        {
            current.Set(null, "", ip);
            db.CurrentUserId = current.EmployeeId;
            await _next(context);
            return;
        }

        var auth = await context.AuthenticateAsync();
        var sub = auth.Succeeded ? SubjectOf(auth.Principal!, config) : null;
        if (string.IsNullOrWhiteSpace(sub))
        {
            await Problem(context, StatusCodes.Status401Unauthorized, "Не авторизовано", "Користувача не авторизовано або строк дії авторизації минув. Увійдіть знову.");
            return;
        }

        var resolver = context.RequestServices.GetRequiredService<MedLinkUserResolver>();
        var access = resolver.BridgeEnabled
            ? await resolver.ResolveAsync(sub, context.Request.Headers[HeaderName].FirstOrDefault(), context.RequestAborted)
            : await resolver.ResolveLocalAsync(sub, context.RequestAborted);
        if (access.Employee == null)
        {
            var status = access.Failure == LabAccessFailure.MisUnavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status403Forbidden;
            await Problem(context, status, status == 503 ? "Сервіс тимчасово недоступний" : "Немає доступу до Лабораторії", access.Message ?? "Доступ заборонено.");
            return;
        }
        current.Set(access.Employee, access.Employee.Id, ip);
        db.CurrentUserId = current.EmployeeId;
        await _next(context);
    }

    /// <summary>Ідентифікатор користувача з токена: claim з Auth:Oidc:EmployeeIdClaim (лише без моста), інакше sub.</summary>
    internal static string? SubjectOf(System.Security.Claims.ClaimsPrincipal user, IConfiguration config)
    {
        var custom = config["Auth:Oidc:EmployeeIdClaim"];
        if (!string.IsNullOrWhiteSpace(config["Medlink:ConnectionString"])) custom = null; // у режимі моста ключ — завжди sub (cmn_person.user_id)
        return (string.IsNullOrWhiteSpace(custom) ? null : user.FindFirst(custom)?.Value)
               ?? user.FindFirst("sub")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }

    internal static bool IsPublic(PathString path, IConfiguration config)
    {
        var value = path.Value ?? "";
        if (DefaultPublicPrefixes.Any(p => value.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return true;
        return config.GetSection("Auth:PublicPaths").Get<string[]>()?.Any(p => !string.IsNullOrWhiteSpace(p) && value.StartsWith(p, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static Task Problem(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        return context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails { Type = "https://httpstatuses.com/" + status, Title = title, Status = status, Detail = detail, Instance = context.Request.Path });
    }
}
