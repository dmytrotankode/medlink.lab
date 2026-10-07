using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Models;

namespace MedLink.LIS.Api.Controllers
{
    [Route("api/laboratory/biobank")]
    [ApiController]
    public class LaboratoryBiobankController : ControllerBase
    {
        private readonly MedLinkLabDbContext _db;

        public LaboratoryBiobankController(MedLinkLabDbContext db)
        {
            _db = db;
        }

        [HttpGet("cells")]
        public async Task<IActionResult> GetCells()
        {
            var cells = await _db.ArchiveCells.OrderBy(c => c.CellCoordinate).ToListAsync();
            return Ok(new { success = true, data = cells });
        }

        public class PlaceCellRequest
        {
            public string Coordinate { get; set; } = "A-01";
            public string Barcode { get; set; } = "1026004812";
            public string Rack { get; set; } = "RACK-A1";
            public string Box { get; set; } = "BOX-01";
        }

        [HttpPost("cells/place")]
        public async Task<IActionResult> PlaceCell([FromBody] PlaceCellRequest req)
        {
            var cell = await _db.ArchiveCells.FirstOrDefaultAsync(c => c.CellCoordinate == req.Coordinate);
            if (cell == null)
            {
                cell = new LabSampleArchiveCell
                {
                    Id = $"CELL-{req.Coordinate}",
                    RackCode = req.Rack,
                    BoxNumber = req.Box,
                    CellCoordinate = req.Coordinate,
                    SampleId = req.Barcode,
                    StoredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ExpiryAt = DateTime.Now.AddDays(180).ToString("yyyy-MM-dd HH:mm:ss")
                };
                await _db.ArchiveCells.AddAsync(cell);
            }
            else
            {
                cell.SampleId = req.Barcode;
                cell.StoredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                cell.ExpiryAt = DateTime.Now.AddDays(180).ToString("yyyy-MM-dd HH:mm:ss");
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = $"Зразок {req.Barcode} успішно розміщено в комірці {req.Coordinate} бази даних!", coordinate = req.Coordinate });
        }

        public class RemoveCellRequest
        {
            public string Coordinate { get; set; } = string.Empty;
        }

        [HttpPost("cells/remove")]
        public async Task<IActionResult> RemoveCell([FromBody] RemoveCellRequest req)
        {
            var cell = await _db.ArchiveCells.FirstOrDefaultAsync(c => c.CellCoordinate == req.Coordinate);
            if (cell != null)
            {
                _db.ArchiveCells.Remove(cell);
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true, message = $"Комірку {req.Coordinate} звільнено в базі даних!" });
        }
    }

    [Route("api/laboratory/dictionaries")]
    [ApiController]
    public class LaboratoryDictionariesController : ControllerBase
    {
        private readonly MedLinkLabDbContext _db;

        public LaboratoryDictionariesController(MedLinkLabDbContext db)
        {
            _db = db;
        }

        // Biomaterials
        [HttpGet("biomaterials")]
        public async Task<IActionResult> GetBiomaterials() =>
            Ok(new { success = true, data = await _db.BiomaterialTypes.OrderBy(b => b.Id).ToListAsync() });

        [HttpPost("biomaterials")]
        public async Task<IActionResult> SaveBiomaterial([FromBody] LabBiomaterialType item)
        {
            if (item.Id == 0) item.Id = (await _db.BiomaterialTypes.MaxAsync(b => (int?)b.Id) ?? 0) + 1;
            var ex = await _db.BiomaterialTypes.FindAsync(item.Id);
            if (ex != null) _db.Entry(ex).CurrentValues.SetValues(item);
            else await _db.BiomaterialTypes.AddAsync(item);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Біоматеріал збережено в SQLite!", id = item.Id });
        }

        [HttpDelete("biomaterials/{id}")]
        public async Task<IActionResult> DeleteBiomaterial(int id)
        {
            var item = await _db.BiomaterialTypes.FindAsync(id);
            if (item != null) { _db.BiomaterialTypes.Remove(item); await _db.SaveChangesAsync(); }
            return Ok(new { success = true, message = $"Біоматеріал {id} видалено!" });
        }

        // Tubes
        [HttpGet("tubes")]
        public async Task<IActionResult> GetTubes() =>
            Ok(new { success = true, data = await _db.TubeTypes.OrderBy(t => t.Id).ToListAsync() });

        [HttpPost("tubes")]
        public async Task<IActionResult> SaveTube([FromBody] LabTubeType item)
        {
            if (item.Id == 0) item.Id = (await _db.TubeTypes.MaxAsync(t => (int?)t.Id) ?? 0) + 1;
            var ex = await _db.TubeTypes.FindAsync(item.Id);
            if (ex != null) _db.Entry(ex).CurrentValues.SetValues(item);
            else await _db.TubeTypes.AddAsync(item);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Пробірку збережено в SQLite!", id = item.Id });
        }

        [HttpDelete("tubes/{id}")]
        public async Task<IActionResult> DeleteTube(int id)
        {
            var item = await _db.TubeTypes.FindAsync(id);
            if (item != null) { _db.TubeTypes.Remove(item); await _db.SaveChangesAsync(); }
            return Ok(new { success = true, message = $"Пробірку {id} видалено!" });
        }

        // Analyzers
        [HttpGet("analyzers")]
        public async Task<IActionResult> GetAnalyzers() =>
            Ok(new { success = true, data = await _db.Analyzers.OrderBy(a => a.Id).ToListAsync() });

        [HttpPost("analyzers")]
        public async Task<IActionResult> SaveAnalyzer([FromBody] LabAnalyzer item)
        {
            if (string.IsNullOrEmpty(item.Id)) item.Id = $"AN-{await _db.Analyzers.CountAsync() + 1:02d}";
            var ex = await _db.Analyzers.FindAsync(item.Id);
            if (ex != null) _db.Entry(ex).CurrentValues.SetValues(item);
            else await _db.Analyzers.AddAsync(item);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Аналізатор збережено в SQLite!", id = item.Id });
        }

        [HttpDelete("analyzers/{id}")]
        public async Task<IActionResult> DeleteAnalyzer(string id)
        {
            var item = await _db.Analyzers.FindAsync(id);
            if (item != null) { _db.Analyzers.Remove(item); await _db.SaveChangesAsync(); }
            return Ok(new { success = true, message = $"Аналізатор {id} видалено!" });
        }

        // Parameters
        [HttpGet("parameters")]
        public async Task<IActionResult> GetParameters() =>
            Ok(new { success = true, data = await _db.TestDefinitions.OrderBy(p => p.Id).ToListAsync() });

        [HttpPost("parameters")]
        public async Task<IActionResult> SaveParameter([FromBody] LabTestDefinition item)
        {
            if (item.Id == 0) item.Id = (await _db.TestDefinitions.MaxAsync(p => (int?)p.Id) ?? 0) + 1;
            var ex = await _db.TestDefinitions.FindAsync(item.Id);
            if (ex != null) _db.Entry(ex).CurrentValues.SetValues(item);
            else await _db.TestDefinitions.AddAsync(item);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Параметр дослідження збережено в SQLite!", id = item.Id });
        }

        [HttpDelete("parameters/{id}")]
        public async Task<IActionResult> DeleteParameter(int id)
        {
            var item = await _db.TestDefinitions.FindAsync(id);
            if (item != null) { _db.TestDefinitions.Remove(item); await _db.SaveChangesAsync(); }
            return Ok(new { success = true, message = $"Параметр {id} видалено!" });
        }
    }
}
