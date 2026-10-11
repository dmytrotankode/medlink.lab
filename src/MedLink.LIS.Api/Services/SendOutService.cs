// =============================================================================
// Направлення у зовнішні лабораторії (send-out) — процес без електронної інтеграції:
// маршрутизація тесту до виконавця → черга «до відправки» → реєстр (накладна) → відправка →
// підтвердження приймання → результати (ручний ввід / файл CSV·XLSX·XML / PDF-бланк) →
// верифікація нашим лікарем (автоверифікація вимкнена) → видача з позначкою виконавця.
// Провайдери, інтегровані в MedLink (TerraLab), обслуговує evomis; ЛІС не пише в ter_*.
// =============================================================================
using System.Security.Cryptography;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class PerformerTestRequest
{
    public string TestId { get; set; } = "";
    public string? ExternalCode { get; set; }
    public string? ExternalName { get; set; }
    public decimal? Cost { get; set; }
    public int? TatHours { get; set; }
    public bool IsDefaultRoute { get; set; }
    public string? SpecimenRequirements { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class RouteTestRequest
{
    /// <summary>null — повернути у власну лабораторію.</summary>
    public string? PerformerId { get; set; }
    public string? Reason { get; set; }
}

public sealed class CreateSendOutRequest
{
    public string PerformerId { get; set; } = "";
    public List<string> OrderTestIds { get; set; } = new();
    public string? CourierName { get; set; }
    public string? Notes { get; set; }
}

public sealed class DispatchSendOutRequest
{
    public string? CourierName { get; set; }
    public double? Temperature { get; set; }
    public string? ExternalBatchNumber { get; set; }
}

public sealed class AcceptSendOutRequest
{
    public string? ExternalBatchNumber { get; set; }
    /// <summary>Номери замовлень у зовнішній лабораторії за позиціями (необов'язково).</summary>
    public Dictionary<string, string> ExternalOrderNumbers { get; set; } = new();
}

public sealed class SendOutResultRequest
{
    public double? NumericValue { get; set; }
    public string? StringValue { get; set; }
    public string? Unit { get; set; }
    public string? ReportText { get; set; }
    public string? Comment { get; set; }
    public string? ExternalReference { get; set; }
    /// <summary>Референс зовнішньої лабораторії (як у її бланку).</summary>
    public string? ReferenceText { get; set; }
    public DateTime? MeasuredAt { get; set; }
}

public sealed class SendOutItemRejectRequest
{
    public string Reason { get; set; } = "";
    /// <summary>true — тест повертається в чергу (PENDING) для повторної відправки/виконання; false — тест відхиляється.</summary>
    public bool ReturnToQueue { get; set; } = true;
}

public sealed class SendOutService
{
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;
    private readonly INumeratorService _numerators;
    private readonly ResultPipelineService _pipeline;
    private readonly OrderStateService _state;

    public static readonly string[] Operators = { LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician };
    public const string SendOutNumerator = "lab_send_out_number";

    public SendOutService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current, INumeratorService numerators, ResultPipelineService pipeline, OrderStateService state)
    {
        _db = db; _policy = policy; _audit = audit; _current = current; _numerators = numerators; _pipeline = pipeline; _state = state;
    }

    // ------------------------------------------------------------------ виконавці
    public async Task<List<object>> PerformersAsync(bool includeInactive) =>
        (await _db.Performers.AsNoTracking().Include(p => p.Tests).ThenInclude(t => t.Test)
            .Where(p => p.RecordState != RecordStates.Deleted && (includeInactive || p.IsActive)).OrderBy(p => p.Kind).ThenBy(p => p.Name).ToListAsync())
        .Select(ToDto).ToList();

    public async Task<object> PerformerAsync(string id) => ToDto(await LoadPerformerAsync(id, tracking: false));

    private async Task<LabPerformer> LoadPerformerAsync(string id, bool tracking = true)
    {
        var q = _db.Performers.Include(p => p.Tests).ThenInclude(t => t.Test).Where(p => (p.Id == id || p.Code == id) && p.RecordState != RecordStates.Deleted);
        return await (tracking ? q : q.AsNoTracking()).FirstOrDefaultAsync() ?? throw NotFoundException.For("Лабораторія-виконавець", id);
    }

    public async Task<object> SavePerformerAsync(string? id, LabPerformer req)
    {
        _policy.Require("Довідник лабораторій-виконавців", LabRoles.Admin, LabRoles.Doctor);
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Код і назва лабораторії обов'язкові");
        if (req.Kind is not (PerformerKinds.Internal or PerformerKinds.External)) throw ValidationException.Field("kind", "INTERNAL або EXTERNAL");
        if (!ExchangeModes.All.Contains(req.ExchangeMode)) throw ValidationException.Field("exchangeMode", string.Join(", ", ExchangeModes.All));
        var code = req.Code.Trim().ToUpperInvariant();
        if (await _db.Performers.AnyAsync(p => p.Code == code && p.Id != id && p.RecordState != RecordStates.Deleted)) throw new ConflictException($"Лабораторія з кодом {code} вже існує");
        LabPerformer p;
        if (id == null) { p = new LabPerformer(); _db.Performers.Add(p); }
        else p = await LoadPerformerAsync(id);
        var before = id == null ? null : ToDto(p);
        p.Code = code; p.Name = req.Name.Trim(); p.Kind = req.Kind; p.ExchangeMode = req.ExchangeMode; p.MedlinkProvider = string.IsNullOrWhiteSpace(req.MedlinkProvider) ? null : req.MedlinkProvider.Trim().ToUpperInvariant();
        p.Edrpou = req.Edrpou; p.LicenseNumber = req.LicenseNumber; p.Address = req.Address; p.Phone = req.Phone; p.Email = req.Email;
        p.ContractNumber = req.ContractNumber; p.ContractDate = req.ContractDate; p.DefaultTatHours = req.DefaultTatHours > 0 ? req.DefaultTatHours : 72;
        p.ReportNote = req.ReportNote; p.DepartmentId = string.IsNullOrWhiteSpace(req.DepartmentId) ? null : req.DepartmentId; p.IsActive = req.IsActive;
        _audit.Log(id == null ? "CREATE" : "UPDATE", "lab_performer", p.Id, before, new { p.Code, p.Name, p.Kind, p.ExchangeMode, p.MedlinkProvider });
        await _db.SaveChangesAsync();
        return await PerformerAsync(p.Id);
    }

    public async Task<object> DeletePerformerAsync(string id)
    {
        _policy.Require("Видалення лабораторії-виконавця", LabRoles.Admin);
        var p = await LoadPerformerAsync(id);
        var used = await _db.OrderTests.AnyAsync(t => t.PerformerId == p.Id) || await _db.SendOuts.AnyAsync(s => s.PerformerId == p.Id);
        if (used) p.IsActive = false; else p.RecordState = RecordStates.Deleted;
        _audit.Log(used ? "SOFT_DELETE" : "DELETE", "lab_performer", p.Id, null, null);
        await _db.SaveChangesAsync();
        return new { id = p.Id, deleted = !used, deactivated = used };
    }

    /// <summary>Повна заміна переліку показників виконавця.</summary>
    public async Task<object> SetPerformerTestsAsync(string id, List<PerformerTestRequest> items)
    {
        _policy.Require("Прайс зовнішньої лабораторії", LabRoles.Admin, LabRoles.Doctor);
        var p = await LoadPerformerAsync(id);
        if (p.Kind == PerformerKinds.Internal && items.Count > 0) throw new ValidationException("Власна лабораторія виконує всі показники довідника — прайс не потрібен");
        foreach (var dup in items.GroupBy(i => i.TestId).Where(g => g.Count() > 1)) throw new ValidationException($"Показник {dup.Key} вказано двічі");
        var tests = await _db.Tests.Where(t => items.Select(i => i.TestId).Contains(t.Id) || items.Select(i => i.TestId).Contains(t.Code)).ToListAsync();
        var mapped = new List<LabPerformerTest>();
        foreach (var i in items)
        {
            var t = tests.FirstOrDefault(x => x.Id == i.TestId || x.Code == i.TestId) ?? throw NotFoundException.For("Показник", i.TestId);
            if (i.IsDefaultRoute && await _db.PerformerTests.AnyAsync(x => x.TestId == t.Id && x.PerformerId != p.Id && x.IsDefaultRoute && x.IsActive && x.Performer!.IsActive))
                throw new ConflictException($"Показник {t.Code} уже маршрутизовано за замовчуванням до іншої лабораторії");
            mapped.Add(new LabPerformerTest { PerformerId = p.Id, TestId = t.Id, ExternalCode = i.ExternalCode, ExternalName = i.ExternalName, Cost = i.Cost, TatHours = i.TatHours, IsDefaultRoute = i.IsDefaultRoute, SpecimenRequirements = i.SpecimenRequirements, IsActive = i.IsActive });
        }
        _db.PerformerTests.RemoveRange(p.Tests);
        _db.PerformerTests.AddRange(mapped);
        _audit.Log("UPDATE_TESTS", "lab_performer", p.Id, null, mapped.Select(m => new { m.TestId, m.ExternalCode, m.IsDefaultRoute }));
        await _db.SaveChangesAsync();
        return await PerformerAsync(p.Id);
    }

    /// <summary>Маршрути за замовчуванням: testId → performerId (активні зовнішні виконавці).</summary>
    public async Task<Dictionary<string, (string PerformerId, string PerformerCode)>> DefaultRoutesAsync(IEnumerable<string> testIds)
    {
        var ids = testIds.Distinct().ToList();
        return await _db.PerformerTests.AsNoTracking().Include(x => x.Performer)
            .Where(x => ids.Contains(x.TestId) && x.IsDefaultRoute && x.IsActive && x.Performer!.IsActive && x.Performer.Kind == PerformerKinds.External && x.Performer.RecordState != RecordStates.Deleted)
            .ToDictionaryAsync(x => x.TestId, x => (x.PerformerId, x.Performer!.Code));
    }

    // ------------------------------------------------------------------ маршрутизація тесту замовлення
    public async Task<object> RouteTestAsync(string orderTestId, RouteTestRequest req)
    {
        _policy.Require("Зміна виконавця дослідження", Operators.Append(LabRoles.Registrar).ToArray());
        var t = await _db.OrderTests.Include(x => x.Order).Include(x => x.Test).FirstOrDefaultAsync(x => x.Id == orderTestId) ?? throw NotFoundException.For("Тест замовлення", orderTestId);
        if (t.Status is not (OrderTestStatuses.Pending or OrderTestStatuses.Rerun))
            throw new ConflictException($"Тест {t.TestCode} у статусі {t.Status}: змінити виконавця можна лише до відправки/виконання");
        if (await _db.SendOutItems.AnyAsync(i => i.OrderTestId == t.Id && SendOutItemStatuses.Open.Contains(i.Status)))
            throw new ConflictException($"Тест {t.TestCode} уже включено до реєстру відправки — спершу відкличте його");
        string? performerId = null;
        if (!string.IsNullOrWhiteSpace(req.PerformerId))
        {
            var p = await LoadPerformerAsync(req.PerformerId, tracking: false);
            if (!p.IsActive) throw new ConflictException($"Лабораторія {p.Name} неактивна");
            performerId = p.Kind == PerformerKinds.Internal ? null : p.Id;
            if (performerId != null && !p.Tests.Any(x => x.TestId == t.TestId && x.IsActive))
                throw new ConflictException($"Лабораторія {p.Name} не виконує показник {t.TestCode} (немає в її прайсі)");
        }
        var before = t.PerformerId;
        t.PerformerId = performerId;
        _audit.Log("ROUTE_TEST", "lab_order_test", t.Id, new { performerId = before }, new { performerId }, req.Reason);
        await _db.SaveChangesAsync();
        return new { t.Id, t.TestCode, t.PerformerId, t.Status };
    }

    // ------------------------------------------------------------------ черга до відправки
    /// <summary>Тести, маршрутизовані до зовнішніх лабораторій, з прийнятими/взятими пробами, ще не включені в реєстр.</summary>
    public async Task<List<object>> QueueAsync(string? performerId)
    {
        var inOpen = _db.SendOutItems.Where(i => SendOutItemStatuses.Open.Contains(i.Status)).Select(i => i.OrderTestId);
        var tests = await _db.OrderTests.AsNoTracking().Include(t => t.Order).ThenInclude(o => o!.Patient).Include(t => t.Sample).Include(t => t.Performer)
            .Where(t => t.PerformerId != null && (performerId == null || t.PerformerId == performerId)
                        && (t.Status == OrderTestStatuses.Pending || t.Status == OrderTestStatuses.Rerun)
                        && t.Order!.Status != OrderStatuses.Cancelled && !inOpen.Contains(t.Id))
            .OrderByDescending(t => t.Order!.IsUrgentCito).ThenBy(t => t.Order!.OrderDatetime).ToListAsync();
        var map = await _db.PerformerTests.AsNoTracking().Where(x => tests.Select(t => t.PerformerId).Contains(x.PerformerId)).ToListAsync();
        return tests.Select(t =>
        {
            var m = map.FirstOrDefault(x => x.PerformerId == t.PerformerId && x.TestId == t.TestId);
            var ready = SampleInLab(t.Sample);
            return (object)new
            {
                orderTestId = t.Id, t.TestCode, t.TestName, t.OrderId, orderNumber = t.Order!.OrderNumber, isCito = t.Order.IsUrgentCito, patientName = t.Order.Patient?.Caption,
                barcode = t.Sample?.Barcode, sampleStatus = t.Sample?.Status, sampleReady = ready, performerId = t.PerformerId, performerName = t.Performer?.Name,
                externalCode = m?.ExternalCode, externalName = m?.ExternalName, specimenRequirements = m?.SpecimenRequirements, tatHours = m?.TatHours ?? t.Performer?.DefaultTatHours
            };
        }).ToList();
    }

    /// <summary>Проба фізично в лабораторії (прийнята) — її можна пакувати для відправки.</summary>
    private static bool SampleInLab(LabOrderSample? s) => s != null && s.Status is SampleStatuses.Received or SampleStatuses.Processing or SampleStatuses.Stored;

    // ------------------------------------------------------------------ реєстри
    private IQueryable<LabSendOut> SendOutQuery() => _db.SendOuts.Include(s => s.Performer)
        .Include(s => s.Items).ThenInclude(i => i.OrderTest).ThenInclude(t => t!.Order).ThenInclude(o => o!.Patient)
        .Include(s => s.Items).ThenInclude(i => i.OrderTest).ThenInclude(t => t!.Result)
        .Include(s => s.Items).ThenInclude(i => i.Sample);

    public async Task<List<object>> ListAsync(string? status, string? performerId)
    {
        var q = SendOutQuery().AsNoTracking().Where(s => s.RecordState != RecordStates.Deleted);
        if (!string.IsNullOrWhiteSpace(status)) { var st = status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToUpperInvariant()).ToArray(); q = q.Where(s => st.Contains(s.Status)); }
        if (!string.IsNullOrWhiteSpace(performerId)) q = q.Where(s => s.PerformerId == performerId);
        return (await q.OrderByDescending(s => s.CreatedOn).ToListAsync()).Select(ToDto).ToList();
    }

    public async Task<object> GetAsync(string id) => ToDto(await LoadAsync(id, tracking: false));

    private async Task<LabSendOut> LoadAsync(string id, bool tracking = true)
    {
        var q = SendOutQuery().Where(s => (s.Id == id || s.Number == id) && s.RecordState != RecordStates.Deleted);
        return await (tracking ? q : q.AsNoTracking()).FirstOrDefaultAsync() ?? throw NotFoundException.For("Реєстр відправки", id);
    }

    public async Task<object> CreateAsync(CreateSendOutRequest req)
    {
        _policy.Require("Формування реєстру відправки", Operators);
        if (req.OrderTestIds.Count == 0) throw ValidationException.Field("orderTestIds", "Оберіть тести для відправки");
        var performer = await LoadPerformerAsync(req.PerformerId, tracking: false);
        if (performer.Kind != PerformerKinds.External) throw new ValidationException("Реєстр формується лише для зовнішньої лабораторії");
        var tests = await _db.OrderTests.Include(t => t.Sample).Include(t => t.Order).Where(t => req.OrderTestIds.Contains(t.Id)).ToListAsync();
        foreach (var id in req.OrderTestIds.Where(id => tests.All(t => t.Id != id))) throw NotFoundException.For("Тест замовлення", id);
        var open = await _db.SendOutItems.Where(i => req.OrderTestIds.Contains(i.OrderTestId) && SendOutItemStatuses.Open.Contains(i.Status)).Select(i => i.OrderTestId).ToListAsync();
        foreach (var t in tests)
        {
            if (open.Contains(t.Id)) throw new ConflictException($"Тест {t.TestCode} ({t.Order?.OrderNumber}) уже є в іншому реєстрі");
            if (t.PerformerId != performer.Id) throw new ConflictException($"Тест {t.TestCode} ({t.Order?.OrderNumber}) маршрутизовано до іншого виконавця");
            if (t.Status is not (OrderTestStatuses.Pending or OrderTestStatuses.Rerun)) throw new ConflictException($"Тест {t.TestCode} у статусі {t.Status}");
            if (!SampleInLab(t.Sample))
                throw new ConflictException($"Тест {t.TestCode} ({t.Order?.OrderNumber}): проба ще не прийнята в лабораторії (або відхилена)");
        }
        var now = DateTime.UtcNow;
        var number = await NextNumberAsync(now);
        var so = new LabSendOut { Number = number, PerformerId = performer.Id, CourierName = req.CourierName, Notes = req.Notes };
        foreach (var t in tests)
        {
            var m = performer.Tests.FirstOrDefault(x => x.TestId == t.TestId);
            so.Items.Add(new LabSendOutItem { SendOutId = so.Id, OrderTestId = t.Id, SampleId = t.SampleId, ExternalCode = m?.ExternalCode, Status = SendOutItemStatuses.Queued });
        }
        _db.SendOuts.Add(so);
        _audit.Log("CREATE", "lab_send_out", so.Id, null, new { so.Number, performer = performer.Code, tests = tests.Select(t => t.Id) });
        await _db.SaveChangesAsync();
        return await GetAsync(so.Id);
    }

    private async Task<string> NextNumberAsync(DateTime at)
    {
        var n = await _numerators.NextCounterAsync(SendOutNumerator + ":" + at.ToString("yyMM"));
        return $"SO-{at:yyMM}-{n:0000}";
    }

    public async Task<object> DispatchAsync(string id, DispatchSendOutRequest req)
    {
        _policy.Require("Відправка проб у зовнішню лабораторію", Operators.Append(LabRoles.Courier).ToArray());
        var so = await LoadAsync(id);
        if (so.Status != SendOutStatuses.Created) throw new ConflictException($"Реєстр {so.Number} у статусі {so.Status} — відправка неможлива");
        var active = so.Items.Where(i => i.Status == SendOutItemStatuses.Queued).ToList();
        if (active.Count == 0) throw new ConflictException("У реєстрі немає позицій для відправки");
        var now = DateTime.UtcNow;
        var tat = await _db.PerformerTests.AsNoTracking().Where(x => x.PerformerId == so.PerformerId).ToDictionaryAsync(x => x.TestId, x => x.TatHours);
        foreach (var i in active)
        {
            var t = i.OrderTest!;
            _policy.Ensure(LisEntities.OrderTest, TestActions.SendOut, t.Status, $"Тест {t.TestCode}");
            t.Status = OrderTestStatuses.SentOut;
            i.Status = SendOutItemStatuses.Sent;
            i.DueAt = now.AddHours((tat.TryGetValue(t.TestId, out var h) ? h : null) ?? so.Performer!.DefaultTatHours);
            _state.MarkInProgress(t.Order!);
        }
        so.Status = SendOutStatuses.Dispatched; so.DispatchedAt = now; so.DispatchedById = _current.EmployeeId;
        so.CourierName = req.CourierName ?? so.CourierName; so.TemperatureDispatch = req.Temperature; so.ExternalBatchNumber = req.ExternalBatchNumber ?? so.ExternalBatchNumber;
        _audit.Log("DISPATCH", "lab_send_out", so.Id, null, new { so.Number, items = active.Count, so.TemperatureDispatch, so.CourierName });
        await _db.SaveChangesAsync();
        return await GetAsync(so.Id);
    }

    public async Task<object> AcceptAsync(string id, AcceptSendOutRequest req)
    {
        _policy.Require("Підтвердження приймання зовнішньою лабораторією", Operators);
        var so = await LoadAsync(id);
        if (so.Status != SendOutStatuses.Dispatched) throw new ConflictException($"Реєстр {so.Number} у статусі {so.Status}");
        so.Status = SendOutStatuses.Accepted; so.AcceptedAt = DateTime.UtcNow; so.ExternalBatchNumber = req.ExternalBatchNumber ?? so.ExternalBatchNumber;
        foreach (var i in so.Items.Where(i => i.Status == SendOutItemStatuses.Sent))
        {
            i.Status = SendOutItemStatuses.Accepted;
            if (req.ExternalOrderNumbers.TryGetValue(i.Id, out var ext) || req.ExternalOrderNumbers.TryGetValue(i.OrderTestId, out ext)) i.ExternalOrderNumber = ext;
        }
        _audit.Log("ACCEPT", "lab_send_out", so.Id, null, new { so.ExternalBatchNumber });
        await _db.SaveChangesAsync();
        return await GetAsync(so.Id);
    }

    public async Task<object> CancelAsync(string id, string? reason)
    {
        _policy.Require("Скасування реєстру відправки", Operators);
        var so = await LoadAsync(id);
        if (so.Status != SendOutStatuses.Created) throw new ConflictException("Скасувати можна лише ще не відправлений реєстр; для відправленого відкличте позиції");
        so.Status = SendOutStatuses.Cancelled;
        foreach (var i in so.Items.Where(i => i.Status == SendOutItemStatuses.Queued)) i.Status = SendOutItemStatuses.Recalled;
        _audit.Log("CANCEL", "lab_send_out", so.Id, null, null, reason);
        await _db.SaveChangesAsync();
        return await GetAsync(so.Id);
    }

    /// <summary>Відкликати позицію (проба загублена/повернута) або зафіксувати відмову зовнішньої лабораторії.</summary>
    public async Task<object> RejectItemAsync(string itemId, SendOutItemRejectRequest req, bool recallOnly = false)
    {
        _policy.Require("Відмова/відкликання позиції реєстру", Operators);
        if (string.IsNullOrWhiteSpace(req.Reason)) throw ValidationException.Field("reason", "Вкажіть причину");
        var item = await _db.SendOutItems.Include(i => i.SendOut).Include(i => i.OrderTest).ThenInclude(t => t!.Order).FirstOrDefaultAsync(i => i.Id == itemId) ?? throw NotFoundException.For("Позиція реєстру", itemId);
        if (!SendOutItemStatuses.Open.Contains(item.Status)) throw new ConflictException($"Позиція у статусі {item.Status}");
        var t = item.OrderTest!;
        item.Status = recallOnly ? SendOutItemStatuses.Recalled : SendOutItemStatuses.Rejected;
        item.RejectReason = req.Reason;
        if (t.Status == OrderTestStatuses.SentOut)
        {
            if (req.ReturnToQueue) { _policy.Ensure(LisEntities.OrderTest, TestActions.RecallSendOut, t.Status, $"Тест {t.TestCode}"); t.Status = OrderTestStatuses.Pending; }
            else { _policy.Ensure(LisEntities.OrderTest, TestActions.Reject, t.Status, $"Тест {t.TestCode}"); t.Status = OrderTestStatuses.Rejected; t.RejectReason = "Зовнішня лабораторія: " + req.Reason; }
        }
        await RecomputeAsync(item.SendOutId);
        _state.RecomputeCompletion(t.Order!);
        _audit.Log(recallOnly ? "RECALL_ITEM" : "REJECT_ITEM", "lab_send_out_item", item.Id, null, new { t.TestCode, req.ReturnToQueue }, req.Reason);
        await _db.SaveChangesAsync();
        return await GetAsync(item.SendOutId);
    }

    // ------------------------------------------------------------------ результати
    public async Task<object> EnterResultAsync(string itemId, SendOutResultRequest req)
    {
        _policy.Require("Внесення результату зовнішньої лабораторії", Operators);
        var item = await _db.SendOutItems.Include(i => i.SendOut).FirstOrDefaultAsync(i => i.Id == itemId) ?? throw NotFoundException.For("Позиція реєстру", itemId);
        if (item.SendOut!.Status is SendOutStatuses.Created or SendOutStatuses.Cancelled) throw new ConflictException("Реєстр ще не відправлено або скасовано");
        if (item.Status is SendOutItemStatuses.Rejected or SendOutItemStatuses.Recalled or SendOutItemStatuses.Queued) throw new ConflictException($"Позиція у статусі {item.Status}");
        var row = await _pipeline.ApplyAsync(new ResultEntry
        {
            OrderTestId = item.OrderTestId, NumericValue = req.NumericValue, StringValue = req.StringValue, Unit = req.Unit, ReportText = req.ReportText,
            Comment = req.Comment, MeasuredAt = req.MeasuredAt, PerformerId = item.SendOut.PerformerId, ExternalReference = req.ExternalReference ?? item.ExternalOrderNumber, ReferenceText = req.ReferenceText
        });
        item.Status = SendOutItemStatuses.Resulted;
        item.ResultReceivedAt = DateTime.UtcNow;
        await RecomputeAsync(item.SendOutId);
        await _db.SaveChangesAsync();
        return row;
    }

    /// <summary>
    /// Імпорт файлу результатів зовнішньої лабораторії (CSV/XLSX/XML: barcode | номер у зовн. лабораторії; testCode | код у прайсі; value; unit).
    /// dryRun=true — лише зіставлення без запису.
    /// </summary>
    public async Task<object> ImportResultsAsync(string sendOutId, Stream stream, string fileName, bool dryRun)
    {
        _policy.Require("Імпорт результатів зовнішньої лабораторії", Operators);
        var so = await LoadAsync(sendOutId, tracking: false);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var rows = ext switch { ".xlsx" => ImportService.ParseXlsx(stream), ".xml" => ImportService.ParseXml(stream), _ => ImportService.ParseCsv(stream) };
        if (rows.Count == 0) throw new ValidationException("Файл не містить рядків (очікуються колонки barcode, testCode, value[, unit])");
        var applied = 0;
        foreach (var r in rows)
        {
            var item = so.Items.FirstOrDefault(i => SendOutItemStatuses.Open.Contains(i.Status)
                && (string.Equals(i.Sample?.Barcode, r.Barcode, StringComparison.OrdinalIgnoreCase) || (i.ExternalOrderNumber != null && string.Equals(i.ExternalOrderNumber, r.Barcode, StringComparison.OrdinalIgnoreCase)))
                && (string.Equals(i.OrderTest?.TestCode, r.TestCode, StringComparison.OrdinalIgnoreCase) || (i.ExternalCode != null && string.Equals(i.ExternalCode, r.TestCode, StringComparison.OrdinalIgnoreCase))));
            if (item == null) { r.Status = "ERROR"; r.Message = "Позицію реєстру не знайдено (штрихкод/номер + код тесту) або її вже закрито"; continue; }
            r.OrderTestId = item.OrderTestId; r.OrderNumber = item.OrderTest?.Order?.OrderNumber; r.PatientName = item.OrderTest?.Order?.Patient?.Caption;
            if (dryRun) { r.Status = "READY"; continue; }
            try
            {
                var num = double.TryParse(r.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (double?)null;
                await EnterResultAsync(item.Id, new SendOutResultRequest { NumericValue = num, StringValue = num == null ? r.Value : null, Unit = r.Unit, Comment = $"Імпорт файлу {fileName}" });
                r.Status = "APPLIED"; applied++;
            }
            catch (Exception ex) when (ex is ValidationException or ConflictException) { r.Status = "ERROR"; r.Message = ex.Message; }
        }
        return new { dryRun, total = rows.Count, matched = rows.Count(r => r.Status is "READY" or "APPLIED"), applied, errors = rows.Count(r => r.Status == "ERROR"), rows };
    }

    private async Task RecomputeAsync(string sendOutId)
    {
        var so = await _db.SendOuts.Include(s => s.Items).FirstAsync(s => s.Id == sendOutId);
        if (so.Status is SendOutStatuses.Created or SendOutStatuses.Cancelled) return;
        var live = so.Items.Where(i => i.Status != SendOutItemStatuses.Recalled && i.Status != SendOutItemStatuses.Rejected).ToList();
        var done = live.Count(i => i.Status == SendOutItemStatuses.Resulted);
        if (live.Count == 0 || done == live.Count) { so.Status = SendOutStatuses.Completed; so.CompletedAt ??= DateTime.UtcNow; }
        else if (done > 0) so.Status = SendOutStatuses.Partial;
    }

    // ------------------------------------------------------------------ вкладення (PDF-бланки)
    public async Task<object> AddAttachmentAsync(string orderId, Stream stream, string fileName, string contentType, string? sendOutId, bool visibleToPatient)
    {
        _policy.Require("Завантаження бланка результатів", Operators);
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId) ?? throw NotFoundException.For("Замовлення", orderId);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        if (ms.Length == 0) throw new ValidationException("Файл порожній");
        if (ms.Length > 20_000_000) throw new ValidationException("Файл більший за 20 МБ");
        var bytes = ms.ToArray();
        string? performerId = null;
        if (sendOutId != null) performerId = (await _db.SendOuts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sendOutId))?.PerformerId ?? throw NotFoundException.For("Реєстр відправки", sendOutId);
        var a = new LabOrderAttachment
        {
            OrderId = order.Id, SendOutId = sendOutId, PerformerId = performerId, FileName = Path.GetFileName(fileName), ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), Content = bytes, VisibleToPatient = visibleToPatient
        };
        _db.Attachments.Add(a);
        _audit.Log("ATTACH", "lab_order_attachment", a.Id, null, new { order.OrderNumber, a.FileName, a.SizeBytes, a.Sha256 });
        await _db.SaveChangesAsync();
        return AttachmentDto(a, null);
    }

    public async Task<List<object>> AttachmentsAsync(string orderId)
    {
        var performers = await _db.Performers.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name);
        return (await _db.Attachments.AsNoTracking().Where(a => a.OrderId == orderId && a.RecordState != RecordStates.Deleted).OrderBy(a => a.CreatedOn)
                .Select(a => new LabOrderAttachment { Id = a.Id, OrderId = a.OrderId, SendOutId = a.SendOutId, PerformerId = a.PerformerId, Kind = a.Kind, FileName = a.FileName, ContentType = a.ContentType, SizeBytes = a.SizeBytes, Sha256 = a.Sha256, VisibleToPatient = a.VisibleToPatient, CreatedOn = a.CreatedOn })
                .ToListAsync())
            .Select(a => AttachmentDto(a, a.PerformerId != null && performers.TryGetValue(a.PerformerId, out var n) ? n : null)).ToList();
    }

    public async Task<LabOrderAttachment> AttachmentAsync(string id) =>
        await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Файл", id);

    private static object AttachmentDto(LabOrderAttachment a, string? performerName) => new
    {
        a.Id, a.OrderId, a.SendOutId, a.PerformerId, performerName, a.Kind, a.FileName, a.ContentType, a.SizeBytes, a.Sha256, a.VisibleToPatient, uploadedAt = a.CreatedOn,
        downloadUrl = $"/api/v1/lab/attachments/{a.Id}/content"
    };

    // ------------------------------------------------------------------ DTO
    private static object ToDto(LabPerformer p) => new
    {
        p.Id, p.Code, p.Name, p.Kind, p.ExchangeMode, p.MedlinkProvider, p.Edrpou, p.LicenseNumber, p.Address, p.Phone, p.Email, p.ContractNumber, p.ContractDate,
        p.DefaultTatHours, p.ReportNote, p.DepartmentId, p.IsActive,
        tests = p.Tests.OrderBy(t => t.Test?.Code).Select(t => new { t.Id, t.TestId, testCode = t.Test?.Code, testName = t.Test?.Name, t.ExternalCode, t.ExternalName, t.Cost, t.TatHours, t.IsDefaultRoute, t.SpecimenRequirements, t.IsActive })
    };

    private static object ToDto(LabSendOut s)
    {
        var now = DateTime.UtcNow;
        return new
        {
            s.Id, s.Number, s.PerformerId, performerName = s.Performer?.Name, performerCode = s.Performer?.Code, exchangeMode = s.Performer?.ExchangeMode, s.Status, s.ExternalBatchNumber,
            s.CourierName, s.TemperatureDispatch, s.DispatchedAt, s.AcceptedAt, s.CompletedAt, s.Notes, createdAt = s.CreatedOn,
            itemsTotal = s.Items.Count, itemsResulted = s.Items.Count(i => i.Status == SendOutItemStatuses.Resulted),
            itemsOverdue = s.Items.Count(i => SendOutItemStatuses.Open.Contains(i.Status) && i.DueAt < now),
            items = s.Items.OrderBy(i => i.OrderTest?.Order?.OrderNumber).ThenBy(i => i.OrderTest?.TestCode).Select(i => new
            {
                i.Id, i.OrderTestId, i.SampleId, barcode = i.Sample?.Barcode, orderId = i.OrderTest?.OrderId, orderNumber = i.OrderTest?.Order?.OrderNumber,
                patientName = i.OrderTest?.Order?.Patient?.Caption, testCode = i.OrderTest?.TestCode, testName = i.OrderTest?.TestName, testStatus = i.OrderTest?.Status,
                i.ExternalCode, i.ExternalOrderNumber, i.Status, i.DueAt, isOverdue = SendOutItemStatuses.Open.Contains(i.Status) && i.DueAt < now,
                i.ResultReceivedAt, i.RejectReason,
                result = i.OrderTest?.Result == null ? null : new { i.OrderTest.Result.NumericValue, i.OrderTest.Result.StringValue, i.OrderTest.Result.Unit, i.OrderTest.Result.Flag, i.OrderTest.Result.ReferenceDisplay }
            })
        };
    }
}
