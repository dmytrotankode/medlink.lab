// Зовнішні лабораторії: виконавці та їхні прайси, маршрутизація тестів, реєстри відправки, результати, PDF-бланки
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class SendOutController : LisControllerBase
{
    private readonly SendOutService _svc;
    public SendOutController(SendOutService svc) => _svc = svc;

    // ---- лабораторії-виконавці
    /// <summary>Лабораторії-виконавці (власна та зовнішні) з їхніми прайсами показників.</summary>
    [HttpGet("performers")] public async Task<IActionResult> Performers([FromQuery] bool includeInactive = false) => Ok(await _svc.PerformersAsync(includeInactive));
    [HttpGet("performers/{id}")] public async Task<IActionResult> Performer(string id) => Ok(await _svc.PerformerAsync(id));
    [HttpPost("performers")] public async Task<IActionResult> CreatePerformer([FromBody] LabPerformer req) => StatusCode(201, await _svc.SavePerformerAsync(null, req));
    [HttpPut("performers/{id}")] public async Task<IActionResult> UpdatePerformer(string id, [FromBody] LabPerformer req) => Ok(await _svc.SavePerformerAsync(id, req));
    [HttpDelete("performers/{id}")] public async Task<IActionResult> DeletePerformer(string id) => Ok(await _svc.DeletePerformerAsync(id));
    /// <summary>Повна заміна прайсу зовнішньої лабораторії (код/назва в її прайсі, вартість, TAT, маршрут за замовчуванням).</summary>
    [HttpPut("performers/{id}/tests")] public async Task<IActionResult> PerformerTests(string id, [FromBody] List<PerformerTestRequest> items) => Ok(await _svc.SetPerformerTestsAsync(id, items));

    // ---- маршрутизація
    /// <summary>Змінити виконавця тесту замовлення (до відправки/виконання). performerId=null — власна лабораторія.</summary>
    [HttpPut("order-tests/{orderTestId}/performer")] public async Task<IActionResult> Route(string orderTestId, [FromBody] RouteTestRequest req) => Ok(await _svc.RouteTestAsync(orderTestId, req));

    // ---- реєстри відправки
    /// <summary>Черга «до відправки»: тести, маршрутизовані до зовнішніх лабораторій і ще не включені в реєстр.</summary>
    [HttpGet("send-out/queue")] public async Task<IActionResult> Queue([FromQuery] string? performerId) => Ok(await _svc.QueueAsync(performerId));
    [HttpGet("send-out")] public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? performerId) => Ok(await _svc.ListAsync(status, performerId));
    [HttpGet("send-out/{id}")] public async Task<IActionResult> Get(string id) => Ok(await _svc.GetAsync(id));
    [HttpPost("send-out")] public async Task<IActionResult> Create([FromBody] CreateSendOutRequest req) => StatusCode(201, await _svc.CreateAsync(req));
    [HttpPost("send-out/{id}/dispatch")] public async Task<IActionResult> Dispatch(string id, [FromBody] DispatchSendOutRequest req) => Ok(await _svc.DispatchAsync(id, req));
    [HttpPost("send-out/{id}/accept")] public async Task<IActionResult> Accept(string id, [FromBody] AcceptSendOutRequest req) => Ok(await _svc.AcceptAsync(id, req));
    [HttpPost("send-out/{id}/cancel")] public async Task<IActionResult> Cancel(string id, [FromBody] SendOutItemRejectRequest? req) => Ok(await _svc.CancelAsync(id, req?.Reason));
    [HttpPost("send-out/items/{itemId}/result")] public async Task<IActionResult> Result(string itemId, [FromBody] SendOutResultRequest req) => Ok(await _svc.EnterResultAsync(itemId, req));
    [HttpPost("send-out/items/{itemId}/reject")] public async Task<IActionResult> Reject(string itemId, [FromBody] SendOutItemRejectRequest req) => Ok(await _svc.RejectItemAsync(itemId, req));
    [HttpPost("send-out/items/{itemId}/recall")] public async Task<IActionResult> Recall(string itemId, [FromBody] SendOutItemRejectRequest req) => Ok(await _svc.RejectItemAsync(itemId, req, recallOnly: true));

    /// <summary>Імпорт файлу результатів зовнішньої лабораторії (CSV/XLSX/XML). dryRun=true — попередній перегляд зіставлення.</summary>
    [HttpPost("send-out/{id}/import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Import(string id, IFormFile file, [FromQuery] bool dryRun = true)
    {
        if (file == null || file.Length == 0) throw new ValidationException("Файл не передано (поле form-data «file»)");
        await using var s = file.OpenReadStream();
        return Ok(await _svc.ImportResultsAsync(id, s, file.FileName, dryRun));
    }

    // ---- вкладення (бланки зовнішніх лабораторій)
    [HttpGet("orders/{orderId}/attachments")] public async Task<IActionResult> Attachments(string orderId) => Ok(await _svc.AttachmentsAsync(orderId));

    [HttpPost("orders/{orderId}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21_000_000)]
    public async Task<IActionResult> Attach(string orderId, IFormFile file, [FromQuery] string? sendOutId, [FromQuery] bool visibleToPatient = true)
    {
        if (file == null || file.Length == 0) throw new ValidationException("Файл не передано (поле form-data «file»)");
        await using var s = file.OpenReadStream();
        return StatusCode(201, await _svc.AddAttachmentAsync(orderId, s, file.FileName, file.ContentType, sendOutId, visibleToPatient));
    }

    [HttpGet("attachments/{id}/content")]
    public async Task<IActionResult> Content(string id)
    {
        var a = await _svc.AttachmentAsync(id);
        return File(a.Content, a.ContentType, a.FileName);
    }
}
