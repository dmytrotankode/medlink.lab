// Калібрування аналізаторів (FR-GAP-031): журнал, статуси дійсне/протерміноване, перевірка для конвеєра результатів
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public static class CalibrationStatuses
{
    /// <summary>Журнал для цього приладу/показника порожній — калібрування не відстежується, обмежень немає.</summary>
    public const string None = "NONE";
    public const string Valid = "VALID";
    /// <summary>Дійсне, але термін спливає найближчими днями (нагадування).</summary>
    public const string Expiring = "EXPIRING";
    public const string Expired = "EXPIRED";
    public const string Rejected = "REJECTED";
    /// <summary>Лот реагенту, під який виконано калібрування, прострочений або неактивний.</summary>
    public const string LotInvalid = "LOT_INVALID";
}

public sealed class CalibrationRequest
{
    public string AnalyzerId { get; set; } = "";
    public string? TestCode { get; set; }
    public string? ReagentLotId { get; set; }
    public string? CalibratorName { get; set; }
    public string CalibratorLot { get; set; } = "";
    public DateTime? PerformedAt { get; set; }
    public double? CalibrationValue { get; set; }
    public string? Unit { get; set; }
    public bool Accepted { get; set; } = true;
    public string? RejectReason { get; set; }
    /// <summary>Термін дії: або дата, або кількість днів (ValidDays).</summary>
    public DateTime? ValidUntil { get; set; }
    public int? ValidDays { get; set; }
    public DateTime? NextDueAt { get; set; }
    public string? Comment { get; set; }
}

public sealed class CalibrationUpdateRequest
{
    public string? Comment { get; set; }
    public DateTime? NextDueAt { get; set; }
}

/// <summary>Результат перевірки калібрування для пари прилад + показник.</summary>
public sealed class CalibrationCheck
{
    public string Status { get; init; } = CalibrationStatuses.None;
    public LabCalibration? Calibration { get; init; }
    public string? Message { get; init; }
    public int? DaysLeft { get; init; }
    /// <summary>true — калібрування протерміноване/відхилене/з недійсним лотом (результати потребують уваги).</summary>
    public bool IsProblem => Status is CalibrationStatuses.Expired or CalibrationStatuses.Rejected or CalibrationStatuses.LotInvalid;
}

public sealed class CalibrationService
{
    private static readonly string[] WriteRoles = { LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician };
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;

    public CalibrationService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current)
    { _db = db; _policy = policy; _audit = audit; _current = current; }

    public static string StatusOf(LabCalibration c, DateTime now, int warnDays)
    {
        if (!c.Accepted) return CalibrationStatuses.Rejected;
        if (c.ValidUntil < now) return CalibrationStatuses.Expired;
        if (c.ReagentLot != null && (!c.ReagentLot.IsActive || c.ReagentLot.ExpiryDate < now)) return CalibrationStatuses.LotInvalid;
        return (c.ValidUntil - now).TotalDays <= warnDays ? CalibrationStatuses.Expiring : CalibrationStatuses.Valid;
    }

    public static string StatusText(string status) => status switch
    {
        CalibrationStatuses.Valid => "Дійсне",
        CalibrationStatuses.Expiring => "Спливає",
        CalibrationStatuses.Expired => "Протерміноване",
        CalibrationStatuses.Rejected => "Відхилене",
        CalibrationStatuses.LotInvalid => "Лот реагенту недійсний",
        _ => "Не ведеться"
    };

    /// <summary>
    /// Поточний стан калібрування для приладу й показника. Береться останнє калібрування показника;
    /// якщо для показника записів немає — останнє «загальне» (без коду показника). Без записів — NONE (обмежень немає).
    /// </summary>
    public static async Task<CalibrationCheck> CheckAsync(LisDbContext db, string? analyzerId, string testCode, DateTime? at = null)
    {
        if (string.IsNullOrEmpty(analyzerId)) return new CalibrationCheck();
        var now = at ?? DateTime.UtcNow;
        var all = await db.Calibrations.AsNoTracking().Include(c => c.ReagentLot)
            .Where(c => c.AnalyzerId == analyzerId && c.RecordState != RecordStates.Deleted && c.PerformedAt <= now && (c.TestCode == testCode || c.TestCode == null))
            .OrderByDescending(c => c.PerformedAt).ToListAsync();
        var scoped = all.Where(c => c.TestCode == testCode).ToList();
        var pick = (scoped.Count > 0 ? scoped : all).FirstOrDefault();
        if (pick == null) return new CalibrationCheck();
        var warnDays = (await db.Settings.AsNoTracking().Select(s => (int?)s.CalibrationWarnDays).FirstOrDefaultAsync()) ?? 7;
        var status = StatusOf(pick, now, warnDays);
        var analyzer = await db.Analyzers.AsNoTracking().Where(a => a.Id == analyzerId).Select(a => a.Name).FirstOrDefaultAsync() ?? "прилад";
        string? msg = status switch
        {
            CalibrationStatuses.Expired => $"Калібрування приладу «{analyzer}» для {testCode} протерміноване (діяло до {pick.ValidUntil:dd.MM.yyyy}). Виконайте калібрування і внесіть його в журнал.",
            CalibrationStatuses.Rejected => $"Останнє калібрування приладу «{analyzer}» для {testCode} від {pick.PerformedAt:dd.MM.yyyy} не прийнято. Повторіть калібрування.",
            CalibrationStatuses.LotInvalid => $"Лот реагенту {pick.ReagentLot?.LotNumber}, під який калібрувався прилад «{analyzer}» ({testCode}), прострочений або не активний. Виконайте калібрування під новий лот.",
            CalibrationStatuses.Expiring => $"Калібрування приладу «{analyzer}» для {testCode} спливає {pick.ValidUntil:dd.MM.yyyy}.",
            _ => null
        };
        return new CalibrationCheck { Status = status, Calibration = pick, Message = msg, DaysLeft = (int)Math.Floor((pick.ValidUntil - now).TotalDays) };
    }

    private async Task<int> WarnDaysAsync() => (await _db.Settings.AsNoTracking().Select(s => (int?)s.CalibrationWarnDays).FirstOrDefaultAsync()) ?? 7;

    private static object ToDto(LabCalibration c, DateTime now, int warnDays, Dictionary<string, string>? people = null)
    {
        var st = StatusOf(c, now, warnDays);
        return new
        {
            c.Id, c.AnalyzerId, analyzerName = c.Analyzer?.Name, c.TestCode, c.ReagentLotId, reagentLotNumber = c.ReagentLot?.LotNumber, reagentName = c.ReagentLot?.ReagentName,
            c.CalibratorName, c.CalibratorLot, c.PerformedAt, c.CalibrationValue, c.Unit, c.Accepted, c.RejectReason, c.ValidUntil, c.NextDueAt,
            c.PerformedById, performedByName = c.PerformedById != null && people != null && people.TryGetValue(c.PerformedById, out var n) ? n : null,
            c.Comment, status = st, statusText = StatusText(st), daysLeft = (int)Math.Floor((c.ValidUntil - now).TotalDays)
        };
    }

    private async Task<Dictionary<string, string>> PeopleAsync(IEnumerable<string?> ids)
    {
        var list = ids.Where(i => i != null).Select(i => i!).Distinct().ToList();
        return await _db.Employees.AsNoTracking().Where(e => list.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Caption ?? "");
    }

    public async Task<List<object>> ListAsync(string? analyzerId, string? testCode, string? status, DateTime? from, DateTime? to)
    {
        var q = _db.Calibrations.AsNoTracking().Include(c => c.Analyzer).Include(c => c.ReagentLot).Where(c => c.RecordState != RecordStates.Deleted);
        if (!string.IsNullOrEmpty(analyzerId)) q = q.Where(c => c.AnalyzerId == analyzerId);
        if (!string.IsNullOrEmpty(testCode)) q = q.Where(c => c.TestCode == testCode);
        if (from.HasValue) q = q.Where(c => c.PerformedAt >= from.Value);
        if (to.HasValue) q = q.Where(c => c.PerformedAt <= to.Value);
        var rows = await q.OrderByDescending(c => c.PerformedAt).ToListAsync();
        var now = DateTime.UtcNow; var warn = await WarnDaysAsync();
        var people = await PeopleAsync(rows.Select(r => r.PerformedById));
        return rows.Where(c => string.IsNullOrEmpty(status) || StatusOf(c, now, warn) == status).Select(c => ToDto(c, now, warn, people)).ToList();
    }

    public async Task<object> GetAsync(string id)
    {
        var c = await _db.Calibrations.AsNoTracking().Include(x => x.Analyzer).Include(x => x.ReagentLot).FirstOrDefaultAsync(x => x.Id == id && x.RecordState != RecordStates.Deleted)
                ?? throw NotFoundException.For("Калібрування", id);
        return ToDto(c, DateTime.UtcNow, await WarnDaysAsync(), await PeopleAsync(new[] { c.PerformedById }));
    }

    /// <summary>Поточний стан по кожній парі «прилад + показник» з журналу: для екрана, нагадувань і індикаторів.</summary>
    public async Task<List<object>> SummaryAsync(string? analyzerId)
    {
        var q = _db.Calibrations.AsNoTracking().Include(c => c.Analyzer).Include(c => c.ReagentLot).Where(c => c.RecordState != RecordStates.Deleted);
        if (!string.IsNullOrEmpty(analyzerId)) q = q.Where(c => c.AnalyzerId == analyzerId);
        var rows = await q.OrderByDescending(c => c.PerformedAt).ToListAsync();
        var now = DateTime.UtcNow; var warn = await WarnDaysAsync();
        return rows.GroupBy(c => new { c.AnalyzerId, TestCode = c.TestCode ?? "" })
            .Select(g => g.First()).OrderBy(c => c.Analyzer?.Name).ThenBy(c => c.TestCode).Select(c => ToDto(c, now, warn)).ToList();
    }

    public async Task<object> CheckEndpointAsync(string analyzerId, string testCode)
    {
        var chk = await CheckAsync(_db, analyzerId, testCode);
        var block = (await _db.Settings.AsNoTracking().Select(s => (bool?)s.BlockOnExpiredCalibration).FirstOrDefaultAsync()) ?? false;
        return new { status = chk.Status, statusText = StatusText(chk.Status), isProblem = chk.IsProblem, blocking = chk.IsProblem && block, message = chk.Message, daysLeft = chk.DaysLeft, calibrationId = chk.Calibration?.Id };
    }

    public async Task<object> CreateAsync(CalibrationRequest req)
    {
        _policy.Require("Внесення калібрування", WriteRoles);
        if (!await _db.Analyzers.AnyAsync(a => a.Id == req.AnalyzerId)) throw ValidationException.Field("analyzerId", "Оберіть аналізатор");
        var testCode = string.IsNullOrWhiteSpace(req.TestCode) ? null : req.TestCode.Trim();
        if (testCode != null && !await _db.Tests.AnyAsync(t => t.Code == testCode)) throw ValidationException.Field("testCode", $"Показник {testCode} не знайдено");
        if (string.IsNullOrWhiteSpace(req.CalibratorLot)) throw ValidationException.Field("calibratorLot", "Вкажіть лот калібратора");
        var now = DateTime.UtcNow;
        var performed = req.PerformedAt ?? now;
        if (performed > now.AddMinutes(5)) throw ValidationException.Field("performedAt", "Дата калібрування не може бути в майбутньому");
        if (!req.Accepted && string.IsNullOrWhiteSpace(req.RejectReason)) throw ValidationException.Field("rejectReason", "Для відхиленого калібрування вкажіть причину");
        if (req.ReagentLotId != null)
        {
            var lot = await _db.ReagentLots.AsNoTracking().FirstOrDefaultAsync(l => l.Id == req.ReagentLotId && l.RecordState != RecordStates.Deleted)
                      ?? throw ValidationException.Field("reagentLotId", "Лот реагенту не знайдено");
            if (lot.AnalyzerId != null && lot.AnalyzerId != req.AnalyzerId) throw ValidationException.Field("reagentLotId", "Лот реагенту належить іншому аналізатору");
        }
        DateTime validUntil;
        if (req.ValidUntil.HasValue) validUntil = req.ValidUntil.Value;
        else if (req.ValidDays is > 0) validUntil = performed.AddDays(req.ValidDays.Value);
        else throw ValidationException.Field("validUntil", "Вкажіть термін дії калібрування (дату або кількість днів)");
        if (validUntil <= performed) throw ValidationException.Field("validUntil", "Термін дії має бути пізніше дати калібрування");
        var c = new LabCalibration
        {
            AnalyzerId = req.AnalyzerId, TestCode = testCode, ReagentLotId = req.ReagentLotId, CalibratorName = req.CalibratorName, CalibratorLot = req.CalibratorLot.Trim(),
            PerformedAt = performed, CalibrationValue = req.CalibrationValue, Unit = req.Unit, Accepted = req.Accepted, RejectReason = req.Accepted ? null : req.RejectReason,
            ValidUntil = validUntil, NextDueAt = req.NextDueAt ?? validUntil, PerformedById = _current.EmployeeId, Comment = req.Comment
        };
        _db.Calibrations.Add(c);
        _audit.Log("CREATE", "lab_calibration", c.Id, null, new { c.AnalyzerId, c.TestCode, c.CalibratorLot, c.Accepted, c.ValidUntil, c.ReagentLotId });
        await _db.SaveChangesAsync();
        return await GetAsync(c.Id);
    }

    public async Task<object> UpdateAsync(string id, CalibrationUpdateRequest req)
    {
        _policy.Require("Редагування калібрування", WriteRoles);
        var c = await _db.Calibrations.FirstOrDefaultAsync(x => x.Id == id && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Калібрування", id);
        var before = new { c.Comment, c.NextDueAt };
        if (req.Comment != null) c.Comment = req.Comment;
        if (req.NextDueAt.HasValue) c.NextDueAt = req.NextDueAt.Value;
        _audit.Log("UPDATE", "lab_calibration", c.Id, before, req);
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task DeleteAsync(string id, string? reason)
    {
        _policy.Require("Видалення калібрування", LabRoles.Admin, LabRoles.Doctor);
        var c = await _db.Calibrations.FirstOrDefaultAsync(x => x.Id == id && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Калібрування", id);
        if (string.IsNullOrWhiteSpace(reason)) throw ValidationException.Field("reason", "Вкажіть причину видалення запису журналу");
        c.RecordState = RecordStates.Deleted;
        _audit.Log("SOFT_DELETE", "lab_calibration", c.Id, new { c.AnalyzerId, c.TestCode, c.CalibratorLot, c.ValidUntil }, null, reason);
        await _db.SaveChangesAsync();
    }
}
