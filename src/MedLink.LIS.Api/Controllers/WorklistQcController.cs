// Робочий лист, результати, панічні дзвінки, незв'язані результати, батчі; ВКЯ (Вестгард, Леві-Дженнінгс, lockout)
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class WorklistController : LisControllerBase
{
    private readonly WorklistService _worklist;
    private readonly ResultPipelineService _pipeline;
    private readonly ICurrentEmployee _current;
    public WorklistController(WorklistService worklist, ResultPipelineService pipeline, ICurrentEmployee current) { _worklist = worklist; _pipeline = pipeline; _current = current; }

    /// <summary>Рядки робочого листа (join + projection, пагінація).</summary>
    [HttpGet("worklist")]
    public async Task<IActionResult> Query([FromQuery] WorklistFilter filter, [FromQuery] PagingQuery paging) => Ok(await _worklist.QueryAsync(filter, paging));

    [HttpGet("worklist/summary")]
    public async Task<IActionResult> Summary() => Ok(await _worklist.SummaryAsync());

    /// <summary>Повторна автоверифікація вибраних (або всіх RESULTED) тестів.</summary>
    [HttpPost("worklist/autoverify")]
    public async Task<IActionResult> AutoVerify([FromBody] AutoVerifyRequest? req) => Ok(await _pipeline.AutoVerifyAsync(req?.OrderTestIds));

    [HttpGet("worklist/batches")]
    public async Task<IActionResult> Batches([FromQuery] string? status) => Ok(await _worklist.BatchesAsync(status));

    [HttpPost("worklist/batches")]
    public async Task<IActionResult> CreateBatch([FromBody] BatchRequest req) => StatusCode(201, await _worklist.CreateBatchAsync(req));

    [HttpPut("worklist/batches/{id}")]
    public async Task<IActionResult> UpdateBatch(string id, [FromBody] BatchRequest req) => Ok(await _worklist.UpdateBatchAsync(id, req));

    [HttpDelete("worklist/batches/{id}")]
    public async Task<IActionResult> DeleteBatch(string id) { await _worklist.DeleteBatchAsync(id); return NoContent(); }

    [HttpGet("worklist/batches/{id}/print")]
    [Produces("text/html")]
    public async Task<IActionResult> PrintBatch(string id) => Content(await _worklist.BatchPrintHtmlAsync(id), "text/html; charset=utf-8");

    // ------------------------------------------------------------------ results
    /// <summary>Ручне введення результату: каскад норм → прапорець → delta → reflex → автоверифікація.</summary>
    [HttpPut("results/{orderTestId}")]
    public async Task<IActionResult> Enter(string orderTestId, [FromBody] ResultInputRequest req)
        => Ok(await _pipeline.ApplyAsync(new ResultEntry { OrderTestId = orderTestId, NumericValue = req.NumericValue, StringValue = req.StringValue, ReportText = req.ReportText, Comment = req.Comment, Unit = req.Unit, AnalyzerId = req.AnalyzerId }));

    [HttpGet("results/{orderTestId}")]
    public async Task<IActionResult> Row(string orderTestId) => Ok(await _pipeline.RowAsync(orderTestId));

    /// <summary>Верифікація (LAB_DOCTOR/LAB_ADMIN); при CRIT_* коментар обов'язковий; lockout → override.</summary>
    [HttpPost("results/{orderTestId}/verify")]
    public async Task<IActionResult> Verify(string orderTestId, [FromBody] VerifyRequest? req) => Ok(await _pipeline.VerifyAsync(orderTestId, req ?? new VerifyRequest()));

    [HttpPost("results/verify-batch")]
    public async Task<IActionResult> VerifyBatch([FromBody] VerifyBatchRequest req) => Ok(await _pipeline.VerifyBatchAsync(req));

    [HttpPost("results/{orderTestId}/reject")]
    public async Task<IActionResult> Reject(string orderTestId, [FromBody] ReasonRequest req) => Ok(await _pipeline.RejectAsync(orderTestId, req.Reason));

    [HttpPost("results/{orderTestId}/rerun")]
    public async Task<IActionResult> Rerun(string orderTestId, [FromBody] ReasonRequest? req) => Ok(await _pipeline.RerunAsync(orderTestId, req?.Reason));

    [HttpPost("results/{orderTestId}/reopen")]
    public async Task<IActionResult> Reopen(string orderTestId, [FromBody] ReasonRequest? req) => Ok(await _pipeline.ReopenAsync(orderTestId, req?.Reason));

    /// <summary>Видалення результату (історія зберігається) → тест PENDING.</summary>
    [HttpDelete("results/{orderTestId}")]
    public async Task<IActionResult> DeleteResult(string orderTestId, [FromQuery] string? reason) => Ok(await _pipeline.DeleteResultAsync(orderTestId, reason));

    [HttpPost("results/{orderTestId}/assign-analyzer")]
    public async Task<IActionResult> AssignAnalyzer(string orderTestId, [FromBody] AssignAnalyzerRequest req) => Ok(await _pipeline.AssignAnalyzerAsync(orderTestId, req.AnalyzerId));

    /// <summary>Довільна дія машини станів тесту: VERIFY | REJECT | RERUN | REOPEN | DELETE_RESULT | START_ANALYSIS.</summary>
    [HttpPost("results/{orderTestId}/transition")]
    public async Task<IActionResult> Transition(string orderTestId, [FromBody] TransitionRequest req) => Ok(await _pipeline.TransitionAsync(orderTestId, req.Status, req.Comment));

    [HttpGet("results/{orderTestId}/history")]
    public async Task<IActionResult> History(string orderTestId) => Ok(await _pipeline.HistoryAsync(orderTestId));

    // ------------------------------------------------------------------ unmatched
    [HttpGet("results/unmatched")]
    public async Task<IActionResult> Unmatched([FromQuery] bool includeLinked = false) => Ok(await _worklist.UnmatchedAsync(includeLinked));

    [HttpPost("results/unmatched/{id}/link")]
    public async Task<IActionResult> Link(string id, [FromBody] LinkRequest req) => Ok(await _worklist.LinkUnmatchedAsync(id, req.OrderTestId));

    [HttpDelete("results/unmatched/{id}")]
    public async Task<IActionResult> DeleteUnmatched(string id) { await _worklist.DeleteUnmatchedAsync(id); return NoContent(); }

    // ------------------------------------------------------------------ panic
    [HttpGet("panic-calls")]
    public async Task<IActionResult> PanicCalls([FromQuery] DateTime? from, [FromQuery] DateTime? to) => Ok(await _worklist.PanicCallsAsync(from, to));

    [HttpPost("panic-calls")]
    public async Task<IActionResult> CreatePanicCall([FromBody] PanicCallRequest req) => StatusCode(201, await _worklist.CreatePanicCallAsync(req));

    [HttpPut("panic-calls/{id}")]
    public async Task<IActionResult> UpdatePanicCall(string id, [FromBody] PanicCallRequest req) => Ok(await _worklist.UpdatePanicCallAsync(id, req));

    [HttpDelete("panic-calls/{id}")]
    public async Task<IActionResult> DeletePanicCall(string id) { await _worklist.DeletePanicCallAsync(id); return NoContent(); }

    /// <summary>Критичні результати без зареєстрованого дзвінка.</summary>
    [HttpGet("panic/pending")]
    public async Task<IActionResult> PanicPending() => Ok(await _worklist.PanicPendingAsync());
}

public sealed class LinkRequest { public string OrderTestId { get; set; } = ""; }

public sealed class QcController : LisControllerBase
{
    private readonly QcService _qc;
    public QcController(QcService qc) => _qc = qc;

    [HttpGet("qc/materials")]
    public async Task<IActionResult> Materials([FromQuery] string? analyzerId, [FromQuery] bool? isActive) => Ok(await _qc.MaterialsAsync(analyzerId, isActive));

    [HttpGet("qc/materials/{id}")]
    public async Task<IActionResult> Material(string id) => Ok(await _qc.MaterialAsync(id));

    [HttpPost("qc/materials")]
    public async Task<IActionResult> CreateMaterial([FromBody] QcMaterialRequest req) => StatusCode(201, await _qc.CreateMaterialAsync(req));

    [HttpPut("qc/materials/{id}")]
    public async Task<IActionResult> UpdateMaterial(string id, [FromBody] QcMaterialRequest req) => Ok(await _qc.UpdateMaterialAsync(id, req));

    [HttpDelete("qc/materials/{id}")]
    public async Task<IActionResult> DeleteMaterial(string id) { await _qc.DeleteMaterialAsync(id); return NoContent(); }

    [HttpDelete("qc/materials/{id}/targets/{targetId}")]
    public async Task<IActionResult> DeleteTarget(string id, string targetId) { await _qc.DeleteTargetAsync(id, targetId); return NoContent(); }

    /// <summary>Дані карти Леві-Дженнінгса з оцінкою правил і статусом (OK|WARNING|LOCKOUT).</summary>
    [HttpGet("qc/levey-jennings")]
    public async Task<IActionResult> LeveyJennings([FromQuery] string? analyzerId, [FromQuery] string testCode, [FromQuery] string? materialId, [FromQuery] int days = 30)
        => Ok(await _qc.LeveyJenningsAsync(analyzerId, testCode, materialId, days));

    [HttpGet("qc/results")]
    public async Task<IActionResult> Results([FromQuery] string? materialId, [FromQuery] string? testCode, [FromQuery] int days = 30) => Ok(await _qc.ResultsAsync(materialId, testCode, days));

    /// <summary>Внесення контрольного результату → оцінка Вестгарда → lockout при бракувальних правилах.</summary>
    [HttpPost("qc/results")]
    public async Task<IActionResult> AddResult([FromBody] QcResultRequest req) => StatusCode(201, await _qc.AddResultAsync(req));

    [HttpPut("qc/results/{id}")]
    public async Task<IActionResult> UpdateResult(string id, [FromBody] QcResultRequest req) => Ok(await _qc.UpdateResultAsync(id, req));

    [HttpDelete("qc/results/{id}")]
    public async Task<IActionResult> DeleteResult(string id) { await _qc.DeleteResultAsync(id); return NoContent(); }

    [HttpGet("qc/lockouts")]
    public async Task<IActionResult> Lockouts([FromQuery] bool? active, [FromQuery] string? analyzerId) => Ok(await _qc.LockoutsAsync(active, analyzerId));

    [HttpPost("qc/lockouts")]
    public async Task<IActionResult> CreateLockout([FromBody] LockoutCreateRequest req) => StatusCode(201, await _qc.CreateLockoutAsync(req.AnalyzerId, req.TestCode, req.Reason));

    /// <summary>Розблокування з протоколом (причина + коригувальна дія).</summary>
    [HttpPost("qc/lockouts/{id}/resolve")]
    public async Task<IActionResult> Resolve(string id, [FromBody] LockoutResolveRequest req) => Ok(await _qc.ResolveLockoutAsync(id, req));

    [HttpDelete("qc/lockouts/{id}")]
    public async Task<IActionResult> DeleteLockout(string id) { await _qc.DeleteLockoutAsync(id); return NoContent(); }

    [HttpGet("qc/report")]
    public async Task<IActionResult> Report([FromQuery] string? analyzerId, [FromQuery] DateTime? from, [FromQuery] DateTime? to) => Ok(await _qc.ReportAsync(analyzerId, from, to));
}

public sealed class LockoutCreateRequest
{
    public string AnalyzerId { get; set; } = "";
    public string? TestCode { get; set; }
    public string Reason { get; set; } = "";
}
