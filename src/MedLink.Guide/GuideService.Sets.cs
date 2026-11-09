// Набори інструкцій (презентації): CRUD для редактора, розкриття в каталозі плеєра, ідемпотентний імпорт.
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace MedLink.Guide;

/// <summary>Короткий опис набору для позначення розділів/інструкцій у каталозі.</summary>
public sealed record SetRef(string Code, string Caption, string? Color);

public sealed partial class GuideService
{
    private static readonly Regex ColorRx = new("^#[0-9a-fA-F]{3,8}$", RegexOptions.Compiled);

    private static string? CleanColor(string? c)
    {
        c = c?.Trim();
        return string.IsNullOrEmpty(c) ? null : ColorRx.IsMatch(c) ? c : throw Bad("Колір має бути у форматі #rrggbb");
    }

    private sealed class CatalogSets
    {
        public List<object> Sets { get; } = new();
        public Dictionary<string, List<SetRef>> FolderSets { get; } = new();
        public Dictionary<string, List<SetRef>> ScenarioSets { get; } = new();
    }

    /// <summary>Розкриває набори у списки опублікованих (та видимих користувачу) інструкцій; розділ → усі його інструкції разом з підрозділами.</summary>
    private async Task<CatalogSets> BuildCatalogSetsAsync(string appId, List<GuideScenario> scenarios, List<GuideFolder> folders, GuideUser user)
    {
        var res = new CatalogSets();
        var sets = (await _db.Sets.AsNoTracking().Where(x => x.AppId == appId).OrderBy(x => x.SortOrder).ThenBy(x => x.Caption).ToListAsync())
            .Where(x => TenantVisible(x.TenantId, user)).ToList();
        if (sets.Count == 0) return res;
        var setIds = sets.Select(x => x.Id).ToList();
        var items = (await _db.SetItems.AsNoTracking().Where(i => setIds.Contains(i.SetId)).ToListAsync()).OrderBy(i => i.SortOrder).ToList();

        var scById = scenarios.ToDictionary(s => s.Id);
        var folderById = folders.ToDictionary(f => f.Id);
        var childrenOf = folders.Where(f => f.ParentId != null).GroupBy(f => f.ParentId!).ToDictionary(g => g.Key, g => g.Select(f => f.Id).ToList());

        List<string> Subtree(string folderId)
        {
            var list = new List<string> { folderId };
            for (var i = 0; i < list.Count; i++)
                if (childrenOf.TryGetValue(list[i], out var ch)) list.AddRange(ch.Where(c => !list.Contains(c)));
            return list;
        }

        static void Mark(Dictionary<string, List<SetRef>> map, string id, SetRef r)
        {
            if (!map.TryGetValue(id, out var l)) map[id] = l = new();
            if (l.All(x => x.Code != r.Code)) l.Add(r);
        }

        foreach (var set in sets)
        {
            var code = set.Code ?? Short(set.Id);
            var r = new SetRef(code, set.Caption, set.Color);
            var ordered = new List<GuideScenario>();
            void Add(GuideScenario s) { if (ordered.All(x => x.Id != s.Id)) ordered.Add(s); }
            foreach (var it in items.Where(i => i.SetId == set.Id))
            {
                if (it.ScenarioId != null)
                {
                    if (scById.TryGetValue(it.ScenarioId, out var s)) { Add(s); Mark(res.ScenarioSets, s.Id, r); }
                }
                else if (it.FolderId != null && folderById.ContainsKey(it.FolderId))
                {
                    var fids = Subtree(it.FolderId);
                    foreach (var fid in fids) Mark(res.FolderSets, fid, r);
                    foreach (var s in scenarios.Where(x => x.FolderId != null && fids.Contains(x.FolderId)).OrderBy(x => x.SortOrder).ThenBy(x => x.Caption))
                    { Add(s); Mark(res.ScenarioSets, s.Id, r); }
                }
            }
            if (ordered.Count == 0) continue;
            res.Sets.Add(new
            {
                set.Id, Code = code, set.Caption, set.Description, set.Color, set.Icon, set.SortOrder,
                estMinutes = ordered.Sum(s => s.EstMinutes ?? 0),
                scenarioCount = ordered.Count,
                scenarios = ordered.Select(s => new
                {
                    s.Id, s.Code, s.Caption, s.EstMinutes,
                    folder = s.FolderId != null && folderById.TryGetValue(s.FolderId, out var f) ? f.Caption : null
                }).ToList()
            });
        }
        return res;
    }

    // ------------------------------------------------------------------ редактор
    private async Task<SetDto> MapSetAsync(GuideSet s, string appCode)
    {
        var items = await _db.SetItems.AsNoTracking().Where(i => i.SetId == s.Id).OrderBy(i => i.SortOrder).ToListAsync();
        return new SetDto
        {
            Id = s.Id, AppCode = appCode, TenantId = s.TenantId, Code = s.Code, Caption = s.Caption, Description = s.Description, Color = s.Color, Icon = s.Icon,
            SortOrder = s.SortOrder, Items = items.Select(i => new SetItemDto { ScenarioId = i.ScenarioId, FolderId = i.FolderId }).ToList()
        };
    }

    public async Task<List<SetDto>> ListSetsAsync(string appCode, GuideUser user)
    {
        var app = await AppAsync(appCode);
        var sets = (await _db.Sets.AsNoTracking().Where(x => x.AppId == app.Id).OrderBy(x => x.SortOrder).ThenBy(x => x.Caption).ToListAsync())
            .Where(x => TenantVisible(x.TenantId, user)).ToList();
        var res = new List<SetDto>();
        foreach (var s in sets) res.Add(await MapSetAsync(s, appCode));
        return res;
    }

    public async Task<SetDto> CreateSetAsync(SetDto dto, GuideUser user)
    {
        Use(user);
        if (string.IsNullOrWhiteSpace(dto.Caption)) throw Bad("Назва набору обов'язкова");
        if (string.IsNullOrWhiteSpace(dto.AppCode)) throw Bad("appCode обов'язковий");
        var app = await AppAsync(dto.AppCode!);
        var code = NullIfEmpty(dto.Code);
        if (code != null && await _db.Sets.AnyAsync(x => x.AppId == app.Id && x.Code == code)) throw new GuideException(409, $"Набір із кодом «{code}» уже існує");
        var s = new GuideSet
        {
            AppId = app.Id, TenantId = dto.TenantId, Code = code, Caption = dto.Caption.Trim(), Description = dto.Description,
            Color = CleanColor(dto.Color), Icon = dto.Icon, SortOrder = dto.SortOrder
        };
        _db.Sets.Add(s);
        await _db.SaveChangesAsync();
        if (dto.Items.Count > 0) await ReplaceSetItemsAsync(s, dto.Items);
        return await MapSetAsync(s, app.Code);
    }

    private async Task<(GuideSet s, string appCode)> LoadSetAsync(string id)
    {
        var s = await _db.Sets.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFound("Набір");
        var app = await _db.Apps.AsNoTracking().FirstAsync(a => a.Id == s.AppId);
        return (s, app.Code);
    }

    public async Task<SetDto> UpdateSetAsync(string id, SetDto dto, GuideUser user)
    {
        Use(user);
        var (s, appCode) = await LoadSetAsync(id);
        if (string.IsNullOrWhiteSpace(dto.Caption)) throw Bad("Назва набору обов'язкова");
        var code = NullIfEmpty(dto.Code);
        if (code != null && await _db.Sets.AnyAsync(x => x.AppId == s.AppId && x.Code == code && x.Id != s.Id)) throw new GuideException(409, $"Набір із кодом «{code}» уже існує");
        s.Caption = dto.Caption.Trim(); s.Code = code; s.Description = dto.Description; s.Color = CleanColor(dto.Color);
        s.Icon = dto.Icon; s.SortOrder = dto.SortOrder; s.TenantId = dto.TenantId;
        await _db.SaveChangesAsync();
        return await MapSetAsync(s, appCode);
    }

    public async Task DeleteSetAsync(string id, GuideUser user)
    {
        Use(user);
        var (s, _) = await LoadSetAsync(id);
        s.RecordState = RecordStates.Deleted;
        await _db.SaveChangesAsync();
    }

    public async Task<SetDto> ReplaceSetItemsAsync(string id, List<SetItemDto> items, GuideUser user)
    {
        Use(user);
        var (s, appCode) = await LoadSetAsync(id);
        await ReplaceSetItemsAsync(s, items);
        return await MapSetAsync(s, appCode);
    }

    private async Task ReplaceSetItemsAsync(GuideSet set, List<SetItemDto> items)
    {
        var scIds = new HashSet<string>(await _db.Scenarios.AsNoTracking().Where(x => x.AppId == set.AppId).Select(x => x.Id).ToListAsync());
        var fIds = new HashSet<string>(await _db.Folders.AsNoTracking().Where(x => x.AppId == set.AppId).Select(x => x.Id).ToListAsync());
        var rows = new List<GuideSetItem>();
        foreach (var it in items)
        {
            var hasS = !string.IsNullOrWhiteSpace(it.ScenarioId);
            var hasF = !string.IsNullOrWhiteSpace(it.FolderId);
            if (hasS == hasF) throw Bad("Елемент набору — або scenarioId, або folderId");
            if (hasS && !scIds.Contains(it.ScenarioId!)) throw NotFound("Інструкцію");
            if (hasF && !fIds.Contains(it.FolderId!)) throw NotFound("Розділ");
            var sc = hasS ? it.ScenarioId : null;
            var fo = hasF ? it.FolderId : null;
            if (rows.Any(r => r.ScenarioId == sc && r.FolderId == fo)) continue;
            rows.Add(new GuideSetItem { SetId = set.Id, ScenarioId = sc, FolderId = fo, SortOrder = rows.Count });
        }
        _db.SetItems.RemoveRange(await _db.SetItems.Where(i => i.SetId == set.Id).ToListAsync());
        _db.SetItems.AddRange(rows);
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ імпорт (ідемпотентно за code)
    private async Task ImportSetsAsync(GuideApp app, List<SetDto> sets, ImportResult res)
    {
        if (sets.Count == 0) return;
        var scByCode = (await _db.Scenarios.AsNoTracking().Where(s => s.AppId == app.Id && s.Code != null).ToListAsync()).ToDictionary(s => s.Code!, s => s.Id, StringComparer.Ordinal);
        var fByCode = (await _db.Folders.AsNoTracking().Where(f => f.AppId == app.Id && f.Code != null).ToListAsync()).ToDictionary(f => f.Code!, f => f.Id, StringComparer.Ordinal);
        foreach (var sd in sets)
        {
            if (string.IsNullOrWhiteSpace(sd.Code)) throw Bad("Для імпорту кожен набір має мати code");
            var s = await _db.Sets.FirstOrDefaultAsync(x => x.AppId == app.Id && x.Code == sd.Code);
            if (s == null) { s = new GuideSet { AppId = app.Id, Code = sd.Code, TenantId = sd.TenantId }; _db.Sets.Add(s); res.SetsCreated++; }
            else res.SetsUpdated++;
            s.Caption = sd.Caption; s.Description = sd.Description; s.Color = CleanColor(sd.Color); s.Icon = sd.Icon; s.SortOrder = sd.SortOrder;
            await _db.SaveChangesAsync();

            var wanted = new List<(string? sc, string? f)>();
            foreach (var it in sd.Items)
            {
                if (!string.IsNullOrWhiteSpace(it.ScenarioCode) && scByCode.TryGetValue(it.ScenarioCode, out var sid)) wanted.Add((sid, null));
                else if (!string.IsNullOrWhiteSpace(it.FolderCode) && fByCode.TryGetValue(it.FolderCode, out var fid)) wanted.Add((null, fid));
                // невідомі коди пропускаємо: набір не повинен ламати завантаження решти бандла
            }
            wanted = wanted.Distinct().ToList();
            var cur = await _db.SetItems.Where(i => i.SetId == s.Id).OrderBy(i => i.SortOrder).ToListAsync();
            if (cur.Select(i => (i.ScenarioId, i.FolderId)).SequenceEqual(wanted.Select(w => (w.sc, w.f)))) continue;
            _db.SetItems.RemoveRange(cur);
            for (var i = 0; i < wanted.Count; i++)
                _db.SetItems.Add(new GuideSetItem { SetId = s.Id, ScenarioId = wanted[i].sc, FolderId = wanted[i].f, SortOrder = i });
            await _db.SaveChangesAsync();
        }
    }
}
