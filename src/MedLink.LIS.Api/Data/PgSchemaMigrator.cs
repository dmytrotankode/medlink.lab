// =============================================================================
// Версіонована схема PostgreSQL: упорядковані ідемпотентні SQL-скрипти db/postgres/upgrades/NNNN_назва.sql
// (вбудовані в збірку) + журнал lab_schema_version. Доповнює SchemaUpgrader (той лише додає відсутні таблиці/колонки за EF-моделлю):
// усе, що EF не вміє або що небезпечно робити автоматично (індекси-вирази, trigram, зміна типів, перейменування,
// виправлення даних), оформлюється як окремий скрипт з наступним номером.
// Гарантії: один екземпляр за раз (advisory lock), кожен скрипт — у власній транзакції разом із записом у журнал,
// зміна вже застосованого скрипта виявляється за контрольною сумою (попередження), скрипти застосовуються строго зростаючи.
// Скрипт, якому потрібне виконання поза транзакцією (CREATE INDEX CONCURRENTLY), позначається першим рядком «-- no-transaction».
// =============================================================================
using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MedLink.LIS.Api.Data;

public static class PgSchemaMigrator
{
    public const string ResourcePrefix = "MedLink.LIS.Api.PgUpgrades.";
    /// <summary>Ключ advisory lock (довільне фіксоване число, унікальне для ЛІС).</summary>
    private const long LockKey = 7_270_015_001;

    public sealed record UpgradeScript(int Version, string Name, string Sql, string Checksum, bool Transactional);

    public static IReadOnlyList<UpgradeScript> LoadScripts(Assembly? assembly = null)
    {
        assembly ??= typeof(PgSchemaMigrator).Assembly;
        var list = new List<UpgradeScript>();
        foreach (var res in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)))
        {
            var file = res[ResourcePrefix.Length..];
            var m = Regex.Match(file, @"^(\d{4})_(.+)\.sql$");
            if (!m.Success) throw new InvalidOperationException($"Скрипт оновлення схеми має ім'я NNNN_назва.sql: {file}");
            using var stream = assembly.GetManifestResourceStream(res)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var sql = reader.ReadToEnd().Replace("\r\n", "\n");
            var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)))[..16];
            list.Add(new UpgradeScript(int.Parse(m.Groups[1].Value), m.Groups[2].Value, sql, checksum, !sql.TrimStart().StartsWith("-- no-transaction", StringComparison.OrdinalIgnoreCase)));
        }
        var ordered = list.OrderBy(s => s.Version).ToList();
        var dup = ordered.GroupBy(s => s.Version).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new InvalidOperationException($"Дубль номера скрипта оновлення схеми: {dup.Key:0000}");
        return ordered;
    }

    /// <summary>Застосовує нові скрипти. Повертає перелік застосованих («0002_name»). Попередження про змінені скрипти — у <paramref name="warnings"/>.</summary>
    public static async Task<List<string>> ApplyAsync(LisDbContext db, ICollection<string>? warnings = null, CancellationToken ct = default)
    {
        var applied = new List<string>();
        if (!db.Database.IsNpgsql()) return applied;
        var scripts = LoadScripts();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var conn = (NpgsqlConnection)db.Database.GetDbConnection();
            await Exec(conn, null, $"SELECT pg_advisory_lock({LockKey})", ct);
            // Власна схема (Database:Schema ≠ public): скриптам потрібні розширення з public (gin_trgm_ops), тож на час виконання
            // search_path = «власна, public». Таблиці ЛІС існують у власній схемі, тому однойменні таблиці МІС не зачіпаються.
            string? ownSchema = null;
            await using (var cs = new NpgsqlCommand("SELECT current_schema()", conn)) ownSchema = (string?)await cs.ExecuteScalarAsync(ct);
            var widened = !string.IsNullOrEmpty(ownSchema) && ownSchema != "public";
            if (widened) await Exec(conn, null, $"SELECT set_config('search_path', '{ownSchema}, public', false)", ct);
            try
            {
                await Exec(conn, null, @"CREATE TABLE IF NOT EXISTS lab_schema_version (
                    version integer NOT NULL, name varchar(128) NOT NULL, checksum varchar(32) NOT NULL,
                    applied_at timestamp without time zone NOT NULL DEFAULT (now() at time zone 'utc'), duration_ms integer NOT NULL DEFAULT 0,
                    CONSTRAINT pk_lab_schema_version PRIMARY KEY (version))", ct);
                var done = new Dictionary<int, string>();
                await using (var cmd = new NpgsqlCommand("SELECT version, checksum FROM lab_schema_version", conn))
                await using (var r = await cmd.ExecuteReaderAsync(ct))
                    while (await r.ReadAsync(ct)) done[r.GetInt32(0)] = r.GetString(1);

                foreach (var s in scripts)
                {
                    if (done.TryGetValue(s.Version, out var sum))
                    {
                        if (sum != s.Checksum) warnings?.Add($"Скрипт {s.Version:0000}_{s.Name} змінено після застосування (було {sum}, є {s.Checksum}); зміни не застосовуються — додайте новий скрипт.");
                        continue;
                    }
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    NpgsqlTransaction? tx = s.Transactional ? await conn.BeginTransactionAsync(ct) : null;
                    try
                    {
                        await Exec(conn, tx, s.Sql, ct);
                        var insert = new NpgsqlCommand("INSERT INTO lab_schema_version (version, name, checksum, duration_ms) VALUES (@v, @n, @c, @d)", conn, tx);
                        insert.Parameters.AddWithValue("v", s.Version);
                        insert.Parameters.AddWithValue("n", s.Name);
                        insert.Parameters.AddWithValue("c", s.Checksum);
                        insert.Parameters.AddWithValue("d", (int)sw.ElapsedMilliseconds);
                        await insert.ExecuteNonQueryAsync(ct);
                        if (tx != null) await tx.CommitAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        if (tx != null) await tx.RollbackAsync(ct);
                        throw new InvalidOperationException($"Не вдалося застосувати скрипт схеми {s.Version:0000}_{s.Name}: {ex.Message}", ex);
                    }
                    finally { if (tx != null) await tx.DisposeAsync(); }
                    applied.Add($"{s.Version:0000}_{s.Name}");
                }
            }
            finally
            {
                if (widened) await Exec(conn, null, $"SELECT set_config('search_path', '{ownSchema}', false)", CancellationToken.None);
                await Exec(conn, null, $"SELECT pg_advisory_unlock({LockKey})", CancellationToken.None);
            }
        }
        finally { await db.Database.CloseConnectionAsync(); }
        return applied;
    }

    private static async Task Exec(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx) { CommandTimeout = 600 };
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
