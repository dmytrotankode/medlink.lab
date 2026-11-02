// ТЗ 21.10, 23.9: інтервенції ЕСОЗ, співставлення, послуги обладнання, «Послуга ЕСОЗ × аналізатори», каталог і пакети для МІС,
// направлення МІС, розподіл по аналізаторах, маршрутизація й черги приладів
using System.Text;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class InterventionsController : LisControllerBase
{
    private readonly MisCatalogService _svc;
    public InterventionsController(MisCatalogService svc) => _svc = svc;

    /// <summary>Словник інтервенцій ЕСОЗ зі статусом співставлення/готовності (FR-MIS-001…006). status: UNMAPPED|NO_EQUIPMENT|PARTIAL|READY.</summary>
    [HttpGet("interventions")] public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? status) => Ok(await _svc.InterventionsAsync(search, status));
    [HttpPost("interventions/sync")] public async Task<IActionResult> Sync() => Ok(await _svc.SyncAsync());

    [HttpGet("intervention-map")] public async Task<IActionResult> Maps() => Ok(await _svc.MapsAsync());
    [HttpGet("intervention-map/suggest")] public async Task<IActionResult> Suggest([FromQuery] string interventionId) => Ok(await _svc.SuggestAsync(interventionId));
    [HttpGet("intervention-map/coverage")] public async Task<IActionResult> Coverage() => Ok(await _svc.CoverageAsync());
    [HttpGet("intervention-map/{id}")] public async Task<IActionResult> Map(string id) => Ok(await _svc.MapAsync(id));
    [HttpPost("intervention-map")] public async Task<IActionResult> Create([FromBody] InterventionMapRequest req) => Ok(await _svc.SaveMapAsync(null, req));
    [HttpPut("intervention-map/{id}")] public async Task<IActionResult> Update(string id, [FromBody] InterventionMapRequest req) => Ok(await _svc.SaveMapAsync(id, req));
    [HttpDelete("intervention-map/{id}")] public async Task<IActionResult> Delete(string id) { await _svc.DeleteMapAsync(id); return NoContent(); }

    /// <summary>Послуги обладнання приладу з показниками (FR-EQP-001).</summary>
    [HttpGet("analyzers/{id}/services")] public async Task<IActionResult> AnalyzerServices(string id) => Ok(await _svc.AnalyzerServicesAsync(id));
    /// <summary>Збереження послуг приладу; dryRun=true — кількість зачеплених тестів до підтвердження (FR-EQP-007).</summary>
    [HttpPut("analyzers/{id}/services")] public async Task<IActionResult> SaveAnalyzerServices(string id, [FromBody] List<AnalyzerServiceItem> items, [FromQuery] bool dryRun = false) => Ok(await _svc.SaveAnalyzerServicesAsync(id, items, dryRun));
    [HttpPost("analyzers/{id}/services/prefill")] public async Task<IActionResult> Prefill(string id) => Ok(await _svc.PrefillAsync(id));

    /// <summary>Довідник «Послуга ЕСОЗ × аналізатори» (FR-SVA-001): пріоритет або «½» у клітинці, готовність, стан приладів.</summary>
    [HttpGet("analyzer-services/matrix")] public async Task<IActionResult> Matrix() => Ok(await _svc.MatrixAsync());
    [HttpGet("analyzer-services/export")]
    public async Task<IActionResult> Export() => File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(await _svc.MatrixCsvAsync())).ToArray(), "text/csv; charset=utf-8", "analyzer-services.csv");
    [HttpPost("analyzer-services/import")]
    public async Task<IActionResult> Import([FromQuery] bool dryRun = true)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        return Ok(await _svc.ImportMatrixCsvAsync(await reader.ReadToEndAsync(), dryRun));
    }
    /// <summary>Картка послуги (FR-SVA-003): ключ — код інтервенції, id/код профілю або id співставлення.</summary>
    [HttpGet("analyzer-services/{key}")] public async Task<IActionResult> Card(string key) => Ok(await _svc.ServiceCardAsync(key));
    [HttpPut("analyzer-services/{key}")] public async Task<IActionResult> SaveCard(string key, [FromBody] ServiceCardUpdateRequest req) => Ok(await _svc.SaveServiceCardAsync(key, req));
}

public sealed class MisController : LisControllerBase
{
    private readonly MisCatalogService _catalog;
    private readonly MisReferralService _referrals;
    public MisController(MisCatalogService catalog, MisReferralService referrals) { _catalog = catalog; _referrals = referrals; }

    /// <summary>Каталог послуг для МІС (FR-SVA-010/011): склад, режим вибору, обов'язкові/типові показники; ETag = catalogVersion.</summary>
    [HttpGet("mis/catalog")]
    public async Task<IActionResult> Catalog()
    {
        var (body, version) = await _catalog.CatalogAsync();
        var etag = $"\"{version}\"";
        if (Request.Headers.IfNoneMatch.ToString() == etag) return StatusCode(304);
        Response.Headers.ETag = etag;
        return Ok(body);
    }
    [HttpGet("mis/catalog/packages")] public async Task<IActionResult> Packages() => Ok(await _catalog.PackagesAsync());

    /// <summary>Направлення/призначення МІС (FR-MIS-024, FR-PKG-003): позиції з обраними показниками, пакет; відповідь — маршрутизація по кожній позиції.</summary>
    [HttpPost("mis/referrals")] public async Task<IActionResult> Create([FromBody] MisReferralRequest req) => StatusCode(201, await _referrals.CreateAsync(req));
    [HttpGet("mis/referrals")] public async Task<IActionResult> List([FromQuery] int take = 100) => Ok(await _referrals.ListAsync(take));
    [HttpGet("mis/referrals/{id}")] public async Task<IActionResult> Get(string id) => Ok(await _referrals.GetAsync(id));
    [HttpPost("mis/referrals/{id}/cancel")] public async Task<IActionResult> Cancel(string id, [FromBody] ReasonBody? body) => Ok(await _referrals.CancelAsync(id, body?.Reason));
    [HttpPut("mis/referrals/{id}/items/{itemId}/indicators")]
    public async Task<IActionResult> Indicators(string id, string itemId, [FromBody] ItemIndicatorsRequest req) => Ok(await _referrals.SetIndicatorsAsync(id, itemId, req));

    /// <summary>Вхідні направлення (FR-DST-001/002): state=pending|all, source.</summary>
    [HttpGet("distribution/inbox")] public async Task<IActionResult> Inbox([FromQuery] string? state, [FromQuery] string? source) => Ok(await _referrals.InboxAsync(state, source));
    [HttpGet("distribution/{orderId}")] public async Task<IActionResult> Proposal(string orderId) => Ok(await _referrals.ProposalAsync(orderId));
    [HttpPut("distribution/{orderId}")] public async Task<IActionResult> Change(string orderId, [FromBody] List<DistributionChange> changes) => Ok(await _referrals.ChangeAsync(orderId, changes));
    [HttpPost("distribution/{orderId}/auto")] public async Task<IActionResult> Auto(string orderId) => Ok(await _referrals.AutoAsync(orderId));
    [HttpPost("distribution/{orderId}/confirm")] public async Task<IActionResult> Confirm(string orderId, [FromBody] DistributionConfirmRequest? req) => Ok(await _referrals.ConfirmAsync(orderId, req ?? new()));
}

public sealed class ReasonBody
{
    public string? Reason { get; set; }
}

public sealed class RoutingPreviewRequest
{
    public string? ProfileId { get; set; }
    public List<string> TestCodes { get; set; } = new();
    public bool Cito { get; set; }
}

public sealed class ReassignRequest
{
    public string AnalyzerId { get; set; } = "";
    public string? Reason { get; set; }
}

public sealed class RoutingController : LisControllerBase
{
    private readonly RoutingService _routing;
    private readonly Data.LisDbContext _db;
    public RoutingController(RoutingService routing, Data.LisDbContext db) { _routing = routing; _db = db; }

    [HttpPost("routing/preview")] public async Task<IActionResult> Preview([FromBody] RoutingPreviewRequest req) => Ok(await _routing.PreviewAsync(req.ProfileId, req.TestCodes, req.Cito));
    [HttpGet("routing/log")] public async Task<IActionResult> Log([FromQuery] string? orderTestId, [FromQuery] string? orderId, [FromQuery] int take = 200) => Ok(await _routing.LogAsync(orderTestId, orderId, take));
    [HttpGet("analyzers/{id}/queue")] public async Task<IActionResult> Queue(string id) => Ok(await _routing.QueueAsync(id));

    /// <summary>Ручне перенаправлення тесту (FR-RTE-006): лише на кандидата, причина обов'язкова.</summary>
    [HttpPut("order-tests/{id}/analyzer")]
    public async Task<IActionResult> Reassign(string id, [FromBody] ReassignRequest req)
    {
        var t = await _routing.ReassignAsync(id, req.AnalyzerId, req.Reason);
        await _db.SaveChangesAsync();
        return Ok(new { orderTestId = t.Id, t.TestCode, analyzerId = t.AssignedAnalyzerId, t.RoutedBy, t.RoutingStatus });
    }

    /// <summary>Переназначення черги приладу без зв'язку (FR-RTE-010): лише на online-кандидатів.</summary>
    [HttpPost("analyzers/{id}/reroute")]
    public async Task<IActionResult> Reroute(string id, [FromBody] ReasonBody? body)
    {
        var res = await _routing.RerouteFromAsync(id, null, body?.Reason ?? "Прилад без зв'язку", onlineOnly: true);
        await _db.SaveChangesAsync();
        return Ok(res);
    }
}
