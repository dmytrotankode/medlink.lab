// Налаштування модуля Guide та визначення поточного користувача (без власної автентифікації — її надає хост).
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedLink.Guide;

/// <summary>Налаштування модуля. Секція конфігурації: <c>Guide</c> (RoutePrefix, Schema, AllowAnonymousEditing, EditorRoles, заголовки).</summary>
public sealed class GuideOptions
{
    /// <summary>Підключення провайдера БД хостом: <c>o => o.UseSqlite(...)</c> або <c>o => o.UseNpgsql(...)</c>. Обов'язково.</summary>
    public Action<DbContextOptionsBuilder>? ConfigureDb { get; set; }

    /// <summary>Префікс маршрутів API.</summary>
    public string RoutePrefix { get; set; } = "/api/v1/guide";

    /// <summary>Схема PostgreSQL (у SQLite ігнорується).</summary>
    public string Schema { get; set; } = "guide";

    /// <summary>true — редагування дозволене будь-кому (режим без автентифікації, пілот); false — лише ролі з <see cref="EditorRoles"/>.</summary>
    public bool AllowAnonymousEditing { get; set; }

    /// <summary>Ролі, яким дозволено конструктор.</summary>
    public List<string> EditorRoles { get; set; } = new() { "admin", "guide_editor" };

    /// <summary>Власне правило «чи редактор» (має пріоритет над AllowAnonymousEditing/EditorRoles).</summary>
    public Func<GuideUser, bool>? IsEditor { get; set; }

    /// <summary>Типовий tenant (organization_id), якщо заголовок не передано.</summary>
    public Func<HttpContext, string?>? DefaultTenantId { get; set; }

    /// <summary>Додаткове визначення ролей користувача (до тих, що в заголовку).</summary>
    public Func<HttpContext, IEnumerable<string>>? ResolveRoles { get; set; }

    public string EmployeeHeader { get; set; } = "X-MedLink-Employee-Id";
    public string RolesHeader { get; set; } = "X-MedLink-Roles";
    public string TenantHeader { get; set; } = "X-MedLink-Tenant-Id";

    /// <summary>Максимальний розмір медіа (байт).</summary>
    public int MaxMediaBytes { get; set; } = 10 * 1024 * 1024;

    internal bool CheckEditor(GuideUser user)
    {
        if (IsEditor != null) return IsEditor(user);
        if (AllowAnonymousEditing) return true;
        return EditorRoles.Any(r => user.Roles.Contains(r));
    }
}

/// <summary>Контекст користувача запиту.</summary>
public sealed class GuideUser
{
    /// <summary>uuid співробітника (якщо у заголовку не GUID — нульовий GUID).</summary>
    public string EmployeeId { get; init; } = GuideConst.EmptyGuid;
    public HashSet<string> Roles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>organization_id evomis; null — немає tenant.</summary>
    public string? TenantId { get; init; }
}

public interface IGuideUserResolver
{
    GuideUser Resolve(HttpContext context);
}

/// <summary>Типова реалізація: заголовки X-MedLink-Employee-Id, X-MedLink-Roles (через кому), X-MedLink-Tenant-Id.</summary>
public sealed class HeaderGuideUserResolver : IGuideUserResolver
{
    private readonly GuideOptions _options;
    public HeaderGuideUserResolver(GuideOptions options) => _options = options;

    public GuideUser Resolve(HttpContext context)
    {
        var h = context.Request.Headers;
        var emp = h[_options.EmployeeHeader].FirstOrDefault();
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roleHeader = h[_options.RolesHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(roleHeader))
            foreach (var r in roleHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) roles.Add(r);
        if (_options.ResolveRoles != null)
            foreach (var r in _options.ResolveRoles(context)) roles.Add(r);
        var tenant = h[_options.TenantHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenant)) tenant = _options.DefaultTenantId?.Invoke(context);
        return new GuideUser
        {
            EmployeeId = Guid.TryParse(emp, out var g) ? g.ToString() : GuideConst.EmptyGuid,
            Roles = roles,
            TenantId = Guid.TryParse(tenant, out var t) ? t.ToString() : null
        };
    }
}
