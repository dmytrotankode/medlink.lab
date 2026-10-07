// =============================================================================
// MedLink LIS Analyzer Connector — пакетне завантаження замовлень (batch download)
// для приладів без режиму запиту: опитування GET /connector/orders/pending.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using MedLink.LabConnector.Configuration;
using MedLink.LabConnector.Pipeline;
using MedLink.LabConnector.Server;

namespace MedLink.LabConnector.Services;

public sealed class PendingOrdersService : BackgroundService
{
    private readonly AnalyzerManager _manager;
    private readonly ResultPipeline _pipeline;
    private readonly LisApiClient _api;
    private readonly ConfigStore _config;
    private readonly ILogger<PendingOrdersService> _logger;

    public PendingOrdersService(AnalyzerManager manager, ResultPipeline pipeline, LisApiClient api, ConfigStore config, ILogger<PendingOrdersService> logger)
    {
        _manager = manager;
        _pipeline = pipeline;
        _api = api;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(10000, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            int interval = _config.Current.PollIntervalSec;
            if (interval <= 0) { try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); } catch { break; } continue; }
            if (_api.IsConfigured)
            {
                foreach (var session in _manager.Sessions)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    var cfg = session.Config;
                    // Пакетний режим — для приладів, що не запитують замовлення самі, і лише коли прилад підключено
                    if (cfg.AutoQueryOrders || !session.IsConnected) continue;
                    if (string.Equals(cfg.OrderTemplate, "NONE", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        var orders = await _pipeline.GetPendingAsync(cfg, stoppingToken);
                        if (orders.Count == 0) continue;
                        _logger.LogInformation("[{Code}] пакетне завантаження: {Count} замовлень", cfg.Code, orders.Count);
                        await session.SendOrdersAsync(orders, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("[{Code}] пакетне завантаження не вдалося: {Error}", cfg.Code, ex.Message);
                    }
                }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, interval)), stoppingToken); } catch { break; }
        }
    }
}
