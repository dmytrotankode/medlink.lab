// Універсальний CRUD довідників: /dictionaries/{name} (біоматеріали, пробірки, методики, типи аналізаторів, тести,
// профілі, reflex-правила, збудники, антибіотики, EUCAST, підрозділи, співробітники). Soft delete → isActive=false; 409 при залежностях.
using System.Text.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class DictionaryService
{
    public static readonly string[] Names = { "biomaterials", "tube-types", "method-types", "analyzer-types", "tests", "profiles", "reflex-rules", "organisms", "antibiotics", "eucast-breakpoints", "departments", "employees" };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true, ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };

    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;

    public DictionaryService(LisDbContext db, IRolePolicy policy, IAuditService audit) { _db = db; _policy = policy; _audit = audit; }

    private static string Table(string name) => name switch
    {
        "biomaterials" => "lab_biomaterial_type", "tube-types" => "lab_tube_type", "method-types" => "lab_method_type", "analyzer-types" => "lab_analyzer_type",
        "tests" => "lab_test_definition", "profiles" => "lab_test_profile", "reflex-rules" => "lab_reflex_rule", "organisms" => "lab_micro_organism",
        "antibiotics" => "lab_antibiotic", "eucast-breakpoints" => "lab_eucast_breakpoint", "departments" => "org_department", "employees" => "org_employee",
        _ => throw new NotFoundException($"Довідник '{name}' не існує. Доступні: {string.Join(", ", Names)}")
    };

    // ------------------------------------------------------------------ list / get
    public async Task<object> ListAsync(string name, string? search, bool? isActive, PagingQuery paging)
    {
        Table(name);
        var s = search?.Trim().ToLowerInvariant();
        switch (name)
        {
            case "biomaterials": return Page(await _db.BiomaterialTypes.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name}", x => x.IsActive, s, isActive, paging);
            case "tube-types": return Page(await _db.TubeTypes.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.OrderOfDrawIndex).ThenBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name}", x => x.IsActive, s, isActive, paging);
            case "method-types": return Page(await _db.MethodTypes.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name}", x => x.IsActive, s, isActive, paging);
            case "analyzer-types": return Page(await _db.AnalyzerTypes.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name} {x.Manufacturer} {x.ExchType}", x => x.IsActive, s, isActive, paging);
            case "tests": return Page(await _db.Tests.AsNoTracking().Include(x => x.BiomaterialType).Include(x => x.TubeType).Include(x => x.Method).Where(x => !x.IsDeleted).OrderBy(x => x.Category).ThenBy(x => x.Code).ToListAsync(), x => $"{x.Code} {x.Name} {x.ShortName} {x.LoincCode} {x.Category}", x => x.IsActive, s, isActive, paging);
            case "profiles": return Page(await _db.Profiles.AsNoTracking().Include(x => x.Items).ThenInclude(i => i.Test).Where(x => !x.IsDeleted).OrderBy(x => x.Code).ToListAsync(), x => $"{x.Code} {x.Name} {x.Category}", x => x.IsActive, s, isActive, paging);
            case "reflex-rules": return Page(await _db.ReflexRules.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.TriggerTestCode).ToListAsync(), x => $"{x.TriggerTestCode} {x.ReflexTestCode} {x.Description}", x => x.IsActive, s, isActive, paging);
            case "organisms": return Page(await _db.Organisms.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.LatinName} {x.CommonName}", x => x.IsActive, s, isActive, paging);
            case "antibiotics": return Page(await _db.Antibiotics.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id).ToListAsync(), x => $"{x.Code} {x.Name} {x.GroupName}", x => x.IsActive, s, isActive, paging);
            case "eucast-breakpoints": return Page(await _db.EucastBreakpoints.AsNoTracking().Include(x => x.Organism).Include(x => x.Antibiotic).Where(x => !x.IsDeleted).OrderBy(x => x.OrganismId).ThenBy(x => x.AntibioticId).ToListAsync(), x => $"{x.Organism?.Code} {x.Organism?.LatinName} {x.Antibiotic?.Code} {x.Antibiotic?.Name}", x => true, s, isActive, paging);
            case "departments": return Page(await _db.Departments.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(), x => $"{x.Code} {x.Name}", x => x.IsActive, s, isActive, paging);
            case "employees": return Page(await _db.Employees.AsNoTracking().Include(x => x.Department).Where(x => !x.IsDeleted).OrderBy(x => x.FullName).ToListAsync(), x => $"{x.FullName} {x.PositionName} {x.LabRole}", x => x.IsActive, s, isActive, paging);
        }
        throw new NotFoundException($"Довідник '{name}' не існує");
    }

    private static PagedResult<T> Page<T>(List<T> items, Func<T, string> text, Func<T, bool> active, string? search, bool? isActive, PagingQuery paging)
    {
        IEnumerable<T> q = items;
        if (!string.IsNullOrEmpty(search)) q = q.Where(x => text(x).ToLowerInvariant().Contains(search));
        if (isActive.HasValue) q = q.Where(x => active(x) == isActive.Value);
        return q.ToPaged(paging);
    }

    public async Task<object> GetAsync(string name, string id)
    {
        Table(name);
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
            "departments" => await _db.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id || x.Code == id),
            "employees" => await _db.Employees.AsNoTracking().Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == id),
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
                if (!await _db.Tests.AnyAsync(t => t.Code == e.TriggerTestCode) || !await _db.Tests.AnyAsync(t => t.Code == e.ReflexTestCode)) throw new ValidationException("Коди тестів тригера/reflex мають існувати у довіднику тестів");
                _db.ReflexRules.Add(e); entity = e; break;
            }
            case "organisms": { var e = Deserialize<LabMicroOrganism>(body); if (e.Id == 0) e.Id = await NextIntId(_db.Organisms.Select(x => x.Id)); await EnsureUniqueCode(_db.Organisms.AnyAsync(x => x.Code == e.Code), e.Code); _db.Organisms.Add(e); entity = e; break; }
            case "antibiotics": { var e = Deserialize<LabAntibiotic>(body); if (e.Id == 0) e.Id = await NextIntId(_db.Antibiotics.Select(x => x.Id)); await EnsureUniqueCode(_db.Antibiotics.AnyAsync(x => x.Code == e.Code), e.Code); _db.Antibiotics.Add(e); entity = e; break; }
            case "eucast-breakpoints":
            {
                var e = Deserialize<LabEucastBreakpoint>(body);
                if (!await _db.Organisms.AnyAsync(o => o.Id == e.OrganismId) || !await _db.Antibiotics.AnyAsync(a => a.Id == e.AntibioticId)) throw new ValidationException("Збудник/антибіотик не знайдено");
                if (await _db.EucastBreakpoints.AnyAsync(b => b.OrganismId == e.OrganismId && b.AntibioticId == e.AntibioticId && b.EucastVersion == e.EucastVersion)) throw new ConflictException("Breakpoint для цієї пари вже існує");
                e.Organism = null; e.Antibiotic = null;
                _db.EucastBreakpoints.Add(e); entity = e; break;
            }
            case "departments": { var e = Deserialize<OrgDepartment>(body); if (string.IsNullOrWhiteSpace(e.Name)) throw new ValidationException("Назва підрозділу обов'язкова"); _db.Departments.Add(e); entity = e; break; }
            case "employees":
            {
                var e = Deserialize<OrgEmployee>(body);
                if (string.IsNullOrWhiteSpace(e.FullName)) throw new ValidationException("ПІБ співробітника обов'язкове");
                if (!LabRoles.All.Contains(e.LabRole)) throw ValidationException.Field("labRole", $"Роль має бути однією з: {string.Join(", ", LabRoles.All)}");
                if (e.DepartmentId != null && !await _db.Departments.AnyAsync(d => d.Id == e.DepartmentId)) throw ValidationException.Field("departmentId", "Підрозділ не знайдено");
                e.Department = null;
                _db.Employees.Add(e); entity = e; break;
            }
            default: throw new NotFoundException($"Довідник '{name}' не існує");
        }
        var id = IdOf(entity);
        _audit.Log("CREATE", table, id, null, entity);
        await _db.SaveChangesAsync();
        return await GetAsync(name, id);
    }

    public async Task<object> UpdateAsync(string name, string id, JsonElement body)
    {
        _policy.Require($"Редагування запису довідника {name}", LabRoles.Admin, LabRoles.Doctor);
        var table = Table(name);
        var existing = await GetTrackedAsync(name, id);
        var before = JsonSerializer.Serialize(existing, Json);
        // Патч: лише присутні властивості (без Id)
        var node = JsonNode.Parse(body.GetRawText())!.AsObject();
        node.Remove("id"); node.Remove("createdOn"); node.Remove("createdBy"); node.Remove("items"); node.Remove("dropdownOptions"); node.Remove("biomaterialType"); node.Remove("tubeType"); node.Remove("method"); node.Remove("organism"); node.Remove("antibiotic"); node.Remove("department");
        var current = JsonNode.Parse(JsonSerializer.Serialize(existing, Json))!.AsObject();
        foreach (var kv in node) current[kv.Key] = kv.Value?.DeepClone();
        var merged = JsonSerializer.Deserialize(current.ToJsonString(), existing.GetType(), Json)!;
        foreach (var prop in existing.GetType().GetProperties().Where(p => p.CanWrite && p.Name is not ("Id" or "Items" or "CreatedOn" or "CreatedBy" or "BiomaterialType" or "TubeType" or "Method" or "Organism" or "Antibiotic" or "Department" or "DropdownOptions")))
            if (node.ContainsKey(char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1))) prop.SetValue(existing, prop.GetValue(merged));

        if (existing is LabTestDefinition td && body.TryGetProperty("dropdownOptions", out var opts) && opts.ValueKind == JsonValueKind.Array) td.DropdownOptions = opts.EnumerateArray().Select(o => o.GetString() ?? "").ToList();
        if (existing is LabTestProfile profile && body.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            _db.ProfileItems.RemoveRange(profile.Items);
            profile.Items = await BuildProfileItems(profile.Id, body);
        }
        if (existing is OrgEmployee emp && !LabRoles.All.Contains(emp.LabRole)) throw ValidationException.Field("labRole", $"Роль має бути однією з: {string.Join(", ", LabRoles.All)}");
        _audit.Log("UPDATE", table, id, before, existing);
        await _db.SaveChangesAsync();
        return await GetAsync(name, IdOf(existing));
    }

    public async Task<object> DeleteAsync(string name, string id)
    {
        _policy.Require($"Видалення запису довідника {name}", LabRoles.Admin);
        var table = Table(name);
        var existing = await GetTrackedAsync(name, id);
        var realId = IdOf(existing);
        var deps = await DependencyCountAsync(name, existing);
        if (deps > 0)
        {
            // Soft delete: isActive=false (та is_deleted для прихованих довідників)
            var isActiveProp = existing.GetType().GetProperty("IsActive");
            if (isActiveProp == null) throw new ConflictException($"Запис використовується ({deps} залежностей) і не може бути видалений");
            isActiveProp.SetValue(existing, false);
            _audit.Log("SOFT_DELETE", table, realId, null, new { isActive = false, dependencies = deps });
            await _db.SaveChangesAsync();
            return new { id = realId, deleted = false, deactivated = true, dependencies = deps, message = $"Запис використовується ({deps}) — деактивовано (isActive=false)" };
        }
        _db.Remove(existing);
        _audit.Log("DELETE", table, realId, existing, null);
        await _db.SaveChangesAsync();
        return new { id = realId, deleted = true };
    }

    private async Task<object> GetTrackedAsync(string name, string id)
    {
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
            "departments" => await _db.Departments.FirstOrDefaultAsync(x => x.Id == id || x.Code == id),
            "employees" => await _db.Employees.FirstOrDefaultAsync(x => x.Id == id),
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
        "departments" => await _db.Employees.CountAsync(x => x.DepartmentId == ((OrgDepartment)e).Id) + await _db.Orders.CountAsync(o => o.DepartmentId == ((OrgDepartment)e).Id) + await _db.Analyzers.CountAsync(a => a.DepartmentId == ((OrgDepartment)e).Id),
        "employees" => await _db.Orders.CountAsync(o => o.DoctorId == ((OrgEmployee)e).Id || o.CreatedById == ((OrgEmployee)e).Id) + await _db.Results.CountAsync(r => r.VerifiedById == ((OrgEmployee)e).Id || r.EnteredById == ((OrgEmployee)e).Id),
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

    /// <summary>Імпорт довідника (JSON-масив або CSV) з попереднім переглядом.</summary>
    public async Task<object> ImportAsync(string name, string content, bool dryRun)
    {
        _policy.Require($"Імпорт довідника {name}", LabRoles.Admin);
        Table(name);
        List<JsonElement> rows;
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("["))
            rows = JsonSerializer.Deserialize<List<JsonElement>>(content, Json) ?? new();
        else
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
            if (lines.Count < 2) throw new ValidationException("CSV має містити заголовок і хоча б один рядок");
            var sep = lines[0].Contains(';') ? ';' : ',';
            var header = lines[0].Split(sep).Select(h => h.Trim()).ToList();
            rows = lines.Skip(1).Select(l =>
            {
                var cells = l.Split(sep);
                var obj = new JsonObject();
                for (var i = 0; i < header.Count && i < cells.Length; i++)
                {
                    var v = cells[i].Trim();
                    obj[header[i]] = double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && !header[i].Equals("code", StringComparison.OrdinalIgnoreCase) ? JsonValue.Create(d) : bool.TryParse(v, out var b) ? JsonValue.Create(b) : JsonValue.Create(v);
                }
                return JsonSerializer.Deserialize<JsonElement>(obj.ToJsonString());
            }).ToList();
        }
        var preview = new List<object>();
        var created = 0; var updated = 0; var errors = 0;
        foreach (var row in rows)
        {
            string? code = row.TryGetProperty("code", out var c) ? c.GetString() : null;
            try
            {
                var exists = code != null && await ExistsByCodeAsync(name, code);
                preview.Add(new { code, action = exists ? "UPDATE" : "CREATE", row });
                if (!dryRun)
                {
                    if (exists) { await UpdateAsync(name, code!, row); updated++; }
                    else { await CreateAsync(name, row); created++; }
                }
            }
            catch (LisException ex) { errors++; preview.Add(new { code, action = "ERROR", error = ex.Message }); }
        }
        return new { dryRun, total = rows.Count, created, updated, errors, rows = preview };
    }

    private async Task<bool> ExistsByCodeAsync(string name, string code) => name switch
    {
        "biomaterials" => await _db.BiomaterialTypes.AnyAsync(x => x.Code == code),
        "tube-types" => await _db.TubeTypes.AnyAsync(x => x.Code == code),
        "method-types" => await _db.MethodTypes.AnyAsync(x => x.Code == code),
        "analyzer-types" => await _db.AnalyzerTypes.AnyAsync(x => x.Code == code),
        "tests" => await _db.Tests.AnyAsync(x => x.Code == code),
        "profiles" => await _db.Profiles.AnyAsync(x => x.Code == code),
        "organisms" => await _db.Organisms.AnyAsync(x => x.Code == code),
        "antibiotics" => await _db.Antibiotics.AnyAsync(x => x.Code == code),
        "departments" => await _db.Departments.AnyAsync(x => x.Code == code),
        _ => false
    };
}
