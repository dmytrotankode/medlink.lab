// =============================================================================
// Дооновлення схеми SQLite без міграцій: EnsureCreated не змінює наявну БД, тому після нього
// створюємо відсутні таблиці (з індексами) і додаємо відсутні колонки за моделлю EF.
// Для перенесення в evomis/PostgreSQL ті самі зміни фіксуються SQL-скриптами в db/migrations.
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
        if (!db.Database.IsSqlite()) return applied;

        var tables = (await db.Database.SqlQueryRaw<string>("SELECT name AS \"Value\" FROM sqlite_master WHERE type = 'table'").ToListAsync())
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
            var columns = (await db.Database.SqlQueryRaw<string>($"SELECT name AS \"Value\" FROM pragma_table_info('{table}')").ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in entity.GetProperties())
            {
                var column = prop.GetColumnName(store);
                if (column == null || columns.Contains(column)) continue;
                var type = prop.GetColumnType();
                var sql = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}" + (prop.IsNullable ? "" : $" NOT NULL DEFAULT {DefaultLiteral(prop)}");
                await db.Database.ExecuteSqlRawAsync(sql);
                columns.Add(column);
                applied.Add(sql);
            }
        }
        return applied;
    }

    private static string DefaultLiteral(IProperty prop)
    {
        var clr = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
        var value = prop.GetDefaultValue() ?? (clr.IsValueType ? Activator.CreateInstance(clr) : null);
        return value switch
        {
            null => "''",
            bool b => b ? "1" : "0",
            string s => "'" + s.Replace("'", "''") + "'",
            DateTime d => "'" + d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'",
            IFormattable f when clr.IsPrimitive || clr == typeof(decimal) => f.ToString(null, CultureInfo.InvariantCulture),
            _ => "'" + Convert.ToString(value, CultureInfo.InvariantCulture) + "'"
        };
    }
}
