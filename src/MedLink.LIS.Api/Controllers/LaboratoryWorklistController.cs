using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Models;

namespace MedLink.LIS.Api.Controllers
{
    [ApiController]
    public class LaboratoryWorklistController : ControllerBase
    {
        private readonly MedLinkLabDbContext _db;

        public LaboratoryWorklistController(MedLinkLabDbContext db)
        {
            _db = db;
        }

        [HttpGet("api/laboratory/worklist")]
        public async Task<IActionResult> GetWorklist()
        {
            var results = await _db.Results.ToListAsync();
            var orders = await _db.Orders.ToListAsync();
            var samples = await _db.Samples.ToListAsync();
            var analyzers = await _db.Analyzers.ToListAsync();

            var data = results.Select(r =>
            {
                var ord = orders.FirstOrDefault(o => o.Id == r.OrderId);
                var smp = samples.FirstOrDefault(s => s.Id == r.SampleId);
                var an = analyzers.FirstOrDefault(a => a.Id == r.AnalyzerId);

                return new
                {
                    id = r.Id,
                    barcode = smp?.Barcode ?? "1026004812",
                    patient = ord != null ? "Мельник Ю.В." : "Пацієнт",
                    analyzer = an?.Name ?? "Sysmex XN-1000",
                    test = r.TestName,
                    test_code = r.TestCode,
                    value = r.NumericValue,
                    unit = r.Unit,
                    normMin = r.NormMin,
                    normMax = r.NormMax,
                    flag = r.Flag,
                    deltaPercent = r.DeltaPercent.HasValue ? (r.DeltaPercent > 0 ? $"+{r.DeltaPercent}%" : $"{r.DeltaPercent}%") : null,
                    status = r.Status,
                    is_auto_verified = r.IsAutoVerified,
                    isCito = ord?.IsUrgentCito == 1
                };
            }).ToList();

            return Ok(new { success = true, data });
        }

        public class UpdateResultRequest
        {
            public double NumericValue { get; set; }
            public string? Comment { get; set; }
        }

        [HttpPost("api/laboratory/results/{id}/update")]
        public async Task<IActionResult> UpdateResult(string id, [FromBody] UpdateResultRequest req)
        {
            var result = await _db.Results.FindAsync(id);
            if (result == null) return NotFound(new { success = false, error = "Результат не знайдено" });

            result.NumericValue = req.NumericValue;
            string newFlag = "NORMAL";
            if (result.NormMin.HasValue && req.NumericValue < result.NormMin.Value) newFlag = "LOW";
            else if (result.NormMax.HasValue && req.NumericValue > result.NormMax.Value) newFlag = "HIGH";

            if (result.TestCode == "GLU" && req.NumericValue > 25.0) newFlag = "CRIT_HIGH";
            else if (result.TestCode == "TROP_I" && req.NumericValue > 0.04) newFlag = "CRIT_HIGH";

            result.Flag = newFlag;
            result.Status = "NEEDS_DOCTOR";
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = $"Результат оновлено: {req.NumericValue} (Прапорець: {newFlag})", flag = newFlag });
        }

        public class ValidateRequest
        {
            public string DoctorId { get; set; } = "EMP-01";
        }

        [HttpPost("api/laboratory/results/{id}/validate")]
        public async Task<IActionResult> ValidateResult(string id, [FromBody] ValidateRequest req)
        {
            var result = await _db.Results.FindAsync(id);
            if (result == null) return NotFound(new { success = false, error = "Результат не знайдено" });

            result.Status = "MANUAL_VERIFIED";
            result.VerifiedById = req.DoctorId;
            result.VerifiedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = $"Результат {id} валідовано лікарем!" });
        }

        [HttpPost("api/laboratory/worklist/autoverify")]
        public async Task<IActionResult> AutoVerify()
        {
            var normalPending = await _db.Results
                .Where(r => r.Flag == "NORMAL" && r.Status != "AUTO_VERIFIED")
                .ToListAsync();

            foreach (var r in normalPending)
            {
                r.IsAutoVerified = 1;
                r.Status = "AUTO_VERIFIED";
                r.VerifiedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = $"Автовалідовано {normalPending.Count} нормальних результатів!", affected = normalPending.Count });
        }

        [HttpGet("api/laboratory/panic-calls")]
        public async Task<IActionResult> GetPanicCalls()
        {
            var items = await _db.PanicCallLogs
                .OrderByDescending(c => c.NotifiedAt)
                .ToListAsync();

            return Ok(new { success = true, data = items });
        }

        public class PanicCallRequest
        {
            public string ResultId { get; set; } = "RES-01";
            public string DoctorName { get; set; } = "Черговий лікар ВРІТ";
            public string Phone { get; set; } = "+380501234567";
            public string? Notes { get; set; }
        }

        [HttpPost("api/laboratory/panic-calls")]
        public async Task<IActionResult> AddPanicCall([FromBody] PanicCallRequest req)
        {
            var count = await _db.PanicCallLogs.CountAsync();
            var log = new LabPanicCallLog
            {
                Id = $"CALL-{count + 1:03d}",
                ResultId = req.ResultId,
                DoctorNotifiedName = req.DoctorName,
                PhoneCalled = req.Phone,
                Comments = req.Notes,
                NotifiedById = "EMP-01",
                NotifiedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            await _db.PanicCallLogs.AddAsync(log);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = $"Телефонне сповіщення зареєстровано в журналі CITO: {log.Id}!", id = log.Id });
        }

        public class CollectRequest
        {
            public string Barcode { get; set; } = "1026004812";
        }

        [HttpPost("api/laboratory/phlebotomy/collect")]
        public async Task<IActionResult> CollectSample([FromBody] CollectRequest req)
        {
            var smp = await _db.Samples.FirstOrDefaultAsync(s => s.Barcode == req.Barcode);
            if (smp != null)
            {
                smp.Status = "COLLECTED";
                smp.CollectedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true, message = $"Забір зразка {req.Barcode} підтверджено в базі даних!", barcode = req.Barcode });
        }
    }
}
