// Імпорт результатів із CSV/XLSX/XML (barcode, testCode, value, unit) з попереднім переглядом dryRun
using System.Globalization;
using System.Xml.Linq;
using ClosedXML.Excel;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class ImportRow
{
    public int Line { get; set; }
    public string Barcode { get; set; } = "";
    public string TestCode { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Unit { get; set; }
    public string? Flags { get; set; }
    public string? OrderTestId { get; set; }
    public string? OrderNumber { get; set; }
    public string? PatientName { get; set; }
    public string Status { get; set; } = "READY";
    public string? Message { get; set; }
}

public sealed class ImportService
{
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly ResultPipelineService _pipeline;

    public ImportService(LisDbContext db, IRolePolicy policy, IAuditService audit, ResultPipelineService pipeline)
    {
        _db = db; _policy = policy; _audit = audit; _pipeline = pipeline;
    }

    public async Task<object> ImportAsync(Stream stream, string fileName, bool dryRun, string? analyzerId)
    {
        _policy.Require("Імпорт результатів", LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var rows = ext switch
        {
            ".xlsx" => ParseXlsx(stream),
            ".xml" => ParseXml(stream),
            _ => ParseCsv(stream)
        };
        if (rows.Count == 0) throw new ValidationException("Файл не містить рядків із даними (очікуються колонки barcode, testCode, value[, unit, flags])");

        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.Barcode) || string.IsNullOrWhiteSpace(r.TestCode) || string.IsNullOrWhiteSpace(r.Value)) { r.Status = "ERROR"; r.Message = "Порожні обов'язкові поля"; continue; }
            var sample = await _db.Samples.AsNoTracking().Include(s => s.Order).ThenInclude(o => o!.Patient).FirstOrDefaultAsync(s => s.Barcode == r.Barcode);
            if (sample == null) { r.Status = "ERROR"; r.Message = "Штрихкод не знайдено"; continue; }
            r.OrderNumber = sample.Order?.OrderNumber; r.PatientName = sample.Order?.Patient?.FullName;
            var test = await _db.OrderTests.AsNoTracking().Where(t => t.OrderId == sample.OrderId && t.TestCode == r.TestCode.ToUpper() && t.Status != OrderTestStatuses.Rejected)
                .OrderBy(t => t.SampleId == sample.Id ? 0 : 1).FirstOrDefaultAsync();
            if (test == null) { r.Status = "ERROR"; r.Message = $"У замовленні немає тесту {r.TestCode}"; continue; }
            if (OrderTestStatuses.VerifiedAny.Contains(test.Status)) { r.Status = "SKIP"; r.Message = "Тест уже верифіковано"; continue; }
            r.OrderTestId = test.Id;
            r.Status = test.Status is OrderTestStatuses.Resulted or OrderTestStatuses.NeedsReview ? "OVERWRITE" : "READY";
        }

        if (dryRun)
            return new { dryRun = true, fileName, total = rows.Count, ready = rows.Count(r => r.Status is "READY" or "OVERWRITE"), errors = rows.Count(r => r.Status == "ERROR"), skipped = rows.Count(r => r.Status == "SKIP"), rows };

        var applied = 0;
        foreach (var r in rows.Where(r => r.Status is "READY" or "OVERWRITE"))
        {
            try
            {
                double? numeric = double.TryParse(r.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
                await _pipeline.ApplyAsync(new ResultEntry { OrderTestId = r.OrderTestId!, NumericValue = numeric, StringValue = numeric.HasValue ? null : r.Value, Unit = r.Unit, AnalyzerFlags = r.Flags, AnalyzerId = analyzerId, Comment = $"Імпорт із файлу {fileName}" });
                r.Status = "APPLIED"; applied++;
            }
            catch (LisException ex) { r.Status = "ERROR"; r.Message = ex.Message; }
        }
        _audit.Log("IMPORT", "lab_test_result", null, null, new { fileName, total = rows.Count, applied });
        await _db.SaveChangesAsync();
        return new { dryRun = false, fileName, total = rows.Count, applied, errors = rows.Count(r => r.Status == "ERROR"), skipped = rows.Count(r => r.Status == "SKIP"), rows };
    }

    private static List<ImportRow> ParseCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line) lines.Add(line);
        if (lines.Count == 0) return new();
        var sep = lines[0].Contains(';') ? ';' : lines[0].Contains('\t') ? '\t' : ',';
        var header = lines[0].Split(sep).Select(h => h.Trim().Trim('"').ToLowerInvariant()).ToList();
        int Idx(params string[] names) => header.FindIndex(h => names.Contains(h));
        var iB = Idx("barcode", "штрихкод", "sample"); var iT = Idx("testcode", "test_code", "test", "code", "тест"); var iV = Idx("value", "result", "значення", "результат"); var iU = Idx("unit", "од", "одиниці"); var iF = Idx("flags", "flag");
        if (iB < 0 || iT < 0 || iV < 0) { iB = 0; iT = 1; iV = 2; iU = header.Count > 3 ? 3 : -1; }
        var hasHeader = !(header.Count > 2 && header[0].All(char.IsDigit));
        var rows = new List<ImportRow>();
        for (var i = hasHeader ? 1 : 0; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var f = lines[i].Split(sep).Select(x => x.Trim().Trim('"')).ToArray();
            string G(int idx) => idx >= 0 && idx < f.Length ? f[idx] : "";
            rows.Add(new ImportRow { Line = i + 1, Barcode = G(iB), TestCode = G(iT), Value = G(iV), Unit = iU >= 0 ? G(iU) : null, Flags = iF >= 0 ? G(iF) : null });
        }
        return rows;
    }

    private static List<ImportRow> ParseXlsx(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var rows = new List<ImportRow>();
        var header = ws.Row(1).CellsUsed().ToDictionary(c => c.GetString().Trim().ToLowerInvariant(), c => c.Address.ColumnNumber);
        int Col(params string[] names) => names.Select(n => header.TryGetValue(n, out var c) ? c : 0).FirstOrDefault(c => c > 0);
        var cB = Col("barcode", "штрихкод"); var cT = Col("testcode", "test_code", "test", "тест"); var cV = Col("value", "result", "значення"); var cU = Col("unit", "од"); var cF = Col("flags", "flag");
        if (cB == 0) { cB = 1; cT = 2; cV = 3; cU = 4; }
        foreach (var row in ws.RowsUsed().Skip(1))
            rows.Add(new ImportRow { Line = row.RowNumber(), Barcode = row.Cell(cB).GetString().Trim(), TestCode = row.Cell(cT).GetString().Trim(), Value = row.Cell(cV).GetString().Trim(), Unit = cU > 0 ? row.Cell(cU).GetString() : null, Flags = cF > 0 ? row.Cell(cF).GetString() : null });
        return rows;
    }

    private static List<ImportRow> ParseXml(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var rows = new List<ImportRow>();
        var i = 0;
        foreach (var el in doc.Descendants().Where(e => e.Name.LocalName.Equals("result", StringComparison.OrdinalIgnoreCase) || e.Name.LocalName.Equals("row", StringComparison.OrdinalIgnoreCase)))
        {
            string? V(params string[] names) => names.Select(n => (string?)el.Attribute(n) ?? el.Elements().FirstOrDefault(x => x.Name.LocalName.Equals(n, StringComparison.OrdinalIgnoreCase))?.Value).FirstOrDefault(v => v != null);
            rows.Add(new ImportRow { Line = ++i, Barcode = V("barcode") ?? "", TestCode = V("testCode", "test") ?? "", Value = V("value", "result") ?? "", Unit = V("unit"), Flags = V("flags", "flag") });
        }
        return rows;
    }
}
