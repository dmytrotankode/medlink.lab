// ТЗ 23.5, 23.6: робота за направленнями (розрізи «аналізатор / пацієнт / дослідження») і звіти лабораторії (JSON, XLSX, PDF)
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class WorkReportsController : LisControllerBase
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private readonly WorkReportsService _svc;
    public WorkReportsController(WorkReportsService svc) => _svc = svc;

    [HttpGet("work/summary")] public async Task<IActionResult> Summary([FromQuery] WorkFilter f) => Ok(await _svc.SummaryAsync(f));
    /// <summary>Черга приладу з фільтрами (FR-WRK-002); analyzerId=none — «Без обладнання».</summary>
    [HttpGet("work/by-analyzer/{analyzerId}")] public async Task<IActionResult> ByAnalyzer(string analyzerId, [FromQuery] WorkFilter f) => Ok(await _svc.ByAnalyzerAsync(analyzerId, f));
    [HttpGet("work/by-patient")] public async Task<IActionResult> Patients([FromQuery] WorkFilter f) => Ok(await _svc.PatientsAsync(f));
    [HttpGet("work/by-patient/{patientId}")] public async Task<IActionResult> ByPatient(string patientId, [FromQuery] WorkFilter f) => Ok(await _svc.ByPatientAsync(patientId, f));
    [HttpGet("work/by-service")] public async Task<IActionResult> Services([FromQuery] WorkFilter f) => Ok(await _svc.ServicesAsync(f));
    [HttpGet("work/by-service/{serviceCode}")] public async Task<IActionResult> ByService(string serviceCode, [FromQuery] WorkFilter f) => Ok(await _svc.ByServiceAsync(serviceCode, f));
    /// <summary>Експорт поточного розрізу в XLSX (FR-WRK-008): view=analyzer|patient|service, key — прилад/пацієнт/код послуги.</summary>
    [HttpGet("work/export")]
    public async Task<IActionResult> Export([FromQuery] string view, [FromQuery] string? key, [FromQuery] WorkFilter f) => File(await _svc.ExportAsync(view, key, f), XlsxType, $"work-{view}.xlsx");

    [HttpGet("reports")] public IActionResult Reports() => Ok(_svc.List());
    /// <summary>Кумулятивний звіт пацієнта (FR-RPT-001): «показник × дата», прилад під датою, ↑ ↓ (↑↑ ↓↓ — критичні), кома як десятковий роздільник.</summary>
    [HttpGet("reports/cumulative/{patientId}")]
    public async Task<IActionResult> Cumulative(string patientId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? group, [FromQuery] bool abnormalOnly = false)
        => Ok(await _svc.CumulativeAsync(patientId, from, to, group, abnormalOnly));
    [HttpPost("reports/stat-daily/rebuild")] public async Task<IActionResult> Rebuild([FromQuery] DateTime from, [FromQuery] DateTime to) => Ok(await _svc.RebuildStatDailyAsync(from, to));
    /// <summary>Звіт за типом (load, sources, indicators, tat, distribution, unperformed, packages); format=json|xlsx|pdf.</summary>
    [HttpGet("reports/{type}")]
    public async Task<IActionResult> Report(string type, [FromQuery] WorkFilter f, [FromQuery] string format = "json")
    {
        var table = await _svc.BuildAsync(type, f, format);
        return format.ToLowerInvariant() switch
        {
            "xlsx" => File(WorkReportsService.Xlsx(table), XlsxType, $"report-{type}.xlsx"),
            "pdf" => File(WorkReportsService.Pdf(table), "application/pdf", $"report-{type}.pdf"),
            _ => Ok(table)
        };
    }
}
