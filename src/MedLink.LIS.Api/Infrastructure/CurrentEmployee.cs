// Поточний співробітник: заголовок X-MedLink-Employee-Id або Lab:DefaultEmployeeId (без автентифікації)
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
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
    private readonly RequestDelegate _next;

    public CurrentEmployeeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICurrentEmployee current, LisDbContext db, IConfiguration config)
    {
        var requested = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requested)) requested = config["Lab:DefaultEmployeeId"] ?? "";
        OrgEmployee? employee = null;
        if (!string.IsNullOrWhiteSpace(requested) && context.Request.Path.StartsWithSegments("/api"))
            employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == requested);
        current.Set(employee, requested, context.Connection.RemoteIpAddress?.ToString());
        db.CurrentUserId = current.EmployeeId;
        await _next(context);
    }
}
