// Мікробіологія: посіви (INCUBATING→PRELIMINARY→ISOLATED→COMPLETED), ізоляти, антибіотикограма за EUCAST, фенотипи, звіт
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Core.Clinical;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class CultureRequest
{
    public string? OrderTestId { get; set; }
    public string? SpecimenLocus { get; set; }
    public string? CultureMedium { get; set; }
    public int? IncubationHoursRecommended { get; set; }
    public DateTime? IncubationStart { get; set; }
    public bool? GrowthDetected { get; set; }
    public string? GrowthIntensity { get; set; }
    public string? CfuPerMl { get; set; }
    public string? PreliminaryReport { get; set; }
    public string? FinalMicroscopyDescription { get; set; }
}

public sealed class IsolateRequest
{
    public int OrganismId { get; set; }
    public string? QuantitativeCount { get; set; }
    public bool IsClinicallySignificant { get; set; } = true;
    public string? ColonyMorphology { get; set; }
}

public sealed class SusceptibilityRequest
{
    public int AntibioticId { get; set; }
    public string Method { get; set; } = "DISK_DIFFUSION";
    public double? ZoneMm { get; set; }
    public double? Mic { get; set; }
    /// <summary>Ручне перевизначення S/I/R (потрібна причина).</summary>
    public string? OverrideInterpretation { get; set; }
    public string? OverrideReason { get; set; }
}

public sealed class MicrobiologyService
{
    private static readonly string[] Roles = { LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician };
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;

    public MicrobiologyService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current)
    {
        _db = db; _policy = policy; _audit = audit; _current = current;
    }

    private IQueryable<LabCultureOrder> Query() => _db.Cultures
        .Include(c => c.OrderTest).Include(c => c.Order).ThenInclude(o => o!.Patient)
        .Include(c => c.Isolates).ThenInclude(i => i.Organism)
        .Include(c => c.Isolates).ThenInclude(i => i.Susceptibilities).ThenInclude(s => s.Antibiotic);

    public async Task<LabCultureOrder> LoadAsync(string id) => await Query().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted) ?? throw NotFoundException.For("Посів", id);

    public async Task<List<object>> ListAsync(string? status)
    {
        var items = await Query().AsNoTracking().Where(c => !c.IsDeleted && (status == null || c.Status == status.ToUpper())).OrderByDescending(c => c.CreatedOn).ToListAsync();
        return items.Select(ToDto).ToList();
    }

    public async Task<object> GetAsync(string id) => ToDto(await LoadAsync(id));

    public object ToDto(LabCultureOrder c) => new
    {
        c.Id, c.OrderTestId, c.OrderId, orderNumber = c.Order?.OrderNumber, patientName = c.Order?.Patient?.FullName, testCode = c.OrderTest?.TestCode,
        c.SpecimenLocus, c.IncubationStart, c.IncubationHoursRecommended, incubationHoursElapsed = Math.Round((DateTime.UtcNow - c.IncubationStart).TotalHours, 1),
        c.CultureMedium, c.GrowthDetected, c.GrowthIntensity, c.CfuPerMl, c.PreliminaryReport, c.FinalMicroscopyDescription, c.Status, c.CreatedOn,
        isolates = c.Isolates.OrderBy(i => i.IsolateNumber).Select(i => new
        {
            i.Id, i.IsolateNumber, i.OrganismId, organismCode = i.Organism?.Code, organismLatin = i.Organism?.LatinName, organismName = i.Organism?.CommonName, alertCritical = i.Organism?.AlertCriticalOrganism,
            i.QuantitativeCount, i.IsClinicallySignificant, i.ColonyMorphology, phenotypes = (i.ResistancePhenotypes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries),
            susceptibilities = i.Susceptibilities.Select(s => new { s.Id, s.AntibioticId, antibioticCode = s.Antibiotic?.Code, antibioticName = s.Antibiotic?.Name, group = s.Antibiotic?.GroupName, s.Method, s.ZoneDiameterMm, s.MicValueMgL, s.Interpretation, s.IsIntrinsicResistance, s.OverrideReason })
        }),
        allowedActions = _policy.AllowedActions(LisEntities.Culture, c.Status), stateMachine = LisStateMachine.Describe(LisEntities.Culture)
    };

    public async Task<object> CreateAsync(CultureRequest req)
    {
        _policy.Require("Створення посіву", Roles);
        if (string.IsNullOrWhiteSpace(req.OrderTestId)) throw ValidationException.Field("orderTestId", "Вкажіть тест замовлення (посів)");
        var test = await _db.OrderTests.Include(t => t.Order).FirstOrDefaultAsync(t => t.Id == req.OrderTestId) ?? throw NotFoundException.For("Тест замовлення", req.OrderTestId);
        if (await _db.Cultures.AnyAsync(c => c.OrderTestId == test.Id && !c.IsDeleted)) throw new ConflictException("Для цього тесту посів уже створено");
        var c = new LabCultureOrder
        {
            OrderTestId = test.Id, OrderId = test.OrderId, SpecimenLocus = req.SpecimenLocus, CultureMedium = req.CultureMedium ?? "Blood Agar",
            IncubationHoursRecommended = req.IncubationHoursRecommended ?? 48, IncubationStart = req.IncubationStart ?? DateTime.UtcNow, Status = LisStateMachine.CultureIncubating
        };
        if (test.Status == OrderTestStatuses.Pending) test.Status = OrderTestStatuses.InAnalysis;
        _db.Cultures.Add(c);
        _audit.Log("CREATE", "lab_culture_order", c.Id, null, req);
        await _db.SaveChangesAsync();
        return await GetAsync(c.Id);
    }

    public async Task<object> UpdateAsync(string id, CultureRequest req)
    {
        var c = await LoadAsync(id);
        _policy.Ensure(LisEntities.Culture, CultureActions.Edit, c.Status, "Посів");
        var before = new { c.SpecimenLocus, c.CultureMedium, c.GrowthDetected, c.GrowthIntensity, c.CfuPerMl, c.PreliminaryReport, c.FinalMicroscopyDescription };
        if (req.SpecimenLocus != null) c.SpecimenLocus = req.SpecimenLocus;
        if (req.CultureMedium != null) c.CultureMedium = req.CultureMedium;
        if (req.IncubationHoursRecommended.HasValue) c.IncubationHoursRecommended = req.IncubationHoursRecommended.Value;
        if (req.IncubationStart.HasValue) c.IncubationStart = req.IncubationStart.Value;
        if (req.GrowthDetected.HasValue) c.GrowthDetected = req.GrowthDetected;
        if (req.GrowthIntensity != null) c.GrowthIntensity = req.GrowthIntensity;
        if (req.CfuPerMl != null) c.CfuPerMl = req.CfuPerMl;
        if (req.PreliminaryReport != null) c.PreliminaryReport = req.PreliminaryReport;
        if (req.FinalMicroscopyDescription != null) c.FinalMicroscopyDescription = req.FinalMicroscopyDescription;
        _audit.Log("UPDATE", "lab_culture_order", c.Id, before, req);
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<object> TransitionAsync(string id, string action, string? comment)
    {
        var c = await LoadAsync(id);
        var act = action.ToUpperInvariant();
        var rule = _policy.Ensure(LisEntities.Culture, act, c.Status, "Посів");
        if (rule.ToStatus == null) throw new ValidationException($"Дія {action} не є переходом статусу");
        if (act == CultureActions.Isolate && c.Isolates.Count == 0) throw new ConflictException("Неможливо перейти до ISOLATED: не додано жодного ізоляту");
        if (act == CultureActions.Complete && c.GrowthDetected == true && c.Isolates.Count == 0) throw new ConflictException("Виявлено ріст, але ізоляти не описано");
        var before = c.Status;
        c.Status = rule.ToStatus;
        if (act == CultureActions.Complete)
        {
            // Результат тесту-посіву: текстовий підсумок у конвеєр не йде (ручна верифікація), статус тесту → RESULTED
            if (c.OrderTest != null && c.OrderTest.Status is OrderTestStatuses.Pending or OrderTestStatuses.InAnalysis)
            {
                c.OrderTest.Status = OrderTestStatuses.Resulted;
                var summary = c.GrowthDetected == true
                    ? string.Join("; ", c.Isolates.Select(i => $"{i.Organism?.LatinName} {i.QuantitativeCount}".Trim()))
                    : "росту не виявлено";
                var existing = await _db.Results.FirstOrDefaultAsync(r => r.OrderTestId == c.OrderTestId);
                if (existing == null)
                    _db.Results.Add(new LabTestResult { OrderTestId = c.OrderTestId, StringValue = summary, Flag = c.GrowthDetected == true ? ResultFlags.Abnormal : ResultFlags.Normal, ReferenceDisplay = "росту не виявлено", EnteredById = _current.EmployeeId, AutoVerifyBlockReason = "Мікробіологія — ручна верифікація" });
                else { existing.StringValue = summary; existing.Flag = c.GrowthDetected == true ? ResultFlags.Abnormal : ResultFlags.Normal; existing.Version++; }
            }
        }
        _audit.Log(act, "lab_culture_order", c.Id, new { status = before }, new { c.Status }, comment);
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task DeleteAsync(string id)
    {
        var c = await LoadAsync(id);
        _policy.Ensure(LisEntities.Culture, CultureActions.Delete, c.Status, "Посів");
        _db.Cultures.Remove(c);
        _audit.Log("DELETE", "lab_culture_order", id, new { c.OrderTestId }, null);
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ isolates
    public async Task<object> AddIsolateAsync(string cultureId, IsolateRequest req)
    {
        var c = await LoadAsync(cultureId);
        _policy.Ensure(LisEntities.Culture, CultureActions.AddIsolate, c.Status, "Посів");
        var organism = await _db.Organisms.FirstOrDefaultAsync(o => o.Id == req.OrganismId) ?? throw ValidationException.Field("organismId", "Мікроорганізм не знайдено");
        var isolate = new LabIsolate { CultureOrderId = c.Id, IsolateNumber = c.Isolates.Count + 1, OrganismId = organism.Id, QuantitativeCount = req.QuantitativeCount, IsClinicallySignificant = req.IsClinicallySignificant, ColonyMorphology = req.ColonyMorphology };
        c.Isolates.Add(isolate);
        c.GrowthDetected = true;
        if (c.Status == LisStateMachine.CultureIncubating) c.Status = LisStateMachine.CulturePreliminary;
        _audit.Log("ADD_ISOLATE", "lab_isolate", isolate.Id, null, new { organism.Code, req.QuantitativeCount });
        await _db.SaveChangesAsync();
        return await GetAsync(cultureId);
    }

    public async Task<object> UpdateIsolateAsync(string isolateId, IsolateRequest req)
    {
        var isolate = await _db.Isolates.Include(i => i.Culture).FirstOrDefaultAsync(i => i.Id == isolateId) ?? throw NotFoundException.For("Ізолят", isolateId);
        _policy.Ensure(LisEntities.Culture, CultureActions.Edit, isolate.Culture!.Status, "Посів");
        var before = new { isolate.OrganismId, isolate.QuantitativeCount, isolate.IsClinicallySignificant, isolate.ColonyMorphology };
        if (req.OrganismId > 0) { if (!await _db.Organisms.AnyAsync(o => o.Id == req.OrganismId)) throw ValidationException.Field("organismId", "Мікроорганізм не знайдено"); isolate.OrganismId = req.OrganismId; }
        isolate.QuantitativeCount = req.QuantitativeCount ?? isolate.QuantitativeCount;
        isolate.IsClinicallySignificant = req.IsClinicallySignificant;
        isolate.ColonyMorphology = req.ColonyMorphology ?? isolate.ColonyMorphology;
        _audit.Log("UPDATE", "lab_isolate", isolateId, before, req);
        await _db.SaveChangesAsync();
        await RecomputePhenotypesAsync(isolateId);
        return await GetAsync(isolate.CultureOrderId);
    }

    public async Task DeleteIsolateAsync(string isolateId)
    {
        var isolate = await _db.Isolates.Include(i => i.Culture).FirstOrDefaultAsync(i => i.Id == isolateId) ?? throw NotFoundException.For("Ізолят", isolateId);
        _policy.Ensure(LisEntities.Culture, CultureActions.Edit, isolate.Culture!.Status, "Посів");
        _db.Isolates.Remove(isolate);
        _audit.Log("DELETE", "lab_isolate", isolateId, new { isolate.OrganismId }, null);
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ susceptibility
    public async Task<object> AddSusceptibilityAsync(string isolateId, SusceptibilityRequest req)
    {
        var isolate = await _db.Isolates.Include(i => i.Culture).Include(i => i.Organism).Include(i => i.Susceptibilities).FirstOrDefaultAsync(i => i.Id == isolateId) ?? throw NotFoundException.For("Ізолят", isolateId);
        _policy.Ensure(LisEntities.Culture, CultureActions.AddSusceptibility, isolate.Culture!.Status, "Посів");
        var antibiotic = await _db.Antibiotics.FirstOrDefaultAsync(a => a.Id == req.AntibioticId) ?? throw ValidationException.Field("antibioticId", "Антибіотик не знайдено");
        if (!req.ZoneMm.HasValue && !req.Mic.HasValue && string.IsNullOrEmpty(req.OverrideInterpretation)) throw new ValidationException("Вкажіть зону (мм) або МІК (мг/л)");

        var bp = await _db.EucastBreakpoints.AsNoTracking().FirstOrDefaultAsync(b => b.OrganismId == isolate.OrganismId && b.AntibioticId == antibiotic.Id);
        string interpretation = "";
        if (bp != null)
            interpretation = EucastInterpreter.Interpret(new EucastBreakpointInput
            {
                OrganismCode = isolate.Organism?.Code ?? "", AntibioticCode = antibiotic.Code, MicSusceptibleLe = bp.MicSusceptibleLe, MicResistantGt = bp.MicResistantGt,
                ZoneSusceptibleGe = bp.ZoneSusceptibleGe, ZoneResistantLt = bp.ZoneResistantLt, IntrinsicResistance = bp.IntrinsicResistance
            }, req.ZoneMm, req.Mic);
        if (!string.IsNullOrEmpty(req.OverrideInterpretation))
        {
            if (string.IsNullOrWhiteSpace(req.OverrideReason)) throw ValidationException.Field("overrideReason", "Ручна інтерпретація потребує обґрунтування");
            interpretation = req.OverrideInterpretation.ToUpperInvariant();
        }
        if (string.IsNullOrEmpty(interpretation)) throw new ConflictException($"Немає breakpoint EUCAST для {isolate.Organism?.LatinName} / {antibiotic.Name} — вкажіть інтерпретацію вручну з обґрунтуванням");

        var existing = isolate.Susceptibilities.FirstOrDefault(s => s.AntibioticId == antibiotic.Id);
        if (existing == null) { existing = new LabSusceptibilityResult { IsolateId = isolate.Id, AntibioticId = antibiotic.Id }; isolate.Susceptibilities.Add(existing); }
        existing.Method = req.Method; existing.ZoneDiameterMm = req.ZoneMm; existing.MicValueMgL = req.Mic; existing.Interpretation = interpretation;
        existing.IsIntrinsicResistance = bp?.IntrinsicResistance ?? false; existing.BreakpointId = bp?.Id;
        existing.OverrideReason = req.OverrideInterpretation != null ? req.OverrideReason : null; existing.OverriddenById = req.OverrideInterpretation != null ? _current.EmployeeId : null;
        await _db.SaveChangesAsync();
        var phenotypes = await RecomputePhenotypesAsync(isolate.Id);
        _audit.Log("SUSCEPTIBILITY", "lab_susceptibility_result", existing.Id, null, new { antibiotic.Code, req.ZoneMm, req.Mic, interpretation, phenotypes });
        await _db.SaveChangesAsync();
        return new { id = existing.Id, isolateId = isolate.Id, antibioticCode = antibiotic.Code, antibioticName = antibiotic.Name, req.Method, zoneMm = req.ZoneMm, mic = req.Mic, interpretation, breakpointFound = bp != null, phenotypes };
    }

    public async Task DeleteSusceptibilityAsync(string id)
    {
        var s = await _db.Susceptibilities.Include(x => x.Isolate).ThenInclude(i => i!.Culture).FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Результат антибіотикограми", id);
        _policy.Ensure(LisEntities.Culture, CultureActions.AddSusceptibility, s.Isolate!.Culture!.Status, "Посів");
        _db.Susceptibilities.Remove(s);
        await _db.SaveChangesAsync();
        await RecomputePhenotypesAsync(s.IsolateId);
        _audit.Log("DELETE", "lab_susceptibility_result", id, new { s.AntibioticId }, null);
        await _db.SaveChangesAsync();
    }

    private async Task<List<string>> RecomputePhenotypesAsync(string isolateId)
    {
        var isolate = await _db.Isolates.Include(i => i.Organism).Include(i => i.Susceptibilities).ThenInclude(s => s.Antibiotic).FirstAsync(i => i.Id == isolateId);
        var phenotypes = EucastInterpreter.DetectPhenotypes(isolate.Organism?.Code ?? "", isolate.Organism?.LatinName,
            isolate.Susceptibilities.Select(s => new SusceptibilityObservation { AntibioticCode = s.Antibiotic?.Code ?? "", Interpretation = s.Interpretation }));
        isolate.ResistancePhenotypes = phenotypes.Count == 0 ? null : string.Join(",", phenotypes.Select(p => p.Code));
        await _db.SaveChangesAsync();
        return phenotypes.Select(p => p.Code).ToList();
    }

    public async Task<string> ReportHtmlAsync(string id)
    {
        var c = await LoadAsync(id);
        var lab = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        var sb = new System.Text.StringBuilder();
        string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        sb.Append("<!doctype html><html lang=\"uk\"><head><meta charset=\"utf-8\"><title>Бактеріологічне дослідження</title><style>body{font-family:Arial,sans-serif;font-size:12px;margin:20mm}h1{font-size:16px;color:#4274A7}table{border-collapse:collapse;width:100%;margin:8px 0}td,th{border:1px solid #999;padding:4px 6px}th{background:#eef}.S{color:#15803d;font-weight:bold}.I{color:#b45309;font-weight:bold}.R{color:#b91c1c;font-weight:bold}.alert{background:#fee2e2;padding:6px;border:1px solid #b91c1c}@page{size:A4}</style></head><body>");
        sb.Append($"<h1>{H(lab?.Name)}</h1><p>{H(lab?.Address)} · {H(lab?.Phone)}</p><h2>Бактеріологічне дослідження з антибіотикограмою (EUCAST v14.0)</h2>");
        sb.Append($"<p><b>Пацієнт:</b> {H(c.Order?.Patient?.FullName)} · <b>Замовлення:</b> {H(c.Order?.OrderNumber)} · <b>Локус:</b> {H(c.SpecimenLocus)} · <b>Середовище:</b> {H(c.CultureMedium)}</p>");
        sb.Append($"<p><b>Інкубація:</b> з {c.IncubationStart:dd.MM.yyyy HH:mm} ({c.IncubationHoursRecommended} год) · <b>Ріст:</b> {(c.GrowthDetected == true ? $"виявлено, {H(c.GrowthIntensity)} {H(c.CfuPerMl)}" : c.GrowthDetected == false ? "не виявлено" : "в інкубації")} · <b>Статус:</b> {c.Status}</p>");
        if (!string.IsNullOrEmpty(c.FinalMicroscopyDescription)) sb.Append($"<p><b>Мікроскопія:</b> {H(c.FinalMicroscopyDescription)}</p>");
        foreach (var i in c.Isolates.OrderBy(i => i.IsolateNumber))
        {
            sb.Append($"<h3>Ізолят №{i.IsolateNumber}: <i>{H(i.Organism?.LatinName)}</i> ({H(i.Organism?.CommonName)}) {H(i.QuantitativeCount)}</h3>");
            if (!string.IsNullOrEmpty(i.ResistancePhenotypes)) sb.Append($"<p class=\"alert\"><b>Фенотип стійкості:</b> {H(i.ResistancePhenotypes)} — повідомлено інфекційний контроль</p>");
            sb.Append("<table><tr><th>Антибіотик</th><th>Група</th><th>Метод</th><th>Зона, мм</th><th>МІК, мг/л</th><th>Інтерпретація</th></tr>");
            foreach (var s in i.Susceptibilities.OrderBy(s => s.Antibiotic?.GroupName))
                sb.Append($"<tr><td>{H(s.Antibiotic?.Name)}</td><td>{H(s.Antibiotic?.GroupName)}</td><td>{s.Method}</td><td>{s.ZoneDiameterMm}</td><td>{s.MicValueMgL}</td><td class=\"{s.Interpretation}\">{s.Interpretation}{(s.IsIntrinsicResistance ? " (природна)" : "")}</td></tr>");
            sb.Append("</table>");
        }
        sb.Append("<p>S — чутливий; I — чутливий при підвищеній експозиції; R — резистентний.</p>");
        sb.Append($"<p style=\"margin-top:20px\">{H(lab?.ReportFooter)}</p></body></html>");
        return sb.ToString();
    }
}
