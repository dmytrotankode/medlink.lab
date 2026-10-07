// Пацієнти (локальна картка МІС): CRUD, історія, тренди; налаштування лабораторії; нумератори
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class PatientService
{
    private static readonly string[] EditorRoles = { LabRoles.Admin, LabRoles.Registrar, LabRoles.Doctor, LabRoles.Phlebotomist };
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;

    public PatientService(LisDbContext db, IRolePolicy policy, IAuditService audit) { _db = db; _policy = policy; _audit = audit; }

    public async Task<PagedResult<PatientDto>> SearchAsync(string? search, PagingQuery paging)
    {
        var q = _db.Patients.AsNoTracking().Where(p => p.RecordState != RecordStates.Deleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p => (p.Caption != null && p.Caption.Contains(s)) || (p.Person!.Phone != null && p.Person.Phone.Contains(s)) || (p.Person.Ipn != null && p.Person.Ipn.Contains(s)));
        }
        var paged = await q.OrderBy(p => p.Caption).ToPagedAsync(paging);
        return new PagedResult<PatientDto> { Total = paged.Total, Page = paged.Page, PageSize = paged.PageSize, Items = paged.Items.Select(p => DtoMapper.ToDto(p)).ToList() };
    }

    public async Task<MisPatientCard> LoadAsync(string id) => await _db.Patients.FirstOrDefaultAsync(p => p.Id == id && p.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Пацієнт", id);
    public async Task<PatientDto> GetAsync(string id) => DtoMapper.ToDto(await LoadAsync(id));

    public async Task<PatientDto> CreateAsync(NewPatientRequest req)
    {
        _policy.Require("Створення картки пацієнта", EditorRoles);
        if (string.IsNullOrWhiteSpace(req.LastName) || string.IsNullOrWhiteSpace(req.FirstName)) throw new ValidationException("Прізвище та ім'я обов'язкові");
        req.Gender = OrderService.NormalizeGender(req.Gender);
        var p = MedLinkPeople.NewPatient(req, MedLinkDefaults.OrganizationId);
        _db.Patients.Add(p);
        _audit.Log("CREATE", "mis_patient_card", p.Id, null, req);
        await _db.SaveChangesAsync();
        return DtoMapper.ToDto(p);
    }

    public async Task<PatientDto> UpdateAsync(string id, NewPatientRequest req)
    {
        _policy.Require("Редагування картки пацієнта", EditorRoles);
        var p = await LoadAsync(id);
        var before = MedLinkPeople.ToDto(p);
        if (!string.IsNullOrWhiteSpace(req.Gender)) req.Gender = OrderService.NormalizeGender(req.Gender);
        MedLinkPeople.Apply(p, req, isNew: false);
        _audit.Log("UPDATE", "mis_patient_card", p.Id, before, req);
        await _db.SaveChangesAsync();
        return DtoMapper.ToDto(p);
    }

    public async Task DeleteAsync(string id)
    {
        _policy.Require("Видалення картки пацієнта", LabRoles.Admin, LabRoles.Registrar);
        var p = await LoadAsync(id);
        if (await _db.Orders.AnyAsync(o => o.PatientId == id)) throw new ConflictException("Пацієнт має замовлення — видалення неможливе (картку деактивовано)");
        _db.Patients.Remove(p);
        if (p.Person != null && !await _db.Patients.AnyAsync(x => x.PersonId == p.PersonId && x.Id != p.Id) && !await _db.Employees.AnyAsync(e => e.PersonId == p.PersonId))
            _db.Persons.Remove(p.Person);
        _audit.Log("DELETE", "mis_patient_card", id, new { p.Caption }, null);
        await _db.SaveChangesAsync();
    }

    public async Task<object> HistoryAsync(string patientId, bool releasedOnly)
    {
        await LoadAsync(patientId);
        var tests = await _db.OrderTests.AsNoTracking().Include(t => t.Order).Include(t => t.Test).Include(t => t.Result).Include(t => t.Profile)
            .Where(t => t.Order!.PatientId == patientId && t.Result != null && (!releasedOnly || t.Order.Status == OrderStatuses.Released) && t.Status != OrderTestStatuses.Rejected)
            .OrderByDescending(t => t.Order!.OrderDatetime).ToListAsync();
        var byTest = tests.GroupBy(t => t.TestCode).Select(g =>
        {
            var ordered = g.OrderBy(t => t.Result!.EnteredAt).ToList();
            var last = ordered.Last(); var prev = ordered.Count > 1 ? ordered[^2] : null;
            return new
            {
                testCode = g.Key, testName = last.TestName, unit = last.Result!.Unit, count = g.Count(), lastValue = DtoMapper.FormatValue(last.Result.NumericValue, last.Result.StringValue, last.Test?.DecimalPlaces ?? 2),
                lastAt = last.Result.EnteredAt, lastFlag = last.Result.Flag, referenceDisplay = last.Result.ReferenceDisplay,
                trend = prev?.Result?.NumericValue != null && last.Result.NumericValue != null ? (last.Result.NumericValue > prev.Result.NumericValue ? "UP" : last.Result.NumericValue < prev.Result.NumericValue ? "DOWN" : "SAME") : null,
                hasTrendChart = ordered.Count(t => t.Result!.NumericValue != null) >= 2
            };
        }).OrderBy(x => x.testCode).ToList();
        var orders = tests.GroupBy(t => t.Order!).Select(g => new { g.Key.Id, g.Key.OrderNumber, g.Key.OrderDatetime, g.Key.Status, g.Key.ReleasedAt, profiles = g.Select(t => t.Profile?.Name).Where(n => n != null).Distinct(), tests = g.Count(), abnormal = g.Count(t => Core.Clinical.ResultFlags.IsAbnormal(t.Result!.Flag)) }).OrderByDescending(o => o.OrderDatetime).ToList();
        return new { patientId, orders, tests = byTest };
    }

    public async Task<List<object>> TrendAsync(string patientId, string testCode, bool releasedOnly)
    {
        await LoadAsync(patientId);
        var results = await _db.Results.AsNoTracking().Include(r => r.OrderTest).ThenInclude(t => t!.Order)
            .Where(r => r.OrderTest!.TestCode == testCode && r.OrderTest.Order!.PatientId == patientId && r.OrderTest.Status != OrderTestStatuses.Rejected && (!releasedOnly || r.OrderTest.Order.Status == OrderStatuses.Released))
            .OrderBy(r => r.EnteredAt).ToListAsync();
        return results.Select(r => new { at = r.EnteredAt, value = r.NumericValue, stringValue = r.StringValue, normLow = r.NormLow, normHigh = r.NormHigh, flag = r.Flag, unit = r.Unit, orderNumber = r.OrderTest?.Order?.OrderNumber, orderId = r.OrderTest?.OrderId }).ToList<object>();
    }
}

public sealed class LabSettingsRequest
{
    public string? Name { get; set; }
    public string? Edrpou { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LicenseNumber { get; set; }
    public string? LogoBase64 { get; set; }
    public string? DirectorName { get; set; }
    public string? WorkingHours { get; set; }
    public string? OrderNumberMask { get; set; }
    public string? BarcodePrefix { get; set; }
    public string? ReportFooter { get; set; }
    public string? PanicPhone { get; set; }
    public double? ColdChainMinC { get; set; }
    public double? ColdChainMaxC { get; set; }
    public string? LabelPrinterHost { get; set; }
    public int? LabelPrinterPort { get; set; }
    public string? LabelPrinterName { get; set; }
    public Dictionary<string, bool>? ReportTemplates { get; set; }
}

public sealed class NumeratorRequest
{
    public string? Name { get; set; }
    public string? Mask { get; set; }
    public long? CurrentValue { get; set; }
    public bool? ResetByPeriod { get; set; }
}

public sealed class SettingsService
{
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;

    public SettingsService(LisDbContext db, IRolePolicy policy, IAuditService audit) { _db = db; _policy = policy; _audit = audit; }

    public async Task<LabSettings> GetAsync() => await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();

    public async Task<LabSettings> UpdateAsync(LabSettingsRequest req)
    {
        _policy.Require("Зміна налаштувань лабораторії", LabRoles.Admin);
        var s = await _db.Settings.FirstOrDefaultAsync();
        if (s == null) { s = new LabSettings(); _db.Settings.Add(s); }
        var before = new { s.Name, s.Edrpou, s.Address, s.Phone, s.Email, s.LicenseNumber, s.DirectorName, s.OrderNumberMask, s.PanicPhone, s.ColdChainMinC, s.ColdChainMaxC };
        if (req.Name != null) s.Name = req.Name; if (req.Edrpou != null) s.Edrpou = req.Edrpou; if (req.Address != null) s.Address = req.Address; if (req.Phone != null) s.Phone = req.Phone;
        if (req.Email != null) s.Email = req.Email; if (req.LicenseNumber != null) s.LicenseNumber = req.LicenseNumber; if (req.LogoBase64 != null) s.LogoBase64 = req.LogoBase64;
        if (req.DirectorName != null) s.DirectorName = req.DirectorName; if (req.WorkingHours != null) s.WorkingHours = req.WorkingHours; if (req.BarcodePrefix != null) s.BarcodePrefix = req.BarcodePrefix;
        if (req.ReportFooter != null) s.ReportFooter = req.ReportFooter; if (req.PanicPhone != null) s.PanicPhone = req.PanicPhone;
        if (req.ColdChainMinC.HasValue) s.ColdChainMinC = req.ColdChainMinC.Value; if (req.ColdChainMaxC.HasValue) s.ColdChainMaxC = req.ColdChainMaxC.Value;
        if (req.LabelPrinterHost != null) s.LabelPrinterHost = req.LabelPrinterHost; if (req.LabelPrinterPort.HasValue) s.LabelPrinterPort = req.LabelPrinterPort.Value; if (req.LabelPrinterName != null) s.LabelPrinterName = req.LabelPrinterName;
        if (req.ReportTemplates != null) { var rt = s.ReportTemplates; foreach (var kv in req.ReportTemplates) rt[kv.Key] = kv.Value; s.ReportTemplates = rt; }
        if (!string.IsNullOrWhiteSpace(req.OrderNumberMask))
        {
            if (!req.OrderNumberMask.Contains("{0")) throw ValidationException.Field("orderNumberMask", "Маска має містити лічильник, напр. {yyMM}-{000000}");
            s.OrderNumberMask = req.OrderNumberMask;
            var num = await _db.Numerators.FirstOrDefaultAsync(n => n.Code == NumeratorService.OrderNumerator);
            if (num != null) num.Mask = req.OrderNumberMask;
        }
        _audit.Log("UPDATE", "lab_settings", s.Id, before, req);
        await _db.SaveChangesAsync();
        return s;
    }

    public async Task<List<LabNumerator>> NumeratorsAsync() => await _db.Numerators.AsNoTracking().OrderBy(n => n.Code).ToListAsync();

    public async Task<LabNumerator> UpdateNumeratorAsync(string code, NumeratorRequest req)
    {
        _policy.Require("Зміна нумератора", LabRoles.Admin);
        var n = await _db.Numerators.FirstOrDefaultAsync(x => x.Code == code) ?? throw NotFoundException.For("Нумератор", code);
        var before = new { n.Name, n.Mask, n.CurrentValue, n.ResetByPeriod };
        if (req.Name != null) n.Name = req.Name;
        if (!string.IsNullOrWhiteSpace(req.Mask)) n.Mask = req.Mask;
        if (req.CurrentValue.HasValue) n.CurrentValue = req.CurrentValue.Value;
        if (req.ResetByPeriod.HasValue) n.ResetByPeriod = req.ResetByPeriod.Value;
        n.ModifiedOn = DateTime.UtcNow;
        _audit.Log("UPDATE", "lab_numerator", code, before, req);
        await _db.SaveChangesAsync();
        return n;
    }

    public async Task<List<LabCounter>> CountersAsync() => await _db.Counters.AsNoTracking().OrderBy(c => c.CounterCode).ToListAsync();
}
