// Вирівнювання з MedLink (evomis): структура дзеркальних таблиць, uuid-ключі, record_state, created_by, cmn_person,
// mis_diagnostic_report при видачі, резервування БД попередньої структури
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Data.Seed;
using MedLink.LIS.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class MedLinkAlignmentTests
{
    private readonly LisApiFactory _f;
    public MedLinkAlignmentTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private async Task<T> Db<T>(Func<LisDbContext, Task<T>> f)
    {
        using var scope = _f.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<LisDbContext>());
    }

    [Fact]
    public async Task New_patient_is_stored_as_cmn_person_plus_mis_patient_card()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var dto = await Json(await reg.PostAsJsonAsync("/api/v1/lab/patients", new
        {
            lastName = "Петренко", firstName = "Ольга", secondName = "Іванівна", birthDate = "1990-05-17", gender = "F", phone = "+380501112233", taxId = "3300112233", address = "м. Львів"
        }));
        var id = dto["id"]!.GetValue<string>();
        Assert.True(Guid.TryParse(id, out _));
        Assert.Equal("Петренко Ольга Іванівна", dto["fullName"]!.GetValue<string>());
        Assert.Equal("Petrenko", dto["lastNameLatin"]!.GetValue<string>());

        var card = await Db(db => db.Patients.AsNoTracking().FirstAsync(p => p.Id == id));
        Assert.NotNull(card.Person);
        Assert.Equal(card.PersonId, card.Person!.Id);
        Assert.Equal("Петренко", card.Person.LastName);
        Assert.Equal("Ольга", card.Person.Name);
        Assert.Equal("Іванівна", card.Person.MiddleName);
        Assert.Equal("3300112233", card.Person.Ipn);
        Assert.Equal(MedLinkEnums.GenderFemale, card.Person.GenderId); // GUID як в evomis DbInitializer
        Assert.Equal(MedLinkEnums.GenderFemale, card.GenderId);
        Assert.Equal("Петренко Ольга Іванівна", card.Caption);
        Assert.Equal(MedLinkDefaults.OrganizationId, card.OrganizationId);
        Assert.Equal(RecordStates.Active, card.RecordState);
        Assert.Equal(DemoDataSeeder.Registrar, card.CreatedBy);

        // Пошук за ПІБ, ІПН і телефоном (колонки cmn_person)
        foreach (var q in new[] { "Петренко", "3300112233", "1112233" })
        {
            var found = await Json(await reg.GetAsync($"/api/v1/lab/patients?search={Uri.EscapeDataString(q)}"));
            Assert.Contains(found["items"]!.AsArray(), p => p!["id"]!.GetValue<string>() == id);
        }
    }

    [Fact]
    public async Task Released_order_creates_mis_diagnostic_report_linked_from_lab_order()
    {
        var (order, report) = await Db(async db =>
        {
            var o = await db.Orders.AsNoTracking().Where(x => x.DiagnosticReportId != null).OrderBy(x => x.OrderDatetime).FirstAsync();
            var r = await db.DiagnosticReports.AsNoTracking().FirstAsync(x => x.Id == o.DiagnosticReportId);
            return (o, r);
        });
        Assert.Equal(order.PatientId, report.PatientCardId);
        Assert.Equal(order.OrderNumber, report.RegNumber);
        Assert.Equal(0, report.Status); // DiagnosticReportTransferStatus.NotTransfered
        Assert.NotEqual(MedLinkEnums.EmptyGuid, report.EhealthServiceCatalogServiceId);
        Assert.True(await Db(db => db.ServiceCatalog.AnyAsync(s => s.Id == report.EhealthServiceCatalogServiceId)));
        Assert.NotNull(report.IssuedAt);
        Assert.Equal(MedLinkDefaults.OrganizationId, order.OrganizationId);
    }

    [Fact]
    public async Task Mirror_tables_use_uuid_keys_and_evomis_audit_columns()
    {
        var ids = await Db(async db => new
        {
            persons = await db.Persons.AsNoTracking().Select(p => p.Id).ToListAsync(),
            patients = await db.Patients.AsNoTracking().Select(p => p.Id).ToListAsync(),
            employees = await db.Employees.AsNoTracking().Select(e => new { e.Id, e.PersonId, e.CreatedBy, e.RecordState }).ToListAsync(),
            departments = await db.Departments.AsNoTracking().Select(d => d.Id).ToListAsync(),
            orders = await db.Orders.AsNoTracking().Select(o => new { o.Id, o.CreatedBy }).ToListAsync()
        });
        Assert.All(ids.persons.Concat(ids.patients).Concat(ids.departments), id => Assert.True(Guid.TryParse(id, out _), id));
        Assert.All(ids.employees, e => { Assert.True(Guid.TryParse(e.Id, out _)); Assert.True(Guid.TryParse(e.PersonId, out _)); Assert.True(Guid.TryParse(e.CreatedBy, out _)); Assert.Contains(e.RecordState, new[] { RecordStates.Active, RecordStates.Deleted }); });
        Assert.All(ids.employees.Where(e => e.Id.StartsWith("0e000000-")), e => Assert.Equal(RecordStates.Active, e.RecordState)); // демо-співробітники
        Assert.All(ids.orders, o => { Assert.True(Guid.TryParse(o.Id, out _)); Assert.True(Guid.TryParse(o.CreatedBy, out _)); });
        // Гендерні записи з тими самими GUID і кодами, що в evomis
        var gender = await Db(db => db.EnumRecords.AsNoTracking().Where(e => e.EnumType == "Gender").ToDictionaryAsync(e => e.Code!, e => e.Id));
        Assert.Equal("07b60329-7282-4262-9409-b24ea4440428", gender["M"]);
        Assert.Equal("01f0eff5-a698-4873-a45b-ad034a386d08", gender["F"]);
    }

    [Fact]
    public async Task Department_and_employee_dictionaries_map_to_medlink_tables()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var dep = await Json(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/departments", new { code = "CP-77", name = "Пункт забору №77", labKind = "COLLECTION_POINT", address = "м. Київ, вул. Тестова, 7", phone = "+380440000077" }));
        var depId = dep["id"]!.GetValue<string>();
        Assert.Equal("COLLECTION_POINT", dep["labKind"]!.GetValue<string>());
        var emp = await Json(await admin.PostAsJsonAsync("/api/v1/lab/dictionaries/employees", new { fullName = "Тестенко Тарас Петрович", labRole = "PHLEBOTOMIST", position = "Медсестра", departmentId = depId, phone = "+380500000077" }));
        var empId = emp["id"]!.GetValue<string>();

        var (d, e) = await Db(async db => (await db.Departments.AsNoTracking().FirstAsync(x => x.Id == depId), await db.Employees.AsNoTracking().FirstAsync(x => x.Id == empId)));
        Assert.Equal("Пункт забору №77", d.Caption);
        Assert.Equal(MedLinkEnums.DeptTypeDiagnostic, d.DepartmentTypeId);
        Assert.Equal("COLLECTION_POINT", d.LabSettings!.LabKind);
        Assert.Equal("Тестенко Тарас Петрович", e.Caption);
        Assert.Equal("Тестенко", e.Person!.LastName);
        Assert.Equal("Тарас", e.Person.Name);
        Assert.Equal(MedLinkEnums.PositionNurses, e.PositionTypeId);
        Assert.Equal("PHLEBOTOMIST", e.LabSettings!.LabRole);

        // Видалення без залежностей співробітника — м'яке, record_state = 4 (як в evomis)
        var del = await Json(await admin.DeleteAsync($"/api/v1/lab/dictionaries/employees/{empId}"));
        Assert.True(del["deleted"]!.GetValue<bool>());
        Assert.Equal(RecordStates.Deleted, await Db(db => db.Employees.AsNoTracking().Where(x => x.Id == empId).Select(x => x.RecordState).FirstAsync()));
        var list = await Json(await admin.GetAsync("/api/v1/lab/dictionaries/employees?pageSize=200"));
        Assert.DoesNotContain(list["items"]!.AsArray(), x => x!["id"]!.GetValue<string>() == empId);
    }

    [Fact]
    public async Task Legacy_database_is_backed_up_before_recreation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"medlink-lis-legacy-{Guid.NewGuid():N}.db");
        try
        {
            using (var c = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = "CREATE TABLE lab_order (id TEXT PRIMARY KEY, is_deleted INTEGER NOT NULL); CREATE TABLE mis_patient_card (id TEXT PRIMARY KEY, last_name TEXT);";
                cmd.ExecuteNonQuery();
            }
            var options = new DbContextOptionsBuilder<LisDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            string? backup;
            await using (var db = new LisDbContext(options)) backup = await SchemaUpgrader.BackupIfIncompatibleAsync(db);
            Assert.NotNull(backup);
            Assert.True(File.Exists(backup));
            Assert.False(File.Exists(path));
            await using (var db = new LisDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                Assert.Null(await SchemaUpgrader.BackupIfIncompatibleAsync(db)); // нова структура — без резервування
                Assert.Equal(0, await db.Persons.CountAsync());
            }
            File.Delete(backup!);
        }
        finally { try { File.Delete(path); } catch { /* ignore */ } }
    }
}
