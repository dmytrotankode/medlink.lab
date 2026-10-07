// FR-REF-002: лабораторія як виконавець для клінік — клієнти, реєстр замовлень клініки, замовлення клієнта, рахунки/акти за період
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class ClientsController : LisControllerBase
{
    private readonly ClientService _svc;
    public ClientsController(ClientService svc) => _svc = svc;

    /// <summary>Клініки-клієнти (платники kind=CLINIC) зі статистикою замовлень і невиставлених сум.</summary>
    [HttpGet("clients")] public async Task<IActionResult> Clients() => Ok(await _svc.ClientsAsync());

    /// <summary>Замовлення клієнта зі статусами результатів і посиланнями на бланки (основа порталу клієнта).</summary>
    [HttpGet("clients/{id}/orders")] public async Task<IActionResult> Orders(string id, [FromQuery] DateTime? from, [FromQuery] DateTime? to) => Ok(await _svc.OrdersAsync(id, from, to));

    /// <summary>Імпорт реєстру замовлень клініки (CSV/XLSX). dryRun=true — перевірка без створення.</summary>
    [HttpPost("clients/{id}/registry")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Registry(string id, IFormFile file, [FromQuery] bool dryRun = true)
    {
        if (file == null || file.Length == 0) throw new ValidationException("Файл не передано (поле form-data «file»)");
        await using var s = file.OpenReadStream();
        return Ok(await _svc.ImportAsync(id, s, file.FileName, dryRun));
    }

    /// <summary>Зведений рахунок/акт платнику (клініка, страхова, підприємство, НСЗУ) за період по невиставлених нарахуваннях.</summary>
    [HttpPost("payers/{id}/invoices/period")] public async Task<IActionResult> PeriodInvoice(string id, [FromBody] PeriodInvoiceRequest req) => StatusCode(201, await _svc.PeriodInvoiceAsync(id, req));
    [HttpGet("payers/{id}/invoices")] public async Task<IActionResult> Invoices(string id) => Ok(await _svc.InvoicesAsync(id));
}
