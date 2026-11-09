// =============================================================================
// Клієнт eHealth-сервісу (проксі) MedLink для лабораторії-виконавця. ЛІС НЕ ходить напряму в ЕСОЗ: проксі тримає токени й секрети, сам
// звертається до ЕСОЗ і повертає результат. Контракт (шляхи, заголовки, тіла) — docs/EHEALTH_MEDLINK_PROXY_CONTRACT.md, джерело — SDK МІС
// (App.EhealthSdk/Endpoints/*) і код проксі. Усі шляхи відносні до Ehealth:Proxy:BaseUrl (префікса /api немає):
//   GET   patients/service_requests?requisition=&status=&page=&page_size=        — пошук направлення
//   PATCH patients/service_requests/{id}/actions/use      {used_by_employee, used_by_legal_entity, program}
//   PATCH patients/service_requests/{id}/actions/release  (без тіла)
//   PATCH patients/service_requests/{id}/actions/complete {completed_with[, program_service]}
//   POST  patients/{patient_id}/diagnostic_report_package   {signed_data}     — Submit Diagnostic Report Package (job)
//   PATCH patients/{patient_id}/diagnostic_report_package   {signed_data}     — Cancel Diagnostic Report Package (job)
//   GET   patients/{patient_id}/diagnostic_reports/{id}                       — перевірка перед повтором
//   GET   jobs/{id}                                                           — стан асинхронної операції
//   GET   auth/EhealthUILink/{role}?isReorganized=  ·  POST auth {code, role_name}  ·  GET auth/hasAccessToken | auth/update | auth/logout
//   GET   dictionaries[/{name}]  ·  GET equipments[?…]
// Кожен запит несе заголовки ClientId (id організації в ЕСОЗ) і UserId (співробітник ЛІС, від імені якого проксі бере його токен ЕСОЗ);
// заголовка Authorization немає. Помилки проксі приходять у «конверті» {HasException, InternalMessage, StatusCode, EhealthResponse:{error:{type,message,invalid[]}}},
// інколи з HTTP 200 — тому конверт розпізнається й на успішних відповідях. Повтори з витримкою — лише для ідемпотентних GET; записуючі виклики
// повторює outbox-воркер (див. EhealthOutboxEngine). Режим Live (прямий ЕСОЗ, шляхи OpenAPI, Bearer-токен) ЗАСТАРІВ і збережений лише для довідки.
// =============================================================================
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;

namespace MedLink.LIS.Api.Services.Ehealth;

public interface IEhealthTokenProvider
{
    /// <summary>ЗАСТАРІЛО (режим Live): access token прямого ЕСОЗ; null — не налаштовано. У схемі через проксі токен тримає проксі.</summary>
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
}

/// <summary>Статичний токен із конфігурації (лише застарілий Live).</summary>
public sealed class StaticEhealthTokenProvider : IEhealthTokenProvider
{
    private readonly EhealthOptions _options;
    public StaticEhealthTokenProvider(EhealthOptions options) => _options = options;
    public Task<string?> GetAccessTokenAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>(!string.IsNullOrWhiteSpace(_options.AccessToken) ? _options.AccessToken : null);
}

/// <summary>Контекст виклику: співробітник, від імені якого проксі бере токен ЕСОЗ (заголовок UserId). Воркер черги встановлює його з даних задачі.</summary>
public static class EhealthCallContext
{
    private static readonly AsyncLocal<string?> User = new();
    public static string? UserId => User.Value;

    private sealed class Scope : IDisposable
    {
        private readonly string? _prev;
        public Scope(string? prev) => _prev = prev;
        public void Dispose() => User.Value = _prev;
    }

    /// <summary>Встановлює UserId до звільнення повернутого об'єкта.</summary>
    public static IDisposable Use(string? userId) { var prev = User.Value; User.Value = userId; return new Scope(prev); }
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

/// <summary>Відповідь auth/EhealthUILink/{role}: набір прав (scope), client_id і зареєстрований redirect_uri клієнта.</summary>
public sealed record EhealthAuthLink(string Scopes, string ClientId, string? RedirectUri);

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

    // --- сесія користувача в ЕСОЗ (токен тримає проксі; ЛІС його не бачить) ---
    /// <summary>auth/EhealthUILink/{role}: параметри посилання на сторінку входу в ЕСОЗ.</summary>
    Task<EhealthAuthLink> GetAuthLinkAsync(string? role = null, CancellationToken ct = default);
    /// <summary>POST auth {code, role_name}: обмін коду авторизації на токен (токен зберігає проксі).</summary>
    Task AuthByCodeAsync(string code, string? role = null, CancellationToken ct = default);
    /// <summary>auth/hasAccessToken: чи є в проксі чинний токен цього користувача.</summary>
    Task<bool> HasAccessTokenAsync(CancellationToken ct = default);
    /// <summary>auth/update: оновити токен (refresh) у проксі.</summary>
    Task RefreshAccessTokenAsync(CancellationToken ct = default);
    /// <summary>auth/logout: видалити токен користувача з проксі.</summary>
    Task LogoutAsync(CancellationToken ct = default);

    // --- довідники й обладнання ---
    /// <summary>dictionaries[/{name}]: довідники eHealth (повертається як є).</summary>
    Task<JsonNode?> GetDictionaryAsync(string? name = null, CancellationToken ct = default);
    /// <summary>equipments?…: обладнання закладу в ЕСОЗ (повертається як є; вимагає прапорця Equipment).</summary>
    Task<JsonNode?> GetEquipmentsAsync(IReadOnlyDictionary<string, string>? query = null, CancellationToken ct = default);
}

public sealed class EhealthClient : IEhealthClient
{
    public const string HttpClientName = "ehealth";
    private readonly IHttpClientFactory _factory;
    private readonly EhealthOptions _options;
    private readonly IEhealthExchangeLog _log;
    private readonly IEhealthTokenProvider _tokens;
    private readonly ICurrentEmployee? _current;

    public EhealthClient(IHttpClientFactory factory, EhealthOptions options, IEhealthExchangeLog log, IEhealthTokenProvider tokens, ICurrentEmployee? current = null)
    {
        _factory = factory; _options = options; _log = log; _tokens = tokens; _current = current;
    }

    /// <summary>Ідентифікатор eHealth у форматі SDK (system=eHealth/resources).</summary>
    public static JsonObject Identifier(string code, string value) => EhealthRef.Identifier(code, value);

    // ------------------------------------------------------------------ шляхи (проксі MedLink / застарілий прямий ЕСОЗ)
    private string Prefix => _options.UsesProxyContract ? "patients/" : "";
    private string ActionPath(string id, string action) => $"{Prefix}service_requests/{Uri.EscapeDataString(id)}/actions/{action}";

    // ------------------------------------------------------------------ операції
    public async Task<List<EhealthServiceRequest>> SearchServiceRequestsAsync(string requisition, string status = "active", CancellationToken ct = default)
    {
        var url = $"{Prefix}service_requests?requisition={Uri.EscapeDataString(requisition)}" + (string.IsNullOrEmpty(status) ? "" : $"&status={Uri.EscapeDataString(status)}") + $"&page=1&page_size={_options.PageSize}";
        var r = await SendAsync(HttpMethod.Get, url, null, EhealthOperation.SearchRead, idempotent: true, ct: ct);
        var list = new List<EhealthServiceRequest>();
        foreach (var d in (r.Json?["data"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()) list.Add(ReadServiceRequest(d, requisition));
        return list;
    }

    public async Task<EhealthJob> UseServiceRequestAsync(string serviceRequestId, string employeeEhealthId, string legalEntityEhealthId, string? programEhealthId = null, string? divisionEhealthId = null, CancellationToken ct = default)
    {
        var body = new JsonObject { ["used_by_legal_entity"] = Identifier("legal_entity", legalEntityEhealthId), ["used_by_employee"] = Identifier("employee", employeeEhealthId) };
        // SDK МІС поля used_by_division не надсилає — через проксі лише у застарілому прямому режимі
        if (divisionEhealthId != null && !_options.UsesProxyContract) body["used_by_division"] = Identifier("division", divisionEhealthId);
        if (programEhealthId != null) body["program"] = Identifier("medical_program", programEhealthId);
        var r = await SendAsync(HttpMethod.Patch, ActionPath(serviceRequestId, "use"), body, EhealthOperation.Use, false, ct: ct);
        return EhealthJobReader.ParseAccepted(r.Json, r.Status);
    }

    public async Task<EhealthJob> ReleaseServiceRequestAsync(string serviceRequestId, CancellationToken ct = default)
    {
        var r = await SendAsync(HttpMethod.Patch, ActionPath(serviceRequestId, "release"), null, EhealthOperation.Use, false, ct: ct);
        return EhealthJobReader.ParseAccepted(r.Json, r.Status);
    }

    public async Task<EhealthJob> CompleteServiceRequestAsync(string serviceRequestId, string completedWithType, string completedWithEhealthId, string? programServiceEhealthId = null, string? statusReason = "default", CancellationToken ct = default)
    {
        var body = new JsonObject { ["completed_with"] = Identifier(completedWithType, completedWithEhealthId) };
        if (programServiceEhealthId != null) body["program_service"] = Identifier("program_service", programServiceEhealthId);
        // SDK МІС status_reason при погашенні не надсилає — лише у застарілому прямому режимі
        if (!string.IsNullOrEmpty(statusReason) && !_options.UsesProxyContract) body["status_reason"] = EhealthRef.Coding("eHealth/service_request_complete_reasons", statusReason);
        var r = await SendAsync(HttpMethod.Patch, ActionPath(serviceRequestId, "complete"), body, EhealthOperation.Complete, false, ct: ct);
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
        // jobs/{id}: {data:{status: pending|processed|failed, status_code, error, links}}; помилкове тіло з status=failed — результат job, а не помилка транспорту
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

    // ------------------------------------------------------------------ сесія в ЕСОЗ через проксі
    public async Task<EhealthAuthLink> GetAuthLinkAsync(string? role = null, CancellationToken ct = default)
    {
        EnsureProxyContract();
        var url = $"auth/EhealthUILink/{Uri.EscapeDataString(string.IsNullOrWhiteSpace(role) ? _options.Proxy.Role : role)}?isReorganized={(_options.Proxy.IsReorganized ? "true" : "false")}";
        var r = await SendAsync(HttpMethod.Get, url, null, null, true, ct: ct);
        var d = r.Json as JsonObject ?? throw new EhealthException(502, "Проксі не повернув параметрів посилання на вхід в ЕСОЗ", EhealthErrorKind.Transient, r.Text);
        var clientId = Str(d["client_id"]) ?? Str(d["ClientId"]) ?? "";
        if (string.IsNullOrEmpty(clientId) || (Guid.TryParse(clientId, out var g) && g == Guid.Empty))
            throw new EhealthException(409, "Проксі не знає клієнта ЕСОЗ для цієї організації (client_id порожній): організацію не підключено до проксі", EhealthErrorKind.Config, r.Text);
        return new EhealthAuthLink(Str(d["scopes"]) ?? Str(d["Scopes"]) ?? "", clientId, Str(d["redirect_uri"]) ?? Str(d["RedirectUri"]));
    }

    public async Task AuthByCodeAsync(string code, string? role = null, CancellationToken ct = default)
    {
        EnsureProxyContract();
        var body = new JsonObject { ["code"] = code, ["role_name"] = string.IsNullOrWhiteSpace(role) ? _options.Proxy.Role : role };
        await SendAsync(HttpMethod.Post, "auth", body, null, false, ct: ct);
    }

    public async Task<bool> HasAccessTokenAsync(CancellationToken ct = default)
    {
        EnsureProxyContract();
        var r = await SendAsync(HttpMethod.Get, "auth/hasAccessToken", null, null, true, ct: ct);
        // SDK читає рядок "true"/"false" (bool.TryParse): допускаємо і JSON-boolean, і рядок у лапках
        var t = (r.Json is JsonValue v && v.TryGetValue<bool>(out var b) ? b.ToString() : r.Text).Trim().Trim('"');
        return bool.TryParse(t, out var res) && res;
    }

    public async Task RefreshAccessTokenAsync(CancellationToken ct = default)
    {
        EnsureProxyContract();
        await SendAsync(HttpMethod.Get, "auth/update", null, null, false, ct: ct);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        EnsureProxyContract();
        await SendAsync(HttpMethod.Get, "auth/logout", null, null, false, ct: ct);
    }

    public async Task<JsonNode?> GetDictionaryAsync(string? name = null, CancellationToken ct = default)
    {
        var r = await SendAsync(HttpMethod.Get, string.IsNullOrWhiteSpace(name) ? "dictionaries" : $"dictionaries/{Uri.EscapeDataString(name)}", null, null, true, ct: ct);
        return r.Json;
    }

    public async Task<JsonNode?> GetEquipmentsAsync(IReadOnlyDictionary<string, string>? query = null, CancellationToken ct = default)
    {
        var qs = query == null || query.Count == 0 ? "" : "?" + string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var r = await SendAsync(HttpMethod.Get, "equipments" + qs, null, EhealthOperation.Equipment, true, ct: ct);
        return r.Json;
    }

    private void EnsureProxyContract()
    {
        if (!_options.UsesProxyContract) throw new EhealthException(0, "Сесія ЕСОЗ керується проксі MedLink: у застарілому режимі Live (прямий ЕСОЗ) недоступна", EhealthErrorKind.Config);
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
            throw new EhealthException(0, $"Операцію ЕСОЗ {op} вимкнено ({(_options.IsProxy ? "Ehealth:Proxy:Operations" : "Ehealth:Live:Operations")}:{op})", EhealthErrorKind.Disabled);
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

    /// <summary>Заголовки проксі: ClientId (організація в ЕСОЗ) і UserId (співробітник ЛІС). Значення в журнал не пишуться.</summary>
    private (string ClientId, string UserId) ProxyIdentity()
    {
        var client = _options.ClientIdValue ?? (_options.Simulated ? DeterministicGuid.For("ehealth-legal-entity:" + MedLinkDefaults.OrganizationId) : null);
        if (string.IsNullOrWhiteSpace(client))
            throw new EhealthException(0, "Не налаштовано ClientId: id організації в ЕСОЗ (Ehealth:Proxy:ClientId або Ehealth:LegalEntityId)", EhealthErrorKind.Config);
        var user = EhealthCallContext.UserId ?? (_current is { IsResolved: true } ? _current.EmployeeId : null) ?? (string.IsNullOrWhiteSpace(_options.Proxy.ServiceUserId) ? null : _options.Proxy.ServiceUserId) ?? (_options.Simulated ? "system" : null);
        if (string.IsNullOrWhiteSpace(user))
            throw new EhealthException(0, "Не визначено користувача для проксі (заголовок UserId): виклик без співробітника і не задано Ehealth:Proxy:ServiceUserId", EhealthErrorKind.Config);
        return (client, user);
    }

    private async Task<Response> SendOnceAsync(HttpMethod method, string relativeUrl, JsonObject? body, CancellationToken ct)
    {
        string? bearer = null;
        (string ClientId, string UserId)? identity = null;
        if (_options.UsesProxyContract) identity = ProxyIdentity();
        else
        {
            bearer = await _tokens.GetAccessTokenAsync(ct);
            if (string.IsNullOrEmpty(bearer)) throw new EhealthException(0, "Токен доступу ЕСОЗ не налаштовано (Ehealth:AccessToken; режим Live застарів)", EhealthErrorKind.Config);
        }

        var client = _factory.CreateClient(HttpClientName);
        var logUrl = (client.BaseAddress?.AbsolutePath ?? "/") + relativeUrl;
        using var req = new HttpRequestMessage(method, relativeUrl);
        if (identity != null) { req.Headers.TryAddWithoutValidation("ClientId", identity.Value.ClientId); req.Headers.TryAddWithoutValidation("UserId", identity.Value.UserId); }
        else
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            if (!string.IsNullOrEmpty(_options.ApiKey)) req.Headers.TryAddWithoutValidation("api-key", _options.ApiKey);
            if (!string.IsNullOrEmpty(_options.CustomPsk)) req.Headers.TryAddWithoutValidation("X-Custom-PSK", _options.CustomPsk);
        }
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
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { transportError = _options.UsesProxyContract ? "таймаут запиту до проксі MedLink" : "таймаут запиту до ЕСОЗ"; }
        catch (HttpRequestException ex) { transportError = ex.Message; }
        var status = resp == null ? (int?)null : (int)resp.StatusCode;
        await SafeLogAsync(method.Method, logUrl, MaskBody(bodyText), status, transportError ?? text, started);
        if (resp == null) throw new EhealthException(503, $"{(_options.UsesProxyContract ? "Проксі MedLink" : "ЕСОЗ")} недоступний: {transportError}", EhealthErrorKind.Transient);

        var code = (int)resp.StatusCode;
        JsonNode? json = null;
        if (!string.IsNullOrWhiteSpace(text)) { try { json = JsonNode.Parse(text); } catch (System.Text.Json.JsonException) { /* не JSON (наприклад, true) */ } }
        // «конверт» помилки проксі може прийти з HTTP 200 (шлюз): статус береться з поля StatusCode конверта
        if (code is >= 200 and < 300 && EhealthEnvelope.TryRead(json, out var envStatus, out _) && envStatus >= 400) code = envStatus;
        if (code is 200 or 201 or 202 or 204 or 303) return new Response(code, json, text);
        throw ToException(code, json, text);
    }

    /// <summary>Доменна помилка за HTTP-статусом і тілом: {error:{type,message,invalid}}, {data:{error…}} або «конверт» проксі {EhealthResponse:{error…}, StatusCode, InternalMessage}.</summary>
    public static EhealthException ToException(int code, JsonNode? json, string text)
    {
        EhealthEnvelope.TryRead(json, out var envStatus, out var envMessage);
        var err = json?["error"] ?? json?["data"]?["error"] ?? EhealthEnvelope.Child(EhealthEnvelope.Child(json, "EhealthResponse"), "error");
        if (code < 400 && envStatus >= 400) code = envStatus;
        var type = err is JsonObject eo0 ? Str(eo0["type"]) : null;
        string? message = err is JsonObject eo ? (Str(eo["message"]) ?? Str(eo["type"])) : err != null ? Str(err) : null;
        var invalid = EhealthJobReader.ReadInvalid(err);
        message ??= envMessage;
        message ??= text.Length > 300 ? text[..300] : text;
        if (string.IsNullOrWhiteSpace(message)) message = "без пояснення";
        // відсутній/прострочений токен користувача в проксі — це вхід в ЕСОЗ, незалежно від HTTP-статусу
        var kind = type is "token_not_found" or "token_expired" or "invalid_grant" or "invalid_client" ? EhealthErrorKind.Auth : EhealthException.KindOf(code);
        var ua = kind switch
        {
            EhealthErrorKind.Auth => "немає чинного токена ЕСОЗ: потрібен вхід в ЕСОЗ",
            EhealthErrorKind.Permission => "немає права (скоупу) на операцію",
            EhealthErrorKind.NotFound => "об'єкт не знайдено",
            EhealthErrorKind.Conflict => "конфлікт стану",
            EhealthErrorKind.Validation => "помилка валідації",
            EhealthErrorKind.Transient => "тимчасова помилка ЕСОЗ",
            _ => "помилка"
        };
        return new EhealthException(code, $"ЕСОЗ ({code}, {ua}): {message}", kind, text, invalid);
    }

    /// <summary>Підписані дані (з ПД пацієнта) і код авторизації в журнал не пишемо: лише довжину.</summary>
    private static string? MaskBody(string? body)
    {
        if (body == null) return null;
        try
        {
            var n = JsonNode.Parse(body) as JsonObject;
            if (n?["signed_data"] is JsonValue v && v.TryGetValue<string>(out var s)) { n["signed_data"] = $"[base64:{s.Length}]"; return n.ToJsonString(); }
            if (n?["code"] is JsonValue c && n["role_name"] != null && c.TryGetValue<string>(out var cs)) { n["code"] = $"[code:{cs.Length}]"; return n.ToJsonString(); }
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

/// <summary>«Конверт» помилки eHealth-сервісу MedLink: {HasException, InternalMessage, StatusCode, EhealthResponse:{error:{…}, meta}} (регістр ключів довільний).</summary>
public static class EhealthEnvelope
{
    public static JsonNode? Child(JsonNode? node, string name) =>
        node is JsonObject o ? o.FirstOrDefault(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)).Value : null;

    /// <summary>true — тіло є конвертом помилки (є EhealthResponse.error або HasException=true). status — StatusCode конверта (0, якщо не задано).</summary>
    public static bool TryRead(JsonNode? node, out int status, out string? internalMessage)
    {
        status = 0; internalMessage = null;
        if (node is not JsonObject) return false;
        var hasErr = Child(Child(node, "EhealthResponse"), "error") != null;
        var hasEx = Child(node, "HasException") is JsonValue h && h.TryGetValue<bool>(out var hv) && hv;
        if (!hasErr && !hasEx) return false;
        if (Child(node, "StatusCode") is JsonValue sc && int.TryParse(sc.ToString(), out var n)) status = n;
        if (hasErr && status < 400) status = 400;
        var im = Child(node, "InternalMessage") as JsonValue;
        internalMessage = im != null && im.TryGetValue<string>(out var s) && !string.Equals(s, "Success", StringComparison.OrdinalIgnoreCase) ? s : null;
        return true;
    }
}
