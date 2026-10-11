// =============================================================================
// Мок API ЕСОЗ для автономної роботи й тестів (Ehealth:Mode = Mock). Перехоплює HTTP-запити EhealthClient
// і відповідає у форматі eHealth ({"data": …, "paging": …}; job {"data":{"id","status":"processed"}}),
// перевіряючи бізнес-правила реєстру направлень: use лише для active і не використаного іншим закладом,
// release лише використаного, complete лише використаного цим закладом. Стан «ЕСОЗ» тримається в пам'яті
// процесу (окремо від дзеркала МІС); вміст реєстру направлень береться з ehe_incoming_medical_referral.
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
    public sealed class ServiceRequestState
    {
        public string Status { get; set; } = "active";
        public string? UsedByLegalEntity { get; set; }
        public string? UsedByEmployee { get; set; }
        public string? CompletedWithType { get; set; }
        public string? CompletedWithId { get; set; }
    }
    public ConcurrentDictionary<string, ServiceRequestState> ServiceRequests { get; } = new();
}

public sealed class EhealthMockHandler : HttpMessageHandler
{
    private static readonly Regex ActionRx = new(@"patients/service_requests/(?<id>[^/]+)/actions/(?<action>use|release|complete)$", RegexOptions.Compiled);
    private readonly IServiceScopeFactory _scopes;
    private readonly EhealthMockState _state;

    public EhealthMockHandler(IServiceScopeFactory scopes, EhealthMockState state) { _scopes = scopes; _state = state; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        var body = request.Content == null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(ct));

        if (request.Method == HttpMethod.Get && path.EndsWith("patients/service_requests"))
            return await SearchAsync(query["requisition"], query["status"]);

        var m = ActionRx.Match(path);
        if (request.Method == HttpMethod.Patch && m.Success)
        {
            var id = m.Groups["id"].Value;
            var sr = await LoadAsync(id);
            if (sr == null) return Error(HttpStatusCode.NotFound, "Service request not found");
            switch (m.Groups["action"].Value)
            {
                case "use":
                {
                    var le = body?["used_by_legal_entity"]?["identifier"]?["value"]?.GetValue<string>();
                    if (le == null || body?["used_by_employee"] == null) return Error((HttpStatusCode)422, "used_by_employee and used_by_legal_entity are required");
                    if (sr.Status != "active") return Error(HttpStatusCode.Conflict, $"Service request is {sr.Status}");
                    if (sr.UsedByLegalEntity != null && sr.UsedByLegalEntity != le) return Error(HttpStatusCode.Conflict, "Service request is already used by another legal entity");
                    sr.UsedByLegalEntity = le; sr.UsedByEmployee = body["used_by_employee"]?["identifier"]?["value"]?.GetValue<string>();
                    break;
                }
                case "release":
                    if (sr.UsedByLegalEntity == null) return Error(HttpStatusCode.Conflict, "Service request is not used");
                    if (sr.Status != "active") return Error(HttpStatusCode.Conflict, $"Service request is {sr.Status}");
                    sr.UsedByLegalEntity = null; sr.UsedByEmployee = null;
                    break;
                case "complete":
                {
                    var cw = body?["completed_with"]?["identifier"];
                    if (cw == null) return Error((HttpStatusCode)422, "completed_with is required");
                    if (sr.Status != "active") return Error(HttpStatusCode.Conflict, $"Service request is {sr.Status}");
                    if (sr.UsedByLegalEntity == null) return Error(HttpStatusCode.Conflict, "Service request must be used before completion");
                    sr.Status = "completed";
                    sr.CompletedWithType = cw["type"]?["coding"]?[0]?["code"]?.GetValue<string>();
                    sr.CompletedWithId = cw["value"]?.GetValue<string>();
                    break;
                }
            }
            return Json(HttpStatusCode.Accepted, new JsonObject { ["data"] = JobJson(Guid.NewGuid().ToString(), id), ["meta"] = Meta(202) });
        }

        if (request.Method == HttpMethod.Get && path.Contains("/jobs/"))
            return Json(HttpStatusCode.OK, new JsonObject { ["data"] = JobJson(path[(path.LastIndexOf('/') + 1)..], null), ["meta"] = Meta(200) });

        return Error(HttpStatusCode.NotFound, $"Mock eHealth: endpoint {request.Method} {path} is not emulated");
    }

    private async Task<HttpResponseMessage> SearchAsync(string? requisition, string? status)
    {
        if (string.IsNullOrWhiteSpace(requisition)) return Error((HttpStatusCode)422, "requisition is required");
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var rows = await db.Referrals.AsNoTracking().Include(r => r.ServiceCatalogService).Where(r => r.RegNumber == requisition && r.RecordState != RecordStates.Deleted).ToListAsync();
        var data = new JsonArray();
        foreach (var r in rows)
        {
            var id = r.EhealthId ?? r.Id;
            var st = State(id, r);
            if (!string.IsNullOrEmpty(status) && st.Status != status) continue;
            data.Add(new JsonObject
            {
                ["id"] = id, ["requisition"] = r.RegNumber, ["status"] = st.Status, ["category"] = "laboratory_procedure",
                ["subject"] = EhealthClient.Identifier("patient", r.PatientCardId ?? ""),
                ["code"] = EhealthClient.Identifier("service", r.ServiceCatalogService?.Code ?? ""),
                ["expiration_date"] = r.ExpirationDate?.ToString("yyyy-MM-dd"),
                ["used_by_legal_entity"] = st.UsedByLegalEntity == null ? null : EhealthClient.Identifier("legal_entity", st.UsedByLegalEntity)
            });
        }
        return Json(HttpStatusCode.OK, new JsonObject
        {
            ["data"] = data, ["meta"] = Meta(200),
            ["paging"] = new JsonObject { ["page_number"] = 1, ["page_size"] = 50, ["total_entries"] = data.Count, ["total_pages"] = 1 }
        });
    }

    private async Task<EhealthMockState.ServiceRequestState?> LoadAsync(string id)
    {
        if (_state.ServiceRequests.TryGetValue(id, out var s)) return s;
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
        var r = await db.Referrals.AsNoTracking().FirstOrDefaultAsync(x => x.EhealthId == id || x.Id == id);
        return r == null ? null : State(id, r);
    }

    private EhealthMockState.ServiceRequestState State(string id, EheIncomingMedicalReferral r) =>
        _state.ServiceRequests.GetOrAdd(id, _ => new EhealthMockState.ServiceRequestState
        {
            Status = r.StatusId == MedLinkEnums.ReferralStatusCompleted ? "completed" : r.StatusId == MedLinkEnums.ReferralStatusRecalled ? "recalled" : "active"
        });

    private static JsonObject JobJson(string jobId, string? entityId) => new()
    {
        ["id"] = jobId, ["status"] = "processed", ["status_code"] = 200, ["entity_id"] = entityId, ["eta"] = DateTime.UtcNow.ToString("O")
    };

    private static JsonObject Meta(int code) => new() { ["code"] = code, ["type"] = "object", ["request_id"] = Guid.NewGuid().ToString(), ["url"] = "mock" };

    private static HttpResponseMessage Json(HttpStatusCode code, JsonNode node) =>
        new(code) { Content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Error(HttpStatusCode code, string message) =>
        Json(code, new JsonObject { ["error"] = new JsonObject { ["type"] = "request_conflict", ["message"] = message }, ["meta"] = Meta((int)code) });
}
