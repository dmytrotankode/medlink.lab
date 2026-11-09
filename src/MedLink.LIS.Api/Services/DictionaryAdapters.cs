// Реєстр «простих» довідників для універсального CRUD /dictionaries/{name}: довідники патоморфології,
// синоніми показників, виробничий календар, номенклатура реагентів. Кожен має ключ (для імпорту й унікальності),
// перевірку, залежності (м'яке видалення/архів) і необов'язкове оновлення кешу після зміни.
using System.Globalization;
using System.Text.Json;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public interface IDictAdapter
{
    string Name { get; }
    string Table { get; }
    /// <summary>Видалення завжди архівує (isActive=false): записи цього довідника ніколи не видаляються фізично.</summary>
    bool ArchiveOnly { get; }
    Task<List<object>> ListAsync(LisDbContext db);
    Task<object?> FindAsync(LisDbContext db, string idOrKey, bool tracked);
    object FromJson(JsonElement body, JsonSerializerOptions json);
    Task ValidateAsync(LisDbContext db, object entity, bool isNew);
    void Add(LisDbContext db, object entity);
    Task<int> DependenciesAsync(LisDbContext db, object entity);
    string Search(object entity);
    string? KeyOf(object entity);
    Task OnChangedAsync(LisDbContext db);
}

public sealed class SimpleDict<T> : IDictAdapter where T : AuditableEntity, new()
{
    public string Name { get; }
    public string Table { get; }
    public bool ArchiveOnly { get; }
    private readonly Func<LisDbContext, IQueryable<T>> _set;
    private readonly Func<T, string?> _key;
    private readonly Func<T, string> _search;
    private readonly Func<LisDbContext, T, bool, Task>? _validate;
    private readonly Func<LisDbContext, T, Task<int>>? _deps;
    private readonly Func<LisDbContext, Task>? _changed;
    private readonly string _keyLabel;

    public SimpleDict(string name, string table, Func<LisDbContext, IQueryable<T>> set, Func<T, string?> key, Func<T, string> search, string keyLabel = "Код",
        Func<LisDbContext, T, bool, Task>? validate = null, Func<LisDbContext, T, Task<int>>? deps = null, bool archiveOnly = true, Func<LisDbContext, Task>? changed = null)
    { Name = name; Table = table; _set = set; _key = key; _search = search; _keyLabel = keyLabel; _validate = validate; _deps = deps; ArchiveOnly = archiveOnly; _changed = changed; }

    private static string IdOf(T e) => (string)typeof(T).GetProperty("Id")!.GetValue(e)!;

    public async Task<List<object>> ListAsync(LisDbContext db) =>
        (await _set(db).AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).ToListAsync()).OrderBy(x => _key(x), StringComparer.OrdinalIgnoreCase).Cast<object>().ToList();

    public async Task<object?> FindAsync(LisDbContext db, string idOrKey, bool tracked)
    {
        var q = _set(db).Where(x => x.RecordState != RecordStates.Deleted);
        if (!tracked) q = q.AsNoTracking();
        var all = await q.ToListAsync();
        return all.FirstOrDefault(x => IdOf(x) == idOrKey) ?? all.FirstOrDefault(x => string.Equals(_key(x), idOrKey, StringComparison.OrdinalIgnoreCase));
    }

    public object FromJson(JsonElement body, JsonSerializerOptions json) => JsonSerializer.Deserialize<T>(body.GetRawText(), json) ?? new T();

    public async Task ValidateAsync(LisDbContext db, object entity, bool isNew)
    {
        var e = (T)entity;
        if (_validate != null) await _validate(db, e, isNew);
        var key = _key(e);
        if (string.IsNullOrWhiteSpace(key)) throw ValidationException.Field("code", $"{_keyLabel} обов'язковий");
        var all = await _set(db).AsNoTracking().Where(x => x.RecordState != RecordStates.Deleted).ToListAsync();
        var id = IdOf(e);
        if (all.Any(x => IdOf(x) != id && string.Equals(_key(x), key, StringComparison.OrdinalIgnoreCase))) throw new ConflictException($"Запис «{key}» вже існує");
    }

    public void Add(LisDbContext db, object entity) => db.Set<T>().Add((T)entity);
    public Task<int> DependenciesAsync(LisDbContext db, object entity) => _deps == null ? Task.FromResult(0) : _deps(db, (T)entity);
    public string Search(object entity) => _search((T)entity);
    public string? KeyOf(object entity) => _key((T)entity);
    public Task OnChangedAsync(LisDbContext db) => _changed == null ? Task.CompletedTask : _changed(db);
}

public static class DictionaryAdapters
{
    private static string D(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static bool TopoOk(string s) => System.Text.RegularExpressions.Regex.IsMatch(s ?? "", @"^C\d{2}(\.\d)?$");

    public static readonly IReadOnlyDictionary<string, IDictAdapter> All = Build();

    private static Dictionary<string, IDictAdapter> Build()
    {
        var list = new IDictAdapter[]
        {
            new SimpleDict<LabStainMethod>("path-methods", "lab_stain_method", db => db.StainMethods, x => x.Code, x => $"{x.Code} {x.Name}",
                validate: (db, x, isNew) =>
                {
                    if (string.IsNullOrWhiteSpace(x.Name)) throw ValidationException.Field("name", "Назва обов'язкова");
                    if (x.Price < 0) throw ValidationException.Field("price", "Ціна не може бути від'ємною");
                    return Task.CompletedTask;
                }),
            new SimpleDict<LabPathMaterialType>("path-material-types", "lab_path_material_type", db => db.PathMaterialTypes, x => x.Code, x => $"{x.Code} {x.Name} {x.ServiceGroup}",
                validate: (db, x, isNew) =>
                {
                    if (string.IsNullOrWhiteSpace(x.Name)) throw ValidationException.Field("name", "Назва обов'язкова");
                    if (x.SlaDays < 0 || x.SlaDays > 365) throw ValidationException.Field("slaDays", "Строк у робочих днях: 0–365");
                    if (x.BasePrice < 0) throw ValidationException.Field("basePrice", "Ціна не може бути від'ємною");
                    return Task.CompletedTask;
                }),
            new SimpleDict<LabFixative>("path-fixatives", "lab_fixative", db => db.Fixatives, x => x.Name, x => x.Name, keyLabel: "Назва",
                validate: (db, x, isNew) =>
                {
                    if (x.MinHours < 0 || x.MaxHours < 0) throw ValidationException.Field("minHours", "Тривалість не може бути від'ємною");
                    if (x.MaxHours > 0 && x.MinHours > x.MaxHours) throw ValidationException.Field("minHours", "Мінімальна тривалість фіксації перевищує максимальну");
                    return Task.CompletedTask;
                }),
            new SimpleDict<LabPathRejectReason>("path-reject-reasons", "lab_path_reject_reason", db => db.PathRejectReasons, x => x.Code, x => $"{x.Code} {x.Name}",
                validate: (db, x, isNew) => string.IsNullOrWhiteSpace(x.Name) ? throw ValidationException.Field("name", "Назва обов'язкова") : Task.CompletedTask),
            new SimpleDict<LabPathTemplate>("path-templates", "lab_path_template", db => db.PathTemplates, x => x.Name, x => $"{x.Name} {x.Macro} {x.Micro}", keyLabel: "Назва"),
            new SimpleDict<LabPathStorageRoom>("path-rooms", "lab_path_storage_room", db => db.PathStorageRooms, x => x.Code, x => $"{x.Code} {x.Name}",
                validate: (db, x, isNew) => string.IsNullOrWhiteSpace(x.Name) ? throw ValidationException.Field("name", "Назва обов'язкова") : Task.CompletedTask,
                deps: (db, x) => db.PathBlocks.CountAsync(b => b.RoomId == x.Id)),
            new SimpleDict<LabIhcReflexRule>("path-ihc-rules", "lab_ihc_reflex_rule", db => db.IhcReflexRules, x => $"{x.TopoFrom}-{x.TopoTo}|{x.MorphCodes}|{x.Panel}", x => $"{x.TopoFrom} {x.TopoTo} {x.MorphCodes} {x.Panel} {x.Why}", keyLabel: "Діапазон топографії, морфологія й панель",
                validate: (db, x, isNew) =>
                {
                    x.TopoFrom = (x.TopoFrom ?? "").Trim().ToUpperInvariant(); x.TopoTo = (x.TopoTo ?? "").Trim().ToUpperInvariant();
                    if (!TopoOk(x.TopoFrom) || !TopoOk(x.TopoTo)) throw ValidationException.Field("topoFrom", "Топографія МКХ-О у форматі C00 або C50.9");
                    if (string.CompareOrdinal(x.TopoFrom, x.TopoTo) > 0) throw ValidationException.Field("topoFrom", "Початок діапазону топографії більший за кінець");
                    if (string.IsNullOrWhiteSpace(x.Panel)) throw ValidationException.Field("panel", "Вкажіть панель маркерів");
                    return Task.CompletedTask;
                }),
            new SimpleDict<LabPathCytoItem>("path-cytology", "lab_path_cyto_item", db => db.PathCytoItems, x => $"{x.Kind}:{x.Code}", x => $"{x.Kind} {x.Code} {x.Name}", keyLabel: "Вид і код",
                validate: (db, x, isNew) =>
                {
                    if (!CytoItemKinds.All.Contains(x.Kind)) throw ValidationException.Field("kind", "Вид позиції: матеріал, адекватність або категорія висновку");
                    if (string.IsNullOrWhiteSpace(x.Code) || string.IsNullOrWhiteSpace(x.Name)) throw ValidationException.Field("name", "Код і назва обов'язкові");
                    return Task.CompletedTask;
                }, changed: DictionaryRuntime.ApplyAsync),
            new SimpleDict<LabPathPrice>("path-prices", "lab_path_price", db => db.PathPriceItems, x => x.Code, x => $"{x.Code} {x.Name}",
                validate: (db, x, isNew) =>
                {
                    if (!PathPrices.Defaults.Any(d => d.Code == x.Code)) throw ValidationException.Field("code", "Невідомий тариф. Доступні: " + string.Join(", ", PathPrices.Defaults.Select(d => d.Code)));
                    if (x.Amount < 0) throw ValidationException.Field("amount", "Значення не може бути від'ємним");
                    return Task.CompletedTask;
                }, changed: DictionaryRuntime.ApplyAsync),
            new SimpleDict<LabTestSynonym>("test-synonyms", "lab_test_synonym", db => db.TestSynonyms, x => $"{x.TestCode}|{x.Synonym}".ToLowerInvariant(), x => $"{x.TestCode} {x.Synonym}", keyLabel: "Показник і синонім",
                validate: async (db, x, isNew) =>
                {
                    x.TestCode = (x.TestCode ?? "").Trim().ToUpperInvariant(); x.Synonym = (x.Synonym ?? "").Trim();
                    if (x.Synonym.Length == 0) throw ValidationException.Field("synonym", "Вкажіть синонім");
                    if (!await db.Tests.AnyAsync(t => t.Code == x.TestCode)) throw ValidationException.Field("testCode", "Показник не знайдено у довіднику показників");
                }),
            new SimpleDict<LabWorkCalendarDay>("work-calendar", "lab_work_calendar", db => db.WorkCalendar, x => D(x.Day), x => $"{D(x.Day)} {x.Name} {CalendarDayKinds.Label(x.Kind)}", keyLabel: "Дата",
                validate: (db, x, isNew) =>
                {
                    x.Day = DateTime.SpecifyKind(x.Day.Date, DateTimeKind.Utc);
                    if (!CalendarDayKinds.All.Contains(x.Kind)) throw ValidationException.Field("kind", "Вид дня: вихідний або робочий (перенесення)");
                    var weekend = x.Day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                    if (x.Kind == CalendarDayKinds.Workday && !weekend) throw ValidationException.Field("kind", "Робочий день-виняток має припадати на суботу або неділю");
                    if (x.Kind == CalendarDayKinds.Holiday && weekend) throw ValidationException.Field("kind", "Субота й неділя вже вихідні; додавайте лише святкові дні в будні");
                    return Task.CompletedTask;
                }, archiveOnly: false, changed: WorkCalendar.RefreshAsync),
            new SimpleDict<LabReagentItem>("reagent-items", "lab_reagent_item", db => db.ReagentItems, x => x.Code, x => $"{x.Code} {x.Name} {x.Manufacturer} {x.CatalogNumber} {x.TestCode}",
                validate: (db, x, isNew) =>
                {
                    if (string.IsNullOrWhiteSpace(x.Name)) throw ValidationException.Field("name", "Назва обов'язкова");
                    if (x.MinimumTests < 0) throw ValidationException.Field("minimumTests", "Мінімальний запас не може бути від'ємним");
                    return Task.CompletedTask;
                }, deps: (db, x) => db.ReagentLots.CountAsync(l => l.ReagentItemId == x.Id), archiveOnly: false)
        };
        return list.ToDictionary(a => a.Name);
    }
}
