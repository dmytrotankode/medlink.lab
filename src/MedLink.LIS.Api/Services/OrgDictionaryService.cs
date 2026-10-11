// Довідники «Відділення» та «Співробітники» у структурі MedLink:
//  відділення = [MedLink] org_department + [ЛІС] lab_department_settings (вид підрозділу для ЛІС, телефон, активність);
//  співробітник = [MedLink] org_employee + cmn_person + [ЛІС] lab_employee_settings (роль, посада, КЕП, активність).
// API віддає «плоский» вигляд (як у v4.0), щоб фронтенд не залежав від розкладки таблиць evomis.
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class DepartmentView
{
    public string Id { get; set; } = "";
    public string? Code { get; set; }
    public string? Name { get; set; }
    /// <summary>LABORATORY | COLLECTION_POINT | CLINICAL | BRANCH (lab_department_settings.lab_kind).</summary>
    public string LabKind { get; set; } = "CLINICAL";
    /// <summary>Тип підрозділу MedLink (cmn_enum_record DepartmentType).</summary>
    public string? DepartmentTypeId { get; set; }
    public string? DepartmentTypeName { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class EmployeeView
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? Position { get; set; }
    public string? PositionTypeId { get; set; }
    public string LabRole { get; set; } = LabRoles.Technician;
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? DigitalSignatureCertId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class OrgDictionaryService
{
    public static readonly string[] LabKinds = { "LABORATORY", "COLLECTION_POINT", "CLINICAL", "BRANCH" };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private readonly LisDbContext _db;
    private readonly IAuditService _audit;
    public OrgDictionaryService(LisDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    /// <summary>Тип підрозділу MedLink за видом підрозділу ЛІС.</summary>
    public static string DepartmentTypeFor(string labKind) => labKind switch
    {
        "LABORATORY" or "COLLECTION_POINT" => MedLinkEnums.DeptTypeDiagnostic,
        "BRANCH" => MedLinkEnums.DeptTypeBranch,
        _ => MedLinkEnums.DeptTypeTreatment
    };

    /// <summary>Тип посади MedLink (OrgPositionType) за роллю ЛІС.</summary>
    public static string PositionTypeFor(string labRole) => labRole switch
    {
        LabRoles.Doctor or LabRoles.Admin => MedLinkEnums.PositionDoctors,
        LabRoles.Technician => MedLinkEnums.PositionLabTechs,
        LabRoles.Phlebotomist => MedLinkEnums.PositionNurses,
        _ => MedLinkEnums.PositionOther
    };

    // ------------------------------------------------------------------ departments
    public static DepartmentView ToView(OrgDepartment d) => new()
    {
        Id = d.Id, Code = d.Code, Name = d.Caption, LabKind = d.LabSettings?.LabKind ?? "CLINICAL", DepartmentTypeId = d.DepartmentTypeId,
        DepartmentTypeName = d.DepartmentType?.Caption, Address = d.Location, Phone = d.LabSettings?.Phone, IsActive = d.LabSettings?.IsActive ?? true
    };

    public async Task<List<DepartmentView>> DepartmentsAsync() =>
        (await _db.Departments.AsNoTracking().Include(d => d.DepartmentType).Where(d => d.RecordState != RecordStates.Deleted).OrderBy(d => d.Caption).ToListAsync()).Select(ToView).ToList();

    public async Task<DepartmentView?> DepartmentAsync(string id)
    {
        var d = await _db.Departments.AsNoTracking().Include(x => x.DepartmentType).FirstOrDefaultAsync(x => (x.Id == id || x.Code == id) && x.RecordState != RecordStates.Deleted);
        return d == null ? null : ToView(d);
    }

    public async Task<DepartmentView> SaveDepartmentAsync(string? id, JsonElement body)
    {
        OrgDepartment d;
        if (id == null)
        {
            d = new OrgDepartment { OrganizationId = MedLinkDefaults.OrganizationId, LabSettings = new LabDepartmentSettings() };
            d.LabSettings.DepartmentId = d.Id;
            _db.Departments.Add(d);
        }
        else
        {
            d = await _db.Departments.FirstOrDefaultAsync(x => (x.Id == id || x.Code == id) && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Відділення", id);
            if (d.LabSettings == null) { d.LabSettings = new LabDepartmentSettings { DepartmentId = d.Id }; _db.DepartmentSettings.Add(d.LabSettings); }
        }
        var before = ToView(d);
        var v = Merge(before, body);
        if (string.IsNullOrWhiteSpace(v.Name)) throw new ValidationException("Назва підрозділу обов'язкова");
        // сумісність із v4.0: departmentType = LABORATORY|COLLECTION_POINT|…
        if (body.TryGetProperty("departmentType", out var dt) && dt.ValueKind == JsonValueKind.String && LabKinds.Contains(dt.GetString())) v.LabKind = dt.GetString()!;
        if (!LabKinds.Contains(v.LabKind)) throw ValidationException.Field("labKind", $"Вид підрозділу: {string.Join(", ", LabKinds)}");
        if (!string.IsNullOrWhiteSpace(v.Code) && await _db.Departments.AnyAsync(x => x.Code == v.Code && x.Id != d.Id && x.RecordState != RecordStates.Deleted))
            throw new ConflictException($"Відділення з кодом {v.Code} вже існує");

        d.Caption = v.Name!.Trim(); d.FullName ??= d.Caption; d.Code = v.Code; d.Location = v.Address;
        d.DepartmentTypeId = string.IsNullOrWhiteSpace(v.DepartmentTypeId) || id == null ? DepartmentTypeFor(v.LabKind) : v.DepartmentTypeId!;
        d.LabSettings!.LabKind = v.LabKind; d.LabSettings.Phone = v.Phone; d.LabSettings.IsActive = v.IsActive;
        _audit.Log(id == null ? "CREATE" : "UPDATE", "org_department", d.Id, id == null ? null : before, v);
        await _db.SaveChangesAsync();
        return (await DepartmentAsync(d.Id))!;
    }

    // ------------------------------------------------------------------ employees
    public static EmployeeView ToView(OrgEmployee e) => new()
    {
        Id = e.Id, FullName = e.Caption ?? "", LastName = e.Person?.LastName, FirstName = e.Person?.Name, MiddleName = e.Person?.MiddleName,
        Position = e.LabSettings?.PositionName ?? e.PositionType?.Caption, PositionTypeId = e.PositionTypeId,
        LabRole = e.LabSettings?.LabRole ?? LabRoles.Technician, DepartmentId = e.DepartmentId, DepartmentName = e.Department?.Caption,
        Phone = e.Person?.Phone, Email = e.Person?.Email, DigitalSignatureCertId = e.LabSettings?.DigitalSignatureCertId, IsActive = e.LabSettings?.IsActive ?? true
    };

    private IQueryable<OrgEmployee> EmployeesQuery() => _db.Employees.Include(e => e.Department).Include(e => e.PositionType).Where(e => e.RecordState != RecordStates.Deleted);

    public async Task<List<EmployeeView>> EmployeesAsync() =>
        (await EmployeesQuery().AsNoTracking().OrderBy(e => e.Caption).ToListAsync()).Select(ToView).ToList();

    public async Task<EmployeeView?> EmployeeAsync(string id)
    {
        var e = await EmployeesQuery().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return e == null ? null : ToView(e);
    }

    public async Task<EmployeeView> SaveEmployeeAsync(string? id, JsonElement body)
    {
        OrgEmployee e;
        if (id == null)
        {
            var person = new CmnPerson();
            e = new OrgEmployee { Person = person, PersonId = person.Id, OrganizationId = MedLinkDefaults.OrganizationId, WorkingStartDate = DateTime.UtcNow.Date, LabSettings = new LabEmployeeSettings() };
            e.LabSettings.EmployeeId = e.Id;
            _db.Employees.Add(e);
        }
        else
        {
            e = await EmployeesQuery().FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Співробітник", id);
            if (e.LabSettings == null) { e.LabSettings = new LabEmployeeSettings { EmployeeId = e.Id }; _db.EmployeeSettings.Add(e.LabSettings); }
        }
        var before = ToView(e);
        var v = Merge(before, body);
        if (body.TryGetProperty("positionName", out var pn) && pn.ValueKind == JsonValueKind.String) v.Position = pn.GetString(); // сумісність із v4.0
        if (string.IsNullOrWhiteSpace(v.FullName) && string.IsNullOrWhiteSpace(v.LastName)) throw new ValidationException("ПІБ співробітника обов'язкове");
        if (!LabRoles.All.Contains(v.LabRole)) throw ValidationException.Field("labRole", $"Роль має бути однією з: {string.Join(", ", LabRoles.All)}");
        if (v.DepartmentId != null && !await _db.Departments.AnyAsync(d => d.Id == v.DepartmentId)) throw ValidationException.Field("departmentId", "Підрозділ не знайдено");

        // ПІБ: якщо змінено fullName — розкладаємо на прізвище/ім'я/по батькові
        if (body.TryGetProperty("fullName", out _) && !body.TryGetProperty("lastName", out _))
        {
            var parts = (v.FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            v.LastName = parts.ElementAtOrDefault(0); v.FirstName = parts.ElementAtOrDefault(1); v.MiddleName = parts.Length > 2 ? string.Join(" ", parts.Skip(2)) : null;
        }
        var p = e.Person!;
        p.LastName = v.LastName; p.Name = v.FirstName; p.MiddleName = v.MiddleName; p.Phone = v.Phone; p.Email = v.Email;
        p.Caption = CmnPerson.BuildCaption(p.LastName, p.Name, p.MiddleName);
        e.Caption = p.Caption; e.DepartmentId = v.DepartmentId;
        e.PositionTypeId = string.IsNullOrWhiteSpace(v.PositionTypeId) || id == null ? PositionTypeFor(v.LabRole) : v.PositionTypeId!;
        e.LabSettings!.LabRole = v.LabRole; e.LabSettings.PositionName = v.Position; e.LabSettings.DigitalSignatureCertId = v.DigitalSignatureCertId; e.LabSettings.IsActive = v.IsActive;
        _audit.Log(id == null ? "CREATE" : "UPDATE", "org_employee", e.Id, id == null ? null : before, v);
        await _db.SaveChangesAsync();
        return (await EmployeeAsync(e.Id))!;
    }

    // ------------------------------------------------------------------ delete (деактивація при залежностях)
    public async Task<object> DeleteAsync(string name, string id)
    {
        if (name == "departments")
        {
            var d = await _db.Departments.FirstOrDefaultAsync(x => (x.Id == id || x.Code == id) && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Відділення", id);
            var deps = await _db.Employees.CountAsync(x => x.DepartmentId == d.Id) + await _db.Orders.CountAsync(o => o.DepartmentId == d.Id) + await _db.Analyzers.CountAsync(a => a.DepartmentId == d.Id)
                       + await _db.Sections.CountAsync(s => s.DepartmentId == d.Id);
            return await DeleteOrDeactivate("org_department", d, d.LabSettings, deps, s => s.IsActive = false);
        }
        var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Співробітник", id);
        var edeps = await _db.Orders.CountAsync(o => o.DoctorId == e.Id || o.CreatedById == e.Id) + await _db.Results.CountAsync(r => r.VerifiedById == e.Id || r.EnteredById == e.Id);
        return await DeleteOrDeactivate("org_employee", e, e.LabSettings, edeps, s => s.IsActive = false);
    }

    private async Task<object> DeleteOrDeactivate<TSettings>(string table, GuidEntity entity, TSettings? settings, int deps, Action<TSettings> deactivate) where TSettings : class
    {
        if (deps > 0)
        {
            if (settings != null) deactivate(settings);
            _audit.Log("SOFT_DELETE", table, entity.Id, null, new { isActive = false, dependencies = deps });
            await _db.SaveChangesAsync();
            return new { id = entity.Id, deleted = false, deactivated = true, dependencies = deps, message = $"Запис використовується ({deps}) — деактивовано (isActive=false)" };
        }
        // Як в evomis — м'яке видалення record_state = 4
        entity.RecordState = RecordStates.Deleted;
        if (settings != null) deactivate(settings);
        _audit.Log("DELETE", table, entity.Id, null, null);
        await _db.SaveChangesAsync();
        return new { id = entity.Id, deleted = true };
    }

    /// <summary>Накладає присутні в тілі запиту поля на поточний вигляд.</summary>
    private static T Merge<T>(T current, JsonElement body)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(current, Json))!.AsObject();
        foreach (var prop in body.EnumerateObject())
        {
            var key = node.Select(k => k.Key).FirstOrDefault(k => string.Equals(k, prop.Name, StringComparison.OrdinalIgnoreCase));
            if (key != null && !string.Equals(key, "id", StringComparison.OrdinalIgnoreCase)) node[key] = System.Text.Json.Nodes.JsonNode.Parse(prop.Value.GetRawText());
        }
        return JsonSerializer.Deserialize<T>(node.ToJsonString(), Json)!;
    }
}
