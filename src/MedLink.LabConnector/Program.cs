// =============================================================================
// MedLink LIS Analyzer Connector — точка входу: CLI-команди та запуск служби
// (Windows Service / systemd / консоль) з локальною сторінкою статусу (Kestrel).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using MedLink.LabConnector.Cli;
using MedLink.LabConnector.Configuration;
using MedLink.LabConnector.Logging;
using MedLink.LabConnector.Pipeline;
using MedLink.LabConnector.Server;
using MedLink.LabConnector.Services;
using MedLink.LabConnector.Status;
using MedLink.LabConnector.Storage;
using Microsoft.Extensions.Options;

namespace MedLink.LabConnector;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var cli = CommandLine.Parse(args);
        try
        {
            switch (cli.Command)
            {
                case "run": return await RunServiceAsync(cli);
                case "setup": return await Commands.SetupAsync(cli);
                case "test": return Commands.Test(cli);
                case "preview-order": return Commands.PreviewOrder(cli);
                case "status": return await Commands.StatusAsync(cli);
                case "install-service": return Commands.InstallService(cli);
                case "uninstall-service": return Commands.UninstallService(cli);
                case "list-profiles": return Commands.ListProfiles(cli);
                case "version": Console.WriteLine($"MedLink LIS Analyzer Connector {ConnectorInfo.Version}"); return 0;
                case "help": case "--help": case "-h": Commands.PrintHelp(); return 0;
                default:
                    Console.Error.WriteLine($"Невідома команда: {cli.Command}");
                    Commands.PrintHelp();
                    return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ПОМИЛКА: " + ex.Message);
            return 1;
        }
    }

    private static async Task<int> RunServiceAsync(CommandLine cli)
    {
        var paths = ConnectorPaths.FromEnvironment(cli.Get("data-dir"));
        paths.EnsureDirectories();

        var options = new WebApplicationOptions
        {
            Args = cli.Passthrough,
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = "MedLink.LabConnector",
        };
        var builder = WebApplication.CreateBuilder(options);
        // Налаштування принтера, записані інсталятором (Inno Setup / install-windows.ps1)
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Printing.json"), optional: true, reloadOnChange: true);
        builder.Configuration.AddJsonFile(paths.LocalSettingsFile, optional: true, reloadOnChange: true);
        builder.Configuration.AddEnvironmentVariables("MEDLINK_");

        builder.Host.UseWindowsService(o => o.ServiceName = ConnectorInfo.ServiceName);
        builder.Host.UseSystemd();

        var local = builder.Configuration.GetSection("Local").Get<LocalOptions>() ?? new LocalOptions();
        int port = int.TryParse(cli.Get("status-port"), out var p) ? p : local.StatusPort;
        builder.WebHost.UseUrls($"http://localhost:{port}", $"http://127.0.0.1:{port}");
        builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);

        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddSimpleConsole(o => { o.TimestampFormat = "HH:mm:ss "; o.SingleLine = true; });
        if (OperatingSystem.IsWindows() && Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService())
            builder.Logging.AddEventLog(o => o.SourceName = ConnectorInfo.DisplayName);
        builder.Logging.AddProvider(new RollingFileLoggerProvider(paths.LogsDir, local.LogRetentionDays));
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);

        builder.Services.Configure<ConnectorOptions>(builder.Configuration);
        builder.Services.AddSingleton(paths);
        builder.Services.AddSingleton<StatusState>();
        builder.Services.AddSingleton(sp => new RawTrafficLog(paths.LogsDir, local.LogRetentionDays, local.LogRawTraffic));
        builder.Services.AddSingleton<LisApiClient>();
        builder.Services.AddSingleton<OfflineBuffer>();
        builder.Services.AddSingleton<ConfigStore>();
        builder.Services.AddSingleton<ResultPipeline>();
        builder.Services.AddSingleton<AnalyzerManager>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<AnalyzerManager>());
        builder.Services.AddHostedService<BufferRetryService>();
        builder.Services.AddHostedService<HeartbeatService>();
        builder.Services.AddHostedService<PendingOrdersService>();
        builder.Services.AddSingleton<Printing.LabelPrintService>();
        // SPA ЛІС (інший хост) звертається до localhost:5088 для друку етикеток — дозволяємо будь-яке походження
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        var app = builder.Build();
        app.UseCors();
        StatusEndpoints.Map(app);
        Printing.PrintEndpoints.Map(app);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MedLink.LabConnector");
        var opts = app.Services.GetRequiredService<IOptionsMonitor<ConnectorOptions>>().CurrentValue;
        logger.LogInformation("Каталог даних: {DataDir}; сторінка статусу: http://localhost:{Port}/", paths.DataDir, port);
        if (!opts.Server.IsConfigured)
            logger.LogWarning("Сервер ЛІС не налаштовано. Виконайте: MedLink.LabConnector setup --server https://lis.example.ua --install-key XXXX");

        try
        {
            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Службу зупинено через помилку: {Error}", ex.Message);
            return 1;
        }
    }
}
