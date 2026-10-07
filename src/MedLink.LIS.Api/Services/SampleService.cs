// Пробірки: забір, прийом, відбраковка, редагування, заміна, видалення, етикетки
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Core.Barcodes;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class LabelDto
{
    public string Barcode { get; set; } = "";
    public string Zpl { get; set; } = "";
    public string Svg { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string Tube { get; set; } = "";
    public string? TubeColor { get; set; }
    public string OrderNumber { get; set; } = "";
    public DateTime? CollectedAt { get; set; }
    public List<string> TestCodes { get; set; } = new();
}

public sealed class SampleService
{
    private readonly LisDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRolePolicy _policy;
    private readonly ICurrentEmployee _current;
    private readonly OrderStateService _state;
    private readonly INumeratorService _numerators;
    private readonly OrderService _orders;
    private readonly SectionJournalService _journal;

    public SampleService(LisDbContext db, IAuditService audit, IRolePolicy policy, ICurrentEmployee current, OrderStateService state, INumeratorService numerators, OrderService orders, SectionJournalService journal)
    {
        _db = db; _audit = audit; _policy = policy; _current = current; _state = state; _numerators = numerators; _orders = orders; _journal = journal;
    }

    private IQueryable<LabOrderSample> Query() => _db.Samples
        .Include(s => s.TubeType).Include(s => s.BiomaterialType).Include(s => s.LabSection)
        .Include(s => s.Order).ThenInclude(o => o!.Patient)
        .Include(s => s.Order).ThenInclude(o => o!.Tests);

    public async Task<LabOrderSample> LoadByBarcodeAsync(string barcode)
    {
        var s = await Query().FirstOrDefaultAsync(x => x.Barcode == barcode) ?? throw NotFoundException.For("Пробірка зі штрихкодом", barcode);
        if (s.Order != null) await _db.Entry(s.Order).Collection(o => o.Samples).LoadAsync();
        return s;
    }

    public async Task<LabOrderSample> LoadAsync(string idOrBarcode)
    {
        var s = await Query().FirstOrDefaultAsync(x => x.Id == idOrBarcode || x.Barcode == idOrBarcode) ?? throw NotFoundException.For("Пробірка", idOrBarcode);
        if (s.Order != null) await _db.Entry(s.Order).Collection(o => o.Samples).LoadAsync();
        return s;
    }

    public SampleDto ToDto(LabOrderSample s) => DtoMapper.ToDto(s, s.Order?.Tests ?? new List<LabOrderTest>(), _policy);

    public async Task<PagedResult<SampleDto>> ListAsync(string? status, string? barcode, string? orderId, PagingQuery paging)
    {
        var q = Query().AsNoTracking().Where(s => !s.IsDeleted);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var statuses = status.ToUpperInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            q = q.Where(s => statuses.Contains(s.Status));
        }
        if (!string.IsNullOrWhiteSpace(barcode)) q = q.Where(s => s.Barcode.Contains(barcode));
        if (!string.IsNullOrWhiteSpace(orderId)) q = q.Where(s => s.OrderId == orderId);
        q = q.OrderByDescending(s => s.Order!.IsUrgentCito).ThenByDescending(s => s.CreatedOn);
        var total = await q.CountAsync();
        var items = await q.Skip((paging.SafePage - 1) * paging.SafePageSize).Take(paging.SafePageSize).ToListAsync();
        return new PagedResult<SampleDto> { Total = total, Page = paging.SafePage, PageSize = paging.SafePageSize, Items = items.Select(ToDto).ToList() };
    }

    public async Task<SampleDto> CollectAsync(string barcode, CollectSampleRequest req)
    {
        var sample = await LoadByBarcodeAsync(barcode);
        var rule = _policy.Ensure(LisEntities.Sample, SampleActions.Collect, sample.Status, $"Пробірка {barcode}");
        if (req.Checklist != null && !req.Checklist.IdVerified) throw new ValidationException("Чек-лист забору: особу пацієнта не підтверджено");
        var before = sample.Status;
        sample.Status = rule.ToStatus!;
        sample.CollectedAt = req.CollectedAt ?? DateTime.UtcNow;
        sample.CollectedById = _current.EmployeeId;
        sample.VolumeMl = req.VolumeMl;
        sample.CollectChecklistJson = req.Checklist == null ? null : JsonSerializer.Serialize(req.Checklist);
        _state.RecomputeFromSamples(sample.Order!);
        _audit.Log("COLLECT", "lab_order_sample", sample.Id, new { status = before }, new { sample.Status, sample.CollectedAt, req.Checklist });
        await _db.SaveChangesAsync();
        return ToDto(sample);
    }

    public async Task<SampleDto> UncollectAsync(string barcode)
    {
        var sample = await LoadByBarcodeAsync(barcode);
        var rule = _policy.Ensure(LisEntities.Sample, SampleActions.Uncollect, sample.Status, $"Пробірка {barcode}");
        sample.Status = rule.ToStatus!;
        sample.CollectedAt = null; sample.CollectedById = null;
        if (sample.Order!.Status == OrderStatuses.Collected && sample.Order.Samples.All(s => s.Status == SampleStatuses.Pending)) sample.Order.Status = OrderStatuses.New;
        _audit.Log("UNCOLLECT", "lab_order_sample", sample.Id, null, new { sample.Status });
        await _db.SaveChangesAsync();
        return ToDto(sample);
    }

    public async Task<SampleDto> ReceiveAsync(string barcode, ReceiveSampleRequest req)
    {
        var sample = await LoadByBarcodeAsync(barcode);
        var rule = _policy.Ensure(LisEntities.Sample, SampleActions.Receive, sample.Status, $"Пробірка {barcode}");
        var before = sample.Status;
        sample.Status = rule.ToStatus!;
        sample.ReceivedAt = DateTime.UtcNow;
        sample.ReceivedById = _current.EmployeeId;
        sample.IsHemolyzed = req.IsHemolyzed; sample.IsLipemic = req.IsLipemic; sample.IsIcteric = req.IsIcteric; sample.IsClotted = req.IsClotted; sample.IsInsufficientVolume = req.IsInsufficientVolume;
        _state.RecomputeFromSamples(sample.Order!);
        await _journal.RegisterSampleAsync(sample, sample.Order!);
        _audit.Log("RECEIVE", "lab_order_sample", sample.Id, new { status = before }, new { sample.Status, sample.ReceivedAt, req });
        await _db.SaveChangesAsync();
        return ToDto(sample);
    }

    /// <summary>Технічний прийом (результат з аналізатора прийшов раніше за сканування прийому).</summary>
    public void SystemReceive(LabOrderSample sample)
    {
        if (sample.Status is SampleStatuses.Collected or SampleStatuses.InTransit)
        {
            sample.Status = SampleStatuses.Received;
            sample.ReceivedAt ??= DateTime.UtcNow;
            if (sample.Order != null)
            {
                _state.RecomputeFromSamples(sample.Order);
                _journal.RegisterSampleAsync(sample, sample.Order).GetAwaiter().GetResult();
            }
        }
    }

    public async Task<object> RejectAsync(string barcode, RejectSampleRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Reason)) throw ValidationException.Field("reason", "Вкажіть причину відбраковки");
        var sample = await LoadByBarcodeAsync(barcode);
        var rule = _policy.Ensure(LisEntities.Sample, SampleActions.Reject, sample.Status, $"Пробірка {barcode}");
        var before = sample.Status;
        sample.Status = rule.ToStatus!;
        sample.RejectReason = req.Reason;
        var order = sample.Order!;
        var affected = order.Tests.Where(t => t.SampleId == sample.Id && !OrderTestStatuses.Final.Contains(t.Status)).ToList();
        foreach (var t in affected) { t.Status = OrderTestStatuses.Rejected; t.RejectReason = "Пробірку відбраковано: " + req.Reason; }
        _state.RecomputeFromSamples(order);
        _state.RecomputeCompletion(order);

        string? repeatOrderId = null;
        if (req.CreateRepeatOrder && affected.Count > 0)
        {
            var repeat = await _orders.BuildOrderAsync(new CreateOrderRequest
            {
                PatientId = order.PatientId, DoctorId = order.DoctorId, DepartmentId = order.DepartmentId, IsUrgentCito = order.IsUrgentCito,
                ClinicalNotes = $"Повторний забір (відбраковано пробірку {barcode}: {req.Reason}). Первинне замовлення {order.OrderNumber}",
                IsPregnant = order.IsPregnant, PregnancyWeek = order.PregnancyWeek, MenstrualPhase = order.MenstrualPhase, Icd10Code = order.Icd10Code,
                TestIds = affected.Select(t => t.TestId).Distinct().ToList()
            }, order.Id);
            repeatOrderId = repeat.Id;
        }
        _audit.Log("REJECT", "lab_order_sample", sample.Id, new { status = before }, new { sample.Status, req.Reason, repeatOrderId });
        await _db.SaveChangesAsync();
        return new { sample = ToDto(sample), repeatOrderId, rejectedTests = affected.Select(t => t.TestCode) };
    }

    public async Task<SampleDto> UpdateAsync(string idOrBarcode, SampleFlagsRequest req)
    {
        var sample = await LoadAsync(idOrBarcode);
        if (sample.Status == SampleStatuses.Pending)
            _policy.Ensure(LisEntities.Sample, SampleActions.Replace, sample.Status, $"Пробірка {sample.Barcode}");
        else
            _policy.Ensure(LisEntities.Sample, SampleActions.EditFlags, sample.Status, $"Пробірка {sample.Barcode}");
        var before = new { sample.IsHemolyzed, sample.IsLipemic, sample.IsIcteric, sample.IsClotted, sample.IsInsufficientVolume, sample.VolumeMl, sample.TubeTypeId, sample.BiomaterialTypeId };
        if (req.IsHemolyzed.HasValue) sample.IsHemolyzed = req.IsHemolyzed.Value;
        if (req.IsLipemic.HasValue) sample.IsLipemic = req.IsLipemic.Value;
        if (req.IsIcteric.HasValue) sample.IsIcteric = req.IsIcteric.Value;
        if (req.IsClotted.HasValue) sample.IsClotted = req.IsClotted.Value;
        if (req.IsInsufficientVolume.HasValue) sample.IsInsufficientVolume = req.IsInsufficientVolume.Value;
        if (req.VolumeMl.HasValue) sample.VolumeMl = req.VolumeMl;
        if (req.TubeTypeId.HasValue || req.BiomaterialTypeId.HasValue)
        {
            if (sample.Status != SampleStatuses.Pending) throw new ConflictException("Тип пробірки/біоматеріалу можна змінити лише до забору");
            if (req.TubeTypeId.HasValue) sample.TubeTypeId = req.TubeTypeId.Value;
            if (req.BiomaterialTypeId.HasValue) sample.BiomaterialTypeId = req.BiomaterialTypeId.Value;
        }
        _audit.Log("UPDATE", "lab_order_sample", sample.Id, before, req);
        await _db.SaveChangesAsync();
        return ToDto(await LoadAsync(sample.Id));
    }

    /// <summary>Заміна пробірки: новий штрихкод, статус PENDING, тести лишаються прив'язаними.</summary>
    public async Task<SampleDto> ReplaceAsync(string idOrBarcode)
    {
        var sample = await LoadAsync(idOrBarcode);
        _policy.Ensure(LisEntities.Sample, SampleActions.Replace, sample.Status, $"Пробірка {sample.Barcode}");
        var oldBarcode = sample.Barcode;
        sample.Barcode = await _numerators.NextTubeBarcodeAsync();
        sample.Status = SampleStatuses.Pending;
        sample.CollectedAt = null; sample.CollectedById = null; sample.ReceivedAt = null; sample.RejectReason = null;
        sample.IsHemolyzed = sample.IsLipemic = sample.IsIcteric = sample.IsClotted = sample.IsInsufficientVolume = false;
        foreach (var t in sample.Order!.Tests.Where(t => t.SampleId == sample.Id && t.Status == OrderTestStatuses.Rejected)) { t.Status = OrderTestStatuses.Pending; t.RejectReason = null; }
        if (sample.Order.Status == OrderStatuses.Rejected) sample.Order.Status = OrderStatuses.New;
        _audit.Log("REPLACE", "lab_order_sample", sample.Id, new { barcode = oldBarcode }, new { sample.Barcode });
        await _db.SaveChangesAsync();
        return ToDto(sample);
    }

    public async Task DeleteAsync(string idOrBarcode)
    {
        var sample = await LoadAsync(idOrBarcode);
        _policy.Ensure(LisEntities.Sample, SampleActions.Delete, sample.Status, $"Пробірка {sample.Barcode}");
        var tests = sample.Order!.Tests.Where(t => t.SampleId == sample.Id).ToList();
        if (tests.Count > 0) throw new ConflictException($"Пробірка містить тести ({string.Join(", ", tests.Select(t => t.TestCode))}) — спочатку перенесіть їх на іншу пробірку");
        _db.Samples.Remove(sample);
        _audit.Log("DELETE", "lab_order_sample", sample.Id, new { sample.Barcode }, null);
        await _db.SaveChangesAsync();
    }

    public async Task<SampleDto> TransitionAsync(string idOrBarcode, string action)
    {
        var sample = await LoadAsync(idOrBarcode);
        var rule = _policy.Ensure(LisEntities.Sample, action.ToUpperInvariant(), sample.Status, $"Пробірка {sample.Barcode}");
        if (rule.ToStatus == null) throw new ValidationException($"Дія {action} не є переходом статусу");
        var before = sample.Status;
        sample.Status = rule.ToStatus;
        if (rule.ToStatus == SampleStatuses.Disposed)
        {
            foreach (var cell in await _db.Cells.Where(c => c.SampleId == sample.Id && !c.IsDisposed).ToListAsync())
            { cell.IsDisposed = true; cell.DisposedAt = DateTime.UtcNow; cell.DisposalReason = "Утилізовано"; }
        }
        _state.RecomputeFromSamples(sample.Order!);
        _audit.Log(action.ToUpperInvariant(), "lab_order_sample", sample.Id, new { status = before }, new { sample.Status });
        await _db.SaveChangesAsync();
        return ToDto(sample);
    }

    public LabelDto BuildLabel(LabOrderSample sample)
    {
        var patientName = sample.Order?.Patient?.FullName ?? "";
        var tube = sample.TubeType?.Name ?? "";
        var at = sample.CollectedAt ?? sample.Order?.OrderDatetime ?? DateTime.UtcNow;
        return new LabelDto
        {
            Barcode = sample.Barcode,
            Zpl = ZplLabelBuilder.Build(sample.Barcode, patientName, tube, at.ToLocalTime(), sample.Order?.OrderNumber),
            Svg = Code128Svg.Render(sample.Barcode),
            PatientName = patientName, Tube = tube, TubeColor = sample.TubeType?.ColorCode, OrderNumber = sample.Order?.OrderNumber ?? "",
            CollectedAt = sample.CollectedAt,
            TestCodes = sample.Order?.Tests.Where(t => t.SampleId == sample.Id).Select(t => t.TestCode).ToList() ?? new()
        };
    }

    public async Task<LabelDto> LabelAsync(string barcode) => BuildLabel(await LoadByBarcodeAsync(barcode));

    public async Task<List<LabelDto>> LabelsForOrderAsync(string orderId)
    {
        var samples = await Query().AsNoTracking().Where(s => s.OrderId == orderId).OrderBy(s => s.GroupNumb).ToListAsync();
        if (samples.Count == 0 && !await _db.Orders.AnyAsync(o => o.Id == orderId)) throw NotFoundException.For("Замовлення", orderId);
        return samples.Select(BuildLabel).ToList();
    }
}
