// Стартовий набір біохімічного каталогу та довідників-сутностей (db/seed/biochem_catalog.json):
// одиниці виміру з правилами перерахунку, вікові групи, причини браку та відмови, нові методики/біоматеріали/пробірки,
// нові показники з нормами за статтю та віком, заповнення порожніх методик і LOINC у наявних показників.
// Безпечний на наявних базах: кожен набір застосовується один раз (lab_seed_marker); усередині набору додається лише відсутнє
// (за кодом), заповнюються лише порожні поля — ручні зміни адміністратора не перезаписуються і не повертаються.
using System.Text.Json;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Data.Seed;

public sealed class BiochemCatalogSeeder
{
    public const string PackReference = "ref-dictionaries-v1";
    public const string PackCatalog = "biochem-catalog-v1";
    public const string NormSource = "Стартовий набір — потребує затвердження лікарем-лаборантом";

    private readonly LisDbContext _db;
    public BiochemCatalogSeeder(LisDbContext db) => _db = db;

    private static string? Str(JsonElement el, string name) => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static double? Dbl(JsonElement el, string name) => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
    private static int Int(JsonElement el, string name, int def) => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;
    private static bool Bool(JsonElement el, string name, bool def) => el.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : def;

    public async Task SeedAsync()
    {
        var cat = SeedService.ReadResource<JsonElement>("biochem_catalog.json");
        var applied = (await _db.SeedMarkers.AsNoTracking().Select(m => m.Code).ToListAsync()).ToHashSet();
        if (!applied.Contains(PackReference)) { await SeedReferenceAsync(cat); await Mark(PackReference, "Одиниці, вікові групи, причини браку та відмови"); }
        if (!applied.Contains(PackCatalog)) { await SeedCatalogAsync(cat); await Mark(PackCatalog, "Біохімічний каталог: показники, норми, методики, пробірки"); }
        // Міграція вільного тексту одиниць у довідник: ідемпотентна, зачіпає лише рядки без UnitId
        await UnitCatalog.LinkAllAsync(_db);
    }

    private async Task Mark(string code, string note)
    {
        _db.SeedMarkers.Add(new LabSeedMarker { Code = code, Note = note, AppliedOn = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ довідники-сутності
    private async Task SeedReferenceAsync(JsonElement cat)
    {
        var units = await _db.Units.ToDictionaryAsync(u => u.Code);
        var nextUnit = (units.Count == 0 ? 0 : units.Values.Max(u => u.Id)) + 1;
        foreach (var el in cat.GetProperty("units").EnumerateArray())
        {
            var code = Str(el, "code")!;
            if (units.ContainsKey(code)) continue;
            // Якщо така назва вже є (напр. створена з вільного тексту) — не дублюємо
            var name = Str(el, "name") ?? "";
            if (units.Values.Any(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
            var u = new LabUnit { Id = nextUnit++, Code = code, Name = name, FullName = Str(el, "fullName"), Dimension = Str(el, "dimension") ?? "OTHER", SystemKind = Str(el, "system") ?? "OTHER", SortOrder = Int(el, "sort", 100) };
            units[code] = u; _db.Units.Add(u);
        }
        var groups = await _db.AgeGroups.ToDictionaryAsync(g => g.Code);
        var nextGroup = (groups.Count == 0 ? 0 : groups.Values.Max(g => g.Id)) + 1;
        foreach (var el in cat.GetProperty("ageGroups").EnumerateArray())
        {
            var code = Str(el, "code")!;
            if (groups.ContainsKey(code)) continue;
            var g = new LabAgeGroup { Id = nextGroup++, Code = code, Name = Str(el, "name")!, AgeFrom = Dbl(el, "from") ?? 0, AgeTo = Dbl(el, "to") ?? 120, AgeUnit = Str(el, "unit") ?? "YEARS", SortOrder = Int(el, "sort", 100) };
            groups[code] = g; _db.AgeGroups.Add(g);
        }
        var rej = await _db.RejectionReasons.ToDictionaryAsync(r => r.Code);
        var nextRej = (rej.Count == 0 ? 0 : rej.Values.Max(r => r.Id)) + 1;
        var order = 0;
        foreach (var el in cat.GetProperty("rejectionReasons").EnumerateArray())
        {
            var code = Str(el, "code")!; order += 10;
            if (rej.ContainsKey(code)) continue;
            var r = new LabRejectionReason { Id = nextRej++, Code = code, Name = Str(el, "name")!, Category = Str(el, "category") ?? "OTHER", RequiresRecollection = Bool(el, "recollect", true), SortOrder = order };
            rej[code] = r; _db.RejectionReasons.Add(r);
        }
        var can = await _db.CancelReasons.ToDictionaryAsync(r => r.Code);
        var nextCan = (can.Count == 0 ? 0 : can.Values.Max(r => r.Id)) + 1;
        order = 0;
        foreach (var el in cat.GetProperty("cancelReasons").EnumerateArray())
        {
            var code = Str(el, "code")!; order += 10;
            if (can.ContainsKey(code)) continue;
            var r = new LabCancelReason { Id = nextCan++, Code = code, Name = Str(el, "name")!, Category = Str(el, "category") ?? "OTHER", SortOrder = order };
            can[code] = r; _db.CancelReasons.Add(r);
        }
        await _db.SaveChangesAsync();

        // Правила перерахунку: додаються лише відсутні пари (показник, з, до); показники, яких немає в базі, пропускаються
        var testCodes = (await _db.Tests.AsNoTracking().Select(t => t.Code).ToListAsync()).ToHashSet();
        var allTestCodes = testCodes.Union(cat.GetProperty("tests").EnumerateArray().Select(t => Str(t, "code")!)).ToHashSet();
        var existing = (await _db.UnitConversions.AsNoTracking().Select(c => new { c.TestCode, c.FromUnitId, c.ToUnitId }).ToListAsync()).Select(c => (c.TestCode, c.FromUnitId, c.ToUnitId)).ToHashSet();
        foreach (var el in cat.GetProperty("conversions").EnumerateArray())
        {
            var tc = Str(el, "testCode")!;
            if (!allTestCodes.Contains(tc) || !units.TryGetValue(Str(el, "from")!, out var from) || !units.TryGetValue(Str(el, "to")!, out var to)) continue;
            if (!existing.Add((tc, from.Id, to.Id))) continue;
            _db.UnitConversions.Add(new LabUnitConversion { Id = DeterministicGuid.For($"unitconv:{tc}:{from.Code}:{to.Code}"), TestCode = tc, FromUnitId = from.Id, ToUnitId = to.Id, Factor = Dbl(el, "factor") ?? 1, Offset = Dbl(el, "offset") ?? 0, Note = Str(el, "note") });
        }
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ біохімічний каталог
    private async Task SeedCatalogAsync(JsonElement cat)
    {
        // методики
        var methods = await _db.MethodTypes.ToDictionaryAsync(m => m.Code);
        var nextMethod = (methods.Count == 0 ? 0 : methods.Values.Max(m => m.Id)) + 1;
        foreach (var el in cat.GetProperty("methods").EnumerateArray())
        {
            var code = Str(el, "code")!;
            if (methods.ContainsKey(code)) continue;
            var m = new LabMethodType { Id = nextMethod++, Code = code, Name = Str(el, "name")! };
            methods[code] = m; _db.MethodTypes.Add(m);
        }
        // біоматеріали
        var bms = await _db.BiomaterialTypes.ToDictionaryAsync(b => b.Code);
        var nextBm = (bms.Count == 0 ? 0 : bms.Values.Max(b => b.Id)) + 1;
        foreach (var el in cat.GetProperty("biomaterials").EnumerateArray())
        {
            var code = Str(el, "code")!;
            if (bms.ContainsKey(code)) continue;
            var b = new LabBiomaterialType { Id = nextBm++, Code = code, Name = Str(el, "name")!, DefaultContainer = Str(el, "defaultContainer"), StabilityHours = Int(el, "stabilityHours", 24), TemperatureRegime = Str(el, "temperatureRegime") ?? "+2..+8°C" };
            bms[code] = b; _db.BiomaterialTypes.Add(b);
        }
        // пробірки
        var tubes = await _db.TubeTypes.ToDictionaryAsync(t => t.Code);
        var nextTube = (tubes.Count == 0 ? 0 : tubes.Values.Max(t => t.Id)) + 1;
        foreach (var el in cat.GetProperty("tubes").EnumerateArray())
        {
            var code = Str(el, "code")!;
            if (tubes.ContainsKey(code)) continue;
            var t = new LabTubeType
            {
                Id = nextTube++, Code = code, Name = Str(el, "name")!, ColorCode = Str(el, "colorCode") ?? "#4274A7", Anticoagulant = Str(el, "anticoagulant"), VolumeMl = Dbl(el, "volumeMl") ?? 4,
                OrderOfDrawIndex = Int(el, "orderOfDrawIndex", 99), InversionsCount = Int(el, "inversionsCount", 0), UsableVolumeMl = Dbl(el, "usableVolumeMl"), MaxTestsPerTube = el.TryGetProperty("maxTestsPerTube", out var mt) && mt.ValueKind == JsonValueKind.Number ? mt.GetInt32() : null
            };
            tubes[code] = t; _db.TubeTypes.Add(t);
        }
        await _db.SaveChangesAsync();

        var units = await _db.Units.ToDictionaryAsync(u => u.Code);
        var groups = await _db.AgeGroups.ToDictionaryAsync(g => g.Code);
        var sections = await _db.Sections.ToDictionaryAsync(s => s.Code, s => s.Id);
        var tests = await _db.Tests.ToDictionaryAsync(t => t.Code);
        var layerIds = (await _db.ReferenceLayers.AsNoTracking().Select(l => l.Id).ToListAsync()).ToHashSet();

        // нові показники та їхні норми
        foreach (var el in cat.GetProperty("tests").EnumerateArray())
        {
            var code = Str(el, "code")!;
            if (tests.ContainsKey(code)) continue;
            var unitText = Str(el, "unit") ?? "";
            var unit = units.Values.FirstOrDefault(u => string.Equals(u.Name, unitText, StringComparison.OrdinalIgnoreCase));
            var methodRef = Str(el, "methodRef");
            var t = new LabTestDefinition
            {
                Id = DeterministicGuid.For("test:" + code), Code = code, Name = Str(el, "name")!, ShortName = Str(el, "shortName"), LoincCode = Str(el, "loincCode"),
                Unit = unit?.Name ?? unitText, UnitId = unit?.Id, DecimalPlaces = Int(el, "decimalPlaces", 2), ResultType = Str(el, "resultType") ?? "NUMERIC", Formula = Str(el, "formula"),
                Category = Str(el, "category") ?? "BIOCHEM", BiomaterialTypeId = bms[Str(el, "biomaterialCode")!].Id, TubeTypeId = tubes[Str(el, "tubeCode")!].Id,
                MethodId = methodRef != null && methods.TryGetValue(methodRef, out var meth) ? meth.Id : null, MethodCode = Str(el, "methodCode"),
                LabSectionId = sections.GetValueOrDefault(Str(el, "labSectionCode") ?? ""), Price = (decimal)(Dbl(el, "price") ?? 0)
            };
            tests[code] = t; _db.Tests.Add(t);
            foreach (var n in el.GetProperty("norms").EnumerateArray()) AddLayer(t, n, groups, units, layerIds);
        }

        // наявні показники: заповнити лише порожнє (методика, LOINC)
        foreach (var p in cat.GetProperty("patches").EnumerateObject())
        {
            if (!tests.TryGetValue(p.Name, out var t)) continue;
            var methodRef = Str(p.Value, "methodRef");
            if (t.MethodId == null && methodRef != null && methods.TryGetValue(methodRef, out var meth)) t.MethodId = meth.Id;
            var loinc = Str(p.Value, "loincCode");
            if (string.IsNullOrWhiteSpace(t.LoincCode) && loinc != null) t.LoincCode = loinc;
        }

        // додаткові (зокрема дитячі) норми наявних показників
        foreach (var p in cat.GetProperty("extraNorms").EnumerateObject())
        {
            if (!tests.TryGetValue(p.Name, out var t)) continue;
            foreach (var n in p.Value.EnumerateArray()) AddLayer(t, n, groups, units, layerIds);
        }
        await _db.SaveChangesAsync();

        // Норми наявного сіду теж позначаємо як стартові (лише порожнє джерело)
        var seeded = new HashSet<string>();
        foreach (var e in SeedService.ReadResource<List<JsonElement>>("reference_layers.json"))
        {
            var id = Str(e, "id")!;
            seeded.Add(Guid.TryParse(id, out _) ? id : DeterministicGuid.For("layer:" + id));
        }
        foreach (var e in SeedService.ReadResource<JsonElement>("mis_catalog.json").GetProperty("tests").EnumerateArray())
            seeded.Add(DeterministicGuid.For("layer:REF-" + Str(e, "code") + "-BASE"));
        foreach (var l in await _db.ReferenceLayers.Where(l => l.Source == null).ToListAsync())
            if (seeded.Contains(l.Id)) l.Source = NormSource;
        await _db.SaveChangesAsync();
    }

    private static string SexUk(string s) => s switch { "M" => "чоловіки", "F" => "жінки", _ => "усі" };

    private void AddLayer(LabTestDefinition t, JsonElement n, Dictionary<string, LabAgeGroup> groups, Dictionary<string, LabUnit> units, HashSet<string> existingIds)
    {
        var gcode = Str(n, "g");
        var sex = Str(n, "s") ?? "ANY";
        var id = DeterministicGuid.For($"layer:REF-{t.Code}-{gcode ?? "BASE"}-{sex}");
        if (!existingIds.Add(id)) return;
        groups.TryGetValue(gcode ?? "", out var g);
        var baseline = gcode == null;
        _db.ReferenceLayers.Add(new LabReferenceLayer
        {
            Id = id, TestId = t.Id, TestCode = t.Code, MethodCode = t.MethodCode,
            LayerType = baseline ? "BASELINE" : "DEMOGRAPHIC", PriorityOrder = baseline ? 10 : (g != null && (g.AgeUnit != "YEARS" || g.AgeTo < 18) ? 41 : 40), // дитячі групи — на крок вище за загальні вікові шари
            NormName = baseline ? "Базова норма " + t.Code : $"{t.Code}: {SexUk(sex)}, {g?.Name ?? gcode}",
            Gender = sex, IsGender = sex != "ANY", AgeUnit = g?.AgeUnit ?? "YEARS", AgeFrom = g?.AgeFrom ?? 0, AgeTo = g?.AgeTo ?? 120, IsAge = !baseline, AgeGroupId = g?.Id,
            NormLow = Dbl(n, "lo"), NormHigh = Dbl(n, "hi"), CritLow = Dbl(n, "cl"), CritHigh = Dbl(n, "ch"), NormText = Str(n, "t"),
            Unit = t.Unit, UnitId = t.UnitId, Source = NormSource
        });
    }
}
