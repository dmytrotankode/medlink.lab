// =============================================================================
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
