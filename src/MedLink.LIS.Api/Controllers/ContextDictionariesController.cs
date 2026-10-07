// Контекст співробітника («Працюю як…»), довідники, матриця замовлення, норми, пацієнти
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Api.Services;
using MedLink.LIS.Core.Clinical;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Controllers;

/// <summary>Контекст поточного співробітника (без автентифікації).</summary>
public sealed class ContextController : LisControllerBase
{
    private readonly LisDbContext _db;
    private readonly ICurrentEmployee _current;
    private readonly IRolePolicy _policy;
    public ContextController(LisDbContext db, ICurrentEmployee current, IRolePolicy policy) { _db = db; _current = current; _policy = policy; }

    /// <summary>Поточний співробітник (X-MedLink-Employee-Id або Lab:DefaultEmployeeId) та лабораторія.</summary>
    [HttpGet("context/me")]
    public async Task<IActionResult> Me()
    {
        var emp = await _db.Employees.AsNoTracking().Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == _current.EmployeeId);
        var lab = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        return Ok(new
        {
            employee = emp == null ? null : new { emp.Id, emp.FullName, position = emp.PositionName, emp.LabRole, emp.DepartmentId, departmentName = emp.Department?.Name, emp.Email, emp.Phone },
            requestedEmployeeId = _current.EmployeeId, isResolved = _current.IsResolved,
            lab = lab == null ? null : new { lab.Name, lab.Edrpou, lab.Address, lab.Phone, lab.Email, lab.LicenseNumber, lab.DirectorName, lab.WorkingHours, lab.PanicPhone },
            roles = LabRoles.All, stateMachine = new { order = LisStateMachine.Describe(LisEntities.Order), sample = LisStateMachine.Describe(LisEntities.Sample), orderTest = LisStateMachine.Describe(LisEntities.OrderTest), manifest = LisStateMachine.Describe(LisEntities.Manifest), culture = LisStateMachine.Describe(LisEntities.Culture), connector = LisStateMachine.Describe(LisEntities.Connector) }
        });
    }

    /// <summary>Співробітники для перемикача «Працюю як…».</summary>
    [HttpGet("context/employees")]
    public async Task<IActionResult> Employees()
    {
        var list = await _db.Employees.AsNoTracking().Include(e => e.Department).Where(e => e.IsActive && !e.IsDeleted).OrderBy(e => e.FullName)
            .Select(e => new { e.Id, e.FullName, position = e.PositionName, e.LabRole, e.DepartmentId, departmentName = e.Department!.Name }).ToListAsync();
        return Ok(list);
    }
}

/// <summary>Довідники: універсальний CRUD /dictionaries/{name}, імпорт, матриця замовлення, обрані набори.</summary>
public sealed class DictionariesController : LisControllerBase
{
    private readonly DictionaryService _dict;
    private readonly OrderMatrixService _matrix;
    public DictionariesController(DictionaryService dict, OrderMatrixService matrix) { _dict = dict; _matrix = matrix; }

    /// <summary>Перелік доступних довідників.</summary>
    [HttpGet("dictionaries")]
    public IActionResult Names() => Ok(DictionaryService.Names);

    /// <summary>Матриця замовлення: тести/профілі за секціями та категоріями (для чекбокс-сітки).</summary>
    [HttpGet("dictionaries/order-matrix")]
    public async Task<IActionResult> OrderMatrix([FromQuery] bool includeInactive = false) => Ok(await _matrix.MatrixAsync(includeInactive));

    [HttpGet("dictionaries/order-matrix/favorites")]
    public async Task<IActionResult> Favorites([FromQuery] string? employeeId) => Ok(await _matrix.FavoritesAsync(employeeId));

    /// <summary>Повна заміна обраних наборів співробітника.</summary>
    [HttpPut("dictionaries/order-matrix/favorites")]
    public async Task<IActionResult> SaveFavorites([FromQuery] string? employeeId, [FromBody] List<FavoriteSetRequest> sets) => Ok(await _matrix.SaveFavoritesAsync(employeeId, sets));

    [HttpDelete("dictionaries/order-matrix/favorites/{id}")]
    public async Task<IActionResult> DeleteFavorite(string id) { await _matrix.DeleteFavoriteAsync(id); return NoContent(); }

    /// <summary>Імпорт довідника (JSON-масив або CSV у тілі як text) з попереднім переглядом ?dryRun=true.</summary>
    [HttpPost("dictionaries/import")]
    [Consumes("application/json", "text/plain", "text/csv")]
    public async Task<IActionResult> Import([FromQuery] string name, [FromQuery] bool dryRun = true)
    {
        using var reader = new StreamReader(Request.Body);
        var content = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(content)) throw new ValidationException("Порожнє тіло запиту");
        return Ok(await _dict.ImportAsync(name, content, dryRun));
    }

    /// <summary>У яких профілях використовується тест.</summary>
    [HttpGet("dictionaries/tests/{code}/profiles")]
    public async Task<IActionResult> TestProfiles(string code) => Ok(await _dict.TestProfilesAsync(code));

    [HttpGet("dictionaries/{name}")]
    public async Task<IActionResult> List(string name, [FromQuery] string? search, [FromQuery] bool? isActive, [FromQuery] PagingQuery paging) => Ok(await _dict.ListAsync(name, search, isActive, paging));

    [HttpGet("dictionaries/{name}/{id}")]
    public async Task<IActionResult> Get(string name, string id) => Ok(await _dict.GetAsync(name, id));

    [HttpPost("dictionaries/{name}")]
    public async Task<IActionResult> Create(string name, [FromBody] JsonElement body) => StatusCode(201, await _dict.CreateAsync(name, body));

    [HttpPut("dictionaries/{name}/{id}")]
    public async Task<IActionResult> Update(string name, string id, [FromBody] JsonElement body) => Ok(await _dict.UpdateAsync(name, id, body));

    /// <summary>Soft delete (isActive=false) при залежностях, інакше фізичне видалення.</summary>
    [HttpDelete("dictionaries/{name}/{id}")]
    public async Task<IActionResult> Delete(string name, string id) => Ok(await _dict.DeleteAsync(name, id));
}

public sealed class ResolveCascadeRequest
{
    public string TestCode { get; set; } = "";
    public string? MethodCode { get; set; }
    public string Gender { get; set; } = "ANY";
    public double Age { get; set; } = 30;
    public string AgeUnit { get; set; } = "YEARS";
    public bool IsPregnant { get; set; }
    public int? PregnancyWeek { get; set; }
    public string? MenstrualPhase { get; set; }
    public string? Icd10Code { get; set; }
    public double? MeasuredValue { get; set; }
    public double? PreviousValue { get; set; }
    public DateTime? PreviousAt { get; set; }
}

/// <summary>Каскад референтних норм Simplex.</summary>
public sealed class NormsController : LisControllerBase
{
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    public NormsController(LisDbContext db, IRolePolicy policy, IAuditService audit) { _db = db; _policy = policy; _audit = audit; }

    /// <summary>Шари норми тесту (priority desc), опційно за методикою.</summary>
    [HttpGet("norms/layers")]
    public async Task<IActionResult> Layers([FromQuery] string testCode, [FromQuery] string? methodCode)
    {
        if (string.IsNullOrWhiteSpace(testCode)) throw ValidationException.Field("testCode", "Вкажіть testCode");
        var q = _db.ReferenceLayers.AsNoTracking().Where(l => l.TestCode == testCode && l.IsActive);
        if (!string.IsNullOrWhiteSpace(methodCode)) q = q.Where(l => l.MethodCode == methodCode);
        return Ok(await q.OrderByDescending(l => l.PriorityOrder).ThenBy(l => l.NormName).ToListAsync());
    }

    [HttpGet("norms/combinations")]
    public async Task<IActionResult> Combinations([FromQuery] string? testCode, [FromQuery] bool includeInactive = false)
    {
        var q = _db.ReferenceLayers.AsNoTracking().Where(l => includeInactive || l.IsActive);
        if (!string.IsNullOrWhiteSpace(testCode)) q = q.Where(l => l.TestCode == testCode);
        var items = await q.OrderBy(l => l.TestCode).ThenBy(l => l.MethodCode).ThenByDescending(l => l.PriorityOrder).ToListAsync();
        return Ok(items);
    }

    [HttpGet("norms/combinations/{id}")]
    public async Task<IActionResult> Combination(string id) => Ok(await _db.ReferenceLayers.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id) ?? throw NotFoundException.For("Шар норми", id));

    /// <summary>Створити (без id) або оновити (з id) шар норми.</summary>
    [HttpPost("norms/combinations")]
    public async Task<IActionResult> Upsert([FromBody] LabReferenceLayer layer)
    {
        _policy.Require("Редагування норм", LabRoles.Admin, LabRoles.Doctor);
        if (string.IsNullOrWhiteSpace(layer.TestCode)) throw ValidationException.Field("testCode", "Вкажіть testCode");
        var test = await _db.Tests.AsNoTracking().FirstOrDefaultAsync(t => t.Code == layer.TestCode) ?? throw ValidationException.Field("testCode", $"Тест {layer.TestCode} не знайдено");
        if (layer.NormLow.HasValue && layer.NormHigh.HasValue && layer.NormLow > layer.NormHigh) throw new ValidationException("normLow не може перевищувати normHigh");
        if (layer.CritLow.HasValue && layer.NormLow.HasValue && layer.CritLow > layer.NormLow) throw new ValidationException("critLow має бути ≤ normLow");
        if (layer.CritHigh.HasValue && layer.NormHigh.HasValue && layer.CritHigh < layer.NormHigh) throw new ValidationException("critHigh має бути ≥ normHigh");
        if (!new[] { LayerTypes.Baseline, LayerTypes.Demographic, LayerTypes.ClinicalIcd10, LayerTypes.MenstrualPhase, LayerTypes.Pregnancy }.Contains(layer.LayerType)) throw ValidationException.Field("layerType", "BASELINE|DEMOGRAPHIC|CLINICAL_ICD10|MENSTRUAL_PHASE|PREGNANCY");
        if (layer.PriorityOrder <= 0) layer.PriorityOrder = LayerTypes.DefaultPriority(layer.LayerType);
        layer.TestId = test.Id;
        layer.Unit ??= test.Unit;
        var existing = string.IsNullOrWhiteSpace(layer.Id) ? null : await _db.ReferenceLayers.FirstOrDefaultAsync(l => l.Id == layer.Id);
        if (existing == null)
        {
            if (string.IsNullOrWhiteSpace(layer.Id)) layer.Id = Guid.NewGuid().ToString();
            _db.ReferenceLayers.Add(layer);
            _audit.Log("CREATE", "lab_reference_layer", layer.Id, null, layer);
            await _db.SaveChangesAsync();
            return StatusCode(201, layer);
        }
        var before = JsonSerializer.Serialize(existing);
        _db.Entry(existing).CurrentValues.SetValues(layer);
        existing.CreatedOn = existing.CreatedOn == default ? DateTime.UtcNow : existing.CreatedOn;
        _audit.Log("UPDATE", "lab_reference_layer", existing.Id, before, layer);
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPut("norms/combinations/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] LabReferenceLayer layer) { layer.Id = id; return await Upsert(layer); }

    [HttpDelete("norms/combinations/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        _policy.Require("Видалення норм", LabRoles.Admin, LabRoles.Doctor);
        var l = await _db.ReferenceLayers.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Шар норми", id);
        var used = await _db.Results.AnyAsync(r => r.AppliedLayerId == id);
        if (used) { l.IsActive = false; _audit.Log("SOFT_DELETE", "lab_reference_layer", id, null, null, "Шар застосовано у результатах — деактивовано"); }
        else { _db.ReferenceLayers.Remove(l); _audit.Log("DELETE", "lab_reference_layer", id, l, null); }
        await _db.SaveChangesAsync();
        return Ok(new { id, deleted = !used, deactivated = used });
    }

    /// <summary>Інтерактивний резолвер каскаду (playground).</summary>
    [HttpPost("norms/resolve-cascade")]
    public async Task<IActionResult> Resolve([FromBody] ResolveCascadeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TestCode)) throw ValidationException.Field("testCode", "Вкажіть testCode");
        var test = await _db.Tests.AsNoTracking().FirstOrDefaultAsync(t => t.Code == req.TestCode);
        var layers = await _db.ReferenceLayers.AsNoTracking().Where(l => l.TestCode == req.TestCode && l.IsActive).ToListAsync();
        var ctx = new PatientContext
        {
            Gender = OrderService.NormalizeGender(req.Gender) == "U" ? "ANY" : OrderService.NormalizeGender(req.Gender), AgeDays = AgeUnits.ToDays(req.Age, req.AgeUnit),
            IsPregnant = req.IsPregnant, PregnancyWeek = req.PregnancyWeek, MenstrualPhase = req.MenstrualPhase, Icd10Code = req.Icd10Code, MethodCode = req.MethodCode ?? test?.MethodCode
        };
        var res = NormsCascadeResolver.Resolve(layers.Select(ResultPipelineService.ToInput), ctx);
        string? flag = null; bool panic = false; DeltaCheckResult? delta = null;
        if (req.MeasuredValue.HasValue)
        {
            flag = ResultFlagger.Flag(req.MeasuredValue.Value, res.NormLow, res.NormHigh, res.CritLow, res.CritHigh);
            panic = ResultFlags.IsCritical(flag);
            if (req.PreviousValue.HasValue)
                delta = DeltaCheckEvaluator.Evaluate(req.MeasuredValue.Value, req.PreviousValue, req.PreviousAt, DateTime.UtcNow, req.PreviousAt.HasValue ? test?.DeltaCheckHours ?? 72 : 0, test?.DeltaCheckMaxPct ?? res.DeltaCheckMaxPct);
        }
        return Ok(new
        {
            testCode = req.TestCode, methodCode = ctx.MethodCode, found = res.Found, winningLayer = res.WinningLayer == null ? null : layers.First(l => l.Id == res.WinningLayer.Id),
            normLow = res.NormLow, normHigh = res.NormHigh, critLow = res.CritLow, critHigh = res.CritHigh, unit = res.Unit ?? test?.Unit, normText = res.NormText, referenceDisplay = res.ReferenceDisplay,
            statusFlag = flag, isPanicCito = panic, isDeltaAlert = delta?.IsAlert ?? false, deltaPercent = delta?.DeltaPercent, deltaCheckMaxPct = test?.DeltaCheckMaxPct ?? res.DeltaCheckMaxPct,
            patientContext = ctx, auditTrace = res.AuditTrace
        });
    }

    /// <summary>«Картка послуги»: Головна (dct_service МІС) / Показники / Норми / Лабораторія.</summary>
    [HttpGet("norms/service-card/{profileId}")]
    public async Task<IActionResult> ServiceCard(string profileId)
    {
        var p = await _db.Profiles.AsNoTracking().Include(x => x.MisService).Include(x => x.Items.OrderBy(i => i.DisplayOrder)).ThenInclude(i => i.Test).ThenInclude(t => t!.BiomaterialType)
            .Include(x => x.Items).ThenInclude(i => i.Test).ThenInclude(t => t!.TubeType).Include(x => x.Items).ThenInclude(i => i.Test).ThenInclude(t => t!.Method)
            .Include(x => x.Items).ThenInclude(i => i.Test).ThenInclude(t => t!.LabSection)
            .FirstOrDefaultAsync(x => x.Id == profileId || x.Code == profileId) ?? throw NotFoundException.For("Профіль (послуга)", profileId);
        var codes = p.Items.Select(i => i.Test!.Code).ToList();
        var layers = await _db.ReferenceLayers.AsNoTracking().Where(l => codes.Contains(l.TestCode) && l.IsActive).OrderBy(l => l.TestCode).ThenByDescending(l => l.PriorityOrder).ToListAsync();
        var bm = p.DefaultBiomaterialTypeId == null ? null : await _db.BiomaterialTypes.AsNoTracking().FirstOrDefaultAsync(b => b.Id == p.DefaultBiomaterialTypeId);
        var tube = p.DefaultTubeTypeId == null ? null : await _db.TubeTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == p.DefaultTubeTypeId);
        var usage = await _db.OrderTests.AsNoTracking().CountAsync(t => t.ProfileId == p.Id);
        return Ok(new
        {
            main = new
            {
                p.Id, p.Code, p.Name, p.Category, p.IsActive, p.TurnaroundHours, p.FastingRequired,
                misService = p.MisService == null ? null : new { p.MisService.Id, p.MisService.Code, p.MisService.Name, p.MisService.Price, p.MisService.IsActive },
                price = p.MisService?.Price ?? p.Price, ordersCount = usage
            },
            tests = p.Items.Select(i => new { i.Test!.Id, i.Test.Code, i.Test.Name, i.Test.ShortName, i.Test.LoincCode, i.Test.Unit, i.Test.DecimalPlaces, i.Test.ResultType, i.Test.Category, method = i.Test.Method?.Name, i.Test.MethodCode, section = i.Test.LabSection?.Name, i.DisplayOrder, i.IsRequired, i.Test.DeltaCheckMaxPct, i.Test.DeltaCheckHours, i.Test.RequiresManualVerification, layersCount = layers.Count(l => l.TestCode == i.Test.Code) }),
            norms = layers,
            laboratory = new
            {
                biomaterial = bm == null ? null : new { bm.Id, bm.Code, bm.Name, bm.StabilityHours, bm.TemperatureRegime },
                tube = tube == null ? null : new { tube.Id, tube.Code, tube.Name, tube.ColorCode, tube.VolumeMl, tube.Anticoagulant, tube.OrderOfDrawIndex },
                sections = p.Items.Select(i => i.Test!.LabSection).Where(s => s != null).DistinctBy(s => s!.Id).Select(s => new { s!.Id, s.Code, s.Name, s.JournalMask, s.AutoReleaseVerified }),
                methods = p.Items.Select(i => i.Test!.MethodCode).Where(m => m != null).Distinct(),
                analyzers = await _db.AnalyzerParameters.AsNoTracking().Include(m => m.Analyzer).Where(m => codes.Contains(m.TestCode)).Select(m => new { m.AnalyzerId, m.Analyzer!.Name, m.TestCode, m.AnalyzerCode }).ToListAsync()
            }
        });
    }
}

/// <summary>Пацієнти (локальна картка МІС).</summary>
public sealed class PatientsController : LisControllerBase
{
    private readonly PatientService _patients;
    public PatientsController(PatientService patients) => _patients = patients;

    [HttpGet("patients")]
    public async Task<IActionResult> Search([FromQuery] string? search, [FromQuery] PagingQuery paging) => Ok(await _patients.SearchAsync(search, paging));

    [HttpGet("patients/{id}")]
    public async Task<IActionResult> Get(string id) => Ok(await _patients.GetAsync(id));

    [HttpPost("patients")]
    public async Task<IActionResult> Create([FromBody] NewPatientRequest req) => StatusCode(201, await _patients.CreateAsync(req));

    [HttpPut("patients/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] NewPatientRequest req) => Ok(await _patients.UpdateAsync(id, req));

    [HttpDelete("patients/{id}")]
    public async Task<IActionResult> Delete(string id) { await _patients.DeleteAsync(id); return NoContent(); }

    /// <summary>Усі результати пацієнта з трендами.</summary>
    [HttpGet("patients/{id}/history")]
    public async Task<IActionResult> History(string id) => Ok(await _patients.HistoryAsync(id, releasedOnly: false));

    [HttpGet("patients/{id}/trend/{testCode}")]
    public async Task<IActionResult> Trend(string id, string testCode) => Ok(await _patients.TrendAsync(id, testCode, releasedOnly: false));
}
