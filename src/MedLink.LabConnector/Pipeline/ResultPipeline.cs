// =============================================================================
// MedLink LIS Analyzer Connector — конвеєр результатів: розібране повідомлення →
// мапінг parameterMap (factor/offset/unit) → POST /connector/results; при помилці —
// офлайн-буфер SQLite. Журнал обміну → /connector/messages (+ буфер) та data/logs/raw-*.
// Також реалізує IOrderSource (замовлення із сервера).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Globalization;
using System.Text.Json;
using MedLink.LabConnector.Logging;
using MedLink.LabConnector.Server;
using MedLink.LabConnector.Sessions;
using MedLink.LabConnector.Status;
using MedLink.LabConnector.Storage;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;

namespace MedLink.LabConnector.Pipeline;

public sealed class ResultPipeline : IInboundSink, IOrderSource
{
    private readonly LisApiClient _api;
    private readonly OfflineBuffer _buffer;
    private readonly StatusState _status;
    private readonly RawTrafficLog _rawLog;
    private readonly ILogger<ResultPipeline> _logger;

    public ResultPipeline(LisApiClient api, OfflineBuffer buffer, StatusState status, RawTrafficLog rawLog, ILogger<ResultPipeline> logger)
    {
        _api = api;
        _buffer = buffer;
        _status = status;
        _rawLog = rawLog;
        _logger = logger;
    }

    // ------------------------------------------------------------ IOrderSource

    public async Task<AnalyzerOrderDto?> GetByBarcodeAsync(string barcode, AnalyzerConfigDto cfg, CancellationToken ct)
    {
        if (!_api.IsConfigured) { _logger.LogWarning("[{Code}] сервер не налаштовано — замовлення за {Barcode} недоступне", cfg.Code, barcode); return null; }
        return await _api.GetOrderByBarcodeAsync(barcode, cfg.AnalyzerId, ct);
    }

    public async Task<IReadOnlyList<AnalyzerOrderDto>> GetPendingAsync(AnalyzerConfigDto cfg, CancellationToken ct)
    {
        if (!_api.IsConfigured) return Array.Empty<AnalyzerOrderDto>();
        return await _api.GetPendingOrdersAsync(cfg.AnalyzerId, ct);
    }

    // ------------------------------------------------------------ IInboundSink

    public async Task OnInboundAsync(AnalyzerConfigDto cfg, string raw, AnalyzerInboundMessage parsed, CancellationToken ct)
    {
        var error = parsed.Warnings.Count > 0 ? string.Join("; ", parsed.Warnings) : null;
        bool parsedOk = parsed.Kind != InboundMessageKind.Other || parsed.Warnings.Count == 0;
        var kind = parsed.Kind.ToString();
        _status.LogRaw(cfg, "IN", raw, parsedOk, kind, parsed.Results.Count, error);
        _logger.LogInformation("[{Code}] ← {Kind}: штрихкоди [{Barcodes}], результатів {Count}{Warn}", cfg.Code, kind, string.Join(",", parsed.Barcodes), parsed.Results.Count, error != null ? ", попередження: " + error : "");

        // Журнал обміну на сервер (не блокує обробку результатів)
        var log = new AnalyzerMessageLogDto
        {
            AnalyzerId = cfg.AnalyzerId,
            Direction = "IN",
            Protocol = cfg.Protocol,
            RawText = raw,
            ReceivedAt = DateTime.UtcNow,
            ParsedOk = parsedOk,
            ResultsCount = parsed.Results.Count,
            Error = error,
        };
        _ = PostMessageOrBufferAsync(cfg, log, ct);

        if (parsed.Kind == InboundMessageKind.Results && parsed.Results.Count > 0)
            await PostResultsAsync(cfg, parsed, ct);
    }

    public void OnOutbound(AnalyzerConfigDto cfg, string raw, string? note = null)
    {
        _status.LogRaw(cfg, "OUT", raw, true, note);
        _status.Update(cfg.AnalyzerId, s => s.OrdersSent++);
        _logger.LogInformation("[{Code}] → {Note}: {Preview}", cfg.Code, note ?? "відправлено", StatusState.Preview(raw, 200));
        var log = new AnalyzerMessageLogDto { AnalyzerId = cfg.AnalyzerId, Direction = "OUT", Protocol = cfg.Protocol, RawText = raw, ReceivedAt = DateTime.UtcNow, ParsedOk = true, Error = note != null && note.StartsWith("НЕ ВІДПРАВЛЕНО") ? note : null };
        _ = PostMessageOrBufferAsync(cfg, log, CancellationToken.None);
    }

    public void OnError(AnalyzerConfigDto cfg, string error)
    {
        _status.SetError(cfg.AnalyzerId, error);
        _logger.LogWarning("[{Code}] помилка: {Error}", cfg.Code, error);
    }

    public void OnConnection(AnalyzerConfigDto cfg, bool connected)
    {
        _status.SetConnected(cfg.AnalyzerId, connected);
        _status.AddMessage(new MessageLogEntry { At = DateTime.UtcNow, AnalyzerId = cfg.AnalyzerId, AnalyzerCode = cfg.Code, Direction = "SYS", Protocol = cfg.Protocol, Preview = connected ? "Прилад підключено" : "Прилад відключено", ParsedOk = true });
    }

    public void OnRawTraffic(AnalyzerConfigDto cfg, string direction, string raw) => _rawLog.Write(cfg.Code, direction, raw);

    // ------------------------------------------------------------ результати

    /// <summary>Будує пакет результатів із мапінгом параметрів.</summary>
    public static AnalyzerResultsBatchDto BuildBatch(AnalyzerConfigDto cfg, AnalyzerInboundMessage parsed, DateTime receivedAt)
    {
        var batch = new AnalyzerResultsBatchDto { AnalyzerId = cfg.AnalyzerId, ReceivedAt = receivedAt, IsQc = parsed.IsQc };
        var map = (cfg.ParameterMap ?? new()).Where(m => !string.IsNullOrWhiteSpace(m.AnalyzerCode)).ToList();
        foreach (var r in parsed.Results)
        {
            var item = new AnalyzerResultItemDto
            {
                Barcode = r.Barcode,
                AnalyzerCode = r.AnalyzerCode,
                Value = r.Value,
                Unit = r.Unit,
                Flags = r.Flags,
                MeasuredAt = r.MeasuredAt,
                ReferenceText = r.ReferenceText,
                QcLotNumber = r.QcLotNumber,
            };
            var m = map.FirstOrDefault(x => string.Equals(x.AnalyzerCode, r.AnalyzerCode, StringComparison.OrdinalIgnoreCase))
                    ?? map.FirstOrDefault(x => string.Equals(x.AnalyzerCode, r.AnalyzerCode.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
            if (m != null)
            {
                if ((Math.Abs(m.Factor - 1.0) > 1e-12 || Math.Abs(m.Offset) > 1e-12) && TryParseNumber(r.Value, out var v))
                {
                    var converted = v * m.Factor + m.Offset;
                    item.Value = converted.ToString("0.######", CultureInfo.InvariantCulture);
                }
                if (!string.IsNullOrWhiteSpace(m.UnitOverride)) item.Unit = m.UnitOverride;
            }
            else if (TryParseNumber(r.Value, out var v2) && r.Value.Contains(','))
            {
                item.Value = v2.ToString("0.######", CultureInfo.InvariantCulture);
            }
            batch.Results.Add(item);
        }
        return batch;
    }

    public static bool TryParseNumber(string? s, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().Replace(',', '.').TrimStart('<', '>', '=', ' ');
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private async Task PostResultsAsync(AnalyzerConfigDto cfg, AnalyzerInboundMessage parsed, CancellationToken ct)
    {
        var batch = BuildBatch(cfg, parsed, DateTime.UtcNow);
        var json = JsonSerializer.Serialize(batch, LisApiClient.Json);
        if (!_api.IsConfigured)
        {
            await _buffer.EnqueueAsync("results", "connector/results", cfg.AnalyzerId, json, "Сервер не налаштовано", ct);
            _status.BufferedCount = _buffer.Count;
            _logger.LogWarning("[{Code}] сервер не налаштовано — {Count} результат(ів) збережено у буфер", cfg.Code, batch.Results.Count);
            return;
        }
        try
        {
            var accepted = await _api.PostResultsAsync(batch, ct);
            _status.Update(cfg.AnalyzerId, s => s.ResultsSent += batch.Results.Count);
            if (accepted != null && accepted.Unmatched.Count > 0)
                _logger.LogWarning("[{Code}] сервер прийняв {Accepted}, не зіставлено {Unmatched}: {List}", cfg.Code, accepted.Accepted, accepted.Unmatched.Count, string.Join("; ", accepted.Unmatched.Select(u => $"{u.Barcode}/{u.AnalyzerCode}: {u.Reason}")));
            else
                _logger.LogInformation("[{Code}] результати відправлено: {Count} (штрихкоди {Barcodes})", cfg.Code, batch.Results.Count, string.Join(",", parsed.Barcodes));
        }
        catch (Exception ex)
        {
            await _buffer.EnqueueAsync("results", "connector/results", cfg.AnalyzerId, json, ex.Message, ct);
            _status.BufferedCount = _buffer.Count;
            _status.SetError(cfg.AnalyzerId, "Результати збережено у буфер: " + ex.Message);
            _logger.LogWarning("[{Code}] сервер недоступний ({Error}) — {Count} результат(ів) збережено в офлайн-буфер (у черзі: {Buffered})", cfg.Code, ex.Message, batch.Results.Count, _buffer.Count);
        }
    }

    private async Task PostMessageOrBufferAsync(AnalyzerConfigDto cfg, AnalyzerMessageLogDto log, CancellationToken ct)
    {
        try
        {
            if (!_api.IsConfigured) return;
            if (await _api.PostMessageAsync(log, ct)) return;
            await _buffer.EnqueueAsync("message", "connector/messages", cfg.AnalyzerId, JsonSerializer.Serialize(log, LisApiClient.Json), _api.LastError, ct);
            _status.BufferedCount = _buffer.Count;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("[{Code}] журнал обміну не відправлено: {Error}", cfg.Code, ex.Message);
        }
    }
}
