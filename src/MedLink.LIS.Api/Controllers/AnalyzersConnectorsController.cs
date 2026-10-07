// Аналізатори (CRUD, журнал обміну, симулятор, попередній перегляд замовлення), інсталяції коннекторів, API коннектора
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services;
using MedLink.LIS.Core.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class AnalyzersController : LisControllerBase
{
    private readonly AnalyzerService _analyzers;
    public AnalyzersController(AnalyzerService analyzers) => _analyzers = analyzers;

    [HttpGet("analyzers")]
    public async Task<IActionResult> List([FromQuery] bool? isActive) => Ok(await _analyzers.ListAsync(isActive));

    [HttpGet("analyzers/{id}")]
    public async Task<IActionResult> Get(string id) => Ok(await _analyzers.GetAsync(id));

    [HttpPost("analyzers")]
    public async Task<IActionResult> Create([FromBody] AnalyzerRequest req) => StatusCode(201, await _analyzers.CreateAsync(req));

    [HttpPut("analyzers/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] AnalyzerRequest req) => Ok(await _analyzers.UpdateAsync(id, req));

    [HttpDelete("analyzers/{id}")]
    public async Task<IActionResult> Delete(string id) { await _analyzers.DeleteAsync(id); return NoContent(); }

    /// <summary>Журнал обміну (повний сирий текст повідомлень).</summary>
    [HttpGet("analyzers/{id}/messages")]
    public async Task<IActionResult> Messages(string id, [FromQuery] string? direction, [FromQuery] PagingQuery paging) => Ok(await _analyzers.MessagesAsync(id, direction, paging));

    [HttpDelete("analyzers/{id}/messages/{messageId}")]
    public async Task<IActionResult> DeleteMessage(string id, string messageId) { await _analyzers.DeleteMessageAsync(id, messageId); return NoContent(); }

    /// <summary>Парсинг тестового повідомлення (ASTM/HL7) без збереження.</summary>
    [HttpPost("analyzers/{id}/simulate")]
    public async Task<IActionResult> Simulate(string id, [FromBody] SimulateRequest req) => Ok(await _analyzers.SimulateAsync(id, req.RawMessage));

    /// <summary>Згенерований текст замовлення для приладу за штрихкодом (налагодження).</summary>
    [HttpGet("analyzers/{id}/order-preview/{barcode}")]
    public async Task<IActionResult> OrderPreview(string id, string barcode) => Ok(await _analyzers.OrderPreviewAsync(id, barcode));
}

public sealed class SimulateRequest { public string RawMessage { get; set; } = ""; }

public sealed class ConnectorsController : LisControllerBase
{
    private readonly ConnectorService _connectors;
    private readonly IConfiguration _config;
    public ConnectorsController(ConnectorService connectors, IConfiguration config) { _connectors = connectors; _config = config; }

    [HttpGet("connectors")]
    public async Task<IActionResult> List() => Ok(await _connectors.ListAsync());

    [HttpGet("connectors/{id}")]
    public async Task<IActionResult> Get(string id) => Ok(await _connectors.GetAsync(id));

    /// <summary>Створити інсталяцію → {id, installKey, setupCommand}.</summary>
    [HttpPost("connectors")]
    public async Task<IActionResult> Create([FromBody] ConnectorCreateRequest req) => StatusCode(201, await _connectors.CreateAsync(req));

    [HttpPut("connectors/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ConnectorCreateRequest req) => Ok(await _connectors.UpdateAsync(id, req));

    [HttpDelete("connectors/{id}")]
    public async Task<IActionResult> Delete(string id) { await _connectors.DeleteAsync(id); return NoContent(); }

    [HttpPost("connectors/{id}/disable")]
    public async Task<IActionResult> Disable(string id) => Ok(await _connectors.TransitionAsync(id, ConnectorActions.Disable));

    [HttpPost("connectors/{id}/enable")]
    public async Task<IActionResult> Enable(string id) => Ok(await _connectors.TransitionAsync(id, ConnectorActions.Enable));

    [HttpPost("connectors/{id}/mark-offline")]
    public async Task<IActionResult> MarkOffline(string id) => Ok(await _connectors.TransitionAsync(id, ConnectorActions.MarkOffline));

    /// <summary>Перевипуск apiKey (повертається один раз).</summary>
    [HttpPost("connectors/{id}/rotate-key")]
    public async Task<IActionResult> RotateKey(string id) => Ok(await _connectors.RotateKeyAsync(id));

    [HttpPost("connectors/{id}/commands")]
    public async Task<IActionResult> Command(string id, [FromBody] ConnectorCommandDto cmd) { await _connectors.QueueCommandAsync(id, cmd.Type, cmd.Payload); return Accepted(); }

    /// <summary>ZIP: appsettings.local.json (URL сервера, install key), README_UA.txt, скрипти встановлення.</summary>
    [HttpGet("connectors/{id}/download")]
    public async Task<IActionResult> Download(string id)
    {
        var serverUrl = _config["Lab:PublicBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var bytes = await _connectors.DownloadZipAsync(id, serverUrl.TrimEnd('/'));
        return File(bytes, "application/zip", $"medlink-labconnector-{id}.zip");
    }

    [HttpGet("connectors/{id}/logs")]
    public async Task<IActionResult> Logs(string id, [FromQuery] PagingQuery paging) => Ok(await _connectors.LogsAsync(id, paging));
}

/// <summary>API коннектора (авторизація X-MedLink-ApiKey; /register — за install key).</summary>
[ServiceFilter(typeof(ConnectorApiKeyFilter))]
public sealed class ConnectorApiController : LisControllerBase
{
    private readonly ConnectorService _connectors;
    public ConnectorApiController(ConnectorService connectors) => _connectors = connectors;

    [HttpPost("connector/register")]
    [AllowWithoutApiKey]
    public async Task<IActionResult> Register([FromBody] ConnectorRegisterRequest req) => Ok(await _connectors.RegisterAsync(req));

    [HttpGet("connector/config")]
    public async Task<IActionResult> Config() => Ok(await _connectors.ConfigAsync(HttpContext.CurrentConnector()));

    [HttpPost("connector/heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] ConnectorHeartbeatRequest req) => Ok(await _connectors.HeartbeatAsync(HttpContext.CurrentConnector(), req));

    [HttpGet("connector/orders/by-barcode/{barcode}")]
    public async Task<IActionResult> OrderByBarcode(string barcode, [FromQuery] string? analyzerId)
        => Ok(await _connectors.OrderByBarcodeAsync(HttpContext.CurrentConnector(), barcode, analyzerId) ?? throw NotFoundException.For("Замовлення для штрихкоду", barcode));

    [HttpGet("connector/orders/pending")]
    public async Task<IActionResult> Pending([FromQuery] string? analyzerId) => Ok(await _connectors.PendingOrdersAsync(HttpContext.CurrentConnector(), analyzerId));

    /// <summary>Результати з приладу — той самий конвеєр, що й ручне введення; невідомі → unmatched.</summary>
    [HttpPost("connector/results")]
    public async Task<IActionResult> Results([FromBody] AnalyzerResultsBatchDto batch) => Ok(await _connectors.AcceptResultsAsync(HttpContext.CurrentConnector(), batch));

    [HttpPost("connector/messages")]
    public async Task<IActionResult> Messages([FromBody] AnalyzerMessageLogDto dto) { var m = await _connectors.StoreMessageAsync(HttpContext.CurrentConnector(), dto); return Accepted(new { id = m.Id }); }

    [HttpPost("connector/logs")]
    public async Task<IActionResult> Logs([FromBody] ConnectorLogBatchDto batch) { await _connectors.StoreLogsAsync(HttpContext.CurrentConnector(), batch); return Accepted(); }
}
