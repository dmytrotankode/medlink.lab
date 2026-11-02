// ТЗ 21.7, 23.4: маршрутизація тестів у черги приладів — кандидати (послуга + показник на приладі), виключення локаутів ВКЯ
// і тривалої відсутності зв'язку, групування показників послуги проби на один прилад, стратегія PRIORITY / LEAST_LOAD / PRIORITY_THEN_LOAD,
// CITO — менший TAT; ручне перенаправлення з причиною; переназначення при локауті/вимкненні; журнал lab_routing_log.
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

/// <summary>Прилад-кандидат для показника: пріоритет, черга, TAT і доступність з причиною.</summary>
public sealed record RoutingCandidate(string AnalyzerId, string Name, string Kind, int Priority, int Queue, int AvgTatMinutes, bool Available, string? Reason, bool Online);

public sealed class RoutingService
{
    public static readonly string[] QueueStatuses = { OrderTestStatuses.Pending, OrderTestStatuses.Rerun, OrderTestStatuses.InAnalysis };
    private static readonly string[] MovableStatuses = { OrderTestStatuses.Pending, OrderTestStatuses.Rerun };
    /// <summary>Розділи без маршрутизації: мікробіологія (розд. 4.10), патоморфологія й цитологія (розд. 22).</summary>
    private static readonly string[] UnroutedSections = { "MICRO", "PATH", "CYTO" };
    public const int SeparateIndicatorPriority = 10;

    private readonly LisDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;

    public RoutingService(LisDbContext db, IAuditService audit, ICurrentEmployee current) { _db = db; _audit = audit; _current = current; }

    /// <summary>Знімок довідника обладнання на момент маршрутизації (один запит на виклик).</summary>
    public sealed class Snapshot
    {
        public Dictionary<string, LabAnalyzer> Analyzers { get; init; } = new();
        public ILookup<string, LabAnalyzerParameterMap> ParamsByTest { get; init; } = null!;
        public Dictionary<(string AnalyzerId, string ProfileId), LabAnalyzerService> Services { get; init; } = new();
        public HashSet<(string AnalyzerId, string? TestCode)> Lockouts { get; init; } = new();
        public Dictionary<string, int> Queue { get; init; } = new();
        public LabSettings Settings { get; init; } = new();
        public DateTime Now { get; init; } = DateTime.UtcNow;

        public bool IsLocked(string analyzerId, string testCode) => Lockouts.Contains((analyzerId, testCode)) || Lockouts.Contains((analyzerId, null));

        /// <summary>Прилад без зв'язку довше за поріг (FR-RTE-010, 21.7 крок 5).</summary>
        public bool IsLongOffline(LabAnalyzer a)
        {
            // Відлік — з моменту втрати зв'язку (heartbeat коннектора); прилад, що ще не підключався, лишається кандидатом
            if (a.IsOnline || a.Kind == AnalyzerKinds.Manual || a.OfflineSince == null) return false;
            return (Now - a.OfflineSince.Value).TotalMinutes > Settings.RerouteOnOfflineMinutes;
        }
    }

    public async Task<Snapshot> SnapshotAsync()
    {
        var analyzers = await _db.Analyzers.AsNoTracking().Where(a => a.IsActive && a.RecordState != RecordStates.Deleted && a.ServiceStatus != AnalyzerServiceStatuses.Retired).ToListAsync();
        var ids = analyzers.Select(a => a.Id).ToList();
        var parms = await _db.AnalyzerParameters.AsNoTracking().Where(p => ids.Contains(p.AnalyzerId) && p.IsEnabled).ToListAsync();
        var services = await _db.AnalyzerServices.AsNoTracking().Where(s => ids.Contains(s.AnalyzerId)).ToListAsync();
        var locks = await _db.Lockouts.AsNoTracking().Where(l => l.ResolvedAt == null).Select(l => new { l.AnalyzerId, l.TestCode }).ToListAsync();
        var queue = await _db.OrderTests.AsNoTracking().Where(t => t.AssignedAnalyzerId != null && QueueStatuses.Contains(t.Status))
            .GroupBy(t => t.AssignedAnalyzerId!).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        return new Snapshot
        {
            Analyzers = analyzers.ToDictionary(a => a.Id),
            ParamsByTest = parms.ToLookup(p => p.TestCode),
            Services = services.GroupBy(s => (s.AnalyzerId, s.ProfileId)).ToDictionary(g => g.Key, g => g.First()),
            Lockouts = locks.Select(l => (l.AnalyzerId, l.TestCode)).ToHashSet(),
            Queue = queue,
            Settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings()
        };
    }

    /// <summary>Кандидати для показника послуги: прилади з активним показником; послуга приладу вимкнена — прилад виключається;
    /// прилад поза переліком послуги бере участь як «окремі показники» (FR-SVA-004) з нижчим пріоритетом.</summary>
    public static List<RoutingCandidate> Candidates(Snapshot s, string testCode, string? profileId)
    {
        var list = new List<RoutingCandidate>();
        foreach (var p in s.ParamsByTest[testCode])
        {
            if (!s.Analyzers.TryGetValue(p.AnalyzerId, out var a)) continue;
            int priority;
            if (profileId != null && s.Services.TryGetValue((a.Id, profileId), out var svc))
            {
                if (!svc.IsEnabled) continue;
                priority = p.Priority ?? svc.Priority;
            }
            else priority = p.Priority ?? SeparateIndicatorPriority;
            string? reason = null;
            if (a.ServiceStatus == AnalyzerServiceStatuses.Maintenance) reason = "технічне обслуговування";
            else if (s.IsLocked(a.Id, testCode)) reason = "lockout ВКЯ";
            else if (s.IsLongOffline(a)) reason = "немає зв'язку";
            list.Add(new RoutingCandidate(a.Id, a.Name, a.Kind, priority, s.Queue.GetValueOrDefault(a.Id), a.AvgTatMinutes, reason == null, reason, a.IsOnline || a.Kind == AnalyzerKinds.Manual));
        }
        // Кілька кодів одного показника на приладі — один кандидат (найкращий пріоритет)
        return list.GroupBy(c => c.AnalyzerId).Select(g => g.OrderBy(c => c.Priority).First())
            .OrderBy(c => c.Priority).ThenBy(c => c.Queue).ThenBy(c => c.Name).ToList();
    }

    /// <summary>Ключ сортування за стратегією (менше — краще). CITO: менший TAT має перевагу (крайні випадки 21.7, FR-PKG-004).</summary>
    public static (double, double, double) Key(RoutingCandidate c, string strategy, bool cito)
    {
        double load = c.Queue * Math.Max(1, c.AvgTatMinutes);
        if (cito) return (c.AvgTatMinutes, c.Priority, load);
        return strategy switch
        {
            "PRIORITY" => (c.Priority, 0, 0),
            "LEAST_LOAD" => (load, c.Priority, 0),
            _ => (c.Priority, c.Queue, 0)
        };
    }

    /// <summary>Розподіл групи показників однієї послуги в одній пробі: спершу прилад, що виконує всі показники (21.7 крок 3);
    /// інакше кожен показник — на найкращий доступний прилад з урахуванням покриття. Повертає testCode → кандидат (null — немає обладнання).</summary>
    public static Dictionary<string, RoutingCandidate?> Assign(Snapshot s, IReadOnlyList<string> testCodes, string? profileId, bool cito)
    {
        var strategy = s.Settings.RoutingStrategy;
        var byTest = testCodes.Distinct().ToDictionary(c => c, c => Candidates(s, c, profileId).Where(x => x.Available).ToList());
        // Покриття — кількість різних показників групи (прилад може мати кілька кодів одного показника)
        var analyzers = byTest.SelectMany(kv => kv.Value.Select(c => (Code: kv.Key, Cand: c))).GroupBy(x => x.Cand.AnalyzerId)
            .Select(g => new { Id = g.Key, Cover = g.Select(x => x.Code).Distinct().Count(), Best = g.Select(x => x.Cand).OrderBy(x => Key(x, strategy, cito)).First() }).ToList();
        var result = new Dictionary<string, RoutingCandidate?>();
        var whole = analyzers.Where(a => a.Cover == byTest.Count).OrderBy(a => Key(a.Best, strategy, cito)).FirstOrDefault();
        foreach (var (code, cands) in byTest)
        {
            if (whole != null) { result[code] = cands.First(c => c.AnalyzerId == whole.Id); continue; }
            var pick = cands.OrderByDescending(c => analyzers.First(a => a.Id == c.AnalyzerId).Cover).ThenBy(c => Key(c, strategy, cito)).FirstOrDefault();
            result[code] = pick;
        }
        return result;
    }

    /// <summary>Маршрутизує нові тести замовлення (FR-RTE-001). propose=true — пропозиція (PROPOSED), що чекає підтвердження розподілу (FR-DST-008).</summary>
    public async Task RouteAsync(LabOrder order, IEnumerable<LabOrderTest> tests, bool propose, Snapshot? snapshot = null)
    {
        var list = tests.ToList();
        if (list.Count == 0) return;
        var s = snapshot ?? await SnapshotAsync();
        var sectionCodes = await _db.Sections.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code);
        var formulas = await _db.Tests.AsNoTracking().Where(t => t.Formula != null && t.Formula != "").Select(t => t.Id).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var group in list.GroupBy(t => (t.SampleId, t.ProfileId, t.OrderItemId)))
        {
            var routable = new List<LabOrderTest>();
            foreach (var t in group)
            {
                if (t.PerformerId != null) { t.RoutingStatus = RoutingStatuses.SendOut; continue; }
                var section = t.Test?.LabSectionId == null ? null : sectionCodes.GetValueOrDefault(t.Test.LabSectionId);
                if (section != null && UnroutedSections.Contains(section)) continue; // мікробіологія/патоморфологія — без маршрутизації
                if (formulas.Contains(t.TestId)) continue;                            // розрахунковий — обчислюється після складових
                routable.Add(t);
            }
            if (routable.Count == 0) continue;
            var assigned = Assign(s, routable.Select(t => t.TestCode).ToList(), group.Key.ProfileId, order.IsUrgentCito);
            var split = assigned.Values.Where(v => v != null).Select(v => v!.AnalyzerId).Distinct().Count() > 1;
            foreach (var t in routable)
            {
                var pick = assigned[t.TestCode];
                var candidates = Candidates(s, t.TestCode, t.ProfileId);
                if (pick == null)
                {
                    t.RoutingStatus = RoutingStatuses.NoEquipment;
                    t.AssignedAnalyzerId = null;
                }
                else
                {
                    t.AssignedAnalyzerId = pick.AnalyzerId;
                    t.RoutingStatus = propose ? RoutingStatuses.Proposed : RoutingStatuses.Routed;
                    t.RoutedBy = "AUTO";
                    t.EnqueuedOn = propose ? null : now;
                    s.Queue[pick.AnalyzerId] = s.Queue.GetValueOrDefault(pick.AnalyzerId) + 1;
                }
                _db.RoutingLog.Add(new LabRoutingLog
                {
                    OrderTestId = t.Id, OrderId = order.Id, TestCode = t.TestCode, Event = propose ? RoutingEvents.Propose : RoutingEvents.Route,
                    CandidatesJson = JsonSerializer.Serialize(candidates.Select(c => new { c.AnalyzerId, c.Name, c.Priority, c.Queue, c.AvgTatMinutes, c.Available, c.Reason }), JsonDefaults.Options),
                    AnalyzerId = pick?.AnalyzerId, Rule = order.IsUrgentCito ? "CITO_MIN_TAT" : s.Settings.RoutingStrategy,
                    Reason = pick == null ? "Жоден прилад не виконує показник (або всі недоступні)" : split ? "Показники проби розподілено між приладами" : null,
                    ActorId = _current.EmployeeId, At = now
                });
            }
        }
    }

    /// <summary>Попередній розрахунок маршрутизації без збереження (POST /routing/preview).</summary>
    public async Task<object> PreviewAsync(string? profileId, List<string> testCodes, bool cito)
    {
        var s = await SnapshotAsync();
        var profile = profileId == null ? null : await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId || p.Code == profileId);
        var assigned = Assign(s, testCodes, profile?.Id, cito);
        return new
        {
            strategy = s.Settings.RoutingStrategy, cito,
            tests = testCodes.Select(c => new
            {
                code = c, analyzerId = assigned.GetValueOrDefault(c)?.AnalyzerId, analyzer = assigned.GetValueOrDefault(c)?.Name,
                routingStatus = assigned.GetValueOrDefault(c) == null ? RoutingStatuses.NoEquipment : RoutingStatuses.Routed,
                candidates = Candidates(s, c, profile?.Id)
            }),
            split = assigned.Values.Where(v => v != null).Select(v => v!.AnalyzerId).Distinct().Count() > 1
        };
    }

    /// <summary>Ручне перенаправлення (FR-RTE-006, FR-DST-004): лише на кандидата, обов'язкова причина, IN_ANALYSIS не переназначаються.</summary>
    public async Task<LabOrderTest> ReassignAsync(string orderTestId, string analyzerId, string? reason, bool distribution = false)
    {
        var t = await _db.OrderTests.Include(x => x.Order).Include(x => x.Test).FirstOrDefaultAsync(x => x.Id == orderTestId) ?? throw NotFoundException.For("Тест замовлення", orderTestId);
        if (!distribution && string.IsNullOrWhiteSpace(reason)) throw ValidationException.Field("reason", "Вкажіть причину перенаправлення");
        if (!MovableStatuses.Contains(t.Status)) throw new ConflictException($"Тест {t.TestCode} у статусі {t.Status} — перенаправлення неможливе (лише в черзі, до початку аналізу)");
        var s = await SnapshotAsync();
        var cand = Candidates(s, t.TestCode, t.ProfileId).FirstOrDefault(c => c.AnalyzerId == analyzerId)
                   ?? throw new ValidationException($"Прилад не виконує показник {t.TestCode} — оберіть прилад із переліку кандидатів");
        if (!cand.Available) throw new ConflictException($"Прилад {cand.Name} недоступний: {cand.Reason}");
        var from = t.AssignedAnalyzerId;
        if (from == analyzerId) return t;
        t.AssignedAnalyzerId = analyzerId;
        t.RoutedBy = "MANUAL";
        if (t.RoutingStatus != RoutingStatuses.Proposed) { t.RoutingStatus = RoutingStatuses.Routed; t.EnqueuedOn = DateTime.UtcNow; }
        _db.RoutingLog.Add(new LabRoutingLog
        {
            OrderTestId = t.Id, OrderId = t.OrderId, TestCode = t.TestCode, Event = distribution ? RoutingEvents.Change : RoutingEvents.Reroute,
            FromAnalyzerId = from, AnalyzerId = analyzerId, RoutedBy = "MANUAL", Reason = reason, ActorId = _current.EmployeeId, Rule = "MANUAL"
        });
        _audit.Log("REROUTE", "lab_order_test", t.Id, new { analyzerId = from }, new { analyzerId }, reason);
        return t;
    }

    /// <summary>Тести в черзі приладу (для показника, якщо задано), які зачепить вимкнення/локаут (FR-EQP-007).</summary>
    public async Task<List<LabOrderTest>> AffectedAsync(string analyzerId, string? testCode = null, string? profileId = null) =>
        await _db.OrderTests.Include(t => t.Order).Include(t => t.Test)
            .Where(t => t.AssignedAnalyzerId == analyzerId && MovableStatuses.Contains(t.Status) && (testCode == null || t.TestCode == testCode) && (profileId == null || t.ProfileId == profileId)
                        && t.Result == null)
            .ToListAsync();

    /// <summary>Переназначення тестів з приладу (локаут ВКЯ FR-RTE-008, вимкнення FR-RTE-009, офлайн FR-RTE-010). Без кандидатів — лишаються з позначкою.</summary>
    public async Task<object> RerouteFromAsync(string analyzerId, string? testCode, string reason, string? profileId = null, bool onlineOnly = false, bool unassignIfStuck = false)
    {
        var affected = await AffectedAsync(analyzerId, testCode, profileId);
        var s = await SnapshotAsync();
        int moved = 0, stuck = 0;
        foreach (var t in affected)
        {
            var cand = Candidates(s, t.TestCode, t.ProfileId).Where(c => c.AnalyzerId != analyzerId && c.Available && (!onlineOnly || c.Online))
                .OrderBy(c => Key(c, s.Settings.RoutingStrategy, t.Order?.IsUrgentCito ?? false)).FirstOrDefault();
            if (cand == null)
            {
                stuck++;
                if (unassignIfStuck) { t.AssignedAnalyzerId = null; t.RoutingStatus = RoutingStatuses.NoEquipment; }
                continue;
            }
            _db.RoutingLog.Add(new LabRoutingLog
            {
                OrderTestId = t.Id, OrderId = t.OrderId, TestCode = t.TestCode, Event = RoutingEvents.Reroute, FromAnalyzerId = analyzerId, AnalyzerId = cand.AnalyzerId,
                RoutedBy = "AUTO", Reason = reason, Rule = s.Settings.RoutingStrategy, ActorId = _current.EmployeeId
            });
            t.AssignedAnalyzerId = cand.AnalyzerId;
            t.RoutedBy = "AUTO";
            if (t.RoutingStatus != RoutingStatuses.Proposed) t.RoutingStatus = RoutingStatuses.Routed;
            s.Queue[cand.AnalyzerId] = s.Queue.GetValueOrDefault(cand.AnalyzerId) + 1;
            moved++;
        }
        return new { affected = affected.Count, moved, stuck };
    }

    /// <summary>Черга приладу (FR-RTE-002): CITO → плановий час забору → час приймання проби.</summary>
    public async Task<object> QueueAsync(string analyzerId)
    {
        var a = await _db.Analyzers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == analyzerId) ?? throw NotFoundException.For("Аналізатор", analyzerId);
        var rows = await QueueQuery(analyzerId).ToListAsync();
        return new
        {
            analyzerId, analyzer = a.Name, a.Kind, a.IsOnline, a.Location, a.AvgTatMinutes,
            items = rows.Select((t, i) => new
            {
                position = i + 1, orderTestId = t.Id, t.OrderId, orderNumber = t.Order!.OrderNumber, barcode = t.Sample?.Barcode, sampleStatus = t.Sample?.Status,
                patientName = t.Order.Patient?.Caption, t.TestCode, t.TestName, t.Status, t.RoutingStatus, t.RoutedBy, isCito = t.Order.IsUrgentCito,
                source = ReferralSources.Of(t.Order), t.Order.PackageCode, department = t.Order.RequestingDepartmentName ?? t.Order.Department?.Caption,
                bed = Bed(t.Order), waiting = t.Sample == null || t.Sample.ReceivedAt == null
            })
        };
    }

    public IQueryable<LabOrderTest> QueueQuery(string analyzerId) => _db.OrderTests.AsNoTracking()
        .Include(t => t.Order).ThenInclude(o => o!.Patient).Include(t => t.Order).ThenInclude(o => o!.Department).Include(t => t.Sample)
        .Where(t => t.AssignedAnalyzerId == analyzerId && QueueStatuses.Contains(t.Status) && t.Result == null)
        .OrderByDescending(t => t.Order!.IsUrgentCito).ThenBy(t => t.Order!.PlannedCollectionAt ?? t.Order.OrderDatetime).ThenBy(t => t.Sample!.ReceivedAt).ThenBy(t => t.DisplayOrder);

    /// <summary>Позиції тестів у чергах приладів (для картки замовлення, FR-RTE-004).</summary>
    public async Task<Dictionary<string, int>> PositionsAsync(IEnumerable<LabOrderTest> tests)
    {
        var res = new Dictionary<string, int>();
        foreach (var g in tests.Where(t => t.AssignedAnalyzerId != null && QueueStatuses.Contains(t.Status)).GroupBy(t => t.AssignedAnalyzerId!))
        {
            var ids = await QueueQuery(g.Key).Select(t => t.Id).ToListAsync();
            foreach (var t in g) { var i = ids.IndexOf(t.Id); if (i >= 0) res[t.Id] = i + 1; }
        }
        return res;
    }

    public async Task<object> LogAsync(string? orderTestId, string? orderId, int take = 200)
    {
        var q = _db.RoutingLog.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(orderTestId)) q = q.Where(l => l.OrderTestId == orderTestId);
        if (!string.IsNullOrWhiteSpace(orderId)) q = q.Where(l => l.OrderId == orderId);
        var rows = await q.OrderByDescending(l => l.At).Take(Math.Clamp(take, 1, 2000)).ToListAsync();
        var names = await _db.Analyzers.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Name);
        return rows.Select(l => new
        {
            l.Id, l.At, l.Event, l.OrderTestId, l.OrderId, l.TestCode, l.Rule, l.RoutedBy, l.Reason, l.ActorId,
            fromAnalyzer = l.FromAnalyzerId == null ? null : names.GetValueOrDefault(l.FromAnalyzerId), l.FromAnalyzerId,
            analyzer = l.AnalyzerId == null ? null : names.GetValueOrDefault(l.AnalyzerId), l.AnalyzerId,
            candidates = l.CandidatesJson == null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(l.CandidatesJson)
        });
    }

    public static string? Bed(LabOrder o) =>
        o.Ward == null && o.Bed == null && o.Room == null ? null : string.Join(", ", new[] { o.Ward == null ? null : "палата " + o.Ward, o.Bed == null ? null : "ліжко " + o.Bed }.Where(x => x != null));
}
