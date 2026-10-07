using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Models;
using MedLink.LIS.Api.Services;

namespace MedLink.LIS.Api.Controllers
{
    [Route("api/laboratory/norms")]
    [ApiController]
    public class LaboratoryNormsController : ControllerBase
    {
        private readonly MedLinkLabDbContext _db;
        private readonly ILabNormsCascadeService _cascadeService;

        public LaboratoryNormsController(MedLinkLabDbContext db, ILabNormsCascadeService cascadeService)
        {
            _db = db;
            _cascadeService = cascadeService;
        }

        [HttpGet("combinations")]
        public async Task<IActionResult> GetAllCombinations()
        {
            var items = await _db.ReferenceLayers
                .OrderBy(r => r.TestCode)
                .ThenByDescending(r => r.PriorityOrder)
                .ToListAsync();

            return Ok(new { success = true, data = items });
        }

        [HttpGet("layers")]
        public async Task<IActionResult> GetLayers([FromQuery] string testCode = "GLU", [FromQuery] string methodCode = "HEX_IFCC")
        {
            var layers = await _cascadeService.GetLayersAsync(testCode, methodCode);
            return Ok(new { success = true, testCode, methodCode, data = layers });
        }

        [HttpPost("combinations")]
        public async Task<IActionResult> SaveCombination([FromBody] LabReferenceLayer layer)
        {
            if (string.IsNullOrEmpty(layer.Id))
            {
                layer.Id = $"REF-{layer.TestCode}-{await _db.ReferenceLayers.CountAsync() + 1}";
            }

            var existing = await _db.ReferenceLayers.FindAsync(layer.Id);
            if (existing != null)
            {
                _db.Entry(existing).CurrentValues.SetValues(layer);
            }
            else
            {
                await _db.ReferenceLayers.AddAsync(layer);
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Шар норми успішно збережено в базі даних!", id = layer.Id });
        }

        [HttpDelete("combinations/{id}")]
        public async Task<IActionResult> DeleteCombination(string id)
        {
            var layer = await _db.ReferenceLayers.FindAsync(id);
            if (layer == null)
            {
                return NotFound(new { success = false, error = "Шар не знайдено" });
            }

            _db.ReferenceLayers.Remove(layer);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = $"Шар {id} видалено з бази даних!" });
        }

        [HttpPost("resolve-cascade")]
        public async Task<IActionResult> ResolveCascade([FromBody] CascadeResolveRequest request)
        {
            var result = await _cascadeService.ResolveCascadeAsync(request);
            return Ok(result);
        }
    }
}
