// =============================================================================
// Генератор DDL PostgreSQL для перенесення ЛІС у MedLink (evomis) — з тієї самої EF-моделі, що й SQLite.
//  • [MedLink]-таблиці (cmn_*, org_*, mis_*, ehe_*) не створюються: вони вже є в evomis; у скрипті — перелік
//    колонок, які використовує ЛІС, та додаткові колонки [MedLink+] (ALTER TABLE … ADD COLUMN IF NOT EXISTS).
//  • [ЛІС]-таблиці (lab_*) — CREATE TABLE IF NOT EXISTS з типами evomis: ключі та посилання — uuid,
//    дати — timestamp without time zone (Npgsql legacy timestamp, як в evomis), службові колонки created_by/modified_by — uuid.
// Запуск: dotnet run --project src/MedLink.LIS.Api -- --export-pg-ddl db/postgres/medlink_lis_schema.sql
// =============================================================================
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MedLink.LIS.Api.Data;

public static class PostgresDdlGenerator
{
    /// <summary>Колонки [MedLink+], які ЛІС додає до таблиць evomis.</summary>
    public static readonly Dictionary<string, string[]> MedLinkExtensions = new()
    {
        ["mis_patient_card"] = new[] { "last_name_latin", "first_name_latin" }
    };

    /// <summary>Рядкові *Id, що не є uuid (вільні ідентифікатори журналів, приладів).</summary>
    private static readonly HashSet<string> NonUuidIds = new(StringComparer.OrdinalIgnoreCase) { "UserId", "EntityId", "InstrumentId", "OperatorId", "ActorId", "ById" };

    /// <summary>[MedLink-new] — нові загальні сутності МІС, які пропонується додати до ядра MedLink (створюються скриптом).</summary>
    public static readonly HashSet<string> MedLinkNewTables = new() { "mis_patient_insurance" };

    public static bool IsMedLinkTable(string table) => !MedLinkNewTables.Contains(table) &&
        (table.StartsWith("cmn_") || table.StartsWith("org_") || table.StartsWith("mis_") || table.StartsWith("ehe_"));

    public static string Generate(LisDbContext db)
    {
        var model = db.Model;
        var sb = new StringBuilder();
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("-- MedLink LIS — схема модуля «Лабораторія» для PostgreSQL (evomis). Згенеровано з EF-моделі ЛІС.");
        sb.AppendLine($"-- Дата генерації: {DateTime.UtcNow:yyyy-MM-dd}. Не редагувати вручну — перегенерувати: --export-pg-ddl.");
        sb.AppendLine("-- [MedLink]  — таблиця evomis, не створюється (наведено колонки, які використовує ЛІС).");
        sb.AppendLine("-- [MedLink+] — колонки, які ЛІС додає до таблиці evomis.");
        sb.AppendLine("-- [ЛІС]      — таблиця модуля, створюється цим скриптом.");
        sb.AppendLine("-- [MedLink-new] — нова загальна сутність МІС (пропонується до ядра MedLink), створюється цим скриптом.");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("BEGIN;");
        sb.AppendLine();

        var entities = model.GetEntityTypes().Where(e => e.GetTableName() != null).OrderBy(e => e.GetTableName()).ToList();

        sb.AppendLine("-- ----------------------------------------------------------------------------- [MedLink]");
        foreach (var e in entities.Where(e => IsMedLinkTable(e.GetTableName()!)))
        {
            var table = e.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, null);
            sb.AppendLine($"-- [MedLink] {table}: {string.Join(", ", e.GetProperties().Select(p => p.GetColumnName(store)).Where(c => c != null && !(MedLinkExtensions.TryGetValue(table, out var ext) && ext.Contains(c))))}");
            if (MedLinkExtensions.TryGetValue(table, out var extra))
                foreach (var col in extra)
                {
                    var prop = e.GetProperties().First(p => p.GetColumnName(store) == col);
                    sb.AppendLine($"ALTER TABLE {table} ADD COLUMN IF NOT EXISTS {col} {PgType(prop)}{(prop.IsNullable ? "" : " NOT NULL DEFAULT " + Default(prop))}; -- [MedLink+]");
                }
        }
        sb.AppendLine();

        foreach (var e in entities.Where(e => !IsMedLinkTable(e.GetTableName()!)))
        {
            var table = e.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, null);
            sb.AppendLine($"-- [{(MedLinkNewTables.Contains(table) ? "MedLink-new" : "ЛІС")}] {table} ({e.ClrType.Name})");
            sb.AppendLine($"CREATE TABLE IF NOT EXISTS {table} (");
            var lines = new List<string>();
            foreach (var p in e.GetProperties())
            {
                var col = p.GetColumnName(store)!;
                var def = !p.IsNullable && (col is "record_state") ? " DEFAULT 2" : "";
                lines.Add($"    {col} {PgType(p)}{(p.IsNullable ? "" : " NOT NULL")}{def}");
            }
            var pk = e.FindPrimaryKey();
            if (pk != null) lines.Add($"    CONSTRAINT pk_{table} PRIMARY KEY ({string.Join(", ", pk.Properties.Select(p => p.GetColumnName(store)))})");
            sb.AppendLine(string.Join(",\n", lines));
            sb.AppendLine(");");
            foreach (var ix in e.GetIndexes())
                sb.AppendLine($"CREATE {(ix.IsUnique ? "UNIQUE " : "")}INDEX IF NOT EXISTS {ix.GetDatabaseName()} ON {table} ({string.Join(", ", ix.Properties.Select(p => p.GetColumnName(store)))});");
            sb.AppendLine();
        }

        sb.AppendLine("-- ----------------------------------------------------------------------------- зовнішні ключі");
        foreach (var e in entities.Where(e => !IsMedLinkTable(e.GetTableName()!)))
        {
            var table = e.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, null);
            foreach (var fk in e.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType.GetTableName()!;
                var pstore = StoreObjectIdentifier.Table(principal, null);
                var name = fk.GetConstraintName() ?? $"fk_{table}_{principal}";
                var onDelete = fk.DeleteBehavior switch { DeleteBehavior.Cascade => " ON DELETE CASCADE", DeleteBehavior.SetNull => " ON DELETE SET NULL", _ => "" };
                sb.AppendLine($"DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = '{name}') THEN " +
                              $"ALTER TABLE {table} ADD CONSTRAINT {name} FOREIGN KEY ({string.Join(", ", fk.Properties.Select(p => p.GetColumnName(store)))}) " +
                              $"REFERENCES {principal} ({string.Join(", ", fk.PrincipalKey.Properties.Select(p => p.GetColumnName(pstore)))}){onDelete}; END IF; END $$;{(IsMedLinkTable(principal) ? " -- → [MedLink]" : "")}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("COMMIT;");
        return sb.ToString();
    }

    public static string PgType(IProperty p)
    {
        var clr = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
        if (clr == typeof(string))
        {
            var max = p.GetMaxLength();
            var isId = p.IsPrimaryKey() || p.IsForeignKey() || ((p.Name.EndsWith("Id") || p.Name is "CreatedBy" or "ModifiedBy") && !NonUuidIds.Contains(p.Name));
            if (isId && max == 64) return "uuid";
            return max.HasValue ? $"varchar({max})" : "text";
        }
        if (clr == typeof(int)) return "integer";
        if (clr == typeof(long)) return "bigint";
        if (clr == typeof(short)) return "smallint";
        if (clr == typeof(bool)) return "boolean";
        if (clr == typeof(double)) return "double precision";
        if (clr == typeof(float)) return "real";
        if (clr == typeof(decimal)) return "numeric(18,2)";
        if (clr == typeof(DateTime)) return "timestamp without time zone";
        if (clr == typeof(byte[])) return "bytea";
        return "text";
    }

    private static string Default(IProperty p)
    {
        var clr = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
        return clr == typeof(bool) ? "false" : clr == typeof(string) ? "''" : "0";
    }
}
