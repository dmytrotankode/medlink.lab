// Шаблони бланків результатів: версії варіантів (основний / попередній / CITO), активація версії,
// редагування реквізитів (заголовок, рядок під шапкою, підвал, мови) без дизайнера.
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Controllers;

public sealed class ReportTemplateRequest
{
    public string? Variant { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? HeaderNote { get; set; }
    public string? FooterText { get; set; }
    public string? Languages { get; set; }
    public string? Note { get; set; }
    /// <summary>Версія-джерело, з якої копіюються реквізити, що не вказані.</summary>
    public string? CopyFromId { get; set; }
    public bool Activate { get; set; }
}

public sealed class ReportTemplatesController : LisControllerBase
{
    public static readonly string[] Variants = { "final", "preliminary", "cito" };
    private static readonly string[] Langs = { "uk", "en" };
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    public ReportTemplatesController(LisDbContext db, IRolePolicy policy, IAuditService audit) { _db = db; _policy = policy; _audit = audit; }

    private static string VariantLabel(string v) => v switch { "final" => "Основний бланк", "preliminary" => "Попередній бланк", "cito" => "Бланк CITO", _ => v };
    private static object Dto(LabReportTemplate t) => new { t.Id, t.Variant, variantName = VariantLabel(t.Variant), t.Version, t.Name, t.Title, t.HeaderNote, t.FooterText, t.Languages, t.Note, t.IsActive, t.CreatedOn, t.ModifiedOn };

    /// <summary>Усі версії шаблонів: за варіантом, нові версії зверху. Читання — усім ролям.</summary>
    [HttpGet("report-templates")]
    public async Task<IActionResult> List([FromQuery] string? variant)
    {
        var q = _db.ReportTemplates.AsNoTracking().Where(t => t.RecordState != RecordStates.Deleted);
        if (!string.IsNullOrWhiteSpace(variant)) q = q.Where(t => t.Variant == variant);
        return Ok((await q.ToListAsync()).OrderBy(t => Array.IndexOf(Variants, t.Variant)).ThenByDescending(t => t.Version).Select(Dto).ToList());
    }

    [HttpGet("report-templates/{id}")]
    public async Task<IActionResult> Get(string id) => Ok(Dto(await Find(id)));

    private async Task<LabReportTemplate> Find(string id) =>
        await _db.ReportTemplates.FirstOrDefaultAsync(t => t.Id == id && t.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Шаблон бланка", id);

    private static string NormalizeLanguages(string? languages)
    {
        var list = (languages ?? "uk").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToLowerInvariant()).Distinct().ToList();
        if (list.Count == 0) list.Add("uk");
        if (list.Any(l => !Langs.Contains(l))) throw ValidationException.Field("languages", "Мови бланка: uk, en");
        return string.Join(",", list);
    }

    private static void Validate(LabReportTemplate t)
    {
        if (string.IsNullOrWhiteSpace(t.Name)) throw ValidationException.Field("name", "Вкажіть назву версії");
        if ((t.Title?.Length ?? 0) > 256) throw ValidationException.Field("title", "Заголовок — до 256 символів");
        if ((t.HeaderNote?.Length ?? 0) > 512) throw ValidationException.Field("headerNote", "Рядок під шапкою — до 512 символів");
        if ((t.FooterText?.Length ?? 0) > 4000) throw ValidationException.Field("footerText", "Підвал — до 4000 символів");
    }

    /// <summary>Нова версія варіанта (номер — наступний за найбільшим). Реквізити, що не передано, копіюються з активної (або вказаної) версії.</summary>
    [HttpPost("report-templates")]
    public async Task<IActionResult> Create([FromBody] ReportTemplateRequest req)
    {
        _policy.Require("Шаблони бланків результатів", LabRoles.Admin);
        var variant = (req.Variant ?? "").Trim().ToLowerInvariant();
        if (!Variants.Contains(variant)) throw ValidationException.Field("variant", "Варіант бланка: final, preliminary або cito");
        var siblings = await _db.ReportTemplates.Where(t => t.Variant == variant).ToListAsync();
        var source = req.CopyFromId != null ? siblings.FirstOrDefault(t => t.Id == req.CopyFromId) ?? throw ValidationException.Field("copyFromId", "Версію-джерело не знайдено") : siblings.FirstOrDefault(t => t.IsActive);
        var t = new LabReportTemplate
        {
            Variant = variant, Version = (siblings.Count == 0 ? 0 : siblings.Max(x => x.Version)) + 1,
            Name = req.Name ?? (source != null ? source.Name : VariantLabel(variant)),
            Title = req.Title ?? source?.Title, HeaderNote = req.HeaderNote ?? source?.HeaderNote, FooterText = req.FooterText ?? source?.FooterText,
            Languages = NormalizeLanguages(req.Languages ?? source?.Languages), Note = req.Note, IsActive = false
        };
        Validate(t);
        _db.ReportTemplates.Add(t);
        if (req.Activate) { foreach (var s in siblings) s.IsActive = false; t.IsActive = true; }
        _audit.Log("CREATE", "lab_report_template", t.Id, null, new { t.Variant, t.Version, t.Name, t.IsActive });
        await _db.SaveChangesAsync();
        return StatusCode(201, Dto(t));
    }

    /// <summary>Редагування реквізитів версії (заголовок, рядок під шапкою, підвал, мови, назва, примітка).</summary>
    [HttpPut("report-templates/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ReportTemplateRequest req)
    {
        _policy.Require("Шаблони бланків результатів", LabRoles.Admin);
        var t = await Find(id);
        var before = Dto(t);
        if (req.Name != null) t.Name = req.Name.Trim();
        if (req.Title != null) t.Title = string.IsNullOrWhiteSpace(req.Title) ? null : req.Title.Trim();
        if (req.HeaderNote != null) t.HeaderNote = string.IsNullOrWhiteSpace(req.HeaderNote) ? null : req.HeaderNote.Trim();
        if (req.FooterText != null) t.FooterText = string.IsNullOrWhiteSpace(req.FooterText) ? null : req.FooterText.Trim();
        if (req.Languages != null) t.Languages = NormalizeLanguages(req.Languages);
        if (req.Note != null) t.Note = req.Note;
        Validate(t);
        _audit.Log("UPDATE", "lab_report_template", t.Id, before, Dto(t));
        await _db.SaveChangesAsync();
        return Ok(Dto(t));
    }

    /// <summary>Зробити версію активною: попередня активна версія цього варіанта вимикається.</summary>
    [HttpPost("report-templates/{id}/activate")]
    public async Task<IActionResult> Activate(string id)
    {
        _policy.Require("Шаблони бланків результатів", LabRoles.Admin);
        var t = await Find(id);
        foreach (var s in await _db.ReportTemplates.Where(x => x.Variant == t.Variant && x.Id != t.Id && x.IsActive).ToListAsync()) s.IsActive = false;
        t.IsActive = true;
        _audit.Log("ACTIVATE", "lab_report_template", t.Id, null, new { t.Variant, t.Version });
        await _db.SaveChangesAsync();
        return Ok(Dto(t));
    }

    /// <summary>Видалення неактивної версії (активну видалити не можна).</summary>
    [HttpDelete("report-templates/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        _policy.Require("Шаблони бланків результатів", LabRoles.Admin);
        var t = await Find(id);
        if (t.IsActive) throw new ConflictException("Активну версію видалити не можна: спершу активуйте іншу");
        _db.ReportTemplates.Remove(t);
        _audit.Log("DELETE", "lab_report_template", t.Id, new { t.Variant, t.Version }, null);
        await _db.SaveChangesAsync();
        return Ok(new { id = t.Id, deleted = true });
    }
}
