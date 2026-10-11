// Біобанк (штативи 8×12, комірки) та реагенти (лоти, списання, попередження)
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class RackRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? RoomNumber { get; set; }
    public string? FreezerName { get; set; }
    public string? ShelfNumber { get; set; }
    public double TemperatureCelsius { get; set; } = -20;
    public int RowsCount { get; set; } = 8;
    public int ColsCount { get; set; } = 12;
    public bool IsActive { get; set; } = true;
}

public sealed class PlaceCellRequest
{
    public string RackId { get; set; } = "";
    /// <summary>1..rows або літера A..H</summary>
    public string Row { get; set; } = "";
    public int Col { get; set; }
    public string Barcode { get; set; } = "";
    public DateTime? ExpiryAt { get; set; }
}

public sealed class ReagentLotRequest
{
    public string? AnalyzerId { get; set; }
    public string? TestCode { get; set; }
    public string ReagentName { get; set; } = "";
    public string LotNumber { get; set; } = "";
    public string? Manufacturer { get; set; }
    public int TestsInitial { get; set; }
    public int? TestsRemaining { get; set; }
    public int MinimumTests { get; set; } = 50;
    public DateTime ExpiryDate { get; set; }
    public DateTime? OpenedAt { get; set; }
    public int? OnboardStabilityDays { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class BiobankService
{
    private static readonly string[] Roles = { LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician };
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;
    private readonly OrderStateService _state;

    public BiobankService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current, OrderStateService state)
    {
        _db = db; _policy = policy; _audit = audit; _current = current; _state = state;
    }

    public async Task<List<object>> RacksAsync()
    {
        var racks = await _db.Racks.AsNoTracking().Include(r => r.Cells).Where(r => r.RecordState != RecordStates.Deleted).OrderBy(r => r.Code).ToListAsync();
        return racks.Select(r => RackDto(r)).ToList();
    }

    private static object RackDto(LabArchiveRack r) => new
    {
        r.Id, r.Code, r.Name, r.RoomNumber, r.FreezerName, r.ShelfNumber, r.TemperatureCelsius, r.RowsCount, r.ColsCount, r.IsActive, r.CreatedOn,
        capacity = r.RowsCount * r.ColsCount, occupied = r.Cells.Count(c => !c.IsDisposed && c.SampleId != null),
        expiredCount = r.Cells.Count(c => !c.IsDisposed && c.SampleId != null && c.ExpiryAt < DateTime.UtcNow)
    };

    public async Task<object> RackAsync(string id) => RackDto(await LoadRackAsync(id));
    private async Task<LabArchiveRack> LoadRackAsync(string id) => await _db.Racks.Include(r => r.Cells).FirstOrDefaultAsync(r => r.Id == id && r.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Штатив", id);

    public async Task<object> CreateRackAsync(RackRequest req)
    {
        _policy.Require("Створення штатива", Roles);
        if (string.IsNullOrWhiteSpace(req.Code)) throw ValidationException.Field("code", "Вкажіть код штатива");
        if (await _db.Racks.AnyAsync(r => r.Code == req.Code && r.RecordState != RecordStates.Deleted)) throw new ConflictException($"Штатив із кодом {req.Code} вже існує");
        if (req.RowsCount is < 1 or > 26 || req.ColsCount is < 1 or > 50) throw new ValidationException("Розмір штатива: рядків 1..26, колонок 1..50");
        var r = new LabArchiveRack { Code = req.Code, Name = req.Name, RoomNumber = req.RoomNumber, FreezerName = req.FreezerName, ShelfNumber = req.ShelfNumber, TemperatureCelsius = req.TemperatureCelsius, RowsCount = req.RowsCount, ColsCount = req.ColsCount, IsActive = req.IsActive };
        _db.Racks.Add(r);
        _audit.Log("CREATE", "lab_archive_rack", r.Id, null, req);
        await _db.SaveChangesAsync();
        return RackDto(r);
    }

    public async Task<object> UpdateRackAsync(string id, RackRequest req)
    {
        _policy.Require("Редагування штатива", Roles);
        var r = await LoadRackAsync(id);
        var before = new { r.Code, r.Name, r.RoomNumber, r.FreezerName, r.ShelfNumber, r.TemperatureCelsius, r.RowsCount, r.ColsCount, r.IsActive };
        var maxRow = r.Cells.Where(c => c.SampleId != null && !c.IsDisposed).Select(c => c.RowNum).DefaultIfEmpty(0).Max();
        var maxCol = r.Cells.Where(c => c.SampleId != null && !c.IsDisposed).Select(c => c.ColNum).DefaultIfEmpty(0).Max();
        if (req.RowsCount < maxRow || req.ColsCount < maxCol) throw new ConflictException("Неможливо зменшити штатив: є зайняті комірки поза новими межами");
        if (!string.IsNullOrWhiteSpace(req.Code)) r.Code = req.Code;
        if (!string.IsNullOrWhiteSpace(req.Name)) r.Name = req.Name;
        r.RoomNumber = req.RoomNumber ?? r.RoomNumber; r.FreezerName = req.FreezerName ?? r.FreezerName; r.ShelfNumber = req.ShelfNumber ?? r.ShelfNumber;
        r.TemperatureCelsius = req.TemperatureCelsius; r.RowsCount = req.RowsCount; r.ColsCount = req.ColsCount; r.IsActive = req.IsActive;
        _audit.Log("UPDATE", "lab_archive_rack", r.Id, before, req);
        await _db.SaveChangesAsync();
        return RackDto(r);
    }

    public async Task DeleteRackAsync(string id)
    {
        _policy.Require("Видалення штатива", LabRoles.Admin, LabRoles.Doctor);
        var r = await LoadRackAsync(id);
        if (r.Cells.Any(c => c.SampleId != null && !c.IsDisposed)) throw new ConflictException("Штатив містить зразки — спочатку вилучіть їх");
        _db.Racks.Remove(r);
        _audit.Log("DELETE", "lab_archive_rack", id, new { r.Code }, null);
        await _db.SaveChangesAsync();
    }

    public async Task<object> CellsAsync(string rackId)
    {
        var rack = await _db.Racks.AsNoTracking().Include(r => r.Cells).ThenInclude(c => c.Sample).ThenInclude(s => s!.Order).ThenInclude(o => o!.Patient)
            .FirstOrDefaultAsync(r => r.Id == rackId) ?? throw NotFoundException.For("Штатив", rackId);
        var cells = new List<object>();
        for (var row = 1; row <= rack.RowsCount; row++)
            for (var col = 1; col <= rack.ColsCount; col++)
            {
                var c = rack.Cells.FirstOrDefault(x => x.RowNum == row && x.ColNum == col && !x.IsDisposed);
                cells.Add(new
                {
                    id = c?.Id, rackId, row, col, coordinate = $"{(char)('A' + row - 1)}{col:00}", isOccupied = c?.SampleId != null,
                    sampleId = c?.SampleId, barcode = c?.Barcode, storedAt = c?.StoredAt, expiryAt = c?.ExpiryAt, isExpired = c?.ExpiryAt < DateTime.UtcNow,
                    patientName = c?.Sample?.Order?.Patient?.Caption, orderNumber = c?.Sample?.Order?.OrderNumber
                });
            }
        return new { rack = RackDto(rack), cells };
    }

    public async Task<object> PlaceAsync(PlaceCellRequest req)
    {
        _policy.Require("Розміщення зразка в біобанку", Roles);
        var rack = await LoadRackAsync(req.RackId);
        var row = ParseRow(req.Row);
        if (row < 1 || row > rack.RowsCount || req.Col < 1 || req.Col > rack.ColsCount) throw new ValidationException($"Координата поза межами штатива {rack.RowsCount}×{rack.ColsCount}");
        var sample = await _db.Samples.Include(s => s.Order).ThenInclude(o => o!.Samples).FirstOrDefaultAsync(s => s.Barcode == req.Barcode) ?? throw NotFoundException.For("Пробірка", req.Barcode);
        if (await _db.Cells.AnyAsync(c => c.SampleId == sample.Id && !c.IsDisposed)) throw new ConflictException($"Пробірка {req.Barcode} вже розміщена в біобанку");
        var cell = rack.Cells.FirstOrDefault(c => c.RowNum == row && c.ColNum == req.Col);
        if (cell != null && cell.SampleId != null && !cell.IsDisposed) throw new ConflictException($"Комірка {(char)('A' + row - 1)}{req.Col:00} зайнята пробіркою {cell.Barcode}");
        if (cell == null) { cell = new LabArchiveCell { RackId = rack.Id, RowNum = row, ColNum = req.Col }; rack.Cells.Add(cell); }
        cell.SampleId = sample.Id; cell.Barcode = sample.Barcode; cell.StoredAt = DateTime.UtcNow; cell.StoredById = _current.EmployeeId;
        cell.ExpiryAt = req.ExpiryAt ?? DateTime.UtcNow.AddDays(180); cell.IsDisposed = false; cell.DisposedAt = null; cell.DisposalReason = null;
        _policy.Ensure(LisEntities.Sample, SampleActions.Store, sample.Status, $"Пробірка {sample.Barcode}");
        sample.Status = SampleStatuses.Stored;
        _audit.Log("PLACE", "lab_archive_cell", cell.Id, null, new { rack.Code, row, req.Col, req.Barcode, cell.ExpiryAt });
        await _db.SaveChangesAsync();
        return new { cell.Id, rack.Code, row, req.Col, coordinate = cell.Coordinate, cell.Barcode, cell.StoredAt, cell.ExpiryAt };
    }

    public async Task<object> RemoveAsync(string cellId, string reason)
    {
        _policy.Require("Вилучення зразка з біобанку", Roles);
        var cell = await _db.Cells.Include(c => c.Sample).ThenInclude(s => s!.Order).FirstOrDefaultAsync(c => c.Id == cellId) ?? throw NotFoundException.For("Комірка", cellId);
        if (cell.SampleId == null || cell.IsDisposed) throw new ConflictException("Комірка порожня");
        cell.IsDisposed = true; cell.DisposedAt = DateTime.UtcNow; cell.DisposalReason = reason;
        if (cell.Sample != null && cell.Sample.Status == SampleStatuses.Stored) cell.Sample.Status = SampleStatuses.Disposed;
        _audit.Log("REMOVE", "lab_archive_cell", cell.Id, new { cell.Barcode }, new { reason });
        await _db.SaveChangesAsync();
        return new { cell.Id, cell.Barcode, cell.IsDisposed, cell.DisposedAt, cell.DisposalReason };
    }

    public async Task<object> UpdateCellAsync(string cellId, DateTime? expiryAt)
    {
        _policy.Require("Редагування комірки", Roles);
        var cell = await _db.Cells.FirstOrDefaultAsync(c => c.Id == cellId) ?? throw NotFoundException.For("Комірка", cellId);
        var before = cell.ExpiryAt;
        cell.ExpiryAt = expiryAt;
        _audit.Log("UPDATE", "lab_archive_cell", cell.Id, new { expiryAt = before }, new { expiryAt });
        await _db.SaveChangesAsync();
        return new { cell.Id, cell.Barcode, cell.ExpiryAt };
    }

    public async Task DeleteCellAsync(string cellId)
    {
        _policy.Require("Видалення комірки", LabRoles.Admin, LabRoles.Doctor);
        var cell = await _db.Cells.FirstOrDefaultAsync(c => c.Id == cellId) ?? throw NotFoundException.For("Комірка", cellId);
        if (cell.SampleId != null && !cell.IsDisposed) throw new ConflictException("Комірка зайнята — спочатку вилучіть зразок");
        _db.Cells.Remove(cell);
        _audit.Log("DELETE", "lab_archive_cell", cellId, new { cell.Coordinate }, null);
        await _db.SaveChangesAsync();
    }

    public async Task<object?> SearchAsync(string barcode)
    {
        var cell = await _db.Cells.AsNoTracking().Include(c => c.Rack).Include(c => c.Sample).ThenInclude(s => s!.Order).ThenInclude(o => o!.Patient)
            .FirstOrDefaultAsync(c => c.Barcode == barcode && !c.IsDisposed);
        if (cell == null) return null;
        return new
        {
            cell.Id, rackId = cell.RackId, rackCode = cell.Rack?.Code, rackName = cell.Rack?.Name, freezer = cell.Rack?.FreezerName, shelf = cell.Rack?.ShelfNumber, temperature = cell.Rack?.TemperatureCelsius,
            row = cell.RowNum, col = cell.ColNum, coordinate = cell.Coordinate, cell.Barcode, cell.StoredAt, cell.ExpiryAt, isExpired = cell.ExpiryAt < DateTime.UtcNow,
            patientName = cell.Sample?.Order?.Patient?.Caption, orderNumber = cell.Sample?.Order?.OrderNumber, sampleStatus = cell.Sample?.Status
        };
    }

    public async Task<List<object>> ExpiredAsync()
    {
        var cells = await _db.Cells.AsNoTracking().Include(c => c.Rack).Where(c => !c.IsDisposed && c.SampleId != null && c.ExpiryAt < DateTime.UtcNow).ToListAsync();
        return cells.Select(c => new { c.Id, rackCode = c.Rack?.Code, coordinate = c.Coordinate, c.Barcode, c.ExpiryAt }).ToList<object>();
    }

    private static int ParseRow(string row)
    {
        if (string.IsNullOrWhiteSpace(row)) throw ValidationException.Field("row", "Вкажіть рядок (A..Z або 1..26)");
        if (int.TryParse(row, out var n)) return n;
        var ch = char.ToUpperInvariant(row.Trim()[0]);
        if (ch < 'A' || ch > 'Z') throw ValidationException.Field("row", "Рядок має бути літерою A..Z або числом");
        return ch - 'A' + 1;
    }
}

public sealed class ReagentService
{
    private static readonly string[] Roles = { LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician };
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;

    public ReagentService(LisDbContext db, IRolePolicy policy, IAuditService audit) { _db = db; _policy = policy; _audit = audit; }

    public async Task<List<LabReagentLot>> ListAsync(string? analyzerId, bool? isActive) =>
        await _db.ReagentLots.AsNoTracking().Include(l => l.Analyzer).Where(l => l.RecordState != RecordStates.Deleted && (analyzerId == null || l.AnalyzerId == analyzerId) && (isActive == null || l.IsActive == isActive))
            .OrderBy(l => l.ExpiryDate).ToListAsync();

    public async Task<LabReagentLot> GetAsync(string id) => await _db.ReagentLots.AsNoTracking().Include(l => l.Analyzer).FirstOrDefaultAsync(l => l.Id == id && l.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Лот реагенту", id);

    public async Task<LabReagentLot> CreateAsync(ReagentLotRequest req)
    {
        _policy.Require("Створення лоту реагенту", Roles);
        if (string.IsNullOrWhiteSpace(req.ReagentName) || string.IsNullOrWhiteSpace(req.LotNumber)) throw new ValidationException("Вкажіть назву реагенту та номер лоту");
        if (req.TestsInitial <= 0) throw ValidationException.Field("testsInitial", "Кількість тестів має бути > 0");
        if (req.AnalyzerId != null && !await _db.Analyzers.AnyAsync(a => a.Id == req.AnalyzerId)) throw ValidationException.Field("analyzerId", "Аналізатор не знайдено");
        var lot = new LabReagentLot
        {
            AnalyzerId = req.AnalyzerId, TestCode = req.TestCode, ReagentName = req.ReagentName, LotNumber = req.LotNumber, Manufacturer = req.Manufacturer, TestsInitial = req.TestsInitial,
            TestsRemaining = req.TestsRemaining ?? req.TestsInitial, MinimumTests = req.MinimumTests, ExpiryDate = req.ExpiryDate, OpenedAt = req.OpenedAt, OnboardStabilityDays = req.OnboardStabilityDays, IsActive = req.IsActive
        };
        _db.ReagentLots.Add(lot);
        _audit.Log("CREATE", "lab_reagent_lot", lot.Id, null, req);
        await _db.SaveChangesAsync();
        return lot;
    }

    public async Task<LabReagentLot> UpdateAsync(string id, ReagentLotRequest req)
    {
        _policy.Require("Редагування лоту реагенту", Roles);
        var lot = await _db.ReagentLots.FirstOrDefaultAsync(l => l.Id == id && l.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Лот реагенту", id);
        var before = new { lot.ReagentName, lot.LotNumber, lot.TestsRemaining, lot.MinimumTests, lot.ExpiryDate, lot.IsActive };
        if (!string.IsNullOrWhiteSpace(req.ReagentName)) lot.ReagentName = req.ReagentName;
        if (!string.IsNullOrWhiteSpace(req.LotNumber)) lot.LotNumber = req.LotNumber;
        lot.AnalyzerId = req.AnalyzerId ?? lot.AnalyzerId; lot.TestCode = req.TestCode ?? lot.TestCode; lot.Manufacturer = req.Manufacturer ?? lot.Manufacturer;
        if (req.TestsInitial > 0) lot.TestsInitial = req.TestsInitial;
        if (req.TestsRemaining.HasValue) lot.TestsRemaining = req.TestsRemaining.Value;
        lot.MinimumTests = req.MinimumTests;
        if (req.ExpiryDate != default) lot.ExpiryDate = req.ExpiryDate;
        lot.OpenedAt = req.OpenedAt ?? lot.OpenedAt; lot.OnboardStabilityDays = req.OnboardStabilityDays ?? lot.OnboardStabilityDays; lot.IsActive = req.IsActive;
        _audit.Log("UPDATE", "lab_reagent_lot", lot.Id, before, req);
        await _db.SaveChangesAsync();
        return lot;
    }

    public async Task DeleteAsync(string id)
    {
        _policy.Require("Видалення лоту реагенту", LabRoles.Admin, LabRoles.Doctor);
        var lot = await _db.ReagentLots.FirstOrDefaultAsync(l => l.Id == id) ?? throw NotFoundException.For("Лот реагенту", id);
        if (lot.TestsRemaining != lot.TestsInitial) { lot.IsActive = false; lot.RecordState = RecordStates.Deleted; _audit.Log("SOFT_DELETE", "lab_reagent_lot", id, null, null, "Лот частково використано — деактивовано"); }
        else { _db.ReagentLots.Remove(lot); _audit.Log("DELETE", "lab_reagent_lot", id, new { lot.LotNumber }, null); }
        await _db.SaveChangesAsync();
    }

    public async Task<LabReagentLot> ConsumeAsync(string id, int tests)
    {
        _policy.Require("Списання реагенту", Roles);
        if (tests <= 0) throw ValidationException.Field("tests", "Кількість має бути > 0");
        var lot = await _db.ReagentLots.FirstOrDefaultAsync(l => l.Id == id && l.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Лот реагенту", id);
        if (!lot.IsActive) throw new ConflictException("Лот неактивний");
        if (lot.TestsRemaining < tests) throw new ConflictException($"Недостатньо реагенту: залишок {lot.TestsRemaining}, потрібно {tests}");
        lot.TestsRemaining -= tests;
        lot.OpenedAt ??= DateTime.UtcNow;
        _audit.Log("CONSUME", "lab_reagent_lot", lot.Id, null, new { tests, lot.TestsRemaining });
        await _db.SaveChangesAsync();
        return lot;
    }

    public async Task<List<object>> AlertsAsync()
    {
        var now = DateTime.UtcNow;
        var lots = await _db.ReagentLots.AsNoTracking().Include(l => l.Analyzer).Where(l => l.IsActive && l.RecordState != RecordStates.Deleted).ToListAsync();
        var alerts = new List<object>();
        foreach (var l in lots)
        {
            if (l.TestsRemaining <= 0) alerts.Add(new { l.Id, l.ReagentName, l.LotNumber, analyzer = l.Analyzer?.Name, type = "EMPTY", severity = "CRITICAL", message = "Реагент вичерпано" });
            else if (l.TestsRemaining <= l.MinimumTests) alerts.Add(new { l.Id, l.ReagentName, l.LotNumber, analyzer = l.Analyzer?.Name, type = "LOW_STOCK", severity = "WARNING", message = $"Залишок {l.TestsRemaining} тестів (мінімум {l.MinimumTests})" });
            if (l.ExpiryDate < now) alerts.Add(new { l.Id, l.ReagentName, l.LotNumber, analyzer = l.Analyzer?.Name, type = "EXPIRED", severity = "CRITICAL", message = $"Термін придатності минув {l.ExpiryDate:dd.MM.yyyy}" });
            else if (l.ExpiryDate < now.AddDays(14)) alerts.Add(new { l.Id, l.ReagentName, l.LotNumber, analyzer = l.Analyzer?.Name, type = "EXPIRING", severity = "WARNING", message = $"Термін придатності спливає {l.ExpiryDate:dd.MM.yyyy}" });
            if (l.OpenedAt.HasValue && l.OnboardStabilityDays.HasValue && l.OpenedAt.Value.AddDays(l.OnboardStabilityDays.Value) < now)
                alerts.Add(new { l.Id, l.ReagentName, l.LotNumber, analyzer = l.Analyzer?.Name, type = "ONBOARD_EXPIRED", severity = "WARNING", message = $"Перевищено on-board стабільність ({l.OnboardStabilityDays} дн. після відкриття)" });
        }
        return alerts;
    }
}
