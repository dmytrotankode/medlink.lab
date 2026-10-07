// Рольова політика (без автентифікації): вирішує, чи може поточний співробітник виконати дію.
// Повертає 403 ProblemDetails з українським текстом.
using MedLink.LIS.Api.Infrastructure;

namespace MedLink.LIS.Api.Domain;

public interface IRolePolicy
{
    string? CurrentRole { get; }
    bool Can(params string[] roles);
    /// <summary>Кидає ForbiddenException, якщо роль поточного співробітника не входить до переліку.</summary>
    void Require(string actionLabel, params string[] roles);
    /// <summary>Перевірка дії машини станів для сутності (409 статус / 403 роль).</summary>
    ActionRule Ensure(string entity, string action, string currentStatus, string? objectLabel = null);
    List<AllowedActionDto> AllowedActions(string entity, string status);
}

public sealed class RolePolicy : IRolePolicy
{
    private readonly ICurrentEmployee _current;
    public RolePolicy(ICurrentEmployee current) => _current = current;

    public string? CurrentRole => _current.IsResolved ? _current.LabRole : null;

    public bool Can(params string[] roles)
    {
        var role = CurrentRole;
        if (role == null) return false;
        if (role == LabRoles.Admin) return true;
        return roles.Length == 0 || roles.Contains(role);
    }

    public void Require(string actionLabel, params string[] roles)
    {
        if (!_current.IsResolved)
            throw new ForbiddenException($"Співробітника '{_current.EmployeeId}' не знайдено. Передайте коректний заголовок {CurrentEmployeeMiddleware.HeaderName}");
        if (!Can(roles))
            throw new ForbiddenException($"Дія «{actionLabel}» недоступна для ролі {CurrentRole}. Потрібна роль: {string.Join(" / ", roles)}");
    }

    public ActionRule Ensure(string entity, string action, string currentStatus, string? objectLabel = null)
    {
        if (!_current.IsResolved)
            throw new ForbiddenException($"Співробітника '{_current.EmployeeId}' не знайдено. Передайте коректний заголовок {CurrentEmployeeMiddleware.HeaderName}");
        return LisStateMachine.Ensure(entity, action, currentStatus, CurrentRole, objectLabel);
    }

    public List<AllowedActionDto> AllowedActions(string entity, string status) => LisStateMachine.AllowedActions(entity, status, CurrentRole);
}

/// <summary>Політика для технічного актора (коннектор) — усі дії дозволені.</summary>
public sealed class SystemRolePolicy : IRolePolicy
{
    public string? CurrentRole => LabRoles.System;
    public bool Can(params string[] roles) => true;
    public void Require(string actionLabel, params string[] roles) { }
    public ActionRule Ensure(string entity, string action, string currentStatus, string? objectLabel = null) =>
        LisStateMachine.Ensure(entity, action, currentStatus, LabRoles.System, objectLabel);
    public List<AllowedActionDto> AllowedActions(string entity, string status) => LisStateMachine.AllowedActions(entity, status, LabRoles.System);
}
