// Підрозділи лабораторії (секції) та журнали відділень: нумерація за маскою з атомарними лічильниками
// lab_numerator (ключ секція+період), реєстрація при прийомі проби (одна позиція на замовлення+секція+проба)
using System.Globalization;
using System.Text.RegularExpressions;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class SectionRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string SectionType { get; set; } = "OTHER";
    public string? DepartmentId { get; set; }
    public string JournalMask { get; set; } = "{yyyy}-{seq6}";
    public string JournalResetPeriod { get; set; } = "YEAR";
    public string? SecondaryMask { get; set; }
    public bool AutoReleaseVerified { get; set; } = true;
    public string WorkflowTemplateCode { get; set; } = "CLINICAL";
    public bool IsActive { get; set; } = true;
}

public sealed class SectionJournalService
{
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;

    public SectionJournalService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current)
    {
        _db = db; _policy = policy; _audit = audit; _current = current;
    }

    // ------------------------------------------------------------------ sections CRUD
    public async Task<List<object>> ListAsync(bool? isActive)
    {
        var items = await _db.Sections.AsNoTracking().Include(s => s.Department).Where(s => s.RecordState != RecordStates.Deleted && (isActive == null || s.IsActive == isActive)).OrderBy(s => s.Name).ToListAsync();
        var counts = await _db.Tests.AsNoTracking().Where(t => t.LabSectionId != null).GroupBy(t => t.LabSectionId!).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        return items.Select(s => ToDto(s, counts.TryGetValue(s.Id, out var n) ? n : 0)).ToList();
    }

    public async Task<object> GetAsync(string id)
    {
        var s = await LoadAsync(id);
        return ToDto(s, await _db.Tests.CountAsync(t => t.LabSectionId == id));
    }

    public async Task<LabSection> LoadAsync(string idOrCode) =>
        await _db.Sections.Include(s => s.Department).FirstOrDefaultAsync(s => (s.Id == idOrCode || s.Code == idOrCode) && s.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Підрозділ лабораторії", idOrCode);

    private static object ToDto(LabSection s, int testsCount) => new
    {
        s.Id, s.Code, s.Name, s.SectionType, s.DepartmentId, departmentName = s.Department?.Caption, s.JournalMask, s.JournalResetPeriod, s.SecondaryMask,
        s.AutoReleaseVerified, s.WorkflowTemplateCode, s.IsActive, s.CreatedOn, testsCount, sampleNumber = FormatMask(s.JournalMask, DateTime.UtcNow, 1, 1)
    };

    public async Task<object> CreateAsync(SectionRequest req)
    {
        _policy.Require("Створення підрозділу лабораторії", LabRoles.Admin);
        Validate(req);
        if (await _db.Sections.AnyAsync(s => s.Code == req.Code && s.RecordState != RecordStates.Deleted)) throw new ConflictException($"Підрозділ із кодом {req.Code} вже існує");
        var s = new LabSection();
        Apply(s, req);
        _db.Sections.Add(s);
        _audit.Log("CREATE", "lab_section", s.Id, null, req);
        await _db.SaveChangesAsync();
        return await GetAsync(s.Id);
    }

    public async Task<object> UpdateAsync(string id, SectionRequest req)
    {
        _policy.Require("Редагування підрозділу лабораторії", LabRoles.Admin);
        var s = await LoadAsync(id);
        if (string.IsNullOrWhiteSpace(req.Code)) req.Code = s.Code;
        if (string.IsNullOrWhiteSpace(req.Name)) req.Name = s.Name;
        Validate(req);
        if (await _db.Sections.AnyAsync(x => x.Code == req.Code && x.Id != s.Id && x.RecordState != RecordStates.Deleted)) throw new ConflictException($"Підрозділ із кодом {req.Code} вже існує");
        var before = new { s.Code, s.Name, s.SectionType, s.JournalMask, s.JournalResetPeriod, s.AutoReleaseVerified, s.WorkflowTemplateCode, s.IsActive };
        Apply(s, req);
        _audit.Log("UPDATE", "lab_section", s.Id, before, req);
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<object> DeleteAsync(string id)
    {
        _policy.Require("Видалення підрозділу лабораторії", LabRoles.Admin);
        var s = await LoadAsync(id);
        var deps = await _db.Tests.CountAsync(t => t.LabSectionId == s.Id) + await _db.JournalEntries.CountAsync(j => j.LabSectionId == s.Id);
        if (deps > 0) { s.IsActive = false; _audit.Log("SOFT_DELETE", "lab_section", s.Id, null, new { deps }); await _db.SaveChangesAsync(); return new { id = s.Id, deleted = false, deactivated = true, dependencies = deps }; }
        _db.Sections.Remove(s);
        _audit.Log("DELETE", "lab_section", s.Id, new { s.Code }, null);
        await _db.SaveChangesAsync();
        return new { id = s.Id, deleted = true };
    }

    private static void Validate(SectionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Код та назва підрозділу обов'язкові");
        if (!SectionTypes.All.Contains(req.SectionType)) throw ValidationException.Field("sectionType", $"Тип: {string.Join(" | ", SectionTypes.All)}");
        if (!JournalResetPeriods.All.Contains(req.JournalResetPeriod)) throw ValidationException.Field("journalResetPeriod", "YEAR | MONTH | DAY | NEVER");
        if (!Regex.IsMatch(req.JournalMask, @"\{(seq\d*|dayseq\d*)\}")) throw ValidationException.Field("journalMask", "Маска має містити {seqN} або {dayseqN}, напр. {yyyy}-{seq6}");
    }

    private static void Apply(LabSection s, SectionRequest req)
    {
        s.Code = req.Code.Trim().ToUpperInvariant(); s.Name = req.Name.Trim(); s.SectionType = req.SectionType; s.DepartmentId = string.IsNullOrWhiteSpace(req.DepartmentId) ? null : req.DepartmentId;
        s.JournalMask = req.JournalMask; s.JournalResetPeriod = req.JournalResetPeriod; s.SecondaryMask = req.SecondaryMask; s.AutoReleaseVerified = req.AutoReleaseVerified;
        s.WorkflowTemplateCode = string.IsNullOrWhiteSpace(req.WorkflowTemplateCode) ? "CLINICAL" : req.WorkflowTemplateCode; s.IsActive = req.IsActive;
    }

    // ------------------------------------------------------------------ numbering
    public static string PeriodKey(string resetPeriod, DateTime at) => resetPeriod switch
    {
        JournalResetPeriods.Day => at.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
        JournalResetPeriods.Month => at.ToString("yyyyMM", CultureInfo.InvariantCulture),
        JournalResetPeriods.Year => at.ToString("yyyy", CultureInfo.InvariantCulture),
        _ => "ALL"
    };

    /// <summary>{yyyy} {yy} {MM} {dd} → дата; {seqN}/{seq} → послідовність періоду; {dayseqN}/{dayseq} → денний номер.</summary>
    public static string FormatMask(string mask, DateTime at, long seq, long daySeq)
    {
        var r = mask.Replace("{yyyy}", at.ToString("yyyy", CultureInfo.InvariantCulture)).Replace("{yy}", at.ToString("yy", CultureInfo.InvariantCulture))
                    .Replace("{MM}", at.ToString("MM", CultureInfo.InvariantCulture)).Replace("{dd}", at.ToString("dd", CultureInfo.InvariantCulture));
        r = Regex.Replace(r, @"\{dayseq(\d*)\}", m => Pad(daySeq, m.Groups[1].Value));
        r = Regex.Replace(r, @"\{seq(\d*)\}", m => Pad(seq, m.Groups[1].Value));
        return r;
    }

    private static string Pad(long v, string width) => string.IsNullOrEmpty(width) ? v.ToString(CultureInfo.InvariantCulture) : v.ToString(new string('0', int.Parse(width)), CultureInfo.InvariantCulture);

    /// <summary>Атомарний інкремент лічильника з автоскиданням за ключем періоду.</summary>
    private async Task<long> NextAsync(string code, string periodKey, string name)
    {
        if (!await _db.Numerators.AsNoTracking().AnyAsync(n => n.Code == code))
        {
            _db.Numerators.Add(new LabNumerator { Code = code, Name = name, Mask = "{seq}", CurrentValue = 0, PeriodKey = periodKey, ResetByPeriod = true });
            await _db.SaveChangesAsync();
        }
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE lab_numerator SET current_value = 0, period_key = {periodKey} WHERE code = {code} AND (period_key IS NULL OR period_key <> {periodKey})");
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE lab_numerator SET current_value = current_value + 1, modified_on = {DateTime.UtcNow} WHERE code = {code}");
        return await _db.Numerators.AsNoTracking().Where(n => n.Code == code).Select(n => n.CurrentValue).FirstAsync();
    }

    public async Task<(string number, long seq, int daySeq, string? secondary, string periodKey)> NextJournalNumberAsync(LabSection section, DateTime at)
    {
        var periodKey = PeriodKey(section.JournalResetPeriod, at);
        var seq = await NextAsync($"journal:{section.Code}", periodKey, $"Журнал {section.Name}");
        var daySeq = await NextAsync($"journal_day:{section.Code}", PeriodKey(JournalResetPeriods.Day, at), $"Денний номер {section.Name}");
        var number = FormatMask(section.JournalMask, at, seq, daySeq);
        var secondary = string.IsNullOrWhiteSpace(section.SecondaryMask) ? null : FormatMask(section.SecondaryMask, at, seq, daySeq);
        return (number, seq, (int)daySeq, secondary, periodKey);
    }

    /// <summary>Попередній перегляд нумерації за маскою без зміни лічильників (dry run).</summary>
    public async Task<object> RenumberPreviewAsync(string sectionId, string? mask, string? resetPeriod, DateTime? at, int count)
    {
        var section = await LoadAsync(sectionId);
        var m = string.IsNullOrWhiteSpace(mask) ? section.JournalMask : mask;
        var period = string.IsNullOrWhiteSpace(resetPeriod) ? section.JournalResetPeriod : resetPeriod;
        var date = at ?? DateTime.UtcNow;
        var periodKey = PeriodKey(period, date);
        var num = await _db.Numerators.AsNoTracking().FirstOrDefaultAsync(n => n.Code == $"journal:{section.Code}");
        var day = await _db.Numerators.AsNoTracking().FirstOrDefaultAsync(n => n.Code == $"journal_day:{section.Code}");
        var seqStart = num == null || num.PeriodKey != periodKey ? 0 : num.CurrentValue;
        var dayStart = day == null || day.PeriodKey != PeriodKey(JournalResetPeriods.Day, date) ? 0 : day.CurrentValue;
        var n = Math.Clamp(count, 1, 50);
        return new
        {
            sectionId = section.Id, sectionCode = section.Code, mask = m, resetPeriod = period, periodKey, currentSequence = seqStart, currentDaySequence = dayStart, dryRun = true,
            preview = Enumerable.Range(1, n).Select(i => new { seq = seqStart + i, daySeq = dayStart + i, journalNumber = FormatMask(m, date, seqStart + i, dayStart + i), secondary = string.IsNullOrWhiteSpace(section.SecondaryMask) ? null : FormatMask(section.SecondaryMask, date, seqStart + i, dayStart + i) })
        };
    }

    // ------------------------------------------------------------------ registration on receive
    /// <summary>Створює позиції журналу для кожної секції тестів проби (одна на замовлення+секція+проба).</summary>
    public async Task<List<LabSectionJournalEntry>> RegisterSampleAsync(LabOrderSample sample, LabOrder order)
    {
        var tests = order.Tests.Where(t => t.SampleId == sample.Id && t.Status != OrderTestStatuses.Rejected).ToList();
        if (tests.Count == 0) return new();
        var testIds = tests.Select(t => t.TestId).Distinct().ToList();
        var sections = await _db.Tests.AsNoTracking().Where(t => testIds.Contains(t.Id)).Select(t => new { t.Id, t.LabSectionId }).ToListAsync();
        var created = new List<LabSectionJournalEntry>();
        var now = DateTime.UtcNow;
        foreach (var grp in tests.GroupBy(t => sections.FirstOrDefault(s => s.Id == t.TestId)?.LabSectionId).Where(g => g.Key != null))
        {
            var sectionId = grp.Key!;
            var existing = _db.JournalEntries.Local.FirstOrDefault(j => j.OrderId == order.Id && j.LabSectionId == sectionId && j.SampleId == sample.Id)
                           ?? await _db.JournalEntries.FirstOrDefaultAsync(j => j.OrderId == order.Id && j.LabSectionId == sectionId && j.SampleId == sample.Id);
            if (existing != null)
            {
                var ids = existing.OrderTestIds; var changed = false;
                foreach (var t in grp) if (!ids.Contains(t.Id)) { ids.Add(t.Id); changed = true; }
                if (changed) existing.OrderTestIds = ids;
                continue;
            }
            var section = await _db.Sections.FirstOrDefaultAsync(s => s.Id == sectionId);
            if (section == null) continue;
            var (number, seq, daySeq, secondary, periodKey) = await NextJournalNumberAsync(section, now);
            var entry = new LabSectionJournalEntry
            {
                LabSectionId = section.Id, OrderId = order.Id, SampleId = sample.Id, OrderTestIds = grp.Select(t => t.Id).ToList(), JournalNumber = number, SequenceValue = seq,
                DayNumber = daySeq, SecondaryNumber = secondary, PeriodKey = periodKey, RegisteredAt = now, RegisteredById = _current.IsResolved ? _current.EmployeeId : null, Status = "REGISTERED"
            };
            _db.JournalEntries.Add(entry);
            created.Add(entry);
            _audit.Log("JOURNAL_REGISTER", "lab_section_journal_entry", entry.Id, null, new { section.Code, number, sample.Barcode, tests = grp.Select(t => t.TestCode) });
        }
        return created;
    }

    public async Task<PagedResult<object>> JournalAsync(string sectionId, DateTime? date, DateTime? from, DateTime? to, string? status, PagingQuery paging)
    {
        var section = await LoadAsync(sectionId);
        var q = _db.JournalEntries.AsNoTracking().Include(j => j.Order).ThenInclude(o => o!.Patient).Include(j => j.Sample).Where(j => j.LabSectionId == section.Id);
        if (date.HasValue) { var d = date.Value.Date; q = q.Where(j => j.RegisteredAt >= d && j.RegisteredAt < d.AddDays(1)); }
        if (from.HasValue) q = q.Where(j => j.RegisteredAt >= from.Value);
        if (to.HasValue) q = q.Where(j => j.RegisteredAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(j => j.Status == status.ToUpper());
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(j => j.RegisteredAt).Skip((paging.SafePage - 1) * paging.SafePageSize).Take(paging.SafePageSize).ToListAsync();
        var testIds = items.SelectMany(j => j.OrderTestIds).Distinct().ToList();
        var tests = await _db.OrderTests.AsNoTracking().Include(t => t.Result).Where(t => testIds.Contains(t.Id)).ToListAsync();
        return new PagedResult<object>
        {
            Total = total, Page = paging.SafePage, PageSize = paging.SafePageSize,
            Items = items.Select(j => (object)new
            {
                j.Id, j.JournalNumber, j.DayNumber, j.SecondaryNumber, j.RegisteredAt, j.RegisteredById, j.Status, j.OrderId, orderNumber = j.Order?.OrderNumber, isCito = j.Order?.IsUrgentCito,
                patientName = j.Order?.Patient?.Caption, patientAgeGender = DtoMapper.AgeGender(j.Order?.Patient, j.RegisteredAt), barcode = j.Sample?.Barcode, sampleStatus = j.Sample?.Status,
                tests = tests.Where(t => j.OrderTestIds.Contains(t.Id)).Select(t => new { t.Id, t.TestCode, t.TestName, t.Status, value = t.Result == null ? null : DtoMapper.FormatValue(t.Result.NumericValue, t.Result.StringValue, 2), flag = t.Result?.Flag, t.ReleasedAt })
            }).ToList()
        };
    }

    public async Task<List<object>> OrderEntriesAsync(string orderId)
    {
        if (!await _db.Orders.AnyAsync(o => o.Id == orderId)) throw NotFoundException.For("Замовлення", orderId);
        var items = await _db.JournalEntries.AsNoTracking().Include(j => j.Section).Include(j => j.Sample).Where(j => j.OrderId == orderId).OrderBy(j => j.RegisteredAt).ToListAsync();
        return items.Select(j => (object)new { j.Id, j.LabSectionId, sectionCode = j.Section?.Code, sectionName = j.Section?.Name, j.JournalNumber, j.DayNumber, j.SecondaryNumber, j.RegisteredAt, j.Status, j.SampleId, barcode = j.Sample?.Barcode, orderTestIds = j.OrderTestIds }).ToList();
    }

    public async Task<object> UpdateEntryStatusAsync(string entryId, string status)
    {
        _policy.Require("Зміна статусу позиції журналу", LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician);
        var e = await _db.JournalEntries.FirstOrDefaultAsync(j => j.Id == entryId) ?? throw NotFoundException.For("Позиція журналу", entryId);
        var next = status.ToUpperInvariant();
        if (!new[] { "REGISTERED", "IN_PROGRESS", "COMPLETED", "CANCELLED" }.Contains(next)) throw ValidationException.Field("status", "REGISTERED | IN_PROGRESS | COMPLETED | CANCELLED");
        var before = e.Status; e.Status = next;
        _audit.Log("UPDATE", "lab_section_journal_entry", e.Id, new { status = before }, new { status = next });
        await _db.SaveChangesAsync();
        return new { e.Id, e.JournalNumber, e.Status };
    }
}
