// Контрактні тести клієнта проксі MedLink без БД і без HTTP-сервера: шляхи, заголовки (ClientId/UserId), тіла запитів/відповідей за SDK МІС, «конверт» помилок, класифікація помилок,
// повтори лише для ідемпотентних викликів, читання job, структурна перевірка підпису CAdES-X Long, UUID v1, витримки воркера.
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Services.Ehealth;

namespace MedLink.LIS.Tests.Api;

public class EhealthContractTests
{
    // ------------------------------------------------------------------ тестовий транспорт
    private sealed class StubHandler : HttpMessageHandler
    {
        public readonly List<(HttpMethod Method, string Url, string Body, string? Auth, string? ClientId, string? UserId)> Requests = new();
        private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _respond;
        public StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) => _respond = respond;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, request.RequestUri!.PathAndQuery, body, request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("ClientId", out var c) ? c.FirstOrDefault() : null, request.Headers.TryGetValues("UserId", out var u) ? u.FirstOrDefault() : null));
            return _respond(request, Requests.Count);
        }
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _h;
        public StubFactory(HttpMessageHandler h) => _h = h;
        public HttpClient CreateClient(string name) => new(_h, false) { BaseAddress = new Uri("https://ehealth-proxy.test/"), Timeout = Timeout.InfiniteTimeSpan };
    }

    private sealed class CapturingLog : IEhealthExchangeLog
    {
        public readonly List<EhealthExchangeEntry> Entries = new();
        public Task WriteAsync(EhealthExchangeEntry e) { Entries.Add(e); return Task.CompletedTask; }
    }

    private static HttpResponseMessage Json(int status, string json) => new((HttpStatusCode)status) { Content = new StringContent(json, new UTF8Encoding(false), "application/json") };

    private static (EhealthClient Client, StubHandler Handler, CapturingLog Log) Make(Func<HttpRequestMessage, int, HttpResponseMessage> respond, Action<EhealthOptions>? cfg = null)
    {
        var o = new EhealthOptions
        {
            Mode = EhealthModes.MedLinkProxy, AccessToken = "test-token",
            Proxy = new EhealthProxyOptions { BaseUrl = "https://ehealth-proxy.test/", ClientId = "client-1", ServiceUserId = "user-1", Operations = new EhealthOperationFlags { SearchRead = true, Use = true, Complete = true, SubmitDr = true, CancelDr = true, Specimen = true, Equipment = true } },
            Http = new EhealthHttpOptions { MaxRetries = 2, RetryBaseDelayMs = 1, TimeoutSeconds = 5, SyncJobWaitSeconds = 3, SyncJobPollDelaysMs = new[] { 1 } }
        };
        cfg?.Invoke(o);
        var h = new StubHandler(respond);
        var log = new CapturingLog();
        return (new EhealthClient(new StubFactory(h), o, log, new StaticEhealthTokenProvider(o)), h, log);
    }

    private const string Accepted = """{"data":{"status":"pending","eta":"2026-10-01T10:00:00Z"},"links":[{"entity":"job","href":"/Jobs/NBXk9EyErUZv1RhXgyvgg"}],"meta":{"code":202}}""";

    // ------------------------------------------------------------------ читання job
    [Fact]
    public void Accepted_202_takes_job_id_from_links_and_does_not_need_data_id()
    {
        var job = EhealthJobReader.ParseAccepted(JsonNode.Parse(Accepted), 202);
        Assert.Equal("NBXk9EyErUZv1RhXgyvgg", job.Id);
        Assert.True(job.IsPending);
    }

    [Fact]
    public void Accepted_202_without_job_link_is_a_transient_error_not_a_null_reference()
    {
        var ex = Assert.Throws<EhealthException>(() => EhealthJobReader.ParseAccepted(JsonNode.Parse("""{"data":{"status":"pending"}}"""), 202));
        Assert.Equal(EhealthErrorKind.Transient, ex.Kind);
        Assert.Throws<EhealthException>(() => EhealthJobReader.ParseAccepted(null, 202));
    }

    [Fact]
    public void Synchronous_200_or_201_is_processed_without_job_id()
    {
        // у data лежить оновлена сутність (її id — не id job)
        var job = EhealthJobReader.ParseAccepted(JsonNode.Parse("""{"data":{"id":"90a9e15b-b71b-4caf-8f2e-ff247e8a5600","status":"active"}}"""), 200);
        Assert.True(job.IsProcessed);
        Assert.Equal("", job.Id);
    }

    [Fact]
    public void Job_states_and_string_status_code_are_normalized()
    {
        Assert.True(EhealthJobReader.Parse(JsonNode.Parse("""{"data":{"status":"pending"}}"""), 200).IsPending);
        Assert.True(EhealthJobReader.Parse(JsonNode.Parse("""{"data":{"status":"processed"}}"""), 303).IsProcessed);
        Assert.True(EhealthJobReader.Parse(null, 303).IsProcessed);
        var failed = EhealthJobReader.Parse(JsonNode.Parse("""{"data":{"status":"failed","status_code":"409","error":{"type":"request_conflict","message":"Service request already used"}}}"""), 200);
        Assert.True(failed.IsFailed);
        Assert.Equal(409, failed.StatusCode);
        Assert.Contains("already used", failed.Error);
        var invalid = EhealthJobReader.Parse(JsonNode.Parse("""{"data":{"status":"failed","status_code":"422","error":{"invalid":[{"entry":"$.diagnostic_report.issued","rules":[{"description":"Issued date must be in the past"}]}]}}}"""), 422);
        Assert.Contains("$.diagnostic_report.issued: Issued date must be in the past", invalid.Invalid!);
        Assert.Equal("NBXk9EyErUZv1RhXgyvgg", EhealthJobReader.LastSegment("/Jobs/NBXk9EyErUZv1RhXgyvgg?x=1"));
    }

    // ------------------------------------------------------------------ шляхи й форми запитів (OpenAPI)
    [Fact]
    public async Task Search_uses_proxy_path_query_and_identity_headers_without_authorization()
    {
        var (c, h, _) = Make((_, _) => Json(200, """{"data":[{"id":"90a9e15b-b71b-4caf-8f2e-ff247e8a5600","requisition":"AX654654T","status":"active","category":{"coding":[{"code":"108252007"}]},"code":{"identifier":{"value":"LAB-BIOCHEM"}},"subject":{"identifier":{"value":"p-1"}},"expiration_date":"2099-01-01T00:00:00Z"}]}"""));
        var found = Assert.Single(await c.SearchServiceRequestsAsync("AX654654T"));
        Assert.Equal("/patients/service_requests?requisition=AX654654T&status=active&page=1&page_size=50", h.Requests[0].Url);
        Assert.Null(h.Requests[0].Auth); // токен ЕСОЗ тримає проксі, ЛІС його не надсилає
        Assert.Equal(("client-1", "user-1"), (h.Requests[0].ClientId, h.Requests[0].UserId));
        Assert.Equal("90a9e15b-b71b-4caf-8f2e-ff247e8a5600", found.Id);
        Assert.Equal("LAB-BIOCHEM", found.ServiceCode);
        Assert.Equal("p-1", found.PatientId);
        Assert.Equal("108252007", found.Category);
    }

    [Fact]
    public async Task Actions_use_correct_paths_and_bodies_and_read_job_from_links()
    {
        var (c, h, _) = Make((_, _) => Json(202, Accepted));
        var use = await c.UseServiceRequestAsync("sr-1", "emp-1", "le-1", "prog-1", "div-1");
        var rel = await c.ReleaseServiceRequestAsync("sr-1");
        var done = await c.CompleteServiceRequestAsync("sr-1", "diagnostic_report", "dr-1");
        Assert.All(new[] { use, rel, done }, j => Assert.Equal("NBXk9EyErUZv1RhXgyvgg", j.Id));
        Assert.Equal(new[] { "/patients/service_requests/sr-1/actions/use", "/patients/service_requests/sr-1/actions/release", "/patients/service_requests/sr-1/actions/complete" }, h.Requests.Select(r => r.Url).ToArray());
        Assert.All(h.Requests, r => Assert.Equal(HttpMethod.Patch, r.Method));
        var useBody = JsonNode.Parse(h.Requests[0].Body)!;
        Assert.Equal("prog-1", useBody["program"]!["identifier"]!["value"]!.GetValue<string>());
        Assert.Equal("emp-1", useBody["used_by_employee"]!["identifier"]!["value"]!.GetValue<string>());
        Assert.Null(useBody["used_by_division"]); // SDK МІС підрозділ у use не надсилає
        var doneBody = JsonNode.Parse(h.Requests[2].Body)!;
        Assert.Equal("dr-1", doneBody["completed_with"]!["identifier"]!["value"]!.GetValue<string>());
        Assert.Equal("diagnostic_report", doneBody["completed_with"]!["identifier"]!["type"]!["coding"]![0]!["code"]!.GetValue<string>());
        Assert.Null(doneBody["status_reason"]); // SDK МІС status_reason при погашенні не надсилає
        Assert.Equal("", h.Requests[1].Body); // release — без тіла
    }

    [Fact]
    public async Task Package_calls_use_patient_scoped_path_and_log_hides_signed_data()
    {
        var (c, h, log) = Make((_, _) => Json(202, Accepted));
        var signed = Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('x', 500)));
        await c.SubmitDiagnosticReportPackageAsync("pat-1", signed);
        await c.CancelDiagnosticReportPackageAsync("pat-1", signed);
        Assert.Equal((HttpMethod.Post, "/patients/pat-1/diagnostic_report_package"), (h.Requests[0].Method, h.Requests[0].Url));
        Assert.Equal((HttpMethod.Patch, "/patients/pat-1/diagnostic_report_package"), (h.Requests[1].Method, h.Requests[1].Url));
        Assert.Equal(signed, JsonNode.Parse(h.Requests[0].Body)!["signed_data"]!.GetValue<string>());
        // у журналі обміну підписаних даних (ПД пацієнта) немає — лише довжина
        Assert.All(log.Entries, e => { Assert.DoesNotContain(signed, e.Request); Assert.Contains("[base64:", e.Request); });
    }

    // ------------------------------------------------------------------ помилки й повтори
    [Theory]
    [InlineData(401, EhealthErrorKind.Auth)]
    [InlineData(403, EhealthErrorKind.Permission)]
    [InlineData(404, EhealthErrorKind.NotFound)]
    [InlineData(409, EhealthErrorKind.Conflict)]
    [InlineData(422, EhealthErrorKind.Validation)]
    [InlineData(500, EhealthErrorKind.Transient)]
    [InlineData(503, EhealthErrorKind.Transient)]
    [InlineData(429, EhealthErrorKind.Transient)]
    public async Task Http_statuses_are_mapped_to_domain_errors(int status, EhealthErrorKind kind)
    {
        var (c, _, _) = Make((_, _) => Json(status, """{"error":{"type":"x","message":"Missing allowances for operation"},"meta":{"code":0}}"""));
        var ex = await Assert.ThrowsAsync<EhealthException>(() => c.CompleteServiceRequestAsync("sr", "diagnostic_report", "dr"));
        Assert.Equal(kind, ex.Kind);
        Assert.Equal(status, ex.StatusCode);
        Assert.Contains("Missing allowances", ex.Message);
        Assert.Equal(kind == EhealthErrorKind.Transient, ex.IsTransient);
    }

    [Fact]
    public async Task Validation_error_exposes_invalid_entries()
    {
        var (c, _, _) = Make((_, _) => Json(422, """{"error":{"type":"validation_failed","invalid":[{"entry":"$.requisition","rules":[{"description":"Invalid requisition format"}]}]}}"""));
        var ex = await Assert.ThrowsAsync<EhealthException>(() => c.SearchServiceRequestsAsync("BAD"));
        Assert.Equal(EhealthErrorKind.Validation, ex.Kind);
        Assert.Contains("$.requisition: Invalid requisition format", ex.Invalid);
    }

    [Fact]
    public async Task Get_is_retried_on_transient_errors_but_state_changing_calls_are_not()
    {
        var (c, h, _) = Make((_, n) => n < 3 ? Json(503, """{"error":{"type":"unavailable"}}""") : Json(200, """{"data":[]}"""));
        Assert.Empty(await c.SearchServiceRequestsAsync("AX1"));
        Assert.Equal(3, h.Requests.Count);

        var (c2, h2, _) = Make((_, _) => Json(503, """{"error":{"type":"unavailable"}}"""));
        await Assert.ThrowsAsync<EhealthException>(() => c2.SearchServiceRequestsAsync("AX1"));
        Assert.Equal(3, h2.Requests.Count); // 1 + MaxRetries

        var (c3, h3, _) = Make((_, _) => Json(503, """{"error":{"type":"unavailable"}}"""));
        await Assert.ThrowsAsync<EhealthException>(() => c3.SubmitDiagnosticReportPackageAsync("p", "AAAA"));
        await Assert.ThrowsAsync<EhealthException>(() => c3.CompleteServiceRequestAsync("sr", "diagnostic_report", "dr"));
        await Assert.ThrowsAsync<EhealthException>(() => c3.UseServiceRequestAsync("sr", "e", "l"));
        Assert.Equal(3, h3.Requests.Count); // по одному запиту: POST/PATCH без автоповтору (повтор — справа outbox із перевіркою ідемпотентності)
    }

    [Fact]
    public async Task Get_diagnostic_report_returns_null_on_404()
    {
        var (c, h, _) = Make((_, _) => Json(404, """{"error":{"type":"not_found"}}"""));
        Assert.Null(await c.GetDiagnosticReportAsync("pat", "dr"));
        Assert.Single(h.Requests); // 404 не повторюється
        var (c2, _, _) = Make((_, _) => Json(200, """{"data":{"id":"dr","status":"final"}}"""));
        Assert.Equal("final", (await c2.GetDiagnosticReportAsync("pat", "dr"))!.Status);
    }

    [Fact]
    public async Task Failed_job_body_with_error_status_is_a_job_result_not_a_transport_error()
    {
        var (c, h, _) = Make((_, _) => Json(422, """{"data":{"status":"failed","status_code":"422","error":{"message":"Validation failed","invalid":[{"entry":"$.diagnostic_report.issued","rules":[{"description":"in the past"}]}]}}}"""));
        var job = await c.GetJobAsync("NBXk9EyErUZv1RhXgyvgg");
        Assert.True(job.IsFailed);
        Assert.Equal(422, job.StatusCode);
        Assert.Equal("NBXk9EyErUZv1RhXgyvgg", job.Id);
        Assert.Contains("$.diagnostic_report.issued: in the past", job.Invalid!);
        Assert.Equal("/jobs/NBXk9EyErUZv1RhXgyvgg", h.Requests[0].Url);
    }

    [Fact]
    public async Task Wait_for_job_polls_until_processed()
    {
        var (c, h, _) = Make((_, n) => n < 3 ? Json(200, """{"data":{"status":"pending"}}""") : Json(303, """{"data":{"status":"processed"}}"""));
        var job = await c.WaitForJobAsync(new EhealthJob("job-1", EhealthJobStatus.Pending));
        Assert.True(job.IsProcessed);
        Assert.Equal(3, h.Requests.Count);
    }

    [Fact]
    public async Task Proxy_mode_operations_are_off_by_default_and_need_identity()
    {
        var (c, h, _) = Make((_, _) => Json(200, """{"data":[]}"""), o => o.Proxy.Operations = new EhealthOperationFlags());
        var ex = await Assert.ThrowsAsync<EhealthException>(() => c.SearchServiceRequestsAsync("AX1"));
        Assert.Equal(EhealthErrorKind.Disabled, ex.Kind);
        Assert.Empty(h.Requests);

        var (c2, h2, _) = Make((_, _) => Json(200, """{"data":[]}"""), o => { o.Proxy.Operations = new EhealthOperationFlags { SearchRead = true }; });
        Assert.Empty(await c2.SearchServiceRequestsAsync("AX1"));
        Assert.Single(h2.Requests);
        await Assert.ThrowsAsync<EhealthException>(() => c2.CompleteServiceRequestAsync("sr", "diagnostic_report", "dr")); // прапорець Complete вимкнено
        Assert.Single(h2.Requests);

        // немає ClientId (ні Proxy:ClientId, ні LegalEntityId) -> помилка конфігурації до запиту
        var (c3, h3, _) = Make((_, _) => Json(200, """{"data":[]}"""), o => { o.Proxy.ClientId = null; });
        var cfg = await Assert.ThrowsAsync<EhealthException>(() => c3.SearchServiceRequestsAsync("AX1"));
        Assert.Equal(EhealthErrorKind.Config, cfg.Kind);
        Assert.Empty(h3.Requests);
        // немає користувача (UserId) -> помилка конфігурації; контекст виклику (воркер) його задає
        var (c4, h4, _) = Make((_, _) => Json(200, """{"data":[]}"""), o => { o.Proxy.ServiceUserId = null; });
        Assert.Equal(EhealthErrorKind.Config, (await Assert.ThrowsAsync<EhealthException>(() => c4.SearchServiceRequestsAsync("AX1"))).Kind);
        using (EhealthCallContext.Use("emp-77")) Assert.Empty(await c4.SearchServiceRequestsAsync("AX1"));
        Assert.Equal("emp-77", Assert.Single(h4.Requests).UserId);
    }

    [Fact]
    public async Task Deprecated_live_mode_still_uses_direct_openapi_paths_and_bearer_token()
    {
        var (c, h, _) = Make((_, _) => Json(200, """{"data":[]}"""), o => { o.Mode = EhealthModes.Live; o.Live.Operations.SearchRead = true; });
        Assert.Empty(await c.SearchServiceRequestsAsync("AX1"));
        Assert.Equal("/service_requests?requisition=AX1&status=active&page=1&page_size=50", h.Requests[0].Url);
        Assert.Equal("Bearer test-token", h.Requests[0].Auth);
        Assert.Null(h.Requests[0].ClientId);
        var (c2, _, _) = Make((_, _) => Json(200, """{"data":[]}"""), o => { o.Mode = EhealthModes.Live; o.AccessToken = null; o.Live.Operations.SearchRead = true; });
        Assert.Equal(EhealthErrorKind.Config, (await Assert.ThrowsAsync<EhealthException>(() => c2.SearchServiceRequestsAsync("AX1"))).Kind);
        await Assert.ThrowsAsync<EhealthException>(() => c.GetAuthLinkAsync()); // сесію ЄСОЗ у прямому режимі веде не проксі
    }

    // ------------------------------------------------------------------ контракт проксі: job-відповіді, «конверт» помилок, сесія
    [Fact]
    public async Task Proxy_answers_job_body_with_200_and_the_client_reads_it_as_a_job()
    {
        // проксі повертає тіло job з кодом 200 (а не 202): job визначається за links[entity=job]
        var (c, _, _) = Make((_, _) => Json(200, """{"data":{"status":"pending","eta":"2026-10-01T10:00:00Z","links":[{"entity":"job","href":"/Jobs/NBXk9EyErUZv1RhXgyvgg"}]}}"""));
        var job = await c.SubmitDiagnosticReportPackageAsync("pat-1", "AAAA");
        Assert.True(job.IsPending);
        Assert.Equal("NBXk9EyErUZv1RhXgyvgg", job.Id);
        // 200 з тілом сутності (без job) - виконано синхронно
        var (c2, _, _) = Make((_, _) => Json(200, """{"data":{"id":"90a9e15b-b71b-4caf-8f2e-ff247e8a5600","status":"active"}}"""));
        Assert.True((await c2.ReleaseServiceRequestAsync("sr-1")).IsProcessed);
    }

    [Fact]
    public async Task Proxy_error_envelope_is_recognised_even_with_http_200()
    {
        var env = """{"HasException":true,"InternalMessage":"Validation failed","StatusCode":422,"EhealthResponse":{"error":{"type":"validation_failed","message":"Validation failed","invalid":[{"entry":"$.program","rules":[{"description":"program is required"}]}]},"meta":{"code":422}}}""";
        var (c, _, _) = Make((_, _) => Json(200, env));
        var ex = await Assert.ThrowsAsync<EhealthException>(() => c.UseServiceRequestAsync("sr", "e", "l"));
        Assert.Equal(EhealthErrorKind.Validation, ex.Kind);
        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("$.program: program is required", ex.Invalid);
        // ті самі дані з HTTP 422
        var (c2, _, _) = Make((_, _) => Json(422, env));
        Assert.Equal(EhealthErrorKind.Validation, (await Assert.ThrowsAsync<EhealthException>(() => c2.UseServiceRequestAsync("sr", "e", "l"))).Kind);
    }

    [Theory]
    [InlineData("token_not_found")]
    [InlineData("token_expired")]
    [InlineData("invalid_grant")]
    public async Task Missing_or_expired_user_token_in_the_proxy_means_sign_in_required(string type)
    {
        var (c, _, _) = Make((_, _) => Json(200, """{"HasException":true,"InternalMessage":"x","StatusCode":400,"EhealthResponse":{"error":{"type":"TYPE","message":"Token problem"}}}""".Replace("TYPE", type)));
        var ex = await Assert.ThrowsAsync<EhealthException>(() => c.CompleteServiceRequestAsync("sr", "diagnostic_report", "dr"));
        Assert.Equal(EhealthErrorKind.Auth, ex.Kind); // воркер зупинить задачу зі станом «потрібен вхід», не повторюватиме наосліп
        Assert.Contains("потрібен вхід в ЕСОЗ", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Auth_calls_follow_the_proxy_contract_and_never_log_the_code()
    {
        var (c, h, log) = Make((req, _) => req.RequestUri!.AbsolutePath switch
        {
            "/auth/EhealthUILink/DOCTOR" => Json(200, """{"scopes":"service_request:read job:read","client_id":"6f1b2a80-3c4d-4e5f-8a9b-0c1d2e3f4a5b","redirect_uri":"https://lis.test/cb"}"""),
            "/auth/hasAccessToken" => Json(200, "true"),
            _ => Json(200, "\"ok\"")
        });
        var link = await c.GetAuthLinkAsync();
        Assert.Equal(("service_request:read job:read", "6f1b2a80-3c4d-4e5f-8a9b-0c1d2e3f4a5b", "https://lis.test/cb"), (link.Scopes, link.ClientId, link.RedirectUri));
        Assert.Equal("/auth/EhealthUILink/DOCTOR?isReorganized=false", h.Requests[0].Url);

        await c.AuthByCodeAsync("SECRET-CODE-123");
        Assert.Equal((HttpMethod.Post, "/auth"), (h.Requests[1].Method, h.Requests[1].Url));
        var body = JsonNode.Parse(h.Requests[1].Body)!;
        Assert.Equal("SECRET-CODE-123", body["code"]!.GetValue<string>());
        Assert.Equal("DOCTOR", body["role_name"]!.GetValue<string>());
        Assert.All(log.Entries, e => Assert.DoesNotContain("SECRET-CODE-123", e.Request ?? ""));

        Assert.True(await c.HasAccessTokenAsync());
        await c.RefreshAccessTokenAsync();
        await c.LogoutAsync();
        Assert.Equal(new[] { "/auth/hasAccessToken", "/auth/update", "/auth/logout" }, h.Requests.Skip(2).Select(r => r.Url).ToArray());
        Assert.All(h.Requests, r => Assert.Equal(("client-1", "user-1"), (r.ClientId, r.UserId)));
        Assert.All(h.Requests, r => Assert.Null(r.Auth));
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("\"true\"", true)]
    [InlineData("True", true)]
    [InlineData("", false)]
    public async Task Has_access_token_accepts_the_string_forms_the_sdk_reads(string text, bool expected)
    {
        var (c, _, _) = Make((_, _) => Json(200, text));
        Assert.Equal(expected, await c.HasAccessTokenAsync());
    }

    [Fact]
    public async Task Auth_link_without_client_in_the_proxy_is_a_configuration_error()
    {
        var (c, _, _) = Make((_, _) => Json(200, """{"scopes":"x","client_id":"00000000-0000-0000-0000-000000000000"}"""));
        Assert.Equal(EhealthErrorKind.Config, (await Assert.ThrowsAsync<EhealthException>(() => c.GetAuthLinkAsync())).Kind);
    }

    [Fact]
    public async Task Dictionaries_and_equipments_use_proxy_paths_and_equipment_is_gated()
    {
        var (c, h, _) = Make((_, _) => Json(200, """{"data":[]}"""));
        await c.GetDictionaryAsync("eHealth/diagnostic_report_categories");
        await c.GetEquipmentsAsync(new Dictionary<string, string> { ["status"] = "active" });
        Assert.Equal(new[] { "/dictionaries/eHealth%2Fdiagnostic_report_categories", "/equipments?status=active" }, h.Requests.Select(r => r.Url).ToArray());
        var (c2, _, _) = Make((_, _) => Json(200, """{"data":[]}"""), o => o.Proxy.Operations = new EhealthOperationFlags());
        Assert.Equal(EhealthErrorKind.Disabled, (await Assert.ThrowsAsync<EhealthException>(() => c2.GetEquipmentsAsync())).Kind);
    }

    // ------------------------------------------------------------------ підпис і ідентифікатори
    [Fact]
    public void Structural_signature_check_accepts_xlong_with_matching_content_and_rejects_the_rest()
    {
        var content = Encoding.UTF8.GetBytes("""{"diagnostic_report":{"id":"x"}}""");
        var good = CadesInspector.Inspect(EhealthMockSigner.Sign(content), content);
        Assert.True(good.Valid, string.Join("; ", good.Errors));
        Assert.True(good.ContentMatches && good.HasCertificateValues && good.HasRevocationValues);
        Assert.Equal(CadesInspector.ProfileXLong, good.Profile);

        var other = CadesInspector.Inspect(EhealthMockSigner.Sign(content), Encoding.UTF8.GetBytes("""{"diagnostic_report":{"id":"y"}}"""));
        Assert.False(other.Valid);
        Assert.False(other.ContentMatches);

        var basic = CadesInspector.Inspect(EhealthMockSigner.Sign(content, xLong: false), content);
        Assert.False(basic.Valid); // CAdES-BES без значень сертифікатів/відкликання не годиться
        Assert.False(CadesInspector.Inspect("MIIDEMO==", content).Valid);
        Assert.False(CadesInspector.Inspect("", content).Valid);
        Assert.False(CadesInspector.Inspect("це не base64!", content).Valid);
        Assert.False(CadesInspector.Inspect(Convert.ToBase64String(content), content).Valid); // підписаний JSON без CMS-обгортки
        Assert.Equal(content, CadesInspector.TryExtractContent(EhealthMockSigner.Sign(content)));
    }

    [Fact]
    public void Uuid_v1_has_version_variant_and_is_unique()
    {
        var ids = Enumerable.Range(0, 2000).Select(_ => Uuid1.NewString()).ToList();
        Assert.All(ids, id => Assert.True(Uuid1.IsV1(id), id));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.False(Uuid1.IsV1(Guid.NewGuid().ToString())); // v4
        Assert.False(Uuid1.IsV1("not-a-uuid"));
    }

    [Fact]
    public void Package_contract_reports_json_paths_of_violations()
    {
        var bad = JsonNode.Parse("""{"diagnostic_report":{"id":"1"},"observations":[]}""");
        var errors = EhealthPackageContract.Validate(bad);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("diagnostic_report"));
        Assert.NotEmpty(EhealthPackageContract.Validate(null));
    }

    // ------------------------------------------------------------------ витримки
    [Fact]
    public void Retry_backoff_doubles_up_to_the_cap_and_poll_delays_follow_the_spec()
    {
        var o = new EhealthOutboxOptions { BackoffBaseSeconds = 30, BackoffMaxSeconds = 1800, BackoffJitter = 0 };
        Assert.Equal(new double[] { 30, 60, 120, 240, 480, 960, 1800, 1800 }, Enumerable.Range(1, 8).Select(n => EhealthBackoff.RetryDelay(n, o).TotalSeconds).ToArray());
        Assert.Equal(new double[] { 2, 5, 10, 30, 120, 120 }, Enumerable.Range(0, 6).Select(n => EhealthBackoff.PollDelay(n, o).TotalSeconds).ToArray());
        var jittered = new EhealthOutboxOptions { BackoffBaseSeconds = 100, BackoffMaxSeconds = 100000, BackoffJitter = 0.1 };
        var rng = new Random(1);
        for (var i = 0; i < 200; i++) Assert.InRange(EhealthBackoff.RetryDelay(1, jittered, rng).TotalSeconds, 90, 110);
    }
}
