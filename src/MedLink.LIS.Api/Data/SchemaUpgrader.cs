// =============================================================================
// Адитивне дооновлення схеми за EF-моделлю (SQLite і PostgreSQL): EnsureCreated не змінює наявну БД, тому після нього
// створюємо відсутні таблиці (з індексами), додаємо відсутні колонки та відсутні індекси наявних таблиць.
// Лише додавання — ніколи не видаляє/не змінює типи (безпечно для production). Усе інше (зміна типів, перейменування,
// індекси-вирази, виправлення даних) — версіоновані скрипти PgSchemaMigrator (db/postgres/upgrades); розбіжності типів
// між моделлю і БД лише повідомляються (DetectDriftAsync). Для evomis ті самі зміни фіксуються в db/migrations.
// =============================================================================
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MedLink.LIS.Api.Data;

public static class SchemaUpgrader
{
    /// <summary>
    /// БД версії до вирівнювання з MedLink (v4.0: is_deleted замість record_state, без cmn_person) несумісна структурно.
    /// Файл перейменовується в *.pre-medlink-&lt;час&gt;.bak (дані не втрачаються), далі створюється нова БД. Повертає шлях копії або null.
    /// </summary>
    public static async Task<string?> BackupIfIncompatibleAsync(LisDbContext db)
    {
        if (!db.Database.IsSqlite()) return null;
        var path = db.Database.GetDbConnection().DataSource;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        var tables = (await db.Database.SqlQueryRaw<string>("SELECT name AS \"Value\" FROM sqlite_master WHERE type = 'table'").ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!tables.Contains("lab_order")) return null;
        var orderColumns = await db.Database.SqlQueryRaw<string>("SELECT name AS \"Value\" FROM pragma_table_info('lab_order')").ToListAsync();
        var legacy = !tables.Contains("cmn_person") || orderColumns.Contains("is_deleted", StringComparer.OrdinalIgnoreCase);
        if (!legacy) return null;
        await db.Database.CloseConnectionAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var backup = $"{path}.pre-medlink-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
        File.Move(path, backup);
        foreach (var suffix in new[] { "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Move(path + suffix, backup + suffix);
        return backup;
    }

    public static async Task<List<string>> UpgradeAsync(LisDbContext db)
    {
        var applied = new List<string>();
        var pg = db.Database.IsNpgsql();
        if (!db.Database.IsSqlite() && !pg) return applied;

        var tables = (await db.Database.SqlQueryRaw<string>(pg
                ? "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = current_schema()"
                : "SELECT name AS \"Value\" FROM sqlite_master WHERE type = 'table'").ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1) Відсутні таблиці — інструкції з повного скрипта створення, що стосуються лише їх
        var missing = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Where(t => t != null && !tables.Contains(t)).Select(t => t!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (missing.Count > 0)
        {
            var statements = Regex.Split(db.Database.GenerateCreateScript(), @";\s*(?:\r?\n|$)").Select(s => s.Trim()).Where(s => s.Length > 0);
            foreach (var sql in statements)
            {
                var m = Regex.Match(sql, @"^CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX\s+""[^""]+""\s+ON)\s+""([^""]+)""", RegexOptions.IgnoreCase);
                if (!m.Success || !missing.Contains(m.Groups[1].Value)) continue;
                await db.Database.ExecuteSqlRawAsync(sql);
                applied.Add(sql.Split('\n')[0]);
            }
            foreach (var t in missing) tables.Add(t);
        }

        // 2) Відсутні колонки наявних таблиць
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table == null || missing.Contains(table)) continue;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var columns = (await db.Database.SqlQueryRaw<string>(pg
                    ? $"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = '{table}'"
                    : $"SELECT name AS \"Value\" FROM pragma_table_info('{table}')").ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in entity.GetProperties())
            {
                var column = prop.GetColumnName(store);
                if (column == null || columns.Contains(column)) continue;
                var type = prop.GetColumnType();
                var sql = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}" + (prop.IsNullable ? "" : $" NOT NULL DEFAULT {DefaultLiteral(prop, pg)}");
                await db.Database.ExecuteSqlRawAsync(sql);
                columns.Add(column);
                applied.Add(sql);
            }
        }

        // 3) Відсутні індекси наявних таблиць (нові HasIndex у моделі EF після першого створення БД самі не з'являються)
        var existingIndexes = (await db.Database.SqlQueryRaw<string>(pg
                ? "SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = current_schema()"
                : "SELECT name AS \"Value\" FROM sqlite_master WHERE type = 'index'").ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var sql in Regex.Split(db.Database.GenerateCreateScript(), @";\s*(?:\r?\n|$)").Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            var m = Regex.Match(sql, @"^CREATE\s+(?:UNIQUE\s+)?INDEX\s+""([^""]+)""\s+ON\s+""([^""]+)""", RegexOptions.IgnoreCase);
            var indexName = m.Groups[1].Value;
            if (pg && indexName.Length > 63) indexName = indexName[..63]; // PostgreSQL мовчки обрізає ідентифікатори до 63 байт
            if (!m.Success || missing.Contains(m.Groups[2].Value) || existingIndexes.Contains(indexName)) continue;
            try { await db.Database.ExecuteSqlRawAsync(sql); applied.Add(sql.Split('\n')[0]); }
            catch (Exception ex) { applied.Add($"ПОМИЛКА створення індексу {m.Groups[1].Value}: {ex.Message.Split('\n')[0]}"); }
        }
        return applied;
    }

    /// <summary>
    /// PostgreSQL: розбіжності типів колонок між EF-моделлю та БД (довжина рядка, numeric, timestamp тощо). Нічого не змінює —
    /// зміну типу оформлюють окремим скриптом db/postgres/upgrades. Повертає рядки «таблиця.колонка: у БД X, у моделі Y».
    /// </summary>
    public static async Task<List<string>> DetectDriftAsync(LisDbContext db)
    {
        var drift = new List<string>();
        if (!db.Database.IsNpgsql()) return drift;
        var rows = await db.Database.SqlQueryRaw<ColumnTypeRow>(
            @"SELECT c.relname AS ""Tbl"", a.attname AS ""Col"", format_type(a.atttypid, a.atttypmod) AS ""Typ""
              FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
              WHERE n.nspname = current_schema() AND c.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped").ToListAsync();
        var actual = rows.ToDictionary(r => (r.Tbl.ToLowerInvariant(), r.Col.ToLowerInvariant()), r => r.Typ);
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table == null) continue;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var prop in entity.GetProperties())
            {
                var column = prop.GetColumnName(store);
                if (column == null || !actual.TryGetValue((table.ToLowerInvariant(), column.ToLowerInvariant()), out var have)) continue;
                var want = prop.GetColumnType();
                if (!string.Equals(have, want, StringComparison.OrdinalIgnoreCase)) drift.Add($"{table}.{column}: у БД {have}, у моделі {want}");
            }
        }
        return drift;
    }

    public sealed class ColumnTypeRow { public string Tbl { get; set; } = ""; public string Col { get; set; } = ""; public string Typ { get; set; } = ""; }

    private static string DefaultLiteral(IProperty prop, bool pg)
    {
        var clr = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
        var value = prop.GetDefaultValue() ?? (clr.IsValueType ? Activator.CreateInstance(clr) : null);
        return value switch
        {
            null => "''",
            bool b => pg ? (b ? "true" : "false") : (b ? "1" : "0"),
            string s => "'" + s.Replace("'", "''") + "'",
            DateTime d => "'" + d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'",
            IFormattable f when clr.IsPrimitive || clr == typeof(decimal) => f.ToString(null, CultureInfo.InvariantCulture),
            _ => "'" + Convert.ToString(value, CultureInfo.InvariantCulture) + "'"
        };
    }
}
