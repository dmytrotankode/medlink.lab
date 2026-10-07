import os

base_dir = "C:/__MEDLINK___/LABA/MedLink.LabConnector"

csproj_content = """<Project Sdk="Microsoft.NET.Sdk.Worker">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>dotnet-MedLink.LabConnector-54a8df47-6889</UserSecretsId>
    <AssemblyName>MedLink.LabConnector</AssemblyName>
    <RootNamespace>MedLink.LabConnector</RootNamespace>
    <Company>MedLink LLC</Company>
    <Product>MedLink LIS Analyzer Driver Connector</Product>
    <Copyright>Copyright © 2026 MedLink. Всі права захищені.</Copyright>
    <Version>3.0.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="6.0.1" />
    <PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="6.0.1" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Systemd" Version="6.0.1" />
    <PackageReference Include="System.IO.Ports" Version="6.0.0" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="6.0.30" />
  </ItemGroup>
</Project>
"""

appsettings_content = """{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "MedLinkServer": {
    "ApiBaseUrl": "https://lis.medlink.ua/api/lab/analyzer",
    "ApiKey": "mlk_live_sec_77af092837bc449",
    "ClinicId": "c4d32a01-9876-4a1b-bf88-123456789abc",
    "HeartbeatIntervalSeconds": 30
  },
  "Analyzers": [
    {
      "Id": "018f4a12-8812-7001-a1b2-c3d4e5f60001",
      "Code": "SYSMEX_XN",
      "Name": "Sysmex XN-1000",
      "Protocol": "ASTM",
      "ConnectionMode": "TCP",
      "TcpHost": "192.168.1.101",
      "TcpPort": 5100,
      "IsServer": true,
      "AutoQueryOrders": true
    },
    {
      "Id": "018f4a12-8812-7001-a1b2-c3d4e5f60002",
      "Code": "COBAS_E411",
      "Name": "Roche Cobas e411",
      "Protocol": "ASTM",
      "ConnectionMode": "COM",
      "ComPort": "COM1",
      "BaudRate": 9600,
      "DataBits": 8,
      "Parity": "None",
      "StopBits": "One",
      "AutoQueryOrders": true
    },
    {
      "Id": "018f4a12-8812-7001-a1b2-c3d4e5f60003",
      "Code": "MINDRAY_BS240",
      "Name": "Mindray BS-240",
      "Protocol": "HL7",
      "ConnectionMode": "TCP",
      "TcpHost": "192.168.1.105",
      "TcpPort": 5600,
      "IsServer": false,
      "AutoQueryOrders": true
    }
  ]
}
"""

program_content = """// =============================================================================
// MedLink LIS: Driver & Analyzer Connector Background Service
// Cross-platform .NET Core daemon (Windows Service & Linux Systemd)
// Copyright (c) 2026 MedLink. Всі права належать MedLink.
// =============================================================================

using MedLink.LabConnector;
using MedLink.LabConnector.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "MedLinkLabConnector";
    })
    .UseSystemd()
    .ConfigureServices((hostContext, services) =>
    {
        services.AddSingleton<OfflineBufferQueue>();
        services.AddHttpClient("MedLinkApi", client =>
        {
            var baseUrl = hostContext.Configuration["MedLinkServer:ApiBaseUrl"];
            if (!string.IsNullOrEmpty(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
            client.DefaultRequestHeaders.Add("X-MedLink-ApiKey", hostContext.Configuration["MedLinkServer:ApiKey"]);
        });
        services.AddHostedService<LabConnectorWorker>();
    })
    .Build();

await host.RunAsync();
"""

worker_content = """// =============================================================================
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
"""

with open(f"{base_dir}/MedLink.LabConnector.csproj", "w", encoding="utf-8") as f: f.write(csproj_content)
with open(f"{base_dir}/appsettings.json", "w", encoding="utf-8") as f: f.write(appsettings_content)
with open(f"{base_dir}/Program.cs", "w", encoding="utf-8") as f: f.write(program_content)
with open(f"{base_dir}/LabConnectorWorker.cs", "w", encoding="utf-8") as f: f.write(worker_content)

print("Base files created successfully")
