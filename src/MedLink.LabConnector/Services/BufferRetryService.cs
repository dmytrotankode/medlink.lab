// =============================================================================
// MedLink LIS Analyzer Connector — повторна відправка з офлайн-буфера:
// кожні 15 с, при послідовних невдачах — експоненційно до 5 хв.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Net;
using MedLink.LabConnector.Configuration;
using MedLink.LabConnector.Server;
using MedLink.LabConnector.Status;
using MedLink.LabConnector.Storage;
using Microsoft.Extensions.Options;

namespace MedLink.LabConnector.Services;

public sealed class BufferRetryService : BackgroundService
{
    private readonly OfflineBuffer _buffer;
    private readonly LisApiClient _api;
    private readonly StatusState _status;
    private readonly IOptionsMonitor<ConnectorOptions> _options;
    private readonly ILogger<BufferRetryService> _logger;

    public BufferRetryService(OfflineBuffer buffer, LisApiClient api, StatusState status, IOptionsMonitor<ConnectorOptions> options, ILogger<BufferRetryService> logger)
    {
        _buffer = buffer;
        _api = api;
        _status = status;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _buffer.InitializeAsync(stoppingToken);
        _status.BufferedCount = _buffer.Count;
        int failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var local = _options.CurrentValue.Local;
            var baseDelay = TimeSpan.FromSeconds(Math.Max(5, local.ResultRetrySec));
            var maxDelay = TimeSpan.FromSeconds(Math.Max(local.ResultRetrySec, local.ResultRetryMaxSec));
            var delay = failures == 0 ? baseDelay : TimeSpan.FromTicks(Math.Min(maxDelay.Ticks, baseDelay.Ticks * (1L << Math.Min(failures, 6))));
            try
            {
                if (_buffer.Count > 0 && _api.IsConfigured)
                {
                    var items = await _buffer.PeekAsync(50, stoppingToken);
                    if (items.Count > 0) _logger.LogInformation("Офлайн-буфер: спроба відправити {Count} елемент(ів)", items.Count);
                    bool anyFailed = false;
                    foreach (var item in items)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        try
                        {
                            var code = await _api.PostRawAsync(item.Path, item.Json, stoppingToken);
                            if (code is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.Accepted or HttpStatusCode.NoContent)
                            {
                                await _buffer.RemoveAsync(item.Id, stoppingToken);
                            }
                            else if ((int)code >= 400 && (int)code < 500 && code != HttpStatusCode.Unauthorized && code != HttpStatusCode.RequestTimeout && code != HttpStatusCode.TooManyRequests)
                            {
                                // Помилка даних — сервер не прийме ніколи; прибираємо, щоб не блокувати чергу
                                _logger.LogError("Офлайн-буфер: елемент #{Id} ({Kind}) відхилено сервером HTTP {Code} — видалено з черги", item.Id, item.Kind, (int)code);
                                await _buffer.RemoveAsync(item.Id, stoppingToken);
                            }
                            else
                            {
                                await _buffer.MarkFailedAsync(item.Id, $"HTTP {(int)code}", stoppingToken);
                                anyFailed = true;
                                break;
                            }
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                        catch (Exception ex)
                        {
                            await _buffer.MarkFailedAsync(item.Id, ex.Message, stoppingToken);
                            anyFailed = true;
                            _logger.LogWarning("Офлайн-буфер: сервер недоступний ({Error}); повтор через {Delay}", ex.Message, delay);
                            break;
                        }
                    }
                    failures = anyFailed ? failures + 1 : 0;
                    _status.BufferedCount = _buffer.Count;
                    if (!anyFailed && items.Count > 0) _logger.LogInformation("Офлайн-буфер: відправлено {Count}, залишилось {Left}", items.Count, _buffer.Count);
                }
                else failures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка циклу офлайн-буфера");
                failures++;
            }
            try { await Task.Delay(failures == 0 ? baseDelay : delay, stoppingToken); } catch { break; }
        }
    }
}
