// =============================================================================
// Автосписання реагентів під час отримання результату (FR-REA-001/002/004, FR-GAP-030):
// вибір лоту для пари (прилад, показник), списання за нормою витрати, зв'язок «результат ↔ лот»,
// автоперемикання лота при вичерпанні. Нестача реагенту НІКОЛИ не блокує результат — лише попередження.
// =============================================================================
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class ReagentConsumptionService
{
    private readonly LisDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;

    public ReagentConsumptionService(LisDbContext db, IAuditService audit, ICurrentEmployee current) { _db = db; _audit = audit; _current = current; }

    /// <summary>
    /// Списує реагент за одне вимірювання. Лоти з кодом показника списуються за кожен результат;
    /// спільні лоти приладу (без коду показника, напр. розчини для гематологічного аналізатора) — раз на пробірку.
    /// Зміни лише додаються до ChangeTracker (зберігає викликач). Повертає попередження для користувача.
    /// </summary>
    public async Task<List<string>> ConsumeForResultAsync(LabOrderTest test, LabTestResult result, string analyzerId, bool isRerun)
    {
        var warnings = new List<string>();
        var lots = await _db.ReagentLots.Where(l => l.RecordState != RecordStates.Deleted && l.AnalyzerId == analyzerId && (l.TestCode == test.TestCode || l.TestCode == null)).ToListAsync();
        if (lots.Count == 0) return warnings;
        var now = DateTime.UtcNow;

        var specific = lots.Where(l => l.TestCode != null).ToList();
        if (specific.Count > 0) Take(specific, test, result, analyzerId, isRerun ? "RERUN" : "AUTO", now, warnings, linkResult: true);

        var general = lots.Where(l => l.TestCode == null).ToList();
        if (general.Count > 0 && !isRerun && test.SampleId != null)
        {
            var generalIds = general.Select(l => l.Id).ToList();
            var already = await _db.ReagentConsumptions.AnyAsync(c => c.SampleId == test.SampleId && c.AnalyzerId == analyzerId && generalIds.Contains(c.LotId))
                          || _db.ChangeTracker.Entries<LabReagentConsumption>().Any(e => e.Entity.SampleId == test.SampleId && e.Entity.AnalyzerId == analyzerId && generalIds.Contains(e.Entity.LotId));
            if (!already) Take(general, test, result, analyzerId, "AUTO", now, warnings, linkResult: specific.Count == 0);
        }
        return warnings;
    }

    private void Take(List<LabReagentLot> group, LabOrderTest test, LabTestResult result, string analyzerId, string kind, DateTime now, List<string> warnings, bool linkResult)
    {
        // Кандидат: придатний (термін, залишок) → активний першим → найближчий термін (FEFO)
        LabReagentLot? lot = group.Where(l => l.ExpiryDate >= now && l.TestsRemaining > 0).OrderByDescending(l => l.IsActive).ThenBy(l => l.ExpiryDate).FirstOrDefault();
        var depleted = lot == null;
        lot ??= group.OrderByDescending(l => l.IsActive).ThenByDescending(l => l.ExpiryDate).First();

        var units = Math.Max(1, lot.UnitsPerTest);
        var take = Math.Min(units, Math.Max(0, lot.TestsRemaining));
        var shortfall = units - take;
        lot.TestsRemaining -= take;
        lot.OpenedAt ??= now;

        var own = new List<string>();
        if (!lot.IsActive && !depleted)
        {
            // автоперемикання (FR-REA-001): у парі «прилад — показник» активний один лот
            foreach (var other in group.Where(l => l.IsActive && l.Id != lot.Id)) other.IsActive = false;
            lot.IsActive = true;
            own.Add($"Активовано лот {lot.LotNumber} реагенту «{lot.ReagentName}»");
        }
        if (lot.ExpiryDate < now) own.Add($"У лота {lot.LotNumber} реагенту «{lot.ReagentName}» минув термін придатності ({lot.ExpiryDate:dd.MM.yyyy})");
        if (shortfall > 0) own.Add($"Реагенту «{lot.ReagentName}» не вистачає (лот {lot.LotNumber}, нестача {shortfall}) — результат збережено");
        else if (lot.TestsRemaining <= 0)
        {
            var next = group.Where(l => l.Id != lot.Id && l.ExpiryDate >= now && l.TestsRemaining > 0).OrderBy(l => l.ExpiryDate).FirstOrDefault();
            if (next != null)
            {
                lot.IsActive = false; next.IsActive = true;
                own.Add($"Лот {lot.LotNumber} реагенту «{lot.ReagentName}» вичерпано, активовано лот {next.LotNumber}");
            }
            else own.Add($"Реагент «{lot.ReagentName}» (лот {lot.LotNumber}) вичерпано, резервного лота немає");
        }
        else if (lot.TestsRemaining <= lot.MinimumTests) own.Add($"Залишок реагенту «{lot.ReagentName}» (лот {lot.LotNumber}) — {lot.TestsRemaining}, мінімум {lot.MinimumTests}");
        warnings.AddRange(own);

        _db.ReagentConsumptions.Add(new LabReagentConsumption
        {
            LotId = lot.Id, OrderTestId = test.Id, ResultId = result.Id, SampleId = test.SampleId, AnalyzerId = analyzerId, TestCode = test.TestCode,
            Units = take, Shortfall = shortfall, Kind = kind, ActorId = _current.IsResolved ? _current.EmployeeId : null, At = now, Warning = own.Count == 0 ? null : string.Join("; ", own)
        });
        if (linkResult) { result.ReagentLotId = lot.Id; result.ReagentLotNumber = lot.LotNumber; }
        _audit.Log("CONSUME", "lab_reagent_lot", lot.Id, null, new { tests = take, shortfall, lot.TestsRemaining, auto = true, orderTestId = test.Id, test.TestCode, lot.LotNumber });
    }
}
