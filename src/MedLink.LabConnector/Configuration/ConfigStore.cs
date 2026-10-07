// =============================================================================
// MedLink LIS Analyzer Connector — сховище конфігурації аналізаторів:
// сервер (GET /connector/config) → кеш data/config.cache.json → злиття з локальними
// визначеннями Analyzers із appsettings (локальні перемагають).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text.Json;
using MedLink.LabConnector.Server;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;
using Microsoft.Extensions.Options;

namespace MedLink.LabConnector.Configuration;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IOptionsMonitor<ConnectorOptions> _options;
    private readonly ConnectorPaths _paths;
    private readonly LisApiClient _api;
    private readonly ILogger<ConfigStore> _logger;
    private readonly object _lock = new();
    private ConnectorConfigDto _current = new();
    private ConnectorConfigDto? _server;

    public ConfigStore(IOptionsMonitor<ConnectorOptions> options, ConnectorPaths paths, LisApiClient api, ILogger<ConfigStore> logger)
    {
        _options = options;
        _paths = paths;
        _api = api;
        _logger = logger;
        _options.OnChange(_ => { Rebuild(); ConfigChanged?.Invoke(); });
    }

    /// <summary>Поточна об'єднана конфігурація.</summary>
    public ConnectorConfigDto Current { get { lock (_lock) return _current; } }
    public int ServerConfigVersion { get { lock (_lock) return _server?.ConfigVersion ?? 0; } }
    public DateTime? LastServerSyncAt { get; private set; }
    public string? LastServerError { get; private set; }

    public event Action? ConfigChanged;

    /// <summary>Початкове завантаження: кеш (якщо є) + локальні аналізатори; потім спроба оновити із сервера.</summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        _paths.EnsureDirectories();
        LoadCache();
        Rebuild();
        if (_options.CurrentValue.Server.IsConfigured)
            await RefreshFromServerAsync(ct);
    }

    /// <summary>Завантажує конфігурацію із сервера; при успіху зберігає кеш. Повертає true, якщо версія змінилась.</summary>
    public async Task<bool> RefreshFromServerAsync(CancellationToken ct)
    {
        if (!_options.CurrentValue.Server.IsConfigured) return false;
        try
        {
            var cfg = await _api.GetConfigAsync(ct);
            if (cfg == null) return false;
            bool changed;
            lock (_lock)
            {
                changed = _server == null || _server.ConfigVersion != cfg.ConfigVersion || !SameAnalyzers(_server, cfg);
                _server = cfg;
            }
            LastServerSyncAt = DateTime.UtcNow;
            LastServerError = null;
            SaveCache(cfg);
            Rebuild();
            if (changed)
            {
                _logger.LogInformation("Конфігурацію з сервера оновлено: версія {Version}, аналізаторів: {Count}", cfg.ConfigVersion, cfg.Analyzers.Count);
                ConfigChanged?.Invoke();
            }
            return changed;
        }
        catch (Exception ex)
        {
            LastServerError = ex.Message;
            _logger.LogWarning("Не вдалося отримати конфігурацію із сервера: {Error}. Працюємо за кешем.", ex.Message);
            return false;
        }
    }

    private static bool SameAnalyzers(ConnectorConfigDto a, ConnectorConfigDto b)
        => JsonSerializer.Serialize(a.Analyzers, JsonOpts) == JsonSerializer.Serialize(b.Analyzers, JsonOpts);

    private void LoadCache()
    {
        try
        {
            if (!File.Exists(_paths.ConfigCacheFile)) return;
            var json = File.ReadAllText(_paths.ConfigCacheFile);
            var cfg = JsonSerializer.Deserialize<ConnectorConfigDto>(json, JsonOpts);
            if (cfg != null)
            {
                lock (_lock) _server = cfg;
                _logger.LogInformation("Завантажено кеш конфігурації (версія {Version}, аналізаторів: {Count})", cfg.ConfigVersion, cfg.Analyzers.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Кеш конфігурації пошкоджено: {Error}", ex.Message);
        }
    }

    private void SaveCache(ConnectorConfigDto cfg)
    {
        try
        {
            File.WriteAllText(_paths.ConfigCacheFile, JsonSerializer.Serialize(cfg, JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Не вдалося зберегти кеш конфігурації: {Error}", ex.Message);
        }
    }

    /// <summary>Злиття: серверні аналізатори + локальні (локальні перекривають за AnalyzerId або Code).</summary>
    private void Rebuild()
    {
        var opts = _options.CurrentValue;
        var merged = new ConnectorConfigDto();
        lock (_lock)
        {
            var server = _server;
            merged.ConnectorId = opts.Server.ConnectorId ?? server?.ConnectorId ?? "";
            merged.ConfigVersion = server?.ConfigVersion ?? 0;
            merged.HeartbeatIntervalSec = server?.HeartbeatIntervalSec > 0 ? server.HeartbeatIntervalSec : opts.Local.HeartbeatIntervalSec;
            merged.PollIntervalSec = server?.PollIntervalSec > 0 ? server.PollIntervalSec : opts.Local.PendingOrdersPollSec;
            var list = new List<AnalyzerConfigDto>();
            if (server != null) list.AddRange(server.Analyzers.Select(Clone));
            foreach (var local in opts.Analyzers)
            {
                var l = Clone(local);
                if (string.IsNullOrWhiteSpace(l.AnalyzerId)) l.AnalyzerId = "local:" + (string.IsNullOrWhiteSpace(l.Code) ? Guid.NewGuid().ToString("N") : l.Code);
                int idx = list.FindIndex(a => (l.AnalyzerId.Length > 0 && a.AnalyzerId == l.AnalyzerId) || (l.Code.Length > 0 && string.Equals(a.Code, l.Code, StringComparison.OrdinalIgnoreCase)));
                if (idx >= 0) list[idx] = l; else list.Add(l);
            }
            foreach (var a in list) ApplyProfileDefaults(a);
            merged.Analyzers = list;
            _current = merged;
        }
    }

    public static void ApplyProfileDefaults(AnalyzerConfigDto a)
    {
        a.Connection ??= new AnalyzerConnectionDto();
        a.Framing ??= new AnalyzerFramingDto();
        a.ParameterMap ??= new List<AnalyzerParameterMapDto>();
        var profile = AnalyzerProfileCatalog.Default.GetByCode(a.TypeCode) ?? AnalyzerProfileCatalog.Default.GetByCode(a.Code);
        if (profile != null)
        {
            if (string.IsNullOrWhiteSpace(a.Name)) a.Name = profile.Name;
            profile.ApplyDefaults(a);
        }
        if (string.IsNullOrWhiteSpace(a.Protocol)) a.Protocol = "ASTM";
        if (a.Framing.MaxFrameLen <= 0) a.Framing.MaxFrameLen = AstmFrameCodec.DefaultMaxFrameLen;
    }

    private static AnalyzerConfigDto Clone(AnalyzerConfigDto src)
        => JsonSerializer.Deserialize<AnalyzerConfigDto>(JsonSerializer.Serialize(src, JsonOpts), JsonOpts)!;

    /// <summary>Записує data/appsettings.local.json після реєстрації.</summary>
    public static void WriteLocalSettings(ConnectorPaths paths, string baseUrl, string apiKey, string connectorId, string? name, int? statusPort)
    {
        paths.EnsureDirectories();
        var local = new Dictionary<string, object?>
        {
            ["Server"] = new Dictionary<string, object?> { ["BaseUrl"] = baseUrl, ["ApiKey"] = apiKey, ["ConnectorId"] = connectorId },
            ["Local"] = new Dictionary<string, object?> { ["ConnectorName"] = name, ["StatusPort"] = statusPort },
        };
        var json = JsonSerializer.Serialize(local, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        File.WriteAllText(paths.LocalSettingsFile, json);
    }
}
