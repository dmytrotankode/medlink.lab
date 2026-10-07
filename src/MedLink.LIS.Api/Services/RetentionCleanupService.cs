// Фонове очищення: lab_analyzer_message (Lab:MessageRetentionDays, типово 90) та lab_connector_log (Lab:ConnectorLogRetentionDays, типово 90)
using MedLink.LIS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class RetentionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<RetentionCleanupService> _logger;

    public RetentionCleanupService(IServiceScopeFactory scopes, IConfiguration config, ILogger<RetentionCleanupService> logger)
    {
        _scopes = scopes; _config = config; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(5, _config.GetValue<int?>("Lab:RetentionCleanupIntervalMinutes") ?? 360));
        // перший запуск — через хвилину після старту, щоб не конкурувати із сідером
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Помилка очищення журналів за політикою зберігання"); }
            try { await Task.Delay(interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    public async Task<(int messages, int logs)> RunOnceAsync(CancellationToken ct = default)
    {
        var messageDays = _config.GetValue<int?>("Lab:MessageRetentionDays") ?? 90;
        var logDays = _config.GetValue<int?>("Lab:ConnectorLogRetentionDays") ?? 90;
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var now = DateTime.UtcNow;
        var messages = messageDays > 0 ? await db.AnalyzerMessages.Where(m => m.ReceivedAt < now.AddDays(-messageDays)).ExecuteDeleteAsync(ct) : 0;
        var logs = logDays > 0 ? await db.ConnectorLogs.Where(l => l.At < now.AddDays(-logDays)).ExecuteDeleteAsync(ct) : 0;
        if (messages + logs > 0) _logger.LogInformation("Очищення журналів: видалено {Messages} повідомлень аналізаторів та {Logs} записів логів коннектора", messages, logs);
        return (messages, logs);
    }
}
