// Робочий лист: ефективна вибірка з join/projection, лічильники, батчі, панічні дзвінки, незв'язані результати
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Core.Clinical;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class WorklistFilter
{
    public string? Status { get; set; }
    public string? AnalyzerId { get; set; }
    public string? Flag { get; set; }
    public bool? Cito { get; set; }
    public string? Search { get; set; }
    public string? TestCode { get; set; }
    public string? OrderId { get; set; }
    public string? LabSectionId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public sealed class PanicCallRequest
{
    public string ResultId { get; set; } = "";
    public string DoctorName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Department { get; set; }
    public bool ReadbackConfirmed { get; set; }
    public string? Comments { get; set; }
}

public sealed class BatchRequest
{
    public string? AnalyzerId { get; set; }
    public List<string> OrderTestIds { get; set; } = new();
    public string? Status { get; set; }
}

public sealed class WorklistService
{
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;
    private readonly INumeratorService _numerators;
    private readonly ResultPipelineService _pipeline;

    public WorklistService(LisDbContext db, IRolePolicy policy, IAuditService audit, ICurrentEmployee current, INumeratorService numerators, ResultPipelineService pipeline)
    {
        _db = db; _policy = policy; _audit = audit; _current = current; _numerators = numerators; _pipeline = pipeline;
    }

    public async Task<PagedResult<WorklistRowDto>> QueryAsync(WorklistFilter f, PagingQuery paging)
    {
        var q = _db.OrderTests.AsNoTracking()
            .Include(t => t.Test).ThenInclude(d => d!.LabSection).Include(t => t.Sample).Include(t => t.AssignedAnalyzer)
            .Include(t => t.Order).ThenInclude(o => o!.Patient)
            .Include(t => t.Result).ThenInclude(r => r!.Analyzer)
            .Where(t => !t.IsDeleted && t.Order!.Status != OrderStatuses.Cancelled);

        if (!string.IsNullOrWhiteSpace(f.Status))
        {
            var statuses = f.Status.ToUpperInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            q = q.Where(t => statuses.Contains(t.Status));
        }
        if (!string.IsNullOrWhiteSpace(f.AnalyzerId)) q = q.Where(t => t.AssignedAnalyzerId == f.AnalyzerId || (t.Result != null && t.Result.AnalyzerId == f.AnalyzerId));
        if (!string.IsNullOrWhiteSpace(f.Flag))
        {
            var flags = f.Flag.ToUpperInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (flags.Contains("CRITICAL")) flags = flags.Concat(new[] { ResultFlags.CritHigh, ResultFlags.CritLow }).ToArray();
            q = q.Where(t => t.Result != null && flags.Contains(t.Result.Flag));
        }
        if (f.Cito.HasValue) q = q.Where(t => t.Order!.IsUrgentCito == f.Cito.Value);
        if (!string.IsNullOrWhiteSpace(f.TestCode)) q = q.Where(t => t.TestCode == f.TestCode);
        if (!string.IsNullOrWhiteSpace(f.OrderId)) q = q.Where(t => t.OrderId == f.OrderId);
        if (!string.IsNullOrWhiteSpace(f.LabSectionId)) q = q.Where(t => t.Test!.LabSectionId == f.LabSectionId);
        if (f.From.HasValue) q = q.Where(t => t.Order!.OrderDatetime >= f.From.Value);
        if (f.To.HasValue) q = q.Where(t => t.Order!.OrderDatetime <= f.To.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(t => t.Order!.OrderNumber.Contains(s) || t.Order.Patient!.LastName.Contains(s) || t.Order.Patient.FirstName.Contains(s)
                             || (t.Sample != null && t.Sample.Barcode.Contains(s)) || t.TestCode.Contains(s) || t.TestName.Contains(s));
        }

        q = paging.Sort?.ToLowerInvariant() switch
        {
            "enteredat" => paging.Desc ? q.OrderByDescending(t => t.Result!.EnteredAt) : q.OrderBy(t => t.Result!.EnteredAt),
            "testcode" => paging.Desc ? q.OrderByDescending(t => t.TestCode) : q.OrderBy(t => t.TestCode),
            "patient" => paging.Desc ? q.OrderByDescending(t => t.Order!.Patient!.LastName) : q.OrderBy(t => t.Order!.Patient!.LastName),
            _ => q.OrderByDescending(t => t.Order!.IsUrgentCito).ThenByDescending(t => t.Order!.OrderDatetime).ThenBy(t => t.DisplayOrder)
        };

        var total = await q.CountAsync();
        var rows = await q.Skip((paging.SafePage - 1) * paging.SafePageSize).Take(paging.SafePageSize).ToListAsync();
        var lockouts = await _db.Lockouts.AsNoTracking().Where(l => l.ResolvedAt == null).Select(l => new { l.AnalyzerId, l.TestCode }).ToListAsync();
        bool Locked(string? analyzerId, string testCode) => analyzerId != null && lockouts.Any(l => l.AnalyzerId == analyzerId && (l.TestCode == null || l.TestCode == testCode));
        var orderIds = rows.Select(r => r.OrderId).Distinct().ToList();
        var journal = await _db.JournalEntries.AsNoTracking().Where(j => orderIds.Contains(j.OrderId)).Select(j => new { j.OrderId, j.SampleId, j.LabSectionId, j.JournalNumber, j.OrderTestIdsJson }).ToListAsync();
        string? JournalNo(LabOrderTest t) => journal.FirstOrDefault(j => j.OrderId == t.OrderId && j.OrderTestIdsJson.Contains(t.Id))?.JournalNumber
                                             ?? journal.FirstOrDefault(j => j.OrderId == t.OrderId && j.LabSectionId == t.Test!.LabSectionId && (j.SampleId == null || j.SampleId == t.SampleId))?.JournalNumber;

        return new PagedResult<WorklistRowDto>
        {
            Total = total, Page = paging.SafePage, PageSize = paging.SafePageSize,
            Items = rows.Select(t => DtoMapper.ToWorklistRow(t, Locked(t.Result?.AnalyzerId ?? t.AssignedAnalyzerId, t.TestCode), _policy, JournalNo(t))).ToList()
        };
    }

    public async Task<object> SummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var active = _db.OrderTests.AsNoTracking().Where(t => t.Order!.Status != OrderStatuses.Cancelled && t.Order.Status != OrderStatuses.Released);
        var pending = await active.CountAsync(t => t.Status == OrderTestStatuses.Pending || t.Status == OrderTestStatuses.InAnalysis || t.Status == OrderTestStatuses.Rerun);
        var resulted = await active.CountAsync(t => t.Status == OrderTestStatuses.Resulted);
        var needsReview = await active.CountAsync(t => t.Status == OrderTestStatuses.NeedsReview);
        var panic = await _db.Results.AsNoTracking().CountAsync(r => (r.Flag == ResultFlags.CritHigh || r.Flag == ResultFlags.CritLow)
            && r.OrderTest!.Status != OrderTestStatuses.Rejected && !_db.PanicCalls.Any(p => p.ResultId == r.Id));
        var cito = await _db.Orders.AsNoTracking().CountAsync(o => o.IsUrgentCito && (o.Status != OrderStatuses.Released && o.Status != OrderStatuses.Cancelled && o.Status != OrderStatuses.Rejected));
        var autoVerifiedToday = await _db.Results.AsNoTracking().CountAsync(r => r.IsAutoVerified && r.VerifiedAt >= today);
        var verifiedToday = await _db.Results.AsNoTracking().CountAsync(r => r.VerifiedAt >= today);
        var ordersToday = await _db.Orders.AsNoTracking().CountAsync(o => o.OrderDatetime >= today);
        var lockouts = await _db.Lockouts.AsNoTracking().CountAsync(l => l.ResolvedAt == null);
        var unmatched = await _db.UnmatchedResults.AsNoTracking().CountAsync(u => !u.IsLinked);
        return new { pending, resulted, needsReview, panic, cito, autoVerifiedToday, verifiedToday, ordersToday, activeLockouts = lockouts, unmatched };
    }

    // ------------------------------------------------------------------ panic calls
    public async Task<List<LabPanicCall>> PanicCallsAsync(DateTime? from, DateTime? to)
    {
        var q = _db.PanicCalls.AsNoTracking().Where(p => !p.IsDeleted);
        if (from.HasValue) q = q.Where(p => p.NotifiedAt >= from.Value);
        if (to.HasValue) q = q.Where(p => p.NotifiedAt <= to.Value);
        return await q.OrderByDescending(p => p.NotifiedAt).ToListAsync();
    }

    public async Task<LabPanicCall> CreatePanicCallAsync(PanicCallRequest req)
    {
        _policy.Require("Реєстрація панічного дзвінка", LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician);
        if (string.IsNullOrWhiteSpace(req.DoctorName)) throw ValidationException.Field("doctorName", "Вкажіть ПІБ лікаря, який прийняв повідомлення");
        if (string.IsNullOrWhiteSpace(req.Phone)) throw ValidationException.Field("phone", "Вкажіть телефон");
        var result = await _db.Results.Include(r => r.OrderTest).ThenInclude(t => t!.Order).ThenInclude(o => o!.Patient).FirstOrDefaultAsync(r => r.Id == req.ResultId)
                     ?? throw NotFoundException.For("Результат", req.ResultId);
        var call = new LabPanicCall
        {
            ResultId = result.Id, OrderId = result.OrderTest!.OrderId, PatientName = result.OrderTest.Order?.Patient?.FullName ?? "",
            TestCode = result.OrderTest.TestCode, Value = DtoMapper.FormatValue(result.NumericValue, result.StringValue, 2) + " " + result.Unit,
            DoctorNotifiedName = req.DoctorName, Phone = req.Phone, Department = req.Department, ReadbackConfirmed = req.ReadbackConfirmed,
            NotifiedById = _current.EmployeeId, NotifiedAt = DateTime.UtcNow, Comments = req.Comments
        };
        _db.PanicCalls.Add(call);
        _audit.Log("CREATE", "lab_panic_call", call.Id, null, call);
        await _db.SaveChangesAsync();
        return call;
    }

    public async Task<LabPanicCall> UpdatePanicCallAsync(string id, PanicCallRequest req)
    {
        _policy.Require("Редагування панічного дзвінка", LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician);
        var call = await _db.PanicCalls.FirstOrDefaultAsync(p => p.Id == id) ?? throw NotFoundException.For("Панічний дзвінок", id);
        var before = new { call.DoctorNotifiedName, call.Phone, call.Department, call.ReadbackConfirmed, call.Comments };
        if (!string.IsNullOrWhiteSpace(req.DoctorName)) call.DoctorNotifiedName = req.DoctorName;
        if (!string.IsNullOrWhiteSpace(req.Phone)) call.Phone = req.Phone;
        call.Department = req.Department ?? call.Department;
        call.ReadbackConfirmed = req.ReadbackConfirmed;
        call.Comments = req.Comments ?? call.Comments;
        _audit.Log("UPDATE", "lab_panic_call", call.Id, before, req);
        await _db.SaveChangesAsync();
        return call;
    }

    public async Task DeletePanicCallAsync(string id)
    {
        _policy.Require("Видалення панічного дзвінка", LabRoles.Admin, LabRoles.Doctor);
        var call = await _db.PanicCalls.FirstOrDefaultAsync(p => p.Id == id) ?? throw NotFoundException.For("Панічний дзвінок", id);
        _db.PanicCalls.Remove(call);
        _audit.Log("DELETE", "lab_panic_call", id, call, null);
        await _db.SaveChangesAsync();
    }

    public async Task<List<WorklistRowDto>> PanicPendingAsync()
    {
        var ids = await _db.Results.AsNoTracking()
            .Where(r => (r.Flag == ResultFlags.CritHigh || r.Flag == ResultFlags.CritLow) && r.OrderTest!.Status != OrderTestStatuses.Rejected && !_db.PanicCalls.Any(p => p.ResultId == r.Id))
            .Select(r => r.OrderTestId).ToListAsync();
        var rows = await _pipeline.RowQuery().Where(t => ids.Contains(t.Id)).OrderByDescending(t => t.Result!.EnteredAt).ToListAsync();
        return rows.Select(t => DtoMapper.ToWorklistRow(t, false, _policy)).ToList();
    }

    // ------------------------------------------------------------------ batches
    public async Task<List<LabWorklistBatch>> BatchesAsync(string? status) =>
        await _db.Batches.AsNoTracking().Where(b => status == null || b.Status == status).OrderByDescending(b => b.CreatedOn).ToListAsync();

    public async Task<LabWorklistBatch> CreateBatchAsync(BatchRequest req)
    {
        _policy.Require("Створення робочого листа", LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician);
        var ids = req.OrderTestIds.Distinct().ToList();
        if (ids.Count == 0) throw ValidationException.Field("orderTestIds", "Виберіть тести для робочого листа");
        var found = await _db.OrderTests.Where(t => ids.Contains(t.Id)).ToListAsync();
        if (found.Count != ids.Count) throw new ValidationException("Деякі тести не знайдено");
        if (req.AnalyzerId != null && !await _db.Analyzers.AnyAsync(a => a.Id == req.AnalyzerId)) throw NotFoundException.For("Аналізатор", req.AnalyzerId);
        var batch = new LabWorklistBatch { BatchCode = await _numerators.NextBatchCodeAsync(DateTime.UtcNow), AnalyzerId = req.AnalyzerId, Status = "OPEN", CreatedById = _current.EmployeeId, Items = ids };
        foreach (var t in found.Where(t => t.Status == OrderTestStatuses.Pending || t.Status == OrderTestStatuses.Rerun))
        {
            if (req.AnalyzerId != null) t.AssignedAnalyzerId = req.AnalyzerId;
            t.Status = OrderTestStatuses.InAnalysis;
        }
        _db.Batches.Add(batch);
        _audit.Log("CREATE", "lab_worklist_batch", batch.Id, null, new { batch.BatchCode, batch.AnalyzerId, count = ids.Count });
        await _db.SaveChangesAsync();
        return batch;
    }

    public async Task<LabWorklistBatch> UpdateBatchAsync(string id, BatchRequest req)
    {
        _policy.Require("Редагування робочого листа", LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician);
        var batch = await _db.Batches.FirstOrDefaultAsync(b => b.Id == id) ?? throw NotFoundException.For("Робочий лист", id);
        if (batch.Status == "COMPLETED") throw new ConflictException("Завершений робочий лист не редагується");
        var before = new { batch.Status, batch.AnalyzerId, items = batch.Items };
        if (req.OrderTestIds.Count > 0) batch.Items = req.OrderTestIds.Distinct().ToList();
        if (req.AnalyzerId != null) batch.AnalyzerId = req.AnalyzerId;
        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            var next = req.Status.ToUpperInvariant();
            var allowed = batch.Status switch { "OPEN" => new[] { "SENT", "COMPLETED" }, "SENT" => new[] { "COMPLETED", "OPEN" }, _ => Array.Empty<string>() };
            if (!allowed.Contains(next)) throw new ConflictException($"Робочий лист: перехід {batch.Status} → {next} неможливий");
            batch.Status = next;
        }
        _audit.Log("UPDATE", "lab_worklist_batch", batch.Id, before, req);
        await _db.SaveChangesAsync();
        return batch;
    }

    public async Task DeleteBatchAsync(string id)
    {
        _policy.Require("Видалення робочого листа", LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician);
        var batch = await _db.Batches.FirstOrDefaultAsync(b => b.Id == id) ?? throw NotFoundException.For("Робочий лист", id);
        if (batch.Status == "COMPLETED") throw new ConflictException("Завершений робочий лист не видаляється");
        _db.Batches.Remove(batch);
        _audit.Log("DELETE", "lab_worklist_batch", id, new { batch.BatchCode }, null);
        await _db.SaveChangesAsync();
    }

    public async Task<string> BatchPrintHtmlAsync(string id)
    {
        var batch = await _db.Batches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id) ?? throw NotFoundException.For("Робочий лист", id);
        var ids = batch.Items;
        var rows = await _pipeline.RowQuery().Where(t => ids.Contains(t.Id)).ToListAsync();
        var analyzer = batch.AnalyzerId == null ? null : await _db.Analyzers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == batch.AnalyzerId);
        var sb = new System.Text.StringBuilder();
        sb.Append("<!doctype html><html lang=\"uk\"><head><meta charset=\"utf-8\"><title>Робочий лист ").Append(batch.BatchCode)
          .Append("</title><style>body{font-family:Arial,sans-serif;font-size:12px}table{border-collapse:collapse;width:100%}td,th{border:1px solid #999;padding:4px 6px}th{background:#eef}@page{size:A4}</style></head><body>");
        sb.Append($"<h2>Робочий лист {batch.BatchCode}</h2><p>Аналізатор: {System.Net.WebUtility.HtmlEncode(analyzer?.Name ?? "ручна постановка")} · Статус: {batch.Status} · Створено: {batch.CreatedOn:dd.MM.yyyy HH:mm}</p>");
        sb.Append("<table><tr><th>#</th><th>Штрихкод</th><th>Замовлення</th><th>Пацієнт</th><th>Тест</th><th>Результат</th><th>Од.</th><th>Норма</th><th>Підпис</th></tr>");
        var i = 0;
        foreach (var t in rows.OrderByDescending(t => t.Order!.IsUrgentCito).ThenBy(t => t.Sample?.Barcode))
        {
            sb.Append($"<tr><td>{++i}</td><td>{t.Sample?.Barcode}</td><td>{t.Order?.OrderNumber}{(t.Order?.IsUrgentCito == true ? " <b>CITO</b>" : "")}</td><td>{System.Net.WebUtility.HtmlEncode(t.Order?.Patient?.FullName)}</td><td>{t.TestCode} — {System.Net.WebUtility.HtmlEncode(t.TestName)}</td><td style=\"min-width:80px\">{(t.Result == null ? "" : DtoMapper.FormatValue(t.Result.NumericValue, t.Result.StringValue, t.Test?.DecimalPlaces ?? 2))}</td><td>{t.Test?.Unit}</td><td>{t.Result?.ReferenceDisplay}</td><td style=\"min-width:60px\"></td></tr>");
        }
        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ unmatched
    public async Task<List<LabUnmatchedResult>> UnmatchedAsync(bool includeLinked) =>
        await _db.UnmatchedResults.AsNoTracking().Where(u => includeLinked || !u.IsLinked).OrderByDescending(u => u.ReceivedAt).ToListAsync();

    public async Task<WorklistRowDto> LinkUnmatchedAsync(string id, string orderTestId)
    {
        _policy.Require("Зв'язування результату", LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician);
        var u = await _db.UnmatchedResults.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Незв'язаний результат", id);
        if (u.IsLinked) throw new ConflictException("Результат уже зв'язано");
        double? numeric = double.TryParse(u.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        var row = await _pipeline.ApplyAsync(new ResultEntry
        {
            OrderTestId = orderTestId, NumericValue = numeric, StringValue = numeric.HasValue ? null : u.Value, Unit = u.Unit, AnalyzerId = u.AnalyzerId,
            AnalyzerFlags = u.Flags, RawMessageId = u.RawMessageId, MeasuredAt = u.MeasuredAt, Comment = $"Зв'язано вручну з незв'язаного результату {u.Barcode}/{u.AnalyzerCode}"
        });
        u.IsLinked = true; u.LinkedOrderTestId = orderTestId;
        _audit.Log("LINK", "lab_unmatched_result", u.Id, null, new { orderTestId });
        await _db.SaveChangesAsync();
        return row;
    }

    public async Task DeleteUnmatchedAsync(string id)
    {
        _policy.Require("Видалення незв'язаного результату", LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician);
        var u = await _db.UnmatchedResults.FirstOrDefaultAsync(x => x.Id == id) ?? throw NotFoundException.For("Незв'язаний результат", id);
        _db.UnmatchedResults.Remove(u);
        _audit.Log("DELETE", "lab_unmatched_result", id, u, null);
        await _db.SaveChangesAsync();
    }
}
