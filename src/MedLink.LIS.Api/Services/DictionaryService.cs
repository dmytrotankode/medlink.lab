// Універсальний CRUD довідників: /dictionaries/{name} (біоматеріали, пробірки, методики, типи аналізаторів, тести,
// профілі, reflex-правила, збудники, антибіотики, EUCAST, підрозділи, співробітники). Soft delete → isActive=false; 409 при залежностях.
using System.Text.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Core.Clinical;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed partial class DictionaryService
{
    private static readonly string[] CoreNames = { "biomaterials", "tube-types", "method-types", "analyzer-types", "tests", "profiles", "reflex-rules", "organisms", "antibiotics", "eucast-breakpoints", "departments", "employees", "units", "unit-conversions", "age-groups", "rejection-reasons", "cancel-reasons" };
    /// <summary>Усі універсальні довідники: основні + реєстр простих (патоморфологія, синоніми, календар, номенклатура реагентів).</summary>
    public static readonly string[] Names = CoreNames.Concat(DictionaryAdapters.All.Keys).ToArray();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true, ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };

    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly OrgDictionaryService _org;

    public DictionaryService(LisDbContext db, IRolePolicy policy, IAuditService audit, OrgDictionaryService org) { _db = db; _policy = policy; _audit = audit; _org = org; }

    private static IDictAdapter? Adapter(string name) => DictionaryAdapters.All.TryGetValue(name, out var a) ? a : null;

    private static string Table(string name) => Adapter(name)?.Table ?? name switch
    {
        "biomaterials" => "lab_biomaterial_type", "tube-types" => "lab_tube_type", "method-types" => "lab_method_type", "analyzer-types" => "lab_analyzer_type",
        "tests" => "lab_test_definition", "profiles" => "lab_test_profile", "reflex-rules" => "lab_reflex_rule", "organisms" => "lab_micro_organism",
        "antibiotics" => "lab_antibiotic", "eucast-breakpoints" => "lab_eucast_breakpoint", "departments" => "org_department", "employees" => "org_employee",
        "units" => "lab_unit", "unit-conversions" => "lab_unit_conversion", "age-groups" => "lab_age_group", "rejection-reasons" => "lab_rejection_reason", "cancel-reasons" => "lab_cancel_reason",
        _ => throw new NotFoundException($"Довідник '{name}' не існує. Доступні: {string.Join(", ", Names)}")
    };

    // ------------------------------------------------------------------ list / get
    public async Task<object> ListAsync(string name, string? search, bool? isActive, PagingQuery paging) =>
        (await FilteredAsync(name, search, isActive)).ToPaged(paging);

    /// <summary>Усі записи довідника за фільтрами (без пагінації) — для списку й експорту.</summary>
    public async Task<List<object>> FilteredAsync(string name, string? search, bool? isActive)
    {
        Table(name);
        var s = search?.Trim().ToLowerInvariant();
        var adapter = Adapter(name);
        if (adapter != null) return Filter(await adapter.ListAsync(_db), adapter.Search, x => IsActiveOf(x), s, isActive);
        switch (name)
        {
            case "biomaterials": return Filter(await _db.BiomaterialTypes.AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name}", x => x.IsActive, s, isActive);
            case "tube-types": return Filter(await _db.TubeTypes.AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.OrderOfDrawIndex).ThenBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name}", x => x.IsActive, s, isActive);
            case "method-types": return Filter(await _db.MethodTypes.AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name}", x => x.IsActive, s, isActive);
            case "analyzer-types": return Filter(await _db.AnalyzerTypes.AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name} {x.Manufacturer} {x.ExchType}", x => x.IsActive, s, isActive);
            case "tests":
            {
                // пошук і за синонімами (FR-GAP-052): «глікований гемоглобін» знаходить HBA1C
                var syn = (await _db.TestSynonyms.AsNoTracking().Where(y => y.IsActive && y.RecordState != RecordStates.Deleted).ToListAsync()).GroupBy(y => y.TestCode).ToDictionary(g => g.Key, g => string.Join(" ", g.Select(y => y.Synonym)));
                return Filter(await _db.Tests.AsNoTracking().Include(x => x.BiomaterialType).Include(x => x.TubeType).Include(x => x.Method).Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.Category).ThenBy(x => x.Code).ToListAsync(),
                    x => $"{x.Code} {x.Name} {x.ShortName} {x.LoincCode} {x.Category} {(syn.TryGetValue(x.Code, out var sy) ? sy : "")}", x => x.IsActive, s, isActive);
            }
            case "profiles": return Filter(await _db.Profiles.AsNoTracking().Include(x => x.Items).ThenInclude(i => i.Test).Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.Code).ToListAsync(), x => $"{x.Code} {x.Name} {x.Category}", x => x.IsActive, s, isActive);
            case "reflex-rules": return Filter(await _db.ReflexRules.AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.TriggerTestCode).ToListAsync(), x => $"{x.TriggerTestCode} {x.ReflexTestCode} {x.Description}", x => x.IsActive, s, isActive);
            case "organisms": return Filter(await _db.Organisms.AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.LatinName} {x.CommonName}", x => x.IsActive, s, isActive);
            case "antibiotics": return Filter(await _db.Antibiotics.AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name} {x.GroupName}", x => x.IsActive, s, isActive);
            case "eucast-breakpoints": return Filter(await _db.EucastBreakpoints.AsNoTracking().Include(x => x.Organism).Include(x => x.Antibiotic).Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.OrganismId).ThenBy(x => x.AntibioticId).ToListAsync(), x => $"{x.Organism?.Code} {x.Organism?.LatinName} {x.Antibiotic?.Code} {x.Antibiotic?.Name} {x.EucastVersion}", x => x.IsActive, s, isActive);
            case "units": return Filter(await _db.Units.AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name} {x.FullName} {x.Dimension}", x => x.IsActive, s, isActive);
            case "unit-conversions": return Filter(await _db.UnitConversions.AsNoTracking().Include(x => x.FromUnit).Include(x => x.ToUnit).Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.TestCode).ThenBy(x => x.FromUnitId).ToListAsync(), x => $"{x.TestCode} {x.FromUnit?.Name} {x.ToUnit?.Name} {x.Note}", x => x.IsActive, s, isActive);
            case "age-groups": return Filter(await _db.AgeGroups.AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name}", x => x.IsActive, s, isActive);
            case "rejection-reasons": return Filter(await _db.RejectionReasons.AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name} {x.Category}", x => x.IsActive, s, isActive);
            case "cancel-reasons": return Filter(await _db.CancelReasons.AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name} {x.Category}", x => x.IsActive, s, isActive);
            case "departments": return Filter(await _org.DepartmentsAsync(), x => $"{x.Code} {x.Name} {x.Address}", x => x.IsActive, s, isActive);
            case "employees": return Filter(await _org.EmployeesAsync(), x => $"{x.FullName} {x.Position} {x.LabRole} {x.DepartmentName} {x.Email}", x => x.IsActive, s, isActive);
        }
        throw new NotFoundException($"Довідник '{name}' не існує");
    }

    private static bool IsActiveOf(object x) => x.GetType().GetProperty("IsActive")?.GetValue(x) is not bool b || b;

    private static List<object> Filter<T>(List<T> items, Func<T, string> text, Func<T, bool> active, string? search, bool? isActive)
    {
        IEnumerable<T> q = items;
        if (!string.IsNullOrEmpty(search)) q = q.Where(x => text(x).ToLowerInvariant().Contains(search));
        if (isActive.HasValue) q = q.Where(x => active(x) == isActive.Value);
        return q.Cast<object>().ToList();
    }

    public async Task<object> GetAsync(string name, string id)
    {
        Table(name);
        if (Adapter(name) is { } ad) return await ad.FindAsync(_db, id, false) ?? throw NotFoundException.For($"Запис довідника {name}", id);
        object? item = name switch
        {
            "biomaterials" => await _db.BiomaterialTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "tube-types" => await _db.TubeTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "method-types" => await _db.MethodTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "analyzer-types" => await _db.AnalyzerTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "tests" => await _db.Tests.AsNoTracking().Include(x => x.BiomaterialType).Include(x => x.TubeType).Include(x => x.Method).FirstOrDefaultAsync(x => x.Id == id || x.Code == id),
            "profiles" => await _db.Profiles.AsNoTracking().Include(x => x.Items.OrderBy(i => i.DisplayOrder)).ThenInclude(i => i.Test).FirstOrDefaultAsync(x => x.Id == id || x.Code == id),
            "reflex-rules" => await _db.ReflexRules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id),
            "organisms" => await _db.Organisms.AsNoTracking().FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "antibiotics" => await _db.Antibiotics.AsNoTracking().FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "eucast-breakpoints" => await _db.EucastBreakpoints.AsNoTracking().Include(x => x.Organism).Include(x => x.Antibiotic).FirstOrDefaultAsync(x => x.Id == id),
            "units" => await _db.Units.AsNoTracking().FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "unit-conversions" => await _db.UnitConversions.AsNoTracking().Include(x => x.FromUnit).Include(x => x.ToUnit).FirstOrDefaultAsync(x => x.Id == id),
            "age-groups" => await _db.AgeGroups.AsNoTracking().FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "rejection-reasons" => await _db.RejectionReasons.AsNoTracking().FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "cancel-reasons" => await _db.CancelReasons.AsNoTracking().FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "departments" => await _org.DepartmentAsync(id),
            "employees" => await _org.EmployeeAsync(id),
            _ => null
        };
        return item ?? throw NotFoundException.For($"Запис довідника {name}", id);
    }

    // ------------------------------------------------------------------ create / update
    public async Task<object> CreateAsync(string name, JsonElement body)
    {
        _policy.Require($"Створення запису довідника {name}", LabRoles.Admin, LabRoles.Doctor);
        var table = Table(name);
        object entity;
        if (Adapter(name) is { } ad)
        {
            var e = ad.FromJson(body, Json);
            await ad.ValidateAsync(_db, e, true);
            ad.Add(_db, e);
            _audit.Log("CREATE", table, IdOf(e), null, e);
            await _db.SaveChangesAsync();
            await ad.OnChangedAsync(_db);
            return await GetAsync(name, IdOf(e));
        }
        switch (name)
        {
            case "biomaterials": { var e = Deserialize<LabBiomaterialType>(body); if (e.Id == 0) e.Id = await NextIntId(_db.BiomaterialTypes.Select(x => x.Id)); await EnsureUniqueCode(_db.BiomaterialTypes.AnyAsync(x => x.Code == e.Code), e.Code); _db.BiomaterialTypes.Add(e); entity = e; break; }
            case "tube-types": { var e = Deserialize<LabTubeType>(body); if (e.Id == 0) e.Id = await NextIntId(_db.TubeTypes.Select(x => x.Id)); await EnsureUniqueCode(_db.TubeTypes.AnyAsync(x => x.Code == e.Code), e.Code); _db.TubeTypes.Add(e); entity = e; break; }
            case "method-types": { var e = Deserialize<LabMethodType>(body); if (e.Id == 0) e.Id = await NextIntId(_db.MethodTypes.Select(x => x.Id)); await EnsureUniqueCode(_db.MethodTypes.AnyAsync(x => x.Code == e.Code), e.Code); _db.MethodTypes.Add(e); entity = e; break; }
            case "analyzer-types": { var e = Deserialize<LabAnalyzerType>(body); if (e.Id == 0) e.Id = await NextIntId(_db.AnalyzerTypes.Select(x => x.Id)); await EnsureUniqueCode(_db.AnalyzerTypes.AnyAsync(x => x.Code == e.Code), e.Code); _db.AnalyzerTypes.Add(e); entity = e; break; }
            case "tests":
            {
                var e = Deserialize<LabTestDefinition>(body);
                if (string.IsNullOrWhiteSpace(e.Code) || string.IsNullOrWhiteSpace(e.Name)) throw new ValidationException("Код та назва тесту обов'язкові");
                e.Code = e.Code.ToUpperInvariant();
                await EnsureUniqueCode(_db.Tests.AnyAsync(x => x.Code == e.Code), e.Code);
                if (!await _db.BiomaterialTypes.AnyAsync(b => b.Id == e.BiomaterialTypeId)) throw ValidationException.Field("biomaterialTypeId", "Біоматеріал не знайдено");
                if (body.TryGetProperty("dropdownOptions", out var opts) && opts.ValueKind == JsonValueKind.Array) e.DropdownOptions = opts.EnumerateArray().Select(o => o.GetString() ?? "").ToList();
                e.BiomaterialType = null; e.TubeType = null; e.Method = null;
                await ValidateFormulaAsync(e);
                await UnitCatalog.SyncTestAsync(_db, e, null, "");
                _db.Tests.Add(e); entity = e; break;
            }
            case "profiles":
            {
                var e = Deserialize<LabTestProfile>(body);
                if (string.IsNullOrWhiteSpace(e.Code) || string.IsNullOrWhiteSpace(e.Name)) throw new ValidationException("Код та назва профілю обов'язкові");
                await EnsureUniqueCode(_db.Profiles.AnyAsync(x => x.Code == e.Code), e.Code);
                e.Items = await BuildProfileItems(e.Id, body);
                _db.Profiles.Add(e); entity = e; break;
            }
            case "reflex-rules":
            {
                var e = Deserialize<LabReflexRule>(body);
                await ValidateReflexAsync(e);
                _db.ReflexRules.Add(e); entity = e; break;
            }
            case "organisms": { var e = Deserialize<LabMicroOrganism>(body); if (e.Id == 0) e.Id = await NextIntId(_db.Organisms.Select(x => x.Id)); await EnsureUniqueCode(_db.Organisms.AnyAsync(x => x.Code == e.Code), e.Code); _db.Organisms.Add(e); entity = e; break; }
            case "antibiotics": { var e = Deserialize<LabAntibiotic>(body); if (e.Id == 0) e.Id = await NextIntId(_db.Antibiotics.Select(x => x.Id)); await EnsureUniqueCode(_db.Antibiotics.AnyAsync(x => x.Code == e.Code), e.Code); _db.Antibiotics.Add(e); entity = e; break; }
            case "eucast-breakpoints":
            {
                var e = Deserialize<LabEucastBreakpoint>(body);
                if (!await _db.Organisms.AnyAsync(o => o.Id == e.OrganismId) || !await _db.Antibiotics.AnyAsync(a => a.Id == e.AntibioticId)) throw new ValidationException("Збудник/антибіотик не знайдено");
                if (await _db.EucastBreakpoints.AnyAsync(b => b.OrganismId == e.OrganismId && b.AntibioticId == e.AntibioticId && b.EucastVersion == e.EucastVersion && b.RecordState != RecordStates.Deleted)) throw new ConflictException("Контрольне значення для цієї пари збудник–антибіотик у цій версії EUCAST вже існує");
                e.Organism = null; e.Antibiotic = null;
                _db.EucastBreakpoints.Add(e); entity = e; break;
            }
            case "units": { var e = Deserialize<LabUnit>(body); if (e.Id == 0) e.Id = await NextIntId(_db.Units.Select(x => x.Id)); e.Code = (e.Code ?? "").Trim().ToUpperInvariant(); await EnsureUniqueCode(_db.Units.AnyAsync(x => x.Code == e.Code), e.Code); if (await _db.Units.AnyAsync(x => x.Name == e.Name && x.RecordState != RecordStates.Deleted)) throw new ConflictException($"Одиниця «{e.Name}» вже існує"); _db.Units.Add(e); entity = e; break; }
            case "unit-conversions":
            {
                var e = Deserialize<LabUnitConversion>(body);
                if (!await _db.Tests.AnyAsync(t => t.Code == e.TestCode)) throw ValidationException.Field("testCode", "Показник не знайдено");
                if (!await _db.Units.AnyAsync(u => u.Id == e.FromUnitId) || !await _db.Units.AnyAsync(u => u.Id == e.ToUnitId)) throw new ValidationException("Одиницю виміру (з/до) не знайдено");
                if (e.FromUnitId == e.ToUnitId) throw new ValidationException("Одиниці «з» і «до» мають відрізнятися");
                if (e.Factor == 0) throw ValidationException.Field("factor", "Коефіцієнт не може дорівнювати нулю");
                if (await _db.UnitConversions.AnyAsync(c => c.TestCode == e.TestCode && c.FromUnitId == e.FromUnitId && c.ToUnitId == e.ToUnitId)) throw new ConflictException("Правило перерахунку для цієї пари одиниць вже існує");
                e.FromUnit = null; e.ToUnit = null;
                _db.UnitConversions.Add(e); entity = e; break;
            }
            case "age-groups": { var e = Deserialize<LabAgeGroup>(body); if (e.Id == 0) e.Id = await NextIntId(_db.AgeGroups.Select(x => x.Id)); e.Code = (e.Code ?? "").Trim().ToUpperInvariant(); await EnsureUniqueCode(_db.AgeGroups.AnyAsync(x => x.Code == e.Code), e.Code); ValidateAgeGroup(e); _db.AgeGroups.Add(e); entity = e; break; }
            case "rejection-reasons": { var e = Deserialize<LabRejectionReason>(body); if (e.Id == 0) e.Id = await NextIntId(_db.RejectionReasons.Select(x => x.Id)); e.Code = (e.Code ?? "").Trim().ToUpperInvariant(); await EnsureUniqueCode(_db.RejectionReasons.AnyAsync(x => x.Code == e.Code), e.Code); if (string.IsNullOrWhiteSpace(e.Name)) throw ValidationException.Field("name", "Назва обов'язкова"); ValidateRejectionReason(e); _db.RejectionReasons.Add(e); entity = e; break; }
            case "cancel-reasons": { var e = Deserialize<LabCancelReason>(body); if (e.Id == 0) e.Id = await NextIntId(_db.CancelReasons.Select(x => x.Id)); e.Code = (e.Code ?? "").Trim().ToUpperInvariant(); await EnsureUniqueCode(_db.CancelReasons.AnyAsync(x => x.Code == e.Code), e.Code); if (string.IsNullOrWhiteSpace(e.Name)) throw ValidationException.Field("name", "Назва обов'язкова"); _db.CancelReasons.Add(e); entity = e; break; }
            case "departments": return await _org.SaveDepartmentAsync(null, body);
            case "employees": return await _org.SaveEmployeeAsync(null, body);
            default: throw new NotFoundException($"Довідник '{name}' не існує");
        }
        RequireName(entity);
        var id = IdOf(entity);
        _audit.Log("CREATE", table, id, null, entity);
        await _db.SaveChangesAsync();
        return await GetAsync(name, id);
    }

    private static readonly string[] RejectionCategories = { "COLLECTION", "SAMPLE_QUALITY", "IDENTIFICATION", "TRANSPORT", "ANALYTIC", "OTHER" };

    private static void ValidateAgeGroup(LabAgeGroup e)
    {
        if (string.IsNullOrWhiteSpace(e.Name)) throw ValidationException.Field("name", "Назва обов'язкова");
        if (e.AgeFrom < 0 || e.AgeTo < 0) throw ValidationException.Field("ageFrom", "Вік не може бути від'ємним");
        if (e.AgeFrom > e.AgeTo) throw new ValidationException("Вік «від» не може перевищувати «до»");
        if (!new[] { "DAYS", "MONTHS", "YEARS" }.Contains(e.AgeUnit)) throw ValidationException.Field("ageUnit", "Одиниця віку: дні, місяці або роки");
    }

    private static void ValidateRejectionReason(LabRejectionReason e)
    {
        e.Category = (e.Category ?? "").Trim().ToUpperInvariant();
        if (!RejectionCategories.Contains(e.Category)) throw ValidationException.Field("category", "Категорія причини: забір, якість матеріалу, ідентифікація, транспортування, аналітика або інше");
    }

    /// <summary>Обов'язкова назва для будь-якого запису довідника, що її має (імпорт не повинен обходити форму).</summary>
    private static void RequireName(object entity)
    {
        var p = entity.GetType().GetProperty("Name");
        if (p?.PropertyType == typeof(string) && string.IsNullOrWhiteSpace((string?)p.GetValue(entity)))
            throw ValidationException.Field("name", "Назва обов'язкова");
    }

    public async Task<object> UpdateAsync(string name, string id, JsonElement body)
    {
        _policy.Require($"Редагування запису довідника {name}", LabRoles.Admin, LabRoles.Doctor);
        if (name == "departments") return await _org.SaveDepartmentAsync(id, body);
        if (name == "employees") return await _org.SaveEmployeeAsync(id, body);
        var table = Table(name);
        var existing = await GetTrackedAsync(name, id);
        var before = JsonSerializer.Serialize(existing, Json);
        var prevUnitId = (existing as LabTestDefinition)?.UnitId; var prevUnit = (existing as LabTestDefinition)?.Unit;
        // Патч: лише присутні властивості (без Id)
        var node = JsonNode.Parse(body.GetRawText())!.AsObject();
        node.Remove("id"); node.Remove("createdOn"); node.Remove("createdBy"); node.Remove("items"); node.Remove("dropdownOptions"); node.Remove("biomaterialType"); node.Remove("tubeType"); node.Remove("method"); node.Remove("organism"); node.Remove("antibiotic"); node.Remove("department"); node.Remove("fromUnit"); node.Remove("toUnit");
        var current = JsonNode.Parse(JsonSerializer.Serialize(existing, Json))!.AsObject();
        foreach (var kv in node) current[kv.Key] = kv.Value?.DeepClone();
        var merged = JsonSerializer.Deserialize(current.ToJsonString(), existing.GetType(), Json)!;
        foreach (var prop in existing.GetType().GetProperties().Where(p => p.CanWrite && p.Name is not ("Id" or "Items" or "CreatedOn" or "CreatedBy" or "BiomaterialType" or "TubeType" or "Method" or "Organism" or "Antibiotic" or "Department" or "DropdownOptions" or "FromUnit" or "ToUnit")))
            if (node.ContainsKey(char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1))) prop.SetValue(existing, prop.GetValue(merged));

        if (existing is LabAgeGroup agu) ValidateAgeGroup(agu);
        if (existing is LabRejectionReason rju) ValidateRejectionReason(rju);
        RequireName(existing);
        if (existing is LabReflexRule rr) await ValidateReflexAsync(rr);
        if (existing is LabEucastBreakpoint eb && await _db.EucastBreakpoints.AnyAsync(b => b.Id != eb.Id && b.OrganismId == eb.OrganismId && b.AntibioticId == eb.AntibioticId && b.EucastVersion == eb.EucastVersion && b.RecordState != RecordStates.Deleted))
            throw new ConflictException("Контрольне значення для цієї пари збудник–антибіотик у цій версії EUCAST вже існує");
        if (Adapter(name) is { } uad) await uad.ValidateAsync(_db, existing, false);
        if (existing is LabTestDefinition td && body.TryGetProperty("dropdownOptions", out var opts) && opts.ValueKind == JsonValueKind.Array) td.DropdownOptions = opts.EnumerateArray().Select(o => o.GetString() ?? "").ToList();
        if (existing is LabTestDefinition tdu) await UnitCatalog.SyncTestAsync(_db, tdu, prevUnitId, prevUnit);
        if (existing is LabTestProfile profile && body.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            _db.ProfileItems.RemoveRange(profile.Items);
            profile.Items = await BuildProfileItems(profile.Id, body);
        }
        if (existing is LabTestDefinition tdv)
        {
            await ValidateFormulaAsync(tdv);
            var oldFormula = JsonNode.Parse(before)?["formula"]?.GetValue<string>();
            if (!string.Equals(oldFormula ?? "", tdv.Formula ?? "", StringComparison.Ordinal))
                _audit.Log("FORMULA_CHANGED", table, id, new { formula = oldFormula }, new { formula = tdv.Formula, tdv.ResultType }, $"Змінено формулу показника {tdv.Code}");
        }
        _audit.Log("UPDATE", table, id, before, existing);
        await _db.SaveChangesAsync();
        if (Adapter(name) is { } cad) await cad.OnChangedAsync(_db);
        return await GetAsync(name, IdOf(existing));
    }

    public async Task<object> DeleteAsync(string name, string id)
    {
        _policy.Require($"Видалення запису довідника {name}", LabRoles.Admin);
        if (name is "departments" or "employees") return await _org.DeleteAsync(name, id);
        var table = Table(name);
        var existing = await GetTrackedAsync(name, id);
        var realId = IdOf(existing);
        var adapter = Adapter(name);
        var deps = adapter != null ? await adapter.DependenciesAsync(_db, existing) : await DependencyCountAsync(name, existing);
        if (deps > 0 || adapter is { ArchiveOnly: true })
        {
            // Soft delete: isActive=false (та is_deleted для прихованих довідників)
            var isActiveProp = existing.GetType().GetProperty("IsActive");
            if (isActiveProp == null) throw new ConflictException($"Запис використовується ({deps} залежностей) і не може бути видалений");
            isActiveProp.SetValue(existing, false);
            _audit.Log("SOFT_DELETE", table, realId, null, new { isActive = false, dependencies = deps });
            await _db.SaveChangesAsync();
            if (adapter != null) await adapter.OnChangedAsync(_db);
            return new { id = realId, deleted = false, deactivated = true, dependencies = deps, message = deps > 0 ? $"Запис використовується ({deps}) — переведено в архів" : "Запис переведено в архів" };
        }
        _db.Remove(existing);
        _audit.Log("DELETE", table, realId, existing, null);
        await _db.SaveChangesAsync();
        if (adapter != null) await adapter.OnChangedAsync(_db);
        return new { id = realId, deleted = true };
    }

    /// <summary>Перевірка формули розрахункового показника: синтаксис, відомі коди, відсутність самопосилання й циклів.</summary>
    public async Task<object> CheckFormulaAsync(string? code, string? formula)
    {
        var parsed = FormulaEngine.Parse(formula);
        if (!parsed.IsValid) return new { valid = false, error = parsed.Error, inputs = Array.Empty<string>(), unknownCodes = Array.Empty<string>(), cycle = (string[]?)null };
        var codes = (await _db.Tests.AsNoTracking().Select(t => t.Code).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = parsed.Inputs.Where(i => !codes.Contains(i) && !string.Equals(i, code, StringComparison.OrdinalIgnoreCase)).ToList();
        var self = code != null && parsed.Inputs.Contains(code, StringComparer.OrdinalIgnoreCase);
        var cycle = self ? new List<string> { code!.ToUpperInvariant(), code.ToUpperInvariant() } : await FindFormulaCycleAsync(code, parsed);
        string? error = unknown.Count > 0 ? $"Невідомі показники у формулі: {string.Join(", ", unknown)}"
            : cycle != null ? $"Циклічне посилання: {string.Join(" → ", cycle)}" : null;
        return new { valid = error == null, error, inputs = parsed.Inputs, context = parsed.ContextVariables, unknownCodes = unknown, cycle };
    }

    private async Task<List<string>?> FindFormulaCycleAsync(string? code, ParsedFormula parsed)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var others = await _db.Tests.AsNoTracking().Where(t => t.Formula != null && t.Formula != "" && t.Code != code).Select(t => new { t.Code, t.Formula }).ToListAsync();
        var map = others.ToDictionary(t => t.Code.ToUpperInvariant(), t => FormulaEngine.Parse(t.Formula).Inputs.ToList());
        return FormulaEngine.FindCycle(code, parsed.Inputs, c => map.TryGetValue(c, out var l) ? l : Array.Empty<string>());
    }

    private async Task ValidateFormulaAsync(LabTestDefinition e)
    {
        if (e.ResultType != "CALCULATED") return;
        if (string.IsNullOrWhiteSpace(e.Formula)) throw ValidationException.Field("formula", "Для розрахункового показника вкажіть формулу");
        var check = await CheckFormulaAsync(e.Code, e.Formula);
        var json = JsonSerializer.SerializeToNode(check)!.AsObject();
        if (json["valid"]?.GetValue<bool>() != true) throw ValidationException.Field("formula", json["error"]?.GetValue<string>() ?? "Формула некоректна");
        if (FormulaEngine.Parse(e.Formula).Inputs.Count == 0) throw ValidationException.Field("formula", "У формулі має бути хоча б один код показника");
    }

    private async Task<object> GetTrackedAsync(string name, string id)
    {
        if (Adapter(name) is { } ad) return await ad.FindAsync(_db, id, true) ?? throw NotFoundException.For($"Запис довідника {name}", id);
        object? item = name switch
        {
            "biomaterials" => await _db.BiomaterialTypes.FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "tube-types" => await _db.TubeTypes.FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "method-types" => await _db.MethodTypes.FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "analyzer-types" => await _db.AnalyzerTypes.FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "tests" => await _db.Tests.FirstOrDefaultAsync(x => x.Id == id || x.Code == id),
            "profiles" => await _db.Profiles.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id || x.Code == id),
            "reflex-rules" => await _db.ReflexRules.FirstOrDefaultAsync(x => x.Id == id),
            "organisms" => await _db.Organisms.FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "antibiotics" => await _db.Antibiotics.FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "eucast-breakpoints" => await _db.EucastBreakpoints.FirstOrDefaultAsync(x => x.Id == id),
            "units" => await _db.Units.FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "unit-conversions" => await _db.UnitConversions.FirstOrDefaultAsync(x => x.Id == id),
            "age-groups" => await _db.AgeGroups.FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "rejection-reasons" => await _db.RejectionReasons.FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            "cancel-reasons" => await _db.CancelReasons.FirstOrDefaultAsync(x => x.Id.ToString() == id || x.Code == id),
            _ => null
        };
        return item ?? throw NotFoundException.For($"Запис довідника {name}", id);
    }

    private async Task<int> DependencyCountAsync(string name, object e) => name switch
    {
        "biomaterials" => await _db.Tests.CountAsync(t => t.BiomaterialTypeId == ((LabBiomaterialType)e).Id) + await _db.Samples.CountAsync(s => s.BiomaterialTypeId == ((LabBiomaterialType)e).Id),
        "tube-types" => await _db.Tests.CountAsync(t => t.TubeTypeId == ((LabTubeType)e).Id) + await _db.Samples.CountAsync(s => s.TubeTypeId == ((LabTubeType)e).Id),
        "method-types" => await _db.Tests.CountAsync(t => t.MethodId == ((LabMethodType)e).Id),
        "analyzer-types" => await _db.Analyzers.CountAsync(a => a.AnalyzerTypeId == ((LabAnalyzerType)e).Id),
        "tests" => await _db.OrderTests.CountAsync(t => t.TestId == ((LabTestDefinition)e).Id) + await _db.ProfileItems.CountAsync(i => i.TestId == ((LabTestDefinition)e).Id) + await _db.ReferenceLayers.CountAsync(l => l.TestCode == ((LabTestDefinition)e).Code),
        "profiles" => await _db.OrderTests.CountAsync(t => t.ProfileId == ((LabTestProfile)e).Id),
        "organisms" => await _db.Isolates.CountAsync(i => i.OrganismId == ((LabMicroOrganism)e).Id) + await _db.EucastBreakpoints.CountAsync(b => b.OrganismId == ((LabMicroOrganism)e).Id),
        "antibiotics" => await _db.Susceptibilities.CountAsync(s => s.AntibioticId == ((LabAntibiotic)e).Id) + await _db.EucastBreakpoints.CountAsync(b => b.AntibioticId == ((LabAntibiotic)e).Id),
        "eucast-breakpoints" => await _db.Susceptibilities.CountAsync(s => s.BreakpointId == ((LabEucastBreakpoint)e).Id),
        "units" => await _db.Tests.CountAsync(t => t.UnitId == ((LabUnit)e).Id) + await _db.ReferenceLayers.CountAsync(l => l.UnitId == ((LabUnit)e).Id) + await _db.UnitConversions.CountAsync(c => c.FromUnitId == ((LabUnit)e).Id || c.ToUnitId == ((LabUnit)e).Id),
        "age-groups" => await _db.ReferenceLayers.CountAsync(l => l.AgeGroupId == ((LabAgeGroup)e).Id),
        "rejection-reasons" => await _db.Samples.CountAsync(s => s.RejectReasonCode == ((LabRejectionReason)e).Code),
        "cancel-reasons" => await _db.Orders.CountAsync(o => o.CancelReasonCode == ((LabCancelReason)e).Code),
        _ => 0
    };

    private async Task<List<LabTestProfileItem>> BuildProfileItems(string profileId, JsonElement body)
    {
        var list = new List<LabTestProfileItem>();
        if (!body.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return list;
        var order = 0;
        foreach (var it in items.EnumerateArray())
        {
            var testRef = it.TryGetProperty("testId", out var tid) ? tid.GetString() : it.TryGetProperty("testCode", out var tc) ? tc.GetString() : null;
            if (string.IsNullOrWhiteSpace(testRef)) continue;
            var test = await _db.Tests.FirstOrDefaultAsync(t => t.Id == testRef || t.Code == testRef) ?? throw new ValidationException($"Тест '{testRef}' не знайдено");
            list.Add(new LabTestProfileItem { ProfileId = profileId, TestId = test.Id, DisplayOrder = it.TryGetProperty("displayOrder", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : ++order, IsRequired = !it.TryGetProperty("isRequired", out var r) || r.ValueKind != JsonValueKind.False });
        }
        return list;
    }

    private static T Deserialize<T>(JsonElement body) where T : new() => JsonSerializer.Deserialize<T>(body.GetRawText(), Json) ?? new T();
    private static async Task<int> NextIntId(IQueryable<int> ids) => (await ids.AnyAsync() ? await ids.MaxAsync() : 0) + 1;
    private static async Task EnsureUniqueCode(Task<bool> exists, string code) { if (string.IsNullOrWhiteSpace(code)) throw ValidationException.Field("code", "Код обов'язковий"); if (await exists) throw new ConflictException($"Запис із кодом '{code}' вже існує"); }
    private static string IdOf(object e) => e.GetType().GetProperty("Id")!.GetValue(e)!.ToString()!;

    public async Task<object> TestProfilesAsync(string code)
    {
        var test = await _db.Tests.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code || t.Id == code) ?? throw NotFoundException.For("Тест", code);
        var profiles = await _db.ProfileItems.AsNoTracking().Include(i => i.Profile).Where(i => i.TestId == test.Id).Select(i => new { i.Profile!.Id, i.Profile.Code, i.Profile.Name, i.Profile.Category, i.DisplayOrder, i.IsRequired }).ToListAsync();
        return new { testCode = test.Code, testName = test.Name, profiles };
    }

    /// <summary>
    /// Правило повторного тесту: тести існують, тригер ≠ додаваний тест, немає дубля (тригер + умова + поріг + тест) і циклу
    /// (A → B → … → A): цикл зациклив би автододавання тестів.
    /// </summary>
    private async Task ValidateReflexAsync(LabReflexRule e)
    {
        e.TriggerTestCode = (e.TriggerTestCode ?? "").Trim().ToUpperInvariant(); e.ReflexTestCode = (e.ReflexTestCode ?? "").Trim().ToUpperInvariant();
        if (!await _db.Tests.AnyAsync(t => t.Code == e.TriggerTestCode) || !await _db.Tests.AnyAsync(t => t.Code == e.ReflexTestCode)) throw new ValidationException("Коди тестів тригера/reflex мають існувати у довіднику тестів");
        var ops = new[] { "<", "<=", ">", ">=", "==", "OUT_OF_RANGE", "CRITICAL" };
        if (!ops.Contains(e.ConditionOperator)) throw ValidationException.Field("conditionOperator", "Умова: " + string.Join(", ", ops));
        if (e.ConditionOperator is "<" or "<=" or ">" or ">=" or "==" && e.ThresholdValue == null) throw ValidationException.Field("thresholdValue", "Для цієї умови вкажіть поріг");
        if (e.TriggerTestCode == e.ReflexTestCode) throw new ValidationException("Тест не може додавати сам себе: оберіть інший тест для повторного визначення");
        var others = await _db.ReflexRules.AsNoTracking().Where(r => r.Id != e.Id && r.RecordState != RecordStates.Deleted).ToListAsync();
        if (others.Any(r => r.TriggerTestCode == e.TriggerTestCode && r.ConditionOperator == e.ConditionOperator && r.ThresholdValue == e.ThresholdValue && r.ReflexTestCode == e.ReflexTestCode))
            throw new ConflictException($"Таке правило вже існує: {e.TriggerTestCode} {e.ConditionOperator} {e.ThresholdValue} → {e.ReflexTestCode}");
        if (!e.IsActive) return;
        // цикл: чи досяжний тригер із додаваного тесту через активні правила
        var graph = others.Where(r => r.IsActive).GroupBy(r => r.TriggerTestCode).ToDictionary(g => g.Key, g => g.Select(r => r.ReflexTestCode).Distinct().ToList());
        var parent = new Dictionary<string, string?> { [e.ReflexTestCode] = null };
        var queue = new Queue<string>(); queue.Enqueue(e.ReflexTestCode);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!graph.TryGetValue(cur, out var next)) continue;
            foreach (var n in next)
            {
                if (parent.ContainsKey(n)) continue;
                parent[n] = cur;
                if (n == e.TriggerTestCode)
                {
                    var chain = new List<string>();
                    for (string? p = n; p != null; p = parent[p]) chain.Add(p);
                    chain.Reverse();
                    throw new ConflictException($"Правило створює цикл повторних тестів: {e.TriggerTestCode} → " + string.Join(" → ", chain));
                }
                queue.Enqueue(n);
            }
        }
    }

}
