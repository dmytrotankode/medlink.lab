// Екран «Сповіщення»: черга сповіщень пацієнтам (фільтри, ручний повтор), стан каналів, події для МІС (доставка webhook).
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class NotificationsController : LisControllerBase
{
    private readonly NotificationAdminService _svc;
    public NotificationsController(NotificationAdminService svc) => _svc = svc;

    /// <summary>Черга сповіщень пацієнтам: status (QUEUED|SENDING|SENT|FAILED|SKIPPED|NOT_CONFIGURED), channel, search (пацієнт або номер замовлення), from/to.</summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? channel, [FromQuery] string? search, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] PagingQuery paging)
        => Ok(await _svc.ListAsync(status, channel, search, from, to, paging));
    [HttpGet("notifications/summary")] public async Task<IActionResult> Summary() => Ok(await _svc.SummaryAsync());
    [HttpPost("notifications/{id}/retry")] public async Task<IActionResult> Retry(string id) => Ok(await _svc.RetryAsync(id));
    /// <summary>Обробити чергу зараз (сповіщення й події МІС). Лише адміністратор.</summary>
    [HttpPost("notifications/run")] public async Task<IActionResult> Run() => Ok(await _svc.RunNowAsync());

    [HttpGet("notifications/channels")] public async Task<IActionResult> Channels() => Ok(await _svc.ChannelsAsync());
    [HttpPut("notifications/channels/{channel}")] public async Task<IActionResult> SetChannel(string channel, [FromBody] SetChannelRequest req) => Ok(await _svc.SetChannelAsync(channel, req.Enabled));

    /// <summary>Події ЛІС → МІС: status (QUEUED|SENDING|DELIVERED|FAILED|NOT_CONFIGURED), event, search (замовлення/направлення), from/to.</summary>
    [HttpGet("mis-events")]
    public async Task<IActionResult> MisEventsList([FromQuery] string? status, [FromQuery(Name = "event")] string? eventName, [FromQuery] string? search, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] PagingQuery paging)
        => Ok(await _svc.MisListAsync(status, eventName, search, from, to, paging));
    [HttpGet("mis-events/status")] public async Task<IActionResult> MisStatus() => Ok(await _svc.MisStatusAsync());
    [HttpPost("mis-events/{id}/retry")] public async Task<IActionResult> MisRetry(string id) => Ok(await _svc.MisRetryAsync(id));
}

public sealed class SetChannelRequest { public bool Enabled { get; set; } }
