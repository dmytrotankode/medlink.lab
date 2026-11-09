// Підготовка власної схеми ЛІС у спільній базі МІС (Database:Schema, напр. lab).
// EnsureCreated тут не годиться: база вже існує й містить таблиці МІС у public, тож EF вважає її створеною й нічого не робить.
// Схема створюється (якщо роль має право CREATE), а при порожній схемі таблиці створює EF; решту (нові таблиці/колонки) додає SchemaUpgrader.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace MedLink.LIS.Api.Data;

public static class LabSchemaBootstrap
{
    public static async Task EnsureAsync(LisDbContext db, string schema)
    {
        if (!db.Database.IsNpgsql()) return;
        var name = PgConnectionOptions.NormalizeSchema(schema) ?? throw new ArgumentException("Схема не задана", nameof(schema));
        // Ім'я перевірено регулярним виразом у NormalizeSchema (лише [a-z0-9_]); у лапках на випадок службових слів
        await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{name}\"");
        var tables = await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema = current_schema()").ToListAsync();
        if (tables.FirstOrDefault() == 0)
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }
}
