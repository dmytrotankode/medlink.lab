// Міст до сутностей MedLink (evomis): читання лише зі схеми public за повними іменами (окреме підключення Medlink:ConnectionString).
// Таблиці МІС не змінюються: жодного INSERT/UPDATE/DELETE у public. Власні дані ЛІС живуть у схемі lab (Database:Schema).
using System.Data.Common;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MedLink.LIS.Api.Services.MedLinkBridge;

public sealed class MedLinkOptions
{
    public const string Section = "Medlink";
    /// <summary>Підключення (окрема роль лише з SELECT) до бази МІС. Порожньо — міст вимкнено.</summary>
    public string? ConnectionString { get; set; }
    /// <summary>Схема таблиць МІС.</summary>
    public string Schema { get; set; } = "public";
    /// <summary>Обмежити працівників однією організацією МІС (id). Порожньо — будь-яка організація працівника.</summary>
    public string? OrganizationId { get; set; }
    /// <summary>Роль ЛІС для працівника МІС, профіль якого не знайдено в <see cref="ProfileRoles"/>. Порожньо — доступу немає (403).</summary>
    public string? DefaultLabRole { get; set; }
    /// <summary>Відповідність «профіль безпеки МІС → роль ЛІС» (значення з Domain.LabRoles.All).</summary>
    public Dictionary<string, string> ProfileRoles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Скільки секунд тримати розв'язаного працівника в пам'яті (зміна профілю в МІС діє з такою затримкою).</summary>
    public int CacheSeconds { get; set; } = 60;
    public bool Enabled => !string.IsNullOrWhiteSpace(ConnectionString);
}

public sealed record MedLinkEmployee(string Id, string PersonId, string OrganizationId, string? DepartmentId, string? Caption, string? OrganizationName, string? DepartmentName);
public sealed record MedLinkPerson(string Id, string? UserId, string? LastName, string? Name, string? MiddleName, DateTime? Birthday, string? Phone, string? Email, string? Ipn);
public sealed record MedLinkPatient(string CardId, string PersonId, string OrganizationId, string? RegNumber, string? Caption, DateTime? Birthday, string? Phone, string Sex);
public sealed record MedLinkDepartment(string Id, string OrganizationId, string? Caption, string? Code);
public sealed record MedLinkProfile(string UserId, string ProfileId, string? Caption);
public sealed record MedLinkOrganization(string Id, string? Caption);

public interface IMedLinkDirectory
{
    bool Enabled { get; }
    Task<MedLinkPerson?> PersonByIdentityUserAsync(string identityUserId, CancellationToken ct = default);
    Task<IReadOnlyList<MedLinkEmployee>> EmployeesOfPersonAsync(string personId, CancellationToken ct = default);
    Task<IReadOnlyList<MedLinkProfile>> ProfilesOfEmployeesAsync(IReadOnlyCollection<string> employeeIds, CancellationToken ct = default);
    Task<IReadOnlyList<MedLinkDepartment>> DepartmentsAsync(string organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<MedLinkPatient>> SearchPatientsAsync(string organizationId, string text, int take = 20, CancellationToken ct = default);
    Task<MedLinkPatient?> PatientAsync(string cardId, CancellationToken ct = default);
    Task<MedLinkPerson?> PersonAsync(string personId, CancellationToken ct = default);
    Task<MedLinkOrganization?> OrganizationAsync(string organizationId, CancellationToken ct = default);
}

/// <summary>Запити до public.*: параметризовані, лише читання. Роль застосунку потребує SELECT на перелічені таблиці.</summary>
public sealed class MedLinkDirectory : IMedLinkDirectory
{
    private readonly string _connectionString;
    private readonly string _schema;
    public bool Enabled { get; }

    public MedLinkDirectory(Microsoft.Extensions.Options.IOptions<MedLinkOptions> options)
    {
        var o = options.Value;
        Enabled = o.Enabled;
        _schema = NormalizeSchema(o.Schema);
        _connectionString = Enabled ? new NpgsqlConnectionStringBuilder(o.ConnectionString) { SearchPath = _schema, ApplicationName = "medlink-lab-bridge" }.ConnectionString : "";
    }

    private static string NormalizeSchema(string? schema)
    {
        var s = string.IsNullOrWhiteSpace(schema) ? "public" : schema.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(s, "^[a-z_][a-z0-9_]*$")) throw new InvalidOperationException($"Некоректна назва схеми MedLink: {s}");
        return s;
    }

    private string T(string table) => $"{_schema}.{table}";

    private async Task<List<T>> QueryAsync<T>(string sql, Action<NpgsqlCommand> bind, Func<DbDataReader, T> map, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<T>();
        while (await reader.ReadAsync(ct)) rows.Add(map(reader));
        return rows;
    }

    private static string? Str(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetValue(i).ToString();
    private static DateTime? Date(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDateTime(i);
    private static Guid G(string value) => Guid.Parse(value);

    private const string PersonColumns = "id, user_id, last_name, name, middle_name, birthday, phone, email, ipn";
    private static MedLinkPerson MapPerson(DbDataReader r) => new(r.GetGuid(0).ToString(), Str(r, 1), Str(r, 2), Str(r, 3), Str(r, 4), Date(r, 5), Str(r, 6), Str(r, 7), Str(r, 8));

    public async Task<MedLinkPerson?> PersonByIdentityUserAsync(string identityUserId, CancellationToken ct = default) =>
        (await QueryAsync($"SELECT {PersonColumns} FROM {T("cmn_person")} WHERE user_id::text = @id AND record_state = 2 LIMIT 1", c => c.Parameters.AddWithValue("id", identityUserId), MapPerson, ct)).FirstOrDefault();

    public async Task<MedLinkOrganization?> OrganizationAsync(string organizationId, CancellationToken ct = default) =>
        (await QueryAsync($"SELECT id, caption FROM {T("org_organization")} WHERE id = @id LIMIT 1", c => c.Parameters.AddWithValue("id", G(organizationId)),
            r => new MedLinkOrganization(r.GetGuid(0).ToString(), Str(r, 1)), ct)).FirstOrDefault();

    public async Task<MedLinkPerson?> PersonAsync(string personId, CancellationToken ct = default) =>
        (await QueryAsync($"SELECT {PersonColumns} FROM {T("cmn_person")} WHERE id = @id LIMIT 1", c => c.Parameters.AddWithValue("id", G(personId)), MapPerson, ct)).FirstOrDefault();

    public async Task<IReadOnlyList<MedLinkEmployee>> EmployeesOfPersonAsync(string personId, CancellationToken ct = default) =>
        await QueryAsync($@"SELECT e.id, e.person_id, e.organization_id, e.department_id, e.caption, o.caption, d.caption
                            FROM {T("org_employee")} e
                            JOIN {T("org_organization")} o ON o.id = e.organization_id
                            LEFT JOIN {T("org_department")} d ON d.id = e.department_id
                            WHERE e.person_id = @p AND e.record_state = 2 AND coalesce(e.is_locked, false) = false",
            c => c.Parameters.AddWithValue("p", G(personId)),
            r => new MedLinkEmployee(r.GetGuid(0).ToString(), r.GetGuid(1).ToString(), r.GetGuid(2).ToString(), Str(r, 3), Str(r, 4), Str(r, 5), Str(r, 6)), ct);

    public async Task<IReadOnlyList<MedLinkProfile>> ProfilesOfEmployeesAsync(IReadOnlyCollection<string> employeeIds, CancellationToken ct = default)
    {
        if (employeeIds.Count == 0) return Array.Empty<MedLinkProfile>();
        var ids = employeeIds.Select(G).ToArray();
        return await QueryAsync($@"SELECT u.user_id, u.profile_id, p.caption FROM {T("sys_user_profile")} u
                                   JOIN {T("sys_profile")} p ON p.id = u.profile_id
                                   WHERE u.user_id = ANY(@ids) AND u.record_state = 2 AND p.record_state = 2",
            c => c.Parameters.AddWithValue("ids", ids),
            r => new MedLinkProfile(r.GetGuid(0).ToString(), r.GetGuid(1).ToString(), Str(r, 2)), ct);
    }

    public async Task<IReadOnlyList<MedLinkDepartment>> DepartmentsAsync(string organizationId, CancellationToken ct = default) =>
        await QueryAsync($"SELECT id, organization_id, caption, code FROM {T("org_department")} WHERE organization_id = @o AND record_state = 2 ORDER BY caption",
            c => c.Parameters.AddWithValue("o", G(organizationId)),
            r => new MedLinkDepartment(r.GetGuid(0).ToString(), r.GetGuid(1).ToString(), Str(r, 2), Str(r, 3)), ct);

    // Стать: перелік Gender у cmn_enum_record; у картці вона може бути порожньою, тоді береться з особи.
    private string PatientSelect => $@"SELECT c.id, c.person_id, c.organization_id, c.reg_number, c.caption, coalesce(c.birthday, p.birthday), p.phone, g.code, g.caption
        FROM {T("mis_patient_card")} c JOIN {T("cmn_person")} p ON p.id = c.person_id
        LEFT JOIN {T("cmn_enum_record")} g ON g.id = coalesce(c.gender_id, p.gender_id) WHERE c.record_state = 2 ";
    private static MedLinkPatient MapPatient(DbDataReader r) => new(r.GetGuid(0).ToString(), r.GetGuid(1).ToString(), r.GetGuid(2).ToString(), Str(r, 3), Str(r, 4), Date(r, 5), Str(r, 6), SexFrom(Str(r, 7), Str(r, 8)));

    /// <summary>Значення переліку Gender evomis → OrdPatient.Sex. Коди M/F, назви «Чоловік»/«Жінка»; решта (зокрема «Невідомий») — unknown.</summary>
    public static string SexFrom(string? code, string? caption)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        if (c is "M" or "MALE") return "male";
        if (c is "F" or "FEMALE") return "female";
        var name = (caption ?? "").Trim();
        if (name.StartsWith("Чолов", StringComparison.OrdinalIgnoreCase)) return "male";
        if (name.StartsWith("Жін", StringComparison.OrdinalIgnoreCase)) return "female";
        return "unknown";
    }

    public async Task<IReadOnlyList<MedLinkPatient>> SearchPatientsAsync(string organizationId, string text, int take = 20, CancellationToken ct = default)
    {
        var like = "%" + text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        return await QueryAsync(PatientSelect +
                                "AND c.organization_id = @o AND (c.caption ILIKE @q OR c.reg_number ILIKE @q OR p.phone ILIKE @q OR p.ipn ILIKE @q) ORDER BY c.caption LIMIT @take",
            c => { c.Parameters.AddWithValue("o", G(organizationId)); c.Parameters.AddWithValue("q", like); c.Parameters.AddWithValue("take", Math.Clamp(take, 1, 100)); }, MapPatient, ct);
    }

    public async Task<MedLinkPatient?> PatientAsync(string cardId, CancellationToken ct = default) =>
        (await QueryAsync(PatientSelect + "AND c.id = @id", c => c.Parameters.AddWithValue("id", G(cardId)), MapPatient, ct)).FirstOrDefault();
}
