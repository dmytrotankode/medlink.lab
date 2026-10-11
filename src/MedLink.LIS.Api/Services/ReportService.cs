// Бланк результатів: HTML A4 (реквізити, пацієнт, таблиця з прапорцями, лікар, QR верифікації) та PDF (QuestPDF)
using System.Globalization;
using System.Net;
using System.Text;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Core.Barcodes;
using MedLink.LIS.Core.Clinical;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MedLink.LIS.Api.Services;

public sealed class ReportService
{
    private readonly LisDbContext _db;
    private readonly OrderService _orders;
    private readonly OrderStateService _state;
    private readonly IConfiguration _config;
    private readonly ILogger<ReportService> _logger;

    static ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public ReportService(LisDbContext db, OrderService orders, OrderStateService state, IConfiguration config, ILogger<ReportService> logger)
    {
        _db = db; _orders = orders; _state = state; _config = config; _logger = logger;
    }

    private sealed class ReportModel
    {
        public LabOrder Order { get; set; } = null!;
        public LabSettings Lab { get; set; } = null!;
        public Dictionary<string, string> Employees { get; set; } = new();
        public string VerifyUrl { get; set; } = "";
        public string QrSvg { get; set; } = "";
        public bool IsPreliminary { get; set; }
        /// <summary>final | preliminary | cito</summary>
        public string Variant { get; set; } = "final";
        public bool ShowCitoBadge { get; set; }
        public Dictionary<string, bool> Templates { get; set; } = new();
    }

    public static string NormalizeVariant(string? variant) => (variant ?? "final").Trim().ToLowerInvariant() switch
    {
        "preliminary" or "prelim" => "preliminary",
        "cito" => "cito",
        _ => "final"
    };

    /// <summary>Позначка «*», «**»… для тестів, виконаних зовнішніми лабораторіями (send-out), — по одній на виконавця.</summary>
    private static string ExternalMark(LabOrderTest t)
    {
        var performers = t.Order?.Tests.Where(x => x.PerformerId != null).Select(x => x.PerformerId).Distinct().OrderBy(x => x).ToList() ?? new();
        var idx = t.PerformerId == null ? -1 : performers.IndexOf(t.PerformerId);
        return idx < 0 ? "" : " " + new string('*', idx + 1);
    }

    private static List<(string Mark, string Text)> ExternalNotes(LabOrder o) =>
        o.Tests.Where(x => x.PerformerId != null).Select(x => x.PerformerId!).Distinct().OrderBy(x => x)
            .Select((id, i) =>
            {
                var p = o.Tests.First(x => x.PerformerId == id).Performer;
                var text = !string.IsNullOrWhiteSpace(p?.ReportNote) ? p!.ReportNote! : $"Дослідження виконано зовнішньою лабораторією: {p?.Name ?? id}.";
                return (new string('*', i + 1), text);
            }).ToList();

    private async Task<ReportModel> BuildModelAsync(string orderId, string? host, string? variant = null)
    {
        var order = await _orders.LoadAsync(orderId);
        if (order.Status is OrderStatuses.New or OrderStatuses.Collected or OrderStatuses.InTransit or OrderStatuses.Received)
            throw new ConflictException($"Бланк недоступний: замовлення {order.OrderNumber} у статусі {order.Status} (немає результатів)");
        var lab = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();
        var employees = await _db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Caption ?? "");
        var baseUrl = (_config["Lab:PublicBaseUrl"] ?? (host != null ? $"http://{host}" : "http://localhost:5055")).TrimEnd('/');
        var token = order.VerifyToken ?? _state.VerifyToken(order.Id, order.ReleasedAt ?? order.CompletedAt ?? order.OrderDatetime);
        var url = $"{baseUrl}/verify/{token}";
        var v = NormalizeVariant(variant);
        var templates = lab.ReportTemplates;
        if (templates.TryGetValue(v, out var enabled) && !enabled) throw new ConflictException($"Шаблон бланка «{v}» вимкнено в налаштуваннях лабораторії");
        var allVerified = order.Tests.Where(t => t.Status != OrderTestStatuses.Rejected).All(t => OrderTestStatuses.VerifiedAny.Contains(t.Status));
        // «ПОПЕРЕДНІЙ»: явний варіант preliminary або не всі тести верифіковано / замовлення не видано
        var isPreliminary = v == "preliminary" || !allVerified || order.Status != OrderStatuses.Released;
        return new ReportModel
        {
            Order = order, Lab = lab, Employees = employees, VerifyUrl = url, QrSvg = templates.TryGetValue("showQr", out var qr) && !qr ? "" : QrSvg.Render(url, 3, 2),
            IsPreliminary = isPreliminary, Variant = v, ShowCitoBadge = v == "cito" || order.IsUrgentCito, Templates = templates
        };
    }

    /// <summary>Варіант preliminary показує лише відкриті пацієнту (released) тести; інші — усі невідхилені.</summary>
    private static IEnumerable<LabOrderTest> VisibleTests(ReportModel m)
    {
        var tests = m.Order.Tests.Where(t => t.Status != OrderTestStatuses.Rejected);
        return m.Variant == "preliminary" ? tests.Where(t => t.ReleasedAt != null) : tests;
    }

    private static string FlagLabel(string flag) => flag switch
    {
        ResultFlags.Low => "↓", ResultFlags.High => "↑", ResultFlags.CritLow => "↓↓ КРИТ", ResultFlags.CritHigh => "↑↑ КРИТ", ResultFlags.Abnormal => "!", _ => ""
    };

    private static string Age(MisPatientCard? p, DateTime at) => p?.Birthday == null ? "" : $"{AgeUnits.AgeYears(p.Birthday.Value, at)} р.";

    public async Task<string> HtmlAsync(string orderId, string? host, string? variant = null)
    {
        var m = await BuildModelAsync(orderId, host, variant);
        var o = m.Order; var lab = m.Lab;
        string H(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"uk\"><head><meta charset=\"utf-8\"><title>Бланк результатів ").Append(H(o.OrderNumber)).Append("</title>");
        sb.Append("<style>@page{size:A4;margin:15mm}body{font-family:'Source Sans Pro',Arial,sans-serif;font-size:11pt;color:#212121;margin:0}.hdr{display:flex;justify-content:space-between;border-bottom:3px solid #4274A7;padding-bottom:8px}.hdr h1{margin:0;font-size:15pt;color:#4274A7}.hdr .req{font-size:9pt;color:#555}.meta{display:flex;justify-content:space-between;margin:10px 0;font-size:10pt}.meta table td{padding:1px 8px 1px 0}table.res{width:100%;border-collapse:collapse;margin-top:8px}table.res th{background:#eef3f9;border:1px solid #bbb;padding:4px 6px;text-align:left;font-size:10pt}table.res td{border:1px solid #bbb;padding:3px 6px}.grp{background:#f7f7f7;font-weight:bold}.LOW,.HIGH,.ABNORMAL{color:#b45309;font-weight:bold}.CRIT_LOW,.CRIT_HIGH{color:#b91c1c;font-weight:bold;background:#fee2e2}.foot{margin-top:16px;display:flex;justify-content:space-between;align-items:flex-end;font-size:9.5pt}.prelim{color:#b91c1c;font-weight:bold;border:1px solid #b91c1c;padding:2px 6px;display:inline-block}.small{font-size:8.5pt;color:#555}.wm{position:fixed;top:40%;left:10%;right:10%;text-align:center;font-size:64pt;color:rgba(185,28,28,.12);transform:rotate(-25deg);pointer-events:none;font-weight:bold}.cito{display:inline-block;background:#b91c1c;color:#fff;font-weight:bold;padding:2px 10px;border-radius:4px;margin-left:8px}</style></head><body>");
        if (m.IsPreliminary) sb.Append("<div class=\"wm\">ПОПЕРЕДНІЙ</div>");
        sb.Append($"<div class=\"hdr\"><div><h1>{H(lab.Name)}</h1><div class=\"req\">{H(lab.Address)}<br>Тел.: {H(lab.Phone)} · {H(lab.Email)}<br>{H(lab.LicenseNumber)} · ЄДРПОУ {H(lab.Edrpou)}</div></div>");
        if (!string.IsNullOrEmpty(lab.LogoBase64)) sb.Append($"<img src=\"data:image/png;base64,{lab.LogoBase64}\" style=\"height:48px\">");
        sb.Append("</div>");
        sb.Append($"<h2 style=\"text-align:center;margin:10px 0 4px;font-size:14pt\">РЕЗУЛЬТАТИ ЛАБОРАТОРНИХ ДОСЛІДЖЕНЬ № {H(o.OrderNumber)}{(m.ShowCitoBadge ? "<span class=\"cito\">CITO</span>" : "")}</h2>");
        if (m.IsPreliminary) sb.Append("<div style=\"text-align:center\"><span class=\"prelim\">ПОПЕРЕДНІЙ БЛАНК — результати не видано</span></div>");
        var p = o.Patient;
        sb.Append("<div class=\"meta\"><table>");
        sb.Append($"<tr><td><b>Пацієнт:</b></td><td>{H(p?.Caption)}</td></tr><tr><td><b>Дата народження:</b></td><td>{(p?.Birthday?.ToString("dd.MM.yyyy") ?? "—")} ({Age(p, o.OrderDatetime)}), стать: {(p?.Gender == "M" ? "чоловіча" : p?.Gender == "F" ? "жіноча" : "—")}</td></tr>");
        sb.Append($"<tr><td><b>Телефон:</b></td><td>{H(p?.Person?.Phone)}</td></tr><tr><td><b>Напрямок:</b></td><td>{H(o.Doctor?.Caption ?? "—")} {(o.Department != null ? "· " + H(o.Department.Caption) : "")}</td></tr></table>");
        sb.Append("<table>");
        sb.Append($"<tr><td><b>Замовлення:</b></td><td>{o.OrderDatetime.ToLocalTime():dd.MM.yyyy HH:mm}{(o.IsUrgentCito ? " <b style=\"color:#b91c1c\">CITO</b>" : "")}</td></tr>");
        var collected = o.Samples.Where(s => s.CollectedAt != null).Select(s => s.CollectedAt).Min();
        sb.Append($"<tr><td><b>Забір матеріалу:</b></td><td>{(collected?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—")}</td></tr>");
        sb.Append($"<tr><td><b>Видано:</b></td><td>{(o.ReleasedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—")}</td></tr><tr><td><b>Штрихкоди:</b></td><td>{string.Join(", ", o.Samples.Select(s => s.Barcode))}</td></tr></table></div>");

        sb.Append("<table class=\"res\"><tr><th style=\"width:38%\">Показник</th><th>Результат</th><th>Од. вимір.</th><th>Референтні значення</th><th>Прапорець</th><th>Попередній</th></tr>");
        foreach (var grp in VisibleTests(m).OrderBy(t => t.DisplayOrder).GroupBy(t => t.Profile?.Name ?? "Окремі дослідження"))
        {
            sb.Append($"<tr class=\"grp\"><td colspan=\"6\">{H(grp.Key)}</td></tr>");
            foreach (var t in grp)
            {
                var r = t.Result;
                var val = r == null ? "—" : DtoMapper.FormatValue(r.NumericValue, r.StringValue, t.Test?.DecimalPlaces ?? 2);
                var cls = r?.Flag ?? "";
                var prev = r?.PreviousValue != null ? $"{r.PreviousValue.Value.ToString("0.##", CultureInfo.InvariantCulture)} ({r.PreviousAt?.ToLocalTime():dd.MM.yy}) {(r.DeltaPercent.HasValue ? $"Δ{r.DeltaPercent:+0.0;-0.0}%" : "")}" : "";
                var unverified = r != null && !OrderTestStatuses.VerifiedAny.Contains(t.Status) ? " <span class=\"small\">(не верифіковано)</span>" : "";
                sb.Append($"<tr><td>{H(t.TestName)}{ExternalMark(t)}{(t.IsReflex ? " <span class=\"small\">(reflex)</span>" : "")}</td><td class=\"{cls}\">{H(val)}{unverified}</td><td>{H(r?.Unit ?? t.Test?.Unit)}</td><td>{H(r?.ReferenceDisplay)}</td><td class=\"{cls}\">{FlagLabel(r?.Flag ?? "")}</td><td class=\"small\">{H(prev)}</td></tr>");
            }
        }
        sb.Append("</table>");
        foreach (var note in ExternalNotes(o)) sb.Append($"<p class=\"small\"><b>{H(note.Mark)}</b> {H(note.Text)}</p>");
        var comments = o.Tests.Where(t => !string.IsNullOrWhiteSpace(t.Result?.VerificationComment) || !string.IsNullOrWhiteSpace(t.Result?.OperatorComment)).ToList();
        if (comments.Count > 0)
        {
            sb.Append("<p class=\"small\"><b>Коментарі:</b><br>");
            foreach (var t in comments) sb.Append($"{H(t.TestCode)}: {H(t.Result?.VerificationComment ?? t.Result?.OperatorComment)}<br>");
            sb.Append("</p>");
        }
        var doctors = o.Tests.Select(t => t.Result?.VerifiedById).Where(v => v != null).Distinct().Select(v => m.Employees.TryGetValue(v!, out var n) ? n : v!).ToList();
        var autoCount = o.Tests.Count(t => t.Result?.IsAutoVerified == true);
        sb.Append("<div class=\"foot\"><div>");
        sb.Append($"<b>Лікар-лаборант:</b> {H(doctors.Count > 0 ? string.Join(", ", doctors) : "—")}{(autoCount > 0 ? $"<br><span class=\"small\">Автоматична верифікація: {autoCount} показник(ів) за правилами ЛІС</span>" : "")}");
        sb.Append($"<br><b>Керівник лабораторії:</b> {H(lab.DirectorName)}<br><span class=\"small\">{H(lab.ReportFooter)}</span></div>");
        sb.Append($"<div style=\"text-align:center\">{m.QrSvg}<div class=\"small\">Перевірка автентичності:<br>{H(m.VerifyUrl)}</div></div></div>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>PDF через QuestPDF (шрифт Lato з кирилицею вбудований у бібліотеку). Повертає null, якщо генерація недоступна.</summary>
    public async Task<byte[]?> PdfAsync(string orderId, string? host, string? variant = null)
    {
        var m = await BuildModelAsync(orderId, host, variant);
        try
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(15, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Lato"));
                    if (m.IsPreliminary) page.Background().AlignCenter().AlignMiddle().Rotate(-25).Text("ПОПЕРЕДНІЙ").FontSize(72).Bold().FontColor("#F5D0D0");
                    page.Header().Element(c => ComposeHeader(c, m));
                    page.Content().Element(c => ComposeContent(c, m));
                    page.Footer().AlignCenter().Text(t => { t.Span("Сторінка "); t.CurrentPageNumber(); t.Span(" з "); t.TotalPages(); });
                });
            });
            return doc.GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Генерація PDF недоступна (QuestPDF/Skia)");
            return null;
        }
    }

    private static void ComposeHeader(IContainer c, ReportModel m)
    {
        var lab = m.Lab; var o = m.Order;
        c.Column(col =>
        {
            col.Item().BorderBottom(2).BorderColor("#4274A7").PaddingBottom(4).Row(r =>
            {
                r.RelativeItem().Column(cc =>
                {
                    cc.Item().Text(lab.Name).FontSize(14).Bold().FontColor("#4274A7");
                    cc.Item().Text($"{lab.Address}  ·  Тел.: {lab.Phone}  ·  {lab.Email}").FontSize(8).FontColor("#555");
                    cc.Item().Text($"{lab.LicenseNumber}  ·  ЄДРПОУ {lab.Edrpou}").FontSize(8).FontColor("#555");
                });
                if (!string.IsNullOrEmpty(m.QrSvg)) r.ConstantItem(70).Svg(m.QrSvg);
            });
            col.Item().PaddingTop(6).AlignCenter().Text(t => { t.Span($"РЕЗУЛЬТАТИ ЛАБОРАТОРНИХ ДОСЛІДЖЕНЬ № {o.OrderNumber}").FontSize(12).Bold(); if (m.ShowCitoBadge) t.Span("   CITO").FontSize(12).Bold().FontColor("#b91c1c"); });
            if (m.IsPreliminary) col.Item().AlignCenter().Text("ПОПЕРЕДНІЙ БЛАНК — результати не видано").FontColor("#b91c1c").Bold();
        });
    }

    private static void ComposeContent(IContainer c, ReportModel m)
    {
        var o = m.Order; var p = o.Patient;
        c.PaddingTop(6).Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(cc =>
                {
                    cc.Item().Text(t => { t.Span("Пацієнт: ").Bold(); t.Span(p?.Caption ?? ""); });
                    cc.Item().Text(t => { t.Span("Дата народження: ").Bold(); t.Span($"{p?.Birthday:dd.MM.yyyy} ({Age(p, o.OrderDatetime)}), стать: {(p?.Gender == "M" ? "чоловіча" : p?.Gender == "F" ? "жіноча" : "—")}"); });
                    cc.Item().Text(t => { t.Span("Напрямок: ").Bold(); t.Span($"{o.Doctor?.Caption ?? "—"} {(o.Department != null ? "· " + o.Department.Caption : "")}"); });
                });
                r.RelativeItem().Column(cc =>
                {
                    cc.Item().Text(t => { t.Span("Замовлення: ").Bold(); t.Span($"{o.OrderDatetime.ToLocalTime():dd.MM.yyyy HH:mm}{(o.IsUrgentCito ? "  CITO" : "")}"); });
                    var collected = o.Samples.Where(s => s.CollectedAt != null).Select(s => s.CollectedAt).Min();
                    cc.Item().Text(t => { t.Span("Забір: ").Bold(); t.Span(collected?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—"); });
                    cc.Item().Text(t => { t.Span("Видано: ").Bold(); t.Span(o.ReleasedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—"); });
                    cc.Item().Text(t => { t.Span("Штрихкоди: ").Bold(); t.Span(string.Join(", ", o.Samples.Select(s => s.Barcode))); });
                });
            });

            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cd => { cd.RelativeColumn(4); cd.RelativeColumn(1.6f); cd.RelativeColumn(1.2f); cd.RelativeColumn(2); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.8f); });
                table.Header(h =>
                {
                    foreach (var title in new[] { "Показник", "Результат", "Од.", "Референс", "Прапорець", "Попередній" })
                        h.Cell().Background("#eef3f9").Border(0.5f).BorderColor("#bbb").Padding(3).Text(title).Bold();
                });
                foreach (var grp in VisibleTests(m).OrderBy(t => t.DisplayOrder).GroupBy(t => t.Profile?.Name ?? "Окремі дослідження"))
                {
                    table.Cell().ColumnSpan(6).Background("#f7f7f7").Border(0.5f).BorderColor("#bbb").Padding(3).Text(grp.Key).Bold();
                    foreach (var t in grp)
                    {
                        var r = t.Result;
                        var val = r == null ? "—" : DtoMapper.FormatValue(r.NumericValue, r.StringValue, t.Test?.DecimalPlaces ?? 2);
                        var isCrit = ResultFlags.IsCritical(r?.Flag);
                        var isAbn = ResultFlags.IsAbnormal(r?.Flag);
                        var color = isCrit ? "#b91c1c" : isAbn ? "#b45309" : "#212121";
                        var prev = r?.PreviousValue != null ? $"{r.PreviousValue.Value.ToString("0.##", CultureInfo.InvariantCulture)} ({r.PreviousAt?.ToLocalTime():dd.MM.yy}){(r.DeltaPercent.HasValue ? $" Δ{r.DeltaPercent:+0.0;-0.0}%" : "")}" : "";
                        IContainer Cell() => table.Cell().Border(0.5f).BorderColor("#bbb").Padding(3).Background(isCrit ? "#fee2e2" : "#ffffff");
                        Cell().Text(t.TestName + ExternalMark(t) + (t.IsReflex ? " (reflex)" : ""));
                        Cell().Text(val).FontColor(color).Bold();
                        Cell().Text(r?.Unit ?? t.Test?.Unit ?? "");
                        Cell().Text(r?.ReferenceDisplay ?? "");
                        Cell().Text(FlagLabel(r?.Flag ?? "")).FontColor(color).Bold();
                        Cell().Text(prev).FontSize(8);
                    }
                }
            });

            foreach (var note in ExternalNotes(o)) col.Item().PaddingTop(4).Text($"{note.Mark} {note.Text}").FontSize(8).FontColor("#555");
            var doctors = o.Tests.Select(t => t.Result?.VerifiedById).Where(v => v != null).Distinct().Select(v => m.Employees.TryGetValue(v!, out var n) ? n : v!).ToList();
            var autoCount = o.Tests.Count(t => t.Result?.IsAutoVerified == true);
            col.Item().PaddingTop(12).Text(t => { t.Span("Лікар-лаборант: ").Bold(); t.Span(doctors.Count > 0 ? string.Join(", ", doctors) : "—"); });
            if (autoCount > 0) col.Item().Text($"Автоматична верифікація: {autoCount} показник(ів) за правилами ЛІС").FontSize(8).FontColor("#555");
            col.Item().Text(t => { t.Span("Керівник лабораторії: ").Bold(); t.Span(m.Lab.DirectorName ?? ""); });
            col.Item().PaddingTop(6).Text(m.Lab.ReportFooter ?? "").FontSize(8).FontColor("#555");
            col.Item().PaddingTop(4).Text($"Перевірка автентичності бланка: {m.VerifyUrl}").FontSize(8).FontColor("#555");
        });
    }
}
