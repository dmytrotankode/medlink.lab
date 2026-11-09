// Визначення співробітника лабораторії за користувачем Identity (режим Auth:Mode=Oidc).
// Ланцюг: sub токена → cmn_person.user_id (МІС, лише читання) → org_employee особи → профілі безпеки (sys_user_profile) → роль ЛІС за таблицею
// Medlink:ProfileRoles → проєкція працівника у власній схемі ЛІС. Без Medlink:ConnectionString міст вимкнено: співробітник береться з власної бази
// за значенням claim (Auth:Oidc:EmployeeIdClaim, типово sub) як org_employee.id.
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MedLink.LIS.Api.Services.MedLinkBridge;

public enum LabAccessFailure { None, UnknownUser, NoLabRole, MisUnavailable }

public sealed record LabAccess(OrgEmployee? Employee, LabAccessFailure Failure, string? Message)
{
    public static LabAccess Ok(OrgEmployee e) => new(e, LabAccessFailure.None, null);
    public static LabAccess Fail(LabAccessFailure f, string message) => new(null, f, message);
}

/// <summary>Правила відповідності профілю безпеки МІС ролі лабораторії. Усе береться з конфігурації, у коді таблиці немає.</summary>
public static class MedLinkRoleMap
{
    /// <summary>Від найповноважнішої до найменш: якщо профілів кілька, береться найвища роль.</summary>
    private static readonly string[] Precedence = { LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician, LabRoles.Registrar, LabRoles.Phlebotomist, LabRoles.Courier };

    public static string? RoleFor(IEnumerable<string?> profileCaptions, MedLinkOptions options)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in options.ProfileRoles)
            if (!string.IsNullOrWhiteSpace(key) && IsLabRole(value)) map[key.Trim()] = value.Trim();
        var found = profileCaptions.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => map.TryGetValue(c!.Trim(), out var r) ? r : null).Where(r => r != null).Select(r => r!).ToList();
        if (found.Count > 0) return found.OrderBy(r => Array.IndexOf(Precedence, r)).First();
        return IsLabRole(options.DefaultLabRole) ? options.DefaultLabRole!.Trim() : null;
    }

    public static bool IsLabRole(string? role) => role != null && LabRoles.All.Contains(role.Trim());
}

public sealed class MedLinkUserResolver
{
    private sealed record Candidate(MedLinkEmployee Employee, string? Role);
    private sealed record Known(MedLinkPerson? Person, List<Candidate> Candidates);

    private readonly LisDbContext _db;
    private readonly IMedLinkDirectory _directory;
    private readonly IMisRecords _records;
    private readonly MedLinkOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MedLinkUserResolver> _logger;

    public MedLinkUserResolver(LisDbContext db, IMedLinkDirectory directory, IMisRecords records, IOptions<MedLinkOptions> options, IMemoryCache cache, ILogger<MedLinkUserResolver> logger)
    {
        _db = db; _directory = directory; _records = records; _options = options.Value; _cache = cache; _logger = logger;
    }

    public bool BridgeEnabled => _directory.Enabled;

    /// <summary>Без моста: співробітник за ідентифікатором із claim (як у режимі заголовка, але з перевіреного токена).</summary>
    public async Task<LabAccess> ResolveLocalAsync(string employeeId, CancellationToken ct = default)
    {
        var e = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e == null || !e.IsLabActive || string.IsNullOrEmpty(e.LabRole))
            return LabAccess.Fail(LabAccessFailure.UnknownUser, "Вас не знайдено серед співробітників лабораторії. Зверніться до адміністратора.");
        return LabAccess.Ok(e);
    }

    /// <param name="identityUserId">sub токена Identity (cmn_person.user_id).</param>
    /// <param name="preferredEmployeeId">Бажаний співробітник, якщо в особи кілька посад (заголовок X-MedLink-Employee-Id); приймається лише зі списку власних посад.</param>
    public async Task<LabAccess> ResolveAsync(string identityUserId, string? preferredEmployeeId, CancellationToken ct = default)
    {
        Known known;
        try
        {
            var seconds = Math.Max(0, _options.CacheSeconds);
            if (seconds > 0 && _cache.TryGetValue<Known>("medlink:user:" + identityUserId, out var hit) && hit != null) known = hit;
            else
            {
                known = await LoadAsync(identityUserId, ct);
                if (seconds > 0) _cache.Set("medlink:user:" + identityUserId, known, TimeSpan.FromSeconds(seconds));
            }
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "МІС недоступний, користувача {Sub} не визначено", identityUserId);
            return LabAccess.Fail(LabAccessFailure.MisUnavailable, "МІС тимчасово недоступна, спробуйте за хвилину.");
        }

        if (known.Person == null)
            return LabAccess.Fail(LabAccessFailure.UnknownUser, "Вашого облікового запису не знайдено в МІС. Зверніться до адміністратора.");
        var allowed = known.Candidates.Where(c => c.Role != null).ToList();
        if (allowed.Count == 0)
            return LabAccess.Fail(LabAccessFailure.NoLabRole, "Для вашого профілю в МІС немає доступу до Лабораторії. Зверніться до адміністратора, щоб призначити профіль.");
        var pick = (preferredEmployeeId == null ? null : allowed.FirstOrDefault(c => c.Employee.Id.Equals(preferredEmployeeId, StringComparison.OrdinalIgnoreCase)))
                   ?? allowed.OrderBy(c => Array.IndexOf(LabRoles.All, c.Role)).First();

        var existing = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == pick.Employee.Id, ct);
        if (existing != null && existing.LabRole == pick.Role && existing.IsLabActive && existing.DepartmentId == pick.Employee.DepartmentId)
            return LabAccess.Ok(existing);
        return LabAccess.Ok(await _records.ProjectEmployeeAsync(known.Person, pick.Employee, pick.Role!, ct));
    }

    private async Task<Known> LoadAsync(string identityUserId, CancellationToken ct)
    {
        var person = await _directory.PersonByIdentityUserAsync(identityUserId, ct);
        if (person == null) return new Known(null, new());
        var employees = (await _directory.EmployeesOfPersonAsync(person.Id, ct))
            .Where(e => string.IsNullOrWhiteSpace(_options.OrganizationId) || e.OrganizationId.Equals(_options.OrganizationId.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var profiles = await _directory.ProfilesOfEmployeesAsync(employees.Select(e => e.Id).ToList(), ct);
        return new Known(person, employees.Select(e => new Candidate(e, MedLinkRoleMap.RoleFor(profiles.Where(p => p.UserId.Equals(e.Id, StringComparison.OrdinalIgnoreCase)).Select(p => p.Caption), _options))).ToList());
    }
}
