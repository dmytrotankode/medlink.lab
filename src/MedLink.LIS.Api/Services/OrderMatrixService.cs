// Матриця замовлення для лікарів/медсестер: тести та профілі за секціями й категоріями (для чекбокс-сітки) + обрані набори
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class FavoriteSetRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = "";
    public List<string> ProfileIds { get; set; } = new();
    public List<string> TestIds { get; set; } = new();
    public int DisplayOrder { get; set; }
}

public sealed class OrderMatrixService
{
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;

    public OrderMatrixService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current) { _db = db; _policy = policy; _audit = audit; _current = current; }

    public async Task<object> MatrixAsync(bool includeInactive)
    {
        var tests = await _db.Tests.AsNoTracking().Include(t => t.BiomaterialType).Include(t => t.TubeType).Include(t => t.LabSection).ThenInclude(s => s!.Department)
            .Where(t => includeInactive || t.IsActive).OrderBy(t => t.Category).ThenBy(t => t.Code).ToListAsync();
        var profiles = await _db.Profiles.AsNoTracking().Include(p => p.Items).ThenInclude(i => i.Test).Include(p => p.OrganizationService).Include(p => p.EhealthService).Where(p => includeInactive || p.IsActive).OrderBy(p => p.Name).ToListAsync();
        var sections = await _db.Sections.AsNoTracking().Include(s => s.Department).Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        var bm = await _db.BiomaterialTypes.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name);
        var tubes = await _db.TubeTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t);

        object Col(LabTestDefinition t) => new
        {
            id = t.Id, code = t.Code, name = t.Name, shortName = t.ShortName, isProfile = false, category = t.Category, labSectionId = t.LabSectionId, labSectionName = t.LabSection?.Name,
            biomaterial = t.BiomaterialType?.Name, tubeColor = t.TubeType?.ColorCode, tubeName = t.TubeType?.Name, price = t.Price, tatHours = (int?)null, unit = t.Unit, resultType = t.ResultType,
            memberTestCodes = Array.Empty<string>(), isActive = t.IsActive
        };
        object ProfileCol(LabTestProfile p)
        {
            var firstTest = p.Items.Select(i => i.Test).FirstOrDefault(t => t != null);
            var section = firstTest?.LabSectionId == null ? null : sections.FirstOrDefault(s => s.Id == firstTest.LabSectionId);
            return new
            {
                id = p.Id, code = p.Code, name = p.Name, shortName = (string?)null, isProfile = true, category = p.Category, labSectionId = section?.Id, labSectionName = section?.Name,
                biomaterial = p.DefaultBiomaterialTypeId.HasValue && bm.TryGetValue(p.DefaultBiomaterialTypeId.Value, out var b) ? b : null,
                tubeColor = p.DefaultTubeTypeId.HasValue && tubes.TryGetValue(p.DefaultTubeTypeId.Value, out var tb) ? tb.ColorCode : null, tubeName = p.DefaultTubeTypeId.HasValue && tubes.TryGetValue(p.DefaultTubeTypeId.Value, out var tn) ? tn.Name : null,
                price = p.OrganizationService?.Price ?? p.Price, tatHours = (int?)p.TurnaroundHours, unit = (string?)null, resultType = "PROFILE",
                memberTestCodes = p.Items.OrderBy(i => i.DisplayOrder).Select(i => i.Test?.Code ?? "").Where(c => c != "").ToArray(), isActive = p.IsActive,
                organizationServiceId = p.OrganizationServiceId, ehealthServiceCode = p.EhealthService?.Code, fastingRequired = p.FastingRequired
            };
        }

        var groups = sections.Select(s => new
        {
            sectionId = s.Id, sectionCode = s.Code, sectionName = s.Name, sectionType = s.SectionType, departmentId = s.DepartmentId, departmentName = s.Department?.Caption,
            categories = tests.Where(t => t.LabSectionId == s.Id).GroupBy(t => t.Category).Select(g => new
            {
                category = g.Key,
                profiles = profiles.Where(p => p.Items.Any(i => i.Test != null && i.Test.LabSectionId == s.Id && i.Test.Category == g.Key) && p.Items.All(i => i.Test == null || i.Test.LabSectionId == s.Id)).Select(ProfileCol),
                tests = g.Select(Col)
            })
        }).ToList();
        var unassigned = tests.Where(t => t.LabSectionId == null || sections.All(s => s.Id != t.LabSectionId)).ToList();
        var mixedProfiles = profiles.Where(p => p.Items.Select(i => i.Test?.LabSectionId).Distinct().Count() > 1).Select(ProfileCol).ToList();
        return new
        {
            generatedAt = DateTime.UtcNow, sections = groups,
            other = new { tests = unassigned.Select(Col), profiles = mixedProfiles },
            allProfiles = profiles.Select(ProfileCol), totalTests = tests.Count, totalProfiles = profiles.Count
        };
    }

    public async Task<List<object>> FavoritesAsync(string? employeeId)
    {
        var emp = string.IsNullOrWhiteSpace(employeeId) ? _current.EmployeeId : employeeId;
        var items = await _db.OrderFavorites.AsNoTracking().Where(f => f.EmployeeId == emp && f.RecordState != RecordStates.Deleted).OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name).ToListAsync();
        return items.Select(f => (object)new { f.Id, f.EmployeeId, f.Name, profileIds = f.ProfileIds, testIds = f.TestIds, f.DisplayOrder }).ToList();
    }

    /// <summary>Повна заміна набору обраних для співробітника (PUT).</summary>
    public async Task<List<object>> SaveFavoritesAsync(string? employeeId, List<FavoriteSetRequest> sets)
    {
        var emp = string.IsNullOrWhiteSpace(employeeId) ? _current.EmployeeId : employeeId;
        if (emp != _current.EmployeeId) _policy.Require("Редагування обраних іншого співробітника", LabRoles.Admin);
        else _policy.Require("Збереження обраних наборів", LabRoles.All);
        var existing = await _db.OrderFavorites.Where(f => f.EmployeeId == emp).ToListAsync();
        var keep = new HashSet<string>();
        var order = 0;
        foreach (var s in sets)
        {
            if (string.IsNullOrWhiteSpace(s.Name)) throw ValidationException.Field("name", "Назва набору обов'язкова");
            foreach (var pid in s.ProfileIds) if (!await _db.Profiles.AnyAsync(p => p.Id == pid || p.Code == pid)) throw new ValidationException($"Профіль '{pid}' не знайдено");
            foreach (var tid in s.TestIds) if (!await _db.Tests.AnyAsync(t => t.Id == tid || t.Code == tid)) throw new ValidationException($"Тест '{tid}' не знайдено");
            var f = s.Id != null ? existing.FirstOrDefault(x => x.Id == s.Id) : null;
            if (f == null) { f = new LabOrderFavorite { EmployeeId = emp }; _db.OrderFavorites.Add(f); }
            f.Name = s.Name.Trim(); f.ProfileIds = s.ProfileIds; f.TestIds = s.TestIds; f.DisplayOrder = s.DisplayOrder != 0 ? s.DisplayOrder : ++order; f.RecordState = RecordStates.Active;
            keep.Add(f.Id);
        }
        foreach (var f in existing.Where(x => !keep.Contains(x.Id))) _db.OrderFavorites.Remove(f);
        _audit.Log("SAVE_FAVORITES", "lab_order_favorite", emp, null, new { count = sets.Count });
        await _db.SaveChangesAsync();
        return await FavoritesAsync(emp);
    }

    public async Task DeleteFavoriteAsync(string id)
    {
        var f = await _db.OrderFavorites.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Обраний набір", id);
        if (f.EmployeeId != _current.EmployeeId) _policy.Require("Видалення обраних іншого співробітника", LabRoles.Admin);
        _db.OrderFavorites.Remove(f);
        _audit.Log("DELETE", "lab_order_favorite", id, new { f.Name }, null);
        await _db.SaveChangesAsync();
    }
}
