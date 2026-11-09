// =============================================================================
// Клієнт API ЕСОЗ (eHealth) для лабораторії-виконавця. Шляхи — як в OpenAPI eHealth (medical-events.json), одна константа на операцію:
//   GET   /api/service_requests?requisition=&status=&page=&page_size=                  — пошук направлення (service_request:read)
//   PATCH /api/service_requests/{id}/actions/use      {used_by_legal_entity, used_by_employee[, used_by_division, program]}
//   PATCH /api/service_requests/{id}/actions/release  (позначено в OpenAPI «Will be deprecated!»)
//   PATCH /api/service_requests/{id}/actions/complete {completed_with[, program_service, status_reason]}
//   POST  /api/patients/{patient_id}/diagnostic_report_package   {signed_data}         — Submit Diagnostic Report Package (202 + job)
//   PATCH /api/patients/{patient_id}/diagnostic_report_package   {signed_data}         — Cancel Diagnostic Report Package (202 + job)
//   GET   /api/patients/{patient_id}/diagnostic_reports/{id}                           — перевірка перед повтором
//   GET   /api/jobs/{id}                                                               — стан асинхронної операції (job:read)
// Режим Ehealth:Mode: Mock (типово, EhealthMockHandler), Replay (EhealthReplayHandler), Live (реальний Ehealth:BaseUrl;
// кожна операція вмикається прапорцем Ehealth:Live:Operations). Повтори з витримкою — лише для ідемпотентних GET;
// записуючі виклики повторює outbox-воркер (див. EhealthOutboxEngine), бо повтор PATCH/POST без перевірки небезпечний.
// =============================================================================
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;

namespace MedLink.LIS.Api.Services.Ehealth;

public interface IEhealthTokenProvider
{
    /// <summary>Access token поточного користувача/закладу; null — не налаштовано.</summary>
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
}

/// <summary>Статичний токен із конфігурації (Mock/dev). Для Live призначений OAuth-провайдер per-user (етап 6 плану, потребує preprod).</summary>
public sealed class StaticEhealthTokenProvider : IEhealthTokenProvider
{
    private readonly EhealthOptions _options;
    public StaticEhealthTokenProvider(EhealthOptions options) => _options = options;
    public Task<string?> GetAccessTokenAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>(!string.IsNullOrWhiteSpace(_options.AccessToken) ? _options.AccessToken : _options.IsLive ? null : "mock-access-token");
}

public sealed record EhealthExchangeEntry(string Method, string Url, string? Request, int? Status, string Response, DateTime Started, int DurationMs);

public interface IEhealthExchangeLog
{
    Task WriteAsync(EhealthExchangeEntry entry);
}

/// <summary>Журнал обміну в БД (окремий контекст — запис не залежить від транзакції операції).</summary>
public sealed class DbEhealthExchangeLog : IEhealthExchangeLog
{
    private readonly IServiceScopeFactory _scopes;
    private readonly EhealthOptions _options;
    public DbEhealthExchangeLog(IServiceScopeFactory scopes, EhealthOptions options) { _scopes = scopes; _options = options; }

    public async Task WriteAsync(EhealthExchangeEntry e)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        db.EhealthExchangeLog.Add(new LabEhealthExchangeLog
        {
            Mode = _options.ModeName, Method = e.Method, Url = e.Url.Length > 512 ? e.Url[..512] : e.Url, RequestJson = e.Request, StatusCode = e.Status,
            ResponseJson = e.Response.Length > 8000 ? e.Response[..8000] : e.Response, DurationMs = e.DurationMs, At = e.Started
        });
        await db.SaveChangesAsync();
    }
}

public interface IEhealthClient
{
    /// <summary>Пошук е-направлення за номером. status порожній — без фільтра за статусом.</summary>
    Task<List<EhealthServiceRequest>> SearchServiceRequestsAsync(string requisition, string status = "active", CancellationToken ct = default);
    Task<EhealthJob> UseServiceRequestAsync(string serviceRequestId, string employeeEhealthId, string legalEntityEhealthId, string? programEhealthId = null, string? divisionEhealthId = null, CancellationToken ct = default);
    Task<EhealthJob> ReleaseServiceRequestAsync(string serviceRequestId, CancellationToken ct = default);
    Task<EhealthJob> CompleteServiceRequestAsync(string serviceRequestId, string completedWithType, string completedWithEhealthId, string? programServiceEhealthId = null, string? statusReason = "default", CancellationToken ct = default);
    Task<EhealthJob> SubmitDiagnosticReportPackageAsync(string patientId, string signedData, CancellationToken ct = default);
    Task<EhealthJob> CancelDiagnosticReportPackageAsync(string patientId, string signedData, CancellationToken ct = default);
    /// <summary>Діагностичний звіт за id; null — 404 (ще не зареєстровано).</summary>
    Task<EhealthDiagnosticReport?> GetDiagnosticReportAsync(string patientId, string reportId, CancellationToken ct = default);
    Task<EhealthJob> GetJobAsync(string jobId, CancellationToken ct = default);
    /// <summary>Чекає завершення job обмежений час (синхронні сценарії); повертає останній відомий стан.</summary>
    Task<EhealthJob> WaitForJobAsync(EhealthJob job, CancellationToken ct = default);
}

public sealed class EhealthClient : IEhealthClient
{
    public const string HttpClientName = "ehealth";
    private readonly IHttpClientFactory _factory;
    private readonly EhealthOptions _options;
    private readonly IEhealthExchangeLog _log;
    private readonly IEhealthTokenProvider _tokens;

    public EhealthClient(IHttpClientFactory factory, EhealthOptions options, IEhealthExchangeLog log, IEhealthTokenProvider tokens)
    {
        _factory = factory; _options = options; _log = log; _tokens = tokens;
    }

    /// <summary>Ідентифікатор eHealth у форматі SDK (system=eHealth/resources).</summary>
    public static JsonObject Identifier(string code, string value) => EhealthRef.Identifier(code, value);

    // ------------------------------------------------------------------ операції
    public async Task<List<EhealthServiceRequest>> SearchServiceRequestsAsync(string requisition, string status = "active", CancellationToken ct = default)
    {
        var url = $"service_requests?requisition={Uri.EscapeDataString(requisition)}" + (string.IsNullOrEmpty(status) ? "" : $"&status={Uri.EscapeDataString(status)}") + $"&page=1&page_size={_options.PageSize}";
        var r = await SendAsync(HttpMethod.Get, url, null, EhealthOperation.SearchRead, idempotent: true, ct: ct);
        var list = new List<EhealthServiceRequest>();
        foreach (var d in (r.Json?["data"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()) list.Add(ReadServiceRequest(d, requisition));
        return list;
    }

    public async Task<EhealthJob> UseServiceRequestAsync(string serviceRequestId, string employeeEhealthId, string legalEntityEhealthId, string? programEhealthId = null, string? divisionEhealthId = null, CancellationToken ct = default)
    {
        var body = new JsonObject { ["used_by_legal_entity"] = Identifier("legal_entity", legalEntityEhealthId), ["used_by_employee"] = Identifier("employee", employeeEhealthId) };
        if (divisionEhealthId != null) body["used_by_division"] = Identifier("division", divisionEhealthId);
        if (programEhealthId != null) body["program"] = Identifier("medical_program", programEhealthId);
        var r = await SendAsync(HttpMethod.Patch, $"service_requests/{Uri.EscapeDataString(serviceRequestId)}/actions/use", body, EhealthOperation.Use, false, ct: ct);
        return EhealthJobReader.ParseAccepted(r.Json, r.Status);
    }

    public async Task<EhealthJob> ReleaseServiceRequestAsync(string serviceRequestId, CancellationToken ct = default)
    {
        var r = await SendAsync(HttpMethod.Patch, $"service_requests/{Uri.EscapeDataString(serviceRequestId)}/actions/release", null, EhealthOperation.Use, false, ct: ct);
        return EhealthJobReader.ParseAccepted(r.Json, r.Status);
    }

    public async Task<EhealthJob> CompleteServiceRequestAsync(string serviceRequestId, string completedWithType, string completedWithEhealthId, string? programServiceEhealthId = null, string? statusReason = "default", CancellationToken ct = default)
    {
        var body = new JsonObject { ["completed_with"] = Identifier(completedWithType, completedWithEhealthId) };
        if (programServiceEhealthId != null) body["program_service"] = Identifier("program_service", programServiceEhealthId);
        if (!string.IsNullOrEmpty(statusReason)) body["status_reason"] = EhealthRef.Coding("eHealth/service_request_complete_reasons", statusReason);
        var r = await SendAsync(HttpMethod.Patch, $"service_requests/{Uri.EscapeDataString(serviceRequestId)}/actions/complete", body, EhealthOperation.Complete, false, ct: ct);
        return EhealthJobReader.ParseAccepted(r.Json, r.Status);
    }

    public async Task<EhealthJob> SubmitDiagnosticReportPackageAsync(string patientId, string signedData, CancellationToken ct = default)
    {
        var body = new JsonObject { ["signed_data"] = signedData };
        var r = await SendAsync(HttpMethod.Post, $"patients/{Uri.EscapeDataString(patientId)}/diagnostic_report_package", body, EhealthOperation.SubmitDr, false, ct: ct);
        return EhealthJobReader.ParseAccepted(r.Json, r.Status);
    }

    public async Task<EhealthJob> CancelDiagnosticReportPackageAsync(string patientId, string signedData, CancellationToken ct = default)
    {
        var body = new JsonObject { ["signed_data"] = signedData };
        var r = await SendAsync(HttpMethod.Patch, $"patients/{Uri.EscapeDataString(patientId)}/diagnostic_report_package", body, EhealthOperation.CancelDr, false, ct: ct);
        return EhealthJobReader.ParseAccepted(r.Json, r.Status);
    }

    public async Task<EhealthDiagnosticReport?> GetDiagnosticReportAsync(string patientId, string reportId, CancellationToken ct = default)
    {
        try
        {
            var r = await SendAsync(HttpMethod.Get, $"patients/{Uri.EscapeDataString(patientId)}/diagnostic_reports/{Uri.EscapeDataString(reportId)}", null, EhealthOperation.SubmitDr, true, ct: ct);
            var d = r.Json?["data"] as JsonObject;
            return d == null ? null : new EhealthDiagnosticReport(Str(d["id"]) ?? reportId, Str(d["status"]) ?? "", patientId);
        }
        catch (EhealthException ex) when (ex.Kind == EhealthErrorKind.NotFound) { return null; }
    }

    public async Task<EhealthJob> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        // GET /api/jobs/{id}: 200 pending, 303 processed, 4xx/5xx з тілом {data:{status:"failed",status_code,error}} — це результат job, а не помилка транспорту
        try
        {
            var r = await SendAsync(HttpMethod.Get, $"jobs/{Uri.EscapeDataString(jobId)}", null, null, true, ct: ct);
            var job = EhealthJobReader.Parse(r.Json, r.Status);
            return string.IsNullOrEmpty(job.Id) ? job with { Id = jobId } : job;
        }
        catch (EhealthException ex) when (ex.RawBody != null && ex.Kind is EhealthErrorKind.Validation or EhealthErrorKind.Conflict)
        {
            JsonNode? body = null; try { body = JsonNode.Parse(ex.RawBody!); } catch { /* не JSON */ }
            var job = EhealthJobReader.Parse(body, ex.StatusCode);
            if (job.IsFailed || job.StatusCode != null)
                return job with { Id = string.IsNullOrEmpty(job.Id) ? jobId : job.Id, Status = EhealthJobStatus.Failed, StatusCode = job.StatusCode ?? ex.StatusCode, Error = job.Error ?? ex.Message, Invalid = job.Invalid?.Count > 0 ? job.Invalid : ex.Invalid };
            throw;
        }
    }

    public async Task<EhealthJob> WaitForJobAsync(EhealthJob job, CancellationToken ct = default)
    {
        if (!job.IsPending || string.IsNullOrEmpty(job.Id)) return job;
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(0, _options.Http.SyncJobWaitSeconds));
        var delays = _options.Http.SyncJobPollDelaysMs.Length == 0 ? new[] { 300, 700, 1500, 3000, 5000 } : _options.Http.SyncJobPollDelaysMs;
        for (var i = 0; DateTime.UtcNow < deadline; i++)
        {
            await Task.Delay(delays[Math.Min(i, delays.Length - 1)], ct);
            job = await GetJobAsync(job.Id, ct);
            if (!job.IsPending) break;
        }
        return job;
    }

    // ------------------------------------------------------------------ розбір
    private static EhealthServiceRequest ReadServiceRequest(JsonObject d, string fallbackRequisition)
    {
        string? category = d["category"] switch { JsonObject c => Str(c["coding"]?[0]?["code"]), JsonNode n => Str(n), _ => null };
        DateTime? exp = Str(d["expiration_date"]) is { } e && DateTime.TryParse(e, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var dt) ? dt : null;
        var identity = d["identity"] as JsonObject;
        return new EhealthServiceRequest(
            Str(d["id"]) ?? "", Str(d["requisition"]) ?? fallbackRequisition, Str(d["status"]) ?? "",
            Str(d["subject"]?["identifier"]?["value"]), Str(d["code"]?["identifier"]?["value"]), exp,
            Str(d["used_by_legal_entity"]?["identifier"]?["value"]), category, Str(d["program"]?["identifier"]?["value"]),
            Str(d["program_processing_status"]), decimal.TryParse(Str(d["remaining_quantity"]), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var q) ? q : null,
            Str(identity?["last_name"]), Str(identity?["gender"]), Str(d["code"]?["display_value"]) ?? Str(d["code"]?["identifier"]?["type"]?["text"]));
    }

    private static string? Str(JsonNode? n) => n is JsonValue v ? (v.TryGetValue<string>(out var s) ? s : v.ToString()) : null;

    // ------------------------------------------------------------------ транспорт
    private sealed record Response(int Status, JsonNode? Json, string Text);

    private void EnsureAllowed(EhealthOperation? op)
    {
        if (op != null && !_options.IsAllowed(op.Value))
            throw new EhealthException(0, $"Операцію ЕСОЗ {op} вимкнено (Ehealth:Live:Operations:{op})", EhealthErrorKind.Disabled);
    }

    private async Task<Response> SendAsync(HttpMethod method, string relativeUrl, JsonObject? body, EhealthOperation? op, bool idempotent, CancellationToken ct)
    {
        EnsureAllowed(op);
        var attempts = idempotent ? 1 + Math.Max(0, _options.Http.MaxRetries) : 1;
        for (var attempt = 1; ; attempt++)
        {
            try { return await SendOnceAsync(method, relativeUrl, body, ct); }
            catch (EhealthException ex) when (ex.IsTransient && attempt < attempts && !ct.IsCancellationRequested)
            {
                var pause = _options.Http.RetryBaseDelayMs * (1 << (attempt - 1));
                if (pause > 0) await Task.Delay(pause, ct);
            }
        }
    }

    private async Task<Response> SendOnceAsync(HttpMethod method, string relativeUrl, JsonObject? body, CancellationToken ct)
    {
        var token = await _tokens.GetAccessTokenAsync(ct);
        if (string.IsNullOrEmpty(token) && _options.IsLive)
            throw new EhealthException(0, "Токен доступу ЕСОЗ не налаштовано (Ehealth:AccessToken)", EhealthErrorKind.Config);

        var client = _factory.CreateClient(HttpClientName);
        var logUrl = (client.BaseAddress?.AbsolutePath ?? "/") + relativeUrl;
        using var req = new HttpRequestMessage(method, relativeUrl);
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!string.IsNullOrEmpty(_options.ApiKey)) req.Headers.TryAddWithoutValidation("api-key", _options.ApiKey);
        if (!string.IsNullOrEmpty(_options.CustomPsk)) req.Headers.TryAddWithoutValidation("X-Custom-PSK", _options.CustomPsk);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var bodyText = body?.ToJsonString();
        if (bodyText != null) req.Content = new StringContent(bodyText, new UTF8Encoding(false), "application/json");

        var started = DateTime.UtcNow;
        HttpResponseMessage? resp = null;
        var text = "";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.Http.TimeoutSeconds)));
        string? transportError = null;
        try
        {
            resp = await client.SendAsync(req, cts.Token);
            text = await resp.Content.ReadAsStringAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { transportError = "таймаут запиту до ЕСОЗ"; }
        catch (HttpRequestException ex) { transportError = ex.Message; }
        var status = resp == null ? (int?)null : (int)resp.StatusCode;
        await SafeLogAsync(method.Method, logUrl, MaskBody(bodyText), status, transportError ?? text, started);
        if (resp == null) throw new EhealthException(503, $"ЕСОЗ недоступна: {transportError}", EhealthErrorKind.Transient);

        var code = (int)resp.StatusCode;
        JsonNode? json = null;
        if (!string.IsNullOrWhiteSpace(text)) { try { json = JsonNode.Parse(text); } catch (System.Text.Json.JsonException) { /* не JSON */ } }
        if (code is 200 or 201 or 202 or 204 or 303) return new Response(code, json, text);
        throw ToException(code, json, text);
    }

    /// <summary>Доменна помилка за HTTP-статусом і тілом ({error:{type,message,invalid}} або {data:{error…}} або {meta}).</summary>
    public static EhealthException ToException(int code, JsonNode? json, string text)
    {
        var err = json?["error"] ?? json?["data"]?["error"];
        string? message = err is JsonObject eo ? (Str(eo["message"]) ?? Str(eo["type"])) : err != null ? Str(err) : null;
        var invalid = EhealthJobReader.ReadInvalid(err);
        message ??= text.Length > 300 ? text[..300] : text;
        if (string.IsNullOrWhiteSpace(message)) message = "без пояснення";
        var kind = EhealthException.KindOf(code);
        var ua = kind switch
        {
            EhealthErrorKind.Auth => "токен недійсний або прострочений",
            EhealthErrorKind.Permission => "немає права (скоупу) на операцію",
            EhealthErrorKind.NotFound => "об'єкт не знайдено",
            EhealthErrorKind.Conflict => "конфлікт стану",
            EhealthErrorKind.Validation => "помилка валідації",
            EhealthErrorKind.Transient => "тимчасова помилка ЕСОЗ",
            _ => "помилка"
        };
        return new EhealthException(code, $"ЕСОЗ ({code}, {ua}): {message}", kind, text, invalid);
    }

    /// <summary>Підписані дані (з ПД пацієнта) в журнал не пишемо: лише довжина.</summary>
    private static string? MaskBody(string? body)
    {
        if (body == null) return null;
        try
        {
            var n = JsonNode.Parse(body) as JsonObject;
            if (n?["signed_data"] is JsonValue v && v.TryGetValue<string>(out var s)) { n["signed_data"] = $"[base64:{s.Length}]"; return n.ToJsonString(); }
        }
        catch (System.Text.Json.JsonException) { /* залишаємо як є */ }
        return body;
    }

    private async Task SafeLogAsync(string method, string url, string? request, int? status, string response, DateTime started)
    {
        try { await _log.WriteAsync(new EhealthExchangeEntry(method, url, request, status, response, started, (int)(DateTime.UtcNow - started).TotalMilliseconds)); }
        catch { /* журнал не повинен ламати обмін */ }
    }
}
