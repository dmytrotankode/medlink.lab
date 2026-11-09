// Дублікати пацієнтів (пошук, FR-GAP-001), згоди (FR-GAP-003), реєстр СОП (FR-GAP-041), допуски персоналу (FR-GAP-040)
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class ConsentRequest
{
    public string Kind { get; set; } = "";
    public bool IsGranted { get; set; }
    public string? Source { get; set; }
    public string? Note { get; set; }
}

public sealed class SopRequest
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Version { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? ReviewDueAt { get; set; }
    public string? DocumentUrl { get; set; }
    public string? Description { get; set; }
    public string? TargetKind { get; set; }
    public string? TargetCode { get; set; }
}

public sealed class CompetencyRequest
{
    public string EmployeeId { get; set; } = "";
    public string? Kind { get; set; }
    public string TargetCode { get; set; } = "";
    public DateTime? AssessedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? Note { get; set; }
}

public sealed class QualityRegistryService
{
    public static readonly string[] ConsentKinds = { "DATA_PROCESSING", "RESULTS_PHONE", "RESULTS_SMS", "RESULTS_EMAIL", "RESULTS_VIBER" };
    private static readonly string[] ConsentSources = { "PAPER", "ELECTRONIC", "VERBAL" };
    private static readonly string[] CompetencyKinds = { "TEST", "ANALYZER", "SECTION", "PROCEDURE" };
    private static readonly string[] SopTargets = { "TEST", "ANALYZER", "SECTION", "PROCESS" };
    private static readonly string[] ManageRoles = { LabRoles.Admin, LabRoles.Doctor };

    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;

    public QualityRegistryService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current)
    {
        _db = db; _policy = policy; _audit = audit; _current = current;
    }

    // ------------------------------------------------------------------ дублікати пацієнтів
    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();
    private static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    /// <summary>Кандидати у дублікати за ІПН / ПІБ + датою народження / телефоном + датою народження. Нічого не змінює.</summary>
    public async Task<object> FindDuplicatesAsync(string? lastName, string? firstName, DateTime? birthday, string? phone, string? ipn, string? excludePatientId = null)
    {
        _policy.Require("Пошук дублікатів пацієнтів", LabRoles.Admin, LabRoles.Doctor, LabRoles.Registrar, LabRoles.Technician);
        var ln = Norm(lastName); var fn = Norm(firstName); var ph = Digits(phone); var ip = (ipn ?? "").Trim();
        if (ln == "" && ip == "" && ph.Length < 7) throw new ValidationException("Вкажіть прізвище, ІПН або телефон (від 7 цифр) для пошуку дублікатів");
        var q = _db.Patients.AsNoTracking().Include(p => p.Person).Where(p => p.RecordState != RecordStates.Deleted);
        if (excludePatientId != null) q = q.Where(p => p.Id != excludePatientId);
        // попередній відбір у БД: збіг прізвища, ІПН або дати народження; точне порівняння — нижче в пам'яті
        var pre = await q.Where(p => (ip != "" && p.Person!.Ipn == ip)
                                     || (ln != "" && p.Person!.LastName != null && p.Person.LastName.ToLower() == ln)
                                     || (birthday != null && (p.Birthday == birthday || p.Person!.Birthday == birthday))
                                     || (ph.Length >= 7 && p.Person!.Phone != null)).Take(2000).ToListAsync();
        var res = new List<(int Score, object Item)>();
        foreach (var p in pre)
        {
            var reasons = new List<string>(); var score = 0;
            var pb = p.Birthday ?? p.Person?.Birthday;
            if (ip != "" && p.Person?.Ipn == ip) { reasons.Add("збіг ІПН"); score += 100; }
            var sameName = ln != "" && Norm(p.Person?.LastName) == ln && (fn == "" || Norm(p.Person?.Name) == fn);
            var sameBirth = birthday != null && pb?.Date == birthday.Value.Date;
            if (sameName && sameBirth) { reasons.Add("збіг ПІБ і дати народження"); score += 80; }
            else if (sameName && fn != "") { reasons.Add("збіг ПІБ"); score += 30; }
            if (ph.Length >= 7 && Digits(p.Person?.Phone).EndsWith(ph.Length > 9 ? ph[^9..] : ph)) { reasons.Add("збіг телефону"); score += sameBirth ? 60 : 25; }
            if (score >= 60 || (score >= 30 && reasons.Count > 0 && sameBirth))
                res.Add((score, new { patientId = p.Id, name = p.Caption, birthday = pb, phone = p.Person?.Phone, ipn = p.Person?.Ipn, regNumber = p.RegNumber, score, reasons }));
        }
        return new { total = res.Count, items = res.OrderByDescending(x => x.Score).Select(x => x.Item).ToList() };
    }

    public async Task<object> DuplicatesOfAsync(string patientId)
    {
        var p = await _db.Patients.AsNoTracking().Include(x => x.Person).FirstOrDefaultAsync(x => x.Id == patientId) ?? throw NotFoundException.For("Пацієнт", patientId);
        return await FindDuplicatesAsync(p.Person?.LastName, p.Person?.Name, p.Birthday ?? p.Person?.Birthday, p.Person?.Phone, p.Person?.Ipn, patientId);
    }

    // ------------------------------------------------------------------ згоди
    public async Task<object> ConsentsAsync(string patientId)
    {
        if (!await _db.Patients.AnyAsync(p => p.Id == patientId)) throw NotFoundException.For("Пацієнт", patientId);
        var rows = await _db.PatientConsents.AsNoTracking().Where(c => c.PatientId == patientId).ToListAsync();
        return new
        {
            patientId,
            items = ConsentKinds.Select(k =>
            {
                var c = rows.FirstOrDefault(r => r.Kind == k);
                return new { kind = k, isGranted = c?.IsGranted ?? false, c?.GrantedAt, c?.RevokedAt, source = c?.Source, c?.Note, recorded = c != null };
            }).ToList()
        };
    }

    /// <summary>Фіксує або відкликає згоду; відкликання зберігає дату, повторна згода очищує її. Кожна зміна — в аудит.</summary>
    public async Task<object> SetConsentAsync(string patientId, ConsentRequest req)
    {
        _policy.Require("Облік згоди пацієнта", LabRoles.Admin, LabRoles.Doctor, LabRoles.Registrar, LabRoles.Technician, LabRoles.Phlebotomist);
        if (!await _db.Patients.AnyAsync(p => p.Id == patientId)) throw NotFoundException.For("Пацієнт", patientId);
        var kind = (req.Kind ?? "").Trim().ToUpperInvariant();
        if (!ConsentKinds.Contains(kind)) throw ValidationException.Field("kind", "Вид згоди: обробка даних, результати телефоном/SMS/e-mail/Viber");
        var source = string.IsNullOrWhiteSpace(req.Source) ? "PAPER" : req.Source.Trim().ToUpperInvariant();
        if (!ConsentSources.Contains(source)) throw ValidationException.Field("source", "Джерело згоди: паперова, електронна або усна");
        var c = await _db.PatientConsents.FirstOrDefaultAsync(x => x.PatientId == patientId && x.Kind == kind);
        var before = c == null ? null : new { c.IsGranted, c.GrantedAt, c.RevokedAt };
        if (c == null) { c = new LabPatientConsent { PatientId = patientId, Kind = kind }; _db.PatientConsents.Add(c); }
        if (req.IsGranted) { c.IsGranted = true; c.GrantedAt = DateTime.UtcNow; c.RevokedAt = null; }
        else { c.IsGranted = false; c.RevokedAt = DateTime.UtcNow; }
        c.Source = source; c.Note = req.Note; c.RecordedById = _current.EmployeeId;
        _audit.Log(req.IsGranted ? "CONSENT_GRANT" : "CONSENT_REVOKE", "lab_patient_consent", c.Id, before, new { kind, c.IsGranted, source }, req.Note);
        await _db.SaveChangesAsync();
        return new { c.Id, c.PatientId, c.Kind, c.IsGranted, c.GrantedAt, c.RevokedAt, c.Source };
    }

    /// <summary>Чи можна повідомляти результат каналом (для сповіщень). Без запису про згоду — не можна.</summary>
    public async Task<bool> HasConsentAsync(string patientId, string kind) =>
        await _db.PatientConsents.AsNoTracking().AnyAsync(c => c.PatientId == patientId && c.Kind == kind && c.IsGranted);

    // ------------------------------------------------------------------ СОП
    public async Task<List<object>> SopsAsync(string? status, string? targetKind, string? targetCode)
    {
        var q = _db.Sops.AsNoTracking().Where(s => s.RecordState != RecordStates.Deleted);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(s => s.Status == status.ToUpper());
        if (!string.IsNullOrWhiteSpace(targetKind)) q = q.Where(s => s.TargetKind == targetKind.ToUpper());
        if (!string.IsNullOrWhiteSpace(targetCode)) q = q.Where(s => s.TargetCode == targetCode);
        var rows = await q.OrderBy(s => s.Code).ThenByDescending(s => s.EffectiveFrom).ToListAsync();
        return rows.Select(s => (object)new
        {
            s.Id, s.Code, s.Title, s.Version, s.Status, s.EffectiveFrom, s.ReviewDueAt, s.DocumentUrl, s.Description, s.TargetKind, s.TargetCode,
            reviewOverdue = s.Status == "ACTIVE" && s.ReviewDueAt < DateTime.UtcNow
        }).ToList();
    }

    public async Task<object> CreateSopAsync(SopRequest req)
    {
        _policy.Require("Реєстрація СОП", ManageRoles);
        if (string.IsNullOrWhiteSpace(req.Code)) throw ValidationException.Field("code", "Вкажіть код СОП");
        if (string.IsNullOrWhiteSpace(req.Title)) throw ValidationException.Field("title", "Вкажіть назву СОП");
        var version = string.IsNullOrWhiteSpace(req.Version) ? "1.0" : req.Version.Trim();
        var code = req.Code.Trim().ToUpperInvariant();
        if (await _db.Sops.AnyAsync(s => s.Code == code && s.Version == version && s.RecordState != RecordStates.Deleted)) throw new ConflictException($"СОП {code} версії {version} уже існує");
        var tk = string.IsNullOrWhiteSpace(req.TargetKind) ? null : req.TargetKind.Trim().ToUpperInvariant();
        if (tk != null && !SopTargets.Contains(tk)) throw ValidationException.Field("targetKind", "Застосування СОП: показник, прилад, розділ або процес");
        var s = new LabSop { Code = code, Title = req.Title.Trim(), Version = version, Status = "DRAFT", EffectiveFrom = req.EffectiveFrom, ReviewDueAt = req.ReviewDueAt, DocumentUrl = req.DocumentUrl, Description = req.Description, TargetKind = tk, TargetCode = req.TargetCode };
        _db.Sops.Add(s);
        _audit.Log("CREATE", "lab_sop", s.Id, null, new { s.Code, s.Version });
        await _db.SaveChangesAsync();
        return new { s.Id, s.Code, s.Version, s.Status };
    }

    /// <summary>Затвердження: версія стає ACTIVE, попередня активна версія цього коду — ARCHIVED.</summary>
    public async Task<object> ActivateSopAsync(string id)
    {
        _policy.Require("Затвердження СОП", ManageRoles);
        var s = await _db.Sops.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("СОП", id);
        if (s.Status == "ACTIVE") throw new ConflictException("Ця версія СОП уже діє");
        if (s.Status == "ARCHIVED") throw new ConflictException("Архівну версію СОП не можна затвердити — створіть нову версію");
        foreach (var old in await _db.Sops.Where(x => x.Code == s.Code && x.Status == "ACTIVE").ToListAsync()) { old.Status = "ARCHIVED"; _audit.Log("ARCHIVE", "lab_sop", old.Id, new { old.Version }, new { old.Status }); }
        s.Status = "ACTIVE"; s.ApprovedById = _current.EmployeeId; s.EffectiveFrom ??= DateTime.UtcNow;
        _audit.Log("ACTIVATE", "lab_sop", s.Id, null, new { s.Code, s.Version });
        await _db.SaveChangesAsync();
        return new { s.Id, s.Code, s.Version, s.Status, s.EffectiveFrom };
    }

    // ------------------------------------------------------------------ допуски персоналу
    public async Task<List<object>> CompetenciesAsync(string? employeeId, string? kind, string? targetCode)
    {
        var q = _db.Competencies.AsNoTracking().Where(c => c.RecordState != RecordStates.Deleted);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(c => c.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(kind)) q = q.Where(c => c.Kind == kind.ToUpper());
        if (!string.IsNullOrWhiteSpace(targetCode)) q = q.Where(c => c.TargetCode == targetCode);
        var rows = await q.OrderBy(c => c.EmployeeId).ThenBy(c => c.Kind).ThenBy(c => c.TargetCode).ToListAsync();
        var names = await _db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Caption ?? "");
        return rows.Select(c => (object)new
        {
            c.Id, c.EmployeeId, employeeName = names.GetValueOrDefault(c.EmployeeId), c.Kind, c.TargetCode, c.AssessedAt, c.ValidUntil, c.Note, c.IsActive,
            isValid = c.IsActive && (c.ValidUntil == null || c.ValidUntil >= DateTime.UtcNow)
        }).ToList();
    }

    public async Task<object> GrantCompetencyAsync(CompetencyRequest req)
    {
        _policy.Require("Надання допуску", ManageRoles);
        if (string.IsNullOrWhiteSpace(req.EmployeeId) || !await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId)) throw ValidationException.Field("employeeId", "Оберіть співробітника");
        if (string.IsNullOrWhiteSpace(req.TargetCode)) throw ValidationException.Field("targetCode", "Вкажіть показник, прилад або розділ");
        var kind = string.IsNullOrWhiteSpace(req.Kind) ? "TEST" : req.Kind.Trim().ToUpperInvariant();
        if (!CompetencyKinds.Contains(kind)) throw ValidationException.Field("kind", "Вид допуску: показник, прилад, розділ або процедура");
        var assessed = req.AssessedAt ?? DateTime.UtcNow;
        if (req.ValidUntil != null && req.ValidUntil < assessed) throw ValidationException.Field("validUntil", "Дата завершення допуску раніше за дату оцінки");
        var c = await _db.Competencies.FirstOrDefaultAsync(x => x.EmployeeId == req.EmployeeId && x.Kind == kind && x.TargetCode == req.TargetCode && x.RecordState != RecordStates.Deleted);
        if (c == null) { c = new LabStaffCompetency { EmployeeId = req.EmployeeId, Kind = kind, TargetCode = req.TargetCode }; _db.Competencies.Add(c); }
        c.AssessedAt = assessed; c.ValidUntil = req.ValidUntil; c.AssessorId = _current.EmployeeId; c.Note = req.Note; c.IsActive = true;
        _audit.Log("GRANT_COMPETENCY", "lab_staff_competency", c.Id, null, new { c.EmployeeId, kind, c.TargetCode, c.ValidUntil });
        await _db.SaveChangesAsync();
        return new { c.Id, c.EmployeeId, c.Kind, c.TargetCode, c.AssessedAt, c.ValidUntil };
    }

    public async Task RevokeCompetencyAsync(string id, string? reason)
    {
        _policy.Require("Скасування допуску", ManageRoles);
        var c = await _db.Competencies.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Допуск", id);
        if (string.IsNullOrWhiteSpace(reason)) throw ValidationException.Field("reason", "Вкажіть причину скасування допуску");
        c.IsActive = false;
        _audit.Log("REVOKE_COMPETENCY", "lab_staff_competency", c.Id, null, new { c.EmployeeId, c.Kind, c.TargetCode }, reason);
        await _db.SaveChangesAsync();
    }

    /// <summary>Перевірка допуску співробітника (для екранів і майбутнього блокування верифікації).</summary>
    public async Task<object> CheckCompetencyAsync(string employeeId, string kind, string targetCode)
    {
        var k = (kind ?? "").ToUpperInvariant();
        var c = await _db.Competencies.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Kind == k && x.TargetCode == targetCode && x.IsActive && x.RecordState != RecordStates.Deleted);
        if (c == null) return new { allowed = false, reason = "Допуску немає" };
        if (c.ValidUntil != null && c.ValidUntil < DateTime.UtcNow) return new { allowed = false, reason = $"Допуск закінчився {c.ValidUntil:dd.MM.yyyy}" };
        return new { allowed = true, reason = (string?)null };
    }
}
