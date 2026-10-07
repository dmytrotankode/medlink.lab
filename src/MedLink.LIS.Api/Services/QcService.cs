// Внутрішній контроль якості: матеріали/цілі (CRUD), результати з оцінкою Вестгарда, lockout, Леві-Дженнінгс, звіт
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Core.Clinical;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class QcTargetRequest
{
    public string TestCode { get; set; } = "";
    public double TargetMean { get; set; }
    public double TargetSd { get; set; }
    public string? Unit { get; set; }
    public double? TeaPct { get; set; }
}

public sealed class QcMaterialRequest
{
    public string AnalyzerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Level { get; set; } = "LEVEL_2_NORMAL";
    public string LotNumber { get; set; } = "";
    public string? Manufacturer { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime? OpenedAt { get; set; }
    public int OpenStabilityDays { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public List<QcTargetRequest> Targets { get; set; } = new();
}

public sealed class QcResultRequest
{
    public string QcMaterialId { get; set; } = "";
    public string TestCode { get; set; } = "";
    public double MeasuredValue { get; set; }
    public DateTime? RunAt { get; set; }
    public string? OperatorId { get; set; }
}

public sealed class LockoutResolveRequest
{
    public string Cause { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Comment { get; set; }
}

public sealed class QcService
{
    private static readonly string[] QcRoles = { LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician };
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;

    public QcService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current)
    {
        _db = db; _policy = policy; _audit = audit; _current = current;
    }

    // ------------------------------------------------------------------ materials
    public async Task<List<LabQcMaterial>> MaterialsAsync(string? analyzerId, bool? isActive) =>
        await _db.QcMaterials.AsNoTracking().Include(m => m.Targets).Include(m => m.Analyzer)
            .Where(m => m.RecordState != RecordStates.Deleted && (analyzerId == null || m.AnalyzerId == analyzerId) && (isActive == null || m.IsActive == isActive))
            .OrderBy(m => m.Analyzer!.Name).ThenBy(m => m.Level).ToListAsync();

    public async Task<LabQcMaterial> MaterialAsync(string id) =>
        await _db.QcMaterials.AsNoTracking().Include(m => m.Targets).Include(m => m.Analyzer).FirstOrDefaultAsync(m => m.Id == id) ?? throw NotFoundException.For("Контрольний матеріал", id);

    public async Task<LabQcMaterial> CreateMaterialAsync(QcMaterialRequest req)
    {
        _policy.Require("Створення контрольного матеріалу", QcRoles);
        if (!await _db.Analyzers.AnyAsync(a => a.Id == req.AnalyzerId)) throw ValidationException.Field("analyzerId", "Аналізатор не знайдено");
        if (string.IsNullOrWhiteSpace(req.LotNumber)) throw ValidationException.Field("lotNumber", "Вкажіть номер лоту");
        var m = new LabQcMaterial { AnalyzerId = req.AnalyzerId, Name = req.Name, Level = req.Level, LotNumber = req.LotNumber, Manufacturer = req.Manufacturer, ExpiryDate = req.ExpiryDate, OpenedAt = req.OpenedAt, OpenStabilityDays = req.OpenStabilityDays, IsActive = req.IsActive };
        foreach (var t in req.Targets) m.Targets.Add(ToTarget(t, m.Id));
        _db.QcMaterials.Add(m);
        _audit.Log("CREATE", "lab_qc_material", m.Id, null, req);
        await _db.SaveChangesAsync();
        return await MaterialAsync(m.Id);
    }

    public async Task<LabQcMaterial> UpdateMaterialAsync(string id, QcMaterialRequest req)
    {
        _policy.Require("Редагування контрольного матеріалу", QcRoles);
        var m = await _db.QcMaterials.Include(x => x.Targets).FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Контрольний матеріал", id);
        var before = new { m.Name, m.Level, m.LotNumber, m.ExpiryDate, m.IsActive, targets = m.Targets.Select(t => new { t.TestCode, t.TargetMean, t.TargetSd }) };
        if (!string.IsNullOrWhiteSpace(req.AnalyzerId)) m.AnalyzerId = req.AnalyzerId;
        if (!string.IsNullOrWhiteSpace(req.Name)) m.Name = req.Name;
        if (!string.IsNullOrWhiteSpace(req.Level)) m.Level = req.Level;
        if (!string.IsNullOrWhiteSpace(req.LotNumber)) m.LotNumber = req.LotNumber;
        m.Manufacturer = req.Manufacturer ?? m.Manufacturer;
        if (req.ExpiryDate != default) m.ExpiryDate = req.ExpiryDate;
        m.OpenedAt = req.OpenedAt ?? m.OpenedAt;
        m.OpenStabilityDays = req.OpenStabilityDays;
        m.IsActive = req.IsActive;
        if (req.Targets.Count > 0)
        {
            // upsert цілей за testCode
            foreach (var t in req.Targets)
            {
                var existing = m.Targets.FirstOrDefault(x => x.TestCode == t.TestCode);
                if (existing == null) m.Targets.Add(ToTarget(t, m.Id));
                else { existing.TargetMean = t.TargetMean; existing.TargetSd = t.TargetSd; existing.Unit = t.Unit ?? existing.Unit; existing.TeaPct = t.TeaPct ?? existing.TeaPct; }
            }
        }
        _audit.Log("UPDATE", "lab_qc_material", m.Id, before, req);
        await _db.SaveChangesAsync();
        return await MaterialAsync(id);
    }

    public async Task DeleteMaterialAsync(string id)
    {
        _policy.Require("Видалення контрольного матеріалу", LabRoles.Admin, LabRoles.Doctor);
        var m = await _db.QcMaterials.Include(x => x.Targets).FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Контрольний матеріал", id);
        if (await _db.QcResults.AnyAsync(r => r.QcMaterialId == id)) { m.IsActive = false; m.RecordState = RecordStates.Deleted; _audit.Log("SOFT_DELETE", "lab_qc_material", id, null, null, "Є результати ВКЯ — деактивовано"); }
        else { _db.QcMaterials.Remove(m); _audit.Log("DELETE", "lab_qc_material", id, new { m.Name, m.LotNumber }, null); }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteTargetAsync(string materialId, string targetId)
    {
        _policy.Require("Видалення цільового значення", QcRoles);
        var t = await _db.QcTargets.FirstOrDefaultAsync(x => x.Id == targetId && x.QcMaterialId == materialId) ?? throw NotFoundException.For("Цільове значення", targetId);
        _db.QcTargets.Remove(t);
        _audit.Log("DELETE", "lab_qc_target", targetId, new { t.TestCode }, null);
        await _db.SaveChangesAsync();
    }

    private static LabQcTarget ToTarget(QcTargetRequest t, string materialId)
    {
        if (t.TargetSd <= 0) throw ValidationException.Field("targets", $"SD для {t.TestCode} має бути > 0");
        return new LabQcTarget { QcMaterialId = materialId, TestCode = t.TestCode, TargetMean = t.TargetMean, TargetSd = t.TargetSd, Unit = t.Unit, TeaPct = t.TeaPct };
    }

    // ------------------------------------------------------------------ results
    public async Task<LabQcResult> AddResultAsync(QcResultRequest req, bool isSystem = false)
    {
        if (!isSystem) _policy.Require("Внесення результату ВКЯ", QcRoles);
        var material = await _db.QcMaterials.Include(m => m.Targets).FirstOrDefaultAsync(m => m.Id == req.QcMaterialId) ?? throw NotFoundException.For("Контрольний матеріал", req.QcMaterialId);
        var target = material.Targets.FirstOrDefault(t => t.TestCode == req.TestCode) ?? throw new ValidationException($"Для матеріалу {material.Name} немає цільового значення тесту {req.TestCode}");
        var runAt = req.RunAt ?? DateTime.UtcNow;

        var previous = await _db.QcResults.AsNoTracking()
            .Where(r => r.QcMaterialId == material.Id && r.TestCode == req.TestCode && r.RunAt < runAt && r.ResolvedAt == null)
            .OrderByDescending(r => r.RunAt).Take(9).Select(r => r.MeasuredValue).ToListAsync();
        previous.Reverse();
        previous.Add(req.MeasuredValue);

        var eval = WestgardEvaluator.Evaluate(previous, target.TargetMean, target.TargetSd);
        var result = new LabQcResult
        {
            QcMaterialId = material.Id, AnalyzerId = material.AnalyzerId, TestCode = req.TestCode, MeasuredValue = req.MeasuredValue,
            TargetMean = target.TargetMean, TargetSd = target.TargetSd, ZScore = eval.ZScore, ViolatedRules = eval.ViolatedRules,
            IsWarning = eval.IsWarning, IsRejection = eval.IsRejection, RunAt = runAt, OperatorId = req.OperatorId ?? (isSystem ? null : _current.EmployeeId)
        };

        if (eval.IsRejection)
        {
            var lockout = await _db.Lockouts.FirstOrDefaultAsync(l => l.AnalyzerId == material.AnalyzerId && l.TestCode == req.TestCode && l.ResolvedAt == null);
            if (lockout == null)
            {
                lockout = new LabAnalyzerLockout
                {
                    AnalyzerId = material.AnalyzerId, TestCode = req.TestCode, QcResultId = result.Id, StartedAt = runAt,
                    Reason = $"ВКЯ {material.Name} (лот {material.LotNumber}): порушено {string.Join(", ", eval.ViolatedRules.Where(WestgardRules.IsRejection))}; z={eval.ZScore:+0.00;-0.00}"
                };
                _db.Lockouts.Add(lockout);
                _audit.Log("LOCKOUT", "lab_analyzer_lockout", lockout.Id, null, new { lockout.AnalyzerId, lockout.TestCode, lockout.Reason });
            }
            result.LockoutEnforced = true;
            result.LockoutId = lockout.Id;
        }
        _db.QcResults.Add(result);
        _audit.Log("CREATE", "lab_qc_result", result.Id, null, new { result.TestCode, result.MeasuredValue, result.ZScore, result.ViolatedRules, result.LockoutEnforced });
        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<LabQcResult> UpdateResultAsync(string id, QcResultRequest req)
    {
        _policy.Require("Редагування результату ВКЯ", LabRoles.Admin, LabRoles.Doctor);
        var r = await _db.QcResults.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Результат ВКЯ", id);
        var before = new { r.MeasuredValue, r.RunAt, r.ZScore };
        r.MeasuredValue = req.MeasuredValue;
        if (req.RunAt.HasValue) r.RunAt = req.RunAt.Value;
        r.ZScore = Math.Round((r.MeasuredValue - r.TargetMean) / r.TargetSd, 3);
        _audit.Log("UPDATE", "lab_qc_result", id, before, new { r.MeasuredValue, r.RunAt, r.ZScore }, "Переоцінка правил виконується при наступній точці");
        await _db.SaveChangesAsync();
        return r;
    }

    public async Task DeleteResultAsync(string id)
    {
        _policy.Require("Видалення результату ВКЯ", LabRoles.Admin, LabRoles.Doctor);
        var r = await _db.QcResults.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Результат ВКЯ", id);
        if (r.LockoutEnforced && await _db.Lockouts.AnyAsync(l => l.Id == r.LockoutId && l.ResolvedAt == null)) throw new ConflictException("Результат спричинив активний lockout — спочатку розблокуйте аналізатор");
        _db.QcResults.Remove(r);
        _audit.Log("DELETE", "lab_qc_result", id, new { r.TestCode, r.MeasuredValue }, null);
        await _db.SaveChangesAsync();
    }

    public async Task<List<LabQcResult>> ResultsAsync(string? materialId, string? testCode, int days) =>
        await _db.QcResults.AsNoTracking().Where(r => (materialId == null || r.QcMaterialId == materialId) && (testCode == null || r.TestCode == testCode) && r.RunAt >= DateTime.UtcNow.AddDays(-days))
            .OrderByDescending(r => r.RunAt).ToListAsync();

    // ------------------------------------------------------------------ Levey-Jennings
    public async Task<object> LeveyJenningsAsync(string? analyzerId, string testCode, string? materialId, int days)
    {
        if (string.IsNullOrWhiteSpace(testCode)) throw ValidationException.Field("testCode", "Вкажіть testCode");
        var materials = await _db.QcMaterials.AsNoTracking().Include(m => m.Targets)
            .Where(m => (materialId == null || m.Id == materialId) && (analyzerId == null || m.AnalyzerId == analyzerId) && m.Targets.Any(t => t.TestCode == testCode)).ToListAsync();
        if (materials.Count == 0) throw new NotFoundException($"Контрольний матеріал із цільовим значенням {testCode} не знайдено");
        var since = DateTime.UtcNow.AddDays(-days);
        var materialIds = materials.Select(m => m.Id).ToList();
        var counts = await _db.QcResults.AsNoTracking().Where(r => materialIds.Contains(r.QcMaterialId) && r.TestCode == testCode && r.RunAt >= since)
            .GroupBy(r => r.QcMaterialId).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var material = materials.OrderByDescending(m => counts.TryGetValue(m.Id, out var n) ? n : 0).ThenByDescending(m => m.IsActive).First();
        var target = material.Targets.First(t => t.TestCode == testCode);
        var results = await _db.QcResults.AsNoTracking().Where(r => r.QcMaterialId == material.Id && r.TestCode == testCode && r.RunAt >= since).OrderBy(r => r.RunAt).ToListAsync();
        var stats = QcStatistics.Compute(results.Select(r => r.MeasuredValue), target.TargetMean);
        var activeLockout = await _db.Lockouts.AsNoTracking().FirstOrDefaultAsync(l => l.AnalyzerId == material.AnalyzerId && l.ResolvedAt == null && (l.TestCode == null || l.TestCode == testCode));
        var last = results.LastOrDefault();
        var status = activeLockout != null ? "LOCKOUT" : last?.IsRejection == true ? "LOCKOUT" : last?.IsWarning == true ? "WARNING" : "OK";
        return new
        {
            materialId = material.Id, materialName = material.Name, level = material.Level, lotNumber = material.LotNumber, analyzerId = material.AnalyzerId, testCode,
            targetMean = target.TargetMean, targetSd = target.TargetSd, unit = target.Unit, teaPct = target.TeaPct,
            cvPct = stats.CvPct, n = stats.N, mean = stats.Mean, sd = stats.Sd, bias = stats.BiasPct, currentStatus = status,
            activeLockoutId = activeLockout?.Id,
            points = results.Select(r => new { id = r.Id, at = r.RunAt, value = r.MeasuredValue, z = r.ZScore, rules = r.ViolatedRules, status = r.IsRejection ? "REJECTION" : r.IsWarning ? "WARNING" : "OK", resolved = r.ResolvedAt != null })
        };
    }

    // ------------------------------------------------------------------ lockouts
    public async Task<List<LabAnalyzerLockout>> LockoutsAsync(bool? active, string? analyzerId) =>
        await _db.Lockouts.AsNoTracking().Include(l => l.Analyzer)
            .Where(l => (active == null || (active.Value ? l.ResolvedAt == null : l.ResolvedAt != null)) && (analyzerId == null || l.AnalyzerId == analyzerId))
            .OrderByDescending(l => l.StartedAt).ToListAsync();

    public async Task<LabAnalyzerLockout> CreateLockoutAsync(string analyzerId, string? testCode, string reason)
    {
        _policy.Require("Ручне блокування аналізатора", LabRoles.Admin, LabRoles.Doctor);
        if (!await _db.Analyzers.AnyAsync(a => a.Id == analyzerId)) throw NotFoundException.For("Аналізатор", analyzerId);
        if (string.IsNullOrWhiteSpace(reason)) throw ValidationException.Field("reason", "Вкажіть причину блокування");
        if (await _db.Lockouts.AnyAsync(l => l.AnalyzerId == analyzerId && l.TestCode == testCode && l.ResolvedAt == null)) throw new ConflictException("Активний lockout для цього аналізатора/тесту вже існує");
        var l = new LabAnalyzerLockout { AnalyzerId = analyzerId, TestCode = testCode, Reason = reason };
        _db.Lockouts.Add(l);
        _audit.Log("LOCKOUT_MANUAL", "lab_analyzer_lockout", l.Id, null, new { analyzerId, testCode, reason });
        await _db.SaveChangesAsync();
        return l;
    }

    public async Task<LabAnalyzerLockout> ResolveLockoutAsync(string id, LockoutResolveRequest req)
    {
        _policy.Require("Розблокування аналізатора", LabRoles.Admin, LabRoles.Doctor);
        if (string.IsNullOrWhiteSpace(req.Cause) || string.IsNullOrWhiteSpace(req.Action)) throw new ValidationException("Вкажіть причину (cause) та коригувальну дію (action)");
        var l = await _db.Lockouts.Include(x => x.Analyzer).FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Lockout", id);
        if (l.ResolvedAt != null) throw new ConflictException("Lockout уже розблоковано");
        var now = DateTime.UtcNow;
        l.ResolvedAt = now; l.ResolvedById = _current.EmployeeId; l.Cause = req.Cause; l.Action = req.Action; l.Comment = req.Comment;
        var qc = await _db.QcResults.Where(r => r.LockoutId == l.Id).ToListAsync();
        foreach (var r in qc) { r.ResolvedAt = now; r.ResolvedById = _current.EmployeeId; r.ResolutionAction = req.Action; }
        l.ProtocolText = $"ПРОТОКОЛ РОЗБЛОКУВАННЯ АНАЛІЗАТОРА\n" +
                         $"Аналізатор: {l.Analyzer?.Name} ({l.Analyzer?.Code})\nТест: {l.TestCode ?? "усі"}\n" +
                         $"Блокування: {l.StartedAt:dd.MM.yyyy HH:mm} UTC — {l.Reason}\n" +
                         $"Правила Вестгарда: {string.Join("; ", qc.SelectMany(r => r.ViolatedRules).Distinct().Select(WestgardEvaluator.Describe))}\n" +
                         $"Причина (root cause): {req.Cause}\nКоригувальна дія: {req.Action}\nКоментар: {req.Comment}\n" +
                         $"Розблоковано: {now:dd.MM.yyyy HH:mm} UTC, {_current.FullName ?? _current.EmployeeId} ({_current.LabRole})";
        _audit.Log("LOCKOUT_RESOLVE", "lab_analyzer_lockout", l.Id, new { l.Reason }, new { req.Cause, req.Action, req.Comment });
        await _db.SaveChangesAsync();
        return l;
    }

    public async Task DeleteLockoutAsync(string id)
    {
        _policy.Require("Видалення lockout", LabRoles.Admin);
        var l = await _db.Lockouts.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Lockout", id);
        if (l.ResolvedAt == null) throw new ConflictException("Активний lockout не видаляється — спочатку розблокуйте з протоколом");
        _db.Lockouts.Remove(l);
        _audit.Log("DELETE", "lab_analyzer_lockout", id, new { l.Reason }, null);
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ report
    public async Task<object> ReportAsync(string? analyzerId, DateTime? from, DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow;
        var results = await _db.QcResults.AsNoTracking().Include(r => r.Material).ThenInclude(m => m!.Analyzer)
            .Where(r => r.RunAt >= f && r.RunAt <= t && (analyzerId == null || r.AnalyzerId == analyzerId)).ToListAsync();
        var lockouts = await _db.Lockouts.AsNoTracking().Include(l => l.Analyzer).Where(l => l.StartedAt >= f && l.StartedAt <= t && (analyzerId == null || l.AnalyzerId == analyzerId)).ToListAsync();
        var groups = results.GroupBy(r => new { r.AnalyzerId, analyzerName = r.Material?.Analyzer?.Name, r.QcMaterialId, materialName = r.Material?.Name, r.Material?.Level, r.Material?.LotNumber, r.TestCode })
            .Select(g =>
            {
                var s = QcStatistics.Compute(g.Select(x => x.MeasuredValue), g.First().TargetMean);
                return new
                {
                    g.Key.AnalyzerId, g.Key.analyzerName, g.Key.QcMaterialId, g.Key.materialName, g.Key.Level, g.Key.LotNumber, g.Key.TestCode,
                    targetMean = g.First().TargetMean, targetSd = g.First().TargetSd, s.N, s.Mean, s.Sd, s.CvPct, s.BiasPct,
                    warnings = g.Count(x => x.IsWarning), rejections = g.Count(x => x.IsRejection),
                    rules = g.SelectMany(x => x.ViolatedRules).GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count())
                };
            }).OrderBy(x => x.analyzerName).ThenBy(x => x.materialName).ThenBy(x => x.TestCode).ToList();
        return new
        {
            from = f, to = t, analyzerId, totalRuns = results.Count, totalWarnings = results.Count(r => r.IsWarning), totalRejections = results.Count(r => r.IsRejection),
            lockouts = lockouts.Select(l => new { l.Id, l.AnalyzerId, analyzerName = l.Analyzer?.Name, l.TestCode, l.Reason, l.StartedAt, l.ResolvedAt, l.Cause, l.Action, durationMin = l.ResolvedAt.HasValue ? Math.Round((l.ResolvedAt.Value - l.StartedAt).TotalMinutes) : (double?)null }),
            series = groups
        };
    }
}
