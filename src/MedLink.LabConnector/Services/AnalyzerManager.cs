// =============================================================================
// MedLink LIS Analyzer Connector — менеджер сесій аналізаторів: створює/перезапускає
// сесії за конфігурацією, застосовує RELOAD_CONFIG, тести лінії.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Collections.Concurrent;
using System.Text.Json;
using MedLink.LabConnector.Configuration;
using MedLink.LabConnector.Pipeline;
using MedLink.LabConnector.Sessions;
using MedLink.LabConnector.Status;
using MedLink.LabConnector.Transport;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols.Parsers;
using Microsoft.Extensions.Options;

namespace MedLink.LabConnector.Services;

public sealed class AnalyzerManager : IHostedService
{
    private readonly ConfigStore _config;
    private readonly ResultPipeline _pipeline;
    private readonly StatusState _status;
    private readonly IOptionsMonitor<ConnectorOptions> _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AnalyzerManager> _logger;
    private readonly ConcurrentDictionary<string, (IAnalyzerSession Session, string Fingerprint)> _sessions = new();
    private readonly SemaphoreSlim _applyLock = new(1, 1);
    private CancellationTokenSource? _cts;

    public AnalyzerManager(ConfigStore config, ResultPipeline pipeline, StatusState status, IOptionsMonitor<ConnectorOptions> options, ILoggerFactory loggerFactory, ILogger<AnalyzerManager> logger)
    {
        _config = config;
        _pipeline = pipeline;
        _status = status;
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public IReadOnlyCollection<IAnalyzerSession> Sessions => _sessions.Values.Select(v => v.Session).ToList();

    public IAnalyzerSession? Find(string idOrCode)
        => _sessions.Values.Select(v => v.Session).FirstOrDefault(s => s.Config.AnalyzerId == idOrCode || string.Equals(s.Config.Code, idOrCode, StringComparison.OrdinalIgnoreCase));

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _logger.LogInformation("================================================================");
        _logger.LogInformation(" MedLink LIS Analyzer Connector v{Version} — запуск", Server.ConnectorInfo.Version);
        _logger.LogInformation(" ТОВ «МедЛінк» © 2026. Профілів приладів у каталозі: {Count}", MedLink.LIS.Core.Protocols.AnalyzerProfileCatalog.Default.Count);
        _logger.LogInformation("================================================================");
        await _config.InitializeAsync(cancellationToken);
        _config.ConfigChanged += OnConfigChanged;
        await ApplyConfigAsync(_cts.Token);
    }

    private void OnConfigChanged()
    {
        if (_cts == null) return;
        _ = Task.Run(() => ApplyConfigAsync(_cts.Token));
    }

    /// <summary>Синхронізує сесії з поточною конфігурацією: нові — запускає, змінені — перезапускає, видалені — зупиняє.</summary>
    public async Task ApplyConfigAsync(CancellationToken ct)
    {
        await _applyLock.WaitAsync(ct);
        try
        {
            var cfg = _config.Current;
            var wanted = cfg.Analyzers.Where(a => !string.IsNullOrWhiteSpace(a.AnalyzerId)).ToDictionary(a => a.AnalyzerId);
            // зупиняємо зайві / змінені
            foreach (var (id, entry) in _sessions.ToList())
            {
                if (!wanted.TryGetValue(id, out var a) || Fingerprint(a) != entry.Fingerprint)
                {
                    _logger.LogInformation("Зупинка сесії {Code} ({Reason})", entry.Session.Config.Code, wanted.ContainsKey(id) ? "конфігурацію змінено" : "аналізатор видалено");
                    _sessions.TryRemove(id, out _);
                    try { await entry.Session.DisposeAsync(); } catch (Exception ex) { _logger.LogWarning("Помилка зупинки сесії: {Error}", ex.Message); }
                    if (!wanted.ContainsKey(id)) _status.Remove(id);
                }
            }
            // запускаємо нові
            foreach (var a in wanted.Values)
            {
                if (_sessions.ContainsKey(a.AnalyzerId)) continue;
                var st = _status.Register(a);
                try
                {
                    var session = CreateSession(a);
                    await session.StartAsync(ct);
                    _sessions[a.AnalyzerId] = (session, Fingerprint(a));
                    _status.Update(a.AnalyzerId, s => { s.IsRunning = true; s.ParserKind = (session as SessionBase)?.ParserKind ?? ParserFactory.ForConfig(a).Kind; s.OrderTemplate = (session as SessionBase)?.OrderTemplate ?? a.OrderTemplate; s.LastError = null; });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Не вдалося запустити сесію {Code}: {Error}", a.Code, ex.Message);
                    _status.Update(a.AnalyzerId, s => { s.IsRunning = false; s.LastError = "Запуск не вдався: " + ex.Message; s.LastErrorAt = DateTime.UtcNow; });
                }
            }
            if (wanted.Count == 0) _logger.LogWarning("У конфігурації немає аналізаторів. Додайте їх у ЛІС (Аналізатори → Коннектор) або у appsettings.json → Analyzers.");
        }
        finally { _applyLock.Release(); }
    }

    private IAnalyzerSession CreateSession(AnalyzerConfigDto a)
    {
        var local = _options.CurrentValue.Local;
        var logger = _loggerFactory.CreateLogger($"Analyzer.{a.Code}");
        var transport = TransportFactory.Create(a, new TransportOptions { ReconnectSec = local.TcpReconnectSec, KeepAliveSec = local.TcpKeepAliveSec }, logger);
        var options = new Sessions.SessionOptions { AstmTimeoutSec = local.AstmTimeoutSec, AstmMaxRetries = local.AstmMaxRetries, PacketIdleMs = local.PacketIdleMs, SendNoOrderReply = local.SendNoOrderReply };
        return SessionFactory.Create(a, transport, _pipeline, _pipeline, options, logger);
    }

    private static string Fingerprint(AnalyzerConfigDto a) => JsonSerializer.Serialize(a, Server.LisApiClient.Json);

    /// <summary>Перечитати конфігурацію із сервера та застосувати.</summary>
    public async Task<string> ReloadAsync(CancellationToken ct)
    {
        var changed = await _config.RefreshFromServerAsync(ct);
        await ApplyConfigAsync(ct);
        return _config.LastServerError != null
            ? $"Сервер недоступний ({_config.LastServerError}); застосовано кеш/локальну конфігурацію"
            : changed ? $"Конфігурацію оновлено (версія {_config.ServerConfigVersion})" : "Конфігурація без змін";
    }

    /// <summary>Тестове повідомлення: перевірка зв'язку із сервером та (за наявності) тест лінії приладу.</summary>
    public async Task<string> SendTestMessageAsync(string? analyzerIdOrCode, CancellationToken ct)
    {
        var parts = new List<string>();
        var session = string.IsNullOrWhiteSpace(analyzerIdOrCode) ? _sessions.Values.Select(v => v.Session).FirstOrDefault() : Find(analyzerIdOrCode);
        if (session != null)
        {
            var link = await session.LinkTestAsync(ct);
            parts.Add($"{session.Config.Code}: {link}");
            _pipeline.OnOutbound(session.Config, "TEST", "тестове повідомлення");
        }
        else parts.Add("Аналізатор не знайдено або сесій немає");
        return string.Join(" | ", parts);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _config.ConfigChanged -= OnConfigChanged;
        _cts?.Cancel();
        foreach (var (_, entry) in _sessions.ToList())
        {
            try { await entry.Session.DisposeAsync(); } catch { }
        }
        _sessions.Clear();
        _logger.LogInformation("Усі сесії аналізаторів зупинено");
    }
}
