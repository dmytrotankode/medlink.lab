// Імпорт (JSON/CSV) з перевіркою без запису та експорт (CSV/JSON) універсальних довідників.
// Ключ запису для оновлення «за кодом»: код; правила повторного тесту — тригер + умова + поріг + тест;
// EUCAST — збудник + антибіотик + версія; співробітники — e-mail або ПІБ; прості довідники — ключ адаптера.
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed partial class DictionaryService
{
    /// <summary>Максимум рядків в одному файлі імпорту.</summary>
    public const int MaxImportRows = 5000;

    private static readonly string[] AuditKeys = { "id", "createdOn", "createdBy", "modifiedOn", "modifiedBy", "recordState" };
    /// <summary>Колонки, значення яких завжди залишаються текстом (навіть якщо схожі на число).</summary>
    private static readonly HashSet<string> TextColumns = new(StringComparer.OrdinalIgnoreCase)
    { "code", "name", "shortName", "loincCode", "email", "phone", "fullName", "position", "description", "synonym", "testCode", "triggerTestCode", "reflexTestCode", "eucastVersion", "version", "catalogNumber", "serviceGroup", "morphCodes", "topoFrom", "topoTo", "panel" };

    // ------------------------------------------------------------------ розбір файлу
    internal static List<List<string>> ParseCsv(string content, char sep)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var inQuotes = false;
        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];
            if (inQuotes)
            {
                if (ch == '"') { if (i + 1 < content.Length && content[i + 1] == '"') { cell.Append('"'); i++; } else inQuotes = false; }
                else cell.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                if (row.Any(c => c.Trim().Length > 0)) rows.Add(row);
                row = new List<string>();
            }
            else cell.Append(ch);
        }
        row.Add(cell.ToString());
        if (row.Any(c => c.Trim().Length > 0)) rows.Add(row);
        return rows;
    }

    private static JsonNode? CellValue(string header, string raw)
    {
        var v = raw.Trim();
        if (v.Length == 0) return null; // порожня клітинка = «не змінювати»
        if (header.Equals("dropdownOptions", StringComparison.OrdinalIgnoreCase)) return new JsonArray(v.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
        if (header.Equals("items", StringComparison.OrdinalIgnoreCase)) return new JsonArray(v.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => (JsonNode?)new JsonObject { ["testCode"] = x }).ToArray());
        if (bool.TryParse(v, out var b)) return JsonValue.Create(b);
        if (v.Equals("так", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(true);
        if (v.Equals("ні", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(false);
        if (!TextColumns.Contains(header) && double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return JsonValue.Create(d);
        return JsonValue.Create(v);
    }

    /// <summary>Розбір вмісту: JSON-масив об'єктів або CSV (роздільник ; , або табуляція). Повертає рядки з номерами у файлі.</summary>
    private static List<(int Line, JsonObject Row)> ParseImport(string content)
    {
        content = content.TrimStart('﻿');
        var rows = new List<(int, JsonObject)>();
        if (content.TrimStart().StartsWith("["))
        {
            JsonArray? arr;
            try { arr = JsonNode.Parse(content)?.AsArray(); } catch (Exception ex) when (ex is JsonException or InvalidOperationException) { throw new ValidationException("Файл не є коректним JSON-масивом: " + ex.Message); }
            var i = 0;
            foreach (var n in arr ?? new JsonArray()) { i++; rows.Add((i, n as JsonObject ?? new JsonObject())); }
        }
        else
        {
            var firstLine = content.Split('\n', 2)[0];
            var sep = firstLine.Contains(';') ? ';' : firstLine.Contains('\t') ? '\t' : ',';
            var table = ParseCsv(content, sep);
            if (table.Count < 2) throw new ValidationException("CSV має містити заголовок і хоча б один рядок");
            var header = table[0].Select(h => h.Trim()).ToList();
            if (table.Count - 1 > MaxImportRows) throw TooMany(table.Count - 1);
            for (var r = 1; r < table.Count; r++)
            {
                var obj = new JsonObject();
                for (var c = 0; c < header.Count && c < table[r].Count; c++)
                    if (header[c].Length > 0) { var v = CellValue(header[c], table[r][c]); if (v != null) obj[header[c]] = v; }
                rows.Add((r + 1, obj));
            }
        }
        if (rows.Count > MaxImportRows) throw TooMany(rows.Count);
        if (rows.Count == 0) throw new ValidationException("У файлі немає жодного рядка для імпорту");
        return rows;
    }

    private static ValidationException TooMany(int n) => new($"У файлі {n} рядків — це більше за ліміт {MaxImportRows}. Розділіть файл на частини і завантажте по черзі.");

    // ------------------------------------------------------------------ нормалізація рядка й пошук існуючого запису
    private async Task NormalizeRowAsync(string name, JsonObject row)
    {
        foreach (var k in AuditKeys) row.Remove(k);
        // значення з експорту: "A | B" у рядку → масив
        foreach (var k in new[] { "dropdownOptions", "items" })
            if (row[k] is JsonValue jv && jv.TryGetValue<string>(out var str)) row[k] = CellValue(k, str);
        if (name == "eucast-breakpoints")
        {
            if (row.TryGetPropertyValue("organismCode", out var oc))
            {
                var code = oc?.ToString() ?? "";
                var o = await _db.Organisms.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code) ?? throw new ValidationException($"Збудника з кодом «{code}» немає у довіднику мікроорганізмів");
                row["organismId"] = o.Id; row.Remove("organismCode");
            }
            if (row.TryGetPropertyValue("antibioticCode", out var ac))
            {
                var code = ac?.ToString() ?? "";
                var a = await _db.Antibiotics.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code) ?? throw new ValidationException($"Антибіотика з кодом «{code}» немає у довіднику антибіотиків");
                row["antibioticId"] = a.Id; row.Remove("antibioticCode");
            }
        }
    }

    /// <summary>Текстовий ключ рядка для виявлення дублів у файлі та показу у звіті; null — ключ не визначено.</summary>
    private string? RowKey(string name, JsonObject row)
    {
        string S(string k) => row[k]?.ToString()?.Trim() ?? "";
        var adapter = Adapter(name);
        if (adapter != null)
        {
            try { return adapter.KeyOf(adapter.FromJson(JsonSerializer.SerializeToElement(row), Json)); } catch (JsonException) { return null; }
        }
        return name switch
        {
            "reflex-rules" => $"{S("triggerTestCode").ToUpperInvariant()} {(S("conditionOperator") is { Length: > 0 } op ? op : ">")} {S("thresholdValue")} → {S("reflexTestCode").ToUpperInvariant()}",
            "eucast-breakpoints" => $"{S("organismId")}/{S("antibioticId")}/{(S("eucastVersion") is { Length: > 0 } v ? v : "v14.0_2024")}",
            "employees" => S("email").Length > 0 ? S("email").ToLowerInvariant() : S("fullName").ToLowerInvariant(),
            _ => S("code").Length > 0 ? (name == "tests" ? S("code").ToUpperInvariant() : S("code")) : null
        };
    }

    /// <summary>Ідентифікатор наявного запису, який оновить рядок; null — запису ще немає.</summary>
    private async Task<string?> FindExistingAsync(string name, JsonObject row)
    {
        string S(string k) => row[k]?.ToString()?.Trim() ?? "";
        var adapter = Adapter(name);
        if (adapter != null)
        {
            var key = RowKey(name, row);
            if (string.IsNullOrWhiteSpace(key)) return null;
            var found = await adapter.FindAsync(_db, key, false);
            return found == null ? null : IdOf(found);
        }
        switch (name)
        {
            case "reflex-rules":
            {
                var trig = S("triggerTestCode").ToUpperInvariant(); var refl = S("reflexTestCode").ToUpperInvariant();
                var op = S("conditionOperator") is { Length: > 0 } o ? o : ">";
                double? thr = double.TryParse(S("thresholdValue").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
                var rules = await _db.ReflexRules.AsNoTracking().Where(r => r.RecordState != RecordStates.Deleted && r.TriggerTestCode == trig && r.ReflexTestCode == refl).ToListAsync();
                return rules.FirstOrDefault(r => r.ConditionOperator == op && r.ThresholdValue == thr)?.Id;
            }
            case "eucast-breakpoints":
            {
                if (!int.TryParse(S("organismId"), out var oid) || !int.TryParse(S("antibioticId"), out var aid)) return null;
                var ver = S("eucastVersion") is { Length: > 0 } v ? v : "v14.0_2024";
                return (await _db.EucastBreakpoints.AsNoTracking().FirstOrDefaultAsync(b => b.OrganismId == oid && b.AntibioticId == aid && b.EucastVersion == ver && b.RecordState != RecordStates.Deleted))?.Id;
            }
            case "employees":
            {
                var email = S("email"); var full = S("fullName");
                if (email.Length == 0 && full.Length == 0) return null;
                var all = await _org.EmployeesAsync();
                return all.FirstOrDefault(e => email.Length > 0 ? string.Equals(e.Email, email, StringComparison.OrdinalIgnoreCase) : string.Equals(e.FullName, full, StringComparison.OrdinalIgnoreCase))?.Id;
            }
            default:
            {
                var code = S("code");
                if (code.Length == 0) return null;
                return await ExistsByCodeAsync(name, name == "tests" ? code.ToUpperInvariant() : code) ? (name == "tests" ? code.ToUpperInvariant() : code) : null;
            }
        }
    }

    private async Task<bool> ExistsByCodeAsync(string name, string code) => name switch
    {
        "biomaterials" => await _db.BiomaterialTypes.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "tube-types" => await _db.TubeTypes.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "method-types" => await _db.MethodTypes.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "analyzer-types" => await _db.AnalyzerTypes.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "tests" => await _db.Tests.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "profiles" => await _db.Profiles.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "organisms" => await _db.Organisms.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "antibiotics" => await _db.Antibiotics.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "departments" => await _db.Departments.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "units" => await _db.Units.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "age-groups" => await _db.AgeGroups.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "rejection-reasons" => await _db.RejectionReasons.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        "cancel-reasons" => await _db.CancelReasons.AnyAsync(x => x.Code == code && x.RecordState != RecordStates.Deleted),
        _ => false
    };

    private static string Fingerprint(object o)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(o, Json))!.AsObject();
        foreach (var k in new[] { "createdOn", "createdBy", "modifiedOn", "modifiedBy", "recordState" }) node.Remove(k);
        return node.ToJsonString();
    }

    // ------------------------------------------------------------------ імпорт
    /// <summary>
    /// Імпорт довідника. dryRun=true — те саме виконується в транзакції, що відкочується: звіт показує, скільки створиться,
    /// оновиться, буде пропущено (з причинами) і які рядки відхилено перевірками. Ліміт — 5000 рядків.
    /// </summary>
    public async Task<object> ImportAsync(string name, string content, bool dryRun)
    {
        _policy.Require($"Імпорт довідника {name}", LabRoles.Admin);
        Table(name);
        var rows = ParseImport(content);
        var report = new List<object>();
        int create = 0, update = 0, skip = 0, error = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var (line, row) in rows)
            {
                string? key = null;
                try
                {
                    await NormalizeRowAsync(name, row);
                    key = RowKey(name, row);
                    if (string.IsNullOrWhiteSpace(key)) { skip++; report.Add(new { line, key = (string?)null, action = "SKIP", reason = name == "employees" ? "Не вказано ні e-mail, ні ПІБ" : "Не вказано код (ключ запису)" }); continue; }
                    if (!seen.Add(key)) { skip++; report.Add(new { line, key, action = "SKIP", reason = "Дубль у файлі: запис із таким ключем уже є вище" }); continue; }
                    var existingId = await FindExistingAsync(name, row);
                    var element = JsonSerializer.SerializeToElement(row);
                    if (existingId == null)
                    {
                        await CreateAsync(name, element);
                        create++; report.Add(new { line, key, action = "CREATE", reason = (string?)null });
                    }
                    else
                    {
                        var before = Fingerprint(await GetAsync(name, existingId));
                        var after = Fingerprint(await UpdateAsync(name, existingId, element));
                        if (before == after) { skip++; report.Add(new { line, key, action = "SKIP", reason = "Запис уже існує, зміни відсутні" }); }
                        else { update++; report.Add(new { line, key, action = "UPDATE", reason = (string?)null }); }
                    }
                }
                catch (LisException ex)
                {
                    _db.ChangeTracker.Clear();
                    error++; report.Add(new { line, key, action = "ERROR", reason = ex.Message });
                }
            }
            if (dryRun) await tx.RollbackAsync(); else await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        finally { _db.ChangeTracker.Clear(); }
        if (!dryRun && Adapter(name) is { } ad) await ad.OnChangedAsync(_db);
        else if (dryRun && Adapter(name) is { } ad2) await ad2.OnChangedAsync(_db); // кеш мав бути відкочений разом із транзакцією
        return new
        {
            dryRun, limit = MaxImportRows, total = rows.Count,
            create, update, skip, error,
            created = dryRun ? 0 : create, updated = dryRun ? 0 : update, skipped = skip, errors = error,
            rows = report
        };
    }

    // ------------------------------------------------------------------ експорт
    private static JsonObject Flatten(string name, object item)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(item, Json))!.AsObject();
        foreach (var k in new[] { "createdOn", "createdBy", "modifiedOn", "modifiedBy", "recordState" }) node.Remove(k);
        var flat = new JsonObject();
        foreach (var kv in node)
        {
            if (kv.Key is "biomaterialType" or "tubeType" or "method" or "organism" or "antibiotic" or "department" or "listing") continue;
            if (kv.Value is JsonArray arr)
            {
                if (kv.Key == "items") flat["items"] = string.Join(" | ", arr.Select(i => i?["test"]?["code"]?.ToString() ?? i?["testCode"]?.ToString()).Where(x => !string.IsNullOrEmpty(x)));
                else if (arr.All(a => a is JsonValue)) flat[kv.Key] = string.Join(" | ", arr.Select(a => a?.ToString()));
                continue;
            }
            if (kv.Value is JsonObject) continue;
            flat[kv.Key] = kv.Value?.DeepClone();
        }
        if (item is LabEucastBreakpoint eb) { flat["organismCode"] = eb.Organism?.Code; flat["antibioticCode"] = eb.Antibiotic?.Code; }
        return flat;
    }

    private static string CsvCell(JsonNode? v, char sep)
    {
        if (v == null) return "";
        var s = v is JsonValue jv && jv.TryGetValue<bool>(out var b) ? (b ? "true" : "false")
            : v is JsonValue jd && jd.TryGetValue<double>(out var d) ? d.ToString(CultureInfo.InvariantCulture)
            : v.ToString();
        // захист від формул у Excel
        if (s.Length > 0 && (s[0] is '=' or '+' or '@' || (s[0] == '-' && !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))) s = "'" + s;
        return s.IndexOfAny(new[] { sep, '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>Експорт довідника у CSV (роздільник «;», UTF-8 з BOM, відкривається в Excel) або JSON. Читання — для всіх ролей.</summary>
    public async Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(string name, string format, bool? isActive, string? search)
    {
        Table(name);
        var items = await FilteredAsync(name, search, isActive);
        var flat = items.Select(i => Flatten(name, i)).ToList();
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var arr = new JsonArray(flat.Select(f => (JsonNode?)f).ToArray());
            return (Encoding.UTF8.GetBytes(arr.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })), "application/json; charset=utf-8", $"{name}-{stamp}.json");
        }
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)) throw ValidationException.Field("format", "Формат експорту: csv або json");
        var headers = new List<string>();
        foreach (var f in flat) foreach (var kv in f) if (!headers.Contains(kv.Key)) headers.Add(kv.Key);
        const char sep = ';';
        var sb = new StringBuilder();
        sb.Append(string.Join(sep, headers)).Append("\r\n");
        foreach (var f in flat) sb.Append(string.Join(sep, headers.Select(h => CsvCell(f[h], sep)))).Append("\r\n");
        return (new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv; charset=utf-8", $"{name}-{stamp}.csv");
    }
}
