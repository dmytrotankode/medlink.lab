// Етапи обробки проби та алікотування (ISO 15189 преаналітична простежуваність, CLSI): дочірні проби з похідними
// ідентифікаторами (parent-A1, S26-00123-1, S26-00123-1-HE), дерево проб, події етапів за шаблоном робочого процесу секції
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class SplitSampleRequest
{
    public int Count { get; set; } = 1;
    public double? VolumeEachMl { get; set; }
    /// <summary>ALIQUOT | CASSETTE | BLOCK | SLIDE | CULTURE_PLATE | DILUTION</summary>
    public string DerivationType { get; set; } = "ALIQUOT";
    /// <summary>Секції, тести яких переносяться на дочірні проби (i-та секція → i-та дочірня, інакше всі на першу).</summary>
    public List<string>? TargetSectionIds { get; set; }
    /// <summary>Код фарбування для скелець (суфікс), типово HE.</summary>
    public string? StainCode { get; set; }
    public string? ContainerType { get; set; }
}

public sealed class StageRequest
{
    public string StageCode { get; set; } = "";
    public string? Note { get; set; }
    public double? Temperature { get; set; }
    public string? InstrumentId { get; set; }
    public Dictionary<string, object?>? Data { get; set; }
}

public sealed class SampleProcessingService
{
    private static readonly string[] Roles = { LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician };
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;
    private readonly SampleService _samples;

    public SampleProcessingService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current, SampleService samples)
    {
        _db = db; _policy = policy; _audit = audit; _current = current; _samples = samples;
    }

    public static string ChildBarcode(string parentBarcode, string derivationType, int index, string? stainCode) => derivationType switch
    {
        DerivationTypes.Aliquot => $"{parentBarcode}-A{index}",
        DerivationTypes.Dilution => $"{parentBarcode}-D{index}",
        DerivationTypes.Cassette => $"{parentBarcode}-{index}",
        DerivationTypes.Block => index == 1 ? $"{parentBarcode}-B" : $"{parentBarcode}-B{index}",
        DerivationTypes.Slide => index == 1 ? $"{parentBarcode}-{(stainCode ?? "HE").ToUpperInvariant()}" : $"{parentBarcode}-{(stainCode ?? "HE").ToUpperInvariant()}{index}",
        DerivationTypes.CulturePlate => $"{parentBarcode}-P{index}",
        _ => $"{parentBarcode}-{index}"
    };

    public async Task<object> SplitAsync(string barcode, SplitSampleRequest req)
    {
        _policy.Require("Алікотування / розділення проби", Roles);
        if (req.Count is < 1 or > 50) throw ValidationException.Field("count", "Кількість дочірніх проб 1..50");
        if (!DerivationTypes.All.Contains(req.DerivationType) || req.DerivationType == DerivationTypes.Primary) throw ValidationException.Field("derivationType", "ALIQUOT | CASSETTE | BLOCK | SLIDE | CULTURE_PLATE | DILUTION");
        var parent = await _samples.LoadByBarcodeAsync(barcode);
        if (parent.Status is not (SampleStatuses.Received or SampleStatuses.Processing or SampleStatuses.Stored))
            throw new ConflictException($"Проба {barcode} у статусі {parent.Status}: розділяти можна лише прийняті проби (RECEIVED/PROCESSING/STORED)");
        if (req.VolumeEachMl.HasValue && parent.VolumeMl.HasValue && req.VolumeEachMl.Value * req.Count > parent.VolumeMl.Value + 1e-9)
            throw new ConflictException($"Недостатньо об'єму: {parent.VolumeMl} мл < {req.Count} × {req.VolumeEachMl} мл");
        var order = parent.Order!;
        var sections = new List<LabSection>();
        foreach (var sid in req.TargetSectionIds ?? new())
            sections.Add(await _db.Sections.FirstOrDefaultAsync(s => s.Id == sid || s.Code == sid) ?? throw NotFoundException.For("Підрозділ лабораторії", sid));

        var existingChildren = await _db.Samples.CountAsync(s => s.ParentSampleId == parent.Id && s.DerivationType == req.DerivationType);
        var children = new List<LabOrderSample>();
        for (var i = 1; i <= req.Count; i++)
        {
            var idx = existingChildren + i;
            var childBarcode = ChildBarcode(parent.Barcode, req.DerivationType, idx, req.StainCode);
            if (await _db.Samples.AnyAsync(s => s.Barcode == childBarcode)) throw new ConflictException($"Штрихкод {childBarcode} вже існує");
            var child = new LabOrderSample
            {
                OrderId = order.Id, Barcode = childBarcode, TubeTypeId = parent.TubeTypeId, BiomaterialTypeId = parent.BiomaterialTypeId, GroupNumb = order.Samples.Count + i,
                ParentSampleId = parent.Id, DerivationType = req.DerivationType, DerivationIndex = idx, ContainerType = req.ContainerType ?? DefaultContainer(req.DerivationType),
                LabSectionId = sections.Count > 0 ? sections[Math.Min(i - 1, sections.Count - 1)].Id : parent.LabSectionId,
                Status = SampleStatuses.Received, CollectedAt = parent.CollectedAt, CollectedById = parent.CollectedById, ReceivedAt = DateTime.UtcNow, ReceivedById = _current.EmployeeId,
                VolumeMl = req.VolumeEachMl, CurrentStage = req.DerivationType == DerivationTypes.Aliquot ? "ALIQUOTED" : null
            };
            _db.Samples.Add(child);
            children.Add(child);
        }
        if (req.VolumeEachMl.HasValue && parent.VolumeMl.HasValue) parent.VolumeMl = Math.Round(parent.VolumeMl.Value - req.VolumeEachMl.Value * req.Count, 2);
        if (parent.Status == SampleStatuses.Received) parent.Status = SampleStatuses.Processing;

        // Перенесення тестів цільових секцій на дочірні проби
        var moved = new List<object>();
        if (sections.Count > 0)
        {
            var testIds = order.Tests.Where(t => t.SampleId == parent.Id).Select(t => t.TestId).Distinct().ToList();
            var defs = await _db.Tests.AsNoTracking().Where(t => testIds.Contains(t.Id)).Select(t => new { t.Id, t.LabSectionId }).ToListAsync();
            foreach (var t in order.Tests.Where(t => t.SampleId == parent.Id && !OrderTestStatuses.Final.Contains(t.Status)))
            {
                var sid = defs.FirstOrDefault(d => d.Id == t.TestId)?.LabSectionId;
                var sIdx = sections.FindIndex(s => s.Id == sid);
                if (sIdx < 0) continue;
                var target = children[Math.Min(sIdx, children.Count - 1)];
                t.SampleId = target.Id;
                moved.Add(new { orderTestId = t.Id, t.TestCode, toBarcode = target.Barcode });
            }
        }
        _audit.Log("SPLIT", "lab_order_sample", parent.Id, new { parent.Barcode, parent.VolumeMl }, new { req.DerivationType, children = children.Select(c => c.Barcode), moved });
        await _db.SaveChangesAsync();

        var labels = new List<LabelDto>();
        foreach (var c in children) labels.Add(_samples.BuildLabel(await _samples.LoadAsync(c.Id)));
        return new { parentBarcode = parent.Barcode, parentStatus = parent.Status, parentVolumeMl = parent.VolumeMl, children = children.Select(c => new { c.Id, c.Barcode, c.DerivationType, c.DerivationIndex, c.LabSectionId, c.VolumeMl, c.Status }), movedTests = moved, labels };
    }

    private static string DefaultContainer(string derivationType) => derivationType switch
    {
        DerivationTypes.Aliquot => "Мікропробірка 1.5 мл", DerivationTypes.Cassette => "Гістологічна касета", DerivationTypes.Block => "Парафіновий блок",
        DerivationTypes.Slide => "Предметне скло", DerivationTypes.CulturePlate => "Чашка Петрі", DerivationTypes.Dilution => "Мікропробірка (розведення)", _ => "Контейнер"
    };

    public async Task<object> TreeAsync(string barcode)
    {
        var sample = await _samples.LoadByBarcodeAsync(barcode);
        var root = sample;
        while (root.ParentSampleId != null) root = await _samples.LoadAsync(root.ParentSampleId);
        var all = await _db.Samples.AsNoTracking().Include(s => s.TubeType).Include(s => s.LabSection).Where(s => s.OrderId == root.OrderId).ToListAsync();
        var tests = await _db.OrderTests.AsNoTracking().Where(t => t.OrderId == root.OrderId).Select(t => new { t.Id, t.SampleId, t.TestCode, t.Status }).ToListAsync();
        object Node(LabOrderSample s) => new
        {
            s.Id, s.Barcode, s.DerivationType, s.DerivationIndex, s.ContainerType, s.Status, s.CurrentStage, s.VolumeMl, s.LabSectionId, labSectionName = s.LabSection?.Name, tubeType = s.TubeType?.Name,
            tests = tests.Where(t => t.SampleId == s.Id).Select(t => new { t.Id, t.TestCode, t.Status }),
            children = all.Where(c => c.ParentSampleId == s.Id).OrderBy(c => c.DerivationIndex).Select(Node)
        };
        return new { orderId = root.OrderId, requestedBarcode = barcode, root = Node(root) };
    }

    // ------------------------------------------------------------------ stages
    public async Task<LabWorkflowTemplate> TemplateForSampleAsync(LabOrderSample sample)
    {
        string? code = null;
        if (sample.LabSectionId != null) code = await _db.Sections.AsNoTracking().Where(s => s.Id == sample.LabSectionId).Select(s => s.WorkflowTemplateCode).FirstOrDefaultAsync();
        if (code == null)
        {
            var testIds = await _db.OrderTests.AsNoTracking().Where(t => t.SampleId == sample.Id).Select(t => t.TestId).ToListAsync();
            var sectionId = await _db.Tests.AsNoTracking().Where(t => testIds.Contains(t.Id) && t.LabSectionId != null).Select(t => t.LabSectionId).FirstOrDefaultAsync();
            if (sectionId != null) code = await _db.Sections.AsNoTracking().Where(s => s.Id == sectionId).Select(s => s.WorkflowTemplateCode).FirstOrDefaultAsync();
        }
        code ??= "CLINICAL";
        return await _db.WorkflowTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code)
               ?? await _db.WorkflowTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Code == "CLINICAL")
               ?? throw new ConflictException("Шаблони робочих процесів не налаштовано");
    }

    public async Task<object> AddStageAsync(string barcode, StageRequest req)
    {
        _policy.Require("Фіксація етапу обробки проби", Roles);
        if (string.IsNullOrWhiteSpace(req.StageCode)) throw ValidationException.Field("stageCode", "Вкажіть код етапу");
        var sample = await _samples.LoadByBarcodeAsync(barcode);
        var template = await TemplateForSampleAsync(sample);
        var stages = template.Stages;
        var code = req.StageCode.Trim().ToUpperInvariant();
        var newIdx = stages.FindIndex(s => s.Code == code);
        if (newIdx < 0) throw new ValidationException($"Етап '{code}' відсутній у шаблоні {template.Code}. Доступні: {string.Join(" → ", stages.Select(s => s.Code))}");
        if (sample.CurrentStage != null)
        {
            var curIdx = stages.FindIndex(s => s.Code == sample.CurrentStage);
            if (curIdx >= 0)
            {
                if (newIdx <= curIdx) throw new ConflictException($"Етап {code} не може йти після {sample.CurrentStage} (рух лише вперед за шаблоном {template.Code})");
                var cur = stages[curIdx];
                if (cur.AllowedNext.Count > 0 && !cur.AllowedNext.Contains(code)) throw new ConflictException($"Після {sample.CurrentStage} дозволені етапи: {string.Join(", ", cur.AllowedNext)}");
                var skippedRequired = stages.Skip(curIdx + 1).Take(newIdx - curIdx - 1).Where(s => s.IsRequired).Select(s => s.Code).ToList();
                if (skippedRequired.Count > 0 && cur.AllowedNext.Count == 0) throw new ConflictException($"Пропущено обов'язкові етапи: {string.Join(", ", skippedRequired)}");
            }
        }
        var ev = new LabSampleStageEvent { SampleId = sample.Id, StageCode = code, At = DateTime.UtcNow, ById = _current.EmployeeId, Note = req.Note, Temperature = req.Temperature, InstrumentId = req.InstrumentId, DataJson = req.Data == null ? null : JsonSerializer.Serialize(req.Data) };
        _db.StageEvents.Add(ev);
        var before = sample.CurrentStage;
        sample.CurrentStage = code;
        // Синхронізація статусу проби з етапом
        if (code is "STORED") sample.Status = SampleStatuses.Stored;
        else if (code is "DISPOSED") sample.Status = SampleStatuses.Disposed;
        else if (code is "ARCHIVED") sample.Status = SampleStatuses.Stored;
        else if (sample.Status == SampleStatuses.Received && newIdx > 0) sample.Status = SampleStatuses.Processing;
        _audit.Log("STAGE", "lab_sample_stage_event", ev.Id, new { stage = before }, new { sample.Barcode, code, req.Note, req.Temperature, req.InstrumentId });
        await _db.SaveChangesAsync();
        return new { sampleId = sample.Id, sample.Barcode, template = template.Code, currentStage = sample.CurrentStage, sampleStatus = sample.Status, eventId = ev.Id, at = ev.At,
            nextStages = stages.Skip(newIdx + 1).Where(s => stages[newIdx].AllowedNext.Count == 0 || stages[newIdx].AllowedNext.Contains(s.Code)).Select(s => new { s.Code, s.Name, s.IsRequired }) };
    }

    public async Task<object> StagesAsync(string barcode)
    {
        var sample = await _samples.LoadByBarcodeAsync(barcode);
        var template = await TemplateForSampleAsync(sample);
        var events = await _db.StageEvents.AsNoTracking().Where(e => e.SampleId == sample.Id).OrderBy(e => e.At).ToListAsync();
        var employees = await _db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.FullName);
        return new
        {
            sampleId = sample.Id, sample.Barcode, currentStage = sample.CurrentStage, template = new { template.Code, template.Name, stages = template.Stages },
            events = events.Select(e => new { e.Id, e.StageCode, stageName = template.Stages.FirstOrDefault(s => s.Code == e.StageCode)?.Name, e.At, e.ById, byName = e.ById != null && employees.TryGetValue(e.ById, out var n) ? n : null, e.Note, e.Temperature, e.InstrumentId, data = e.DataJson == null ? null : JsonSerializer.Deserialize<object>(e.DataJson) }),
            timeline = template.Stages.Select(s => new { s.Code, s.Name, s.IsRequired, done = events.Any(e => e.StageCode == s.Code), at = events.FirstOrDefault(e => e.StageCode == s.Code)?.At })
        };
    }

    public async Task DeleteStageEventAsync(string eventId)
    {
        _policy.Require("Видалення події етапу", LabRoles.Admin, LabRoles.Doctor);
        var ev = await _db.StageEvents.Include(e => e.Sample).FirstOrDefaultAsync(e => e.Id == eventId) ?? throw NotFoundException.For("Подія етапу", eventId);
        _db.StageEvents.Remove(ev);
        if (ev.Sample != null && ev.Sample.CurrentStage == ev.StageCode)
            ev.Sample.CurrentStage = await _db.StageEvents.Where(e => e.SampleId == ev.SampleId && e.Id != eventId).OrderByDescending(e => e.At).Select(e => e.StageCode).FirstOrDefaultAsync();
        _audit.Log("DELETE", "lab_sample_stage_event", eventId, new { ev.StageCode }, null);
        await _db.SaveChangesAsync();
    }

    public async Task<List<LabWorkflowTemplate>> TemplatesAsync() => await _db.WorkflowTemplates.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Code).ToListAsync();

    public async Task<LabWorkflowTemplate> UpsertTemplateAsync(string code, string name, List<WorkflowStage> stages)
    {
        _policy.Require("Редагування шаблону робочого процесу", LabRoles.Admin);
        if (stages.Count == 0) throw ValidationException.Field("stages", "Шаблон має містити хоча б один етап");
        var t = await _db.WorkflowTemplates.FirstOrDefaultAsync(x => x.Code == code.ToUpperInvariant());
        if (t == null) { t = new LabWorkflowTemplate { Code = code.ToUpperInvariant() }; _db.WorkflowTemplates.Add(t); }
        t.Name = name; t.Stages = stages; t.IsActive = true;
        _audit.Log("UPSERT", "lab_workflow_template", t.Code, null, new { name, stages = stages.Select(s => s.Code) });
        await _db.SaveChangesAsync();
        return t;
    }
}
