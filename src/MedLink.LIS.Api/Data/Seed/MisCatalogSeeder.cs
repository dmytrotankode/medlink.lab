// ТЗ розд. 21, 23: демо-каталог «Послуга ЕСОЗ × аналізатори» (db/seed/mis_catalog.json). Ідемпотентно: виконується, доки немає
// словника інтервенцій, тож дооновлює й наявну БД — додає показники, профілі, прилади, послуги обладнання, співставлення та пакети.
using System.Text.Json;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Data.Seed;

public sealed class MisCatalogSeeder
{
    private readonly LisDbContext _db;
    public MisCatalogSeeder(LisDbContext db) => _db = db;

    private static string? Str(JsonElement el, string name) => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static bool Bool(JsonElement el, string name, bool def) => el.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : def;
    private static int Int(JsonElement el, string name, int def) => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;
    private static double? Dbl(JsonElement el, string name) => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    public async Task SeedAsync()
    {
        if (await _db.Interventions.AnyAsync()) return;
        var cat = SeedService.ReadResource<JsonElement>("mis_catalog.json");
        var now = DateTime.UtcNow;

        // 1. Показники
        var bm = await _db.BiomaterialTypes.ToDictionaryAsync(b => b.Code, b => b.Id);
        var tubes = await _db.TubeTypes.ToDictionaryAsync(t => t.Code, t => t.Id);
        var sections = await _db.Sections.ToDictionaryAsync(s => s.Code, s => s.Id);
        var tests = await _db.Tests.ToDictionaryAsync(t => t.Code);
        foreach (var el in cat.GetProperty("tests").EnumerateArray())
        {
            var code = Str(el, "code")!;
            if (tests.ContainsKey(code)) continue;
            var t = new LabTestDefinition
            {
                Id = DeterministicGuid.For("test:" + code), Code = code, Name = Str(el, "name")!, ShortName = code, Unit = Str(el, "unit") ?? "",
                DecimalPlaces = Int(el, "decimalPlaces", 2), Category = Str(el, "category") ?? "BIOCHEM", BiomaterialTypeId = bm[Str(el, "biomaterialCode")!],
                TubeTypeId = tubes[Str(el, "tubeCode")!], LabSectionId = sections.GetValueOrDefault(Str(el, "labSectionCode") ?? ""), Price = (decimal)(Dbl(el, "price") ?? 0)
            };
            tests[code] = t;
            _db.Tests.Add(t);
            _db.ReferenceLayers.Add(new LabReferenceLayer
            {
                Id = DeterministicGuid.For("layer:REF-" + code + "-BASE"), TestId = t.Id, TestCode = code, LayerType = "BASELINE", PriorityOrder = 10,
                NormName = "Базова норма " + code, NormLow = Dbl(el, "low"), NormHigh = Dbl(el, "high"), CritLow = Dbl(el, "critLow"), CritHigh = Dbl(el, "critHigh"), Unit = t.Unit
            });
        }
        await _db.SaveChangesAsync();

        // 2. Профілі (послуги ЛІС): нові — створюються; наявні — оновлюються ознаки «обов'язковий/типовий» і додаються нетипові показники
        var profiles = await _db.Profiles.Include(p => p.Items).ToDictionaryAsync(p => p.Code);
        foreach (var el in cat.GetProperty("profiles").EnumerateArray())
        {
            var code = Str(el, "code")!;
            if (!profiles.TryGetValue(code, out var p))
            {
                p = new LabTestProfile
                {
                    Id = DeterministicGuid.For("profile:" + code), Code = code, Name = Str(el, "name")!, Category = Str(el, "category") ?? "",
                    TurnaroundHours = Int(el, "turnaroundHours", 24), Price = (decimal)(Dbl(el, "price") ?? 0),
                    DefaultBiomaterialTypeId = bm[Str(el, "biomaterialCode")!], DefaultTubeTypeId = tubes[Str(el, "tubeCode")!], Items = new()
                };
                profiles[code] = p;
                _db.Profiles.Add(p);
            }
            var order = p.Items.Count == 0 ? 0 : p.Items.Max(i => i.DisplayOrder);
            foreach (var it in el.GetProperty("items").EnumerateArray())
            {
                var test = tests[Str(it, "testCode")!];
                var item = p.Items.FirstOrDefault(i => i.TestId == test.Id);
                if (item == null)
                {
                    item = new LabTestProfileItem { ProfileId = p.Id, TestId = test.Id, DisplayOrder = ++order };
                    p.Items.Add(item);
                }
                item.IsRequired = Bool(it, "isRequired", false);
                item.IsDefaultSelected = Bool(it, "isDefault", true);
            }
        }
        await _db.SaveChangesAsync();

        // 3. Прилади КДЛ і ВРІТ, показники на приладі, послуги обладнання, локаути ВКЯ
        var types = await _db.AnalyzerTypes.ToDictionaryAsync(t => t.Code, t => t.Id);
        var departmentId = await _db.Departments.Select(d => d.Id).FirstOrDefaultAsync();
        foreach (var el in cat.GetProperty("analyzers").EnumerateArray())
        {
            var id = Str(el, "id")!;
            var an = await _db.Analyzers.Include(a => a.ParameterMap).FirstOrDefaultAsync(a => a.Id == id);
            if (an == null)
            {
                if (Bool(el, "existing", false)) continue;
                var online = Bool(el, "online", true);
                an = new LabAnalyzer
                {
                    Id = id, Code = Str(el, "code")!, Name = Str(el, "name")!, AnalyzerTypeId = types.GetValueOrDefault(Str(el, "analyzerTypeCode") ?? "", types.Values.First()),
                    // демо-прилади без коннектора: черга й ручне введення
                    ConnectorId = null, DepartmentId = departmentId, ConnectionMode = "TCP", TcpHost = "0.0.0.0", IsActive = true, IsOnline = online,
                    LastMessageAt = online ? now.AddMinutes(-2) : now.AddMinutes(-Int(el, "offlineMinutes", 46)), LastError = Str(el, "lastError"),
                    OfflineSince = online ? null : now.AddMinutes(-Int(el, "offlineMinutes", 46))
                };
                _db.Analyzers.Add(an);
            }
            an.Kind = Str(el, "kind") ?? AnalyzerKinds.Analyzer;
            an.Location = Str(el, "location");
            an.AvgTatMinutes = Int(el, "avgTatMinutes", 30);
            foreach (var code in el.GetProperty("tests").EnumerateArray().Select(x => x.GetString()!))
                if (!an.ParameterMap.Any(m => m.TestCode == code))
                    an.ParameterMap.Add(new LabAnalyzerParameterMap { AnalyzerId = an.Id, AnalyzerCode = code.Replace("_", ""), TestCode = code });
            foreach (var s in el.GetProperty("services").EnumerateArray())
            {
                var profile = profiles[Str(s, "profileCode")!];
                _db.AnalyzerServices.Add(new LabAnalyzerService
                {
                    AnalyzerId = an.Id, ProfileId = profile.Id, Priority = Int(s, "priority", 1), AvgTatMinutes = an.AvgTatMinutes, IsEnabled = true
                });
            }
            if (el.TryGetProperty("lockout", out var locks))
                foreach (var code in locks.EnumerateArray().Select(x => x.GetString()!))
                    _db.Lockouts.Add(new LabAnalyzerLockout { AnalyzerId = an.Id, TestCode = code, Reason = $"Lockout ВКЯ: {code} (правило 1_3s)", StartedAt = now.AddHours(-1) });
        }
        await _db.SaveChangesAsync();

        // 4. Словник інтервенцій ЕСОЗ (DEMO-) і співставлення
        foreach (var el in cat.GetProperty("interventions").EnumerateArray())
        {
            var code = Str(el, "code")!;
            var pathology = Bool(el, "pathology", false);
            var iv = new LabEhealthIntervention
            {
                Id = DeterministicGuid.For("intervention:" + code), Code = code, Name = Str(el, "name")!, ServiceGroup = Str(el, "serviceGroup"),
                Category = pathology ? "laboratory_procedure/pathology" : "laboratory_procedure", SourceVersion = "demo-2026.10", SyncedOn = now
            };
            _db.Interventions.Add(iv);
            var profileCode = Str(el, "profileCode");
            var hasTests = el.TryGetProperty("tests", out var testCodes);
            if (profileCode == null && !hasTests && !pathology) continue; // не співставлено
            var map = new LabInterventionMap
            {
                InterventionId = iv.Id, ProfileId = profileCode == null ? null : profiles[profileCode].Id, IsPathology = pathology,
                IndicatorSelectionMode = Str(el, "mode") ?? SelectionModes.Fixed, MinSelected = 1
            };
            if (hasTests)
            {
                var n = 0;
                foreach (var tc in testCodes.EnumerateArray().Select(x => x.GetString()!))
                    map.Tests.Add(new LabInterventionMapTest { MapId = map.Id, TestId = tests[tc].Id, IsDefault = true, IsRequired = map.IndicatorSelectionMode == SelectionModes.Fixed, DisplayOrder = ++n });
            }
            _db.InterventionMaps.Add(map);
        }

        // 5. Пакети послуг (дзеркало довідника МІС)
        foreach (var el in cat.GetProperty("packages").EnumerateArray())
            _db.ServicePackages.Add(new LabServicePackage
            {
                Code = Str(el, "code")!, Name = Str(el, "name")!, Context = Str(el, "context") ?? "ANY", Purpose = Str(el, "purpose"), IsCito = Bool(el, "isCito", false),
                ItemsJson = el.GetProperty("items").GetRawText(), SourceVersion = "demo-2026.10", SyncedOn = now
            });
        await _db.SaveChangesAsync();
    }
}
