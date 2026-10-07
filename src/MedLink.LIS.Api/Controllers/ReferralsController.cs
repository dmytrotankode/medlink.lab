// FR-REF-001: е-направлення (пошук і придатність), журнал направлень за типами
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services.Ehealth;
using Microsoft.EntityFrameworkCore;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class ReferralsController : LisControllerBase
{
    private readonly ReferralService _svc;
    private readonly LisDbContext _db;
    private readonly EhealthOptions _ehealth;
    public ReferralsController(ReferralService svc, LisDbContext db, EhealthOptions ehealth) { _svc = svc; _db = db; _ehealth = ehealth; }

    /// <summary>Журнал обміну з ЕСОЗ (запити API eHealth і відповіді; режим MOCK або LIVE).</summary>
    [HttpGet("ehealth/exchange-log")]
    public async Task<IActionResult> ExchangeLog([FromQuery] int take = 200) => Ok(new
    {
        mode = _ehealth.IsMock ? "MOCK" : "LIVE", baseUrl = _ehealth.BaseUrl,
        items = await _db.EhealthExchangeLog.AsNoTracking().OrderByDescending(l => l.At).Take(Math.Clamp(take, 1, 1000))
            .Select(l => new { l.Id, l.At, l.Mode, l.Method, l.Url, l.StatusCode, l.DurationMs, l.RequestJson, l.ResponseJson }).ToListAsync()
    });

    /// <summary>Пошук е-направлення за номером: дані, статус, придатність (активне, не погашене, не прострочене, не використане), профілі ЛІС для послуги.</summary>
    [HttpGet("referrals/ehealth/lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string number, [FromQuery] string? patientId)
    {
        if (string.IsNullOrWhiteSpace(number)) throw ValidationException.Field("number", "Вкажіть номер е-направлення");
        return Ok(await _svc.LookupAsync(number, patientId));
    }

    /// <summary>Журнал направлень: замовлення з типом/номером направлення, направником, статусом ЕСОЗ; підсумок за типами.</summary>
    [HttpGet("referrals/journal")]
    public async Task<IActionResult> Journal([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? type, [FromQuery] string? search)
        => Ok(await _svc.JournalAsync(from, to, type, search));
}
