// =============================================================================
// MedLink LIS Analyzer Connector — heartbeat на сервер (інтервал із конфігурації)
// та виконання команд сервера: RELOAD_CONFIG, RESTART, SEND_TEST_MESSAGE.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using MedLink.LabConnector.Configuration;
using MedLink.LabConnector.Server;
using MedLink.LabConnector.Status;
using MedLink.LabConnector.Storage;
using MedLink.LIS.Core.Contracts;

namespace MedLink.LabConnector.Services;

public sealed class HeartbeatService : BackgroundService
{
    private readonly LisApiClient _api;
    private readonly ConfigStore _config;
    private readonly StatusState _status;
    private readonly OfflineBuffer _buffer;
    private readonly AnalyzerManager _manager;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(LisApiClient api, ConfigStore config, StatusState status, OfflineBuffer buffer, AnalyzerManager manager, IHostApplicationLifetime lifetime, ILogger<HeartbeatService> logger)
    {
        _api = api;
        _config = config;
        _status = status;
        _buffer = buffer;
        _manager = manager;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(3000, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            int interval = Math.Max(10, _config.Current.HeartbeatIntervalSec);
            if (_api.IsConfigured)
            {
                try
                {
                    var req = new ConnectorHeartbeatRequest
                    {
                        Version = ConnectorInfo.Version,
                        UptimeSec = (long)(DateTime.UtcNow - ConnectorInfo.StartedAtUtc).TotalSeconds,
                        BufferedCount = _buffer.Count,
                        Analyzers = _status.ToHeartbeat(),
                    };
                    var resp = await _api.HeartbeatAsync(req, stoppingToken);
                    _status.LastHeartbeatAt = DateTime.UtcNow;
                    _status.Online = true;
                    _status.LastServerError = null;
                    if (resp != null)
                    {
                        if (resp.ConfigVersion != 0 && resp.ConfigVersion != _config.ServerConfigVersion)
                        {
                            _logger.LogInformation("Сервер повідомив нову версію конфігурації {New} (поточна {Cur}) — оновлюємо", resp.ConfigVersion, _config.ServerConfigVersion);
                            await _manager.ReloadAsync(stoppingToken);
                        }
                        foreach (var cmd in resp.Commands ?? new()) await ExecuteCommandAsync(cmd, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _status.Online = false;
                    _status.LastServerError = ex.Message;
                    _logger.LogWarning("Heartbeat не вдався: {Error}", ex.Message);
                }
            }
            else
            {
                _status.Online = false;
                _status.LastServerError = "Сервер не налаштовано (виконайте setup)";
            }
            _status.BufferedCount = _buffer.Count;
            try { await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken); } catch { break; }
        }
    }

    private async Task ExecuteCommandAsync(ConnectorCommandDto cmd, CancellationToken ct)
    {
        _logger.LogInformation("Команда сервера: {Type} {Payload}", cmd.Type, cmd.Payload);
        try
        {
            switch ((cmd.Type ?? "").ToUpperInvariant())
            {
                case "RELOAD_CONFIG":
                    _logger.LogInformation("RELOAD_CONFIG: {Result}", await _manager.ReloadAsync(ct));
                    break;
                case "RESTART":
                    _logger.LogWarning("RESTART: зупиняємо службу (менеджер служб перезапустить її)");
                    _lifetime.StopApplication();
                    break;
                case "SEND_TEST_MESSAGE":
                    _logger.LogInformation("SEND_TEST_MESSAGE: {Result}", await _manager.SendTestMessageAsync(cmd.Payload, ct));
                    break;
                default:
                    _logger.LogWarning("Невідома команда сервера: {Type}", cmd.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка виконання команди {Type}", cmd.Type);
        }
    }
}
