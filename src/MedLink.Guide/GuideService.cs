// Бізнес-логіка модуля Guide: дерево, сценарії, кроки з якорями, публікація (знімки), імпорт/експорт, здоров'я якорів, прогрес.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace MedLink.Guide;

public sealed partial class GuideService
{
    private readonly GuideDbContext _db;

    public GuideService(GuideDbContext db) => _db = db;

    // ------------------------------------------------------------------ допоміжне
    private static GuideException NotFound(string what) => new(404, $"{what} не знайдено");
    private static GuideException Bad(string msg) => new(400, msg);

    private void Use(GuideUser user) => _db.CurrentUserId = user.EmployeeId;

    private static bool TenantVisible(string? entityTenant, GuideUser user) => entityTenant == null || entityTenant == user.TenantId;

    private async Task<GuideApp> AppAsync(string code)
    {
        var app = await _db.Apps.FirstOrDefaultAsync(a => a.Code == code);
        return app ?? throw NotFound($"Систему «{code}»");
    }

    private async Task<GuideApp> EnsureAppAsync(string code, string? caption = null)
    {
        var app = await _db.Apps.FirstOrDefaultAsync(a => a.Code == code);
        if (app != null) return app;
        app = new GuideApp { Code = code, Caption = caption ?? code };
        _db.Apps.Add(app);
        return app;
    }

    private static string Short(string id) => id.Length >= 8 ? id[..8] : id;

    /// <summary>Чи збігається маршрут із шаблоном (:param — сегмент, * — будь-що).</summary>
    public static bool RouteMatches(string? pattern, string? route)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(route)) return false;
        static string Norm(string s)
        {
            var i = s.IndexOfAny(new[] { '?', '#' });
            if (i >= 0) s = s[..i];
            return s.Length > 1 ? s.TrimEnd('/') : s;
        }
        var rx = "^" + Regex.Escape(Norm(pattern)).Replace("\\*", ".*") + "$";
        rx = Regex.Replace(rx, @":[A-Za-z0-9_]+", "[^/]+");
        return Regex.IsMatch(Norm(route), rx, RegexOptions.IgnoreCase);
    }

    // ------------------------------------------------------------------ системи
    public async Task<AppDto> UpsertAppAsync(AppDto dto, GuideUser user)
    {
        Use(user);
        if (string.IsNullOrWhiteSpace(dto.Code)) throw Bad("Код системи обов'язковий");
        var app = await EnsureAppAsync(dto.Code.Trim(), dto.Caption);
        if (!string.IsNullOrWhiteSpace(dto.Caption)) app.Caption = dto.Caption!;
        if (dto.BaseUrl != null) app.BaseUrl = dto.BaseUrl;
        if (!string.IsNullOrWhiteSpace(dto.DefaultLocale)) app.DefaultLocale = dto.DefaultLocale!;
        await _db.SaveChangesAsync();
        return new AppDto { Id = app.Id, Code = app.Code, Caption = app.Caption, BaseUrl = app.BaseUrl, DefaultLocale = app.DefaultLocale };
    }

    // ------------------------------------------------------------------ каталог (плеєр) і дерево (редактор)
    public async Task<object> CatalogAsync(string appCode, string? roleParam, string? route, GuideUser user)
    {
        var app = await AppAsync(appCode);
        var roles = new HashSet<string>(user.Roles, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(roleParam))
            foreach (var r in roleParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) roles.Add(r);

        var all = await _db.Scenarios.AsNoTracking()
            .Where(s => s.AppId == app.Id && s.Status == GuideConst.Published && s.PublishedVersion != null)
            .ToListAsync();
        var scenarios = all.Where(s => TenantVisible(s.TenantId, user)).Where(s =>
        {
            var req = GuideJson.ParseList(s.RequiredRoles);
            return req.Count == 0 || req.Any(roles.Contains);
        }).ToList();

        var ids = scenarios.Select(s => s.Id).ToList();
        var steps = await _db.Steps.AsNoTracking().Where(s => ids.Contains(s.ScenarioId)).Select(s => new { s.ScenarioId, s.Route }).ToListAsync();
        var folders = (await _db.Folders.AsNoTracking().Where(f => f.AppId == app.Id).OrderBy(f => f.SortOrder).ThenBy(f => f.Caption).ToListAsync())
            .Where(f => TenantVisible(f.TenantId, user)).ToList();

        // Порожні папки приховуємо; зберігаємо предків папок зі сценаріями
        var byId = folders.ToDictionary(f => f.Id);
        var keep = new HashSet<string>();
        foreach (var s in scenarios)
        {
            var fid = s.FolderId;
            while (fid != null && byId.TryGetValue(fid, out var f) && keep.Add(f.Id)) fid = f.ParentId;
        }

        var setData = await BuildCatalogSetsAsync(app.Id, scenarios, folders, user, all.Where(s => TenantVisible(s.TenantId, user)).ToList());
        var noSets = Array.Empty<SetRef>();

        return new
        {
            app = new { app.Code, app.Caption },
            sets = setData.Sets,
            folders = folders.Where(f => keep.Contains(f.Id)).Select(f => new
            {
                f.Id, f.ParentId, f.Code, f.Caption, f.Description, f.Icon, f.SortOrder,
                setCodes = setData.FolderSets.TryGetValue(f.Id, out var fs) ? fs.Select(x => x.Code).ToArray() : Array.Empty<string>(),
                sets = setData.FolderSets.TryGetValue(f.Id, out var fs2) ? fs2.ToArray() : noSets
            }),
            scenarios = scenarios.OrderBy(s => s.SortOrder).ThenBy(s => s.Caption).Select(s => new
            {
                s.Id, s.FolderId, s.Code, s.Caption, s.Description, s.Icon, s.StartRoute, s.EstMinutes, s.Locale, s.SortOrder,
                tags = GuideJson.ParseList(s.Tags),
                requiredRoles = GuideJson.ParseList(s.RequiredRoles),
                s.PublishedVersion,
                stepCount = steps.Count(x => x.ScenarioId == s.Id),
                relevantHere = route != null && (RouteMatches(s.StartRoute, route) || steps.Any(x => x.ScenarioId == s.Id && RouteMatches(x.Route, route))),
                setCodes = setData.ScenarioSets.TryGetValue(s.Id, out var ss) ? ss.Select(x => x.Code).ToArray() : Array.Empty<string>(),
                sets = setData.ScenarioSets.TryGetValue(s.Id, out var ss2) ? ss2.ToArray() : noSets
            })
        };
    }

    public async Task<object> TreeAsync(string appCode, GuideUser user)
    {
        var app = await AppAsync(appCode);
        var folders = (await _db.Folders.AsNoTracking().Where(f => f.AppId == app.Id).OrderBy(f => f.SortOrder).ThenBy(f => f.Caption).ToListAsync())
            .Where(f => TenantVisible(f.TenantId, user)).ToList();
        var scenarios = (await _db.Scenarios.AsNoTracking().Where(s => s.AppId == app.Id).OrderBy(s => s.SortOrder).ThenBy(s => s.Caption).ToListAsync())
            .Where(s => TenantVisible(s.TenantId, user)).ToList();
        var counts = (await _db.Steps.AsNoTracking().GroupBy(s => s.ScenarioId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync()).ToDictionary(x => x.Id, x => x.N);
        return new
        {
            app = new { app.Code, app.Caption },
            counts = new
            {
                draft = scenarios.Count(s => s.Status == GuideConst.Draft),
                published = scenarios.Count(s => s.Status == GuideConst.Published),
                archived = scenarios.Count(s => s.Status == GuideConst.Archived)
            },
            folders = folders.Select(f => new
            {
                f.Id, f.ParentId, f.Code, f.Caption, f.Description, f.Icon, f.SortOrder, f.TenantId,
                scenarioCount = scenarios.Count(s => s.FolderId == f.Id)
            }),
            scenarios = scenarios.Select(s => new
            {
                s.Id, s.FolderId, s.Code, s.Caption, s.Icon, s.Status, s.SortOrder, s.TenantId, s.DraftVersion, s.PublishedVersion, s.PublishedOn,
                stepCount = counts.TryGetValue(s.Id, out var n) ? n : 0
            })
        };
    }

    // ------------------------------------------------------------------ папки
    private static FolderDto Map(GuideFolder f) => new()
    {
        Id = f.Id, ParentId = f.ParentId, TenantId = f.TenantId, Code = f.Code, Caption = f.Caption, Description = f.Description, Icon = f.Icon, SortOrder = f.SortOrder
    };

    public async Task<FolderDto> CreateFolderAsync(FolderDto dto, GuideUser user)
    {
        Use(user);
        if (string.IsNullOrWhiteSpace(dto.Caption)) throw Bad("Назва розділу обов'язкова");
        if (string.IsNullOrWhiteSpace(dto.AppCode)) throw Bad("appCode обов'язковий");
        var app = await AppAsync(dto.AppCode!);
        if (dto.ParentId != null && !await _db.Folders.AnyAsync(f => f.Id == dto.ParentId && f.AppId == app.Id)) throw NotFound("Батьківський розділ");
        var f = new GuideFolder
        {
            AppId = app.Id, ParentId = dto.ParentId, TenantId = dto.TenantId, Code = NullIfEmpty(dto.Code), Caption = dto.Caption.Trim(),
            Description = dto.Description, Icon = dto.Icon, SortOrder = dto.SortOrder
        };
        _db.Folders.Add(f);
        await _db.SaveChangesAsync();
        return Map(f);
    }

    public async Task<FolderDto> UpdateFolderAsync(string id, FolderDto dto, GuideUser user)
    {
        Use(user);
        var f = await _db.Folders.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFound("Розділ");
        if (string.IsNullOrWhiteSpace(dto.Caption)) throw Bad("Назва розділу обов'язкова");
        f.Caption = dto.Caption.Trim();
        f.Code = NullIfEmpty(dto.Code);
        f.Description = dto.Description;
        f.Icon = dto.Icon;
        f.SortOrder = dto.SortOrder;
        f.TenantId = dto.TenantId;
        await _db.SaveChangesAsync();
        return Map(f);
    }

    public async Task<FolderDto> MoveFolderAsync(string id, MoveFolderDto dto, GuideUser user)
    {
        Use(user);
        var f = await _db.Folders.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFound("Розділ");
        if (dto.ParentId != null)
        {
            var folders = await _db.Folders.AsNoTracking().Where(x => x.AppId == f.AppId).ToDictionaryAsync(x => x.Id);
            if (!folders.ContainsKey(dto.ParentId)) throw NotFound("Батьківський розділ");
            for (var cur = dto.ParentId; cur != null; cur = folders.TryGetValue(cur, out var p) ? p.ParentId : null)
                if (cur == id) throw Bad("Не можна перемістити розділ усередину самого себе");
        }
        f.ParentId = dto.ParentId;
        if (dto.SortOrder != null) f.SortOrder = dto.SortOrder.Value;
        await _db.SaveChangesAsync();
        return Map(f);
    }

    public async Task DeleteFolderAsync(string id, bool cascade, GuideUser user)
    {
        Use(user);
        var f = await _db.Folders.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFound("Розділ");
        var all = await _db.Folders.Where(x => x.AppId == f.AppId).ToListAsync();
        var subtree = new List<GuideFolder> { f };
        for (var i = 0; i < subtree.Count; i++) subtree.AddRange(all.Where(x => x.ParentId == subtree[i].Id));
        var ids = subtree.Select(x => x.Id).ToList();
        var scenarios = await _db.Scenarios.Where(s => s.FolderId != null && ids.Contains(s.FolderId)).ToListAsync();
        if (!cascade && (subtree.Count > 1 || scenarios.Count > 0))
            throw new GuideException(409, "Розділ не порожній: видаліть вкладене або використайте cascade=true");
        foreach (var x in subtree) x.RecordState = RecordStates.Deleted;
        foreach (var s in scenarios) s.RecordState = RecordStates.Deleted;
        await _db.SaveChangesAsync();
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ------------------------------------------------------------------ відображення сценарію
    private static AnchorDto Map(GuideAnchor a) => new()
    {
        Id = a.Id, Code = a.Code, Caption = a.Caption, RoutePattern = a.RoutePattern,
        Strategies = GuideJson.Parse(a.Strategies) ?? new JsonArray(), Fingerprint = GuideJson.Parse(a.Fingerprint) ?? new JsonObject(),
        ParentLevels = a.ParentLevels, ScreenshotId = a.ScreenshotId, HealthStatus = a.HealthStatus, HealthCheckedOn = a.HealthCheckedOn, LastFoundHow = a.LastFoundHow
    };

    private static StepDto Map(GuideStep s, GuideAnchor? a) => new()
    {
        Id = s.Id, AnchorId = s.AnchorId, Anchor = a == null ? null : Map(a), SortOrder = s.SortOrder, StepType = s.StepType, Title = s.Title, Body = s.Body,
        Route = s.Route, Highlight = s.Highlight, ParentLevels = s.ParentLevels, Rect = GuideJson.Parse(s.Rect), Padding = s.Padding, Placement = s.Placement,
        InputValue = s.InputValue, InputMode = s.InputMode, Expect = s.Expect, WaitRoute = s.WaitRoute, TimeoutMs = s.TimeoutMs, Optional = s.Optional,
        BlockOutside = s.BlockOutside, NarrationMediaId = s.NarrationMediaId, Transcript = s.Transcript
    };

    private static VariableDto Map(GuideScenarioVariable v) => new()
    {
        Id = v.Id, Name = v.Name, Caption = v.Caption, Kind = v.Kind, DefaultValue = v.DefaultValue, Options = GuideJson.Parse(v.Options) ?? new JsonObject(), SortOrder = v.SortOrder
    };

    private async Task<ScenarioDto> LoadScenarioDtoAsync(GuideScenario s, string appCode)
    {
        var steps = await _db.Steps.AsNoTracking().Where(x => x.ScenarioId == s.Id).OrderBy(x => x.SortOrder).ToListAsync();
        var anchorIds = steps.Where(x => x.AnchorId != null).Select(x => x.AnchorId!).Distinct().ToList();
        var anchors = await _db.Anchors.AsNoTracking().Where(a => anchorIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id);
        var vars = await _db.Variables.AsNoTracking().Where(v => v.ScenarioId == s.Id).OrderBy(v => v.SortOrder).ToListAsync();
        return new ScenarioDto
        {
            Id = s.Id, AppCode = appCode, FolderId = s.FolderId, TenantId = s.TenantId, Code = s.Code, Caption = s.Caption, Description = s.Description, Icon = s.Icon,
            Status = s.Status, StartRoute = s.StartRoute, RequiredRoles = GuideJson.ParseList(s.RequiredRoles), Tags = GuideJson.ParseList(s.Tags), EstMinutes = s.EstMinutes,
            Locale = s.Locale, SortOrder = s.SortOrder, DraftVersion = s.DraftVersion, PublishedVersion = s.PublishedVersion, PublishedOn = s.PublishedOn,
            Steps = steps.Select(x => Map(x, x.AnchorId != null && anchors.TryGetValue(x.AnchorId, out var a) ? a : null)).ToList(),
            Variables = vars.Select(Map).ToList()
        };
    }

    private async Task<(GuideScenario s, string appCode)> LoadScenarioAsync(string id, GuideUser user, bool tracking = true)
    {
        var q = tracking ? _db.Scenarios.AsQueryable() : _db.Scenarios.AsNoTracking();
        var s = await q.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFound("Інструкцію");
        if (!TenantVisible(s.TenantId, user)) throw NotFound("Інструкцію");
        var app = await _db.Apps.AsNoTracking().FirstAsync(a => a.Id == s.AppId);
        return (s, app.Code);
    }

    public async Task<ScenarioDto> GetScenarioAsync(string id, GuideUser user)
    {
        var (s, appCode) = await LoadScenarioAsync(id, user, false);
        return await LoadScenarioDtoAsync(s, appCode);
    }

    // ------------------------------------------------------------------ сценарії
    private void ApplyMeta(GuideScenario s, ScenarioDto dto)
    {
        s.Caption = dto.Caption.Trim();
        var st0 = _db.Entry(s).State; var isNew = st0 == Microsoft.EntityFrameworkCore.EntityState.Detached || st0 == Microsoft.EntityFrameworkCore.EntityState.Added;
        if (isNew || dto.Code != null) s.Code = NullIfEmpty(dto.Code) ?? s.Code;
        s.Description = dto.Description;
        if (isNew || dto.Icon != null) s.Icon = dto.Icon ?? s.Icon;
        s.StartRoute = NullIfEmpty(dto.StartRoute);
        s.RequiredRoles = GuideJson.Stringify(dto.RequiredRoles);
        if (isNew || (dto.Tags != null && dto.Tags.Count > 0)) s.Tags = GuideJson.Stringify(dto.Tags);
        if (isNew || dto.EstMinutes != null) s.EstMinutes = dto.EstMinutes ?? s.EstMinutes;
        if (isNew || !string.IsNullOrWhiteSpace(dto.Locale)) s.Locale = string.IsNullOrWhiteSpace(dto.Locale) ? "uk" : dto.Locale;
        if (isNew || dto.SortOrder != 0) s.SortOrder = dto.SortOrder;
        if (isNew || dto.TenantId != null) s.TenantId = dto.TenantId;
    }

    public async Task<ScenarioDto> CreateScenarioAsync(ScenarioDto dto, GuideUser user)
    {
        Use(user);
        if (string.IsNullOrWhiteSpace(dto.Caption)) throw Bad("Назва інструкції обов'язкова");
        if (string.IsNullOrWhiteSpace(dto.AppCode)) throw Bad("appCode обов'язковий");
        var app = await AppAsync(dto.AppCode!);
        if (dto.FolderId != null && !await _db.Folders.AnyAsync(f => f.Id == dto.FolderId && f.AppId == app.Id)) throw NotFound("Розділ");
        var s = new GuideScenario { AppId = app.Id, FolderId = dto.FolderId };
        ApplyMeta(s, dto);
        _db.Scenarios.Add(s);
        await _db.SaveChangesAsync();
        if (dto.Steps.Count > 0) await ReplaceStepsAsync(s, dto.Steps, user, false);
        if (dto.Variables.Count > 0) await ReplaceVariablesAsync(s, dto.Variables);
        await _db.SaveChangesAsync();
        return await LoadScenarioDtoAsync(s, app.Code);
    }

    public async Task<ScenarioDto> UpdateScenarioAsync(string id, ScenarioDto dto, GuideUser user)
    {
        Use(user);
        if (string.IsNullOrWhiteSpace(dto.Caption)) throw Bad("Назва інструкції обов'язкова");
        var (s, appCode) = await LoadScenarioAsync(id, user);
        if (dto.FolderId != null && !await _db.Folders.AnyAsync(f => f.Id == dto.FolderId && f.AppId == s.AppId)) throw NotFound("Розділ");
        s.FolderId = dto.FolderId;
        ApplyMeta(s, dto);
        // Статус змінюється лише в архів; повернення з архіву/публікації — через /unpublish і /publish
        if (dto.Status == GuideConst.Archived) s.Status = GuideConst.Archived;
        s.DraftVersion++;
        await _db.SaveChangesAsync();
        return await LoadScenarioDtoAsync(s, appCode);
    }

    public async Task DeleteScenarioAsync(string id, GuideUser user)
    {
        Use(user);
        var (s, _) = await LoadScenarioAsync(id, user);
        s.RecordState = RecordStates.Deleted;
        await _db.SaveChangesAsync();
    }

    public async Task<ScenarioDto> DuplicateScenarioAsync(string id, GuideUser user)
    {
        Use(user);
        var (src, appCode) = await LoadScenarioAsync(id, user, false);
        var dto = await LoadScenarioDtoAsync(src, appCode);
        dto.Id = null;
        dto.Caption += " (копія)";
        dto.Code = null;
        dto.Status = GuideConst.Draft;
        foreach (var st in dto.Steps) { st.Id = null; st.Anchor = null; } // якорі спільні (за anchorId)
        foreach (var v in dto.Variables) v.Id = null;
        return await CreateScenarioAsync(dto, user);
    }

    // ------------------------------------------------------------------ кроки / якорі / змінні
    private static bool JsonSameContent(string? x, string? y)
    {
        if (x == y) return true;
        try { return System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(x ?? "null"), System.Text.Json.Nodes.JsonNode.Parse(y ?? "null")); }
        catch { return false; }
    }

    private async Task<GuideAnchor?> UpsertAnchorAsync(GuideScenario sc, AnchorDto dto, Dictionary<string, GuideAnchor> cache, string? defaultCode)
    {
        GuideAnchor? a = null;
        if (!string.IsNullOrWhiteSpace(dto.Id))
        {
            if (!cache.TryGetValue("id:" + dto.Id, out a))
                a = await _db.Anchors.FirstOrDefaultAsync(x => x.Id == dto.Id && x.AppId == sc.AppId);
        }
        var code = NullIfEmpty(dto.Code) ?? (a == null ? defaultCode : null);
        if (a == null && code != null)
        {
            if (!cache.TryGetValue("code:" + code, out a))
                a = await _db.Anchors.FirstOrDefaultAsync(x => x.Code == code && x.AppId == sc.AppId);
        }
        var created = a == null;
        a ??= new GuideAnchor { AppId = sc.AppId, TenantId = sc.TenantId, Code = code };
        if (created) _db.Anchors.Add(a);
        if (dto.Code != null && !created) a.Code = NullIfEmpty(dto.Code);
        if (!string.IsNullOrWhiteSpace(dto.Caption)) a.Caption = dto.Caption!;
        else if (string.IsNullOrWhiteSpace(a.Caption)) a.Caption = "Якір";
        if (dto.RoutePattern != null) a.RoutePattern = dto.RoutePattern;
        if (dto.Strategies != null)
        {
            var json = dto.Strategies.ToJsonString();
            if (!created && !JsonSameContent(json, a.Strategies)) { a.HealthStatus = "unknown"; a.HealthCheckedOn = null; }
            a.Strategies = json;
        }
        if (dto.Fingerprint != null) a.Fingerprint = dto.Fingerprint.ToJsonString();
        if (dto.ParentLevels != null) a.ParentLevels = dto.ParentLevels.Value;
        if (dto.ScreenshotId != null) a.ScreenshotId = dto.ScreenshotId;
        cache["id:" + a.Id] = a;
        if (a.Code != null) cache["code:" + a.Code] = a;
        return a;
    }

    private async Task ReplaceStepsAsync(GuideScenario sc, List<StepDto> dtos, GuideUser user, bool byIndex)
    {
        var existing = await _db.Steps.Where(x => x.ScenarioId == sc.Id).OrderBy(x => x.SortOrder).ToListAsync();
        var cache = new Dictionary<string, GuideAnchor>();
        var used = new HashSet<string>();
        for (var i = 0; i < dtos.Count; i++)
        {
            var d = dtos[i];
            GuideStep? st = null;
            if (!string.IsNullOrWhiteSpace(d.Id)) st = existing.FirstOrDefault(x => x.Id == d.Id && !used.Contains(x.Id));
            else if (byIndex && i < existing.Count && !used.Contains(existing[i].Id)) st = existing[i];
            if (st == null) { st = new GuideStep { ScenarioId = sc.Id }; _db.Steps.Add(st); }
            used.Add(st.Id);

            st.SortOrder = i;
            st.StepType = d.StepType; st.Title = d.Title; st.Body = d.Body; st.Route = d.Route;
            st.Highlight = d.Highlight; st.ParentLevels = d.ParentLevels; st.Rect = d.Rect?.ToJsonString();
            st.Padding = d.Padding; st.Placement = d.Placement; st.InputValue = d.InputValue; st.InputMode = d.InputMode;
            st.Expect = d.Expect; st.WaitRoute = d.WaitRoute; st.TimeoutMs = d.TimeoutMs; st.Optional = d.Optional;
            st.BlockOutside = d.BlockOutside; st.NarrationMediaId = d.NarrationMediaId; st.Transcript = d.Transcript;

            if (d.Anchor != null)
            {
                var a = await UpsertAnchorAsync(sc, d.Anchor, cache, byIndex ? $"{sc.Code ?? Short(sc.Id)}.s{i + 1}" : null);
                st.AnchorId = a?.Id;
            }
            else if (!string.IsNullOrWhiteSpace(d.AnchorId))
            {
                if (!cache.ContainsKey("id:" + d.AnchorId) && !await _db.Anchors.AnyAsync(x => x.Id == d.AnchorId && x.AppId == sc.AppId))
                    throw Bad($"Якір {d.AnchorId} не знайдено");
                st.AnchorId = d.AnchorId;
            }
            else st.AnchorId = null;
        }
        foreach (var gone in existing.Where(x => !used.Contains(x.Id))) _db.Steps.Remove(gone);
        sc.DraftVersion++;
    }

    public async Task<ScenarioDto> ReplaceStepsAsync(string id, List<StepDto> steps, GuideUser user)
    {
        Use(user);
        var (s, appCode) = await LoadScenarioAsync(id, user);
        await ReplaceStepsAsync(s, steps, user, false);
        await _db.SaveChangesAsync();
        return await LoadScenarioDtoAsync(s, appCode);
    }

    private async Task ReplaceVariablesAsync(GuideScenario sc, List<VariableDto> dtos)
    {
        var names = dtos.Select(v => v.Name?.Trim()).ToList();
        if (names.Any(string.IsNullOrWhiteSpace)) throw Bad("Ім'я змінної обов'язкове");
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count) throw Bad("Імена змінних мають бути унікальними");
        var existing = await _db.Variables.Where(v => v.ScenarioId == sc.Id).ToListAsync();
        var used = new HashSet<string>();
        for (var i = 0; i < dtos.Count; i++)
        {
            var d = dtos[i];
            var v = existing.FirstOrDefault(x => (d.Id != null && x.Id == d.Id) || (d.Id == null && x.Name == d.Name.Trim()));
            if (v == null) { v = new GuideScenarioVariable { ScenarioId = sc.Id }; _db.Variables.Add(v); }
            used.Add(v.Id);
            v.Name = d.Name.Trim(); v.Caption = d.Caption; v.Kind = d.Kind; v.DefaultValue = d.DefaultValue;
            v.Options = GuideJson.Stringify(d.Options, "{}"); v.SortOrder = i;
        }
        foreach (var gone in existing.Where(x => !used.Contains(x.Id))) _db.Variables.Remove(gone);
        sc.DraftVersion++;
    }

    public async Task<ScenarioDto> ReplaceVariablesAsync(string id, List<VariableDto> vars, GuideUser user)
    {
        Use(user);
        var (s, appCode) = await LoadScenarioAsync(id, user);
        await ReplaceVariablesAsync(s, vars);
        await _db.SaveChangesAsync();
        return await LoadScenarioDtoAsync(s, appCode);
    }

    // ------------------------------------------------------------------ публікація
    private static void StripVolatile(JsonNode? n)
    {
        if (n is JsonObject o)
        {
            foreach (var k in new[] { "id", "anchorId", "scenarioId", "healthStatus", "healthCheckedOn", "lastFoundHow", "screenshotId", "narrationMediaId", "sortOrder" }) o.Remove(k);
            foreach (var kv in o.ToList()) StripVolatile(kv.Value);
        }
        else if (n is JsonArray arr) foreach (var x in arr) StripVolatile(x);
    }

    private static string ContentHash(ScenarioDto dto)
    {
        var node = JsonSerializer.SerializeToNode(new { dto.Caption, dto.Description, dto.StartRoute, dto.RequiredRoles, dto.Tags, dto.EstMinutes, dto.Steps, dto.Variables }, GuideJson.Options);
        StripVolatile(node);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(node!.ToJsonString())));
    }

    public async Task<ScenarioDto> PublishAsync(string id, string? note, GuideUser user)
    {
        Use(user);
        var (s, appCode) = await LoadScenarioAsync(id, user);
        var dto = await LoadScenarioDtoAsync(s, appCode);
        if (dto.Steps.Count == 0) throw Bad("Не можна опублікувати інструкцію без кроків");
        var version = (await _db.Versions.Where(v => v.ScenarioId == s.Id).MaxAsync(v => (int?)v.VersionNo) ?? 0) + 1;
        dto.VersionNo = version;
        dto.Status = GuideConst.Published;
        dto.PublishedVersion = version;
        _db.Versions.Add(new GuideScenarioVersion
        {
            ScenarioId = s.Id, VersionNo = version, Snapshot = JsonSerializer.Serialize(dto, GuideJson.Options), Note = note,
            PublishedBy = user.EmployeeId, PublishedOn = DateTime.UtcNow
        });
        s.Status = GuideConst.Published;
        s.PublishedVersion = version;
        s.PublishedOn = DateTime.UtcNow;
        s.PublishedBy = user.EmployeeId;
        await _db.SaveChangesAsync();
        return await LoadScenarioDtoAsync(s, appCode);
    }

    public async Task<ScenarioDto> UnpublishAsync(string id, GuideUser user)
    {
        Use(user);
        var (s, appCode) = await LoadScenarioAsync(id, user);
        s.Status = GuideConst.Draft;
        await _db.SaveChangesAsync();
        return await LoadScenarioDtoAsync(s, appCode);
    }

    /// <summary>Опублікований знімок. Якорі підтягуються «живими» з gd_anchor, щоб прийняті виправлення діяли без перепублікації.</summary>
    public async Task<ScenarioDto> PlayAsync(string id, GuideUser user)
    {
        var (s, appCode) = await LoadScenarioAsync(id, user, false);
        if (s.Status != GuideConst.Published || s.PublishedVersion == null) throw NotFound("Опубліковану інструкцію");
        var ver = await _db.Versions.AsNoTracking().FirstOrDefaultAsync(v => v.ScenarioId == s.Id && v.VersionNo == s.PublishedVersion)
                  ?? throw NotFound("Версію інструкції");
        var dto = JsonSerializer.Deserialize<ScenarioDto>(ver.Snapshot, GuideJson.Options)!;
        dto.AppCode = appCode;
        dto.VersionNo = ver.VersionNo;
        var ids = dto.Steps.Where(x => x.AnchorId != null).Select(x => x.AnchorId!).Distinct().ToList();
        var live = await _db.Anchors.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id);
        foreach (var st in dto.Steps)
            if (st.AnchorId != null && live.TryGetValue(st.AnchorId, out var a)) st.Anchor = Map(a);
        return dto;
    }

    // ------------------------------------------------------------------ імпорт / експорт
    public async Task<GuideBundle> ExportAsync(string id, GuideUser user)
    {
        var (s, appCode) = await LoadScenarioAsync(id, user, false);
        var app = await _db.Apps.AsNoTracking().FirstAsync(a => a.Id == s.AppId);
        var dto = await LoadScenarioDtoAsync(s, appCode);
        var code = dto.Code ?? "s-" + Short(s.Id);
        dto.Code = code; dto.Id = null; dto.TenantId = null; dto.Status = GuideConst.Draft; dto.Publish = s.Status == GuideConst.Published;
        dto.PublishedVersion = null; dto.PublishedOn = null; dto.AppCode = null;
        var folders = new List<FolderDto>();
        var allFolders = await _db.Folders.AsNoTracking().Where(f => f.AppId == s.AppId).ToDictionaryAsync(f => f.Id);
        var chain = new List<GuideFolder>();
        for (var cur = s.FolderId; cur != null && allFolders.TryGetValue(cur, out var f); cur = f.ParentId) chain.Add(f);
        chain.Reverse();
        string? prevCode = null;
        foreach (var f in chain)
        {
            var fc = f.Code ?? "f-" + Short(f.Id);
            folders.Add(new FolderDto { Code = fc, ParentCode = prevCode, Caption = f.Caption, Description = f.Description, Icon = f.Icon, SortOrder = f.SortOrder });
            prevCode = fc;
        }
        dto.FolderCode = prevCode; dto.FolderId = null;
        foreach (var st in dto.Steps)
        {
            st.Id = null; st.AnchorId = null;
            if (st.Anchor != null)
            {
                st.Anchor.Code ??= "a-" + Short(st.Anchor.Id ?? Guid.NewGuid().ToString());
                st.Anchor.Id = null; st.Anchor.ScreenshotId = null; st.Anchor.HealthStatus = null; st.Anchor.HealthCheckedOn = null; st.Anchor.LastFoundHow = null;
            }
        }
        foreach (var v in dto.Variables) v.Id = null;
        return new GuideBundle { App = new AppDto { Code = app.Code, Caption = app.Caption }, Folders = folders, Scenarios = { dto } };
    }

    /// <summary>Ідемпотентний імпорт за кодами (папки, сценарії, якорі). Повторний імпорт без змін нічого не дублює і не перепубліковує.</summary>
    public async Task<ImportResult> ImportAsync(string appCode, GuideBundle bundle, GuideUser user)
    {
        Use(user);
        if (string.IsNullOrWhiteSpace(appCode)) throw Bad("appCode обов'язковий");
        var res = new ImportResult { AppCode = appCode };
        var app = await EnsureAppAsync(appCode, bundle.App?.Caption);
        await _db.SaveChangesAsync();

        // Папки: спочатку створення/оновлення, потім розстановка батьків
        var existingFolders = await _db.Folders.Where(f => f.AppId == app.Id && f.Code != null).ToListAsync();
        var byCode = existingFolders.ToDictionary(f => f.Code!, StringComparer.Ordinal);
        foreach (var fd in bundle.Folders)
        {
            if (string.IsNullOrWhiteSpace(fd.Code)) throw Bad("Для імпорту кожен розділ має мати code");
            if (!byCode.TryGetValue(fd.Code!, out var f))
            {
                f = new GuideFolder { AppId = app.Id, Code = fd.Code, TenantId = fd.TenantId };
                _db.Folders.Add(f); byCode[fd.Code!] = f; res.FoldersCreated++;
            }
            f.Caption = fd.Caption; f.Description = fd.Description; f.Icon = fd.Icon; f.SortOrder = fd.SortOrder;
        }
        foreach (var fd in bundle.Folders) byCode[fd.Code!].ParentId = fd.ParentCode != null && byCode.TryGetValue(fd.ParentCode, out var p) ? p.Id : null;
        await _db.SaveChangesAsync();

        foreach (var sd in bundle.Scenarios)
        {
            if (string.IsNullOrWhiteSpace(sd.Code)) throw Bad("Для імпорту кожна інструкція має мати code");
            var s = await _db.Scenarios.FirstOrDefaultAsync(x => x.AppId == app.Id && x.Code == sd.Code);
            var created = s == null;
            if (created) { s = new GuideScenario { AppId = app.Id }; _db.Scenarios.Add(s); res.ScenariosCreated++; } else res.ScenariosUpdated++;
            s!.FolderId = sd.FolderCode != null && byCode.TryGetValue(sd.FolderCode, out var fo) ? fo.Id : null;
            ApplyMeta(s, sd);
            await _db.SaveChangesAsync();
            await ReplaceStepsAsync(s, sd.Steps, user, true);
            await ReplaceVariablesAsync(s, sd.Variables);
            await _db.SaveChangesAsync();

            if (sd.Publish == true)
            {
                var dto = await LoadScenarioDtoAsync(s, appCode);
                var hash = ContentHash(dto);
                var last = await _db.Versions.AsNoTracking().Where(v => v.ScenarioId == s.Id).OrderByDescending(v => v.VersionNo).FirstOrDefaultAsync();
                var lastHash = last == null ? null : ContentHash(JsonSerializer.Deserialize<ScenarioDto>(last.Snapshot, GuideJson.Options)!);
                if (last == null || lastHash != hash || s.Status != GuideConst.Published)
                {
                    if (last == null || lastHash != hash || s.PublishedVersion == null) { await PublishAsync(s.Id, "Імпорт", user); res.ScenariosPublished++; }
                    else { s.Status = GuideConst.Published; await _db.SaveChangesAsync(); }
                }
            }
        }
        await ImportSetsAsync(app, bundle.Sets, res);
        return res;
    }

    // ------------------------------------------------------------------ якорі
    public async Task<IEnumerable<AnchorDto>> ListAnchorsAsync(string appCode, GuideUser user)
    {
        var app = await AppAsync(appCode);
        var list = await _db.Anchors.AsNoTracking().Where(a => a.AppId == app.Id).OrderBy(a => a.Caption).ToListAsync();
        return list.Where(a => TenantVisible(a.TenantId, user)).Select(Map).ToList();
    }

    public async Task<AnchorDto> UpdateAnchorAsync(string id, AnchorDto dto, GuideUser user)
    {
        Use(user);
        var a = await _db.Anchors.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFound("Якір");
        if (!TenantVisible(a.TenantId, user)) throw NotFound("Якір");
        if (!string.IsNullOrWhiteSpace(dto.Caption)) a.Caption = dto.Caption!;
        if (dto.Code != null) a.Code = NullIfEmpty(dto.Code);
        if (dto.RoutePattern != null) a.RoutePattern = dto.RoutePattern;
        if (dto.Strategies != null)
        {
            var json = dto.Strategies.ToJsonString();
            if (json != a.Strategies) { a.HealthStatus = "unknown"; a.HealthCheckedOn = null; }
            a.Strategies = json;
        }
        if (dto.Fingerprint != null) a.Fingerprint = dto.Fingerprint.ToJsonString();
        if (dto.ParentLevels != null) a.ParentLevels = dto.ParentLevels.Value;
        if (dto.ScreenshotId != null) a.ScreenshotId = dto.ScreenshotId;
        await _db.SaveChangesAsync();
        return Map(a);
    }

    /// <summary>Телеметрія плеєра. ok лише оновлює статус якоря; healed/missing зберігаються як звіти.</summary>
    public async Task<object> ReportAnchorAsync(AnchorReportDto dto, GuideUser user)
    {
        Use(user);
        var outcome = dto.Outcome?.ToLowerInvariant();
        if (outcome is not ("ok" or "healed" or "missing")) throw Bad("outcome має бути ok, healed або missing");
        var a = await _db.Anchors.FirstOrDefaultAsync(x => x.Id == dto.AnchorId) ?? throw NotFound("Якір");
        a.HealthStatus = outcome switch { "ok" => "ok", "healed" => "healed", _ => "broken" };
        a.LastFoundHow = dto.How;
        a.HealthCheckedOn = DateTime.UtcNow;
        string? reportId = null;
        if (outcome != "ok")
        {
            var r = new GuideAnchorReport
            {
                AnchorId = a.Id, ScenarioId = dto.ScenarioId, StepId = dto.StepId, Outcome = outcome, How = dto.How, Confidence = dto.Confidence, Route = dto.Route,
                NewStrategies = dto.NewStrategies?.ToJsonString(), NewFingerprint = dto.NewFingerprint?.ToJsonString(),
                EmployeeId = user.EmployeeId == GuideConst.EmptyGuid ? null : user.EmployeeId
            };
            _db.AnchorReports.Add(r);
            reportId = r.Id;
        }
        await _db.SaveChangesAsync();
        return new { reportId, anchorId = a.Id, healthStatus = a.HealthStatus };
    }

    public async Task<IEnumerable<object>> AnchorHealthAsync(string appCode, GuideUser user)
    {
        var app = await AppAsync(appCode);
        var anchors = (await _db.Anchors.AsNoTracking().Where(a => a.AppId == app.Id && (a.HealthStatus == "broken" || a.HealthStatus == "healed")).ToListAsync())
            .Where(a => TenantVisible(a.TenantId, user)).ToList();
        var ids = anchors.Select(a => a.Id).ToList();
        var reports = await _db.AnchorReports.AsNoTracking().Where(r => ids.Contains(r.AnchorId) && !r.Resolved).OrderByDescending(r => r.ReportedOn).ToListAsync();
        var stepUse = await _db.Steps.AsNoTracking().Where(s => s.AnchorId != null && ids.Contains(s.AnchorId)).Select(s => new { s.AnchorId, s.ScenarioId }).ToListAsync();
        return anchors.Select(a => (object)new
        {
            anchor = Map(a),
            scenarioIds = stepUse.Where(x => x.AnchorId == a.Id).Select(x => x.ScenarioId).Distinct().ToList(),
            reports = reports.Where(r => r.AnchorId == a.Id).Take(5).Select(r => new
            {
                r.Id, r.Outcome, r.How, r.Confidence, r.Route, r.ScenarioId, r.StepId, r.ReportedOn,
                newStrategies = GuideJson.Parse(r.NewStrategies), newFingerprint = GuideJson.Parse(r.NewFingerprint)
            })
        }).ToList();
    }

    public async Task<AnchorDto> AcceptReportAsync(string reportId, GuideUser user)
    {
        Use(user);
        var r = await _db.AnchorReports.FirstOrDefaultAsync(x => x.Id == reportId) ?? throw NotFound("Звіт");
        var a = await _db.Anchors.FirstOrDefaultAsync(x => x.Id == r.AnchorId) ?? throw NotFound("Якір");
        if (r.NewStrategies == null && r.NewFingerprint == null) throw Bad("Звіт не містить пропозиції оновлення");
        if (r.NewStrategies != null) a.Strategies = r.NewStrategies;
        if (r.NewFingerprint != null) a.Fingerprint = r.NewFingerprint;
        a.HealthStatus = "ok";
        a.HealthCheckedOn = DateTime.UtcNow;
        foreach (var other in await _db.AnchorReports.Where(x => x.AnchorId == a.Id && !x.Resolved).ToListAsync()) other.Resolved = true;
        await _db.SaveChangesAsync();
        return Map(a);
    }

    // ------------------------------------------------------------------ медіа
    public async Task<object> UploadMediaAsync(MediaUploadDto dto, GuideUser user, int maxBytes)
    {
        Use(user);
        byte[] bytes;
        try { bytes = Convert.FromBase64String(dto.Base64 ?? ""); } catch (FormatException) { throw Bad("Некоректний base64"); }
        if (bytes.Length == 0) throw Bad("Порожній файл");
        if (bytes.Length > maxBytes) throw new GuideException(413, "Файл завеликий");
        if (dto.Kind is not ("image" or "audio" or "video")) throw Bad("kind має бути image, audio або video");
        string? appId = null;
        if (!string.IsNullOrWhiteSpace(dto.AppCode)) appId = (await AppAsync(dto.AppCode!)).Id;
        var m = new GuideMedia { AppId = appId, Kind = dto.Kind, Mime = dto.Mime, FileName = dto.FileName, SizeBytes = bytes.Length, Content = bytes, Transcript = dto.Transcript, DurationMs = dto.DurationMs };
        _db.Media.Add(m);
        await _db.SaveChangesAsync();
        return new { id = m.Id, kind = m.Kind, mime = m.Mime, sizeBytes = m.SizeBytes };
    }

    public async Task<GuideMedia> GetMediaAsync(string id) =>
        await _db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id) ?? throw NotFound("Медіа");

    // ------------------------------------------------------------------ прогрес
    public async Task<GuideProgress> SaveProgressAsync(ProgressDto dto, GuideUser user)
    {
        Use(user);
        if (dto.Status is not ("started" or "completed" or "abandoned")) throw Bad("status має бути started, completed або abandoned");
        var sc = await _db.Scenarios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.ScenarioId) ?? throw NotFound("Інструкцію");
        var p = await _db.Progress.Where(x => x.ScenarioId == sc.Id && x.EmployeeId == user.EmployeeId && x.Status == "started")
            .OrderByDescending(x => x.StartedOn).FirstOrDefaultAsync();
        if (p == null)
        {
            p = new GuideProgress { ScenarioId = sc.Id, EmployeeId = user.EmployeeId, VersionNo = dto.VersionNo ?? sc.PublishedVersion ?? 0 };
            _db.Progress.Add(p);
        }
        p.Status = dto.Status;
        p.LastStep = dto.LastStep;
        p.Mode = dto.Mode;
        if (dto.Status == "completed") p.CompletedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return p;
    }

    public async Task<IEnumerable<GuideProgress>> MyProgressAsync(GuideUser user) =>
        await _db.Progress.AsNoTracking().Where(p => p.EmployeeId == user.EmployeeId).OrderByDescending(p => p.StartedOn).ToListAsync();
}
