// ТЗ 23.5 (FR-WRK): робота за направленнями — розрізи «аналізатор», «пацієнт», «дослідження» тих самих lab_order_tests;
// ТЗ 23.6 (FR-RPT): звіти лабораторії — кумулятивний пацієнта, навантаження, джерела, використання показників, TAT, розподіл,
// невиконані, пакети; журнал формування lab_report_run, денні агрегати lab_stat_daily, експорт XLSX (числові клітинки) і PDF.
using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Core.Clinical;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MedLink.LIS.Api.Services;

/// <summary>Спільні фільтри розрізів (FR-WRK-001) і звітів (23.6).</summary>
public sealed class WorkFilter
{
    /// <summary>INPATIENT | POLYCLINIC | EHEALTH | PARTNER | OTHER</summary>
    public string? Source { get; set; }
    public bool PackagesOnly { get; set; }
    public string? Department { get; set; }
    public bool? Cito { get; set; }
    /// <summary>active (типово) | all</summary>
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Q { get; set; }
    public string? AnalyzerId { get; set; }
}

/// <summary>Табличний звіт: колонки (числові — у XLSX числами) і рядки.</summary>
public sealed class ReportTable
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public List<ReportColumn> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public Dictionary<string, object?>? Totals { get; set; }
    public string? Note { get; set; }
}

public sealed record ReportColumn(string Key, string Label, bool Numeric = false);

public sealed class WorkReportsService
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly string[] Active = { OrderTestStatuses.Pending, OrderTestStatuses.Rerun, OrderTestStatuses.InAnalysis, OrderTestStatuses.Resulted, OrderTestStatuses.NeedsReview };

    private readonly LisDbContext _db;
    private readonly RoutingService _routing;
    private readonly ICurrentEmployee _current;
    private readonly IAuditService _audit;

    public WorkReportsService(LisDbContext db, RoutingService routing, ICurrentEmployee current, IAuditService audit) { _db = db; _routing = routing; _current = current; _audit = audit; }

    /// <summary>Статус тесту в розрізах (FR-WRK-006): queue | run | res | ver | none | rej.</summary>
    public static string ViewStatus(LabOrderTest t) =>
        t.Status == OrderTestStatuses.Rejected ? "rej"
        : OrderTestStatuses.VerifiedAny.Contains(t.Status) || t.ReleasedAt != null ? "ver"
        : t.Status is OrderTestStatuses.Resulted or OrderTestStatuses.NeedsReview ? "res"
        : t.Status == OrderTestStatuses.InAnalysis ? "run"
        : t.RoutingStatus == RoutingStatuses.NoEquipment && t.AssignedAnalyzerId == null ? "none" : "queue";

    public static readonly Dictionary<string, string> ViewStatusLabels = new()
    {
        ["queue"] = "В черзі", ["run"] = "В аналізі", ["res"] = "Результат · очікує верифікації", ["ver"] = "Верифіковано", ["none"] = "Без обладнання", ["rej"] = "Відхилено"
    };

    private IQueryable<LabOrderTest> Base(WorkFilter f)
    {
        var q = _db.OrderTests.AsNoTracking()
            .Include(t => t.Order).ThenInclude(o => o!.Patient).Include(t => t.Order).ThenInclude(o => o!.Department).Include(t => t.Order).ThenInclude(o => o!.Items)
            .Include(t => t.Sample).Include(t => t.AssignedAnalyzer).Include(t => t.Result).ThenInclude(r => r!.Analyzer).Include(t => t.Profile)
            .Where(t => t.Order!.Status != OrderStatuses.Cancelled && t.Order.LabDepartment == null);
        if (f.Status != "all") q = q.Where(t => Active.Contains(t.Status) && t.ReleasedAt == null);
        if (f.From.HasValue) q = q.Where(t => t.Order!.OrderDatetime >= f.From.Value);
        if (f.To.HasValue) q = q.Where(t => t.Order!.OrderDatetime <= f.To.Value);
        if (f.Cito.HasValue) q = q.Where(t => t.Order!.IsUrgentCito == f.Cito.Value);
        if (f.PackagesOnly) q = q.Where(t => t.Order!.PackageCode != null);
        if (!string.IsNullOrWhiteSpace(f.Department)) q = q.Where(t => t.Order!.RequestingDepartmentName == f.Department || t.Order.Department!.Caption == f.Department);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var s = f.Q.Trim();
            q = q.Where(t => t.Order!.OrderNumber.Contains(s) || t.TestCode == s.ToUpper() || (t.Sample != null && t.Sample.Barcode == s) || (t.Order.Patient!.Caption != null && t.Order.Patient.Caption.Contains(s)));
        }
        return q;
    }

    private async Task<List<LabOrderTest>> LoadAsync(WorkFilter f)
    {
        var list = await Base(f).ToListAsync();
        if (!string.IsNullOrWhiteSpace(f.Source)) list = list.Where(t => ReferralSources.Of(t.Order!) == f.Source.ToUpperInvariant()).ToList();
        return list;
    }

    private static string? ServiceOf(LabOrderTest t) => t.Order!.Items.FirstOrDefault(i => i.Id == t.OrderItemId)?.InterventionCode;
    private static string ServiceName(LabOrderTest t) => t.Order!.Items.FirstOrDefault(i => i.Id == t.OrderItemId)?.InterventionName ?? t.Profile?.Name ?? t.TestName;
    private static string? Performer(LabOrderTest t) => t.Result?.AnalyzerId ?? t.AssignedAnalyzerId;

    private static object Row(LabOrderTest t, int? position = null) => new
    {
        position, orderTestId = t.Id, t.OrderId, orderNumber = t.Order!.OrderNumber, barcode = t.Sample?.Barcode, patientId = t.Order.PatientId, patientName = t.Order.Patient?.Caption,
        source = ReferralSources.Of(t.Order), sourceLabel = ReferralSources.Label(ReferralSources.Of(t.Order)), packageCode = t.Order.PackageCode, department = t.Order.RequestingDepartmentName ?? t.Order.Department?.Caption,
        bed = RoutingService.Bed(t.Order), service = ServiceName(t), serviceCode = ServiceOf(t), t.TestCode, t.TestName, t.Status, view = ViewStatus(t), viewLabel = ViewStatusLabels[ViewStatus(t)],
        analyzerId = Performer(t), analyzer = t.Result?.Analyzer?.Name ?? t.AssignedAnalyzer?.Name, t.RoutedBy, manual = t.RoutedBy == "MANUAL", isCito = t.Order.IsUrgentCito,
        value = t.Result == null ? null : DtoMapper.FormatValue(t.Result.NumericValue, t.Result.StringValue, 2), flag = t.Result?.Flag
    };

    // ------------------------------------------------------------------ розрізи
    public async Task<object> SummaryAsync(WorkFilter f)
    {
        var tests = await LoadAsync(f);
        var s = await _routing.SnapshotAsync();
        var testNames = (await _db.Tests.AsNoTracking().Select(t => new { t.Code, t.Name }).ToListAsync()).GroupBy(t => t.Code).ToDictionary(g => g.Key, g => g.First().Name);
        return new
        {
            analyzers = s.Analyzers.Values.OrderBy(a => a.Kind == AnalyzerKinds.Manual).ThenBy(a => a.Location).ThenBy(a => a.Name).Select(a =>
            {
                var mine = tests.Where(t => Performer(t) == a.Id).ToList();
                return new
                {
                    a.Id, a.Name, a.Kind, a.Location, a.IsOnline, longOffline = s.IsLongOffline(a), a.ServiceStatus, a.AvgTatMinutes, lastError = a.LastError,
                    lockouts = s.Lockouts.Where(l => l.AnalyzerId == a.Id).Select(l => l.TestCode).ToList(),
                    lockoutNames = s.Lockouts.Where(l => l.AnalyzerId == a.Id).Select(l => l.TestCode == null ? "усі показники" : testNames.TryGetValue(l.TestCode, out var tn) && !string.IsNullOrWhiteSpace(tn) ? $"{tn} ({l.TestCode})" : l.TestCode).ToList(),
                    queue = mine.Count(t => ViewStatus(t) == "queue"), inAnalysis = mine.Count(t => ViewStatus(t) == "run"), results = mine.Count(t => ViewStatus(t) == "res"),
                    cito = mine.Count(t => t.Order!.IsUrgentCito && ViewStatus(t) is "queue" or "run")
                };
            }),
            noEquipment = tests.Count(t => ViewStatus(t) == "none"),
            patients = tests.Select(t => t.Order!.PatientId).Distinct().Count(),
            services = tests.Select(t => ServiceOf(t) ?? t.Profile?.Code).Distinct().Count(),
            total = tests.Count
        };
    }

    public async Task<object> ByAnalyzerAsync(string analyzerId, WorkFilter f)
    {
        var tests = await LoadAsync(f);
        var mine = analyzerId == "none" ? tests.Where(t => ViewStatus(t) == "none").ToList() : tests.Where(t => Performer(t) == analyzerId).ToList();
        var ordered = mine.OrderBy(t => ViewStatus(t) switch { "run" => 0, "queue" => 1, "res" => 2, _ => 3 }).ThenByDescending(t => t.Order!.IsUrgentCito)
            .ThenBy(t => t.Order!.PlannedCollectionAt ?? t.Order.OrderDatetime).ThenBy(t => t.Sample?.ReceivedAt).ThenBy(t => t.DisplayOrder).ToList();
        var a = analyzerId == "none" ? null : await _db.Analyzers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == analyzerId);
        return new { analyzerId, analyzer = a?.Name ?? "Без обладнання", a?.Kind, a?.Location, total = ordered.Count, items = ordered.Select((t, i) => Row(t, i + 1)) };
    }

    public async Task<object> PatientsAsync(WorkFilter f)
    {
        var tests = await LoadAsync(f);
        return tests.GroupBy(t => t.Order!.PatientId).Select(g => new
        {
            patientId = g.Key, patientName = g.First().Order!.Patient?.Caption, orders = g.Select(t => t.OrderId).Distinct().Count(), total = g.Count(), done = g.Count(t => ViewStatus(t) == "ver"),
            cito = g.Any(t => t.Order!.IsUrgentCito), sources = g.Select(t => ReferralSources.Of(t.Order!)).Distinct(), department = g.Select(t => t.Order!.RequestingDepartmentName).FirstOrDefault(d => d != null)
        }).OrderByDescending(x => x.cito).ThenBy(x => x.patientName);
    }

    public async Task<object> ByPatientAsync(string patientId, WorkFilter f)
    {
        f.Status ??= "all";
        var tests = (await LoadAsync(f)).Where(t => t.Order!.PatientId == patientId).ToList();
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId) ?? throw NotFoundException.For("Пацієнт", patientId);
        return new
        {
            patientId, patientName = patient.Caption, ageGender = DtoMapper.AgeGender(patient, DateTime.UtcNow),
            orders = tests.GroupBy(t => t.OrderId).Select(g => g.First().Order!).OrderByDescending(o => o.OrderDatetime).Select(o => new
            {
                orderId = o.Id, o.OrderNumber, o.OrderDatetime, source = ReferralSources.Of(o), sourceLabel = ReferralSources.Label(ReferralSources.Of(o)), o.PackageCode, o.PackageName, isCito = o.IsUrgentCito,
                department = o.RequestingDepartmentName ?? o.Department?.Caption, bed = RoutingService.Bed(o), o.Status,
                services = tests.Where(t => t.OrderId == o.Id).GroupBy(ServiceName).Select(sg => new
                {
                    service = sg.Key, done = sg.Count(t => ViewStatus(t) == "ver"), total = sg.Count(),
                    tests = sg.OrderBy(t => t.DisplayOrder).Select(t => Row(t))
                })
            })
        };
    }

    public async Task<object> ServicesAsync(WorkFilter f)
    {
        var tests = await LoadAsync(f);
        return tests.GroupBy(t => ServiceOf(t) ?? t.Profile?.Code ?? t.TestCode).Select(g => new
        {
            serviceCode = g.Key, service = ServiceName(g.First()), referrals = g.Select(t => t.OrderId).Distinct().Count(), indicators = g.Count(),
            done = g.Count(t => ViewStatus(t) == "ver"), noEquipment = g.Count(t => ViewStatus(t) == "none"),
            byAnalyzer = g.GroupBy(t => t.Result?.Analyzer?.Name ?? t.AssignedAnalyzer?.Name ?? "—").Select(x => new { analyzer = x.Key, count = x.Count() })
        }).OrderByDescending(x => x.indicators);
    }

    /// <summary>Матриця «замовлення × показник» (FR-WRK-005): прилад і статус; «·» — показник не обраний лікарем.</summary>
    public async Task<object> ByServiceAsync(string serviceCode, WorkFilter f)
    {
        var tests = (await LoadAsync(f)).Where(t => (ServiceOf(t) ?? t.Profile?.Code ?? t.TestCode) == serviceCode).ToList();
        var orderIds = tests.Select(t => t.OrderId).Distinct().ToList();
        var items = await _db.OrderItems.AsNoTracking().Include(i => i.Indicators).Where(i => orderIds.Contains(i.OrderId) && i.InterventionCode == serviceCode).ToListAsync();
        var columns = items.SelectMany(i => i.Indicators.Select(x => x.TestCode)).Concat(tests.Select(t => t.TestCode)).Distinct().ToList();
        return new
        {
            serviceCode, service = tests.Select(ServiceName).FirstOrDefault() ?? serviceCode, columns,
            rows = tests.GroupBy(t => t.OrderId).Select(g =>
            {
                var o = g.First().Order!;
                var item = items.FirstOrDefault(i => i.OrderId == o.Id);
                return new
                {
                    orderId = o.Id, o.OrderNumber, patientName = o.Patient?.Caption, source = ReferralSources.Of(o), isCito = o.IsUrgentCito,
                    cells = columns.ToDictionary(c => c, c =>
                    {
                        var t = g.FirstOrDefault(x => x.TestCode == c);
                        if (t == null) return (object)new { notOrdered = item?.Indicators.Any(x => x.TestCode == c && !x.IsSelected) ?? true };
                        return new { notOrdered = false, analyzer = t.Result?.Analyzer?.Name ?? t.AssignedAnalyzer?.Name, view = ViewStatus(t), t.Status };
                    })
                };
            })
        };
    }

    /// <summary>Експорт поточного розрізу в XLSX (FR-WRK-008).</summary>
    public async Task<byte[]> ExportAsync(string view, string? key, WorkFilter f)
    {
        var tests = await LoadAsync(f);
        if (view == "analyzer" && key != null) tests = key == "none" ? tests.Where(t => ViewStatus(t) == "none").ToList() : tests.Where(t => Performer(t) == key).ToList();
        if (view == "patient" && key != null) tests = tests.Where(t => t.Order!.PatientId == key).ToList();
        if (view == "service" && key != null) tests = tests.Where(t => (ServiceOf(t) ?? t.Profile?.Code ?? t.TestCode) == key).ToList();
        var table = new ReportTable
        {
            Title = "Робота за направленнями",
            Columns = new() { new("order", "Замовлення"), new("barcode", "Штрихкод"), new("patient", "Пацієнт"), new("source", "Джерело"), new("package", "Пакет"), new("department", "Відділення"),
                new("service", "Дослідження"), new("test", "Показник"), new("analyzer", "Прилад"), new("status", "Статус"), new("cito", "Терміново") },
            Rows = tests.Select(t => new Dictionary<string, object?>
            {
                ["order"] = t.Order!.OrderNumber, ["barcode"] = t.Sample?.Barcode, ["patient"] = t.Order.Patient?.Caption, ["source"] = ReferralSources.Label(ReferralSources.Of(t.Order)),
                ["package"] = t.Order.PackageCode, ["department"] = t.Order.RequestingDepartmentName ?? t.Order.Department?.Caption, ["service"] = ServiceName(t), ["test"] = t.TestCode,
                ["analyzer"] = t.Result?.Analyzer?.Name ?? t.AssignedAnalyzer?.Name, ["status"] = ViewStatusLabels[ViewStatus(t)], ["cito"] = t.Order.IsUrgentCito ? "так" : ""
            }).ToList()
        };
        return Xlsx(table);
    }

    // ------------------------------------------------------------------ звіти (23.6)
    public static readonly (string Type, string Title, string Release)[] Catalog =
    {
        ("cumulative", "Кумулятивний звіт пацієнта", "R1.2"), ("load", "Навантаження аналізаторів", "R1.2"), ("sources", "Направлення за джерелами", "R1.2"),
        ("indicators", "Використання показників у послугах", "R1.2"), ("tat", "Терміни виконання за дослідженнями й аналізаторами", "R1.2"), ("distribution", "Розподіл і перенаправлення", "R1.2"),
        ("unperformed", "Невиконані показники", "R1.2"), ("packages", "Пакети послуг", "R1.2")
    };

    // Короткі описи звітів для списку (другий рядок під назвою)
    private static readonly Dictionary<string, string> Descriptions = new()
    {
        ["cumulative"] = "Динаміка результатів пацієнта в одній таблиці", ["load"] = "Скільки тестів виконав кожен аналізатор і скільки в черзі",
        ["sources"] = "Направлення за джерелами: стаціонар, поліклініка, ЕСОЗ, партнери", ["indicators"] = "Які показники входять до замовлених послуг",
        ["tat"] = "Час від приймання проби до результату", ["distribution"] = "Куди направлено тести і які перенаправлено вручну",
        ["unperformed"] = "Показники, що ще не виконано", ["packages"] = "Склад і виконання пакетів послуг"
    };

    public object List() => Catalog.Select(c => new { type = c.Type, title = c.Title, description = Descriptions.GetValueOrDefault(c.Type, ""), release = c.Release });

    private async Task LogRunAsync(string type, object parameters, string format, int rows, string? patientId = null)
    {
        _db.ReportRuns.Add(new LabReportRun { ReportType = type, ParamsJson = JsonSerializer.Serialize(parameters, JsonDefaults.Options), PatientId = patientId, RunBy = _current.EmployeeId, Format = format, Rows = rows });
        if (patientId != null) _audit.Log("REPORT_ACCESS", "mis_patient_card", patientId, null, new { type, format }); // журнал доступу до ПД (FR-GAP-005)
        await _db.SaveChangesAsync();
    }

    private static double? Median(IEnumerable<double> xs) => Percentile(xs, 0.5);
    private static double? Percentile(IEnumerable<double> xs, double p)
    {
        var a = xs.OrderBy(x => x).ToArray();
        if (a.Length == 0) return null;
        var idx = (int)Math.Ceiling(p * a.Length) - 1;
        return Math.Round(a[Math.Clamp(idx, 0, a.Length - 1)], 1);
    }
    private static double? TatMinutes(LabOrderTest t)
    {
        var start = t.Sample?.ReceivedAt ?? t.Sample?.CollectedAt;
        var end = t.Result?.EnteredAt;
        return start.HasValue && end.HasValue && end > start ? (end.Value - start.Value).TotalMinutes : null;
    }

    public async Task<ReportTable> BuildAsync(string type, WorkFilter f, string format = "json")
    {
        f.Status ??= "all";
        f.From ??= DateTime.UtcNow.Date.AddDays(-30);
        if (f.To.HasValue && f.To.Value.TimeOfDay == TimeSpan.Zero) f.To = f.To.Value.Date.AddDays(1).AddTicks(-1); // «До» — дата без часу: включаємо весь день
        f.To ??= DateTime.UtcNow;
        var tests = await LoadAsync(f);
        if (!string.IsNullOrWhiteSpace(f.AnalyzerId)) tests = tests.Where(t => Performer(t) == f.AnalyzerId).ToList();
        var analyzers = await _db.Analyzers.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Name);
        ReportTable table = type switch
        {
            "load" => await LoadReportAsync(tests, analyzers, f),
            "sources" => SourcesReport(tests),
            "indicators" => await IndicatorsReportAsync(tests),
            "tat" => TatReport(tests, analyzers),
            "distribution" => await DistributionReportAsync(tests, analyzers, f),
            "unperformed" => UnperformedReport(tests),
            "packages" => PackagesReport(tests),
            _ => throw new NotFoundException($"Звіт '{type}' не знайдено")
        };
        table.Type = type;
        table.Title = Catalog.First(c => c.Type == type).Title + $" · {f.From:dd.MM.yyyy}–{f.To:dd.MM.yyyy}";
        await LogRunAsync(type, f, format, table.Rows.Count);
        return table;
    }

    /// <summary>Години блокування аналізатора в межах періоду звіту (активні блокування рахуються до кінця періоду/зараз).</summary>
    internal static double LockoutHours(IEnumerable<LabAnalyzerLockout> lockouts, string analyzerId, WorkFilter f)
    {
        var now = DateTime.UtcNow;
        var periodEnd = f.To.HasValue && f.To.Value < now ? f.To.Value : now;
        var periodStart = f.From ?? DateTime.MinValue;
        double total = 0;
        foreach (var l in lockouts.Where(x => x.AnalyzerId == analyzerId))
        {
            var from = l.StartedAt > periodStart ? l.StartedAt : periodStart;
            var to = l.ResolvedAt.HasValue && l.ResolvedAt.Value < periodEnd ? l.ResolvedAt.Value : periodEnd;
            if (to > from) total += (to - from).TotalHours;
        }
        return Math.Round(total, 1);
    }

    private async Task<ReportTable> LoadReportAsync(List<LabOrderTest> tests, Dictionary<string, string> analyzers, WorkFilter f)
    {
        var lockouts = await _db.Lockouts.AsNoTracking().Where(l => l.StartedAt <= f.To && (l.ResolvedAt == null || l.ResolvedAt >= f.From)).ToListAsync();
        var queue = (await _routing.SnapshotAsync()).Queue;
        var rows = tests.Where(t => Performer(t) != null && t.Status != OrderTestStatuses.Rejected).GroupBy(t => Performer(t)!).Select(g => new Dictionary<string, object?>
        {
            ["analyzer"] = analyzers.GetValueOrDefault(g.Key, g.Key), ["tests"] = g.Count(), ["cito"] = g.Count(t => t.Order!.IsUrgentCito),
            ["inpatient"] = g.Count(t => ReferralSources.Of(t.Order!) == ReferralSources.Inpatient), ["polyclinic"] = g.Count(t => ReferralSources.Of(t.Order!) == ReferralSources.Polyclinic),
            ["ehealth"] = g.Count(t => ReferralSources.Of(t.Order!) == ReferralSources.Ehealth), ["reruns"] = g.Count(t => (t.Result?.Version ?? 1) > 1 || t.Status == OrderTestStatuses.Rerun),
            ["manual"] = g.Count(t => t.RoutedBy == "MANUAL"), ["tatMedian"] = Median(g.Select(TatMinutes).Where(x => x.HasValue).Select(x => x!.Value)),
            ["lockoutHours"] = LockoutHours(lockouts, g.Key, f),
            ["queueNow"] = queue.GetValueOrDefault(g.Key)
        }).ToList();
        // прилади, заблоковані за період, але без тестів у вибірці, теж мають бути у звіті
        foreach (var aid in lockouts.Select(l => l.AnalyzerId).Distinct())
        {
            if (aid == null || tests.Any(t => Performer(t) == aid)) continue;
            rows.Add(new Dictionary<string, object?>
            {
                ["analyzer"] = analyzers.GetValueOrDefault(aid, aid), ["tests"] = 0, ["cito"] = 0, ["inpatient"] = 0, ["polyclinic"] = 0, ["ehealth"] = 0, ["reruns"] = 0, ["manual"] = 0,
                ["tatMedian"] = null, ["lockoutHours"] = LockoutHours(lockouts, aid, f), ["queueNow"] = queue.GetValueOrDefault(aid)
            });
        }
        rows = rows.OrderByDescending(r => (int)r["tests"]!).ThenByDescending(r => (double)r["lockoutHours"]!).ToList();
        return new ReportTable
        {
            Columns = new() { new("analyzer", "Аналізатор"), new("tests", "Тестів", true), new("cito", "Термінових", true), new("inpatient", "Стаціонар", true), new("polyclinic", "Поліклініка", true),
                new("ehealth", "ЕСОЗ", true), new("reruns", "Повтори", true), new("manual", "Перенаправлено вручну", true), new("tatMedian", "Термін виконання (медіана), хв", true), new("lockoutHours", "Блокування, год", true), new("queueNow", "У черзі зараз", true) },
            Rows = rows, Note = "Тести рахуються за фактичним виконавцем з урахуванням перенаправлень"
        };
    }

    private static ReportTable SourcesReport(List<LabOrderTest> tests)
    {
        var rows = tests.GroupBy(t => ReferralSources.Of(t.Order!)).Select(g =>
        {
            var items = g.SelectMany(t => t.Order!.Items).DistinctBy(i => i.Id).ToList();
            var partial = items.Count(i => i.SelectionMode != SelectionModes.Fixed && i.Status != "NEEDS_SELECTION");
            return new Dictionary<string, object?>
            {
                ["source"] = ReferralSources.Label(g.Key), ["referrals"] = g.Select(t => t.OrderId).Distinct().Count(), ["packages"] = g.Where(t => t.Order!.PackageCode != null).Select(t => t.OrderId).Distinct().Count(),
                ["services"] = items.Count > 0 ? items.Count : g.Select(t => (t.OrderId, t.ProfileId)).Distinct().Count(), ["ordered"] = g.Count(t => t.Status != OrderTestStatuses.Rejected),
                ["performed"] = g.Count(t => ViewStatus(t) == "ver"), ["noIndicators"] = items.Count(i => i.SelectionSource is SelectionSources.Registrar or SelectionSources.LabDoctor),
                ["perService"] = items.Count == 0 ? null : Math.Round((double)g.Count(t => t.OrderItemId != null) / items.Count, 1)
            };
        }).OrderByDescending(r => r["referrals"]).ToList();
        return new ReportTable
        {
            Columns = new() { new("source", "Джерело"), new("referrals", "Направлень", true), new("packages", "З них пакетів", true), new("services", "Послуг", true), new("ordered", "Показників замовлено", true),
                new("performed", "Виконано", true), new("noIndicators", "Без переданих показників", true), new("perService", "Показників на послугу", true) },
            Rows = rows
        };
    }

    private async Task<ReportTable> IndicatorsReportAsync(List<LabOrderTest> tests)
    {
        var orderIds = tests.Select(t => t.OrderId).Distinct().ToList();
        var items = await _db.OrderItems.AsNoTracking().Include(i => i.Indicators).Where(i => orderIds.Contains(i.OrderId) && i.Status != "NEEDS_SELECTION" && i.Status != "UNMAPPED").ToListAsync();
        var analyzers = tests.Where(t => Performer(t) != null).GroupBy(t => t.TestCode).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(t => t.Result?.Analyzer?.Name ?? t.AssignedAnalyzer?.Name).Distinct()));
        var rows = items.GroupBy(i => (i.InterventionCode, i.InterventionName)).SelectMany(g => g.SelectMany(i => i.Indicators).GroupBy(x => x.TestCode).Select(x => new Dictionary<string, object?>
        {
            ["service"] = g.Key.InterventionName, ["indicator"] = x.Key, ["selected"] = x.Count(y => y.IsSelected), ["items"] = g.Count(),
            ["share"] = Math.Round(100.0 * x.Count(y => y.IsSelected) / Math.Max(1, g.Count()), 1), ["required"] = x.Any(y => y.IsRequired) ? "так" : "", ["analyzers"] = analyzers.GetValueOrDefault(x.Key)
        })).OrderBy(r => r["service"]).ThenByDescending(r => r["selected"]).ToList();
        return new ReportTable
        {
            Columns = new() { new("service", "Послуга ЕСОЗ"), new("indicator", "Показник"), new("selected", "Обрано разів", true), new("items", "Позицій послуги", true), new("share", "Частка, %", true), new("required", "Обов'язковий"), new("analyzers", "Прилади") },
            Rows = rows, Note = "Для налаштування типового набору й закупівлі реагентів"
        };
    }

    private static ReportTable TatReport(List<LabOrderTest> tests, Dictionary<string, string> analyzers)
    {
        var rows = tests.Where(t => TatMinutes(t).HasValue).GroupBy(t => (Service: t.Profile?.Name ?? t.TestName, Analyzer: Performer(t))).Select(g =>
        {
            var plan = g.Where(t => !t.Order!.IsUrgentCito).Select(t => TatMinutes(t)!.Value).ToList();
            var cito = g.Where(t => t.Order!.IsUrgentCito).Select(t => TatMinutes(t)!.Value).ToList();
            var norm = (g.First().Profile?.TurnaroundHours ?? 24) * 60.0;
            return new Dictionary<string, object?>
            {
                ["service"] = g.Key.Service, ["analyzer"] = g.Key.Analyzer == null ? "—" : analyzers.GetValueOrDefault(g.Key.Analyzer, g.Key.Analyzer), ["tests"] = g.Count(),
                ["p50"] = Median(plan), ["p90"] = Percentile(plan, 0.9), ["citoP50"] = Median(cito),
                ["inNorm"] = Math.Round(100.0 * g.Count(t => TatMinutes(t) <= (t.Order!.IsUrgentCito ? 60 : norm)) / g.Count(), 1)
            };
        }).OrderBy(r => r["service"]).ToList();
        return new ReportTable
        {
            Columns = new() { new("service", "Дослідження"), new("analyzer", "Аналізатор"), new("tests", "Тестів", true), new("p50", "Медіана (планові), хв", true), new("p90", "90-й процентиль (планові), хв", true),
                new("citoP50", "Медіана термінових, хв", true), new("inNorm", "У нормативі, %", true) },
            Rows = rows, Note = "Час виконання — від приймання проби до результату"
        };
    }

    private async Task<ReportTable> DistributionReportAsync(List<LabOrderTest> tests, Dictionary<string, string> analyzers, WorkFilter f)
    {
        var orders = tests.Select(t => t.Order!).DistinctBy(o => o.Id).Where(o => o.MisReferralId != null).ToList();
        var confirm = orders.Where(o => o.DistributedAt.HasValue).Select(o => (o.DistributedAt!.Value - o.CreatedOn).TotalMinutes).Where(x => x >= 0).ToList();
        var log = await _db.RoutingLog.AsNoTracking().Where(l => l.At >= f.From && l.At <= f.To && (l.Event == RoutingEvents.Change || l.Event == RoutingEvents.Reroute)).ToListAsync();
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();
        var rows = log.GroupBy(l => (l.Event, Reason: l.Reason ?? "—", To: l.AnalyzerId)).Select(g => new Dictionary<string, object?>
        {
            ["event"] = g.Key.Event == RoutingEvents.Change ? "Зміна при розподілі" : "Перенаправлення", ["reason"] = g.Key.Reason,
            ["analyzer"] = g.Key.To == null ? "—" : analyzers.GetValueOrDefault(g.Key.To, g.Key.To), ["count"] = g.Count()
        }).OrderByDescending(r => r["count"]).ToList();
        var routed = tests.Count(t => t.RoutedBy != null);
        return new ReportTable
        {
            Columns = new() { new("event", "Подія"), new("reason", "Причина"), new("analyzer", "На прилад"), new("count", "Кількість", true) },
            Rows = rows,
            Totals = new()
            {
                ["referrals"] = orders.Count, ["confirmMedianMin"] = Median(confirm), ["inTargetPct"] = confirm.Count == 0 ? null : Math.Round(100.0 * confirm.Count(x => x <= settings.DistributionTargetMinutes) / confirm.Count, 1),
                ["manualSharePct"] = routed == 0 ? null : Math.Round(100.0 * tests.Count(t => t.RoutedBy == "MANUAL") / routed, 1), ["targetMinutes"] = settings.DistributionTargetMinutes
            },
            Note = $"Норматив «надходження → підтвердження розподілу» {settings.DistributionTargetMinutes} хв"
        };
    }

    private static ReportTable UnperformedReport(List<LabOrderTest> tests)
    {
        static string Reason(LabOrderTest t) =>
            t.Status != OrderTestStatuses.Rejected ? "Без обладнання"
            : (t.RejectReason ?? "").Contains("скасовано", StringComparison.OrdinalIgnoreCase) ? "Скасування"
            : (t.RejectReason ?? "").Contains("проб", StringComparison.OrdinalIgnoreCase) || (t.RejectReason ?? "").Contains("гемоліз", StringComparison.OrdinalIgnoreCase) ? "Брак проби"
            : (t.RejectReason ?? "").Contains("знято", StringComparison.OrdinalIgnoreCase) ? "Знято лікарем" : "Відхилення";
        var rows = tests.Where(t => t.Status == OrderTestStatuses.Rejected || ViewStatus(t) == "none")
            .GroupBy(t => (Reason: Reason(t), Service: ServiceName(t), Source: ReferralSources.Label(ReferralSources.Of(t.Order!)), Dept: t.Order!.RequestingDepartmentName ?? t.Order.Department?.Caption ?? "—"))
            .Select(g => new Dictionary<string, object?> { ["reason"] = g.Key.Reason, ["service"] = g.Key.Service, ["source"] = g.Key.Source, ["department"] = g.Key.Dept, ["count"] = g.Count(), ["tests"] = string.Join(", ", g.Select(t => t.TestCode).Distinct()) })
            .OrderBy(r => r["reason"]).ThenByDescending(r => r["count"]).ToList();
        return new ReportTable
        {
            Columns = new() { new("reason", "Причина"), new("service", "Послуга"), new("source", "Джерело"), new("department", "Відділення"), new("count", "Показників", true), new("tests", "Коди") },
            Rows = rows
        };
    }

    private static ReportTable PackagesReport(List<LabOrderTest> tests)
    {
        var rows = tests.Where(t => t.Order!.PackageCode != null).GroupBy(t => (t.Order!.PackageCode, t.Order.PackageName)).Select(g =>
        {
            var orders = g.Select(t => t.Order!).DistinctBy(o => o.Id).ToList();
            return new Dictionary<string, object?>
            {
                ["package"] = g.Key.PackageCode, ["name"] = g.Key.PackageName, ["count"] = orders.Count, ["departments"] = string.Join(", ", orders.Select(o => o.RequestingDepartmentName).Where(d => d != null).Distinct()),
                ["changedPct"] = Math.Round(100.0 * orders.Count(o => o.PackageChangedByDoctor) / orders.Count, 1),
                ["completePct"] = Math.Round(100.0 * g.Count(t => ViewStatus(t) == "ver") / Math.Max(1, g.Count(t => t.Status != OrderTestStatuses.Rejected)), 1),
                ["revenue"] = orders.Sum(o => o.TotalPrice)
            };
        }).OrderByDescending(r => r["count"]).ToList();
        return new ReportTable
        {
            Columns = new() { new("package", "Код пакета"), new("name", "Пакет"), new("count", "Направлень", true), new("departments", "Відділення"), new("changedPct", "Змінено лікарем, %", true),
                new("completePct", "Повнота виконання, %", true), new("revenue", "Вартість, грн", true) },
            Rows = rows
        };
    }

    // ------------------------------------------------------------------ кумулятивний звіт (FR-RPT-001, 23.7)
    public static string Arrow(string? flag) => flag switch
    {
        ResultFlags.CritHigh => "↑↑", ResultFlags.CritLow => "↓↓", ResultFlags.High => "↑", ResultFlags.Low => "↓", _ => ""
    };

    public static string Num(double? v, int decimals) => v.HasValue ? v.Value.ToString("F" + Math.Clamp(decimals, 0, 4), Uk) : "";

    public async Task<object> CumulativeAsync(string patientId, DateTime? from, DateTime? to, string? group, bool abnormalOnly, string format = "json")
    {
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId) ?? throw NotFoundException.For("Пацієнт", patientId);
        from ??= DateTime.UtcNow.Date.AddDays(-30);
        to ??= DateTime.UtcNow;
        var tests = await _db.OrderTests.AsNoTracking().Include(t => t.Order).ThenInclude(o => o!.Items).Include(t => t.Result).ThenInclude(r => r!.Analyzer).Include(t => t.Test).Include(t => t.Profile).Include(t => t.Sample)
            .Where(t => t.Order!.PatientId == patientId && t.Result != null && (t.Status == OrderTestStatuses.Verified || t.Status == OrderTestStatuses.AutoVerified || t.ReleasedAt != null)
                        && t.Order.OrderDatetime >= from && t.Order.OrderDatetime <= to)
            .ToListAsync();
        var byAnalyzer = group == "analyzer";
        var groups = tests.GroupBy(t => (Service: ServiceName(t), Analyzer: byAnalyzer ? t.Result!.Analyzer?.Name : null)).OrderBy(g => g.Key.Service).Select(g =>
        {
            var points = g.Select(t => (At: t.Sample?.CollectedAt ?? t.Order!.OrderDatetime, Analyzer: t.Result!.Analyzer?.Name ?? "—")).Distinct().OrderBy(p => p.At).ToList();
            var rows = g.GroupBy(t => t.TestCode).OrderBy(r => r.Min(t => t.DisplayOrder)).Select(r =>
            {
                var first = r.First();
                var values = points.Select(p => r.FirstOrDefault(t => (t.Sample?.CollectedAt ?? t.Order!.OrderDatetime) == p.At && (t.Result!.Analyzer?.Name ?? "—") == p.Analyzer)).ToList();
                return new
                {
                    code = r.Key, name = first.Test?.Name ?? first.TestName, unit = first.Test?.Unit ?? first.Result!.Unit,
                    reference = string.IsNullOrWhiteSpace(first.Result!.ReferenceDisplay) ? "не встановлено" : first.Result.ReferenceDisplay,
                    abnormal = r.Any(t => t.Result!.Flag != ResultFlags.Normal && t.Result.Flag != ResultFlags.None),
                    values = values.Select(t => t == null ? null : new
                    {
                        display = t.Result!.NumericValue.HasValue ? Num(t.Result.NumericValue, t.Test?.DecimalPlaces ?? 2) : t.Result.StringValue, flag = t.Result.Flag, arrow = Arrow(t.Result.Flag),
                        critical = ResultFlags.IsCritical(t.Result.Flag)
                    })
                };
            }).Where(r => !abnormalOnly || r.abnormal).ToList();
            return new { service = g.Key.Service, analyzer = g.Key.Analyzer, points = points.Select(p => new { at = p.At, p.Analyzer }), rows };
        }).Where(g => g.rows.Count > 0).ToList();
        var criticals = tests.Where(t => ResultFlags.IsCritical(t.Result!.Flag)).OrderBy(t => t.Result!.EnteredAt).Select(t => new
        {
            at = t.Sample?.CollectedAt ?? t.Order!.OrderDatetime, t.TestCode, name = t.Test?.Name ?? t.TestName, value = Num(t.Result!.NumericValue, t.Test?.DecimalPlaces ?? 2) + " " + (t.Test?.Unit ?? ""),
            arrow = Arrow(t.Result.Flag), analyzer = t.Result.Analyzer?.Name
        }).ToList();
        await LogRunAsync("cumulative", new { patientId, from, to, group, abnormalOnly }, format, groups.Sum(g => g.rows.Count), patientId);
        return new { patientId, patientName = patient.Caption, ageGender = DtoMapper.AgeGender(patient, DateTime.UtcNow), from, to, group = byAnalyzer ? "analyzer" : "service", abnormalOnly, groups, criticals };
    }

    // ------------------------------------------------------------------ агрегати (FR-RPT-011)
    public async Task<object> RebuildStatDailyAsync(DateTime from, DateTime to)
    {
        var f = new WorkFilter { Status = "all", From = from.Date, To = to.Date.AddDays(1) };
        var tests = await LoadAsync(f);
        var old = await _db.StatDaily.Where(s => s.Day >= f.From && s.Day < f.To).ToListAsync();
        _db.StatDaily.RemoveRange(old);
        var rows = tests.GroupBy(t => (Day: t.Order!.OrderDatetime.Date, Analyzer: Performer(t), t.ProfileId, t.TestCode, Source: ReferralSources.Of(t.Order), t.Order.PackageCode, t.Order.DepartmentId, t.Order.IsUrgentCito))
            .Select(g =>
            {
                var tats = g.Select(TatMinutes).Where(x => x.HasValue).Select(x => x!.Value).ToList();
                return new LabStatDaily
                {
                    Day = g.Key.Day, AnalyzerId = g.Key.Analyzer, ProfileId = g.Key.ProfileId, TestCode = g.Key.TestCode, Source = g.Key.Source, PackageCode = g.Key.PackageCode, DepartmentId = g.Key.DepartmentId,
                    IsCito = g.Key.IsUrgentCito, Tests = g.Count(t => t.Status != OrderTestStatuses.Rejected), Rejected = g.Count(t => t.Status == OrderTestStatuses.Rejected),
                    Reruns = g.Count(t => (t.Result?.Version ?? 1) > 1), TatP50 = Median(tats), TatP90 = Percentile(tats, 0.9)
                };
            }).ToList();
        _db.StatDaily.AddRange(rows);
        await _db.SaveChangesAsync();
        return new { from = f.From, to = f.To, rows = rows.Count };
    }

    // ------------------------------------------------------------------ експорт (FR-RPT-013: числа — числові клітинки, дати dd.MM.yyyy, кома в PDF)
    public static byte[] Xlsx(ReportTable t)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Звіт");
        ws.Cell(1, 1).Value = t.Title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        for (var i = 0; i < t.Columns.Count; i++) { ws.Cell(3, i + 1).Value = t.Columns[i].Label; ws.Cell(3, i + 1).Style.Font.Bold = true; }
        var row = 4;
        foreach (var r in t.Rows)
        {
            for (var i = 0; i < t.Columns.Count; i++)
            {
                var v = r.GetValueOrDefault(t.Columns[i].Key);
                var cell = ws.Cell(row, i + 1);
                switch (v)
                {
                    case null: break;
                    case int n: cell.Value = n; break;
                    case double d: cell.Value = d; cell.Style.NumberFormat.Format = "0.0"; break;
                    case decimal m: cell.Value = m; cell.Style.NumberFormat.Format = "0.00"; break;
                    case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = "dd.MM.yyyy"; break;
                    default: cell.Value = v.ToString(); break;
                }
            }
            row++;
        }
        if (t.Note != null) ws.Cell(row + 1, 1).Value = t.Note;
        ws.Columns().AdjustToContents(1, 60);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] Pdf(ReportTable t)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        static string Cell(object? v) => v switch { null => "", double d => d.ToString("0.#", Uk), decimal m => m.ToString("0.00", Uk), DateTime dt => dt.ToString("dd.MM.yyyy", Uk), _ => v.ToString() ?? "" };
        return Document.Create(c => c.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(20);
            page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily("Lato"));
            page.Header().Text(t.Title).FontSize(13).Bold().FontColor("#4274A7");
            page.Content().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cd => { foreach (var _ in t.Columns) cd.RelativeColumn(); });
                table.Header(h => { foreach (var col in t.Columns) h.Cell().Background("#EEF3F9").Border(0.5f).Padding(3).Text(col.Label).Bold(); });
                foreach (var r in t.Rows)
                    foreach (var col in t.Columns)
                        table.Cell().Border(0.5f).BorderColor("#CFD8E3").Padding(3).AlignLeft().Text(Cell(r.GetValueOrDefault(col.Key)));
            });
            page.Footer().AlignRight().Text(x => { x.Span("MedLink ЛІС · сторінка "); x.CurrentPageNumber(); });
        })).GeneratePdf();
    }
}
