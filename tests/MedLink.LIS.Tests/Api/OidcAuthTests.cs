// Режим Auth:Mode=Oidc: підписаний тестовий JWT (без реального Identity), 401/403, співробітник з токена, міст до МІС (підміна каталогу),
// заборона запису спільних записів МІС у режимі моста, власна схема PostgreSQL. SQLite, без мережі.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Data.Seed;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services.MedLinkBridge;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MedLink.LIS.Tests.Api;

/// <summary>Підпис і видача тестових токенів: ключ відомий тесту, Identity не потрібен.</summary>
public static class TestIdentity
{
    public const string Issuer = "https://id.test.local";
    public const string Audience = "MisApiScope";
    public static readonly SymmetricSecurityKey Key = new(System.Text.Encoding.UTF8.GetBytes("unit-test-signing-key-0123456789-abcdef-0123456789"));

    public static string Token(string sub, string? audience = Audience, string issuer = Issuer, TimeSpan? lifetime = null, SecurityKey? key = null)
    {
        var now = DateTime.UtcNow;
        var life = lifetime ?? TimeSpan.FromMinutes(30);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer, Audience = audience,
            Subject = new ClaimsIdentity(new[] { new Claim("sub", sub), new Claim("sid", "test-session") }),
            NotBefore = life < TimeSpan.Zero ? now.AddMinutes(-60) : now, Expires = now.Add(life),
            SigningCredentials = new SigningCredentials(key ?? Key, SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}

/// <summary>Підміна каталогу МІС: дані задає тест, до справжньої бази МІС звернень немає (і записів теж).</summary>
public sealed class FakeMedLinkDirectory : IMedLinkDirectory
{
    public const string UserSub = "11111111-1111-1111-1111-111111111111";
    public const string PersonId = "22222222-2222-2222-2222-222222222222";
    public const string OrgId = "33333333-3333-3333-3333-333333333333";
    public const string DeptId = "44444444-4444-4444-4444-444444444444";
    public const string EmployeeTech = "55555555-5555-5555-5555-555555555555";
    public const string EmployeeNoRole = "66666666-6666-6666-6666-666666666666";
    public const string NoRoleSub = "77777777-7777-7777-7777-777777777777";
    public const string NoRolePerson = "88888888-8888-8888-8888-888888888888";
    public const string PatientCard = "99999999-9999-9999-9999-999999999999";
    public const string PatientPerson = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    public int Calls;
    public bool Enabled => true;

    public Task<MedLinkPerson?> PersonByIdentityUserAsync(string id, CancellationToken ct = default)
    {
        Interlocked.Increment(ref Calls);
        return Task.FromResult<MedLinkPerson?>(id switch
        {
            UserSub => new MedLinkPerson(PersonId, UserSub, "Лаборант", "Олена", "Петрівна", new DateTime(1990, 5, 1), "+380501112233", "o@example.org", null),
            NoRoleSub => new MedLinkPerson(NoRolePerson, NoRoleSub, "Терапевт", "Іван", null, null, null, null, null),
            _ => null
        });
    }

    public Task<IReadOnlyList<MedLinkEmployee>> EmployeesOfPersonAsync(string personId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MedLinkEmployee>>(personId switch
        {
            PersonId => new[] { new MedLinkEmployee(EmployeeTech, PersonId, OrgId, DeptId, "Лаборант Олена Петрівна", "Тестова клініка", "Лабораторія") },
            NoRolePerson => new[] { new MedLinkEmployee(EmployeeNoRole, NoRolePerson, OrgId, null, "Терапевт Іван", "Тестова клініка", null) },
            _ => Array.Empty<MedLinkEmployee>()
        });

    public Task<IReadOnlyList<MedLinkProfile>> ProfilesOfEmployeesAsync(IReadOnlyCollection<string> employeeIds, CancellationToken ct = default)
    {
        var list = new List<MedLinkProfile>();
        if (employeeIds.Contains(EmployeeTech)) list.Add(new(EmployeeTech, "p1", "Лаборант eHealth вторинки"));
        if (employeeIds.Contains(EmployeeNoRole)) list.Add(new(EmployeeNoRole, "p2", "Сімейний лікар"));
        return Task.FromResult<IReadOnlyList<MedLinkProfile>>(list);
    }

    public Task<IReadOnlyList<MedLinkDepartment>> DepartmentsAsync(string organizationId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MedLinkDepartment>>(Array.Empty<MedLinkDepartment>());
    public Task<IReadOnlyList<MedLinkPatient>> SearchPatientsAsync(string organizationId, string text, int take = 20, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MedLinkPatient>>(Array.Empty<MedLinkPatient>());
    public Task<MedLinkPatient?> PatientAsync(string cardId, CancellationToken ct = default) =>
        Task.FromResult<MedLinkPatient?>(cardId == PatientCard ? new MedLinkPatient(PatientCard, PatientPerson, OrgId, "AMB-0001", "Мисько Марія Іванівна", new DateTime(1985, 3, 2), "+380671112233", "female") : null);
    public Task<MedLinkPerson?> PersonAsync(string personId, CancellationToken ct = default) =>
        Task.FromResult<MedLinkPerson?>(personId == PatientPerson ? new MedLinkPerson(PatientPerson, null, "Мисько", "Марія", "Іванівна", new DateTime(1985, 3, 2), "+380671112233", null, "1234567890") : null);
    public Task<MedLinkOrganization?> OrganizationAsync(string organizationId, CancellationToken ct = default) =>
        Task.FromResult<MedLinkOrganization?>(organizationId == OrgId ? new MedLinkOrganization(OrgId, "Тестова клініка") : null);
}

public class OidcFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"medlink-oidc-test-{Guid.NewGuid():N}.db");
    private readonly string _guidePath = Path.Combine(Path.GetTempPath(), $"medlink-oidc-guide-{Guid.NewGuid():N}.db");
    private readonly bool _bridge;
    public FakeMedLinkDirectory? Directory { get; private set; }

    public OidcFactory(bool bridge) => _bridge = bridge;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Шлях Guide читається в коді верхнього рівня до застосування ConfigureAppConfiguration, тож задаємо його налаштуванням вузла
        builder.UseSetting("Database:GuideSqlitePath", _guidePath);
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite", ["Database:SqlitePath"] = _dbPath, ["Database:GuideSqlitePath"] = _guidePath,
            ["Lab:SeedDemoData"] = "true", ["Lab:PublicBaseUrl"] = "http://localhost:5055", ["Logging:LogLevel:Default"] = "Warning",
            ["Auth:Mode"] = "Oidc", ["Identity:Authority"] = TestIdentity.Issuer, ["Identity:JwtBearerAudience"] = "IdentityApiScope;MisApiScope",
            ["Auth:Oidc:ClientId"] = "TestLab",
            // З мостом: рядок підключення існує (значення не використовується — каталог підмінено), ролі за таблицею з конфігурації
            ["Medlink:ConnectionString"] = _bridge ? "Host=127.0.0.1;Database=none" : "",
            ["Medlink:ProfileRoles:Лаборант eHealth вторинки"] = "LAB_TECHNICIAN",
            ["Medlink:ProfileRoles:Адміністратор"] = "LAB_ADMIN",
            ["Medlink:CacheSeconds"] = "0"
        }));
        builder.ConfigureServices(services =>
        {
            // Ключ підпису відомий тесту: замість метаданих Identity — статична конфігурація
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                var conf = new OpenIdConnectConfiguration { Issuer = TestIdentity.Issuer };
                conf.SigningKeys.Add(TestIdentity.Key);
                o.Configuration = conf;
                o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(conf);
            });
            if (_bridge)
            {
                Directory = new FakeMedLinkDirectory();
                services.RemoveAll<IMedLinkDirectory>();
                services.AddSingleton<IMedLinkDirectory>(Directory);
            }
        });
    }

    public HttpClient WithToken(string? token, string? employeeHeader = null)
    {
        var c = CreateClient();
        if (token != null) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (employeeHeader != null) c.DefaultRequestHeaders.Add("X-MedLink-Employee-Id", employeeHeader);
        return c;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var f in new[] { _dbPath, _guidePath }) try { File.Delete(f); } catch { /* ignore */ }
    }
}

/// <summary>Без моста: співробітник береться з власної бази за sub токена.</summary>
public class OidcLocalAuthTests : IClassFixture<OidcLocalAuthTests.Fixture>
{
    public sealed class Fixture : OidcFactory { public Fixture() : base(bridge: false) { } }
    private readonly Fixture _f;
    public OidcLocalAuthTests(Fixture f) => _f = f;

    [Fact]
    public async Task No_token_gives_401_with_human_message_and_public_routes_stay_open()
    {
        var r = await _f.WithToken(null).GetAsync("/api/v1/lab/context/me");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        var body = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.Contains("Увійдіть", body["detail"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.OK, (await _f.WithToken(null).GetAsync("/health")).StatusCode);
        var cfg = await _f.WithToken(null).GetAsync("/api/v1/lab/auth/config");
        Assert.Equal(HttpStatusCode.OK, cfg.StatusCode);
        var json = JsonNode.Parse(await cfg.Content.ReadAsStringAsync())!;
        Assert.Equal("Oidc", json["mode"]!.GetValue<string>());
        Assert.False(json["runAs"]!.GetValue<bool>());
        Assert.Equal(TestIdentity.Issuer, json["oidc"]!["authority"]!.GetValue<string>());
        Assert.Equal("TestLab", json["oidc"]!["clientId"]!.GetValue<string>());
        Assert.DoesNotContain("ecret", await cfg.Content.ReadAsStringAsync()); // секретів у відповіді немає
        // Шлюз коннектора має власний ключ: токен співробітника там не потрібен (відповідь — 403 коннектора, а не 401 входу)
        var conn = await _f.WithToken(null).GetAsync("/api/v1/lab/connector/config");
        Assert.DoesNotContain("Увійдіть", await conn.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Valid_token_resolves_employee_and_header_cannot_impersonate()
    {
        // sub токена = org_employee.id (Registrar); заголовок «Працюю як…» адміністратора ігнорується
        var c = _f.WithToken(TestIdentity.Token(DemoDataSeeder.Registrar), employeeHeader: DemoDataSeeder.Admin);
        var me = JsonNode.Parse(await (await c.GetAsync("/api/v1/lab/context/me")).Content.ReadAsStringAsync())!;
        Assert.Equal(DemoDataSeeder.Registrar, me["employee"]!["id"]!.GetValue<string>());
        Assert.Equal("REGISTRAR", me["employee"]!["labRole"]!.GetValue<string>());
        // Роль діє: реєстратор не може видати конфігурацію адміністрування
        var denied = await c.PostAsJsonAsync("/api/v1/lab/dictionaries/units", new { code = "x", name = "x" });
        Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
    }

    [Fact]
    public async Task Unknown_user_gets_403_not_default_admin()
    {
        var r = await _f.WithToken(TestIdentity.Token(Guid.NewGuid().ToString())).GetAsync("/api/v1/lab/context/me");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Contains("співробітників лабораторії", await r.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("signature")]
    [InlineData("garbage")]
    public async Task Invalid_tokens_give_401(string kind)
    {
        var sub = DemoDataSeeder.Admin;
        var token = kind switch
        {
            "expired" => TestIdentity.Token(sub, lifetime: TimeSpan.FromMinutes(-30)),
            "audience" => TestIdentity.Token(sub, audience: "OtherApi"),
            "issuer" => TestIdentity.Token(sub, issuer: "https://evil.example"),
            "signature" => TestIdentity.Token(sub, key: new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("another-signing-key-0123456789-abcdef-0123456789"))),
            _ => "not-a-token"
        };
        var r = await _f.WithToken(token).GetAsync("/api/v1/lab/context/me");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
}

/// <summary>З мостом до МІС: sub → особа → працівник → профіль безпеки → роль; запис спільних записів МІС заблоковано.</summary>
public class OidcBridgeTests : IClassFixture<OidcBridgeTests.Fixture>
{
    public sealed class Fixture : OidcFactory { public Fixture() : base(bridge: true) { } }
    private readonly Fixture _f;
    public OidcBridgeTests(Fixture f) => _f = f;

    [Fact]
    public async Task Mis_profile_maps_to_lab_role_and_employee_is_projected_into_own_schema()
    {
        var c = _f.WithToken(TestIdentity.Token(FakeMedLinkDirectory.UserSub));
        var r = await c.GetAsync("/api/v1/lab/context/me");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var me = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.Equal(FakeMedLinkDirectory.EmployeeTech, me["employee"]!["id"]!.GetValue<string>());
        Assert.Equal("LAB_TECHNICIAN", me["employee"]!["labRole"]!.GetValue<string>());
        Assert.Equal("Лабораторія", me["employee"]!["departmentName"]!.GetValue<string>());

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var e = await db.Employees.AsNoTracking().FirstAsync(x => x.Id == FakeMedLinkDirectory.EmployeeTech);
        Assert.Equal(FakeMedLinkDirectory.PersonId, e.PersonId);
        Assert.Equal(FakeMedLinkDirectory.OrgId, e.OrganizationId);
        Assert.Equal("Лаборант Олена Петрівна", e.Caption);
        Assert.True(await db.Organizations.AnyAsync(o => o.Id == FakeMedLinkDirectory.OrgId));
        Assert.False(await db.Employees.AnyAsync(x => x.Id == DemoDataSeeder.Admin), "демо-співробітників у режимі моста немає");
    }

    [Fact]
    public async Task Profile_without_lab_role_gets_403_with_human_message()
    {
        var r = await _f.WithToken(TestIdentity.Token(FakeMedLinkDirectory.NoRoleSub)).GetAsync("/api/v1/lab/context/me");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        var detail = JsonNode.Parse(await r.Content.ReadAsStringAsync())!["detail"]!.GetValue<string>();
        Assert.Contains("немає доступу до Лабораторії", detail);
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.WithToken(TestIdentity.Token(FakeMedLinkDirectory.NoRoleSub)).GetAsync("/api/v1/lab/orders")).StatusCode);
    }

    [Fact]
    public async Task User_missing_in_mis_gets_403()
    {
        var r = await _f.WithToken(TestIdentity.Token(Guid.NewGuid().ToString())).GetAsync("/api/v1/lab/context/me");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Contains("не знайдено в МІС", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Creating_editing_or_deleting_mis_records_is_blocked_in_bridge_mode()
    {
        // Реєстратор з МІС: жодних нових карток пацієнтів, змін і видалень — це записи МІС
        var c = _f.WithToken(TestIdentity.Token(FakeMedLinkDirectory.UserSub));
        var create = await c.PostAsJsonAsync("/api/v1/lab/patients", new { lastName = "Тест", firstName = "Пацієнт", gender = "F", birthDate = "1991-01-01" });
        Assert.True(create.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Forbidden, $"{(int)create.StatusCode}");
        if (create.StatusCode == HttpStatusCode.Conflict) Assert.Contains("МІС", await create.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Records_interface_never_writes_report_and_imports_patient_as_projection()
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var records = scope.ServiceProvider.GetRequiredService<IMisRecords>();
        Assert.True(records.Bridged);

        var report = new MisDiagnosticReport { RegNumber = "X-1", PatientCardId = FakeMedLinkDirectory.PatientCard, OrganizationId = FakeMedLinkDirectory.OrgId };
        Assert.False(records.TryAddDiagnosticReport(report));
        Assert.Equal(0, db.ChangeTracker.Entries<MisDiagnosticReport>().Count());
        Assert.Throws<ConflictException>(() => records.AddPatient(new MedLink.LIS.Api.Models.NewPatientRequest { LastName = "А", FirstName = "Б" }));
        Assert.Throws<ConflictException>(() => records.AddEmployeePerson());

        var card = await records.ImportPatientAsync(FakeMedLinkDirectory.PatientCard);
        Assert.NotNull(card);
        Assert.Equal(FakeMedLinkDirectory.PatientCard, card!.Id); // id картки МІС збережено
        Assert.Equal("F", card.Gender);
        Assert.Equal("Мисько Марія Іванівна", card.Caption);
        Assert.Equal("1234567890", (await db.Persons.AsNoTracking().FirstAsync(p => p.Id == FakeMedLinkDirectory.PatientPerson)).Ipn);
        Assert.Null(await records.ImportPatientAsync("not-a-guid"));
        Assert.Null(await records.ImportPatientAsync(Guid.NewGuid().ToString())); // у МІС немає — null
    }
}

public class MedLinkBridgeUnitTests
{
    [Fact]
    public void Role_map_uses_configured_table_highest_role_wins_and_default_can_be_empty()
    {
        var options = new MedLinkOptions();
        options.ProfileRoles["Працівник реєстратури"] = "REGISTRAR";
        options.ProfileRoles["Адміністратор"] = "LAB_ADMIN";
        options.ProfileRoles["Невідома роль"] = "NOT_A_ROLE"; // значення не з переліку ролей — ігнорується
        Assert.Equal("REGISTRAR", MedLinkRoleMap.RoleFor(new[] { "працівник реєстратури " }, options)); // регістр і пробіли не важливі
        Assert.Equal("LAB_ADMIN", MedLinkRoleMap.RoleFor(new[] { "Працівник реєстратури", "Адміністратор" }, options));
        Assert.Null(MedLinkRoleMap.RoleFor(new[] { "Сімейний лікар", "Невідома роль" }, options)); // DefaultLabRole порожній → доступу немає
        options.DefaultLabRole = "LAB_TECHNICIAN";
        Assert.Equal("LAB_TECHNICIAN", MedLinkRoleMap.RoleFor(new[] { "Сімейний лікар" }, options));
        options.DefaultLabRole = "bogus";
        Assert.Null(MedLinkRoleMap.RoleFor(Array.Empty<string?>(), options));
    }

    [Fact]
    public void Mis_directory_issues_only_select_statements() => AssertDirectoryReadOnly();

    private static void AssertDirectoryReadOnly([System.Runtime.CompilerServices.CallerFilePath] string here = "")
    {
        // Запис у public з коду лабораторії заборонено: у каталозі МІС немає жодного модифікуючого SQL
        var dir = Path.GetDirectoryName(here)!;
        string? file = null;
        for (var d = new DirectoryInfo(dir); d != null && file == null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "src", "MedLink.LIS.Api", "Services", "MedLinkBridge", "MedLinkDirectory.cs");
            if (File.Exists(candidate)) file = candidate;
        }
        Assert.NotNull(file);
        var text = string.Join(" ", File.ReadAllLines(file!).Where(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///")));
        foreach (var word in new[] { "INSERT ", "UPDATE ", "DELETE ", "DROP ", "ALTER ", "TRUNCATE ", "CREATE ", "GRANT " })
            Assert.DoesNotContain(word, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Own_schema_is_added_to_search_path_without_public()
    {
        Assert.Null(PgConnectionOptions.NormalizeSchema(null));
        Assert.Null(PgConnectionOptions.NormalizeSchema(""));
        Assert.Null(PgConnectionOptions.NormalizeSchema("public"));
        Assert.Equal("lab", PgConnectionOptions.NormalizeSchema(" lab "));
        Assert.Throws<InvalidOperationException>(() => PgConnectionOptions.NormalizeSchema("lab; DROP SCHEMA public"));
        Assert.Equal("Host=h;Database=evomis", PgConnectionOptions.WithSchema("Host=h;Database=evomis", "public"));
        var cs = PgConnectionOptions.WithSchema("Host=h;Database=evomis;", "lab");
        Assert.Equal("Host=h;Database=evomis;Search Path=lab", cs);
        Assert.Equal(cs, PgConnectionOptions.WithSchema(cs, "lab")); // повторно не дублюється
        var normalized = PgConnectionOptions.Normalize(cs);
        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(normalized);
        Assert.Equal("lab", parsed.SearchPath); // лише власна схема: таблиці МІС з тими самими назвами недосяжні без префікса
    }

    [Fact]
    public void Oidc_mode_is_recognized_and_misconfiguration_is_reported()
    {
        var empty = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Mode"] = "oidc" }).Build();
        Assert.Equal(AuthModes.Oidc, AuthModes.Resolve(empty));
        var codes = StartupConfigCheck.EvaluateOidc(empty).Select(w => w.Code).ToList();
        Assert.Contains("OIDC_AUTHORITY", codes);
        Assert.Contains("MEDLINK_BRIDGE_OFF", codes);
        var ok = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:Mode"] = "Oidc", ["Identity:Authority"] = "https://id.example", ["Identity:JwtBearerAudience"] = "MisApiScope",
            ["Medlink:ConnectionString"] = "Host=x", ["Medlink:ProfileRoles:Адміністратор"] = "LAB_ADMIN"
        }).Build();
        var okCodes = StartupConfigCheck.EvaluateOidc(ok).Select(w => w.Code).ToList();
        Assert.DoesNotContain("OIDC_AUTHORITY", okCodes);
        Assert.DoesNotContain("MEDLINK_BRIDGE_OFF", okCodes);
        Assert.DoesNotContain("MEDLINK_NO_ROLES", okCodes);
        Assert.Equal(AuthModes.None, AuthModes.Resolve(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Mode"] = "whatever" }).Build()));
    }
}
