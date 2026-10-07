// Замовлення: створення з автопідбором пробірок, редагування, тести, скасування, видача, переходи
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class OrderService
{
    private readonly LisDbContext _db;
    private readonly INumeratorService _numerators;
    private readonly IAuditService _audit;
    private readonly IRolePolicy _policy;
    private readonly ICurrentEmployee _current;
    private readonly OrderStateService _state;
    private readonly TubePlanService _tubes;

    public OrderService(LisDbContext db, INumeratorService numerators, IAuditService audit, IRolePolicy policy, ICurrentEmployee current, OrderStateService state, TubePlanService tubes)
    {
        _db = db; _numerators = numerators; _audit = audit; _policy = policy; _current = current; _state = state; _tubes = tubes;
    }

    public static readonly string[] CreatorRoles = { LabRoles.Admin, LabRoles.Registrar, LabRoles.Doctor, LabRoles.Phlebotomist, LabRoles.Technician };

    // ------------------------------------------------------------------ queries
    public IQueryable<LabOrder> FullQuery() => _db.Orders
        .Include(o => o.Patient).Include(o => o.Doctor).Include(o => o.Department)
        .Include(o => o.Samples).ThenInclude(s => s.TubeType)
        .Include(o => o.Samples).ThenInclude(s => s.BiomaterialType)
        .Include(o => o.Tests).ThenInclude(t => t.Test).ThenInclude(d => d!.LabSection)
        .Include(o => o.Tests).ThenInclude(t => t.Profile)
        .Include(o => o.Tests).ThenInclude(t => t.Sample)
        .Include(o => o.Tests).ThenInclude(t => t.AssignedAnalyzer)
        .Include(o => o.Tests).ThenInclude(t => t.Result).ThenInclude(r => r!.Analyzer);

    public async Task<LabOrder> LoadAsync(string id)
    {
        return await FullQuery().FirstOrDefaultAsync(o => o.Id == id) ?? throw NotFoundException.For("Замовлення", id);
    }

    public async Task<OrderDto> GetAsync(string id) => DtoMapper.ToDto(await LoadAsync(id), _policy);

    public async Task<PagedResult<OrderListItemDto>> ListAsync(string? status, DateTime? from, DateTime? to, bool? cito, string? departmentId,
        string? patientId, string? search, PagingQuery paging)
    {
        var q = _db.Orders.AsNoTracking().Include(o => o.Patient).Include(o => o.Department).Include(o => o.Doctor).Where(o => !o.IsDeleted);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var statuses = status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => s.ToUpperInvariant()).ToArray();
            q = q.Where(o => statuses.Contains(o.Status));
        }
        if (from.HasValue) q = q.Where(o => o.OrderDatetime >= from.Value);
        if (to.HasValue) q = q.Where(o => o.OrderDatetime <= to.Value);
        if (cito.HasValue) q = q.Where(o => o.IsUrgentCito == cito.Value);
        if (!string.IsNullOrWhiteSpace(departmentId)) q = q.Where(o => o.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(patientId)) q = q.Where(o => o.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(o => o.OrderNumber.Contains(s) || o.Patient!.LastName.Contains(s) || o.Patient.FirstName.Contains(s)
                             || (o.Patient.Phone != null && o.Patient.Phone.Contains(s)) || (o.Patient.TaxId != null && o.Patient.TaxId.Contains(s))
                             || o.Samples.Any(sm => sm.Barcode == s));
        }
        q = paging.Sort?.ToLowerInvariant() switch
        {
            "ordernumber" => paging.Desc ? q.OrderByDescending(o => o.OrderNumber) : q.OrderBy(o => o.OrderNumber),
            "status" => paging.Desc ? q.OrderByDescending(o => o.Status) : q.OrderBy(o => o.Status),
            "patient" => paging.Desc ? q.OrderByDescending(o => o.Patient!.LastName) : q.OrderBy(o => o.Patient!.LastName),
            _ => q.OrderByDescending(o => o.IsUrgentCito).ThenByDescending(o => o.OrderDatetime)
        };

        var total = await q.CountAsync();
        var items = await q.Skip((paging.SafePage - 1) * paging.SafePageSize).Take(paging.SafePageSize)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.PatientId, PatientName = o.Patient!.LastName + " " + o.Patient.FirstName + " " + (o.Patient.SecondName ?? ""),
                o.Patient.BirthDate, o.Patient.Gender, o.OrderDatetime, o.Status, o.IsUrgentCito, DepartmentName = o.Department!.Name, DoctorName = o.Doctor!.FullName,
                TestsTotal = o.Tests.Count, TestsVerified = o.Tests.Count(t => t.Status == OrderTestStatuses.Verified || t.Status == OrderTestStatuses.AutoVerified),
                SamplesCount = o.Samples.Count, HasCritical = o.Tests.Any(t => t.Result != null && (t.Result.Flag == "CRIT_HIGH" || t.Result.Flag == "CRIT_LOW")),
                o.TotalPrice
            }).ToListAsync();

        return new PagedResult<OrderListItemDto>
        {
            Total = total, Page = paging.SafePage, PageSize = paging.SafePageSize,
            Items = items.Select(o => new OrderListItemDto
            {
                Id = o.Id, OrderNumber = o.OrderNumber, PatientId = o.PatientId, PatientName = o.PatientName.Trim(),
                PatientAgeGender = DtoMapper.AgeGender(new MisPatientCard { BirthDate = o.BirthDate, Gender = o.Gender }, o.OrderDatetime),
                OrderDatetime = o.OrderDatetime, Status = o.Status, IsUrgentCito = o.IsUrgentCito, DepartmentName = o.DepartmentName, DoctorName = o.DoctorName,
                TestsTotal = o.TestsTotal, TestsVerified = o.TestsVerified, SamplesCount = o.SamplesCount, HasCritical = o.HasCritical, TotalPrice = o.TotalPrice,
                AllowedActions = _policy.AllowedActions(LisEntities.Order, o.Status)
            }).ToList()
        };
    }

    public async Task<OrderDto?> GetByBarcodeAsync(string barcode)
    {
        var orderId = await _db.Samples.AsNoTracking().Where(s => s.Barcode == barcode).Select(s => s.OrderId).FirstOrDefaultAsync();
        return orderId == null ? null : await GetAsync(orderId);
    }

    public async Task<object> TransitionsAsync(string id)
    {
        var order = await LoadAsync(id);
        return new
        {
            orderId = order.Id, status = order.Status, role = _policy.CurrentRole,
            allowedActions = _policy.AllowedActions(LisEntities.Order, order.Status),
            samples = order.Samples.Select(s => new { s.Id, s.Barcode, s.Status, allowedActions = _policy.AllowedActions(LisEntities.Sample, s.Status) }),
            tests = order.Tests.Select(t => new { t.Id, t.TestCode, t.Status, allowedActions = _policy.AllowedActions(LisEntities.OrderTest, t.Status) }),
            stateMachine = LisStateMachine.Describe(LisEntities.Order)
        };
    }

    // ------------------------------------------------------------------ create
    public async Task<OrderDto> CreateAsync(CreateOrderRequest req)
    {
        _policy.Require("Створення замовлення", CreatorRoles);
        var order = await BuildOrderAsync(req);
        await _db.SaveChangesAsync();
        return await GetAsync(order.Id);
    }

    public async Task<List<OrderDto>> CreateBatchAsync(List<CreateOrderRequest> reqs)
    {
        _policy.Require("Створення замовлень", CreatorRoles);
        var ids = new List<string>();
        foreach (var r in reqs) ids.Add((await BuildOrderAsync(r)).Id);
        await _db.SaveChangesAsync();
        var result = new List<OrderDto>();
        foreach (var id in ids) result.Add(await GetAsync(id));
        return result;
    }

    internal async Task<LabOrder> BuildOrderAsync(CreateOrderRequest req, string? repeatOfOrderId = null)
    {
        // Пацієнт
        MisPatientCard patient;
        if (!string.IsNullOrWhiteSpace(req.PatientId))
            patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == req.PatientId) ?? throw NotFoundException.For("Пацієнт", req.PatientId);
        else if (req.NewPatient != null)
        {
            if (string.IsNullOrWhiteSpace(req.NewPatient.LastName) || string.IsNullOrWhiteSpace(req.NewPatient.FirstName))
                throw ValidationException.Field("newPatient", "Прізвище та ім'я пацієнта обов'язкові");
            patient = new MisPatientCard
            {
                LastName = req.NewPatient.LastName.Trim(), FirstName = req.NewPatient.FirstName.Trim(), SecondName = req.NewPatient.SecondName?.Trim(),
                BirthDate = req.NewPatient.BirthDate, Gender = NormalizeGender(req.NewPatient.Gender), Phone = req.NewPatient.Phone, Email = req.NewPatient.Email,
                TaxId = req.NewPatient.TaxId, Address = req.NewPatient.Address
            };
            patient.LastNameLatin = Core.Common.TransliterationKmu2010.ToLatin(patient.LastName);
            patient.FirstNameLatin = Core.Common.TransliterationKmu2010.ToLatin(patient.FirstName);
            _db.Patients.Add(patient);
            _audit.Log("CREATE", "mis_patient_card", patient.Id, null, patient);
        }
        else throw ValidationException.Field("patientId", "Вкажіть patientId або newPatient");

        if (req.ProfileIds.Count == 0 && req.TestIds.Count == 0)
            throw ValidationException.Field("testIds", "Замовлення має містити хоча б один тест або профіль");

        var at = req.OrderDatetime ?? DateTime.UtcNow;
        var order = new LabOrder
        {
            OrderNumber = await _numerators.NextOrderNumberAsync(at),
            PatientId = patient.Id,
            DoctorId = NullIfEmpty(req.DoctorId),
            DepartmentId = NullIfEmpty(req.DepartmentId) ?? _current.DepartmentId,
            OrderDatetime = at,
            Status = OrderStatuses.New,
            IsUrgentCito = req.IsUrgentCito,
            EhealthReferralId = NullIfEmpty(req.EhealthReferralId),
            ClinicalNotes = req.ClinicalNotes,
            IsPregnant = req.IsPregnant,
            PregnancyWeek = req.PregnancyWeek,
            MenstrualPhase = NullIfEmpty(req.MenstrualPhase),
            Icd10Code = NullIfEmpty(req.Icd10Code),
            CreatedById = _current.EmployeeId,
            RepeatOfOrderId = repeatOfOrderId
        };
        await ValidateRefsAsync(order);
        _db.Orders.Add(order);

        await AddTestsInternalAsync(order, req.ProfileIds, req.TestIds);
        _audit.Log("CREATE", "lab_order", order.Id, null, new { order.OrderNumber, order.PatientId, tests = order.Tests.Select(t => t.TestCode), samples = order.Samples.Select(s => s.Barcode) });
        return order;
    }

    private async Task ValidateRefsAsync(LabOrder order)
    {
        if (order.DoctorId != null && !await _db.Employees.AnyAsync(e => e.Id == order.DoctorId)) throw ValidationException.Field("doctorId", "Лікаря не знайдено");
        if (order.DepartmentId != null && !await _db.Departments.AnyAsync(d => d.Id == order.DepartmentId)) throw ValidationException.Field("departmentId", "Підрозділ не знайдено");
        if (order.EhealthReferralId != null && !await _db.Referrals.AnyAsync(r => r.Id == order.EhealthReferralId)) throw ValidationException.Field("ehealthReferralId", "Направлення eHealth не знайдено");
    }

    /// <summary>Додає тести/профілі до замовлення з плануванням пробірок за правилами тари (FR-PRE-004).</summary>
    private async Task AddTestsInternalAsync(LabOrder order, List<string> profileIds, List<string> testIds)
    {
        var existingCodes = new HashSet<string>(order.Tests.Select(t => t.TestCode));
        var (toAdd, profiles, singles) = await _tubes.ResolveAsync(profileIds, testIds, existingCodes);
        foreach (var p in profiles) order.TotalPrice += p.Price;
        foreach (var t in singles.Where(t => toAdd.Any(a => a.Test.Id == t.Id && a.Profile == null))) order.TotalPrice += t.Price;
        if (toAdd.Count == 0 && order.Tests.Count == 0) throw new ValidationException("Немає нових тестів для додавання");

        var plan = await _tubes.BuildAsync(toAdd, order);
        var sampleByTest = new Dictionary<string, string>();
        foreach (var tube in plan.Tubes.Where(t => t.NewTests.Count > 0))
        {
            var sample = tube.ExistingSampleId == null ? null : order.Samples.First(s => s.Id == tube.ExistingSampleId);
            if (sample == null)
            {
                sample = new LabOrderSample
                {
                    OrderId = order.Id, Barcode = await _numerators.NextTubeBarcodeAsync(), TubeTypeId = tube.TubeTypeId, BiomaterialTypeId = tube.BiomaterialTypeId,
                    GroupNumb = order.Samples.Count + 1, Status = SampleStatuses.Pending,
                    PlanReasons = tube.Reasons.Count == 0 ? null : string.Join(",", tube.Reasons)
                };
                order.Samples.Add(sample);
            }
            foreach (var t in tube.NewTests) sampleByTest[t.TestId] = sample.Id;
        }

        var displayOrder = order.Tests.Count == 0 ? 0 : order.Tests.Max(t => t.DisplayOrder);
        foreach (var sel in toAdd)
        {
            order.Tests.Add(new LabOrderTest
            {
                OrderId = order.Id, SampleId = sampleByTest[sel.Test.Id], ProfileId = sel.Profile?.Id, TestId = sel.Test.Id, TestCode = sel.Test.Code, TestName = sel.Test.Name,
                Status = OrderTestStatuses.Pending, DisplayOrder = ++displayOrder, Test = sel.Test
            });
        }
    }

    // ------------------------------------------------------------------ edit
    public async Task<OrderDto> UpdateAsync(string id, UpdateOrderRequest req)
    {
        var order = await LoadAsync(id);
        _policy.Ensure(LisEntities.Order, OrderActions.Edit, order.Status, $"Замовлення {order.OrderNumber}");
        var before = new { order.DoctorId, order.DepartmentId, order.IsUrgentCito, order.ClinicalNotes, order.IsPregnant, order.PregnancyWeek, order.MenstrualPhase, order.Icd10Code, order.EhealthReferralId };
        if (req.DoctorId != null) order.DoctorId = NullIfEmpty(req.DoctorId);
        if (req.DepartmentId != null) order.DepartmentId = NullIfEmpty(req.DepartmentId);
        if (req.IsUrgentCito.HasValue) order.IsUrgentCito = req.IsUrgentCito.Value;
        if (req.EhealthReferralId != null) order.EhealthReferralId = NullIfEmpty(req.EhealthReferralId);
        if (req.ClinicalNotes != null) order.ClinicalNotes = req.ClinicalNotes;
        if (req.IsPregnant.HasValue) order.IsPregnant = req.IsPregnant.Value;
        if (req.PregnancyWeek.HasValue) order.PregnancyWeek = req.PregnancyWeek;
        if (req.MenstrualPhase != null) order.MenstrualPhase = NullIfEmpty(req.MenstrualPhase);
        if (req.Icd10Code != null) order.Icd10Code = NullIfEmpty(req.Icd10Code);
        await ValidateRefsAsync(order);
        _audit.Log("UPDATE", "lab_order", order.Id, before, req);
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<OrderDto> AddTestsAsync(string id, AddTestsRequest req)
    {
        var order = await LoadAsync(id);
        _policy.Ensure(LisEntities.Order, OrderActions.AddTest, order.Status, $"Замовлення {order.OrderNumber}");
        var beforeCodes = order.Tests.Select(t => t.TestCode).ToList();
        await AddTestsInternalAsync(order, req.ProfileIds, req.TestIds);
        _state.RecomputeReopen(order);
        _audit.Log("ADD_TESTS", "lab_order", order.Id, beforeCodes, order.Tests.Select(t => t.TestCode));
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<OrderDto> RemoveTestAsync(string orderId, string orderTestId)
    {
        var order = await LoadAsync(orderId);
        var test = order.Tests.FirstOrDefault(t => t.Id == orderTestId) ?? throw NotFoundException.For("Тест замовлення", orderTestId);
        _policy.Ensure(LisEntities.Order, OrderActions.RemoveTest, order.Status, $"Замовлення {order.OrderNumber}");
        _policy.Ensure(LisEntities.OrderTest, TestActions.Remove, test.Status, $"Тест {test.TestCode}");
        order.Tests.Remove(test);
        _db.OrderTests.Remove(test);
        if (test.ProfileId == null) order.TotalPrice -= test.Test?.Price ?? 0;
        if (order.TotalPrice < 0) order.TotalPrice = 0;
        // Порожня неузята пробірка — прибрати
        var sample = order.Samples.FirstOrDefault(s => s.Id == test.SampleId);
        if (sample != null && sample.Status == SampleStatuses.Pending && !order.Tests.Any(t => t.SampleId == sample.Id))
        {
            order.Samples.Remove(sample);
            _db.Samples.Remove(sample);
        }
        if (order.Tests.Count == 0) throw new ConflictException("Неможливо видалити останній тест замовлення — скасуйте замовлення");
        _audit.Log("REMOVE_TEST", "lab_order", order.Id, new { test.TestCode }, null);
        await _db.SaveChangesAsync();
        return await GetAsync(orderId);
    }

    public async Task<OrderDto> AddSampleAsync(string id, AddSampleRequest req)
    {
        var order = await LoadAsync(id);
        _policy.Ensure(LisEntities.Order, OrderActions.AddSample, order.Status, $"Замовлення {order.OrderNumber}");
        if (!await _db.TubeTypes.AnyAsync(t => t.Id == req.TubeTypeId)) throw ValidationException.Field("tubeTypeId", "Тип пробірки не знайдено");
        if (!await _db.BiomaterialTypes.AnyAsync(b => b.Id == req.BiomaterialTypeId)) throw ValidationException.Field("biomaterialTypeId", "Біоматеріал не знайдено");
        var sample = new LabOrderSample
        {
            OrderId = order.Id, Barcode = await _numerators.NextTubeBarcodeAsync(), TubeTypeId = req.TubeTypeId, BiomaterialTypeId = req.BiomaterialTypeId,
            GroupNumb = order.Samples.Count + 1, Status = SampleStatuses.Pending
        };
        order.Samples.Add(sample);
        foreach (var tid in req.OrderTestIds)
        {
            var t = order.Tests.FirstOrDefault(x => x.Id == tid) ?? throw NotFoundException.For("Тест замовлення", tid);
            if (t.Status != OrderTestStatuses.Pending) throw new ConflictException($"Тест {t.TestCode} вже в роботі — перенести на іншу пробірку неможливо");
            t.SampleId = sample.Id;
        }
        _audit.Log("ADD_SAMPLE", "lab_order_sample", sample.Id, null, new { sample.Barcode, order.OrderNumber });
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    // ------------------------------------------------------------------ transitions
    public async Task<OrderDto> CancelAsync(string id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw ValidationException.Field("reason", "Вкажіть причину скасування");
        var order = await LoadAsync(id);
        var rule = _policy.Ensure(LisEntities.Order, OrderActions.Cancel, order.Status, $"Замовлення {order.OrderNumber}");
        var before = order.Status;
        order.Status = rule.ToStatus!;
        order.CancelReason = reason;
        foreach (var t in order.Tests.Where(t => !OrderTestStatuses.Final.Contains(t.Status))) { t.Status = OrderTestStatuses.Rejected; t.RejectReason = "Замовлення скасовано: " + reason; }
        foreach (var s in order.Samples.Where(s => s.Status == SampleStatuses.Pending || s.Status == SampleStatuses.Collected)) { s.Status = SampleStatuses.Rejected; s.RejectReason = "Замовлення скасовано"; }
        _audit.Log("CANCEL", "lab_order", order.Id, new { status = before }, new { status = order.Status, reason });
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<OrderDto> RejectAsync(string id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw ValidationException.Field("reason", "Вкажіть причину відхилення");
        var order = await LoadAsync(id);
        var rule = _policy.Ensure(LisEntities.Order, OrderActions.Reject, order.Status, $"Замовлення {order.OrderNumber}");
        var before = order.Status;
        order.Status = rule.ToStatus!;
        order.CancelReason = reason;
        foreach (var t in order.Tests.Where(t => !OrderTestStatuses.Final.Contains(t.Status))) { t.Status = OrderTestStatuses.Rejected; t.RejectReason = reason; }
        _audit.Log("REJECT", "lab_order", order.Id, new { status = before }, new { status = order.Status, reason });
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task DeleteAsync(string id)
    {
        var order = await LoadAsync(id);
        _policy.Ensure(LisEntities.Order, OrderActions.Delete, order.Status, $"Замовлення {order.OrderNumber}");
        _db.Orders.Remove(order); // каскад: проби, тести
        _audit.Log("DELETE", "lab_order", order.Id, new { order.OrderNumber, order.Status }, null);
        await _db.SaveChangesAsync();
    }

    public async Task<OrderDto> ReopenAsync(string id, string? comment)
    {
        var order = await LoadAsync(id);
        var rule = _policy.Ensure(LisEntities.Order, OrderActions.Reopen, order.Status, $"Замовлення {order.OrderNumber}");
        var before = order.Status;
        order.Status = rule.ToStatus!;
        order.CompletedAt = null;
        order.ReleasedAt = null;
        order.VerifyToken = null;
        _audit.Log("REOPEN", "lab_order", order.Id, new { status = before }, new { status = order.Status }, comment);
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    /// <summary>Видача: усі тести VERIFIED/AUTO_VERIFIED → RELEASED, mis_diagnostic_report (FINAL), сповіщення пацієнта (QUEUED).</summary>
    public async Task<OrderDto> ReleaseAsync(string id)
    {
        var order = await LoadAsync(id);
        // Якщо всі тести вже верифіковано, а статус ще IN_PROGRESS — довершуємо автоматично
        _state.RecomputeCompletion(order);
        var rule = _policy.Ensure(LisEntities.Order, OrderActions.Release, order.Status, $"Замовлення {order.OrderNumber}");
        var pending = order.Tests.Where(t => !OrderTestStatuses.Final.Contains(t.Status)).Select(t => t.TestCode).ToList();
        if (pending.Count > 0) throw new ConflictException($"Видача неможлива: не верифіковано тести {string.Join(", ", pending)}");
        if (!order.Tests.Any(t => OrderTestStatuses.VerifiedAny.Contains(t.Status))) throw new ConflictException("Видача неможлива: у замовленні немає верифікованих результатів");

        var now = DateTime.UtcNow;
        order.Status = rule.ToStatus!;
        order.ReleasedAt = now;
        order.ReleasedById = _current.EmployeeId;
        order.VerifyToken = _state.VerifyToken(order.Id, now);

        var performer = order.Tests.Select(t => t.Result?.VerifiedById).FirstOrDefault(v => v != null) ?? _current.EmployeeId;
        _db.DiagnosticReports.Add(new MisDiagnosticReport
        {
            ReportNumber = order.OrderNumber, PatientId = order.PatientId, ReferralId = order.EhealthReferralId, LabOrderId = order.Id,
            ServiceName = string.Join("; ", order.Tests.Select(t => t.Profile?.Name ?? t.TestName).Distinct()),
            PerformerDoctorId = performer, Status = "FINAL", EhealthSynced = false, SignedAt = now,
            Conclusions = $"Лабораторне дослідження №{order.OrderNumber}: {order.Tests.Count} показників, критичних: {order.Tests.Count(t => t.Result != null && Core.Clinical.ResultFlags.IsCritical(t.Result.Flag))}"
        });

        var channel = !string.IsNullOrWhiteSpace(order.Patient?.Email) ? "EMAIL" : "SMS";
        _db.Notifications.Add(new LabPatientNotification
        {
            PatientId = order.PatientId, OrderId = order.Id, Channel = channel, Status = "QUEUED",
            Payload = $"Результати дослідження №{order.OrderNumber} готові. Перегляд: /portal/{order.PatientId}/orders/{order.Id}"
        });
        _audit.Log("RELEASE", "lab_order", order.Id, new { status = OrderStatuses.Completed }, new { status = order.Status, order.ReleasedAt, order.VerifyToken });
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<object?> VerifyByTokenAsync(string token)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.VerifyToken == token && o.Status == OrderStatuses.Released);
        if (order == null) return null;
        var lab = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        return new { orderNumber = order.OrderNumber, releasedAt = order.ReleasedAt, lab = lab?.Name, valid = true };
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static string NormalizeGender(string? g) => (g ?? "").Trim().ToUpperInvariant() switch
    {
        "M" or "MALE" or "Ч" or "ЧОЛ" => "M",
        "F" or "FEMALE" or "Ж" or "ЖІН" => "F",
        _ => "U"
    };
}
