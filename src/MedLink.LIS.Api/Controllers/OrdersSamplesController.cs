// Замовлення (повний життєвий цикл), проби (забір/прийом/відбраковка/алікотування/етапи), логістика, секції/журнали
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class OrdersController : LisControllerBase
{
    private readonly OrderService _orders;
    private readonly SampleService _samples;
    private readonly ReportService _reports;
    private readonly SectionJournalService _journal;
    public OrdersController(OrderService orders, SampleService samples, ReportService reports, SectionJournalService journal) { _orders = orders; _samples = samples; _reports = reports; _journal = journal; }

    /// <summary>Черга замовлень із фільтрами та пагінацією.</summary>
    [HttpGet("orders")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] bool? cito, [FromQuery] string? departmentId, [FromQuery] string? patientId, [FromQuery] string? search, [FromQuery] PagingQuery paging)
        => Ok(await _orders.ListAsync(status, from, to, cito, departmentId, patientId, search, paging));

    /// <summary>Пошук замовлення за штрихкодом пробірки.</summary>
    [HttpGet("orders/by-barcode/{barcode}")]
    public async Task<IActionResult> ByBarcode(string barcode) => Ok(await _orders.GetByBarcodeAsync(barcode) ?? throw NotFoundException.For("Замовлення за штрихкодом", barcode));

    [HttpGet("orders/{id}")]
    public async Task<IActionResult> Get(string id) => Ok(await _orders.GetAsync(id));

    /// <summary>Дозволені дії для поточного статусу та ролі + опис машини станів.</summary>
    [HttpGet("orders/{id}/transitions")]
    public async Task<IActionResult> Transitions(string id) => Ok(await _orders.TransitionsAsync(id));

    [HttpPost("orders")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req) { var o = await _orders.CreateAsync(req); return CreatedAtAction(nameof(Get), new { id = o.Id }, o); }

    [HttpPost("orders/batch")]
    public async Task<IActionResult> CreateBatch([FromBody] List<CreateOrderRequest> reqs) => StatusCode(201, await _orders.CreateBatchAsync(reqs));

    /// <summary>Редагування шапки замовлення (до забору).</summary>
    [HttpPut("orders/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateOrderRequest req) => Ok(await _orders.UpdateAsync(id, req));

    [HttpDelete("orders/{id}")]
    public async Task<IActionResult> Delete(string id) { await _orders.DeleteAsync(id); return NoContent(); }

    [HttpPost("orders/{id}/tests")]
    public async Task<IActionResult> AddTests(string id, [FromBody] AddTestsRequest req) => Ok(await _orders.AddTestsAsync(id, req));

    [HttpDelete("orders/{id}/tests/{orderTestId}")]
    public async Task<IActionResult> RemoveTest(string id, string orderTestId) => Ok(await _orders.RemoveTestAsync(id, orderTestId));

    [HttpPost("orders/{id}/samples")]
    public async Task<IActionResult> AddSample(string id, [FromBody] AddSampleRequest req) => Ok(await _orders.AddSampleAsync(id, req));

    [HttpPost("orders/{id}/cancel")]
    public async Task<IActionResult> Cancel(string id, [FromBody] ReasonRequest req) => Ok(await _orders.CancelAsync(id, req.Reason));

    [HttpPost("orders/{id}/reject")]
    public async Task<IActionResult> Reject(string id, [FromBody] ReasonRequest req) => Ok(await _orders.RejectAsync(id, req.Reason));

    /// <summary>Видача: усі тести верифіковано → RELEASED, mis_diagnostic_report, сповіщення пацієнта.</summary>
    [HttpPost("orders/{id}/release")]
    public async Task<IActionResult> Release(string id) => Ok(await _orders.ReleaseAsync(id));

    [HttpPost("orders/{id}/reopen")]
    public async Task<IActionResult> Reopen(string id, [FromBody] ReasonRequest? req) => Ok(await _orders.ReopenAsync(id, req?.Reason));

    /// <summary>HTML-бланк A4. ?variant=final|preliminary|cito.</summary>
    [HttpGet("orders/{id}/report")]
    [Produces("text/html")]
    public async Task<IActionResult> Report(string id, [FromQuery] string? variant) => Content(await _reports.HtmlAsync(id, Request.Host.Value, variant), "text/html; charset=utf-8");

    /// <summary>PDF-бланк (QuestPDF). Якщо PDF недоступний — HTML із заголовком X-MedLink-Pdf-Fallback.</summary>
    [HttpGet("orders/{id}/report.pdf")]
    public async Task<IActionResult> ReportPdf(string id, [FromQuery] string? variant)
    {
        var pdf = await _reports.PdfAsync(id, Request.Host.Value, variant);
        if (pdf == null)
        {
            Response.Headers["X-MedLink-Pdf-Fallback"] = "html";
            return Content(await _reports.HtmlAsync(id, Request.Host.Value, variant), "text/html; charset=utf-8");
        }
        return File(pdf, "application/pdf", $"report-{id}.pdf");
    }

    /// <summary>Етикетки всіх пробірок замовлення (ZPL + SVG).</summary>
    [HttpGet("orders/{id}/labels")]
    public async Task<IActionResult> Labels(string id) => Ok(await _samples.LabelsForOrderAsync(id));

    [HttpGet("orders/{id}/journal-entries")]
    public async Task<IActionResult> JournalEntries(string id) => Ok(await _journal.OrderEntriesAsync(id));

    /// <summary>Публічна перевірка автентичності бланка за QR-токеном (без ПІБ).</summary>
    [HttpGet("verify/{token}")]
    public async Task<IActionResult> Verify(string token) => Ok(await _orders.VerifyByTokenAsync(token) ?? throw new NotFoundException("Токен недійсний або результати ще не видано"));
}

public sealed class SamplesController : LisControllerBase
{
    private readonly SampleService _samples;
    private readonly SampleProcessingService _processing;
    public SamplesController(SampleService samples, SampleProcessingService processing) { _samples = samples; _processing = processing; }

    [HttpGet("samples")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? barcode, [FromQuery] string? orderId, [FromQuery] PagingQuery paging) => Ok(await _samples.ListAsync(status, barcode, orderId, paging));

    [HttpGet("samples/{barcode}")]
    public async Task<IActionResult> Get(string barcode) => Ok(_samples.ToDto(await _samples.LoadAsync(barcode)));

    /// <summary>Забір біоматеріалу з чек-листом (PHLEBOTOMIST).</summary>
    [HttpPost("samples/{barcode}/collect")]
    public async Task<IActionResult> Collect(string barcode, [FromBody] CollectSampleRequest? req) => Ok(await _samples.CollectAsync(barcode, req ?? new CollectSampleRequest()));

    [HttpPost("samples/{barcode}/uncollect")]
    public async Task<IActionResult> Uncollect(string barcode) => Ok(await _samples.UncollectAsync(barcode));

    /// <summary>Прийом у лабораторії (+ гемоліз/ліпемія/іктеричність/згусток) → реєстрація у журналах секцій.</summary>
    [HttpPost("samples/{barcode}/receive")]
    public async Task<IActionResult> Receive(string barcode, [FromBody] ReceiveSampleRequest? req) => Ok(await _samples.ReceiveAsync(barcode, req ?? new ReceiveSampleRequest()));

    [HttpPost("samples/{barcode}/reject")]
    public async Task<IActionResult> Reject(string barcode, [FromBody] RejectSampleRequest req) => Ok(await _samples.RejectAsync(barcode, req));

    /// <summary>Редагування відміток/об'єму (тип пробірки — лише до забору).</summary>
    [HttpPut("samples/{barcode}")]
    public async Task<IActionResult> Update(string barcode, [FromBody] SampleFlagsRequest req) => Ok(await _samples.UpdateAsync(barcode, req));

    /// <summary>Заміна пробірки: новий штрихкод, статус PENDING.</summary>
    [HttpPost("samples/{barcode}/replace")]
    public async Task<IActionResult> Replace(string barcode) => Ok(await _samples.ReplaceAsync(barcode));

    [HttpDelete("samples/{barcode}")]
    public async Task<IActionResult> Delete(string barcode) { await _samples.DeleteAsync(barcode); return NoContent(); }

    /// <summary>Довільний перехід машини станів проби: PROCESS | STORE | DISPOSE | DISPATCH | RECEIVE.</summary>
    [HttpPost("samples/{barcode}/transition")]
    public async Task<IActionResult> Transition(string barcode, [FromBody] TransitionRequest req) => Ok(await _samples.TransitionAsync(barcode, req.Status));

    [HttpGet("samples/{barcode}/label")]
    public async Task<IActionResult> Label(string barcode) => Ok(await _samples.LabelAsync(barcode));

    /// <summary>Алікотування / касети / блоки / скельця: дочірні проби з похідними штрихкодами.</summary>
    [HttpPost("samples/{barcode}/split")]
    public async Task<IActionResult> Split(string barcode, [FromBody] SplitSampleRequest req) => StatusCode(201, await _processing.SplitAsync(barcode, req));

    [HttpGet("samples/{barcode}/tree")]
    public async Task<IActionResult> Tree(string barcode) => Ok(await _processing.TreeAsync(barcode));

    /// <summary>Фіксація етапу обробки за шаблоном робочого процесу секції.</summary>
    [HttpPost("samples/{barcode}/stage")]
    public async Task<IActionResult> Stage(string barcode, [FromBody] StageRequest req) => Ok(await _processing.AddStageAsync(barcode, req));

    [HttpGet("samples/{barcode}/stages")]
    public async Task<IActionResult> Stages(string barcode) => Ok(await _processing.StagesAsync(barcode));

    [HttpDelete("samples/stage-events/{eventId}")]
    public async Task<IActionResult> DeleteStage(string eventId) { await _processing.DeleteStageEventAsync(eventId); return NoContent(); }

    [HttpGet("workflow-templates")]
    public async Task<IActionResult> Templates() => Ok(await _processing.TemplatesAsync());

    [HttpPut("workflow-templates/{code}")]
    public async Task<IActionResult> UpsertTemplate(string code, [FromBody] WorkflowTemplateRequest req) => Ok(await _processing.UpsertTemplateAsync(code, req.Name, req.Stages));
}

public sealed class WorkflowTemplateRequest
{
    public string Name { get; set; } = "";
    public List<Data.Entities.WorkflowStage> Stages { get; set; } = new();
}

public sealed class LogisticsController : LisControllerBase
{
    private readonly LogisticsService _logistics;
    public LogisticsController(LogisticsService logistics) => _logistics = logistics;

    [HttpGet("logistics/manifests")]
    public async Task<IActionResult> List([FromQuery] string? status) => Ok(await _logistics.ListAsync(status));

    [HttpGet("logistics/manifests/{id}")]
    public async Task<IActionResult> Get(string id) => Ok(await _logistics.GetAsync(id));

    /// <summary>Створення маніфесту → DISPATCHED (проби → IN_TRANSIT); dispatchNow=false — чернетка.</summary>
    [HttpPost("logistics/manifests")]
    public async Task<IActionResult> Create([FromBody] ManifestRequest req) => StatusCode(201, await _logistics.CreateAsync(req));

    [HttpPut("logistics/manifests/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ManifestRequest req) => Ok(await _logistics.UpdateAsync(id, req));

    [HttpDelete("logistics/manifests/{id}")]
    public async Task<IActionResult> Delete(string id) { await _logistics.DeleteAsync(id); return NoContent(); }

    [HttpDelete("logistics/manifests/{id}/items/{itemId}")]
    public async Task<IActionResult> RemoveItem(string id, string itemId) => Ok(await _logistics.RemoveItemAsync(id, itemId));

    [HttpPost("logistics/manifests/{id}/dispatch")]
    public async Task<IActionResult> Dispatch(string id) => Ok(await _logistics.DispatchAsync(id));

    /// <summary>Прийом у лабораторії з перевіркою холодового ланцюга (+2..+8 °C).</summary>
    [HttpPost("logistics/manifests/{id}/receive")]
    public async Task<IActionResult> Receive(string id, [FromBody] ManifestReceiveRequest req) => Ok(await _logistics.ReceiveAsync(id, req));

    [HttpPost("logistics/manifests/{id}/reject")]
    public async Task<IActionResult> Reject(string id, [FromBody] ReasonRequest req) => Ok(await _logistics.RejectAsync(id, req.Reason));
}

public sealed class SectionsController : LisControllerBase
{
    private readonly SectionJournalService _sections;
    public SectionsController(SectionJournalService sections) => _sections = sections;

    [HttpGet("sections")]
    public async Task<IActionResult> List([FromQuery] bool? isActive) => Ok(await _sections.ListAsync(isActive));

    [HttpGet("sections/{id}")]
    public async Task<IActionResult> Get(string id) => Ok(await _sections.GetAsync(id));

    [HttpPost("sections")]
    public async Task<IActionResult> Create([FromBody] SectionRequest req) => StatusCode(201, await _sections.CreateAsync(req));

    [HttpPut("sections/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] SectionRequest req) => Ok(await _sections.UpdateAsync(id, req));

    [HttpDelete("sections/{id}")]
    public async Task<IActionResult> Delete(string id) => Ok(await _sections.DeleteAsync(id));

    /// <summary>Журнал відділення за датою/періодом.</summary>
    [HttpGet("sections/{id}/journal")]
    public async Task<IActionResult> Journal(string id, [FromQuery] DateTime? date, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? status, [FromQuery] PagingQuery paging)
        => Ok(await _sections.JournalAsync(id, date, from, to, status, paging));

    /// <summary>Dry-run нумерації за маскою (без зміни лічильників).</summary>
    [HttpPost("sections/{id}/journal/renumber-preview")]
    public async Task<IActionResult> RenumberPreview(string id, [FromBody] RenumberPreviewRequest? req)
        => Ok(await _sections.RenumberPreviewAsync(id, req?.Mask, req?.ResetPeriod, req?.At, req?.Count ?? 5));

    [HttpPut("sections/journal-entries/{entryId}/status")]
    public async Task<IActionResult> EntryStatus(string entryId, [FromBody] TransitionRequest req) => Ok(await _sections.UpdateEntryStatusAsync(entryId, req.Status));
}

public sealed class RenumberPreviewRequest
{
    public string? Mask { get; set; }
    public string? ResetPeriod { get; set; }
    public DateTime? At { get; set; }
    public int Count { get; set; } = 5;
}
