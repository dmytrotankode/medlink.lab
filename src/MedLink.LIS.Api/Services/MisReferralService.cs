// ТЗ 21.6, 21.8, 23.2–23.4: прийом направлень/призначень із МІС (окремі послуги й пакети), вибір показників лікарем і перевірка складу,
// скасування, зміна складу, розподіл по аналізаторах лаборантом (пропозиція → зміна → підтвердження) і зворотні події для МІС.
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class MisReferralItemRequest
{
    public string InterventionCode { get; set; } = "";
    /// <summary>Коди показників, обраних лікарем; порожньо — «потрібен вибір» (FR-MIS-022) або фіксований склад.</summary>
    public List<string> Indicators { get; set; } = new();
    public string? Comment { get; set; }
    public string? EhealthServiceRequestId { get; set; }
}

public sealed class MisPackageRef
{
    public string Code { get; set; } = "";
    public string? Name { get; set; }
    public bool Cito { get; set; }
    public bool ChangedByDoctor { get; set; }
}

public sealed class MisReferralRequest
{
    /// <summary>Ідентифікатор направлення/призначення в МІС (ідемпотентність).</summary>
    public string? MisReferralId { get; set; }
    public string? ReferralNumber { get; set; }
    /// <summary>INPATIENT | AMBULATORY</summary>
    public string Context { get; set; } = ReferralContexts.Inpatient;
    /// <summary>INPATIENT | POLYCLINIC | EHEALTH | PARTNER — джерело для розрізів (23.5); типово з контуру.</summary>
    public string? Source { get; set; }
    public string PatientId { get; set; } = "";
    public string? HospitalizationId { get; set; }
    public string? Department { get; set; }
    public string? Ward { get; set; }
    public string? Room { get; set; }
    public string? Bed { get; set; }
    public string? DoctorId { get; set; }
    public string? DoctorName { get; set; }
    /// <summary>ROUTINE | CITO</summary>
    public string Priority { get; set; } = "ROUTINE";
    public DateTime? PlannedCollectionAt { get; set; }
    public string? Icd10 { get; set; }
    public string? ClinicalNotes { get; set; }
    /// <summary>Номер е-направлення ЕСОЗ (зовнішній контур): use в ЕСОЗ за розд. 20.6.2.</summary>
    public string? EhealthReferralNumber { get; set; }
    public string? PartnerName { get; set; }
    public MisPackageRef? Package { get; set; }
    public List<MisReferralItemRequest> Items { get; set; } = new();
    public DateTime? ReceivedAt { get; set; }
}

public sealed class ItemIndicatorsRequest
{
    public List<string> Indicators { get; set; } = new();
    /// <summary>REFERRING_DOCTOR | REGISTRAR | LAB_DOCTOR</summary>
    public string? SelectedBy { get; set; }
    public string? Reason { get; set; }
}

public sealed class DistributionChange
{
    public string? OrderTestId { get; set; }
    public string? ItemId { get; set; }
    public string? TestCode { get; set; }
    public string AnalyzerId { get; set; } = "";
    public string? Reason { get; set; }
}

public sealed class NoEquipmentDecision
{
    public string OrderTestId { get; set; } = "";
    /// <summary>SENDOUT | MANUAL | REJECT (FR-DST-010)</summary>
    public string Action { get; set; } = "MANUAL";
    public string? PerformerId { get; set; }
    public string? Reason { get; set; }
}

public sealed class DistributionConfirmRequest
{
    public List<NoEquipmentDecision> NoEquipment { get; set; } = new();
}

public sealed class MisReferralService
{
    public static readonly string[] DistributorRoles = { LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician };
    public static readonly string[] PendingDistribution = { "PROPOSED", "NEEDS_SELECTION" };

    private readonly LisDbContext _db;
    private readonly OrderService _orders;
    private readonly TubePlanService _tubes;
    private readonly RoutingService _routing;
    private readonly MisCatalogService _catalog;
    private readonly IAuditService _audit;
    private readonly IRolePolicy _policy;
    private readonly ICurrentEmployee _current;

    public MisReferralService(LisDbContext db, OrderService orders, TubePlanService tubes, RoutingService routing, MisCatalogService catalog, IAuditService audit, IRolePolicy policy, ICurrentEmployee current)
    {
        _db = db; _orders = orders; _tubes = tubes; _routing = routing; _catalog = catalog; _audit = audit; _policy = policy; _current = current;
    }

    private static DomainProblemException Problem(string code, string detail, int status = 422) => new(status, code, code switch
    {
        "intervention-unmapped" => "Інтервенцію не співставлено",
        "no-indicators-selected" => "Не обрано показників",
        "required-indicator-missing" => "Бракує обов'язкового показника",
        "unknown-indicator" => "Невідомий показник",
        "duplicate-referral" => "Направлення вже прийнято",
        "referral-in-progress" => "Направлення вже в роботі",
        _ => "Помилка направлення"
    }, detail);

    // ------------------------------------------------------------------ вибір показників (21.8, 23.2)
    /// <summary>Перевірка й доповнення вибору лікаря: фіксований склад, невідомі/обов'язкові показники, мінімум, складові розрахункових (FR-MIS-032).</summary>
    public static (List<ServiceIndicator> Selected, List<string> AutoAdded) Validate(LabInterventionMap map, List<ServiceIndicator> defs, IEnumerable<string> requested, string interventionCode)
    {
        if (map.IndicatorSelectionMode == SelectionModes.Fixed) return (defs, new());
        var codes = requested.Select(c => c.Trim().ToUpperInvariant()).Where(c => c != "").Distinct().ToList();
        var byCode = defs.ToDictionary(d => d.Test.Code.ToUpperInvariant());
        var unknown = codes.Where(c => !byCode.ContainsKey(c)).ToList();
        if (unknown.Count > 0) throw Problem("unknown-indicator", $"{interventionCode}: показники {string.Join(", ", unknown)} не входять до складу послуги");
        if (codes.Count < Math.Max(1, map.MinSelected)) throw Problem("no-indicators-selected", $"{interventionCode}: оберіть щонайменше {Math.Max(1, map.MinSelected)} показник(и)");
        var missing = defs.Where(d => d.IsRequired && !codes.Contains(d.Test.Code.ToUpperInvariant())).Select(d => d.Test.Code).ToList();
        if (missing.Count > 0) throw Problem("required-indicator-missing", $"{interventionCode}: обов'язкові показники не можна зняти — {string.Join(", ", missing)}");
        var selected = codes.Select(c => byCode[c]).ToList();
        var auto = new List<string>();
        foreach (var d in selected.ToList().Where(d => !string.IsNullOrWhiteSpace(d.Test.Formula)))
            foreach (var dep in defs.Where(x => x.Test.Code != d.Test.Code && System.Text.RegularExpressions.Regex.IsMatch(d.Test.Formula!, $@"\b{x.Test.Code}\b")))
                if (!selected.Contains(dep)) { selected.Add(dep); auto.Add(dep.Test.Code); }
        return (selected.OrderBy(s => s.DisplayOrder).ToList(), auto);
    }

    // ------------------------------------------------------------------ прийом
    public async Task<object> CreateAsync(MisReferralRequest req)
    {
        if (!ReferralContexts.All.Contains(req.Context)) throw ValidationException.Field("context", "Контур: AMBULATORY або INPATIENT");
        if (req.Items.Count == 0) throw ValidationException.Field("items", "Направлення має містити хоча б одну позицію");
        if (!await _db.Patients.AnyAsync(p => p.Id == req.PatientId)) throw NotFoundException.For("Пацієнт", req.PatientId);
        // ідентифікатори МІС — GUID (uuid у PostgreSQL); людський номер — referralNumber
        if (!string.IsNullOrWhiteSpace(req.MisReferralId) && !Guid.TryParse(req.MisReferralId, out _)) throw ValidationException.Field("misReferralId", "Ідентифікатор направлення МІС має бути GUID");
        if (!string.IsNullOrWhiteSpace(req.HospitalizationId) && !Guid.TryParse(req.HospitalizationId, out _)) throw ValidationException.Field("hospitalizationId", "Ідентифікатор госпіталізації МІС має бути GUID");
        if (!string.IsNullOrWhiteSpace(req.MisReferralId) && await _db.Orders.AnyAsync(o => o.MisReferralId == req.MisReferralId && o.Status != OrderStatuses.Cancelled))
            throw Problem("duplicate-referral", $"Направлення МІС {req.MisReferralId} вже прийнято", 409);

        var source = (req.Source ?? (req.EhealthReferralNumber != null ? ReferralSources.Ehealth : req.Context == ReferralContexts.Inpatient ? ReferralSources.Inpatient : ReferralSources.Polyclinic)).ToUpperInvariant();
        var external = source is ReferralSources.Ehealth or ReferralSources.Partner;
        var cito = req.Priority.Equals("CITO", StringComparison.OrdinalIgnoreCase) || req.Package?.Cito == true;
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();
        var warnings = new List<string>();
        var now = DateTime.UtcNow;

        // Позиції: інтервенція → співставлення → показники
        var items = new List<LabOrderItem>();
        var pairs = new List<(LabTestDefinition Test, LabTestProfile? Profile, LabOrderItem Item)>();
        var no = 0;
        foreach (var it in req.Items)
        {
            var code = it.InterventionCode.Trim();
            var iv = await _db.Interventions.AsNoTracking().FirstOrDefaultAsync(i => i.Code == code);
            var map = iv == null ? null : await _db.InterventionMaps.AsNoTracking().Include(m => m.Profile).FirstOrDefaultAsync(m => m.InterventionId == iv.Id && m.IsActive);
            var item = new LabOrderItem
            {
                InterventionId = iv?.Id, InterventionCode = code, InterventionName = iv?.Name ?? code, ProfileId = map?.ProfileId, Comment = it.Comment, IsCito = cito,
                EhealthServiceRequestId = it.EhealthServiceRequestId, DisplayOrder = ++no, PackageItemNo = req.Package == null ? null : no,
                SelectionMode = map?.IndicatorSelectionMode ?? SelectionModes.Selectable
            };
            items.Add(item);
            if (map == null || map.IsPathology)
            {
                if (!external) throw Problem("intervention-unmapped", map?.IsPathology == true
                    ? $"{code}: патоморфологічна послуга — направлення приймає модуль «Патоморфологія» (POST /pathology/referrals)"
                    : $"{code}: інтервенцію ЕСОЗ не співставлено з послугою ЛІС");
                item.Status = "UNMAPPED";
                warnings.Add($"{code}: інтервенцію не співставлено — реєстратор обирає послугу вручну, LAB_ADMIN отримав задачу «Співставити інтервенцію»");
                _audit.Log("MAPPING_TASK", "lab_ehealth_intervention", iv?.Id, null, new { code, assignee = LabRoles.Admin });
                continue;
            }
            var defs = await _catalog.IndicatorsAsync(map);
            if (map.IndicatorSelectionMode != SelectionModes.Fixed && it.Indicators.Count == 0)
            {
                if (!external) throw Problem("no-indicators-selected", $"{code}: оберіть показники послуги");
                item.Status = "NEEDS_SELECTION";
                warnings.Add($"{code}: показники не передано — потрібен вибір показників (типовий набір: {string.Join(", ", defs.Where(d => d.IsDefault).Select(d => d.Test.Code))})");
                foreach (var d in defs) item.Indicators.Add(new LabOrderItemIndicator { ItemId = item.Id, TestId = d.Test.Id, TestCode = d.Test.Code, IsRequired = d.IsRequired, IsSelected = false });
                continue;
            }
            var (selected, auto) = Validate(map, defs, it.Indicators, code);
            item.SelectionSource = map.IndicatorSelectionMode == SelectionModes.Fixed ? SelectionSources.Fixed : SelectionSources.ReferringDoctor;
            foreach (var d in defs)
            {
                var sel = selected.Contains(d);
                item.Indicators.Add(new LabOrderItemIndicator
                {
                    ItemId = item.Id, TestId = d.Test.Id, TestCode = d.Test.Code, IsRequired = d.IsRequired, IsSelected = sel, IsAutoAdded = auto.Contains(d.Test.Code),
                    SelectedBy = sel ? item.SelectionSource : null, SelectedOn = sel ? now : null
                });
                if (sel) pairs.Add((d.Test, map.Profile, item));
            }
            if (auto.Count > 0) warnings.Add($"{code}: додано автоматично складові розрахункових показників — {string.Join(", ", auto)}");
        }

        // Дублювання (FR-MIS-029): той самий пацієнт і інтервенція за 24 год
        var since = now.AddHours(-24);
        var codesNow = items.Select(i => i.InterventionCode).ToList();
        var dup = await _db.OrderItems.AsNoTracking().Include(i => i.Order)
            .Where(i => i.Order!.PatientId == req.PatientId && i.Order.CreatedOn >= since && i.Order.Status != OrderStatuses.Cancelled && codesNow.Contains(i.InterventionCode!) && i.Status != "CANCELLED")
            .Select(i => new { i.InterventionCode, i.Order!.OrderNumber }).ToListAsync();
        foreach (var d in dup) warnings.Add($"{d.InterventionCode}: ідентична позиція вже є в замовленні {d.OrderNumber} за останні 24 год");

        var doctorId = !string.IsNullOrWhiteSpace(req.DoctorId) && await _db.Employees.AnyAsync(e => e.Id == req.DoctorId) ? req.DoctorId : null;
        var create = new CreateOrderRequest
        {
            PatientId = req.PatientId, DoctorId = doctorId, IsUrgentCito = cito, ClinicalNotes = req.ClinicalNotes, Icd10Code = req.Icd10,
            ReferralType = source == ReferralSources.Ehealth && req.EhealthReferralNumber != null ? ReferralTypes.Ehealth : source == ReferralSources.Partner ? ReferralTypes.ExternalClinic : ReferralTypes.Internal,
            EhealthReferralNumber = req.EhealthReferralNumber, ReferrerOrganizationName = source == ReferralSources.Partner ? req.PartnerName ?? req.Department : null,
            ReferrerDoctorName = req.DoctorName, ReferrerNumber = req.ReferralNumber, OrderDatetime = req.ReceivedAt
        };
        var selectedTests = await _tubes.SelectAsync(pairs.Select(p => (p.Test, p.Profile)), new HashSet<string>());
        var itemByTest = pairs.GroupBy(p => p.Test.Id).ToDictionary(g => g.Key, g => g.First().Item.Id);
        var needsSelection = items.Any(i => i.Status == "NEEDS_SELECTION");
        var propose = settings.RequireDistributionConfirmation && !cito;
        var order = await _orders.BuildOrderAsync(create, null, new OrderService.BuildOptions
        {
            Preselected = selectedTests, ItemOf = s => itemByTest.GetValueOrDefault(s.Test.Id), Propose = propose,
            Configure = o =>
            {
                o.ReferralContext = req.Context; o.MisReferralId = req.MisReferralId ?? Guid.NewGuid().ToString(); o.MisReferralNumber = req.ReferralNumber;
                o.MisHospitalizationId = req.HospitalizationId; o.RequestingDepartmentName = req.Department; o.Ward = req.Ward; o.Room = req.Room; o.Bed = req.Bed;
                o.PlannedCollectionAt = req.PlannedCollectionAt; o.PackageCode = req.Package?.Code; o.PackageName = req.Package?.Name; o.PackageChangedByDoctor = req.Package?.ChangedByDoctor ?? false;
                if (source == ReferralSources.Polyclinic && o.ReferralContext == ReferralContexts.Ambulatory) o.ReferralContext = ReferralContexts.Ambulatory;
                o.DistributionStatus = needsSelection ? "NEEDS_SELECTION" : propose ? "PROPOSED" : "CONFIRMED";
                if (!propose && !needsSelection) { o.DistributedAt = now; }
                foreach (var i in items) { i.OrderId = o.Id; o.Items.Add(i); }
            }
        });
        foreach (var t in order.Tests)
        {
            var ind = order.Items.SelectMany(i => i.Indicators).FirstOrDefault(x => x.TestId == t.TestId && x.IsSelected);
            if (ind != null) ind.OrderTestId = t.Id;
        }
        var noEq = order.Tests.Where(t => t.RoutingStatus == RoutingStatuses.NoEquipment).Select(t => t.TestCode).ToList();
        if (noEq.Count > 0) warnings.Add($"Без обладнання: {string.Join(", ", noEq)} — send-out, ручне виконання або відхилення з причиною (FR-PKG-006)");
        if (!propose && !needsSelection) Emit(order, MisEvents.Received, new { order.OrderNumber });
        _audit.Log("MIS_REFERRAL", "lab_order", order.Id, null, new { req.MisReferralId, req.Context, source, req.Package?.Code, items = req.Items.Select(i => i.InterventionCode) });
        await _db.SaveChangesAsync();
        return await GetAsync(order.Id, warnings);
    }

    private void Emit(LabOrder order, string ev, object? payload) => _db.MisEvents.Add(new LabMisEventLog
    {
        OrderId = order.Id, MisReferralId = order.MisReferralId, Event = ev, Payload = payload == null ? null : JsonSerializer.Serialize(payload, JsonDefaults.Options), Status = "DELIVERED", Attempts = 1
    });

    /// <summary>Подія для МІС із замовлення (виклик з інших сервісів, без збереження).</summary>
    public static void EmitFor(LisDbContext db, LabOrder order, string ev, object? payload)
    {
        if (order.MisReferralId == null) return;
        db.MisEvents.Add(new LabMisEventLog { OrderId = order.Id, MisReferralId = order.MisReferralId, Event = ev, Payload = payload == null ? null : JsonSerializer.Serialize(payload, JsonDefaults.Options) });
    }

    private async Task<LabOrder> LoadAsync(string idOrMisId)
    {
        var id = await _db.Orders.AsNoTracking().Where(o => o.Id == idOrMisId || o.MisReferralId == idOrMisId || o.OrderNumber == idOrMisId || o.MisReferralNumber == idOrMisId).OrderByDescending(o => o.CreatedOn).Select(o => o.Id).FirstOrDefaultAsync()
                 ?? throw NotFoundException.For("Направлення МІС", idOrMisId);
        return await _orders.LoadAsync(id);
    }

    public async Task<object> GetAsync(string idOrMisId, List<string>? warnings = null)
    {
        var o = await LoadAsync(idOrMisId);
        var positions = await _routing.PositionsAsync(o.Tests);
        return new
        {
            orderId = o.Id, o.OrderNumber, o.Status, o.MisReferralId, referralNumber = o.MisReferralNumber, context = o.ReferralContext, source = ReferralSources.Of(o),
            o.DistributionStatus, o.IsUrgentCito, o.PackageCode, o.PackageName, o.PackageChangedByDoctor, department = o.RequestingDepartmentName, o.Ward, o.Room, o.Bed, o.PlannedCollectionAt,
            misStatus = o.Status == OrderStatuses.Released ? "Виконано" : o.Status == OrderStatuses.Cancelled ? "Скасовано" : "В роботі",
            items = o.Items.OrderBy(i => i.DisplayOrder).Select(i => new
            {
                itemId = i.Id, i.InterventionCode, i.InterventionName, i.ProfileId, profile = o.Tests.FirstOrDefault(t => t.ProfileId == i.ProfileId && i.ProfileId != null)?.Profile?.Name,
                i.Status, i.SelectionSource, i.SelectionMode, i.PackageItemNo,
                tests = o.Tests.Where(t => t.OrderItemId == i.Id).OrderBy(t => t.DisplayOrder).Select(t => new
                {
                    orderTestId = t.Id, code = t.TestCode, name = t.TestName, t.Status, analyzerId = t.AssignedAnalyzerId, analyzer = t.AssignedAnalyzer?.Name,
                    queuePosition = positions.TryGetValue(t.Id, out var p) ? p : (int?)null, routedBy = t.RoutedBy, routingStatus = t.RoutingStatus
                }),
                notOrdered = i.Indicators.Where(x => !x.IsSelected).Select(x => x.TestCode),
                autoAdded = i.Indicators.Where(x => x.IsAutoAdded).Select(x => x.TestCode)
            }),
            warnings = warnings ?? new List<string>(),
            events = await _db.MisEvents.AsNoTracking().Where(e => e.OrderId == o.Id).OrderBy(e => e.At).Select(e => new { e.Event, e.At, e.Status }).ToListAsync()
        };
    }

    /// <summary>Перелік направлень МІС (емулятор МІС, журнал): останні N.</summary>
    public async Task<object> ListAsync(int take = 100)
    {
        var rows = await _db.Orders.AsNoTracking().Include(o => o.Patient).Include(o => o.Items)
            .Where(o => o.MisReferralId != null).OrderByDescending(o => o.CreatedOn).Take(Math.Clamp(take, 1, 500)).ToListAsync();
        return rows.Select(o => new
        {
            orderId = o.Id, o.OrderNumber, o.MisReferralId, referralNumber = o.MisReferralNumber, o.CreatedOn, patientName = o.Patient?.Caption, context = o.ReferralContext,
            source = ReferralSources.Of(o), o.PackageCode, o.Status, o.DistributionStatus, department = o.RequestingDepartmentName,
            misStatus = o.Status == OrderStatuses.Released ? "Виконано" : o.Status == OrderStatuses.Cancelled ? "Скасовано" : "В роботі",
            items = o.Items.OrderBy(i => i.DisplayOrder).Select(i => i.InterventionCode)
        });
    }

    /// <summary>Скасування в МІС (FR-MIS-025): якщо хоча б один тест у роботі — 409 зі списком, нічого не скасовується.</summary>
    public async Task<object> CancelAsync(string idOrMisId, string? reason)
    {
        var o = await LoadAsync(idOrMisId);
        var busy = o.Tests.Where(t => t.Status != OrderTestStatuses.Pending && t.Status != OrderTestStatuses.Rejected || t.Result != null).Select(t => $"{t.TestCode} ({t.Status})").ToList();
        if (busy.Count > 0)
            throw new DomainProblemException(409, "referral-in-progress", "Направлення вже в роботі",
                $"Скасування неможливе: тести вже в роботі — {string.Join(", ", busy)}. Рішення приймає лікар-лаборант", new Dictionary<string, string[]> { ["tests"] = busy.ToArray() });
        await _orders.CancelAsync(o.Id, string.IsNullOrWhiteSpace(reason) ? "Скасовано в МІС" : reason);
        o = await _orders.LoadAsync(o.Id);
        foreach (var i in o.Items) i.Status = "CANCELLED";
        Emit(o, MisEvents.Cancelled, new { reason });
        await _db.SaveChangesAsync();
        return await GetAsync(o.Id);
    }

    /// <summary>Зміна складу показників позиції (FR-MIS-035, FR-SVA-015) і вибір для «потрібен вибір» (FR-MIS-022).</summary>
    public async Task<object> SetIndicatorsAsync(string idOrMisId, string itemId, ItemIndicatorsRequest req)
    {
        var o = await LoadAsync(idOrMisId);
        var item = o.Items.FirstOrDefault(i => i.Id == itemId || i.InterventionCode == itemId) ?? throw NotFoundException.For("Позиція направлення", itemId);
        var map = await _db.InterventionMaps.AsNoTracking().Include(m => m.Profile).FirstOrDefaultAsync(m => m.InterventionId == item.InterventionId && m.IsActive)
                  ?? throw Problem("intervention-unmapped", $"{item.InterventionCode}: інтервенцію не співставлено");
        var defs = await _catalog.IndicatorsAsync(map);
        var (selected, auto) = Validate(map, defs, req.Indicators, item.InterventionCode ?? "");
        var by = (req.SelectedBy ?? (item.Status == "NEEDS_SELECTION" ? SelectionSources.Registrar : SelectionSources.LabDoctor)).ToUpperInvariant();
        var wasNeedsSelection = item.Status == "NEEDS_SELECTION";
        var now = DateTime.UtcNow;
        var current = o.Tests.Where(t => t.OrderItemId == item.Id && t.Status != OrderTestStatuses.Rejected).ToList();
        var removed = current.Where(t => !selected.Any(s => s.Test.Id == t.TestId)).ToList();
        var blocked = removed.Where(t => t.Status != OrderTestStatuses.Pending || t.Result != null).Select(t => t.TestCode).ToList();
        if (blocked.Count > 0) throw new ConflictException($"Зняти показники {string.Join(", ", blocked)} неможливо — вони вже в аналізі (FR-MIS-035)");
        foreach (var t in removed) { t.Status = OrderTestStatuses.Rejected; t.RejectReason = "Знято лікарем"; }
        var toAdd = selected.Where(s => !current.Any(t => t.TestId == s.Test.Id)).ToList();
        if (!wasNeedsSelection && toAdd.Count > 0 && by != SelectionSources.ReferringDoctor && string.IsNullOrWhiteSpace(req.Reason))
            throw ValidationException.Field("reason", "Вкажіть причину дозамовлення показника (FR-SVA-015)");
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();
        var propose = settings.RequireDistributionConfirmation && !o.IsUrgentCito && o.DistributionStatus != "CONFIRMED";
        var added = await _orders.AddPreselectedAsync(o, toAdd.Select(s => (s.Test, map.Profile)).ToList(), _ => item.Id, propose);
        foreach (var d in defs)
        {
            var ind = item.Indicators.FirstOrDefault(x => x.TestId == d.Test.Id);
            if (ind == null) { ind = new LabOrderItemIndicator { ItemId = item.Id, TestId = d.Test.Id, TestCode = d.Test.Code, IsRequired = d.IsRequired }; item.Indicators.Add(ind); _db.OrderItemIndicators.Add(ind); }
            var sel = selected.Contains(d);
            if (sel && !ind.IsSelected) { ind.SelectedBy = by; ind.SelectedOn = now; }
            ind.IsSelected = sel; ind.IsAutoAdded = auto.Contains(d.Test.Code);
            ind.OrderTestId = sel ? added.Concat(current).FirstOrDefault(t => t.TestId == d.Test.Id)?.Id : null;
        }
        item.Status = "ACTIVE";
        if (wasNeedsSelection) item.SelectionSource = by;
        if (o.DistributionStatus == "NEEDS_SELECTION" && !o.Items.Any(i => i.Status == "NEEDS_SELECTION")) o.DistributionStatus = propose ? "PROPOSED" : "CONFIRMED";
        Emit(o, MisEvents.ItemIndicatorsChanged, new { item.InterventionCode, added = toAdd.Select(s => s.Test.Code), removed = removed.Select(t => t.TestCode), by, req.Reason });
        _audit.Log("ITEM_INDICATORS", "lab_order_item", item.Id, current.Select(t => t.TestCode), selected.Select(s => s.Test.Code), req.Reason);
        await _db.SaveChangesAsync();
        return await GetAsync(o.Id);
    }

    // ------------------------------------------------------------------ розподіл по аналізаторах (23.4)
    private static string StateLabel(LabOrder o)
    {
        if (o.DistributionStatus == "NEEDS_SELECTION") return "Потрібен вибір показників";
        var active = o.Tests.Where(t => t.Status != OrderTestStatuses.Rejected).ToList();
        if (active.Any(t => t.RoutingStatus == RoutingStatuses.NoEquipment)) return "Є показники без обладнання";
        if (o.DistributionStatus == "PROPOSED") return $"Запропоновано: {active.Where(t => t.AssignedAnalyzerId != null).Select(t => t.AssignedAnalyzerId).Distinct().Count()} аналізатор(и)";
        return "Підтверджено";
    }

    public async Task<object> InboxAsync(string? state, string? source)
    {
        var q = _db.Orders.AsNoTracking().Include(o => o.Patient).Include(o => o.Items).Include(o => o.Tests)
            .Where(o => o.MisReferralId != null && o.Status != OrderStatuses.Cancelled && o.LabDepartment == null);
        if (state != "all") q = q.Where(o => PendingDistribution.Contains(o.DistributionStatus) || o.Tests.Any(t => t.RoutingStatus == RoutingStatuses.NoEquipment && t.Status == OrderTestStatuses.Pending) || o.CreatedOn >= DateTime.UtcNow.AddHours(-24));
        var rows = await q.OrderByDescending(o => o.IsUrgentCito).ThenByDescending(o => o.CreatedOn).Take(300).ToListAsync();
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();
        var list = rows.Select(o => new
        {
            orderId = o.Id, o.OrderNumber, referralNumber = o.MisReferralNumber ?? o.OrderNumber, receivedAt = o.CreatedOn, patientId = o.PatientId, patientName = o.Patient?.Caption,
            source = ReferralSources.Of(o), sourceLabel = ReferralSources.Label(ReferralSources.Of(o)), o.PackageCode, o.PackageName, department = o.RequestingDepartmentName, bed = RoutingService.Bed(o),
            isCito = o.IsUrgentCito, services = o.Items.OrderBy(i => i.DisplayOrder).Select(i => i.InterventionName), indicators = o.Tests.Count(t => t.Status != OrderTestStatuses.Rejected),
            o.DistributionStatus, state = StateLabel(o), pending = PendingDistribution.Contains(o.DistributionStatus),
            overdue = PendingDistribution.Contains(o.DistributionStatus) && (DateTime.UtcNow - o.CreatedOn).TotalMinutes > settings.DistributionTargetMinutes
        }).Where(r => string.IsNullOrWhiteSpace(source) || r.source == source.ToUpperInvariant()).ToList();
        return new { total = list.Count, pending = list.Count(r => r.pending), targetMinutes = settings.DistributionTargetMinutes, items = list };
    }

    /// <summary>Панель розподілу (FR-DST-003): по послугах — обрані показники, пропозиція, кандидати зі станом, черга й TAT, не обрані показники.</summary>
    public async Task<object> ProposalAsync(string orderId)
    {
        var o = await LoadAsync(orderId);
        var s = await _routing.SnapshotAsync();
        var settings = s.Settings;
        return new
        {
            orderId = o.Id, o.OrderNumber, referralNumber = o.MisReferralNumber, patientName = o.Patient?.Caption, source = ReferralSources.Of(o), o.PackageCode, o.PackageName, o.IsUrgentCito,
            department = o.RequestingDepartmentName, bed = RoutingService.Bed(o), o.DistributionStatus, state = StateLabel(o), settings.RequireDistributionConfirmation,
            items = o.Items.OrderBy(i => i.DisplayOrder).Select(i =>
            {
                var tests = o.Tests.Where(t => t.OrderItemId == i.Id && t.Status != OrderTestStatuses.Rejected).OrderBy(t => t.DisplayOrder).ToList();
                var split = tests.Where(t => t.AssignedAnalyzerId != null).Select(t => t.AssignedAnalyzerId).Distinct().Count() > 1;
                var allCands = tests.SelectMany(t => RoutingService.Candidates(s, t.TestCode, t.ProfileId)).GroupBy(c => c.AnalyzerId).ToList();
                return new
                {
                    itemId = i.Id, i.InterventionCode, i.InterventionName, i.Status, i.SelectionSource, split,
                    splitWarning = split ? "Показники послуги розподілено між приладами — потрібна аліквота або окрема пробірка" : null,
                    wholeServiceAnalyzers = allCands.Where(g => g.Count() == tests.Count && g.All(c => c.Available)).Select(g => new { analyzerId = g.Key, g.First().Name }),
                    tests = tests.Select(t => new
                    {
                        orderTestId = t.Id, code = t.TestCode, name = t.TestName, t.Status, isRequired = i.Indicators.Any(x => x.TestId == t.TestId && x.IsRequired),
                        analyzerId = t.AssignedAnalyzerId, analyzer = t.AssignedAnalyzerId == null ? null : s.Analyzers.GetValueOrDefault(t.AssignedAnalyzerId)?.Name,
                        t.RoutingStatus, t.RoutedBy, candidates = RoutingService.Candidates(s, t.TestCode, t.ProfileId)
                    }),
                    notOrdered = i.Indicators.Where(x => !x.IsSelected).Select(x => x.TestCode)
                };
            })
        };
    }

    public async Task<object> ChangeAsync(string orderId, List<DistributionChange> changes)
    {
        _policy.Require("Розподіл направлень", DistributorRoles);
        var o = await LoadAsync(orderId);
        foreach (var c in changes)
        {
            var t = c.OrderTestId != null ? o.Tests.FirstOrDefault(x => x.Id == c.OrderTestId)
                : o.Tests.FirstOrDefault(x => x.OrderItemId == c.ItemId && x.TestCode == c.TestCode && x.Status != OrderTestStatuses.Rejected);
            if (t == null) throw NotFoundException.For("Тест направлення", c.OrderTestId ?? c.TestCode);
            await _routing.ReassignAsync(t.Id, c.AnalyzerId, c.Reason ?? "Зміна приладу при розподілі", distribution: true);
            if (t.RoutingStatus == RoutingStatuses.NoEquipment) t.RoutingStatus = o.DistributionStatus == "CONFIRMED" ? RoutingStatuses.Routed : RoutingStatuses.Proposed;
        }
        await _db.SaveChangesAsync();
        return await ProposalAsync(o.Id);
    }

    /// <summary>«Авторозподіл» (FR-DST-005): перерахунок пропозиції за правилом, ручні зміни скидаються.</summary>
    public async Task<object> AutoAsync(string orderId)
    {
        _policy.Require("Розподіл направлень", DistributorRoles);
        var o = await LoadAsync(orderId);
        var tests = o.Tests.Where(t => (t.Status == OrderTestStatuses.Pending || t.Status == OrderTestStatuses.Rerun) && t.Result == null && t.PerformerId == null
                                       && t.RoutingStatus is RoutingStatuses.Proposed or RoutingStatuses.Routed or RoutingStatuses.NoEquipment).ToList();
        await _routing.RouteAsync(o, tests, o.DistributionStatus != "CONFIRMED");
        await _db.SaveChangesAsync();
        return await ProposalAsync(o.Id);
    }

    /// <summary>«Підтвердити й поставити в черги» (FR-DST-007, FR-DST-010).</summary>
    public async Task<object> ConfirmAsync(string orderId, DistributionConfirmRequest req)
    {
        _policy.Require("Розподіл направлень", DistributorRoles);
        var o = await LoadAsync(orderId);
        if (o.DistributionStatus == "NEEDS_SELECTION") throw new ConflictException("Спершу оберіть показники для позицій без переданого складу (FR-MIS-022)");
        var now = DateTime.UtcNow;
        foreach (var d in req.NoEquipment)
        {
            var t = o.Tests.FirstOrDefault(x => x.Id == d.OrderTestId) ?? throw NotFoundException.For("Тест направлення", d.OrderTestId);
            switch (d.Action.ToUpperInvariant())
            {
                case "SENDOUT":
                    var performerId = d.PerformerId ?? await _db.PerformerTests.AsNoTracking().Where(p => p.TestId == t.TestId && p.IsActive).Select(p => p.PerformerId).FirstOrDefaultAsync()
                                      ?? await _db.Performers.AsNoTracking().Where(p => p.IsActive && p.Kind == PerformerKinds.External).Select(p => p.Id).FirstOrDefaultAsync()
                                      ?? throw new ConflictException("Немає зовнішньої лабораторії для send-out");
                    t.PerformerId = performerId; t.RoutingStatus = RoutingStatuses.SendOut;
                    break;
                case "MANUAL":
                    var manual = await _db.Analyzers.AsNoTracking().Where(a => a.Kind == AnalyzerKinds.Manual && a.IsActive).Select(a => a.Id).FirstOrDefaultAsync();
                    t.AssignedAnalyzerId = manual; t.RoutingStatus = RoutingStatuses.Manual; t.RoutedBy = "MANUAL";
                    break;
                case "REJECT":
                    if (string.IsNullOrWhiteSpace(d.Reason)) throw ValidationException.Field("reason", $"Вкажіть причину відхилення показника {t.TestCode}");
                    t.Status = OrderTestStatuses.Rejected; t.RejectReason = d.Reason;
                    break;
                default: throw ValidationException.Field("action", "Дія: SENDOUT, MANUAL або REJECT");
            }
            _db.RoutingLog.Add(new LabRoutingLog { OrderTestId = t.Id, OrderId = o.Id, TestCode = t.TestCode, Event = RoutingEvents.Confirm, AnalyzerId = t.AssignedAnalyzerId, RoutedBy = "MANUAL", Rule = d.Action.ToUpperInvariant(), Reason = d.Reason, ActorId = _current.EmployeeId });
        }
        foreach (var t in o.Tests.Where(t => t.RoutingStatus == RoutingStatuses.Proposed))
        {
            t.RoutingStatus = RoutingStatuses.Routed; t.EnqueuedOn = now; t.DistributedById = _current.EmployeeId; t.DistributedAt = now;
            _db.RoutingLog.Add(new LabRoutingLog { OrderTestId = t.Id, OrderId = o.Id, TestCode = t.TestCode, Event = RoutingEvents.Confirm, AnalyzerId = t.AssignedAnalyzerId, RoutedBy = t.RoutedBy ?? "AUTO", ActorId = _current.EmployeeId, At = now });
        }
        var pendingNoEq = o.Tests.Where(t => t.RoutingStatus == RoutingStatuses.NoEquipment && t.Status != OrderTestStatuses.Rejected).Select(t => t.TestCode).ToList();
        o.DistributionStatus = "CONFIRMED"; o.DistributedById = _current.EmployeeId; o.DistributedAt = now;
        Emit(o, MisEvents.Received, new { o.OrderNumber, noEquipment = pendingNoEq });
        _audit.Log("DISTRIBUTION_CONFIRM", "lab_order", o.Id, null, new { decisions = req.NoEquipment, pendingNoEq });
        await _db.SaveChangesAsync();
        var res = await ProposalAsync(o.Id);
        return new { proposal = res, warnings = pendingNoEq.Count == 0 ? new List<string>() : new List<string> { $"Без обладнання (рішення не прийнято): {string.Join(", ", pendingNoEq)}" } };
    }
}
