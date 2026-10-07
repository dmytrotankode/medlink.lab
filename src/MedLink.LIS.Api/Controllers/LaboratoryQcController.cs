using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Models;

namespace MedLink.LIS.Api.Controllers
{
    [Route("api/laboratory/qc")]
    [ApiController]
    public class LaboratoryQcController : ControllerBase
    {
        private readonly MedLinkLabDbContext _db;

        public LaboratoryQcController(MedLinkLabDbContext db)
        {
            _db = db;
        }

        [HttpGet("measurements")]
        public async Task<IActionResult> GetMeasurements([FromQuery] string param = "WBC")
        {
            var items = await _db.QcResults
                .Where(q => q.TestCode == param)
                .OrderBy(q => q.CreatedAt)
                .ToListAsync();

            var lockoutCount = items.Count(q => q.IsLockout == 1);

            return Ok(new
            {
                success = true,
                param,
                isLockout = lockoutCount > 0,
                data = items
            });
        }

        public class AddQcMeasurementRequest
        {
            public string TestCode { get; set; } = "WBC";
            public double MeasuredValue { get; set; } = 7.2;
            public double TargetMean { get; set; } = 7.20;
            public double TargetSd { get; set; } = 0.30;
            public string LotNumber { get; set; } = "LOT-88412";
            public string ControlMaterial { get; set; } = "Bio-Rad Lyphochek Level 2";
            public string AnalyzerId { get; set; } = "AN-01";
        }

        [HttpPost("measurements")]
        public async Task<IActionResult> AddMeasurement([FromBody] AddQcMeasurementRequest req)
        {
            double z = Math.Round((req.MeasuredValue - req.TargetMean) / req.TargetSd, 2);
            int isViolation = 0;
            string? rule = null;
            int isLockout = 0;

            if (Math.Abs(z) >= 3.0)
            {
                isViolation = 1;
                rule = "1-3s (LOCKOUT)";
                isLockout = 1;
            }
            else if (Math.Abs(z) >= 2.0)
            {
                isViolation = 1;
                rule = "1-2s (ПОПЕРЕДЖЕННЯ)";
            }

            var count = await _db.QcResults.CountAsync(q => q.TestCode == req.TestCode);
            var item = new LabQcResult
            {
                Id = $"QC-{req.TestCode}-{count + 1:02d}",
                AnalyzerId = req.AnalyzerId,
                ControlMaterial = req.ControlMaterial,
                LotNumber = req.LotNumber,
                TestCode = req.TestCode,
                MeasuredValue = req.MeasuredValue,
                TargetMean = req.TargetMean,
                TargetSd = req.TargetSd,
                ZScore = z,
                IsViolation = isViolation,
                ViolatedRule = rule,
                IsLockout = isLockout,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            await _db.QcResults.AddAsync(item);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Точку контролю збережено в базі: Z-score = {z}" + (isLockout == 1 ? " 🚨 УВАГА: 1-3s БЛОКУВАННЯ!" : ""),
                id = item.Id,
                z_score = z,
                is_lockout = isLockout == 1,
                violated_rule = rule
            });
        }

        public class ResolveLockoutRequest
        {
            public string Action { get; set; } = "Промивка Cycle Clean";
            public string Comment { get; set; } = "Повторний прогін у межах норми";
        }

        [HttpPost("resolve-lockout")]
        public async Task<IActionResult> ResolveLockout([FromBody] ResolveLockoutRequest req)
        {
            var locked = await _db.QcResults.Where(q => q.IsLockout == 1).ToListAsync();
            foreach (var l in locked)
            {
                l.IsLockout = 0;
                l.LockoutResolvedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                l.ResolutionAction = $"{req.Action}: {req.Comment}";
            }

            await _db.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                message = $"Блокування аналізатора (Lockout) знято у {locked.Count} записах бази даних!"
            });
        }
    }
}
