// Дублікати та згоди пацієнтів, реєстр СОП, допуски персоналу
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class QualityRegistryController : LisControllerBase
{
    private readonly QualityRegistryService _svc;
    public QualityRegistryController(QualityRegistryService svc) => _svc = svc;

    /// <summary>Пошук дублікатів при реєстрації: ІПН, ПІБ + дата народження, телефон + дата народження (FR-GAP-001). Об'єднання карток — окремо.</summary>
    [HttpGet("patients/duplicates")]
    public async Task<IActionResult> Duplicates([FromQuery] string? lastName, [FromQuery] string? firstName, [FromQuery] DateTime? birthday, [FromQuery] string? phone, [FromQuery] string? ipn)
        => Ok(await _svc.FindDuplicatesAsync(lastName, firstName, birthday, phone, ipn));

    [HttpGet("patients/{id}/duplicates")]
    public async Task<IActionResult> DuplicatesOf(string id) => Ok(await _svc.DuplicatesOfAsync(id));

    [HttpGet("patients/{id}/consents")]
    public async Task<IActionResult> Consents(string id) => Ok(await _svc.ConsentsAsync(id));

    [HttpPut("patients/{id}/consents")]
    public async Task<IActionResult> SetConsent(string id, [FromBody] ConsentRequest req) => Ok(await _svc.SetConsentAsync(id, req));

    [HttpGet("sops")]
    public async Task<IActionResult> Sops([FromQuery] string? status, [FromQuery] string? targetKind, [FromQuery] string? targetCode) => Ok(await _svc.SopsAsync(status, targetKind, targetCode));

    [HttpPost("sops")]
    public async Task<IActionResult> CreateSop([FromBody] SopRequest req) => StatusCode(201, await _svc.CreateSopAsync(req));

    [HttpPost("sops/{id}/activate")]
    public async Task<IActionResult> ActivateSop(string id) => Ok(await _svc.ActivateSopAsync(id));

    [HttpGet("competencies")]
    public async Task<IActionResult> Competencies([FromQuery] string? employeeId, [FromQuery] string? kind, [FromQuery] string? targetCode) => Ok(await _svc.CompetenciesAsync(employeeId, kind, targetCode));

    [HttpPost("competencies")]
    public async Task<IActionResult> Grant([FromBody] CompetencyRequest req) => StatusCode(201, await _svc.GrantCompetencyAsync(req));

    [HttpPost("competencies/{id}/revoke")]
    public async Task<IActionResult> Revoke(string id, [FromBody] ReasonBody? body) { await _svc.RevokeCompetencyAsync(id, body?.Reason); return NoContent(); }

    [HttpGet("competencies/check")]
    public async Task<IActionResult> Check([FromQuery] string employeeId, [FromQuery] string kind, [FromQuery] string targetCode) => Ok(await _svc.CheckCompetencyAsync(employeeId, kind, targetCode));
}
