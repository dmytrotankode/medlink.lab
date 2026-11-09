// Початкове наповнення й завантаження в кеш довідників, що раніше були константами в коді:
// цитологічні довідники, тарифи патоморфології, версії шаблонів бланка. Ідемпотентно (лише порожні таблиці).
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public static class DictionaryRuntime
{
    /// <summary>Сід порожніх таблиць типовими значеннями й завантаження кешу PathPrices/PathCytology.</summary>
    public static async Task SeedAndApplyAsync(LisDbContext db)
    {
        if (!await db.PathCytoItems.AnyAsync())
        {
            var order = 0;
            foreach (var (code, name) in PathCytology.DefaultMaterials) db.PathCytoItems.Add(new LabPathCytoItem { Kind = CytoItemKinds.Material, Code = code, Name = name, SortOrder = ++order });
            order = 0;
            foreach (var (code, name) in PathCytology.DefaultAdequacy) db.PathCytoItems.Add(new LabPathCytoItem { Kind = CytoItemKinds.Adequacy, Code = code, Name = name, SortOrder = ++order });
            order = 0;
            foreach (var c in PathCytology.DefaultCategories) db.PathCytoItems.Add(new LabPathCytoItem { Kind = CytoItemKinds.Category, Code = c.Code, Name = c.Name, IsHigh = c.High, IsNormal = c.Normal, Materials = string.Join(",", c.Materials), SortOrder = ++order });
        }
        if (!await db.PathPriceItems.AnyAsync())
            foreach (var (code, name, def) in PathPrices.Defaults) db.PathPriceItems.Add(new LabPathPrice { Code = code, Name = name, Amount = def });
        if (!await db.ReportTemplates.AnyAsync())
        {
            db.ReportTemplates.Add(new LabReportTemplate { Variant = "final", Version = 1, Name = "Основний бланк", IsActive = true });
            db.ReportTemplates.Add(new LabReportTemplate { Variant = "preliminary", Version = 1, Name = "Попередній бланк", IsActive = true });
            db.ReportTemplates.Add(new LabReportTemplate { Variant = "cito", Version = 1, Name = "Бланк CITO", IsActive = true });
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
        await ApplyAsync(db);
    }

    /// <summary>Перечитує довідники в кеш (після зміни).</summary>
    public static async Task ApplyAsync(LisDbContext db)
    {
        PathCytology.Apply(await db.PathCytoItems.AsNoTracking().ToListAsync());
        PathPrices.Apply(await db.PathPriceItems.AsNoTracking().ToListAsync());
        await WorkCalendar.RefreshAsync(db);
    }
}
