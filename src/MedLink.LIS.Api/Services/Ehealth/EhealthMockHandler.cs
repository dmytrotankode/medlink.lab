// =============================================================================
// Мок API ЕСОЗ — КОНТРАКТНИЙ ДВІЙНИК (Ehealth:Mode = Mock), а не віддзеркалення БД ЛІС. Поведінка за OpenAPI eHealth:
//   • шляхи /api/service_requests…, /api/jobs/{id}, /api/patients/{id}/diagnostic_report_package, /diagnostic_reports/{id};
//   • пошук за requisition у власному реєстрі мока (його наповнюють тести/демо — не з дзеркала ehe_incoming_medical_referral);
//   • записуючі операції відповідають 202 + {data:{status:"pending", links:[{entity:"job", href:"/Jobs/<id>"}]}} БЕЗ data.id;
//   • GET /api/jobs/{id}: pending (N разів) → processed (303 + links на створену сутність) | failed (тіло з status_code і error);
//   • Submit package: структурна перевірка підпису (CAdES-X Long), схема пакета (422 з error.invalid), дубль id → 409,
//     Complete направлення можливий лише після реєстрації діагностичного звіту (job processed) — як у ЕСОЗ;
//   • помилки {error:{type,message}} + meta; 401 без токена; 403 «Missing allowances»; 409 конфлікт стану; 422 валідація;
//   • збої впроваджуються тестами через EhealthMockState.QueueFailure / QueueJobFailure.
// Стан тримається в пам'яті процесу (singleton EhealthMockState); після рестарту реєстр порожній (демо — SeedFromMirror).
// =============================================================================
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services.Ehealth;

public sealed class EhealthMockState
{
    public sealed class MockServiceRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Requisition { get; set; } = "";
        public string Status { get; set; } = "active";
        public string Category { get; set; } = "laboratory_procedure";
        public string? PatientId { get; set; }
        public string? ServiceCode { get; set; }
        public string? ServiceName { get; set; }
        public DateTime? ExpirationDate { get; set; } = DateTime.UtcNow.AddDays(30);
        public string? ProgramId { get; set; }
        public string? ProgramProcessingStatus { get; set; }
        public string? UsedByLegalEntity { get; set; }
        public string? UsedByEmployee { get; set; }
        public string? CompletedWithId { get; set; }
        public decimal RemainingQuantity { get; set; } = 1;
        public string? IdentityLastName { get; set; }
        public string? IdentityGender { get; set; }
    }

    public sealed class MockReport
    {
        public string Id { get; set; } = "";
        public string PatientId { get; set; } = "";
        public string Status { get; set; } = "final";
        public string? ServiceRequestId { get; set; }
        public List<string> ObservationIds { get; set; } = new();
        public bool Registered { get; set; }
        public string PackageJson { get; set; } = "";
    }

    public sealed class MockJob
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..22];
        public int PollsLeft { get; set; }
        public int? FailStatus { get; set; }
        public string? FailMessage { get; set; }
        public List<string> FailInvalid { get; set; } = new();
        public Action? Apply { get; set; }
        public bool Applied { get; set; }
        public string? LinkEntity { get; set; }
        public string? LinkHref { get; set; }
        public string Kind { get; set; } = "";
    }

    public sealed class MockFailure
    {
        public string Method { get; set; } = "";
        public string PathContains { get; set; } = "";
        public int Status { get; set; }
        public string? Message { get; set; }
        public int Times { get; set; } = 1;
    }

    public sealed class QueuedJobFailure
    {
        public string Kind { get; set; } = "";
        public int Status { get; set; } = 422;
        public string Message { get; set; } = "";
        public List<string> Invalid { get; set; } = new();
        public int Times { get; set; } = 1;
    }

    private readonly object _gate = new();
    public ConcurrentDictionary<string, MockServiceRequest> ServiceRequests { get; } = new();
    public ConcurrentDictionary<string, MockReport> Reports { get; } = new();
    public ConcurrentDictionary<string, MockJob> Jobs { get; } = new();
    private readonly List<MockFailure> _failures = new();
    private readonly List<QueuedJobFailure> _jobFailures = new();
    /// <summary>Скільки разів job відповідає pending, перш ніж завершитись.</summary>
    public int JobPendingPolls { get; set; } = 1;
    public bool Seeded { get; set; }
    /// <summary>Усі запити мока (метод + шлях) — для перевірки порядку викликів у тестах.</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    public MockServiceRequest Register(MockServiceRequest sr) { ServiceRequests[sr.Id] = sr; return sr; }

    public MockServiceRequest Register(string requisition, string? patientId = null, string? serviceCode = null, string? programId = null, string? lastName = null, string? id = null)
        => Register(new MockServiceRequest { Id = id ?? Guid.NewGuid().ToString(), Requisition = requisition, PatientId = patientId, ServiceCode = serviceCode, ProgramId = programId, IdentityLastName = lastName });

    /// <summary>Одноразово повернути помилку на запит, шлях якого містить <paramref name="pathContains"/> (метод порожній — будь-який).</summary>
    public void QueueFailure(string method, string pathContains, int status, string? message = null, int times = 1)
    { lock (_gate) _failures.Add(new MockFailure { Method = method, PathContains = pathContains, Status = status, Message = message, Times = times }); }

    /// <summary>Наступний створений job операції <paramref name="kind"/> (submit | cancel | complete | use) завершиться failed.</summary>
    public void QueueJobFailure(string kind, int status, string message, IEnumerable<string>? invalid = null, int times = 1)
    { lock (_gate) _jobFailures.Add(new QueuedJobFailure { Kind = kind, Status = status, Message = message, Invalid = invalid?.ToList() ?? new(), Times = times }); }

    internal MockFailure? TakeFailure(string method, string path)
    {
        lock (_gate)
        {
            var f = _failures.FirstOrDefault(x => (x.Method == "" || x.Method.Equals(method, StringComparison.OrdinalIgnoreCase)) && path.Contains(x.PathContains, StringComparison.OrdinalIgnoreCase));
            if (f == null) return null;
            if (--f.Times <= 0) _failures.Remove(f);
            return f;
        }
    }

    internal QueuedJobFailure? TakeJobFailure(string kind)
    {
        lock (_gate)
        {
            var f = _jobFailures.FirstOrDefault(x => x.Kind == kind);
            if (f == null) return null;
            if (--f.Times <= 0) _jobFailures.Remove(f);
            return f;
        }
    }

    public void Reset()
    {
        ServiceRequests.Clear(); Reports.Clear(); Jobs.Clear();
        lock (_gate) { _failures.Clear(); _jobFailures.Clear(); }
        while (Calls.TryDequeue(out _)) { }
    }
}

public sealed class EhealthMockHandler : HttpMessageHandler
{
    private static readonly Regex ActionRx = new(@"/service_requests/(?<id>[^/]+)/actions/(?<action>use|release|complete|process)$", RegexOptions.Compiled);
    private static readonly Regex PackageRx = new(@"/patients/(?<pid>[^/]+)/diagnostic_report_package$", RegexOptions.Compiled);
    private static readonly Regex ReportRx = new(@"/patients/(?<pid>[^/]+)/diagnostic_reports/(?<id>[^/]+)$", RegexOptions.Compiled);
    private static readonly Regex JobRx = new(@"/jobs/(?<id>[^/]+)$", RegexOptions.Compiled);

    private readonly EhealthMockState _state;
    private readonly IServiceScopeFactory? _scopes;
    private readonly EhealthOptions _options;
    private static readonly SemaphoreSlim SeedGate = new(1, 1);

    public EhealthMockHandler(EhealthMockState state, EhealthOptions options, IServiceScopeFactory? scopes = null)
    {
        _state = state; _options = options; _scopes = scopes;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var method = request.Method.Method;
        _state.Calls.Enqueue($"{method} {path}");
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        JsonNode? body = null;
        if (request.Content != null)
        {
            var text = await request.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(text)) { try { body = JsonNode.Parse(text); } catch (System.Text.Json.JsonException) { return Error(HttpStatusCode.BadRequest, "invalid_json", "Request body is not valid JSON"); } }
        }

        if (request.Headers.Authorization == null || string.IsNullOrWhiteSpace(request.Headers.Authorization.Parameter))
            return Error(HttpStatusCode.Unauthorized, "access_denied", "Access token is missing or expired");
        if (_state.TakeFailure(method, path) is { } fail)
            return fail.Status == 403 ? Error(HttpStatusCode.Forbidden, "forbidden", fail.Message ?? "Missing allowances: " + path)
                : Error((HttpStatusCode)fail.Status, fail.Status >= 500 ? "internal_error" : "request_failed", fail.Message ?? "Injected failure");

        await SeedOnMissAsync(method, path, query["requisition"]);

        if (method == "GET" && path.EndsWith("/service_requests")) return Search(query["requisition"], query["status"]);
        var m = ActionRx.Match(path);
        if (method == "PATCH" && m.Success) return ServiceRequestAction(m.Groups["id"].Value, m.Groups["action"].Value, body);
        var jm = JobRx.Match(path);
        if (method == "GET" && jm.Success) return PollJob(jm.Groups["id"].Value);
        var pm = PackageRx.Match(path);
        if (pm.Success && (method == "POST" || method == "PATCH")) return Package(pm.Groups["pid"].Value, body, cancel: method == "PATCH");
        var rm = ReportRx.Match(path);
        if (method == "GET" && rm.Success) return GetReport(rm.Groups["pid"].Value, rm.Groups["id"].Value);

        return Error(HttpStatusCode.NotFound, "not_found", $"Mock eHealth: endpoint {method} {path} is not emulated");
    }

    // ------------------------------------------------------------------ service requests
    private HttpResponseMessage Search(string? requisition, string? status)
    {
        if (string.IsNullOrWhiteSpace(requisition)) return Invalid("$.requisition", "required", "requisition is required");
        var data = new JsonArray();
        foreach (var sr in _state.ServiceRequests.Values.Where(s => s.Requisition == requisition && (string.IsNullOrEmpty(status) || s.Status == status)))
            data.Add(ToJson(sr));
        return Json(HttpStatusCode.OK, new JsonObject
        {
            ["data"] = data, ["meta"] = Meta(200),
            ["paging"] = new JsonObject { ["page_number"] = 1, ["page_size"] = _options.PageSize, ["total_entries"] = data.Count, ["total_pages"] = 1 }
        });
    }

    private static JsonObject ToJson(EhealthMockState.MockServiceRequest sr)
    {
        var o = new JsonObject
        {
            ["id"] = sr.Id, ["requisition"] = sr.Requisition, ["status"] = sr.Status,
            ["category"] = EhealthRef.Coding("eHealth/SNOMED/service_request_categories", sr.Category),
            ["subject"] = EhealthRef.Identifier("patient", sr.PatientId ?? ""),
            ["code"] = EhealthRef.Identifier("service", sr.ServiceCode ?? ""),
            ["expiration_date"] = sr.ExpirationDate?.ToString("yyyy-MM-dd"),
            ["remaining_quantity"] = sr.RemainingQuantity,
            ["identity"] = new JsonObject { ["last_name"] = sr.IdentityLastName, ["gender"] = sr.IdentityGender }
        };
        if (sr.ServiceName != null) o["code"]!["display_value"] = sr.ServiceName;
        if (sr.UsedByLegalEntity != null) o["used_by_legal_entity"] = EhealthRef.Identifier("legal_entity", sr.UsedByLegalEntity);
        if (sr.ProgramId != null) { o["program"] = EhealthRef.Identifier("medical_program", sr.ProgramId); o["program_processing_status"] = sr.ProgramProcessingStatus ?? "new"; }
        return o;
    }

    private HttpResponseMessage ServiceRequestAction(string id, string action, JsonNode? body)
    {
        if (!_state.ServiceRequests.TryGetValue(id, out var sr)) return Error(HttpStatusCode.NotFound, "not_found", "Service request not found");
        switch (action)
        {
            case "use":
            {
                var le = Ident(body?["used_by_legal_entity"]);
                if (le == null) return Invalid("$.used_by_legal_entity", "required", "used_by_legal_entity is required");
                if (Ident(body?["used_by_employee"]) == null) return Invalid("$.used_by_employee", "required", "used_by_employee is required");
                if (sr.ProgramId != null && Ident(body?["program"]) == null) return Invalid("$.program", "required", "program is required for service request with program");
                if (sr.Status != "active") return Error(HttpStatusCode.Conflict, "request_conflict", $"Service request is {sr.Status}");
                if (sr.UsedByLegalEntity != null && sr.UsedByLegalEntity != le) return Error(HttpStatusCode.Conflict, "request_conflict", "Service request is already used by another legal entity");
                sr.UsedByLegalEntity = le; sr.UsedByEmployee = Ident(body?["used_by_employee"]);
                if (sr.ProgramId != null) sr.ProgramProcessingStatus = "in_progress";
                return Accepted(NewJob("use", () => { }, null, null));
            }
            case "release":
                if (sr.Status != "active") return Error(HttpStatusCode.Conflict, "request_conflict", $"Service request is {sr.Status}");
                if (sr.UsedByLegalEntity == null) return Error(HttpStatusCode.Conflict, "request_conflict", "Service request is not used");
                sr.UsedByLegalEntity = null; sr.UsedByEmployee = null;
                if (sr.ProgramId != null) sr.ProgramProcessingStatus = "new";
                return Accepted(NewJob("release", () => { }, null, null));
            case "complete":
            {
                var cw = body?["completed_with"]?["identifier"];
                var cwType = cw?["type"]?["coding"]?[0]?["code"]?.GetValue<string>();
                var cwId = cw?["value"]?.GetValue<string>();
                if (cwId == null) return Invalid("$.completed_with", "required", "completed_with is required");
                if (sr.Status != "active") return Error(HttpStatusCode.Conflict, "request_conflict", $"Service request is {sr.Status}");
                if (cwType == "diagnostic_report")
                {
                    // ЕСОЗ: погасити можна лише записом, який вже створено (job реєстрації звіту processed) і який посилається на це направлення
                    if (!_state.Reports.TryGetValue(cwId, out var rep) || !rep.Registered || rep.Status != "final" || rep.ServiceRequestId != sr.Id)
                        return Invalid("$.completed_with", "not_found", "Diagnostic report referencing this service request was not found");
                }
                if (sr.ProgramId != null)
                {
                    if (sr.UsedByLegalEntity == null) return Error(HttpStatusCode.Conflict, "request_conflict", "Service request with program must be used before completion");
                    if (Ident(body?["program_service"]) == null) return Invalid("$.program_service", "required", "program_service is required for service request with program");
                }
                return Accepted(NewJob("complete", () => { sr.Status = "completed"; sr.CompletedWithId = cwId; sr.RemainingQuantity = 0; if (sr.ProgramId != null) sr.ProgramProcessingStatus = "completed"; }, "service_request", $"/api/service_requests/{sr.Id}"));
            }
            default:
                return Error(HttpStatusCode.Gone, "deprecated", "Endpoint is deprecated and is not emulated");
        }
    }

    private static string? Ident(JsonNode? n) => n?["identifier"]?["value"]?.GetValue<string>();

    // ------------------------------------------------------------------ diagnostic report package
    private HttpResponseMessage Package(string patientId, JsonNode? body, bool cancel)
    {
        var signed = body?["signed_data"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(signed)) return Invalid("$.signed_data", "required", "signed_data is required");
        var insp = CadesInspector.Inspect(signed);
        if (!insp.Valid) return Invalid("$.signed_data", "invalid", string.Join("; ", insp.Errors));
        var content = CadesInspector.TryExtractContent(signed);
        if (content == null) return Invalid("$.signed_data", "invalid", "Signed content cannot be extracted");
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF) return Invalid("$.signed_data", "invalid", "Signed content must not contain BOM");
        JsonNode? package;
        try { package = JsonNode.Parse(Encoding.UTF8.GetString(content)); }
        catch (System.Text.Json.JsonException) { return Invalid("$.signed_data", "invalid", "Signed content is not valid JSON"); }
        var errors = EhealthPackageContract.Validate(package, cancel);
        if (errors.Count > 0) return InvalidMany(errors);

        var dr = package!["diagnostic_report"]!;
        var drId = dr["id"]!.GetValue<string>();
        var obsIds = (package["observations"] as JsonArray ?? new JsonArray()).Select(o => o!["id"]!.GetValue<string>()).ToList();

        if (!cancel)
        {
            if (_state.Reports.ContainsKey(drId)) return Error(HttpStatusCode.Conflict, "request_conflict", "Diagnostic report with this id already exists");
            string? srId = null;
            if (dr["based_on"] != null)
            {
                srId = Ident(dr["based_on"]);
                if (srId == null || !_state.ServiceRequests.TryGetValue(srId, out var sr)) return Invalid("$.diagnostic_report.based_on", "not_found", "Service request not found");
                if (sr.Status != "active") return Error(HttpStatusCode.Conflict, "request_conflict", $"Service request is {sr.Status}");
            }
            var rep = new EhealthMockState.MockReport { Id = drId, PatientId = patientId, ServiceRequestId = srId, ObservationIds = obsIds, PackageJson = package.ToJsonString() };
            _state.Reports[drId] = rep;
            return Accepted(NewJob(cancel ? "cancel" : "submit", () => rep.Registered = true, "diagnostic_report", $"/api/patients/{patientId}/diagnostic_reports/{drId}", onFail: () => _state.Reports.TryRemove(drId, out _)));
        }
        else
        {
            if (!_state.Reports.TryGetValue(drId, out var rep) || rep.PatientId != patientId) return Error(HttpStatusCode.NotFound, "not_found", "Diagnostic report not found");
            if (!rep.Registered || rep.Status != "final") return Error(HttpStatusCode.Conflict, "request_conflict", $"Diagnostic report is {rep.Status}");
            if (!rep.ObservationIds.OrderBy(x => x).SequenceEqual(obsIds.OrderBy(x => x))) return Invalid("$.observations", "invalid", "Cancel package must contain all observations of the diagnostic report");
            return Accepted(NewJob("cancel", () => rep.Status = "entered_in_error", "diagnostic_report", $"/api/patients/{patientId}/diagnostic_reports/{drId}"));
        }
    }

    private HttpResponseMessage GetReport(string patientId, string id)
    {
        if (!_state.Reports.TryGetValue(id, out var rep) || rep.PatientId != patientId || !rep.Registered) return Error(HttpStatusCode.NotFound, "not_found", "Diagnostic report not found");
        return Json(HttpStatusCode.OK, new JsonObject { ["data"] = new JsonObject { ["id"] = rep.Id, ["status"] = rep.Status }, ["meta"] = Meta(200) });
    }

    // ------------------------------------------------------------------ jobs
    private EhealthMockState.MockJob NewJob(string kind, Action apply, string? linkEntity, string? linkHref, Action? onFail = null)
    {
        var job = new EhealthMockState.MockJob { Kind = kind, PollsLeft = _state.JobPendingPolls, Apply = apply, LinkEntity = linkEntity, LinkHref = linkHref };
        if (_state.TakeJobFailure(kind) is { } f) { job.FailStatus = f.Status; job.FailMessage = f.Message; job.FailInvalid = f.Invalid; job.Apply = onFail; }
        _state.Jobs[job.Id] = job;
        return job;
    }

    private HttpResponseMessage Accepted(EhealthMockState.MockJob job) => Json(HttpStatusCode.Accepted, new JsonObject
    {
        ["data"] = new JsonObject
        {
            ["status"] = "pending", ["eta"] = DateTime.UtcNow.AddSeconds(2).ToString("O"),
            ["links"] = new JsonArray(new JsonObject { ["entity"] = "job", ["href"] = $"/Jobs/{job.Id}" })
        },
        ["meta"] = Meta(202)
    });

    private HttpResponseMessage PollJob(string id)
    {
        if (!_state.Jobs.TryGetValue(id, out var job)) return Error(HttpStatusCode.NotFound, "not_found", "Job not found");
        lock (job)
        {
            if (job.PollsLeft > 0)
            {
                job.PollsLeft--;
                return Json(HttpStatusCode.OK, new JsonObject
                {
                    ["data"] = new JsonObject { ["status"] = "pending", ["eta"] = DateTime.UtcNow.AddSeconds(2).ToString("O"), ["links"] = new JsonArray(new JsonObject { ["entity"] = "job", ["href"] = $"/Jobs/{job.Id}" }) },
                    ["meta"] = Meta(200)
                });
            }
            if (!job.Applied) { job.Applied = true; job.Apply?.Invoke(); }
            if (job.FailStatus != null)
            {
                var err = new JsonObject { ["type"] = job.FailStatus == 409 ? "request_conflict" : "validation_failed", ["message"] = job.FailMessage };
                if (job.FailInvalid.Count > 0) err["invalid"] = new JsonArray(job.FailInvalid.Select(x => (JsonNode)new JsonObject { ["entry"] = x, ["rules"] = new JsonArray(new JsonObject { ["description"] = "invalid" }) }).ToArray());
                return Json((HttpStatusCode)job.FailStatus, new JsonObject
                {
                    ["data"] = new JsonObject { ["status"] = "failed", ["status_code"] = job.FailStatus.ToString(), ["eta"] = DateTime.UtcNow.ToString("O"), ["error"] = err },
                    ["meta"] = Meta(job.FailStatus.Value)
                });
            }
            var links = new JsonArray();
            if (job.LinkEntity != null) links.Add(new JsonObject { ["entity"] = job.LinkEntity, ["href"] = job.LinkHref });
            return Json(HttpStatusCode.SeeOther, new JsonObject
            {
                ["data"] = new JsonObject { ["status"] = "processed", ["eta"] = DateTime.UtcNow.ToString("O"), ["links"] = links },
                ["meta"] = Meta(303)
            });
        }
    }

    // ------------------------------------------------------------------ демо-наповнення
    /// <summary>
    /// Лише для демонстрації (Mock:SeedFromMirror): якщо реєстр мока не знає запитаного е-направлення (за номером чи id), один раз копіює його рядок
    /// з дзеркала БД. Далі запити обслуговує сам реєстр; дзеркало не є джерелом відповіді на кожен запит.
    /// </summary>
    private async Task SeedOnMissAsync(string method, string path, string? requisition)
    {
        if (!_options.Mock.SeedFromMirror || _scopes == null) return;
        string? number = null, srId = null;
        if (method == "GET" && path.EndsWith("/service_requests")) number = requisition;
        else if (method == "PATCH" && ActionRx.Match(path) is { Success: true } m) srId = m.Groups["id"].Value;
        if (string.IsNullOrWhiteSpace(number) && string.IsNullOrWhiteSpace(srId)) return;
        if (number != null && _state.ServiceRequests.Values.Any(s => s.Requisition == number)) return;
        if (srId != null && _state.ServiceRequests.ContainsKey(srId)) return;

        await SeedGate.WaitAsync();
        try
        {
            if (number != null && _state.ServiceRequests.Values.Any(s => s.Requisition == number)) return;
            if (srId != null && _state.ServiceRequests.ContainsKey(srId)) return;
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
            var q = db.Referrals.AsNoTracking().Include(r => r.ServiceCatalogService).Include(r => r.PatientCard).Where(r => r.RecordState != RecordStates.Deleted && r.RegNumber != null);
            var r = number != null ? await q.FirstOrDefaultAsync(x => x.RegNumber == number) : await q.FirstOrDefaultAsync(x => x.EhealthId == srId || x.Id == srId);
            if (r == null) return;
            var id = r.EhealthId ?? r.Id;
            if (_state.ServiceRequests.ContainsKey(id)) return;
            _state.Register(new EhealthMockState.MockServiceRequest
            {
                Id = id, Requisition = r.RegNumber!, PatientId = r.PatientCardId, ServiceCode = r.ServiceCatalogService?.Code, ServiceName = r.ServiceCatalogService?.Name,
                ExpirationDate = r.ExpirationDate, IdentityLastName = r.PatientCard?.Caption?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
                Status = r.StatusId == MedLinkEnums.ReferralStatusCompleted ? "completed" : r.StatusId == MedLinkEnums.ReferralStatusRecalled ? "recalled" : "active"
            });
        }
        finally { SeedGate.Release(); }
    }

    // ------------------------------------------------------------------ відповіді
    private static JsonObject Meta(int code) => new() { ["code"] = code, ["type"] = "object", ["request_id"] = "req-" + Guid.NewGuid().ToString("N")[..12], ["url"] = "mock" };

    private static HttpResponseMessage Json(HttpStatusCode code, JsonNode node) =>
        new(code) { Content = new StringContent(node.ToJsonString(), new UTF8Encoding(false), "application/json") };

    private static HttpResponseMessage Error(HttpStatusCode code, string type, string message) =>
        Json(code, new JsonObject { ["error"] = new JsonObject { ["type"] = type, ["message"] = message }, ["meta"] = Meta((int)code) });

    private static HttpResponseMessage Invalid(string entry, string rule, string description) => InvalidMany(new[] { $"{entry}: {description}" });

    private static HttpResponseMessage InvalidMany(IEnumerable<string> errors)
    {
        var invalid = new JsonArray();
        foreach (var e in errors)
        {
            var i = e.IndexOf(": ", StringComparison.Ordinal);
            var entry = i > 0 ? e[..i] : "$"; var descr = i > 0 ? e[(i + 2)..] : e;
            invalid.Add(new JsonObject { ["entry"] = entry, ["entry_type"] = "json_data_property", ["rules"] = new JsonArray(new JsonObject { ["description"] = descr, ["rule"] = "invalid" }) });
        }
        return Json((HttpStatusCode)422, new JsonObject
        {
            ["error"] = new JsonObject { ["type"] = "validation_failed", ["message"] = "Validation failed. You can find validators description at our API Manifest: http://docs.apimanifest.apiary.io.", ["invalid"] = invalid },
            ["meta"] = Meta(422)
        });
    }
}
