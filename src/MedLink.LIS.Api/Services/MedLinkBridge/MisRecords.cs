// Єдина точка запису «спільних» записів МІС: cmn_person, mis_patient_card, mis_diagnostic_report (і проєкції працівника).
// Автономна ЛІС (Medlink:ConnectionString порожньо) створює їх у власній базі, як і раніше.
// У режимі моста з МІС (Medlink:ConnectionString задано) ЛІС НІКОЛИ не створює й не змінює ці записи за власною ініціативою:
//  - нові картки, редагування й видалення пацієнтів, створення співробітників, медичні висновки — відхиляються або пропускаються;
//  - дозволена лише проєкція (копія для читання) з бази МІС у власну схему ЛІС (Database:Schema) за тими самими id.
// Запису в схему МІС (public) немає в жодному режимі: підключення до МІС (Medlink:ConnectionString) використовується лише для SELECT.
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MedLink.LIS.Api.Services.MedLinkBridge;

public interface IMisRecords
{
    /// <summary>Міст з МІС увімкнено: картки пацієнтів, особи, співробітники й висновки належать МІС.</summary>
    bool Bridged { get; }
    /// <summary>Нова картка пацієнта з особою (автономний режим). У режимі моста — відмова (картки створює МІС).</summary>
    MisPatientCard AddPatient(NewPatientRequest req);
    /// <summary>Зміна даних картки й особи. У режимі моста — відмова.</summary>
    void UpdatePatient(MisPatientCard card, NewPatientRequest req);
    /// <summary>Видалення картки (і особи, якщо вона більше ніде не використовується). У режимі моста — відмова.</summary>
    Task RemovePatientAsync(MisPatientCard card);
    /// <summary>Створення особи для нового співробітника (автономний режим). У режимі моста — відмова (співробітників веде МІС).</summary>
    CmnPerson AddEmployeePerson();
    /// <summary>Медичний висновок за замовленням. Повертає false (і нічого не пише), якщо міст увімкнено: висновок формує МІС за подією результату.</summary>
    bool TryAddDiagnosticReport(MisDiagnosticReport report);
    /// <summary>Проєкція картки пацієнта МІС у власну схему ЛІС (id збігається з id картки МІС). null — картки немає в МІС. Лише в режимі моста.</summary>
    Task<MisPatientCard?> ImportPatientAsync(string cardId, CancellationToken ct = default);
    /// <summary>Проєкція працівника МІС (особа, організація, підрозділ, роль ЛІС) у власну схему ЛІС. Лише в режимі моста.</summary>
    Task<OrgEmployee> ProjectEmployeeAsync(MedLinkPerson person, MedLinkEmployee employee, string role, CancellationToken ct = default);
}

public sealed class MisRecords : IMisRecords
{
    private readonly LisDbContext _db;
    private readonly IMedLinkDirectory _directory;
    private readonly MedLinkOptions _options;

    public MisRecords(LisDbContext db, IMedLinkDirectory directory, IOptions<MedLinkOptions> options)
    {
        _db = db; _directory = directory; _options = options.Value;
    }

    public bool Bridged => _options.Enabled;

    private static ConflictException Blocked(string what) =>
        new($"{what} у лабораторії недоступне: картки пацієнтів, особи та співробітники ведуться в МІС. Виконайте дію в МІС — зміни з’являться в лабораторії автоматично.");

    public MisPatientCard AddPatient(NewPatientRequest req)
    {
        if (Bridged) throw Blocked("Створення картки пацієнта");
        var card = MedLinkPeople.NewPatient(req, MedLinkDefaults.OrganizationId);
        _db.Patients.Add(card);
        return card;
    }

    public void UpdatePatient(MisPatientCard card, NewPatientRequest req)
    {
        if (Bridged) throw Blocked("Редагування картки пацієнта");
        MedLinkPeople.Apply(card, req, isNew: false);
    }

    public async Task RemovePatientAsync(MisPatientCard card)
    {
        if (Bridged) throw Blocked("Видалення картки пацієнта");
        _db.Patients.Remove(card);
        if (card.Person != null && !await _db.Patients.AnyAsync(x => x.PersonId == card.PersonId && x.Id != card.Id) && !await _db.Employees.AnyAsync(e => e.PersonId == card.PersonId))
            _db.Persons.Remove(card.Person);
    }

    public CmnPerson AddEmployeePerson()
    {
        if (Bridged) throw Blocked("Створення співробітника");
        return new CmnPerson();
    }

    public bool TryAddDiagnosticReport(MisDiagnosticReport report)
    {
        if (Bridged) return false;
        _db.DiagnosticReports.Add(report);
        return true;
    }

    public async Task<MisPatientCard?> ImportPatientAsync(string cardId, CancellationToken ct = default)
    {
        if (!Bridged || !Guid.TryParse(cardId, out _)) return null;
        var card = await _directory.PatientAsync(cardId, ct);
        if (card == null) return null;
        var mis = await _directory.PersonAsync(card.PersonId, ct);

        var person = await _db.Persons.FirstOrDefaultAsync(p => p.Id == card.PersonId, ct);
        if (person == null) { person = new CmnPerson { Id = card.PersonId }; _db.Persons.Add(person); }
        ApplyPerson(person, mis);
        person.GenderId = MedLinkEnums.GenderId(SexCode(card.Sex));
        person.Phone ??= card.Phone;
        person.Caption = CmnPerson.BuildCaption(person.LastName, person.Name, person.MiddleName);

        await EnsureOrganizationAsync(card.OrganizationId, ct);
        var local = await _db.Patients.FirstOrDefaultAsync(p => p.Id == cardId, ct);
        if (local == null)
        {
            local = new MisPatientCard
            {
                Id = cardId, RegDate = DateTime.UtcNow,
                DocumentTypeId = MedLinkDefaults.PatientCardDocumentTypeId, PatientCardTypeId = MedLinkDefaults.PatientCardTypePersonId, PrivacyRequestTypeId = MedLinkEnums.PrivacyUnknown
            };
            _db.Patients.Add(local);
        }
        local.PersonId = person.Id; local.Person = person; local.OrganizationId = card.OrganizationId; local.RegNumber = card.RegNumber;
        local.Caption = person.Caption; local.Birthday = card.Birthday ?? person.Birthday; local.GenderId = person.GenderId;
        local.LastNameLatin = Core.Common.TransliterationKmu2010.ToLatin(person.LastName ?? "");
        local.FirstNameLatin = Core.Common.TransliterationKmu2010.ToLatin(person.Name ?? "");
        await _db.SaveChangesAsync(ct);
        return local;
    }

    public async Task<OrgEmployee> ProjectEmployeeAsync(MedLinkPerson person, MedLinkEmployee employee, string role, CancellationToken ct = default)
    {
        if (!Bridged) throw new InvalidOperationException("Проєкція працівника можлива лише в режимі моста з МІС");
        await EnsureOrganizationAsync(employee.OrganizationId, ct, employee.OrganizationName);
        var local = await _db.Persons.FirstOrDefaultAsync(p => p.Id == person.Id, ct);
        if (local == null) { local = new CmnPerson { Id = person.Id, NoIpn = string.IsNullOrWhiteSpace(person.Ipn) }; _db.Persons.Add(local); }
        ApplyPerson(local, person);
        local.Caption = CmnPerson.BuildCaption(local.LastName, local.Name, local.MiddleName);

        if (employee.DepartmentId != null && !await _db.Departments.AnyAsync(d => d.Id == employee.DepartmentId, ct))
            _db.Departments.Add(new OrgDepartment
            {
                Id = employee.DepartmentId, Caption = employee.DepartmentName ?? "Підрозділ МІС", FullName = employee.DepartmentName, Code = "ML-" + employee.DepartmentId[..8],
                OrganizationId = employee.OrganizationId, DepartmentTypeId = OrgDictionaryService.DepartmentTypeFor("CLINICAL"),
                LabSettings = new LabDepartmentSettings { DepartmentId = employee.DepartmentId, LabKind = "CLINICAL" }
            });

        var current = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employee.Id, ct);
        if (current == null)
        {
            current = new OrgEmployee { Id = employee.Id, WorkingStartDate = DateTime.UtcNow, LabSettings = new LabEmployeeSettings { EmployeeId = employee.Id } };
            _db.Employees.Add(current);
        }
        current.PersonId = person.Id; current.Person = local; current.Caption = local.Caption; current.OrganizationId = employee.OrganizationId; current.DepartmentId = employee.DepartmentId;
        current.PositionTypeId = OrgDictionaryService.PositionTypeFor(role);
        current.LabSettings ??= new LabEmployeeSettings { EmployeeId = employee.Id };
        current.LabSettings.LabRole = role; // роль ведеться профілем безпеки МІС
        current.LabSettings.IsActive = true;
        await _db.SaveChangesAsync(ct);
        return current;
    }

    private static void ApplyPerson(CmnPerson target, MedLinkPerson? source)
    {
        if (source == null) return;
        target.LastName = source.LastName; target.Name = source.Name; target.MiddleName = source.MiddleName; target.Birthday = source.Birthday;
        target.Phone = source.Phone; target.Email = source.Email; target.Ipn = source.Ipn; target.NoIpn = string.IsNullOrWhiteSpace(source.Ipn);
    }

    private static string SexCode(string sex) => sex switch { "male" => "M", "female" => "F", _ => "U" };

    private async Task EnsureOrganizationAsync(string organizationId, CancellationToken ct, string? caption = null)
    {
        if (await _db.Organizations.AnyAsync(o => o.Id == organizationId, ct)) return;
        caption ??= (await _directory.OrganizationAsync(organizationId, ct))?.Caption;
        _db.Organizations.Add(new OrgOrganization { Id = organizationId, Caption = caption ?? "Заклад МІС", FullName = caption, Code = "ML-" + organizationId[..8] });
        await _db.SaveChangesAsync(ct);
    }
}
