// =============================================================================
// Клієнт API ЕСОЗ (eHealth) для лабораторії-виконавця. Шляхи та формати — як у evomis App.EhealthSdk
// (MedicalReferralEhealthService, EhealthMedicalReferralUse/CompletePrepareJsonService):
//   GET   patients/service_requests?requisition={номер}&status=active&page_size=&page=   — пошук направлення
//   PATCH patients/service_requests/{id}/actions/use      {used_by_employee, used_by_legal_entity[, program]}
//   PATCH patients/service_requests/{id}/actions/release  (без тіла)
//   PATCH patients/service_requests/{id}/actions/complete {completed_with, program_service}
//   GET   jobs/{jobId}                                     — статус асинхронної операції
// Режим Ehealth:Mode = Mock (типово) — запити обслуговує EhealthMockHandler (автономне тестування);
// Ehealth:Mode = Live — реальний Ehealth:BaseUrl з токеном доступу (в evomis — штатний EhealthSdkClient).
// =============================================================================
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;

namespace MedLink.LIS.Api.Services.Ehealth;

public sealed class EhealthOptions
{
    /// <summary>Mock | Live</summary>
    public string Mode { get; set; } = "Mock";
    public string BaseUrl { get; set; } = "https://api.ehealth.gov.ua/api/";
    /// <summary>Access token (у Live — з авторизації МІС; в evomis — EhealthSdkClient сам підставляє заголовки).</summary>
    public string? AccessToken { get; set; }
    public string? ApiKey { get; set; }
    public int PageSize { get; set; } = 50;
    public bool IsMock => !string.Equals(Mode, "Live", StringComparison.OrdinalIgnoreCase);
}

public sealed class EhealthException : Exception
{
    public int StatusCode { get; }
    public EhealthException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}

public sealed record EhealthServiceRequest(string Id, string Requisition, string Status, string? PatientId, string? ServiceCode, DateTime? ExpiresAt, string? UsedByLegalEntity);
public sealed record EhealthJob(string Id, string Status, string? EntityId);

public interface IEhealthClient
{
    Task<List<EhealthServiceRequest>> SearchServiceRequestsAsync(string requisition, string status = "active");
    Task<EhealthJob> UseServiceRequestAsync(string serviceRequestId, string employeeEhealthId, string legalEntityEhealthId, string? programEhealthId = null);
    Task<EhealthJob> ReleaseServiceRequestAsync(string serviceRequestId);
    Task<EhealthJob> CompleteServiceRequestAsync(string serviceRequestId, string completedWithType, string completedWithEhealthId, string? programServiceEhealthId = null);
    Task<EhealthJob> GetJobAsync(string jobId);
}

public sealed class EhealthClient : IEhealthClient
{
    public const string HttpClientName = "ehealth";
    private readonly IHttpClientFactory _factory;
    private readonly EhealthOptions _options;
    private readonly IServiceScopeFactory _scopes;

    public EhealthClient(IHttpClientFactory factory, EhealthOptions options, IServiceScopeFactory scopes) { _factory = factory; _options = options; _scopes = scopes; }

    /// <summary>Ідентифікатор eHealth у форматі evomis GetIdentifierWithoutText(system=eHealth/resources).</summary>
    public static JsonObject Identifier(string code, string value) => new()
    {
        ["identifier"] = new JsonObject
        {
            ["type"] = new JsonObject { ["coding"] = new JsonArray(new JsonObject { ["system"] = "eHealth/resources", ["code"] = code }) },
            ["value"] = value
        }
    };

    public async Task<List<EhealthServiceRequest>> SearchServiceRequestsAsync(string requisition, string status = "active")
    {
        var url = $"patients/service_requests?requisition={Uri.EscapeDataString(requisition)}&status={status}&page_size={_options.PageSize}&page=1";
        var json = await SendAsync(HttpMethod.Get, url, null);
        return (json["data"] as JsonArray ?? new JsonArray()).Select(d => new EhealthServiceRequest(
            d!["id"]!.GetValue<string>(), d["requisition"]?.GetValue<string>() ?? requisition, d["status"]?.GetValue<string>() ?? "",
            d["subject"]?["identifier"]?["value"]?.GetValue<string>(), d["code"]?["identifier"]?["value"]?.GetValue<string>(),
            d["expiration_date"] is JsonNode e ? DateTime.Parse(e.GetValue<string>()) : null,
            d["used_by_legal_entity"]?["identifier"]?["value"]?.GetValue<string>())).ToList();
    }

    public async Task<EhealthJob> UseServiceRequestAsync(string serviceRequestId, string employeeEhealthId, string legalEntityEhealthId, string? programEhealthId = null)
    {
        var body = new JsonObject { ["used_by_employee"] = Identifier("employee", employeeEhealthId), ["used_by_legal_entity"] = Identifier("legal_entity", legalEntityEhealthId) };
        if (programEhealthId != null) body["program"] = Identifier("medical_program", programEhealthId);
        return Job(await SendAsync(HttpMethod.Patch, $"patients/service_requests/{serviceRequestId}/actions/use", body));
    }

    public async Task<EhealthJob> ReleaseServiceRequestAsync(string serviceRequestId) =>
        Job(await SendAsync(HttpMethod.Patch, $"patients/service_requests/{serviceRequestId}/actions/release", null));

    public async Task<EhealthJob> CompleteServiceRequestAsync(string serviceRequestId, string completedWithType, string completedWithEhealthId, string? programServiceEhealthId = null)
    {
        var body = new JsonObject { ["completed_with"] = Identifier(completedWithType, completedWithEhealthId) };
        if (programServiceEhealthId != null) body["program_service"] = Identifier("program_service", programServiceEhealthId);
        return Job(await SendAsync(HttpMethod.Patch, $"patients/service_requests/{serviceRequestId}/actions/complete", body));
    }

    public async Task<EhealthJob> GetJobAsync(string jobId) => Job(await SendAsync(HttpMethod.Get, $"jobs/{jobId}", null));

    private static EhealthJob Job(JsonNode json)
    {
        var d = json["data"]!;
        return new EhealthJob(d["id"]!.GetValue<string>(), d["status"]?.GetValue<string>() ?? "pending", d["entity_id"]?.GetValue<string>());
    }

    private async Task<JsonNode> SendAsync(HttpMethod method, string relativeUrl, JsonObject? body)
    {
        var client = _factory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(method, relativeUrl);
        if (!string.IsNullOrEmpty(_options.AccessToken)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        if (!string.IsNullOrEmpty(_options.ApiKey)) req.Headers.Add("api-key", _options.ApiKey);
        var bodyText = body?.ToJsonString();
        if (bodyText != null) req.Content = new StringContent(bodyText, Encoding.UTF8, "application/json");
        var started = DateTime.UtcNow;
        HttpResponseMessage? resp = null;
        string text = "";
        try
        {
            resp = await client.SendAsync(req);
            text = await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { text = ex.Message; }
        await LogAsync(method.Method, relativeUrl, bodyText, (int?)resp?.StatusCode, text, started);
        if (resp == null) throw new EhealthException(503, $"ЕСОЗ недоступна: {text}");
        if (!resp.IsSuccessStatusCode)
        {
            string message;
            try { message = JsonNode.Parse(text)?["error"]?["message"]?.GetValue<string>() ?? text; } catch { message = text; }
            throw new EhealthException((int)resp.StatusCode, $"ЕСОЗ ({(int)resp.StatusCode}): {message}");
        }
        return JsonNode.Parse(text)!;
    }

    /// <summary>Журнал обміну з ЕСОЗ (окремий контекст — запис не залежить від транзакції операції).</summary>
    private async Task LogAsync(string method, string url, string? request, int? status, string response, DateTime started)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        db.EhealthExchangeLog.Add(new LabEhealthExchangeLog
        {
            Mode = _options.IsMock ? "MOCK" : "LIVE", Method = method, Url = url, RequestJson = request, StatusCode = status, ResponseJson = response.Length > 8000 ? response[..8000] : response,
            DurationMs = (int)(DateTime.UtcNow - started).TotalMilliseconds, At = started
        });
        await db.SaveChangesAsync();
    }
}
