// Одиниці виміру: підбір/створення запису довідника за текстом, синхронізація текстового поля «одиниця» з довідником та перерахунок SI ↔ традиційні.
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public static class UnitCatalog
{
    public const string NoneCode = "NONE";

    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();

    /// <summary>
    /// Знаходить одиницю за назвою (без урахування регістру); порожній текст → «без одиниць» (NONE). Якщо одиниці немає — створює нову (код CUSTOM_n),
    /// щоб вільний текст, введений раніше, не губився. Збереження змін — на боці виклику.
    /// </summary>
    public static async Task<LabUnit> ResolveOrCreateAsync(LisDbContext db, string? text)
    {
        var key = Norm(text);
        var local = db.Units.Local.FirstOrDefault(u => u.RecordState != RecordStates.Deleted && Norm(u.Name) == key);
        if (local != null) return local;
        var found = (await db.Units.Where(u => u.RecordState != RecordStates.Deleted).ToListAsync()).FirstOrDefault(u => Norm(u.Name) == key);
        if (found != null) return found;
        var all = await db.Units.Select(u => new { u.Id, u.Code }).ToListAsync();
        var nextId = Math.Max(all.Count == 0 ? 0 : all.Max(u => u.Id), db.Units.Local.Count == 0 ? 0 : db.Units.Local.Max(u => u.Id)) + 1;
        var n = nextId;
        var code = $"CUSTOM_{n}";
        while (all.Any(u => u.Code == code) || db.Units.Local.Any(u => u.Code == code)) code = $"CUSTOM_{++n}";
        var unit = new LabUnit { Id = nextId, Code = code, Name = (text ?? "").Trim(), Dimension = "OTHER", SystemKind = "OTHER", SortOrder = 500, FullName = "Додано автоматично з наявного тексту" };
        db.Units.Add(unit);
        return unit;
    }

    /// <summary>
    /// Узгоджує UnitId і текст одиниці показника. Що змінилося порівняно з попереднім станом, те й головне: змінений UnitId → текст береться з довідника;
    /// змінений текст → UnitId визначається за текстом (нова одиниця створюється, ніщо не губиться). Для нового показника попередній стан — (null, "").
    /// </summary>
    public static async Task SyncTestAsync(LisDbContext db, LabTestDefinition test, int? prevUnitId, string? prevUnit)
    {
        var idChanged = test.UnitId != prevUnitId;
        var textChanged = Norm(test.Unit) != Norm(prevUnit);
        if (idChanged && test.UnitId.HasValue)
        {
            var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == test.UnitId.Value) ?? throw ValidationException.Field("unitId", "Одиницю виміру не знайдено");
            test.Unit = unit.Name;
        }
        else if (textChanged || !test.UnitId.HasValue)
        {
            var unit = await ResolveOrCreateAsync(db, test.Unit);
            test.UnitId = unit.Id;
            test.Unit = unit.Name;
        }
    }

    /// <summary>Те саме для шару норми; якщо нічого не задано — успадковується одиниця показника.</summary>
    public static async Task SyncLayerAsync(LisDbContext db, LabReferenceLayer layer, LabTestDefinition test)
    {
        if (layer.UnitId.HasValue)
        {
            var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == layer.UnitId.Value) ?? throw ValidationException.Field("unitId", "Одиницю виміру не знайдено");
            layer.Unit = unit.Name;
        }
        else if (layer.Unit != null && Norm(layer.Unit) != Norm(test.Unit))
        {
            var unit = await ResolveOrCreateAsync(db, layer.Unit);
            layer.UnitId = unit.Id; layer.Unit = unit.Name;
        }
        else
        {
            layer.UnitId = test.UnitId; layer.Unit = test.Unit;
        }
    }

    /// <summary>Проставляє UnitId там, де його ще немає (міграція вільного тексту без втрат). Повертає кількість змінених рядків.</summary>
    public static async Task<int> LinkAllAsync(LisDbContext db)
    {
        var changed = 0;
        foreach (var t in await db.Tests.Where(t => t.UnitId == null).ToListAsync())
        {
            var u = await ResolveOrCreateAsync(db, t.Unit);
            t.UnitId = u.Id; changed++;
            await db.SaveChangesAsync(); // кожна нова одиниця одразу отримує ключ, щоб наступний підбір її бачив
        }
        foreach (var l in await db.ReferenceLayers.Where(l => l.UnitId == null).ToListAsync())
        {
            var u = await ResolveOrCreateAsync(db, l.Unit);
            l.UnitId = u.Id; changed++;
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
        return changed;
    }

    /// <summary>Перерахунок значення показника між одиницями (за кодами або ідентифікаторами). Правило шукається в обидва боки.</summary>
    public static async Task<object> ConvertAsync(LisDbContext db, string testCode, double value, string from, string to)
    {
        var test = await db.Tests.AsNoTracking().FirstOrDefaultAsync(t => t.Code == testCode || t.Id == testCode) ?? throw NotFoundException.For("Показник", testCode);
        var fromUnit = await FindAsync(db, from) ?? throw ValidationException.Field("from", $"Одиницю '{from}' не знайдено");
        var toUnit = await FindAsync(db, to) ?? throw ValidationException.Field("to", $"Одиницю '{to}' не знайдено");
        if (fromUnit.Id == toUnit.Id) return new { testCode = test.Code, value, from = fromUnit.Code, to = toUnit.Code, result = value, factor = 1.0, offset = 0.0, direct = true };
        var rules = await db.UnitConversions.AsNoTracking().Where(c => c.TestCode == test.Code && c.IsActive).ToListAsync();
        var direct = rules.FirstOrDefault(c => c.FromUnitId == fromUnit.Id && c.ToUnitId == toUnit.Id);
        if (direct != null)
            return new { testCode = test.Code, value, from = fromUnit.Code, to = toUnit.Code, result = Round(value * direct.Factor + direct.Offset), factor = direct.Factor, offset = direct.Offset, direct = true, note = direct.Note };
        var rev = rules.FirstOrDefault(c => c.FromUnitId == toUnit.Id && c.ToUnitId == fromUnit.Id);
        if (rev != null && rev.Factor != 0)
            return new { testCode = test.Code, value, from = fromUnit.Code, to = toUnit.Code, result = Round((value - rev.Offset) / rev.Factor), factor = 1 / rev.Factor, offset = -rev.Offset / rev.Factor, direct = false, note = rev.Note };
        throw new ValidationException($"Для показника {test.Code} немає правила перерахунку з «{fromUnit.Name}» у «{toUnit.Name}»");
    }

    private static double Round(double v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);

    private static async Task<LabUnit?> FindAsync(LisDbContext db, string key)
    {
        var k = Norm(key);
        return await db.Units.AsNoTracking().FirstOrDefaultAsync(u => u.Id.ToString() == key || u.Code.ToLower() == k);
    }
}

/// <summary>Причини браку/відмови: приймає і код, і сумісний вільний текст (зберігається як є).</summary>
public static class ReasonCatalog
{
    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();

    /// <summary>Повертає (код, текст): якщо ввід збігається з кодом або назвою активної причини — назва з довідника та її код; інакше — ввід без змін і код null.</summary>
    public static async Task<(string? Code, string Text)> ResolveRejectionAsync(LisDbContext db, string? input)
    {
        var key = Norm(input);
        if (key.Length == 0) return (null, input ?? "");
        var list = await db.RejectionReasons.AsNoTracking().Where(r => r.IsActive && r.RecordState != RecordStates.Deleted).ToListAsync();
        var hit = list.FirstOrDefault(r => Norm(r.Code) == key) ?? list.FirstOrDefault(r => Norm(r.Name) == key);
        return hit == null ? (null, input!.Trim()) : (hit.Code, hit.Name);
    }

    public static async Task<(string? Code, string Text)> ResolveCancelAsync(LisDbContext db, string? input)
    {
        var key = Norm(input);
        if (key.Length == 0) return (null, input ?? "");
        var list = await db.CancelReasons.AsNoTracking().Where(r => r.IsActive && r.RecordState != RecordStates.Deleted).ToListAsync();
        var hit = list.FirstOrDefault(r => Norm(r.Code) == key) ?? list.FirstOrDefault(r => Norm(r.Name) == key);
        return hit == null ? (null, input!.Trim()) : (hit.Code, hit.Name);
    }
}
