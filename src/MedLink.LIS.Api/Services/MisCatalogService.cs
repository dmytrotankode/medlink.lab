// ТЗ 21.3–21.5, 23.1–23.3: словник інтервенцій ЕСОЗ, співставлення з послугами ЛІС, готовність, послуги обладнання,
// довідник «Послуга ЕСОЗ × аналізатори» (матриця, картка послуги), каталог для МІС з версією та пакети послуг.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

/// <summary>Показник послуги ЕСОЗ: тест ЛІС і ознаки вибору.</summary>
public sealed record ServiceIndicator(LabTestDefinition Test, bool IsRequired, bool IsDefault, int DisplayOrder);

public sealed class InterventionMapRequest
{
    public string InterventionId { get; set; } = "";
    public string? ProfileId { get; set; }
    public List<string> TestIds { get; set; } = new();
    public string IndicatorSelectionMode { get; set; } = SelectionModes.Selectable;
    public int MinSelected { get; set; } = 1;
    public bool IsPathology { get; set; }
    public string? Note { get; set; }
}

public sealed class AnalyzerServiceItem
{
    public string? AnalyzerId { get; set; }
    public string? ProfileId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 1;
    public int? AvgTatMinutes { get; set; }
    public string? Note { get; set; }
}

public sealed class AnalyzerParamToggle
{
    public string AnalyzerId { get; set; } = "";
    public string TestCode { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public string? AnalyzerCode { get; set; }
    public int? Priority { get; set; }
}

public sealed class ServiceCardUpdateRequest
{
    public List<AnalyzerServiceItem> Analyzers { get; set; } = new();
    /// <summary>Ознаки показників послуги: testCode → обов'язковий/типовий.</summary>
    public List<IndicatorFlags> Indicators { get; set; } = new();
    public List<AnalyzerParamToggle> Params { get; set; } = new();
    public string? Mode { get; set; }
    /// <summary>true — лише порахувати зачеплені тести (FR-EQP-007), нічого не зберігати.</summary>
    public bool DryRun { get; set; }
}

public sealed class IndicatorFlags
{
    public string TestCode { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool IsDefault { get; set; } = true;
}

public sealed class MisCatalogService
{
    public static readonly string[] EditorRoles = { LabRoles.Admin, LabRoles.Doctor };
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly RoutingService _routing;

    public MisCatalogService(LisDbContext db, IRolePolicy policy, IAuditService audit, RoutingService routing) { _db = db; _policy = policy; _audit = audit; _routing = routing; }

    // ------------------------------------------------------------------ показники послуги
    /// <summary>Показники співставлення: профіль (його склад з ознаками) або набір показників.</summary>
    public async Task<List<ServiceIndicator>> IndicatorsAsync(LabInterventionMap map)
    {
        if (map.ProfileId != null)
        {
            var items = await _db.ProfileItems.AsNoTracking().Include(i => i.Test).Where(i => i.ProfileId == map.ProfileId && i.Test != null && i.Test.IsActive).OrderBy(i => i.DisplayOrder).ToListAsync();
            var fixedMode = map.IndicatorSelectionMode == SelectionModes.Fixed;
            return items.Select(i => new ServiceIndicator(i.Test!, fixedMode || (i.IsRequired && map.IndicatorSelectionMode == SelectionModes.Selectable), fixedMode || i.IsDefaultSelected, i.DisplayOrder)).ToList();
        }
        var tests = await _db.InterventionMapTests.AsNoTracking().Include(t => t.Test).Where(t => t.MapId == map.Id && t.Test != null).OrderBy(t => t.DisplayOrder).ToListAsync();
        return tests.Select(t => new ServiceIndicator(t.Test!, t.IsRequired || map.IndicatorSelectionMode == SelectionModes.Fixed, t.IsDefault, t.DisplayOrder)).ToList();
    }

    /// <summary>Готовність (FR-MIS-014): жодного показника на приладах — NO_EQUIPMENT, частина — PARTIAL, усі — READY.</summary>
    public static string ComputeReadiness(LabInterventionMap? map, IReadOnlyList<ServiceIndicator> indicators, RoutingService.Snapshot s)
    {
        if (map == null || !map.IsActive) return Readiness.Unmapped;
        if (map.IsPathology) return Readiness.Ready;
        if (indicators.Count == 0) return Readiness.Unmapped;
        var covered = indicators.Count(i => RoutingService.Candidates(s, i.Test.Code, map.ProfileId).Count > 0);
        return covered == 0 ? Readiness.NoEquipment : covered == indicators.Count ? Readiness.Ready : Readiness.Partial;
    }

    public async Task RecomputeReadinessAsync()
    {
        var s = await _routing.SnapshotAsync();
        foreach (var map in await _db.InterventionMaps.ToListAsync())
            map.Readiness = ComputeReadiness(map, await IndicatorsAsync(map), s);
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ інтервенції та співставлення
    public async Task<object> InterventionsAsync(string? search, string? status)
    {
        var q = _db.Interventions.AsNoTracking().Where(i => i.RecordState != RecordStates.Deleted);
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(i => i.Code.Contains(s) || i.Name.Contains(s)); }
        var list = await q.OrderBy(i => i.Code).ToListAsync();
        var maps = await _db.InterventionMaps.AsNoTracking().Include(m => m.Profile).Where(m => m.IsActive).ToListAsync();
        var rows = list.Select(i =>
        {
            var m = maps.FirstOrDefault(x => x.InterventionId == i.Id);
            var readiness = m?.Readiness ?? Readiness.Unmapped;
            return new
            {
                i.Id, i.Code, i.Name, i.CodeSystem, i.Category, i.ServiceGroup, i.IsActive, i.SourceVersion, i.SyncedOn,
                mapId = m?.Id, profileId = m?.ProfileId, profileCode = m?.Profile?.Code, profileName = m?.Profile?.Name, mode = m?.IndicatorSelectionMode,
                isPathology = m?.IsPathology ?? false, readiness, readinessLabel = Readiness.Label(readiness)
            };
        }).Where(r => string.IsNullOrWhiteSpace(status) || r.readiness == status.ToUpperInvariant()).ToList();
        return new
        {
            total = rows.Count, items = rows,
            summary = rows.GroupBy(r => r.readiness).ToDictionary(g => g.Key, g => g.Count())
        };
    }

    /// <summary>Синхронізація словника (FR-MIS-002). Демо: оновлення позначки синхронізації та перерахунок готовності.</summary>
    public async Task<object> SyncAsync()
    {
        _policy.Require("Оновлення словника інтервенцій", LabRoles.Admin);
        var now = DateTime.UtcNow;
        var list = await _db.Interventions.ToListAsync();
        foreach (var i in list) i.SyncedOn = now;
        await _db.SaveChangesAsync();
        await RecomputeReadinessAsync();
        _audit.Log("INTERVENTIONS_SYNC", "lab_ehealth_intervention", null, null, new { count = list.Count });
        await _db.SaveChangesAsync();
        return new { synced = list.Count, at = now, source = "MOCK" };
    }

    public async Task<object> MapsAsync() => (await _db.InterventionMaps.AsNoTracking().Include(m => m.Intervention).Include(m => m.Profile).Include(m => m.Tests).ThenInclude(t => t.Test)
        .Where(m => m.RecordState != RecordStates.Deleted).ToListAsync()).OrderBy(m => m.Intervention?.Code).Select(MapDto);

    public async Task<object> MapAsync(string id) => MapDto(await LoadMapAsync(id));

    private async Task<LabInterventionMap> LoadMapAsync(string id) =>
        await _db.InterventionMaps.Include(m => m.Intervention).Include(m => m.Profile).Include(m => m.Tests).ThenInclude(t => t.Test).FirstOrDefaultAsync(m => m.Id == id)
        ?? throw NotFoundException.For("Співставлення", id);

    private static object MapDto(LabInterventionMap m) => new
    {
        m.Id, m.InterventionId, interventionCode = m.Intervention?.Code, interventionName = m.Intervention?.Name, m.ProfileId, profileCode = m.Profile?.Code, profileName = m.Profile?.Name,
        m.IndicatorSelectionMode, modeLabel = SelectionModes.Label(m.IndicatorSelectionMode), m.MinSelected, m.Readiness, readinessLabel = Readiness.Label(m.Readiness), m.IsPathology, m.IsActive, m.Note,
        tests = m.Tests.OrderBy(t => t.DisplayOrder).Select(t => new { t.TestId, code = t.Test?.Code, name = t.Test?.Name, t.IsDefault, t.IsRequired })
    };

    public async Task<object> SaveMapAsync(string? id, InterventionMapRequest req)
    {
        _policy.Require("Співставлення інтервенцій", EditorRoles);
        if (!SelectionModes.All.Contains(req.IndicatorSelectionMode)) throw ValidationException.Field("indicatorSelectionMode", $"Режим: {string.Join(", ", SelectionModes.All)}");
        var iv = await _db.Interventions.FirstOrDefaultAsync(i => i.Id == req.InterventionId || i.Code == req.InterventionId) ?? throw NotFoundException.For("Інтервенція", req.InterventionId);
        string? profileId = null;
        if (!string.IsNullOrWhiteSpace(req.ProfileId))
            profileId = (await _db.Profiles.FirstOrDefaultAsync(p => p.Id == req.ProfileId || p.Code == req.ProfileId) ?? throw NotFoundException.For("Послуга ЛІС", req.ProfileId)).Id;
        if (profileId == null && req.TestIds.Count == 0 && !req.IsPathology) throw ValidationException.Field("profileId", "Оберіть послугу ЛІС або набір показників");
        LabInterventionMap map;
        if (id == null)
        {
            if (await _db.InterventionMaps.AnyAsync(m => m.InterventionId == iv.Id && m.IsActive)) throw new ConflictException($"Інтервенція {iv.Code} вже співставлена");
            map = new LabInterventionMap { InterventionId = iv.Id };
            _db.InterventionMaps.Add(map);
        }
        else map = await LoadMapAsync(id);
        var before = new { map.ProfileId, map.IndicatorSelectionMode, tests = map.Tests.Select(t => t.TestId).ToList() };
        map.InterventionId = iv.Id; map.ProfileId = profileId; map.IndicatorSelectionMode = req.IndicatorSelectionMode; map.MinSelected = Math.Max(1, req.MinSelected);
        map.IsPathology = req.IsPathology; map.Note = req.Note; map.IsActive = true;
        _db.InterventionMapTests.RemoveRange(map.Tests);
        map.Tests.Clear();
        var n = 0;
        foreach (var tid in req.TestIds.Distinct())
        {
            var t = await _db.Tests.FirstOrDefaultAsync(x => x.Id == tid || x.Code == tid) ?? throw NotFoundException.For("Показник", tid);
            map.Tests.Add(new LabInterventionMapTest { MapId = map.Id, TestId = t.Id, IsDefault = true, IsRequired = req.IndicatorSelectionMode == SelectionModes.Fixed, DisplayOrder = ++n });
        }
        map.Readiness = ComputeReadiness(map, await IndicatorsForAsync(map), await _routing.SnapshotAsync());
        _audit.Log(id == null ? "CREATE" : "UPDATE", "lab_intervention_map", map.Id, before, req);
        await _db.SaveChangesAsync();
        return await MapAsync(map.Id);
    }

    /// <summary>Показники незбереженого співставлення (до SaveChanges навігація Tests уже в пам'яті).</summary>
    private async Task<List<ServiceIndicator>> IndicatorsForAsync(LabInterventionMap map)
    {
        if (map.ProfileId != null || map.Tests.Count == 0) return await IndicatorsAsync(map);
        var ids = map.Tests.Select(t => t.TestId).ToList();
        var tests = await _db.Tests.AsNoTracking().Where(t => ids.Contains(t.Id)).ToListAsync();
        return map.Tests.Select(t => new ServiceIndicator(tests.First(x => x.Id == t.TestId), t.IsRequired, t.IsDefault, t.DisplayOrder)).ToList();
    }

    public async Task DeleteMapAsync(string id)
    {
        _policy.Require("Співставлення інтервенцій", EditorRoles);
        var map = await LoadMapAsync(id);
        _db.InterventionMaps.Remove(map);
        _audit.Log("DELETE", "lab_intervention_map", id, new { map.InterventionId, map.ProfileId }, null);
        await _db.SaveChangesAsync();
    }

    /// <summary>Підказка послуги ЛІС за збігом слів у назві (FR-MIS-013).</summary>
    public async Task<object> SuggestAsync(string interventionId)
    {
        var iv = await _db.Interventions.AsNoTracking().FirstOrDefaultAsync(i => i.Id == interventionId || i.Code == interventionId) ?? throw NotFoundException.For("Інтервенція", interventionId);
        static HashSet<string> Words(string s) => s.ToLowerInvariant().Split(new[] { ' ', '(', ')', ',', ';', '.', '-', '/' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).Select(w => w.Length > 6 ? w[..6] : w).ToHashSet();
        var target = Words(iv.Name);
        var profiles = await _db.Profiles.AsNoTracking().Where(p => p.IsActive).ToListAsync();
        return profiles.Select(p => new { p.Id, p.Code, p.Name, score = Words(p.Name).Intersect(target).Count() }).Where(x => x.score > 0).OrderByDescending(x => x.score).Take(5);
    }

    /// <summary>Звіт покриття (FR-MIS-016): кількість інтервенцій за готовністю і перелік проблемних.</summary>
    public async Task<object> CoverageAsync()
    {
        var ivs = await _db.Interventions.AsNoTracking().ToListAsync();
        var maps = await _db.InterventionMaps.AsNoTracking().Where(m => m.IsActive).ToListAsync();
        var rows = ivs.Select(i => new { i.Code, i.Name, readiness = maps.FirstOrDefault(m => m.InterventionId == i.Id)?.Readiness ?? Readiness.Unmapped }).ToList();
        return new
        {
            total = rows.Count,
            byReadiness = rows.GroupBy(r => r.readiness).ToDictionary(g => g.Key, g => g.Count()),
            problems = rows.Where(r => r.readiness != Readiness.Ready).OrderBy(r => r.readiness).ThenBy(r => r.Code)
        };
    }

    // ------------------------------------------------------------------ послуги обладнання (картка приладу)
    public async Task<object> AnalyzerServicesAsync(string analyzerId)
    {
        var a = await _db.Analyzers.AsNoTracking().Include(x => x.ParameterMap).FirstOrDefaultAsync(x => x.Id == analyzerId) ?? throw NotFoundException.For("Аналізатор", analyzerId);
        var services = await _db.AnalyzerServices.AsNoTracking().Include(s => s.Profile).ThenInclude(p => p!.Items).ThenInclude(i => i.Test).Where(s => s.AnalyzerId == analyzerId).ToListAsync();
        var queue = await _db.OrderTests.AsNoTracking().Where(t => t.AssignedAnalyzerId == analyzerId && RoutingService.QueueStatuses.Contains(t.Status) && t.Result == null).GroupBy(t => t.ProfileId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        var map = a.ParameterMap.ToDictionary(m => m.TestCode, m => m, StringComparer.OrdinalIgnoreCase);
        return new
        {
            analyzerId, analyzer = a.Name, a.Kind, a.ServiceStatus, a.Location, a.AvgTatMinutes,
            services = services.OrderBy(s => s.Priority).ThenBy(s => s.Profile?.Name).Select(s => new
            {
                s.Id, s.ProfileId, profileCode = s.Profile?.Code, profileName = s.Profile?.Name, s.IsEnabled, s.Priority, s.AvgTatMinutes, s.Note,
                queue = queue.FirstOrDefault(q => q.Key == s.ProfileId)?.N ?? 0,
                indicators = s.Profile!.Items.Where(i => i.Test != null).OrderBy(i => i.DisplayOrder).Select(i => new
                {
                    i.Test!.Code, i.Test.Name, analyzerCode = map.GetValueOrDefault(i.Test.Code)?.AnalyzerCode, performs = map.ContainsKey(i.Test.Code) && map[i.Test.Code].IsEnabled,
                    priority = map.GetValueOrDefault(i.Test.Code)?.Priority, requiresDilution = map.GetValueOrDefault(i.Test.Code)?.RequiresDilution ?? false
                })
            }),
            separateIndicators = a.ParameterMap.Where(m => !services.Any(s => s.Profile!.Items.Any(i => i.Test?.Code == m.TestCode))).Select(m => new { m.TestCode, m.AnalyzerCode, m.IsEnabled, m.Priority })
        };
    }

    /// <summary>Збереження переліку послуг приладу (FR-EQP-001/002, FR-EQP-007). Вимкнення — переназначення черги; dryRun — лише кількість зачеплених тестів.</summary>
    public async Task<object> SaveAnalyzerServicesAsync(string analyzerId, List<AnalyzerServiceItem> items, bool dryRun)
    {
        _policy.Require("Послуги обладнання", EditorRoles);
        var a = await _db.Analyzers.FirstOrDefaultAsync(x => x.Id == analyzerId) ?? throw NotFoundException.For("Аналізатор", analyzerId);
        var existing = await _db.AnalyzerServices.Where(s => s.AnalyzerId == analyzerId).ToListAsync();
        var disabled = new List<string>();
        foreach (var it in items)
        {
            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == it.ProfileId || p.Code == it.ProfileId) ?? throw NotFoundException.For("Послуга ЛІС", it.ProfileId);
            var svc = existing.FirstOrDefault(s => s.ProfileId == profile.Id);
            if (svc != null && svc.IsEnabled && !it.IsEnabled) disabled.Add(profile.Id);
            if (dryRun) continue;
            if (svc == null) { svc = new LabAnalyzerService { AnalyzerId = analyzerId, ProfileId = profile.Id }; _db.AnalyzerServices.Add(svc); existing.Add(svc); }
            svc.IsEnabled = it.IsEnabled; svc.Priority = Math.Max(1, it.Priority); svc.AvgTatMinutes = it.AvgTatMinutes ?? a.AvgTatMinutes; svc.Note = it.Note;
        }
        var affected = 0;
        foreach (var pid in disabled) affected += (await _routing.AffectedAsync(analyzerId, null, pid)).Count;
        if (dryRun) return new { dryRun = true, affected };
        await _db.SaveChangesAsync();
        var rerouted = new List<object>();
        foreach (var pid in disabled) rerouted.Add(await _routing.RerouteFromAsync(analyzerId, null, "Послугу вимкнено на приладі", pid, unassignIfStuck: true));
        _audit.Log("ANALYZER_SERVICES", "lab_analyzer_service", analyzerId, null, items);
        await _db.SaveChangesAsync();
        await RecomputeReadinessAsync();
        return new { affected, rerouted, services = await AnalyzerServicesAsync(analyzerId) };
    }

    /// <summary>«Заповнити з профілю приладу» (FR-EQP-004): послуги, показники яких є в parameterMap приладу.</summary>
    public async Task<object> PrefillAsync(string analyzerId)
    {
        var a = await _db.Analyzers.AsNoTracking().Include(x => x.ParameterMap).FirstOrDefaultAsync(x => x.Id == analyzerId) ?? throw NotFoundException.For("Аналізатор", analyzerId);
        var codes = a.ParameterMap.Select(m => m.TestCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var have = await _db.AnalyzerServices.AsNoTracking().Where(s => s.AnalyzerId == analyzerId).Select(s => s.ProfileId).ToListAsync();
        var profiles = await _db.Profiles.AsNoTracking().Include(p => p.Items).ThenInclude(i => i.Test).Where(p => p.IsActive).ToListAsync();
        return profiles.Select(p => new { p.Id, p.Code, p.Name, total = p.Items.Count, covered = p.Items.Count(i => i.Test != null && codes.Contains(i.Test.Code)), already = have.Contains(p.Id) })
            .Where(x => x.covered > 0 && !x.already).OrderByDescending(x => (double)x.covered / Math.Max(1, x.total));
    }

    // ------------------------------------------------------------------ довідник «Послуга ЕСОЗ × аналізатори» (23.1)
    public async Task<object> MatrixAsync()
    {
        var s = await _routing.SnapshotAsync();
        var analyzers = s.Analyzers.Values.OrderBy(a => a.Kind == AnalyzerKinds.Manual).ThenBy(a => a.Location).ThenBy(a => a.Name).ToList();
        var maps = await _db.InterventionMaps.AsNoTracking().Include(m => m.Intervention).Include(m => m.Profile).Where(m => m.IsActive && !m.IsPathology).ToListAsync();
        var rows = new List<object>();
        foreach (var m in maps.OrderBy(m => m.Intervention?.Code))
        {
            var ind = await IndicatorsAsync(m);
            var cells = new Dictionary<string, object>();
            foreach (var a in analyzers)
            {
                var covered = ind.Count(i => s.ParamsByTest[i.Test.Code].Any(p => p.AnalyzerId == a.Id));
                if (covered == 0) continue;
                var svc = m.ProfileId == null ? null : s.Services.GetValueOrDefault((a.Id, m.ProfileId));
                cells[a.Id] = new
                {
                    priority = svc?.Priority, enabled = svc?.IsEnabled ?? true, separate = svc == null, partial = covered < ind.Count, covered, total = ind.Count,
                    unavailable = ind.Where(i => s.IsLocked(a.Id, i.Test.Code)).Select(i => i.Test.Code).ToList()
                };
            }
            var readiness = ComputeReadiness(m, ind, s);
            rows.Add(new
            {
                mapId = m.Id, serviceCode = m.Intervention?.Code, serviceName = m.Intervention?.Name, m.ProfileId, profileCode = m.Profile?.Code, profileName = m.Profile?.Name,
                mode = m.IndicatorSelectionMode, modeLabel = SelectionModes.Label(m.IndicatorSelectionMode), indicators = ind.Count, readiness, readinessLabel = Readiness.Label(readiness), cells
            });
        }
        return new
        {
            analyzers = analyzers.Select(a => new
            {
                a.Id, a.Code, a.Name, a.Kind, a.Location, a.AvgTatMinutes, a.IsOnline, a.ServiceStatus, longOffline = s.IsLongOffline(a),
                lockouts = s.Lockouts.Where(l => l.AnalyzerId == a.Id).Select(l => l.TestCode).ToList(), queue = s.Queue.GetValueOrDefault(a.Id)
            }),
            rows
        };
    }

    /// <summary>Картка послуги (FR-SVA-003): показники × прилади з ознакою «виконує», обов'язковий/типовий, стан приладів.</summary>
    public async Task<object> ServiceCardAsync(string profileOrServiceCode)
    {
        var (map, profile) = await ResolveServiceAsync(profileOrServiceCode);
        var s = await _routing.SnapshotAsync();
        var ind = map != null ? await IndicatorsAsync(map) : profile!.Items.Where(i => i.Test != null).OrderBy(i => i.DisplayOrder).Select(i => new ServiceIndicator(i.Test!, i.IsRequired, i.IsDefaultSelected, i.DisplayOrder)).ToList();
        var profileId = map?.ProfileId ?? profile?.Id;
        var analyzerIds = ind.SelectMany(i => s.ParamsByTest[i.Test.Code].Select(p => p.AnalyzerId)).Concat(s.Services.Keys.Where(k => k.ProfileId == profileId).Select(k => k.AnalyzerId)).Distinct()
            .Where(s.Analyzers.ContainsKey).ToList();
        return new
        {
            mapId = map?.Id, serviceCode = map?.Intervention?.Code, serviceName = map?.Intervention?.Name, profileId, profileCode = profile?.Code ?? map?.Profile?.Code,
            profileName = profile?.Name ?? map?.Profile?.Name, mode = map?.IndicatorSelectionMode, readiness = ComputeReadiness(map, ind, s),
            analyzers = analyzerIds.Select(id => s.Analyzers[id]).Select(a =>
            {
                var svc = profileId == null ? null : s.Services.GetValueOrDefault((a.Id, profileId));
                return new
                {
                    a.Id, a.Name, a.Kind, a.Location, a.AvgTatMinutes, a.IsOnline, longOffline = s.IsLongOffline(a), a.ServiceStatus, queue = s.Queue.GetValueOrDefault(a.Id),
                    inService = svc != null, enabled = svc?.IsEnabled ?? true, priority = svc?.Priority
                };
            }).OrderBy(a => a.inService ? 0 : 1).ThenBy(a => a.priority ?? 99),
            indicators = ind.Select(i => new
            {
                i.Test.Id, i.Test.Code, i.Test.Name, i.Test.Unit, i.IsRequired, i.IsDefault,
                analyzers = analyzerIds.ToDictionary(id => id, id =>
                {
                    var p = s.ParamsByTest[i.Test.Code].FirstOrDefault(x => x.AnalyzerId == id);
                    return new { performs = p != null, analyzerCode = p?.AnalyzerCode, priority = p?.Priority, locked = s.IsLocked(id, i.Test.Code) };
                })
            })
        };
    }

    private async Task<(LabInterventionMap? Map, LabTestProfile? Profile)> ResolveServiceAsync(string key)
    {
        var map = await _db.InterventionMaps.AsNoTracking().Include(m => m.Intervention).Include(m => m.Profile)
            .FirstOrDefaultAsync(m => m.IsActive && (m.Id == key || m.Intervention!.Code == key || m.ProfileId == key || m.Profile!.Code == key));
        var profileId = map?.ProfileId ?? key;
        var profile = await _db.Profiles.AsNoTracking().Include(p => p.Items).ThenInclude(i => i.Test).FirstOrDefaultAsync(p => p.Id == profileId || p.Code == profileId);
        if (map == null && profile == null) throw NotFoundException.For("Послуга", key);
        return (map, profile);
    }

    /// <summary>Редагування картки послуги (FR-SVA-007): прилади послуги, пріоритет, «обов'язковий/типовий», показники на приладах. Кожна зміна — в аудит.</summary>
    public async Task<object> SaveServiceCardAsync(string key, ServiceCardUpdateRequest req)
    {
        _policy.Require("Редагування довідника «Послуга ЕСОЗ × аналізатори»", EditorRoles);
        var (mapRo, profileRo) = await ResolveServiceAsync(key);
        var profileId = mapRo?.ProfileId ?? profileRo?.Id;
        var affected = 0;
        var toReroute = new List<(string AnalyzerId, string? TestCode, string? ProfileId)>();
        if (profileId != null)
        {
            foreach (var a in req.Analyzers)
            {
                var svc = await _db.AnalyzerServices.FirstOrDefaultAsync(x => x.AnalyzerId == a.AnalyzerId && x.ProfileId == profileId);
                if (svc != null && svc.IsEnabled && !a.IsEnabled) { affected += (await _routing.AffectedAsync(a.AnalyzerId!, null, profileId)).Count; toReroute.Add((a.AnalyzerId!, null, profileId)); }
                if (req.DryRun) continue;
                if (svc == null)
                {
                    if (!await _db.Analyzers.AnyAsync(x => x.Id == a.AnalyzerId)) throw NotFoundException.For("Аналізатор", a.AnalyzerId);
                    svc = new LabAnalyzerService { AnalyzerId = a.AnalyzerId!, ProfileId = profileId };
                    _db.AnalyzerServices.Add(svc);
                }
                var before = new { svc.IsEnabled, svc.Priority };
                svc.IsEnabled = a.IsEnabled; svc.Priority = Math.Max(1, a.Priority); if (a.AvgTatMinutes.HasValue) svc.AvgTatMinutes = a.AvgTatMinutes.Value;
                _audit.Log("UPDATE", "lab_analyzer_service", svc.Id, before, new { svc.IsEnabled, svc.Priority });
            }
            if (!req.DryRun)
                foreach (var f in req.Indicators)
                {
                    var item = await _db.ProfileItems.Include(i => i.Test).FirstOrDefaultAsync(i => i.ProfileId == profileId && i.Test!.Code == f.TestCode) ?? throw NotFoundException.For("Показник послуги", f.TestCode);
                    _audit.Log("UPDATE", "lab_test_profile_items", item.Id, new { item.IsRequired, item.IsDefaultSelected }, new { f.IsRequired, f.IsDefault });
                    item.IsRequired = f.IsRequired; item.IsDefaultSelected = f.IsDefault || f.IsRequired;
                }
        }
        foreach (var p in req.Params)
        {
            var pm = await _db.AnalyzerParameters.FirstOrDefaultAsync(x => x.AnalyzerId == p.AnalyzerId && x.TestCode == p.TestCode);
            if (pm != null && pm.IsEnabled && !p.IsEnabled) { affected += (await _routing.AffectedAsync(p.AnalyzerId, p.TestCode)).Count; toReroute.Add((p.AnalyzerId, p.TestCode, null)); }
            if (req.DryRun) continue;
            if (pm == null)
            {
                if (!p.IsEnabled) continue;
                if (string.IsNullOrWhiteSpace(p.AnalyzerCode))
                    throw new DomainProblemException(422, "analyzer-code-missing", "Немає коду на приладі", $"Для показника {p.TestCode} не задано код на приладі (FR-EQP-006)");
                pm = new LabAnalyzerParameterMap { AnalyzerId = p.AnalyzerId, TestCode = p.TestCode, AnalyzerCode = p.AnalyzerCode.Trim() };
                _db.AnalyzerParameters.Add(pm);
            }
            pm.IsEnabled = p.IsEnabled; if (p.Priority.HasValue) pm.Priority = p.Priority;
        }
        if (!req.DryRun && mapRo != null && req.Mode != null)
        {
            if (!SelectionModes.All.Contains(req.Mode)) throw ValidationException.Field("mode", $"Режим: {string.Join(", ", SelectionModes.All)}");
            var map = await _db.InterventionMaps.FirstAsync(m => m.Id == mapRo.Id);
            map.IndicatorSelectionMode = req.Mode;
        }
        if (req.DryRun) return new { dryRun = true, affected };
        await _db.SaveChangesAsync();
        var rerouted = new List<object>();
        foreach (var r in toReroute) rerouted.Add(await _routing.RerouteFromAsync(r.AnalyzerId, r.TestCode, "Вимкнено в довіднику «Послуга ЕСОЗ × аналізатори»", r.ProfileId, unassignIfStuck: true));
        await _db.SaveChangesAsync();
        await RecomputeReadinessAsync();
        return new { affected, rerouted, card = await ServiceCardAsync(key) };
    }

    /// <summary>Експорт матриці CSV (FR-SVA-009): послуга; прилад; пріоритет; увімкнено.</summary>
    public async Task<string> MatrixCsvAsync()
    {
        var rows = await _db.AnalyzerServices.AsNoTracking().Include(s => s.Profile).Include(s => s.Analyzer).OrderBy(s => s.Profile!.Code).ThenBy(s => s.Priority).ToListAsync();
        var sb = new StringBuilder("profileCode;profileName;analyzerCode;analyzerName;priority;isEnabled\n");
        foreach (var r in rows) sb.Append($"{r.Profile?.Code};{r.Profile?.Name?.Replace(';', ',')};{r.Analyzer?.Code};{r.Analyzer?.Name?.Replace(';', ',')};{r.Priority};{(r.IsEnabled ? 1 : 0)}\n");
        return sb.ToString();
    }

    /// <summary>Імпорт матриці CSV з попереднім переглядом (dryRun) змін.</summary>
    public async Task<object> ImportMatrixCsvAsync(string csv, bool dryRun)
    {
        _policy.Require("Імпорт матриці послуг обладнання", EditorRoles);
        var changes = new List<object>();
        var errors = new List<string>();
        var lines = csv.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
        foreach (var (line, i) in lines.Select((l, i) => (l, i + 2)))
        {
            var c = line.Split(';');
            if (c.Length < 6) { errors.Add($"Рядок {i}: очікується 6 колонок"); continue; }
            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Code == c[0].Trim());
            var an = await _db.Analyzers.FirstOrDefaultAsync(a => a.Code == c[2].Trim());
            if (profile == null || an == null) { errors.Add($"Рядок {i}: невідома послуга {c[0]} або прилад {c[2]}"); continue; }
            if (!int.TryParse(c[4], out var prio)) { errors.Add($"Рядок {i}: пріоритет має бути числом"); continue; }
            var enabled = c[5].Trim() is "1" or "true" or "так";
            var svc = await _db.AnalyzerServices.FirstOrDefaultAsync(s => s.AnalyzerId == an.Id && s.ProfileId == profile.Id);
            if (svc != null && svc.Priority == prio && svc.IsEnabled == enabled) continue;
            changes.Add(new { profile = profile.Code, analyzer = an.Code, from = svc == null ? null : new { svc.Priority, svc.IsEnabled }, to = new { priority = prio, isEnabled = enabled } });
            if (dryRun) continue;
            if (svc == null) { svc = new LabAnalyzerService { AnalyzerId = an.Id, ProfileId = profile.Id }; _db.AnalyzerServices.Add(svc); }
            svc.Priority = prio; svc.IsEnabled = enabled;
        }
        if (!dryRun && errors.Count == 0) { await _db.SaveChangesAsync(); await RecomputeReadinessAsync(); _audit.Log("IMPORT", "lab_analyzer_service", null, null, new { changes = changes.Count }); await _db.SaveChangesAsync(); }
        return new { dryRun, applied = !dryRun && errors.Count == 0, changes, errors };
    }

    // ------------------------------------------------------------------ каталог для МІС (23.2) і пакети (23.3)
    public async Task<(object Body, string Version)> CatalogAsync()
    {
        var s = await _routing.SnapshotAsync();
        var maps = await _db.InterventionMaps.AsNoTracking().Include(m => m.Intervention).Include(m => m.Profile).Where(m => m.IsActive && m.Intervention!.IsActive).ToListAsync();
        var tubes = await _db.TubeTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name);
        var materials = await _db.BiomaterialTypes.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name);
        var services = new List<object>();
        foreach (var m in maps.OrderBy(m => m.Intervention!.Code))
        {
            var ind = await IndicatorsAsync(m);
            services.Add(new
            {
                interventionCode = m.Intervention!.Code, name = m.Intervention.Name, mode = m.IndicatorSelectionMode, m.MinSelected, isPathology = m.IsPathology,
                biomaterial = m.Profile?.DefaultBiomaterialTypeId is int b ? materials.GetValueOrDefault(b) : null, tube = m.Profile?.DefaultTubeTypeId is int tt ? tubes.GetValueOrDefault(tt) : null, fasting = m.Profile?.FastingRequired ?? false,
                readiness = ComputeReadiness(m, ind, s),
                indicators = ind.Select(i => new { code = i.Test.Code, name = i.Test.Name, unit = i.Test.Unit, isRequired = i.IsRequired, isDefault = i.IsDefault, performedInHouse = RoutingService.Candidates(s, i.Test.Code, m.ProfileId).Count > 0 })
            });
        }
        var json = JsonSerializer.Serialize(services, JsonDefaults.Options);
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..16].ToLowerInvariant();
        return (new { catalogVersion = version, generatedAt = DateTime.UtcNow, services }, version);
    }

    public async Task<object> PackagesAsync() => (await _db.ServicePackages.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Code).ToListAsync())
        .Select(p => new { p.Id, p.Code, p.Name, p.Context, p.Purpose, p.IsCito, items = JsonSerializer.Deserialize<JsonElement>(p.ItemsJson), p.SourceVersion, p.SyncedOn });
}
