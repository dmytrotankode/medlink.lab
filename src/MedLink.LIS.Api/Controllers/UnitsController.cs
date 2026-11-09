// Одиниці виміру: перерахунок значення показника SI ↔ традиційні одиниці та перелік правил перерахунку показника.
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Controllers;

public sealed class UnitsController : LisControllerBase
{
    private readonly LisDbContext _db;
    public UnitsController(LisDbContext db) => _db = db;

    /// <summary>Перерахунок: ?testCode=GLU&amp;value=100&amp;from=MG_DL&amp;to=MMOL_L (одиниці — за кодом або ідентифікатором).</summary>
    [HttpGet("dictionaries/units/convert")]
    public async Task<IActionResult> Convert([FromQuery] string testCode, [FromQuery] double value, [FromQuery] string from, [FromQuery] string to)
        => Ok(await UnitCatalog.ConvertAsync(_db, testCode, value, from, to));

    /// <summary>Правила перерахунку показника (для вибору «показати в…»).</summary>
    [HttpGet("dictionaries/units/conversions")]
    public async Task<IActionResult> Conversions([FromQuery] string testCode)
    {
        var list = await _db.UnitConversions.AsNoTracking().Include(c => c.FromUnit).Include(c => c.ToUnit).Where(c => c.TestCode == testCode && c.IsActive).OrderBy(c => c.FromUnitId).ToListAsync();
        return Ok(list.Select(c => new { c.Id, c.TestCode, from = c.FromUnit!.Code, fromName = c.FromUnit.Name, to = c.ToUnit!.Code, toName = c.ToUnit.Name, c.Factor, c.Offset, c.Note }));
    }
}
