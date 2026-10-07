// Логістика зразків: маніфести (створення → DISPATCHED → RECEIVED/REJECTED), холодовий ланцюг +2..+8 °C
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class ManifestRequest
{
    public string OriginDepartmentId { get; set; } = "";
    public string DestinationDepartmentId { get; set; } = "";
    public string? CourierName { get; set; }
    public string? CourierPhone { get; set; }
    public double? TemperatureDispatch { get; set; }
    public List<string> Barcodes { get; set; } = new();
    public string? Notes { get; set; }
    /// <summary>false → створити чернетку (CREATED) без відправки.</summary>
    public bool DispatchNow { get; set; } = true;
}

public sealed class ManifestReceiveRequest
{
    public double? TemperatureReceipt { get; set; }
    public List<string> ReceivedBarcodes { get; set; } = new();
    public string? Notes { get; set; }
}

public sealed class LogisticsService
{
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;
    private readonly INumeratorService _numerators;
    private readonly OrderStateService _state;
    private readonly SectionJournalService _journal;

    public LogisticsService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current, INumeratorService numerators, OrderStateService state, SectionJournalService journal)
    {
        _db = db; _policy = policy; _audit = audit; _current = current; _numerators = numerators; _state = state; _journal = journal;
    }

    private IQueryable<LabSampleLogistics> Query() => _db.Logistics.Include(m => m.Items).Include(m => m.OriginDepartment).Include(m => m.DestinationDepartment);

    public async Task<List<object>> ListAsync(string? status)
    {
        var items = await Query().AsNoTracking().Where(m => !m.IsDeleted && (status == null || m.Status == status.ToUpper())).OrderByDescending(m => m.CreatedOn).ToListAsync();
        return items.Select(ToDto).ToList();
    }

    public async Task<object> GetAsync(string id) => ToDto(await LoadAsync(id));

    private async Task<LabSampleLogistics> LoadAsync(string id) => await Query().FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted) ?? throw NotFoundException.For("Маніфест", id);

    private object ToDto(LabSampleLogistics m) => new
    {
        m.Id, m.ManifestNumber, m.OriginDepartmentId, originDepartmentName = m.OriginDepartment?.Name, m.DestinationDepartmentId, destinationDepartmentName = m.DestinationDepartment?.Name,
        m.CourierName, m.CourierPhone, m.DispatchedAt, m.DispatchedById, m.TemperatureDispatch, m.ReceivedAt, m.ReceivedById, m.TemperatureReceipt, m.IsColdChainViolated, m.Status, m.Notes, m.CreatedOn,
        items = m.Items.Select(i => new { i.Id, i.SampleId, i.Barcode, i.Status }),
        allowedActions = _policy.AllowedActions(LisEntities.Manifest, m.Status), stateMachine = LisStateMachine.Describe(LisEntities.Manifest)
    };

    public async Task<object> CreateAsync(ManifestRequest req)
    {
        _policy.Require("Створення маніфесту", LabRoles.Admin, LabRoles.Courier, LabRoles.Phlebotomist);
        if (req.Barcodes.Count == 0) throw ValidationException.Field("barcodes", "Додайте хоча б одну пробірку");
        if (!await _db.Departments.AnyAsync(d => d.Id == req.OriginDepartmentId)) throw ValidationException.Field("originDepartmentId", "Підрозділ-відправник не знайдено");
        if (!await _db.Departments.AnyAsync(d => d.Id == req.DestinationDepartmentId)) throw ValidationException.Field("destinationDepartmentId", "Підрозділ-отримувач не знайдено");
        var m = new LabSampleLogistics
        {
            ManifestNumber = await _numerators.NextManifestNumberAsync(DateTime.UtcNow), OriginDepartmentId = req.OriginDepartmentId, DestinationDepartmentId = req.DestinationDepartmentId,
            CourierName = req.CourierName, CourierPhone = req.CourierPhone, TemperatureDispatch = req.TemperatureDispatch, Notes = req.Notes, Status = LisStateMachine.ManifestCreated
        };
        _db.Logistics.Add(m);
        await AddItemsAsync(m, req.Barcodes);
        if (req.DispatchNow) Dispatch(m);
        _audit.Log("CREATE", "lab_sample_logistics", m.Id, null, new { m.ManifestNumber, m.Status, req.Barcodes });
        await _db.SaveChangesAsync();
        return await GetAsync(m.Id);
    }

    private async Task AddItemsAsync(LabSampleLogistics m, IEnumerable<string> barcodes)
    {
        foreach (var bc in barcodes.Distinct())
        {
            var sample = await _db.Samples.Include(s => s.Order).ThenInclude(o => o!.Samples).FirstOrDefaultAsync(s => s.Barcode == bc) ?? throw NotFoundException.For("Пробірка", bc);
            if (sample.Status != SampleStatuses.Collected) throw new ConflictException($"Пробірка {bc} у статусі {sample.Status}: до маніфесту додаються лише зібрані (COLLECTED) пробірки");
            if (m.Items.Any(i => i.SampleId == sample.Id)) continue;
            m.Items.Add(new LabSampleLogisticsItem { LogisticsId = m.Id, SampleId = sample.Id, Barcode = bc, Status = "EN_ROUTE" });
        }
    }

    private void Dispatch(LabSampleLogistics m)
    {
        var rule = _policy.Ensure(LisEntities.Manifest, ManifestActions.Dispatch, m.Status, $"Маніфест {m.ManifestNumber}");
        m.Status = rule.ToStatus!;
        m.DispatchedAt = DateTime.UtcNow;
        m.DispatchedById = _current.EmployeeId;
        foreach (var item in m.Items)
        {
            var sample = _db.Samples.Local.FirstOrDefault(s => s.Id == item.SampleId) ?? _db.Samples.Include(s => s.Order).ThenInclude(o => o!.Samples).First(s => s.Id == item.SampleId);
            if (sample.Status == SampleStatuses.Collected) sample.Status = SampleStatuses.InTransit;
            if (sample.Order != null) _state.RecomputeFromSamples(sample.Order);
        }
    }

    public async Task<object> DispatchAsync(string id)
    {
        var m = await LoadAsync(id);
        Dispatch(m);
        _audit.Log("DISPATCH", "lab_sample_logistics", m.Id, null, new { m.Status, m.DispatchedAt });
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<object> UpdateAsync(string id, ManifestRequest req)
    {
        var m = await LoadAsync(id);
        _policy.Ensure(LisEntities.Manifest, ManifestActions.Edit, m.Status, $"Маніфест {m.ManifestNumber}");
        var before = new { m.CourierName, m.CourierPhone, m.TemperatureDispatch, m.Notes, items = m.Items.Select(i => i.Barcode).ToList() };
        if (req.CourierName != null) m.CourierName = req.CourierName;
        if (req.CourierPhone != null) m.CourierPhone = req.CourierPhone;
        if (req.TemperatureDispatch.HasValue) m.TemperatureDispatch = req.TemperatureDispatch;
        if (req.Notes != null) m.Notes = req.Notes;
        if (!string.IsNullOrWhiteSpace(req.OriginDepartmentId)) m.OriginDepartmentId = req.OriginDepartmentId;
        if (!string.IsNullOrWhiteSpace(req.DestinationDepartmentId)) m.DestinationDepartmentId = req.DestinationDepartmentId;
        if (req.Barcodes.Count > 0)
        {
            // Синхронізація складу: прибрати відсутні, додати нові
            foreach (var item in m.Items.Where(i => !req.Barcodes.Contains(i.Barcode)).ToList()) await RemoveItemInternalAsync(m, item);
            await AddItemsAsync(m, req.Barcodes.Where(b => m.Items.All(i => i.Barcode != b)));
            if (m.Status == LisStateMachine.ManifestDispatched)
                foreach (var item in m.Items)
                {
                    var sample = await _db.Samples.Include(s => s.Order).ThenInclude(o => o!.Samples).FirstAsync(s => s.Id == item.SampleId);
                    if (sample.Status == SampleStatuses.Collected) { sample.Status = SampleStatuses.InTransit; _state.RecomputeFromSamples(sample.Order!); }
                }
        }
        _audit.Log("UPDATE", "lab_sample_logistics", m.Id, before, req);
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    private async Task RemoveItemInternalAsync(LabSampleLogistics m, LabSampleLogisticsItem item)
    {
        var sample = await _db.Samples.Include(s => s.Order).ThenInclude(o => o!.Samples).FirstOrDefaultAsync(s => s.Id == item.SampleId);
        if (sample != null && sample.Status == SampleStatuses.InTransit) { sample.Status = SampleStatuses.Collected; if (sample.Order!.Status == OrderStatuses.InTransit) sample.Order.Status = OrderStatuses.Collected; }
        m.Items.Remove(item);
        _db.LogisticsItems.Remove(item);
    }

    public async Task<object> RemoveItemAsync(string id, string itemId)
    {
        var m = await LoadAsync(id);
        _policy.Ensure(LisEntities.Manifest, ManifestActions.Edit, m.Status, $"Маніфест {m.ManifestNumber}");
        var item = m.Items.FirstOrDefault(i => i.Id == itemId) ?? throw NotFoundException.For("Позиція маніфесту", itemId);
        await RemoveItemInternalAsync(m, item);
        _audit.Log("REMOVE_ITEM", "lab_sample_logistics", m.Id, new { item.Barcode }, null);
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<object> ReceiveAsync(string id, ManifestReceiveRequest req)
    {
        var m = await LoadAsync(id);
        var rule = _policy.Ensure(LisEntities.Manifest, ManifestActions.Receive, m.Status, $"Маніфест {m.ManifestNumber}");
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        var min = settings?.ColdChainMinC ?? 2.0; var max = settings?.ColdChainMaxC ?? 8.0;
        var now = DateTime.UtcNow;
        m.Status = rule.ToStatus!;
        m.ReceivedAt = now; m.ReceivedById = _current.EmployeeId; m.TemperatureReceipt = req.TemperatureReceipt;
        m.IsColdChainViolated = (req.TemperatureReceipt.HasValue && (req.TemperatureReceipt < min || req.TemperatureReceipt > max))
                                || (m.TemperatureDispatch.HasValue && (m.TemperatureDispatch < min || m.TemperatureDispatch > max));
        if (!string.IsNullOrWhiteSpace(req.Notes)) m.Notes = string.IsNullOrWhiteSpace(m.Notes) ? req.Notes : m.Notes + "\n" + req.Notes;
        if (m.IsColdChainViolated) m.Notes = (m.Notes ?? "") + $"\n[!] Порушення холодового ланцюга: відправка {m.TemperatureDispatch}°C, прийом {req.TemperatureReceipt}°C (норма {min}..{max}°C)";

        var received = req.ReceivedBarcodes.Count == 0 ? m.Items.Select(i => i.Barcode).ToList() : req.ReceivedBarcodes;
        var missing = new List<string>();
        foreach (var item in m.Items)
        {
            var sample = await _db.Samples.Include(s => s.Order).ThenInclude(o => o!.Samples).FirstAsync(s => s.Id == item.SampleId);
            if (received.Contains(item.Barcode))
            {
                item.Status = "RECEIVED";
                if (sample.Status is SampleStatuses.InTransit or SampleStatuses.Collected) { sample.Status = SampleStatuses.Received; sample.ReceivedAt = now; sample.ReceivedById = _current.EmployeeId; }
                _state.RecomputeFromSamples(sample.Order!);
                await _db.Entry(sample.Order!).Collection(o => o.Tests).LoadAsync();
                await _journal.RegisterSampleAsync(sample, sample.Order!);
            }
            else { item.Status = "MISSING"; missing.Add(item.Barcode); }
        }
        _audit.Log("RECEIVE", "lab_sample_logistics", m.Id, null, new { m.Status, m.TemperatureReceipt, m.IsColdChainViolated, missing });
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<object> RejectAsync(string id, string reason)
    {
        var m = await LoadAsync(id);
        var rule = _policy.Ensure(LisEntities.Manifest, ManifestActions.Reject, m.Status, $"Маніфест {m.ManifestNumber}");
        m.Status = rule.ToStatus!;
        m.Notes = (m.Notes ?? "") + "\nВідхилено: " + reason;
        foreach (var item in m.Items)
        {
            var sample = await _db.Samples.Include(s => s.Order).ThenInclude(o => o!.Samples).Include(s => s.Order).ThenInclude(o => o!.Tests).FirstAsync(s => s.Id == item.SampleId);
            sample.Status = SampleStatuses.Rejected; sample.RejectReason = "Маніфест відхилено: " + reason;
            foreach (var t in sample.Order!.Tests.Where(t => t.SampleId == sample.Id && !OrderTestStatuses.Final.Contains(t.Status))) { t.Status = OrderTestStatuses.Rejected; t.RejectReason = sample.RejectReason; }
            _state.RecomputeFromSamples(sample.Order);
        }
        _audit.Log("REJECT", "lab_sample_logistics", m.Id, null, new { reason });
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task DeleteAsync(string id)
    {
        var m = await LoadAsync(id);
        _policy.Ensure(LisEntities.Manifest, ManifestActions.Delete, m.Status, $"Маніфест {m.ManifestNumber}");
        foreach (var item in m.Items.ToList()) await RemoveItemInternalAsync(m, item);
        _db.Logistics.Remove(m);
        _audit.Log("DELETE", "lab_sample_logistics", id, new { m.ManifestNumber }, null);
        await _db.SaveChangesAsync();
    }
}
