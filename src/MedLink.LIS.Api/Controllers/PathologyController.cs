// ТЗ 22.13: модуль «Патоморфологія» — прийом, журнал і картка справи ПГД, вирізка, етапи, ІГХ, висновок, CITO, архів, друк 014/о
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class PathologyController : LisControllerBase
{
    private readonly PathologyService _svc;
    public PathologyController(PathologyService svc) => _svc = svc;

    /// <summary>Черга прийому (FR-PAT-001): направлення PATHOLOGY з усіх джерел.</summary>
    [HttpGet("pathology/incoming")] public async Task<IActionResult> Incoming() => Ok(await _svc.IncomingAsync());
    /// <summary>Пряме створення направлення ПГД (те саме, що POST /mis/referrals з type=PATHOLOGY).</summary>
    [HttpPost("pathology/referrals")] public async Task<IActionResult> Create([FromBody] PathologyReferralRequest req) => StatusCode(201, await _svc.CreateReferralAsync(req));
    [HttpPost("pathology/incoming/{id}/accept")] public async Task<IActionResult> Accept(string id, [FromBody] PathAcceptRequest req) => Ok(await _svc.AcceptAsync(id, req));
    [HttpPost("pathology/incoming/{id}/reject")] public async Task<IActionResult> Reject(string id, [FromBody] PathRejectRequest req) => Ok(await _svc.RejectAsync(id, req));
    [HttpPost("pathology/incoming/{id}/query")] public async Task<IActionResult> Query(string id, [FromBody] PathQueryRequest req) => Ok(await _svc.QueryAsync(id, req));

    /// <summary>Журнал справ: stage=RCV|GROSS|…|work|lab|cito|overdue, src=INPATIENT|POLYCLINIC|EHEALTH|PARTNER, q — № / ПІБ / штрихкод.</summary>
    [HttpGet("pathology/cases")] public async Task<IActionResult> Cases([FromQuery] string? stage, [FromQuery] string? src, [FromQuery] string? q, [FromQuery] string? group) => Ok(await _svc.CasesAsync(stage, src, q, group));
    [HttpGet("pathology/summary")] public async Task<IActionResult> Summary() => Ok(await _svc.SummaryAsync());
    [HttpGet("pathology/cases/{id}")] public async Task<IActionResult> Case(string id) => Ok(await _svc.CaseAsync(id));
    [HttpPut("pathology/cases/{id}/gross")] public async Task<IActionResult> Gross(string id, [FromBody] PathGrossRequest req) => Ok(await _svc.SaveGrossAsync(id, req));
    [HttpPost("pathology/cases/{id}/blocks")] public async Task<IActionResult> Blocks(string id, [FromBody] PathGrossRequest req) => Ok(await _svc.SaveGrossAsync(id, req));
    /// <summary>Етап обробки за штрихкодом касети/скельця (FR-PAT-023).</summary>
    [HttpPost("pathology/scan")] public async Task<IActionResult> Scan([FromBody] PathScanRequest req) => Ok(await _svc.ScanAsync(req));
    [HttpPost("pathology/cases/{id}/advance")] public async Task<IActionResult> Advance(string id, [FromBody] PathScanRequest req) => Ok(await _svc.AdvanceAsync(id, req));
    [HttpPost("pathology/cases/{id}/additional")] public async Task<IActionResult> Additional(string id, [FromBody] PathAdditionalRequest req) => Ok(await _svc.AdditionalAsync(id, req));
    [HttpPost("pathology/slides/{id}/ready")] public async Task<IActionResult> SlideReady(string id) => Ok(await _svc.SlideReadyAsync(id));

    [HttpPut("pathology/cases/{id}/report")] public async Task<IActionResult> Report(string id, [FromBody] PathReportRequest req) => Ok(await _svc.SaveReportAsync(id, req));
    [HttpPost("pathology/cases/{id}/report/preliminary")] public async Task<IActionResult> Preliminary(string id, [FromBody] PathPreliminaryRequest req) => Ok(await _svc.PreliminaryAsync(id, req));
    [HttpPost("pathology/cases/{id}/report/sign")] public async Task<IActionResult> Sign(string id, [FromBody] PathSignRequest req) => Ok(await _svc.SignAsync(id, req));
    [HttpPost("pathology/cases/{id}/report/addendum")] public async Task<IActionResult> Addendum(string id, [FromBody] PathAddendumRequest req) => Ok(await _svc.AddendumAsync(id, req));
    [HttpPost("pathology/cases/{id}/cito-notice")] public async Task<IActionResult> CitoNotice(string id, [FromBody] PathCitoNoticeRequest req) => Ok(await _svc.CitoNoticeAsync(id, req));

    /// <summary>Архів блоків і скелець (FR-PAT-070…072): status=ARCHIVED|ISSUED|overdue.</summary>
    [HttpGet("pathology/archive")] public async Task<IActionResult> Archive([FromQuery] string? q, [FromQuery] string? status) => Ok(await _svc.ArchiveAsync(q, status));
    [HttpPost("pathology/blocks/{id}/issue")] public async Task<IActionResult> Issue(string id, [FromBody] PathIssueRequest req) => Ok(await _svc.IssueAsync(id, req));
    /// <summary>Друкований акт видачі (PDF) за ідентифікатором видачі.</summary>
    [HttpGet("pathology/issues/{id}/act.pdf")] public async Task<IActionResult> IssueAct(string id) => File(await _svc.IssueActPdfAsync(id), "application/pdf", "issue-act.pdf");
    [HttpPost("pathology/blocks/{id}/return")] public async Task<IActionResult> Return(string id) => Ok(await _svc.ReturnAsync(id));

    [HttpPost("pathology/slides/{id}/dispose")] public async Task<IActionResult> DisposeSlide(string id, [FromBody] PathDisposeRequest req) => Ok(await _svc.DisposeSlideAsync(id, req));
    [HttpPut("pathology/blocks/{id}/storage")] public async Task<IActionResult> SetStorage(string id, [FromBody] PathStorageRequest req) => Ok(await _svc.SetStorageAsync(id, req));
    [HttpGet("pathology/rooms")] public async Task<IActionResult> Rooms() => Ok(await _svc.RoomsAsync());
    [HttpPost("pathology/rooms")] public async Task<IActionResult> AddRoom([FromBody] PathRoomRequest req) => Ok(await _svc.AddRoomAsync(req));
    [HttpPut("pathology/methods/{code}/price")] public async Task<IActionResult> MethodPrice(string code, [FromBody] PathMethodPriceRequest req) => Ok(await _svc.SetMethodPriceAsync(code, req));
    [HttpGet("pathology/staff")] public async Task<IActionResult> Staff() => Ok(await _svc.StaffAsync());
    [HttpPost("pathology/cases/{id}/dispatch")] public async Task<IActionResult> Dispatch(string id, [FromBody] PathDispatchRequest req) => Ok(await _svc.DispatchAsync(id, req));
    [HttpGet("pathology/cases/{id}/dispatches")] public async Task<IActionResult> Dispatches(string id) => Ok(await _svc.DispatchesAsync(id));

    // цитологія (розд. 22.19)
    [HttpPost("pathology/cases/{id}/prepare")] public async Task<IActionResult> Prepare(string id, [FromBody] PathPrepareRequest req) => Ok(await _svc.PrepareAsync(id, req));
    [HttpPost("pathology/cases/{id}/prepare-repeat")] public async Task<IActionResult> PrepareRepeat(string id, [FromBody] PathRepeatRequest req) => Ok(await _svc.RepeatPrepAsync(id, req));
    [HttpPost("pathology/cases/{id}/screening")] public async Task<IActionResult> Screening(string id, [FromBody] PathScreeningRequest req) => Ok(await _svc.ScreeningAsync(id, req));
    [HttpGet("pathology/cytology/analytics")] public async Task<IActionResult> CytoAnalytics() => Ok(await _svc.CytoAnalyticsAsync());
    [HttpGet("pathology/dictionaries")] public async Task<IActionResult> Dictionaries() => Ok(await _svc.DictionariesAsync());
    /// <summary>Поля форми 014/о (сторони 1 і 2) для екрана «Друк 014/о».</summary>
    [HttpGet("pathology/cases/{id}/form014")] public async Task<IActionResult> Form014(string id) => Ok(await _svc.Form014Async(id));
    [HttpGet("pathology/cases/{id}/form014.pdf")]
    public async Task<IActionResult> Form014Pdf(string id, [FromQuery] string? sides) => File(await _svc.Form014PdfAsync(id, sides), "application/pdf", "form014.pdf");
}
