// Нумератори та лічильники: номер замовлення {yyMM}-{000000}, лічильник штрихкодів lab_tube_barcode_ean8
using System.Globalization;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Core.Barcodes;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public interface INumeratorService
{
    Task<string> NextOrderNumberAsync(DateTime at, CancellationToken ct = default);
    Task<long> NextCounterAsync(string counterCode, CancellationToken ct = default);
    Task<string> NextTubeBarcodeAsync(CancellationToken ct = default);
    Task<string> NextManifestNumberAsync(DateTime at, CancellationToken ct = default);
    Task<string> NextBatchCodeAsync(DateTime at, CancellationToken ct = default);
    static string FormatMask(string mask, DateTime at, long value) => NumeratorService.ApplyMask(mask, at, value);
}

public sealed class NumeratorService : INumeratorService
{
    public const string OrderNumerator = "lab_order_number";
    public const string ManifestNumerator = "lab_manifest_number";
    public const string BatchNumerator = "lab_batch_code";

    private readonly LisDbContext _db;
    public NumeratorService(LisDbContext db) => _db = db;

    public Task<string> NextOrderNumberAsync(DateTime at, CancellationToken ct = default) => NextFormattedAsync(OrderNumerator, "{yyMM}-{000000}", at, ct);
    public Task<string> NextManifestNumberAsync(DateTime at, CancellationToken ct = default) => NextFormattedAsync(ManifestNumerator, "MAN-{yyMMdd}-{0000}", at, ct);
    public Task<string> NextBatchCodeAsync(DateTime at, CancellationToken ct = default) => NextFormattedAsync(BatchNumerator, "WL-{yyMMdd}-{000}", at, ct);

    private async Task<string> NextFormattedAsync(string code, string defaultMask, DateTime at, CancellationToken ct)
    {
        var numerator = await _db.Numerators.FirstOrDefaultAsync(n => n.Code == code, ct);
        if (numerator == null)
        {
            numerator = new LabNumerator { Code = code, Name = code, Mask = defaultMask, CurrentValue = 0, ResetByPeriod = true };
            _db.Numerators.Add(numerator);
            await _db.SaveChangesAsync(ct);
        }
        var periodKey = PeriodKey(numerator.Mask, at);
        if (numerator.ResetByPeriod && numerator.PeriodKey != periodKey)
        {
            // Атомарне скидання періоду
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE lab_numerator SET current_value = 0, period_key = {periodKey} WHERE code = {code} AND (period_key IS NULL OR period_key <> {periodKey})", ct);
        }
        // Атомарний інкремент (SQLite серіалізує записи; у PostgreSQL — рядкове блокування)
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE lab_numerator SET current_value = current_value + 1, modified_on = {DateTime.UtcNow} WHERE code = {code}", ct);
        var value = await _db.Numerators.AsNoTracking().Where(n => n.Code == code).Select(n => n.CurrentValue).FirstAsync(ct);
        _db.Entry(numerator).State = EntityState.Detached;
        return ApplyMask(numerator.Mask, at, value);
    }

    public async Task<long> NextCounterAsync(string counterCode, CancellationToken ct = default)
    {
        var exists = await _db.Counters.AsNoTracking().AnyAsync(c => c.CounterCode == counterCode, ct);
        if (!exists)
        {
            _db.Counters.Add(new LabCounter { CounterCode = counterCode, CounterValue = 0 });
            await _db.SaveChangesAsync(ct);
        }
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE lab_counter SET counter_value = counter_value + 1 WHERE counter_code = {counterCode}", ct);
        return await _db.Counters.AsNoTracking().Where(c => c.CounterCode == counterCode).Select(c => c.CounterValue).FirstAsync(ct);
    }

    public async Task<string> NextTubeBarcodeAsync(CancellationToken ct = default)
    {
        // Повтор на випадок колізії унікального індексу (напр. після відновлення БД)
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var counter = await NextCounterAsync(TubeBarcodeGenerator.CounterCode, ct);
            var barcode = TubeBarcodeGenerator.Generate(counter);
            if (!await _db.Samples.AsNoTracking().AnyAsync(s => s.Barcode == barcode, ct)) return barcode;
        }
        throw new InvalidOperationException("Не вдалося згенерувати унікальний штрихкод пробірки");
    }

    /// <summary>Маска: {yyMM}, {yyMMdd}, {yyyy} → дата; {000000} → значення з доповненням нулями.</summary>
    public static string ApplyMask(string mask, DateTime at, long value)
    {
        var result = mask;
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\{(y+M*d*)\}", m => at.ToString(m.Groups[1].Value, CultureInfo.InvariantCulture));
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\{(0+)\}", m => value.ToString(new string('0', m.Groups[1].Value.Length), CultureInfo.InvariantCulture));
        return result;
    }

    private static string PeriodKey(string mask, DateTime at)
    {
        var m = System.Text.RegularExpressions.Regex.Match(mask, @"\{(y+M*d*)\}");
        return m.Success ? at.ToString(m.Groups[1].Value, CultureInfo.InvariantCulture) : "ALL";
    }
}
