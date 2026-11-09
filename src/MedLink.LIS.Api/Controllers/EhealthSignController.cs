// ТЗ 21.13: сесія ЕСОЗ (L1/L2), підготовка пакета DiagnosticReport, підпис КЕП і постановка відправки в чергу (outbox), повтор, анулювання, перелік задач
using MedLink.LIS.Api.Services.Ehealth;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class EhealthSignController : LisControllerBase
{
    private readonly EhealthSignService _svc;
    public EhealthSignController(EhealthSignService svc) => _svc = svc;

    [HttpGet("ehealth/session")] public async Task<IActionResult> Session() => Ok(await _svc.StatusAsync());
    /// <summary>Вхід в ЄСОЗ, крок 1: посилання на сторінку авторизації (через проксі MedLink). Паролі ЛІС не бачить.</summary>
    [HttpPost("ehealth/session/link")] public async Task<IActionResult> AuthLink() => Ok(await _svc.AuthLinkAsync());
    /// <summary>Вхід в ЄСОЗ, крок 2: обмін коду авторизації на токен у проксі.</summary>
    [HttpPost("ehealth/session/callback")] public async Task<IActionResult> Callback([FromBody] EhealthAuthCallbackRequest req) => Ok(await _svc.AuthCallbackAsync(req));
    [HttpPost("ehealth/session/refresh")] public async Task<IActionResult> Refresh() => Ok(await _svc.RefreshSessionAsync());
    [HttpDelete("ehealth/session")] public async Task<IActionResult> Logout() { await _svc.LogoutAsync(); return NoContent(); }

    [HttpPost("orders/{id}/ehealth/prepare")] public async Task<IActionResult> Prepare(string id) => Ok(await _svc.PrepareAsync(id));
    [HttpPost("orders/{id}/ehealth/submit")] public async Task<IActionResult> Submit(string id, [FromBody] EhealthSubmitRequest req) => Ok(await _svc.SubmitAsync(id, req));
    [HttpPost("orders/{id}/ehealth/retry")] public async Task<IActionResult> Retry(string id) => Ok(await _svc.RetryAsync(id));
    [HttpGet("orders/{id}/ehealth/status")] public async Task<IActionResult> Status(string id) => Ok(await _svc.OrderStatusAsync(id));
    [HttpPost("orders/{id}/ehealth/annul")] public async Task<IActionResult> Annul(string id, [FromBody] EhealthSubmitRequest req) => Ok(await _svc.AnnulAsync(id, req));
    [HttpPost("orders/{id}/ehealth/annul/prepare")] public async Task<IActionResult> AnnulPrepare(string id, [FromBody] EhealthSubmitRequest req) => Ok(await _svc.AnnulPrepareAsync(id, req));
    /// <summary>Тестовий підпис (лише Mock/Replay): структурно коректний CAdES-X Long із вкладеним пакетом; не є криптографічним підписом.</summary>
    [HttpPost("orders/{id}/ehealth/mock-sign")] public async Task<IActionResult> MockSign(string id, [FromBody] EhealthMockSignRequest req) => Ok(await _svc.MockSignAsync(id, req));

    /// <summary>Вихідні задачі відправки (outbox): фільтри status (NEW|SENDING|WAITING_JOB|DONE|FAILED|DEAD|NOT_SENT), from/to, search (замовлення, направлення, пацієнт), kind.</summary>
    [HttpGet("ehealth/submissions")]
    public async Task<IActionResult> Submissions([FromQuery] string? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? search, [FromQuery] string? kind)
        => Ok(await _svc.SubmissionsAsync(status, from, to, search, kind));
    [HttpPost("ehealth/outbox/run")] public async Task<IActionResult> RunOutbox() => Ok(await _svc.RunNowAsync());
    [HttpPost("ehealth/outbox/{id}/retry")] public async Task<IActionResult> RetryTask(string id) => Ok(await _svc.RetryTaskAsync(id));
    [HttpPost("ehealth/outbox/{id}/cancel")] public async Task<IActionResult> CancelTask(string id) => Ok(await _svc.CancelTaskAsync(id));
}
