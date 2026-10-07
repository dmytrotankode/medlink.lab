// =============================================================================
// MedLink LIS: Connector Worker Service
// Copyright (c) 2026 MedLink. All rights reserved.
// =============================================================================

using System.Text.Json;
using MedLink.LabConnector.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedLink.LabConnector;

public class LabConnectorWorker : BackgroundService
{
    private readonly ILogger<LabConnectorWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OfflineBufferQueue _offlineQueue;
    private readonly List<IAnalyzerDriver> _drivers = new();

    public LabConnectorWorker(
        ILogger<LabConnectorWorker> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        OfflineBufferQueue offlineQueue)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _offlineQueue = offlineQueue;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=================================================");
        _logger.LogInformation(" MedLink LIS Analyzer Connector v3.0 Starting... ");
        _logger.LogInformation(" Copyright (c) 2026 MedLink. Всі права захищені. ");
        _logger.LogInformation("=================================================");

        await _offlineQueue.InitializeAsync();
        InitializeDrivers();

        await base.StartAsync(cancellationToken);
    }

    private void InitializeDrivers()
    {
        var analyzersSection = _configuration.GetSection("Analyzers").GetChildren();
        foreach (var sec in analyzersSection)
        {
            var protocol = sec["Protocol"];
            var code = sec["Code"];
            var name = sec["Name"];

            _logger.LogInformation("Configuring Analyzer Driver: {Name} [{Code}] ({Protocol})", name, code, protocol);

            if (string.Equals(protocol, "ASTM", StringComparison.OrdinalIgnoreCase))
            {
                var driver = new AstmDriver(sec, _httpClientFactory, _offlineQueue, _logger);
                _drivers.Add(driver);
            }
            else if (string.Equals(protocol, "HL7", StringComparison.OrdinalIgnoreCase))
            {
                var driver = new Hl7V2Driver(sec, _httpClientFactory, _offlineQueue, _logger);
                _drivers.Add(driver);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start all driver listener loops concurrently
        var driverTasks = _drivers.Select(d => d.StartListeningAsync(stoppingToken)).ToList();

        // Background loop for offline buffer synchronization & heartbeats
        var syncTask = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _offlineQueue.FlushPendingResultsAsync(_httpClientFactory.CreateClient("MedLinkApi"), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Offline buffer flush warning: {Message}", ex.Message);
                }
                await Task.Delay(15000, stoppingToken);
            }
        }, stoppingToken);

        await Task.WhenAll(driverTasks.Concat(new[] { syncTask }));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MedLink Analyzer Connector stopping drivers...");
        foreach (var driver in _drivers)
        {
            await driver.StopAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
