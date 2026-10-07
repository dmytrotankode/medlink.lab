// Біобанк, реагенти, мікробіологія, аналітика/аудит, налаштування, портал пацієнта, імпорт результатів
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Controllers;

public sealed class BiobankController : LisControllerBase
{
    private readonly BiobankService _biobank;
    public BiobankController(BiobankService biobank) => _biobank = biobank;

    [HttpGet("biobank/racks")] public async Task<IActionResult> Racks() => Ok(await _biobank.RacksAsync());
    [HttpGet("biobank/racks/{id}")] public async Task<IActionResult> Rack(string id) => Ok(await _biobank.RackAsync(id));
    [HttpPost("biobank/racks")] public async Task<IActionResult> CreateRack([FromBody] RackRequest req) => StatusCode(201, await _biobank.CreateRackAsync(req));
    [HttpPut("biobank/racks/{id}")] public async Task<IActionResult> UpdateRack(string id, [FromBody] RackRequest req) => Ok(await _biobank.UpdateRackAsync(id, req));
    [HttpDelete("biobank/racks/{id}")] public async Task<IActionResult> DeleteRack(string id) { await _biobank.DeleteRackAsync(id); return NoContent(); }
    /// <summary>Матриця комірок штатива (A01..H12).</summary>
    [HttpGet("biobank/racks/{id}/cells")] public async Task<IActionResult> Cells(string id) => Ok(await _biobank.CellsAsync(id));
    [HttpPost("biobank/cells/place")] public async Task<IActionResult> Place([FromBody] PlaceCellRequest req) => StatusCode(201, await _biobank.PlaceAsync(req));
    [HttpPost("biobank/cells/{id}/remove")] public async Task<IActionResult> Remove(string id, [FromBody] ReasonRequest req) => Ok(await _biobank.RemoveAsync(id, req.Reason));
    [HttpPut("biobank/cells/{id}")] public async Task<IActionResult> UpdateCell(string id, [FromBody] CellUpdateRequest req) => Ok(await _biobank.UpdateCellAsync(id, req.ExpiryAt));
    [HttpDelete("biobank/cells/{id}")] public async Task<IActionResult> DeleteCell(string id) { await _biobank.DeleteCellAsync(id); return NoContent(); }
    [HttpGet("biobank/search")] public async Task<IActionResult> Search([FromQuery] string barcode) => Ok(await _biobank.SearchAsync(barcode) ?? throw NotFoundException.For("Зразок у біобанку", barcode));
    [HttpGet("biobank/expired")] public async Task<IActionResult> Expired() => Ok(await _biobank.ExpiredAsync());
}

public sealed class CellUpdateRequest { public DateTime? ExpiryAt { get; set; } }


public sealed class ReagentsController : LisControllerBase
{
    private readonly ReagentService _reagents;
    public ReagentsController(ReagentService reagents) => _reagents = reagents;

    [HttpGet("reagents/lots")] public async Task<IActionResult> List([FromQuery] string? analyzerId, [FromQuery] bool? isActive) => Ok(await _reagents.ListAsync(analyzerId, isActive));
    [HttpGet("reagents/lots/{id}")] public async Task<IActionResult> Get(string id) => Ok(await _reagents.GetAsync(id));
    [HttpPost("reagents/lots")] public async Task<IActionResult> Create([FromBody] ReagentLotRequest req) => StatusCode(201, await _reagents.CreateAsync(req));
    [HttpPut("reagents/lots/{id}")] public async Task<IActionResult> Update(string id, [FromBody] ReagentLotRequest req) => Ok(await _reagents.UpdateAsync(id, req));
    [HttpDelete("reagents/lots/{id}")] public async Task<IActionResult> Delete(string id) { await _reagents.DeleteAsync(id); return NoContent(); }
    /// <summary>Списання тест-доз.</summary>
    [HttpPost("reagents/lots/{id}/consume")] public async Task<IActionResult> Consume(string id, [FromBody] ConsumeRequest req) => Ok(await _reagents.ConsumeAsync(id, req.Tests));
    /// <summary>Попередження: мінімум/термін/on-board стабільність.</summary>
    [HttpGet("reagents/alerts")] public async Task<IActionResult> Alerts() => Ok(await _reagents.AlertsAsync());
}

public sealed class ConsumeRequest { public int Tests { get; set; } }

public sealed class MicrobiologyController : LisControllerBase
{
    private readonly MicrobiologyService _micro;
    public MicrobiologyController(MicrobiologyService micro) => _micro = micro;

    [HttpGet("microbiology/cultures")] public async Task<IActionResult> List([FromQuery] string? status) => Ok(await _micro.ListAsync(status));
    [HttpGet("microbiology/cultures/{id}")] public async Task<IActionResult> Get(string id) => Ok(await _micro.GetAsync(id));
    [HttpPost("microbiology/cultures")] public async Task<IActionResult> Create([FromBody] CultureRequest req) => StatusCode(201, await _micro.CreateAsync(req));
    [HttpPut("microbiology/cultures/{id}")] public async Task<IActionResult> Update(string id, [FromBody] CultureRequest req) => Ok(await _micro.UpdateAsync(id, req));
    [HttpDelete("microbiology/cultures/{id}")] public async Task<IActionResult> Delete(string id) { await _micro.DeleteAsync(id); return NoContent(); }
    /// <summary>Перехід статусу посіву: PRELIMINARY | ISOLATE | COMPLETE.</summary>
    [HttpPost("microbiology/cultures/{id}/transition")] public async Task<IActionResult> Transition(string id, [FromBody] Models.TransitionRequest req) => Ok(await _micro.TransitionAsync(id, req.Status, req.Comment));
    [HttpPost("microbiology/cultures/{id}/isolates")] public async Task<IActionResult> AddIsolate(string id, [FromBody] IsolateRequest req) => StatusCode(201, await _micro.AddIsolateAsync(id, req));
    [HttpPut("microbiology/isolates/{id}")] public async Task<IActionResult> UpdateIsolate(string id, [FromBody] IsolateRequest req) => Ok(await _micro.UpdateIsolateAsync(id, req));
    [HttpDelete("microbiology/isolates/{id}")] public async Task<IActionResult> DeleteIsolate(string id) { await _micro.DeleteIsolateAsync(id); return NoContent(); }
    /// <summary>Антибіотикограма → S/I/R за EUCAST + фенотипи MRSA/ESBL/CRE/VRE.</summary>
    [HttpPost("microbiology/isolates/{id}/susceptibility")] public async Task<IActionResult> AddSusceptibility(string id, [FromBody] SusceptibilityRequest req) => StatusCode(201, await _micro.AddSusceptibilityAsync(id, req));
    [HttpDelete("microbiology/susceptibility/{id}")] public async Task<IActionResult> DeleteSusceptibility(string id) { await _micro.DeleteSusceptibilityAsync(id); return NoContent(); }
    [HttpGet("microbiology/cultures/{id}/report")]
    [Produces("text/html")]
    public async Task<IActionResult> Report(string id) => Content(await _micro.ReportHtmlAsync(id), "text/html; charset=utf-8");
}

public sealed class AnalyticsController : LisControllerBase
{
    private readonly AnalyticsService _analytics;
    private readonly RetentionCleanupService _retention;
    private readonly IRolePolicy _policy;
    public AnalyticsController(AnalyticsService analytics, RetentionCleanupService retention, IRolePolicy policy) { _analytics = analytics; _retention = retention; _policy = policy; }

    /// <summary>TAT: медіана/P90 за етапами, профілями, CITO; порушення SLA.</summary>
    [HttpGet("analytics/tat")]
    public async Task<IActionResult> Tat([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? profileId, [FromQuery] bool? cito) => Ok(await _analytics.TatAsync(from, to, profileId, cito));

    /// <summary>Обсяги: groupBy=profile|analyzer|employee|day|test|department.</summary>
    [HttpGet("analytics/volume")]
    public async Task<IActionResult> Volume([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string groupBy = "profile") => Ok(await _analytics.VolumeAsync(from, to, groupBy));

    /// <summary>Експорт XLSX: report=orders|tat|volume|qc.</summary>
    [HttpGet("analytics/export.xlsx")]
    public async Task<IActionResult> Export([FromQuery] string report = "orders", [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        => File(await _analytics.ExportXlsxAsync(report, from, to), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"medlink-{report}-{DateTime.UtcNow:yyyyMMdd}.xlsx");

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] string? entity, [FromQuery] string? entityId, [FromQuery] string? userId, [FromQuery] string? action, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] PagingQuery paging)
        => Ok(await _analytics.AuditAsync(entity, entityId, userId, action, from, to, paging));

    /// <summary>Негайне очищення журналів за політикою зберігання (LAB_ADMIN).</summary>
    [HttpPost("maintenance/retention/run")]
    public async Task<IActionResult> RunRetention() { _policy.Require("Очищення журналів", LabRoles.Admin); var (m, l) = await _retention.RunOnceAsync(); return Ok(new { deletedMessages = m, deletedLogs = l }); }
}

public sealed class SettingsController : LisControllerBase
{
    private readonly SettingsService _settings;
    private readonly DictionaryService _dict;
    public SettingsController(SettingsService settings, DictionaryService dict) { _settings = settings; _dict = dict; }

    /// <summary>Реквізити лабораторії, принтер етикеток (TCP/RAW 9100), шаблони бланків.</summary>
    [HttpGet("settings/lab")] public async Task<IActionResult> Lab() => Ok(await _settings.GetAsync());
    [HttpPut("settings/lab")] public async Task<IActionResult> UpdateLab([FromBody] LabSettingsRequest req) => Ok(await _settings.UpdateAsync(req));

    /// <summary>Користувачі = org_employee (CRUD, LAB_ADMIN).</summary>
    [HttpGet("settings/users")] public async Task<IActionResult> Users([FromQuery] string? search, [FromQuery] bool? isActive, [FromQuery] PagingQuery paging) => Ok(await _dict.ListAsync("employees", search, isActive, paging));
    [HttpGet("settings/users/{id}")] public async Task<IActionResult> User(string id) => Ok(await _dict.GetAsync("employees", id));
    [HttpPost("settings/users")] public async Task<IActionResult> CreateUser([FromBody] System.Text.Json.JsonElement body) => StatusCode(201, await _dict.CreateAsync("employees", body));
    [HttpPut("settings/users/{id}")] public async Task<IActionResult> UpdateUser(string id, [FromBody] System.Text.Json.JsonElement body) => Ok(await _dict.UpdateAsync("employees", id, body));
    [HttpDelete("settings/users/{id}")] public async Task<IActionResult> DeleteUser(string id) => Ok(await _dict.DeleteAsync("employees", id));

    [HttpGet("settings/numerators")] public async Task<IActionResult> Numerators() => Ok(await _settings.NumeratorsAsync());
    [HttpPut("settings/numerators/{code}")] public async Task<IActionResult> UpdateNumerator(string code, [FromBody] NumeratorRequest req) => Ok(await _settings.UpdateNumeratorAsync(code, req));
    [HttpGet("settings/counters")] public async Task<IActionResult> Counters() => Ok(await _settings.CountersAsync());
}

/// <summary>Кабінет пацієнта: лише відкриті (released) результати, прогрес виконання, тренди, PDF.</summary>
public sealed class PortalController : LisControllerBase
{
    private readonly LisDbContext _db;
    private readonly OrderService _orders;
    private readonly PatientService _patients;
    private readonly ReportService _reports;
    public PortalController(LisDbContext db, OrderService orders, PatientService patients, ReportService reports) { _db = db; _orders = orders; _patients = patients; _reports = reports; }

    [HttpGet("portal/{patientId}/orders")]
    public async Task<IActionResult> Orders(string patientId)
    {
        await _patients.LoadAsync(patientId);
        var orders = await _orders.FullQuery().AsNoTracking().Where(o => o.PatientId == patientId && o.Status != OrderStatuses.Cancelled).OrderByDescending(o => o.OrderDatetime).ToListAsync();
        return Ok(orders.Select(o => ProgressiveReleaseService.PortalOrder(o, includeTests: false)));
    }

    [HttpGet("portal/{patientId}/orders/{id}")]
    public async Task<IActionResult> Order(string patientId, string id)
    {
        var order = await _orders.FullQuery().AsNoTracking().FirstOrDefaultAsync(o => o.Id == id && o.PatientId == patientId) ?? throw NotFoundException.For("Замовлення пацієнта", id);
        return Ok(ProgressiveReleaseService.PortalOrder(order, includeTests: true));
    }

    /// <summary>PDF бланка для пацієнта: доступний після повної видачі; інакше — попередній (лише відкриті тести).</summary>
    [HttpGet("portal/{patientId}/orders/{id}/report.pdf")]
    public async Task<IActionResult> ReportPdf(string patientId, string id)
    {
        var order = await _db.Orders.AsNoTracking().Include(o => o.Tests).FirstOrDefaultAsync(o => o.Id == id && o.PatientId == patientId) ?? throw NotFoundException.For("Замовлення пацієнта", id);
        if (!order.Tests.Any(t => t.ReleasedAt != null)) throw new ConflictException("Результати ще не відкрито пацієнту");
        var variant = order.Status == OrderStatuses.Released ? "final" : "preliminary";
        var pdf = await _reports.PdfAsync(id, Request.Host.Value, variant);
        if (pdf == null) { Response.Headers["X-MedLink-Pdf-Fallback"] = "html"; return Content(await _reports.HtmlAsync(id, Request.Host.Value, variant), "text/html; charset=utf-8"); }
        return File(pdf, "application/pdf", $"results-{order.OrderNumber}.pdf");
    }

    [HttpGet("portal/{patientId}/trend/{testCode}")]
    public async Task<IActionResult> Trend(string patientId, string testCode)
    {
        await _patients.LoadAsync(patientId);
        var results = await _db.Results.AsNoTracking().Include(r => r.OrderTest).ThenInclude(t => t!.Order)
            .Where(r => r.OrderTest!.TestCode == testCode && r.OrderTest.Order!.PatientId == patientId && r.OrderTest.ReleasedAt != null && r.OrderTest.Status != OrderTestStatuses.Rejected)
            .OrderBy(r => r.EnteredAt).ToListAsync();
        return Ok(results.Select(r => new { at = r.EnteredAt, value = r.NumericValue, stringValue = r.StringValue, normLow = r.NormLow, normHigh = r.NormHigh, flag = r.Flag, unit = r.Unit, orderNumber = r.OrderTest?.Order?.OrderNumber }));
    }

    [HttpGet("portal/{patientId}/history")]
    public async Task<IActionResult> History(string patientId) => Ok(await _patients.HistoryAsync(patientId, releasedOnly: true));

    [HttpGet("portal/{patientId}/notifications")]
    public async Task<IActionResult> Notifications(string patientId)
    {
        await _patients.LoadAsync(patientId);
        return Ok(await _db.Notifications.AsNoTracking().Where(n => n.PatientId == patientId).OrderByDescending(n => n.CreatedOn).Take(100).ToListAsync());
    }
}

public sealed class ImportController : LisControllerBase
{
    private readonly ImportService _import;
    public ImportController(ImportService import) => _import = import;

    /// <summary>Імпорт результатів із CSV/XLSX/XML (multipart "file"), ?dryRun=true → попередній перегляд.</summary>
    [HttpPost("import/results")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Results(IFormFile file, [FromQuery] bool dryRun = true, [FromQuery] string? analyzerId = null)
    {
        if (file == null || file.Length == 0) throw new ValidationException("Файл не передано (поле form-data «file»)");
        await using var s = file.OpenReadStream();
        return Ok(await _import.ImportAsync(s, file.FileName, dryRun, analyzerId));
    }
}
