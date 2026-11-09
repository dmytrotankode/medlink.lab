// Джерела користувачів: конфіг (Sim:Users) або база МІС (лише SELECT: cmn_person.user_id, org_employee, sys_user_profile → sys_profile).
using Npgsql;

namespace MedLink.IdentitySim;

public interface IUserStore
{
    Task<IReadOnlyList<SimUser>> ListAsync(CancellationToken ct = default);
    Task<SimUser?> FindAsync(string sub, CancellationToken ct = default);
}

public sealed class ConfigUserStore : IUserStore
{
    private readonly SimOptions _o;
    public ConfigUserStore(SimOptions o) => _o = o;
    public Task<IReadOnlyList<SimUser>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SimUser>>(_o.Users);
    public Task<SimUser?> FindAsync(string sub, CancellationToken ct = default) => Task.FromResult(_o.Users.FirstOrDefault(u => u.Sub == sub));
}

/// <summary>Користувачі стенду з бази МІС. Запит лише читає; активні (record_state=2) особа, працівник, зв'язок і профіль; працівник не заблокований.</summary>
public sealed class MedLinkUserStore : IUserStore
{
    // Профілі безпеки належать працівнику (sys_user_profile.user_id = org_employee.id); sub токена = cmn_person.user_id
    private const string Sql = """
        select p.user_id, p.last_name, p.name, p.middle_name, sp.id::text, sp.caption, sp.type_id::text
        from cmn_person p
        join org_employee e on e.person_id = p.id and e.record_state = 2 and coalesce(e.is_locked, false) = false
        join sys_user_profile up on up.user_id = e.id and up.record_state = 2
        join sys_profile sp on sp.id = up.profile_id and sp.record_state = 2
        where p.record_state = 2 and p.user_id is not null and p.user_id <> ''
        order by p.last_name, p.name, p.middle_name, sp.caption
        """;

    private readonly string _cs;
    private readonly TimeProvider _time;
    private IReadOnlyList<SimUser>? _cache;
    private DateTimeOffset _loaded;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public MedLinkUserStore(string connectionString, TimeProvider time) { _cs = connectionString; _time = time; }

    public async Task<IReadOnlyList<SimUser>> ListAsync(CancellationToken ct = default)
    {
        if (_cache != null && _time.GetUtcNow() - _loaded < TimeSpan.FromSeconds(60)) return _cache;
        await _lock.WaitAsync(ct);
        try
        {
            if (_cache != null && _time.GetUtcNow() - _loaded < TimeSpan.FromSeconds(60)) return _cache;
            var map = new Dictionary<string, SimUser>();
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(Sql, conn) { CommandTimeout = 20 };
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var sub = r.GetString(0);
                if (!map.TryGetValue(sub, out var u))
                {
                    var fio = string.Join(' ', new[] { Str(r, 1), Str(r, 2), Str(r, 3) }.Where(s => s.Length > 0));
                    map[sub] = u = new SimUser { Sub = sub, Name = fio.Length > 0 ? fio : sub };
                }
                var pid = r.GetString(4);
                if (u.Profiles.All(x => x.Id != pid)) u.Profiles.Add(new SimProfile(pid, r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6)));
            }
            _cache = map.Values.ToList();
            _loaded = _time.GetUtcNow();
            return _cache;
        }
        finally { _lock.Release(); }
    }

    public async Task<SimUser?> FindAsync(string sub, CancellationToken ct = default) => (await ListAsync(ct)).FirstOrDefault(u => u.Sub == sub);
    private static string Str(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i).Trim();
}
