// План пробірок (FR-PRE-004): вибір тари для тесту, правила об'єднання/розділення, попередній перегляд для реєстратора/медсестри
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Core.Preanalytics;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

/// <summary>Тест, обраний до замовлення, з профілем-джерелом і визначеним типом тари.</summary>
public sealed record SelectedTest(LabTestDefinition Test, LabTestProfile? Profile, int TubeTypeId)
{
    /// <summary>Зовнішня лабораторія-виконавець за маршрутом за замовчуванням (null — власна лабораторія).</summary>
    public string? PerformerId { get; init; }
    public string? PerformerCode { get; init; }
}

public sealed class TubePlanService
{
    private readonly LisDbContext _db;
    public TubePlanService(LisDbContext db) => _db = db;

    public static readonly Dictionary<string, string> ReasonTexts = new()
    {
        [TubePlanReasons.SeparateRequired] = "Тест потребує окремої пробірки",
        [TubePlanReasons.VolumeSplit] = "Розділено: перевищено корисний об'єм пробірки",
        [TubePlanReasons.TestLimitSplit] = "Розділено: перевищено ліміт тестів на пробірку",
        [TubePlanReasons.CompatibilityGroup] = "Окрема група сумісності тестів",
        [TubePlanReasons.OverCapacity] = "Тест потребує більше матеріалу, ніж вміщує тара"
    };

    /// <summary>Корисний об'єм тари для контролю об'єднання; null — не контролюється.</summary>
    public static double? CapacityOf(LabTubeType t) => t.UsableVolumeMl is > 0 ? t.UsableVolumeMl : null;

    /// <summary>Розгортає профілі та окремі тести в перелік унікальних тестів (без уже наявних у замовленні).</summary>
    public async Task<(List<SelectedTest> Tests, List<LabTestProfile> Profiles, List<LabTestDefinition> Singles)> ResolveAsync(
        IEnumerable<string> profileIds, IEnumerable<string> testIds, ISet<string> existingCodes)
    {
        var profiles = new List<LabTestProfile>();
        foreach (var pid in profileIds.Distinct())
        {
            var p = await _db.Profiles.Include(x => x.Items).ThenInclude(i => i.Test).FirstOrDefaultAsync(x => (x.Id == pid || x.Code == pid) && x.IsActive)
                    ?? throw NotFoundException.For("Профіль", pid);
            profiles.Add(p);
        }
        var singles = new List<LabTestDefinition>();
        foreach (var tid in testIds.Distinct())
        {
            var t = await _db.Tests.FirstOrDefaultAsync(x => (x.Id == tid || x.Code == tid) && x.IsActive) ?? throw NotFoundException.For("Тест", tid);
            singles.Add(t);
        }

        var codes = new HashSet<string>(existingCodes);
        var result = new List<SelectedTest>();
        var candidateIds = profiles.SelectMany(p => p.Items).Where(i => i.Test != null).Select(i => i.Test!.Id).Concat(singles.Select(t => t.Id)).Distinct().ToList();
        // Маршрути send-out за замовчуванням (lab_performer_test.is_default_route)
        var routes = await _db.PerformerTests.AsNoTracking().Include(x => x.Performer)
            .Where(x => candidateIds.Contains(x.TestId) && x.IsDefaultRoute && x.IsActive && x.Performer!.IsActive && x.Performer.Kind == PerformerKinds.External && x.Performer.RecordState != RecordStates.Deleted)
            .ToDictionaryAsync(x => x.TestId, x => (x.PerformerId, x.Performer!.Code));
        SelectedTest Routed(SelectedTest s) => routes.TryGetValue(s.Test.Id, out var r) ? s with { PerformerId = r.PerformerId, PerformerCode = r.Code } : s;
        foreach (var p in profiles)
            foreach (var item in p.Items.OrderBy(i => i.DisplayOrder))
            {
                if (item.Test == null || !codes.Add(item.Test.Code)) continue;
                result.Add(Routed(new SelectedTest(item.Test, p, item.Test.TubeTypeId ?? p.DefaultTubeTypeId ?? await DefaultTubeForBiomaterialAsync(item.Test.BiomaterialTypeId))));
            }
        foreach (var t in singles)
        {
            if (!codes.Add(t.Code)) continue;
            result.Add(Routed(new SelectedTest(t, null, t.TubeTypeId ?? await DefaultTubeForBiomaterialAsync(t.BiomaterialTypeId))));
        }
        return (result, profiles, singles);
    }

    /// <summary>Будує план з урахуванням наявних первинних пробірок замовлення (для дозамовлення).</summary>
    public async Task<TubePlan> BuildAsync(IReadOnlyList<SelectedTest> tests, LabOrder? order)
    {
        var types = await _db.TubeTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => new TubePlanTubeType
        {
            Id = t.Id, OrderOfDrawIndex = t.OrderOfDrawIndex, CapacityMl = CapacityOf(t), MaxTestsPerTube = t.MaxTestsPerTube
        });
        var existing = new List<TubePlanExistingTube>();
        if (order != null)
            foreach (var s in order.Samples.Where(s => s.ParentSampleId == null && s.Status != SampleStatuses.Rejected && s.Status != SampleStatuses.Disposed).OrderBy(s => s.GroupNumb))
                existing.Add(new TubePlanExistingTube
                {
                    SampleId = s.Id, BiomaterialTypeId = s.BiomaterialTypeId, TubeTypeId = s.TubeTypeId, GroupNumb = s.GroupNumb,
                    Tests = order.Tests.Where(t => t.SampleId == s.Id && t.Test != null).Select(t => ToPlanTest(t.Test!, s.TubeTypeId, t.Performer?.Code ?? (t.PerformerId == null ? null : "EXT"))).ToList()
                });
        return TubePlanner.Plan(tests.Select(t => ToPlanTest(t.Test, t.TubeTypeId, t.PerformerCode)), types, existing);
    }

    public async Task<TubePlanDto> PreviewAsync(TubePlanRequest req)
    {
        LabOrder? order = null;
        if (!string.IsNullOrWhiteSpace(req.OrderId))
            order = await _db.Orders.AsNoTracking().Include(o => o.Samples).Include(o => o.Tests).ThenInclude(t => t.Test).Include(o => o.Tests).ThenInclude(t => t.Performer)
                        .FirstOrDefaultAsync(o => o.Id == req.OrderId) ?? throw NotFoundException.For("Замовлення", req.OrderId!);
        var existingCodes = new HashSet<string>(order?.Tests.Select(t => t.TestCode) ?? Enumerable.Empty<string>());
        var (tests, _, _) = await ResolveAsync(req.ProfileIds, req.TestIds, existingCodes);
        var plan = await BuildAsync(tests, order);

        var tubeTypes = await _db.TubeTypes.AsNoTracking().ToDictionaryAsync(t => t.Id);
        var biomaterials = await _db.BiomaterialTypes.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name);
        var barcodes = order?.Samples.ToDictionary(s => s.Id, s => s.Barcode) ?? new();
        var dto = new TubePlanDto { Warnings = plan.Warnings };
        var index = 0;
        foreach (var t in plan.Tubes.Where(t => t.NewTests.Count > 0))
        {
            tubeTypes.TryGetValue(t.TubeTypeId, out var tt);
            dto.Tubes.Add(new TubePlanItemDto
            {
                Index = ++index, ExistingSampleId = t.ExistingSampleId, ExistingBarcode = t.ExistingSampleId == null ? null : barcodes.GetValueOrDefault(t.ExistingSampleId),
                TubeTypeId = t.TubeTypeId, TubeCode = tt?.Code, TubeName = tt?.Name, ColorCode = tt?.ColorCode, Anticoagulant = tt?.Anticoagulant,
                InversionsCount = tt?.InversionsCount ?? 0, OrderOfDrawIndex = t.OrderOfDrawIndex,
                BiomaterialTypeId = t.BiomaterialTypeId, BiomaterialName = biomaterials.GetValueOrDefault(t.BiomaterialTypeId),
                CompatibilityGroup = t.CompatibilityGroup, IsSeparate = t.IsSeparate, CapacityMl = t.CapacityMl,
                UsedVolumeMl = Math.Round(t.UsedVolumeMl, 3), MaxTests = t.MaxTests,
                Reasons = t.Reasons, ReasonTexts = t.Reasons.Select(r => ReasonTexts.GetValueOrDefault(r, r)).ToList(),
                Tests = t.ExistingTests.Select(x => ToDto(x, false)).Concat(t.NewTests.Select(x => ToDto(x, true))).ToList()
            });
        }
        dto.NewTubesCount = dto.Tubes.Count(t => t.ExistingSampleId == null);
        return dto;
    }

    private static TubePlanTestDto ToDto(TubePlanTest t, bool isNew) => new() { TestId = t.TestId, Code = t.Code, Name = t.Name, RequiredVolumeMl = t.RequiredVolumeMl, IsNew = isNew };

    /// <summary>Тести, що відправляються в зовнішню лабораторію, групуються в окрему тару цієї лабораторії (група SENDOUT:&lt;код&gt;).</summary>
    private static TubePlanTest ToPlanTest(LabTestDefinition t, int tubeTypeId, string? performerCode = null) => new()
    {
        TestId = t.Id, Code = t.Code, Name = t.Name, BiomaterialTypeId = t.BiomaterialTypeId, TubeTypeId = tubeTypeId,
        RequiresSeparateTube = t.RequiresSeparateTube, MaxTestsPerTube = t.MaxTestsPerTube, RequiredVolumeMl = t.RequiredVolumeMl,
        CompatibilityGroup = performerCode != null ? "SENDOUT:" + performerCode : t.TubeCompatibilityGroup
    };

    private async Task<int> DefaultTubeForBiomaterialAsync(int biomaterialTypeId)
    {
        // Евристика за назвою біоматеріалу → тип пробірки; інакше перша активна пробірка
        var bm = await _db.BiomaterialTypes.AsNoTracking().FirstOrDefaultAsync(b => b.Id == biomaterialTypeId);
        var name = bm?.Name?.ToLowerInvariant() ?? "";
        string? code = name.Contains("сеч") ? "URINE_CONTAINER" : name.Contains("кал") ? "STOOL_CONTAINER" : name.Contains("сироват") ? "SERUM_GEL"
            : name.Contains("плазм") ? "CITRATE" : name.Contains("кров") ? "EDTA_CBC" : null;
        var tube = code == null ? null : await _db.TubeTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code);
        tube ??= await _db.TubeTypes.AsNoTracking().OrderBy(t => t.Id).FirstOrDefaultAsync(t => t.IsActive);
        return tube?.Id ?? throw new ValidationException("Довідник пробірок порожній");
    }
}
