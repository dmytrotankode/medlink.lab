// =============================================================================
// MedLink LIS Analyzer Connector — HTTP-клієнт API сервера ЛІС
// (/api/v1/lab/connector/*, заголовок X-MedLink-ApiKey). Див. docs/API_CONTRACT.md §3.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MedLink.LabConnector.Configuration;
using MedLink.LIS.Core.Contracts;
using Microsoft.Extensions.Options;

namespace MedLink.LabConnector.Server;

public sealed class LisApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly IOptionsMonitor<ConnectorOptions> _options;
    private readonly ILogger<LisApiClient> _logger;
    private HttpClient? _client;
    private string? _clientKey;
    private readonly object _lock = new();

    public LisApiClient(IOptionsMonitor<ConnectorOptions> options, ILogger<LisApiClient> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured => _options.CurrentValue.Server.IsConfigured;
    public string? BaseUrl => _options.CurrentValue.Server.BaseUrl;
    public DateTime? LastSuccessAt { get; private set; }
    public string? LastError { get; private set; }
    public bool IsOnline => LastSuccessAt.HasValue && (DateTime.UtcNow - LastSuccessAt.Value) < TimeSpan.FromMinutes(3) && LastError == null;

    /// <summary>Створює HttpClient для заданих параметрів (також для setup, коли ключа ще немає у конфігурації).</summary>
    public static HttpClient CreateClient(string baseUrl, string? apiKey, int timeoutSec = 30, bool allowInvalidCert = false)
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        if (allowInvalidCert) handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        var client = new HttpClient(handler) { BaseAddress = new Uri(ServerOptions.NormalizeBaseUrl(baseUrl)), Timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSec)) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"MedLink.LabConnector/{ConnectorInfo.Version}");
        if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.Add("X-MedLink-ApiKey", apiKey);
        return client;
    }

    private HttpClient Client
    {
        get
        {
            var s = _options.CurrentValue.Server;
            if (!s.IsConfigured) throw new InvalidOperationException("Сервер ЛІС не налаштовано (Server:BaseUrl / Server:ApiKey). Виконайте команду setup.");
            var key = $"{s.BaseUrl}|{s.ApiKey}|{s.TimeoutSec}|{s.AllowInvalidCertificate}";
            lock (_lock)
            {
                if (_client == null || _clientKey != key)
                {
                    _client?.Dispose();
                    _client = CreateClient(s.BaseUrl!, s.ApiKey, s.TimeoutSec, s.AllowInvalidCertificate);
                    _clientKey = key;
                }
                return _client;
            }
        }
    }

    // ---------- реєстрація ----------

    public static async Task<ConnectorRegisterResponse> RegisterAsync(string baseUrl, string installKey, string? name, bool allowInvalidCert, CancellationToken ct)
    {
        using var client = CreateClient(baseUrl, null, 30, allowInvalidCert);
        var req = new ConnectorRegisterRequest
        {
            InstallKey = installKey.Trim(),
            HostName = string.IsNullOrWhiteSpace(name) ? Environment.MachineName : $"{name} ({Environment.MachineName})",
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            Version = ConnectorInfo.Version,
        };
        using var resp = await client.PostAsJsonAsync("connector/register", req, Json, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Реєстрація відхилена: HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. {Shorten(body)}");
        }
        return await resp.Content.ReadFromJsonAsync<ConnectorRegisterResponse>(Json, ct)
               ?? throw new InvalidOperationException("Порожня відповідь сервера на реєстрацію");
    }

    // ---------- конфігурація / heartbeat ----------

    public Task<ConnectorConfigDto?> GetConfigAsync(CancellationToken ct)
        => Track(async () => await Client.GetFromJsonAsync<ConnectorConfigDto>("connector/config", Json, ct));

    public Task<ConnectorHeartbeatResponse?> HeartbeatAsync(ConnectorHeartbeatRequest req, CancellationToken ct)
        => Track(async () =>
        {
            using var resp = await Client.PostAsJsonAsync("connector/heartbeat", req, Json, ct);
            await EnsureSuccess(resp, ct);
            return await resp.Content.ReadFromJsonAsync<ConnectorHeartbeatResponse>(Json, ct);
        });

    // ---------- замовлення ----------

    /// <summary>Замовлення за штрихкодом; null, якщо 404.</summary>
    public Task<AnalyzerOrderDto?> GetOrderByBarcodeAsync(string barcode, string analyzerId, CancellationToken ct)
        => Track(async () =>
        {
            using var resp = await Client.GetAsync($"connector/orders/by-barcode/{Uri.EscapeDataString(barcode)}?analyzerId={Uri.EscapeDataString(analyzerId)}", ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            await EnsureSuccess(resp, ct);
            return await resp.Content.ReadFromJsonAsync<AnalyzerOrderDto>(Json, ct);
        });

    public Task<List<AnalyzerOrderDto>> GetPendingOrdersAsync(string analyzerId, CancellationToken ct)
        => Track(async () => await Client.GetFromJsonAsync<List<AnalyzerOrderDto>>($"connector/orders/pending?analyzerId={Uri.EscapeDataString(analyzerId)}", Json, ct) ?? new List<AnalyzerOrderDto>());

    // ---------- результати / журнал ----------

    public Task<AnalyzerResultsAcceptedDto?> PostResultsAsync(AnalyzerResultsBatchDto batch, CancellationToken ct)
        => Track(async () =>
        {
            using var resp = await Client.PostAsJsonAsync("connector/results", batch, Json, ct);
            await EnsureSuccess(resp, ct);
            if (resp.Content.Headers.ContentLength == 0) return new AnalyzerResultsAcceptedDto { Accepted = batch.Results.Count };
            try { return await resp.Content.ReadFromJsonAsync<AnalyzerResultsAcceptedDto>(Json, ct); }
            catch { return new AnalyzerResultsAcceptedDto { Accepted = batch.Results.Count }; }
        });

    /// <summary>Відправляє запис журналу обміну; повертає false при помилці (без виключення).</summary>
    public async Task<bool> PostMessageAsync(AnalyzerMessageLogDto msg, CancellationToken ct)
    {
        try
        {
            using var resp = await Client.PostAsJsonAsync("connector/messages", msg, Json, ct);
            await EnsureSuccess(resp, ct);
            MarkOk();
            return true;
        }
        catch (Exception ex)
        {
            MarkError(ex);
            return false;
        }
    }

    public async Task<bool> PostLogsAsync(ConnectorLogBatchDto batch, CancellationToken ct)
    {
        try
        {
            using var resp = await Client.PostAsJsonAsync("connector/logs", batch, Json, ct);
            await EnsureSuccess(resp, ct);
            MarkOk();
            return true;
        }
        catch (Exception ex)
        {
            MarkError(ex);
            return false;
        }
    }

    /// <summary>Сирий POST JSON (для повтору з офлайн-буфера).</summary>
    public async Task<HttpStatusCode> PostRawAsync(string relativePath, string json, CancellationToken ct)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var resp = await Client.PostAsync(relativePath, content, ct);
        if (resp.IsSuccessStatusCode) MarkOk(); else MarkError(new HttpRequestException($"HTTP {(int)resp.StatusCode}"));
        return resp.StatusCode;
    }

    // ---------- допоміжне ----------

    private async Task<T> Track<T>(Func<Task<T>> action)
    {
        try
        {
            var r = await action();
            MarkOk();
            return r;
        }
        catch (Exception ex)
        {
            MarkError(ex);
            throw;
        }
    }

    private void MarkOk() { LastSuccessAt = DateTime.UtcNow; LastError = null; }
    private void MarkError(Exception ex) { LastError = ex.Message; }

    private static async Task EnsureSuccess(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Shorten(body)}", null, resp.StatusCode);
    }

    private static string Shorten(string s) => s.Length > 300 ? s[..300] + "…" : s;
}

/// <summary>Інформація про збірку.</summary>
public static class ConnectorInfo
{
    public const string ServiceName = "MedLinkLabConnector";
    public const string DisplayName = "MedLink LIS Analyzer Connector";
    public static string Version => typeof(ConnectorInfo).Assembly.GetName().Version?.ToString(3) ?? "4.0.0";
    public static readonly DateTime StartedAtUtc = DateTime.UtcNow;
}
