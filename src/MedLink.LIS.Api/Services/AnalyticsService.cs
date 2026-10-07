// Аналітика: TAT (медіана/P90 за етапами, профілями, CITO, SLA), обсяги, експорт XLSX
using ClosedXML.Excel;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Core.Clinical;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class AnalyticsService
{
    private readonly LisDbContext _db;
    public AnalyticsService(LisDbContext db) => _db = db;

    private sealed record OrderTat(string OrderId, string OrderNumber, bool Cito, string? ProfileId, string? ProfileName, int SlaHours, TatBreakdown Tat);

    private async Task<List<OrderTat>> CollectAsync(DateTime from, DateTime to, string? profileId, bool? cito)
    {
        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Samples).Include(o => o.Tests).ThenInclude(t => t.Result).Include(o => o.Tests).ThenInclude(t => t.Profile)
            .Where(o => o.OrderDatetime >= from && o.OrderDatetime <= to && o.Status != OrderStatuses.Cancelled && (cito == null || o.IsUrgentCito == cito))
            .ToListAsync();
        var list = new List<OrderTat>();
        foreach (var o in orders)
        {
            var profiles = o.Tests.Where(t => t.Profile != null).Select(t => t.Profile!).DistinctBy(p => p.Id).ToList();
            if (profileId != null && profiles.All(p => p.Id != profileId && p.Code != profileId)) continue;
            var main = profiles.OrderBy(p => p.TurnaroundHours).FirstOrDefault();
            var results = o.Tests.Where(t => t.Result != null).Select(t => t.Result!).ToList();
            var tat = TatCalculator.Compute(new TatTimestamps
            {
                OrderedAt = o.OrderDatetime,
                CollectedAt = o.Samples.Where(s => s.CollectedAt != null).Select(s => s.CollectedAt).Min(),
                ReceivedAt = o.Samples.Where(s => s.ReceivedAt != null).Select(s => s.ReceivedAt).Min(),
                ResultedAt = results.Count == 0 ? null : results.Max(r => r.EnteredAt),
                VerifiedAt = results.Any(r => r.VerifiedAt != null) ? results.Where(r => r.VerifiedAt != null).Max(r => r.VerifiedAt) : null,
                ReleasedAt = o.ReleasedAt
            });
            list.Add(new OrderTat(o.Id, o.OrderNumber, o.IsUrgentCito, main?.Id, main?.Name, o.IsUrgentCito ? Math.Min(main?.TurnaroundHours ?? 2, 2) : main?.TurnaroundHours ?? 24, tat));
        }
        return list;
    }

    public async Task<object> TatAsync(DateTime? from, DateTime? to, string? profileId, bool? cito)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30); var t = to ?? DateTime.UtcNow;
        var data = await CollectAsync(f, t, profileId, cito);
        var totals = data.Where(d => d.Tat.TotalMinutes.HasValue).Select(d => d.Tat.TotalMinutes!.Value).ToList();
        var stages = TatCalculator.StageNames.Select(name =>
        {
            var vals = data.Select(d => d.Tat.Stages.First(s => s.Name == name).Minutes).Where(m => m.HasValue).Select(m => m!.Value).ToList();
            return new { name, medianMin = Round(TatCalculator.Median(vals)), p90Min = Round(TatCalculator.P90(vals)), n = vals.Count };
        }).ToList();
        var byProfile = data.Where(d => d.ProfileName != null).GroupBy(d => new { d.ProfileId, d.ProfileName, d.SlaHours }).Select(g =>
        {
            var vals = g.Where(d => d.Tat.TotalMinutes.HasValue).Select(d => d.Tat.TotalMinutes!.Value).ToList();
            return new { profileId = g.Key.ProfileId, profileName = g.Key.ProfileName, slaHours = g.Key.SlaHours, n = g.Count(), completed = vals.Count, medianMin = Round(TatCalculator.Median(vals)), p90Min = Round(TatCalculator.P90(vals)), slaViolations = vals.Count(v => v > g.Key.SlaHours * 60) };
        }).OrderBy(x => x.profileName).ToList();
        var violations = data.Where(d => d.Tat.TotalMinutes.HasValue && d.Tat.TotalMinutes > d.SlaHours * 60)
            .Select(d => new { d.OrderId, d.OrderNumber, d.Cito, d.ProfileName, slaHours = d.SlaHours, totalMin = d.Tat.TotalMinutes, overMin = Math.Round(d.Tat.TotalMinutes!.Value - d.SlaHours * 60) }).ToList();
        var citoVals = data.Where(d => d.Cito && d.Tat.TotalMinutes.HasValue).Select(d => d.Tat.TotalMinutes!.Value).ToList();
        var routineVals = data.Where(d => !d.Cito && d.Tat.TotalMinutes.HasValue).Select(d => d.Tat.TotalMinutes!.Value).ToList();
        return new
        {
            from = f, to = t, orders = data.Count, completed = totals.Count, medianMin = Round(TatCalculator.Median(totals)), p90Min = Round(TatCalculator.P90(totals)),
            cito = new { n = citoVals.Count, medianMin = Round(TatCalculator.Median(citoVals)), p90Min = Round(TatCalculator.P90(citoVals)) },
            routine = new { n = routineVals.Count, medianMin = Round(TatCalculator.Median(routineVals)), p90Min = Round(TatCalculator.P90(routineVals)) },
            stages, byProfile, slaViolations = violations.Count, slaViolationOrders = violations,
            waterfall = TatCalculator.StageNames.Select(name => new { stage = name, medianMin = stages.First(s => s.name == name).medianMin })
        };
    }

    private static double? Round(double? v) => v.HasValue ? Math.Round(v.Value, 1) : null;

    public async Task<object> VolumeAsync(DateTime? from, DateTime? to, string groupBy)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30); var t = to ?? DateTime.UtcNow;
        var tests = await _db.OrderTests.AsNoTracking().Include(x => x.Order).Include(x => x.Profile).Include(x => x.Result).ThenInclude(r => r!.Analyzer)
            .Where(x => x.Order!.OrderDatetime >= f && x.Order.OrderDatetime <= t && x.Order.Status != OrderStatuses.Cancelled).ToListAsync();
        var employees = await _db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.FullName);
        IEnumerable<IGrouping<string, LabOrderTest>> groups = groupBy.ToLowerInvariant() switch
        {
            "analyzer" => tests.GroupBy(x => x.Result?.Analyzer?.Name ?? "Ручне введення"),
            "employee" => tests.GroupBy(x => x.Result?.VerifiedById != null && employees.TryGetValue(x.Result.VerifiedById, out var n) ? n : x.Result?.IsAutoVerified == true ? "Автоверифікація" : "—"),
            "day" => tests.GroupBy(x => x.Order!.OrderDatetime.ToString("yyyy-MM-dd")),
            "test" => tests.GroupBy(x => x.TestCode),
            "department" => tests.GroupBy(x => x.Order!.DepartmentId ?? "—"),
            _ => tests.GroupBy(x => x.Profile?.Name ?? "Окремі тести")
        };
        var rows = groups.Select(g => new
        {
            key = g.Key, tests = g.Count(), orders = g.Select(x => x.OrderId).Distinct().Count(), resulted = g.Count(x => x.Result != null),
            verified = g.Count(x => OrderTestStatuses.VerifiedAny.Contains(x.Status)), autoVerified = g.Count(x => x.Result?.IsAutoVerified == true),
            critical = g.Count(x => x.Result != null && ResultFlags.IsCritical(x.Result.Flag)), abnormal = g.Count(x => x.Result != null && ResultFlags.IsAbnormal(x.Result.Flag)),
            cito = g.Count(x => x.Order!.IsUrgentCito), revenue = g.Where(x => x.ProfileId == null).Sum(x => (decimal?)0) ?? 0
        }).OrderByDescending(r => r.tests).ToList();
        var revenue = await _db.Orders.AsNoTracking().Where(o => o.OrderDatetime >= f && o.OrderDatetime <= t && o.Status != OrderStatuses.Cancelled).SumAsync(o => o.TotalPrice);
        return new { from = f, to = t, groupBy, totalTests = tests.Count, totalOrders = tests.Select(x => x.OrderId).Distinct().Count(), totalRevenue = revenue, rows };
    }

    public async Task<byte[]> ExportXlsxAsync(string report, DateTime? from, DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30); var t = to ?? DateTime.UtcNow;
        using var wb = new XLWorkbook();
        switch (report.ToLowerInvariant())
        {
            case "tat":
            {
                var data = await CollectAsync(f, t, null, null);
                var ws = wb.AddWorksheet("TAT");
                var headers = new[] { "Замовлення", "CITO", "Профіль", "SLA, год" }.Concat(TatCalculator.StageNames).Concat(new[] { "Всього, хв", "Порушення SLA" }).ToArray();
                for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                var row = 2;
                foreach (var d in data)
                {
                    var c = 1;
                    ws.Cell(row, c++).Value = d.OrderNumber; ws.Cell(row, c++).Value = d.Cito ? "Так" : ""; ws.Cell(row, c++).Value = d.ProfileName ?? ""; ws.Cell(row, c++).Value = d.SlaHours;
                    foreach (var s in d.Tat.Stages) { if (s.Minutes.HasValue) ws.Cell(row, c).Value = s.Minutes.Value; c++; }
                    if (d.Tat.TotalMinutes.HasValue) ws.Cell(row, c).Value = d.Tat.TotalMinutes.Value; c++;
                    ws.Cell(row, c).Value = d.Tat.TotalMinutes > d.SlaHours * 60 ? "Так" : "";
                    row++;
                }
                break;
            }
            case "volume":
            {
                var ws = wb.AddWorksheet("Обсяги");
                var tests = await _db.OrderTests.AsNoTracking().Include(x => x.Order).Include(x => x.Profile).Include(x => x.Result).ThenInclude(r => r!.Analyzer)
                    .Where(x => x.Order!.OrderDatetime >= f && x.Order.OrderDatetime <= t && x.Order.Status != OrderStatuses.Cancelled).ToListAsync();
                var headers = new[] { "Профіль", "Тест", "Кількість", "З результатом", "Верифіковано", "Автоверифіковано", "Критичних", "CITO" };
                for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                var row = 2;
                foreach (var g in tests.GroupBy(x => new { profile = x.Profile?.Name ?? "Окремі тести", x.TestCode }).OrderBy(g => g.Key.profile).ThenBy(g => g.Key.TestCode))
                {
                    ws.Cell(row, 1).Value = g.Key.profile; ws.Cell(row, 2).Value = g.Key.TestCode; ws.Cell(row, 3).Value = g.Count(); ws.Cell(row, 4).Value = g.Count(x => x.Result != null);
                    ws.Cell(row, 5).Value = g.Count(x => OrderTestStatuses.VerifiedAny.Contains(x.Status)); ws.Cell(row, 6).Value = g.Count(x => x.Result?.IsAutoVerified == true);
                    ws.Cell(row, 7).Value = g.Count(x => x.Result != null && ResultFlags.IsCritical(x.Result.Flag)); ws.Cell(row, 8).Value = g.Count(x => x.Order!.IsUrgentCito);
                    row++;
                }
                break;
            }
            case "qc":
            {
                var ws = wb.AddWorksheet("ВКЯ");
                var results = await _db.QcResults.AsNoTracking().Include(r => r.Material).ThenInclude(m => m!.Analyzer).Where(r => r.RunAt >= f && r.RunAt <= t).OrderBy(r => r.RunAt).ToListAsync();
                var headers = new[] { "Дата", "Аналізатор", "Матеріал", "Лот", "Тест", "Значення", "Ціль", "SD", "z", "Правила", "Lockout" };
                for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                var row = 2;
                foreach (var r in results)
                {
                    ws.Cell(row, 1).Value = r.RunAt; ws.Cell(row, 2).Value = r.Material?.Analyzer?.Name ?? ""; ws.Cell(row, 3).Value = r.Material?.Name ?? ""; ws.Cell(row, 4).Value = r.Material?.LotNumber ?? "";
                    ws.Cell(row, 5).Value = r.TestCode; ws.Cell(row, 6).Value = r.MeasuredValue; ws.Cell(row, 7).Value = r.TargetMean; ws.Cell(row, 8).Value = r.TargetSd; ws.Cell(row, 9).Value = r.ZScore;
                    ws.Cell(row, 10).Value = string.Join(",", r.ViolatedRules); ws.Cell(row, 11).Value = r.LockoutEnforced ? "Так" : "";
                    row++;
                }
                break;
            }
            default:
            {
                var ws = wb.AddWorksheet("Замовлення");
                var orders = await _db.Orders.AsNoTracking().Include(o => o.Patient).Include(o => o.Department).Include(o => o.Tests)
                    .Where(o => o.OrderDatetime >= f && o.OrderDatetime <= t).OrderBy(o => o.OrderDatetime).ToListAsync();
                var headers = new[] { "Номер", "Дата", "Пацієнт", "Стать", "Дата народження", "Підрозділ", "Статус", "CITO", "Тестів", "Сума" };
                for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                var row = 2;
                foreach (var o in orders)
                {
                    ws.Cell(row, 1).Value = o.OrderNumber; ws.Cell(row, 2).Value = o.OrderDatetime; ws.Cell(row, 3).Value = o.Patient?.FullName ?? ""; ws.Cell(row, 4).Value = o.Patient?.Gender ?? "";
                    if (o.Patient?.BirthDate != null) ws.Cell(row, 5).Value = o.Patient.BirthDate.Value;
                    ws.Cell(row, 6).Value = o.Department?.Name ?? ""; ws.Cell(row, 7).Value = o.Status; ws.Cell(row, 8).Value = o.IsUrgentCito ? "Так" : ""; ws.Cell(row, 9).Value = o.Tests.Count; ws.Cell(row, 10).Value = o.TotalPrice;
                    row++;
                }
                break;
            }
        }
        foreach (var ws in wb.Worksheets) { ws.Row(1).Style.Font.Bold = true; ws.SheetView.FreezeRows(1); }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<PagedResult<LabAuditLog>> AuditAsync(string? entity, string? entityId, string? userId, string? action, DateTime? from, DateTime? to, PagingQuery paging)
    {
        var q = _db.AuditLog.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entity)) q = q.Where(a => a.Entity == entity);
        if (!string.IsNullOrWhiteSpace(entityId)) q = q.Where(a => a.EntityId == entityId);
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (from.HasValue) q = q.Where(a => a.At >= from.Value);
        if (to.HasValue) q = q.Where(a => a.At <= to.Value);
        return await q.OrderByDescending(a => a.At).ToPagedAsync(paging);
    }
}
