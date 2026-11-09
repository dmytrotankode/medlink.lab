// Ведення довідників оплати: архів/видалення платників і прайс-листів, пакети послуг, масове оновлення цін файлом.
using System.Globalization;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class PackageSaveRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public List<string>? Members { get; set; }
    public bool? IsActive { get; set; }
}

public sealed partial class BillingService
{
    public const int MaxPriceImportRows = 5000;

    /// <summary>Платник: використовується в замовленнях/рахунках/полісах → архів (isActive=false), інакше видалення. Платник «Пацієнт» не видаляється.</summary>
    public async Task<object> DeletePayerAsync(string id, string? kind = null)
    {
        _policy.Require("Довідник платників", LabRoles.Admin);
        var p = await _db.Payers.FirstOrDefaultAsync(x => (x.Id == id || x.Code == id) && x.RecordState != RecordStates.Deleted && (kind == null || x.Kind == kind)) ?? throw NotFoundException.For("Платник", id);
        if (p.Code == PatientPayerCode || p.Kind == PayerKinds.Patient) throw new ConflictException("Платника «Пацієнт» не можна видалити або архівувати: на ньому тримається каса");
        var deps = await _db.Orders.CountAsync(o => o.PayerId == p.Id) + await _db.OrderCharges.CountAsync(c => c.PayerId == p.Id) + await _db.Invoices.CountAsync(i => i.PayerId == p.Id) + await _db.PatientInsurances.CountAsync(i => i.PayerId == p.Id);
        if (deps > 0)
        {
            p.IsActive = false;
            _audit.Log("SOFT_DELETE", "lab_payer", p.Id, null, new { isActive = false, dependencies = deps });
            await _db.SaveChangesAsync();
            return new { id = p.Id, deleted = false, deactivated = true, dependencies = deps, message = $"Запис використовується ({deps}) — переведено в архів" };
        }
        _db.Payers.Remove(p);
        _audit.Log("DELETE", "lab_payer", p.Id, new { p.Code, p.Name }, null);
        await _db.SaveChangesAsync();
        return new { id = p.Id, deleted = true };
    }

    /// <summary>Прайс-лист: використовується платниками/нарахуваннями → архів, інакше видалення разом з позиціями. Роздрібний (за замовчуванням) не видаляється.</summary>
    public async Task<object> DeletePriceListAsync(string id)
    {
        _policy.Require("Прайс-листи", LabRoles.Admin);
        var l = await _db.PriceLists.Include(x => x.Items).Include(x => x.Packages).FirstOrDefaultAsync(x => (x.Id == id || x.Code == id) && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Прайс-лист", id);
        if (l.IsDefault) throw new ConflictException("Роздрібний прайс-лист (за замовчуванням) не можна видалити: призначте іншим прайсом за замовчуванням");
        var deps = await _db.Payers.CountAsync(p => p.PriceListId == l.Id) + await _db.OrderCharges.CountAsync(c => c.PriceListId == l.Id);
        if (deps > 0)
        {
            l.IsActive = false;
            _audit.Log("SOFT_DELETE", "lab_price_list", l.Id, null, new { isActive = false, dependencies = deps });
            await _db.SaveChangesAsync();
            return new { id = l.Id, deleted = false, deactivated = true, dependencies = deps, message = $"Прайс використовується ({deps}) — переведено в архів" };
        }
        _db.PriceListItems.RemoveRange(l.Items);
        _db.PricePackages.RemoveRange(l.Packages);
        _db.PriceLists.Remove(l);
        _audit.Log("DELETE", "lab_price_list", l.Id, new { l.Code, l.Name }, null);
        await _db.SaveChangesAsync();
        return new { id = l.Id, deleted = true };
    }

    // ------------------------------------------------------------------ пакети
    public async Task<object> SavePackageAsync(string priceListId, string? packageId, PackageSaveRequest req)
    {
        _policy.Require("Прайс-листи", LabRoles.Admin);
        var list = await _db.PriceLists.FirstOrDefaultAsync(x => (x.Id == priceListId || x.Code == priceListId) && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Прайс-лист", priceListId);
        LabPricePackage pk;
        if (packageId == null)
        {
            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Код і назва пакета обов'язкові");
            pk = new LabPricePackage { PriceListId = list.Id };
            _db.PricePackages.Add(pk);
        }
        else pk = await _db.PricePackages.FirstOrDefaultAsync(x => x.Id == packageId && x.PriceListId == list.Id && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Пакет послуг", packageId);
        if (req.Code != null)
        {
            var code = req.Code.Trim().ToUpperInvariant();
            if (code.Length == 0) throw ValidationException.Field("code", "Вкажіть код пакета");
            if (await _db.PricePackages.AnyAsync(x => x.PriceListId == list.Id && x.Code == code && x.Id != pk.Id && x.RecordState != RecordStates.Deleted)) throw new ConflictException($"Пакет {code} у цьому прайсі вже існує");
            pk.Code = code;
        }
        if (req.Name != null) { if (string.IsNullOrWhiteSpace(req.Name)) throw ValidationException.Field("name", "Вкажіть назву пакета"); pk.Name = req.Name.Trim(); }
        if (req.Price.HasValue) { if (req.Price < 0) throw ValidationException.Field("price", "Ціна пакета не може бути від'ємною"); pk.Price = req.Price.Value; }
        if (req.Members != null)
        {
            var ids = await ResolveIdsAsync(req.Members);
            var members = req.Members.Select(m => ids[m].ProfileId ?? ids[m].TestId!).Distinct().ToList();
            if (members.Count < 2) throw new ValidationException("Пакет має містити щонайменше дві різні послуги");
            pk.MemberIds = members;
        }
        else if (packageId == null) throw new ValidationException("Вкажіть склад пакета (щонайменше дві послуги)");
        if (req.IsActive.HasValue) pk.IsActive = req.IsActive.Value;
        _audit.Log(packageId == null ? "CREATE" : "UPDATE", "lab_price_package", pk.Id, null, new { pk.Code, pk.Name, pk.Price, members = pk.MemberIds.Count, pk.IsActive });
        await _db.SaveChangesAsync();
        return new { pk.Id, pk.PriceListId, pk.Code, pk.Name, pk.Price, members = pk.MemberIds, pk.IsActive };
    }

    public async Task<object> DeletePackageAsync(string priceListId, string packageId)
    {
        _policy.Require("Прайс-листи", LabRoles.Admin);
        var pk = await _db.PricePackages.FirstOrDefaultAsync(x => x.Id == packageId && (x.PriceListId == priceListId || x.PriceList!.Code == priceListId) && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Пакет послуг", packageId);
        var deps = await _db.OrderCharges.CountAsync(c => c.PackageId == pk.Id);
        if (deps > 0)
        {
            pk.IsActive = false;
            _audit.Log("SOFT_DELETE", "lab_price_package", pk.Id, null, new { isActive = false, dependencies = deps });
            await _db.SaveChangesAsync();
            return new { id = pk.Id, deleted = false, deactivated = true, dependencies = deps, message = $"Пакет застосовано в {deps} нарахуваннях — переведено в архів" };
        }
        _db.PricePackages.Remove(pk);
        _audit.Log("DELETE", "lab_price_package", pk.Id, new { pk.Code, pk.Name }, null);
        await _db.SaveChangesAsync();
        return new { id = pk.Id, deleted = true };
    }

    // ------------------------------------------------------------------ масове оновлення цін файлом
    /// <summary>
    /// Масове оновлення цін прайс-листа з CSV («;» або «,»): колонки code (код послуги/показника) і price. Існуючі позиції оновлюються,
    /// нові додаються; інші позиції прайса не чіпаються. dryRun=true — звіт без запису. Ліміт — 5000 рядків.
    /// </summary>
    public async Task<object> ImportPricesAsync(string priceListId, string content, bool dryRun)
    {
        _policy.Require("Прайс-листи", LabRoles.Admin);
        var list = await _db.PriceLists.Include(x => x.Items).FirstOrDefaultAsync(x => (x.Id == priceListId || x.Code == priceListId) && x.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Прайс-лист", priceListId);
        content = content.TrimStart('﻿');
        var firstLine = content.Split('\n', 2)[0];
        var table = DictionaryService.ParseCsv(content, firstLine.Contains(';') ? ';' : firstLine.Contains('\t') ? '\t' : ',');
        if (table.Count < 2) throw new ValidationException("CSV має містити заголовок (code;price) і хоча б один рядок");
        if (table.Count - 1 > MaxPriceImportRows) throw new ValidationException($"У файлі {table.Count - 1} рядків — це більше за ліміт {MaxPriceImportRows}. Розділіть файл на частини.");
        var header = table[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int ci = header.IndexOf("code"), pi = header.IndexOf("price");
        if (ci < 0 || pi < 0) throw new ValidationException("У заголовку мають бути колонки code і price");
        var profiles = await _db.Profiles.AsNoTracking().Where(p => p.RecordState != RecordStates.Deleted).Select(p => new { p.Id, p.Code }).ToListAsync();
        var tests = await _db.Tests.AsNoTracking().Where(t => t.RecordState != RecordStates.Deleted).Select(t => new { t.Id, t.Code }).ToListAsync();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var report = new List<object>(); int create = 0, update = 0, skip = 0, error = 0;
        for (var r = 1; r < table.Count; r++)
        {
            var line = r + 1; var row = table[r];
            var code = ci < row.Count ? row[ci].Trim() : ""; var raw = pi < row.Count ? row[pi].Trim() : "";
            if (code.Length == 0) { skip++; report.Add(new { line, key = (string?)null, action = "SKIP", reason = "Не вказано код послуги" }); continue; }
            if (!seen.Add(code)) { skip++; report.Add(new { line, key = code, action = "SKIP", reason = "Дубль у файлі: код уже зустрічався вище" }); continue; }
            if (!decimal.TryParse(raw.Replace(" ", "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price < 0)
            { error++; report.Add(new { line, key = code, action = "ERROR", reason = $"Некоректна ціна «{raw}»: потрібне число не менше 0" }); continue; }
            var prof = profiles.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
            var test = prof == null ? tests.FirstOrDefault(t => string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase)) : null;
            if (prof == null && test == null) { error++; report.Add(new { line, key = code, action = "ERROR", reason = "Послугу або показник із таким кодом не знайдено" }); continue; }
            var item = list.Items.FirstOrDefault(i => prof != null ? i.ProfileId == prof.Id : i.TestId == test!.Id);
            if (item == null)
            {
                create++; report.Add(new { line, key = code, action = "CREATE", reason = (string?)null });
                if (!dryRun) { var n = new LabPriceListItem { PriceListId = list.Id, ProfileId = prof?.Id, TestId = test?.Id, Price = price }; list.Items.Add(n); _db.PriceListItems.Add(n); }
            }
            else if (item.Price == price) { skip++; report.Add(new { line, key = code, action = "SKIP", reason = "Ціна не змінилась" }); }
            else
            {
                update++; report.Add(new { line, key = code, action = "UPDATE", reason = $"{item.Price:0.##} → {price:0.##}" });
                if (!dryRun) item.Price = price;
            }
        }
        if (!dryRun && (create > 0 || update > 0))
        {
            _audit.Log("PRICE_IMPORT", "lab_price_list", list.Id, null, new { list.Code, create, update });
            await _db.SaveChangesAsync();
        }
        return new { dryRun, limit = MaxPriceImportRows, total = table.Count - 1, create, update, skip, error, created = dryRun ? 0 : create, updated = dryRun ? 0 : update, skipped = skip, errors = error, rows = report };
    }
}
