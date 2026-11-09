// Бланк результатів: HTML A4 (реквізити, пацієнт, таблиця з прапорцями, лікар, QR верифікації) та PDF (QuestPDF)
using System.Globalization;
using System.Security.Cryptography;
using System.Net;
using System.Text;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
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
    private readonly IAuditService _audit;
    private readonly ICurrentEmployee _current;

    static ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public ReportService(LisDbContext db, OrderService orders, OrderStateService state, IConfiguration config, ILogger<ReportService> logger, IAuditService audit, ICurrentEmployee current)
    {
        _db = db; _orders = orders; _state = state; _config = config; _logger = logger; _audit = audit; _current = current;
    }

    // ------------------------------------------------------------------ мова бланка (FR-PRN-002)
    public static string? NormalizeLang(string? lang) => (lang ?? "").Trim().ToLowerInvariant() switch
    {
        "uk" or "ua" or "" => lang == null ? null : "uk",
        "en" => "en",
        "both" or "uk-en" or "bilingual" => "both",
        _ => null
    };

    private static readonly Dictionary<string, (string Uk, string En)> Texts = new()
    {
        ["title"] = ("РЕЗУЛЬТАТИ ЛАБОРАТОРНИХ ДОСЛІДЖЕНЬ", "LABORATORY TEST RESULTS"),
        ["doc"] = ("Бланк результатів", "Results report"),
        ["patient"] = ("Пацієнт", "Patient"), ["dob"] = ("Дата народження", "Date of birth"), ["sex"] = ("стать", "sex"),
        ["male"] = ("чоловіча", "male"), ["female"] = ("жіноча", "female"), ["phone"] = ("Телефон", "Phone"), ["tel"] = ("Тел.", "Tel."),
        ["referral"] = ("Напрямок", "Referred by"), ["ordered"] = ("Замовлення", "Ordered"), ["collected"] = ("Забір матеріалу", "Collected"),
        ["released"] = ("Видано", "Released"), ["barcodes"] = ("Штрихкоди", "Barcodes"),
        ["test"] = ("Показник", "Test"), ["result"] = ("Результат", "Result"), ["unit"] = ("Од. вимір.", "Unit"), ["ref"] = ("Референтні значення", "Reference range"),
        ["flag"] = ("Прапорець", "Flag"), ["prev"] = ("Попередній", "Previous"), ["single"] = ("Окремі дослідження", "Individual tests"),
        ["unverified"] = ("(не верифіковано)", "(not verified)"), ["comments"] = ("Коментарі", "Comments"),
        ["physician"] = ("Лікар-лаборант", "Laboratory physician"), ["director"] = ("Керівник лабораторії", "Laboratory director"),
        ["auto"] = ("Автоматична верифікація: {0} показник(ів) за правилами ЛІС", "Automatic verification: {0} result(s) by LIS rules"),
        ["auth"] = ("Перевірка автентичності", "Authenticity check"),
        ["prelimWm"] = ("ПОПЕРЕДНІЙ", "PRELIMINARY"), ["prelimBanner"] = ("ПОПЕРЕДНІЙ БЛАНК — результати не видано", "PRELIMINARY REPORT — results not released"),
        ["crit"] = ("КРИТ", "CRIT"), ["years"] = ("р.", "y."), ["page"] = ("Сторінка", "Page"), ["of"] = ("з", "of"),
        ["duplicate"] = ("ДУБЛІКАТ", "DUPLICATE"), ["amended"] = ("ВИПРАВЛЕНО", "AMENDED"),
        ["amendNote"] = ("Версія {0}. Замінює бланк версії {1} від {2}. Причина: {3}", "Version {0}. Replaces report version {1} dated {2}. Reason: {3}"),
        ["dupNote"] = ("Копія бланка версії {0} від {1}. Оригінал збережено в архіві, хеш {2}", "Copy of report version {0} dated {1}. Original kept in the archive, hash {2}"),
        ["external"] = ("Дослідження виконано зовнішньою лабораторією: {0}.", "Test performed by an external laboratory: {0}."),
    };

    /// <summary>Текст за ключем і мовою; both → «укр / англ».</summary>
    internal static string Tx(string lang, string key, params object[] args)
    {
        var (uk, en) = Texts[key];
        var t = lang switch { "en" => en, "both" => uk == en ? uk : uk + " / " + en, _ => uk };
        return args.Length == 0 ? t : string.Format(CultureInfo.InvariantCulture, t, args);
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
        /// <summary>uk | en | both</summary>
        public string Lang { get; set; } = "uk";
        /// <summary>"" | AMENDED | DUPLICATE</summary>
        public string Mark { get; set; } = "";
        public string? MarkNote { get; set; }
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

    private static List<(string Mark, string Text)> ExternalNotes(LabOrder o, string lang) =>
        o.Tests.Where(x => x.PerformerId != null).Select(x => x.PerformerId!).Distinct().OrderBy(x => x)
            .Select((id, i) =>
            {
                var p = o.Tests.First(x => x.PerformerId == id).Performer;
                var text = !string.IsNullOrWhiteSpace(p?.ReportNote) ? p!.ReportNote! : Tx(lang, "external", p?.Name ?? id);
                return (new string('*', i + 1), text);
            }).ToList();

    /// <summary>Версії бланка для замовлення (для позначки «ВИПРАВЛЕНО» і нумерації).</summary>
    private async Task<List<LabReportArchive>> ArchiveHeadersAsync(string orderId) =>
        await _db.ReportArchive.AsNoTracking().Where(a => a.OrderId == orderId).OrderBy(a => a.VersionNo)
            .Select(a => new LabReportArchive { Id = a.Id, OrderId = a.OrderId, VersionNo = a.VersionNo, Kind = a.Kind, CreatedOn = a.CreatedOn, AmendReason = a.AmendReason, ContentHash = a.ContentHash }).ToListAsync();

    private async Task<ReportModel> BuildModelAsync(string orderId, string? host, string? variant = null, string? lang = null, int? archivingVersion = null, string? archivingReason = null)
    {
        var order = await _orders.LoadAsync(orderId);
        if (order.Status is OrderStatuses.New or OrderStatuses.Collected or OrderStatuses.InTransit or OrderStatuses.Received)
            throw new ConflictException($"Бланк недоступний: направлення {order.OrderNumber} має статус «{MedLink.LIS.Api.Domain.UkLabels.Status(MedLink.LIS.Api.Domain.LisEntities.Order, order.Status)}» (немає результатів)");
        var lab = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new LabSettings();
        var language = string.IsNullOrWhiteSpace(lang) ? (NormalizeLang(lab.ReportLanguage) ?? "uk") : NormalizeLang(lang) ?? throw ValidationException.Field("lang", "Мова бланка: uk, en або both");
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
        var model = new ReportModel
        {
            Order = order, Lab = lab, Employees = employees, VerifyUrl = url, QrSvg = templates.TryGetValue("showQr", out var qr) && !qr ? "" : QrSvg.Render(url, 3, 2),
            IsPreliminary = isPreliminary, Variant = v, ShowCitoBadge = v == "cito" || order.IsUrgentCito, Templates = templates, Lang = language
        };
        // Виправлений бланк (FR-GAP-010): існують раніше видані версії → позначка «ВИПРАВЛЕНО» з посиланням на попередню
        if (!isPreliminary)
        {
            var versions = await ArchiveHeadersAsync(orderId);
            var current = archivingVersion ?? versions.LastOrDefault()?.VersionNo ?? 0;
            if (current > 1)
            {
                var prev = versions.FirstOrDefault(a => a.VersionNo == current - 1);
                var reason = archivingReason ?? versions.FirstOrDefault(a => a.VersionNo == current)?.AmendReason ?? "виправлення результатів";
                model.Mark = "AMENDED";
                model.MarkNote = Tx(language, "amendNote", current, current - 1, prev?.CreatedOn.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—", reason);
            }
        }
        return model;
    }

    /// <summary>Варіант preliminary показує лише відкриті пацієнту (released) тести; інші — усі невідхилені.</summary>
    private static IEnumerable<LabOrderTest> VisibleTests(ReportModel m)
    {
        var tests = m.Order.Tests.Where(t => t.Status != OrderTestStatuses.Rejected);
        return m.Variant == "preliminary" ? tests.Where(t => t.ReleasedAt != null) : tests;
    }

    private static string FlagLabel(string flag, string lang) => flag switch
    {
        ResultFlags.Low => "↓", ResultFlags.High => "↑", ResultFlags.CritLow => "↓↓ " + Tx(lang == "both" ? "en" : lang, "crit"), ResultFlags.CritHigh => "↑↑ " + Tx(lang == "both" ? "en" : lang, "crit"), ResultFlags.Abnormal => "!", _ => ""
    };

    private static string Age(MisPatientCard? p, DateTime at, string lang) => p?.Birthday == null ? "" : $"{AgeUnits.AgeYears(p.Birthday.Value, at)} {Tx(lang == "both" ? "en" : lang, "years")}";

    private static string GenderText(MisPatientCard? p, string lang) => p?.Gender == "M" ? Tx(lang, "male") : p?.Gender == "F" ? Tx(lang, "female") : "—";

    /// <summary>Назва показника за мовою: en → NameEn, запасний варіант — основна назва; both → «назва / name».</summary>
    private static string TestTitle(LabOrderTest t, string lang)
    {
        var en = t.Test?.NameEn;
        if (string.IsNullOrWhiteSpace(en)) return t.TestName;
        return lang switch { "en" => en, "both" => en.Equals(t.TestName, StringComparison.OrdinalIgnoreCase) ? t.TestName : $"{t.TestName} / {en}", _ => t.TestName };
    }

    private static string GroupTitle(LabOrderTest t, string lang)
    {
        var p = t.Profile;
        if (p == null) return Tx(lang, "single");
        if (string.IsNullOrWhiteSpace(p.NameEn)) return p.Name;
        return lang switch { "en" => p.NameEn, "both" => $"{p.Name} / {p.NameEn}", _ => p.Name };
    }

    private static string MarkText(ReportModel m) => m.Mark == "AMENDED" ? Tx(m.Lang, "amended") : m.Mark == "DUPLICATE" ? Tx(m.Lang, "duplicate") : "";

    public async Task<string> HtmlAsync(string orderId, string? host, string? variant = null, string? lang = null)
    {
        var m = await BuildModelAsync(orderId, host, variant, lang);
        return RenderHtml(m);
    }

    private static string RenderHtml(ReportModel m)
    {
        var o = m.Order; var lab = m.Lab; var L = m.Lang;
        string H(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        sb.Append($"<!doctype html><html lang=\"{(L == "en" ? "en" : "uk")}\"><head><meta charset=\"utf-8\"><title>").Append(H(Tx(L, "doc"))).Append(' ').Append(H(o.OrderNumber)).Append("</title>");
        sb.Append("<style>@page{size:A4;margin:15mm}body{font-family:'Source Sans Pro',Arial,sans-serif;font-size:11pt;color:#212121;margin:0}.hdr{display:flex;justify-content:space-between;border-bottom:3px solid #4274A7;padding-bottom:8px}.hdr h1{margin:0;font-size:15pt;color:#4274A7}.hdr .req{font-size:9pt;color:#555}.meta{display:flex;justify-content:space-between;margin:10px 0;font-size:10pt}.meta table td{padding:1px 8px 1px 0}table.res{width:100%;border-collapse:collapse;margin-top:8px}table.res th{background:#eef3f9;border:1px solid #bbb;padding:4px 6px;text-align:left;font-size:10pt}table.res td{border:1px solid #bbb;padding:3px 6px}.grp{background:#f7f7f7;font-weight:bold}.LOW,.HIGH,.ABNORMAL{color:#b45309;font-weight:bold}.CRIT_LOW,.CRIT_HIGH{color:#b91c1c;font-weight:bold;background:#fee2e2}.foot{margin-top:16px;display:flex;justify-content:space-between;align-items:flex-end;font-size:9.5pt}.prelim{color:#b91c1c;font-weight:bold;border:1px solid #b91c1c;padding:2px 6px;display:inline-block}.small{font-size:8.5pt;color:#555}.wm{position:fixed;top:40%;left:10%;right:10%;text-align:center;font-size:64pt;color:rgba(185,28,28,.12);transform:rotate(-25deg);pointer-events:none;font-weight:bold}.cito{display:inline-block;background:#b91c1c;color:#fff;font-weight:bold;padding:2px 10px;border-radius:4px;margin-left:8px}.mark{text-align:center;margin:8px 0}.mark b{display:inline-block;border:2px solid #b45309;color:#b45309;padding:2px 12px;font-size:13pt}</style></head><body>");
        if (m.IsPreliminary) sb.Append($"<div class=\"wm\">{H(Tx(L, "prelimWm"))}</div>");
        sb.Append($"<div class=\"hdr\"><div><h1>{H(lab.Name)}</h1><div class=\"req\">{H(lab.Address)}<br>{H(Tx(L == "both" ? "en" : L, "tel"))}: {H(lab.Phone)} · {H(lab.Email)}<br>{H(lab.LicenseNumber)} · ЄДРПОУ {H(lab.Edrpou)}</div></div>");
        if (!string.IsNullOrEmpty(lab.LogoBase64)) sb.Append($"<img src=\"data:image/png;base64,{lab.LogoBase64}\" style=\"height:48px\">");
        sb.Append("</div>");
        sb.Append($"<h2 style=\"text-align:center;margin:10px 0 4px;font-size:14pt\">{H(Tx(L, "title"))} № {H(o.OrderNumber)}{(m.ShowCitoBadge ? "<span class=\"cito\">CITO</span>" : "")}</h2>");
        if (m.Mark != "") sb.Append($"<div class=\"mark\"><b>{H(MarkText(m))}</b>{(m.MarkNote != null ? $"<div class=\"small\">{H(m.MarkNote)}</div>" : "")}</div>");
        if (m.IsPreliminary) sb.Append($"<div style=\"text-align:center\"><span class=\"prelim\">{H(Tx(L, "prelimBanner"))}</span></div>");
        var p = o.Patient;
        sb.Append("<div class=\"meta\"><table>");
        sb.Append($"<tr><td><b>{H(Tx(L, "patient"))}:</b></td><td>{H(p?.Caption)}</td></tr><tr><td><b>{H(Tx(L, "dob"))}:</b></td><td>{(p?.Birthday?.ToString("dd.MM.yyyy") ?? "—")} ({Age(p, o.OrderDatetime, L)}), {H(Tx(L, "sex"))}: {H(GenderText(p, L))}</td></tr>");
        sb.Append($"<tr><td><b>{H(Tx(L, "phone"))}:</b></td><td>{H(p?.Person?.Phone)}</td></tr><tr><td><b>{H(Tx(L, "referral"))}:</b></td><td>{H(o.Doctor?.Caption ?? "—")} {(o.Department != null ? "· " + H(o.Department.Caption) : "")}</td></tr></table>");
        sb.Append("<table>");
        sb.Append($"<tr><td><b>{H(Tx(L, "ordered"))}:</b></td><td>{o.OrderDatetime.ToLocalTime():dd.MM.yyyy HH:mm}{(o.IsUrgentCito ? " <b style=\"color:#b91c1c\">CITO</b>" : "")}</td></tr>");
        var collected = o.Samples.Where(s => s.CollectedAt != null).Select(s => s.CollectedAt).Min();
        sb.Append($"<tr><td><b>{H(Tx(L, "collected"))}:</b></td><td>{(collected?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—")}</td></tr>");
        sb.Append($"<tr><td><b>{H(Tx(L, "released"))}:</b></td><td>{(o.ReleasedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—")}</td></tr><tr><td><b>{H(Tx(L, "barcodes"))}:</b></td><td>{string.Join(", ", o.Samples.Select(s => s.Barcode))}</td></tr></table></div>");

        sb.Append($"<table class=\"res\"><tr><th style=\"width:38%\">{H(Tx(L, "test"))}</th><th>{H(Tx(L, "result"))}</th><th>{H(Tx(L, "unit"))}</th><th>{H(Tx(L, "ref"))}</th><th>{H(Tx(L, "flag"))}</th><th>{H(Tx(L, "prev"))}</th></tr>");
        foreach (var grp in VisibleTests(m).OrderBy(t => t.DisplayOrder).GroupBy(t => GroupTitle(t, L)))
        {
            sb.Append($"<tr class=\"grp\"><td colspan=\"6\">{H(grp.Key)}</td></tr>");
            foreach (var t in grp)
            {
                var r = t.Result;
                var val = r == null ? "—" : DtoMapper.FormatValue(r.NumericValue, r.StringValue, t.Test?.DecimalPlaces ?? 2);
                var cls = r?.Flag ?? "";
                var prev = r?.PreviousValue != null ? $"{r.PreviousValue.Value.ToString("0.##", CultureInfo.InvariantCulture)} ({r.PreviousAt?.ToLocalTime():dd.MM.yy}) {(r.DeltaPercent.HasValue ? $"Δ{r.DeltaPercent:+0.0;-0.0}%" : "")}" : "";
                var unverified = r != null && !OrderTestStatuses.VerifiedAny.Contains(t.Status) ? $" <span class=\"small\">{H(Tx(L, "unverified"))}</span>" : "";
                sb.Append($"<tr><td>{H(TestTitle(t, L))}{ExternalMark(t)}{(t.IsReflex ? " <span class=\"small\">(reflex)</span>" : "")}</td><td class=\"{cls}\">{H(val)}{unverified}</td><td>{H(r?.Unit ?? t.Test?.Unit)}</td><td>{H(r?.ReferenceDisplay)}</td><td class=\"{cls}\">{FlagLabel(r?.Flag ?? "", L)}</td><td class=\"small\">{H(prev)}</td></tr>");
            }
        }
        sb.Append("</table>");
        foreach (var note in ExternalNotes(o, L)) sb.Append($"<p class=\"small\"><b>{H(note.Mark)}</b> {H(note.Text)}</p>");
        var comments = o.Tests.Where(t => !string.IsNullOrWhiteSpace(t.Result?.VerificationComment) || !string.IsNullOrWhiteSpace(t.Result?.OperatorComment)).ToList();
        if (comments.Count > 0)
        {
            sb.Append($"<p class=\"small\"><b>{H(Tx(L, "comments"))}:</b><br>");
            foreach (var t in comments) sb.Append($"{H(t.TestCode)}: {H(t.Result?.VerificationComment ?? t.Result?.OperatorComment)}<br>");
            sb.Append("</p>");
        }
        var doctors = o.Tests.Select(t => t.Result?.VerifiedById).Where(v => v != null).Distinct().Select(v => m.Employees.TryGetValue(v!, out var n) ? n : v!).ToList();
        var autoCount = o.Tests.Count(t => t.Result?.IsAutoVerified == true);
        sb.Append("<div class=\"foot\"><div>");
        sb.Append($"<b>{H(Tx(L, "physician"))}:</b> {H(doctors.Count > 0 ? string.Join(", ", doctors) : "—")}{(autoCount > 0 ? $"<br><span class=\"small\">{H(Tx(L, "auto", autoCount))}</span>" : "")}");
        sb.Append($"<br><b>{H(Tx(L, "director"))}:</b> {H(lab.DirectorName)}<br><span class=\"small\">{H(lab.ReportFooter)}</span></div>");
        sb.Append($"<div style=\"text-align:center\">{m.QrSvg}<div class=\"small\">{H(Tx(L, "auth"))}:<br>{H(m.VerifyUrl)}</div></div></div>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>PDF через QuestPDF (шрифт Lato з кирилицею вбудований у бібліотеку). Повертає null, якщо генерація недоступна.</summary>
    public async Task<byte[]?> PdfAsync(string orderId, string? host, string? variant = null, string? lang = null)
    {
        var m = await BuildModelAsync(orderId, host, variant, lang);
        return RenderPdf(m);
    }

    private byte[]? RenderPdf(ReportModel m)
    {
        try
        {
            var L1 = m.Lang == "both" ? "en" : m.Lang;
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(15, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Lato"));
                    if (m.IsPreliminary) page.Background().AlignCenter().AlignMiddle().Rotate(-25).Text(Tx(L1, "prelimWm")).FontSize(72).Bold().FontColor("#F5D0D0");
                    page.Header().Element(c => ComposeHeader(c, m));
                    page.Content().Element(c => ComposeContent(c, m));
                    page.Footer().AlignCenter().Text(t => { t.Span(Tx(L1, "page") + " "); t.CurrentPageNumber(); t.Span(" " + Tx(L1, "of") + " "); t.TotalPages(); });
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
        var lab = m.Lab; var o = m.Order; var L = m.Lang; var L1 = L == "both" ? "en" : L;
        c.Column(col =>
        {
            col.Item().BorderBottom(2).BorderColor("#4274A7").PaddingBottom(4).Row(r =>
            {
                r.RelativeItem().Column(cc =>
                {
                    cc.Item().Text(lab.Name).FontSize(14).Bold().FontColor("#4274A7");
                    cc.Item().Text($"{lab.Address}  ·  {Tx(L1, "tel")}: {lab.Phone}  ·  {lab.Email}").FontSize(8).FontColor("#555");
                    cc.Item().Text($"{lab.LicenseNumber}  ·  ЄДРПОУ {lab.Edrpou}").FontSize(8).FontColor("#555");
                });
                if (!string.IsNullOrEmpty(m.QrSvg)) r.ConstantItem(70).Svg(m.QrSvg);
            });
            col.Item().PaddingTop(6).AlignCenter().Text(t => { t.Span($"{Tx(L, "title")} № {o.OrderNumber}").FontSize(12).Bold(); if (m.ShowCitoBadge) t.Span("   CITO").FontSize(12).Bold().FontColor("#b91c1c"); });
            if (m.Mark != "")
            {
                col.Item().AlignCenter().Text(MarkText(m)).FontSize(13).Bold().FontColor("#b45309");
                if (m.MarkNote != null) col.Item().AlignCenter().Text(m.MarkNote).FontSize(8).FontColor("#555");
            }
            if (m.IsPreliminary) col.Item().AlignCenter().Text(Tx(L, "prelimBanner")).FontColor("#b91c1c").Bold();
        });
    }

    private static void ComposeContent(IContainer c, ReportModel m)
    {
        var o = m.Order; var p = o.Patient; var L = m.Lang;
        c.PaddingTop(6).Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(cc =>
                {
                    cc.Item().Text(t => { t.Span(Tx(L, "patient") + ": ").Bold(); t.Span(p?.Caption ?? ""); });
                    cc.Item().Text(t => { t.Span(Tx(L, "dob") + ": ").Bold(); t.Span($"{p?.Birthday:dd.MM.yyyy} ({Age(p, o.OrderDatetime, L)}), {Tx(L, "sex")}: {GenderText(p, L)}"); });
                    cc.Item().Text(t => { t.Span(Tx(L, "referral") + ": ").Bold(); t.Span($"{o.Doctor?.Caption ?? "—"} {(o.Department != null ? "· " + o.Department.Caption : "")}"); });
                });
                r.RelativeItem().Column(cc =>
                {
                    cc.Item().Text(t => { t.Span(Tx(L, "ordered") + ": ").Bold(); t.Span($"{o.OrderDatetime.ToLocalTime():dd.MM.yyyy HH:mm}{(o.IsUrgentCito ? "  CITO" : "")}"); });
                    var collected = o.Samples.Where(s => s.CollectedAt != null).Select(s => s.CollectedAt).Min();
                    cc.Item().Text(t => { t.Span(Tx(L, "collected") + ": ").Bold(); t.Span(collected?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—"); });
                    cc.Item().Text(t => { t.Span(Tx(L, "released") + ": ").Bold(); t.Span(o.ReleasedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—"); });
                    cc.Item().Text(t => { t.Span(Tx(L, "barcodes") + ": ").Bold(); t.Span(string.Join(", ", o.Samples.Select(s => s.Barcode))); });
                });
            });

            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cd => { cd.RelativeColumn(4); cd.RelativeColumn(1.6f); cd.RelativeColumn(1.2f); cd.RelativeColumn(2); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.8f); });
                table.Header(h =>
                {
                    foreach (var title in new[] { Tx(L, "test"), Tx(L, "result"), Tx(L, "unit"), Tx(L, "ref"), Tx(L, "flag"), Tx(L, "prev") })
                        h.Cell().Background("#eef3f9").Border(0.5f).BorderColor("#bbb").Padding(3).Text(title).Bold();
                });
                foreach (var grp in VisibleTests(m).OrderBy(t => t.DisplayOrder).GroupBy(t => GroupTitle(t, L)))
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
                        Cell().Text(TestTitle(t, L) + ExternalMark(t) + (t.IsReflex ? " (reflex)" : ""));
                        Cell().Text(val).FontColor(color).Bold();
                        Cell().Text(r?.Unit ?? t.Test?.Unit ?? "");
                        Cell().Text(r?.ReferenceDisplay ?? "");
                        Cell().Text(FlagLabel(r?.Flag ?? "", L)).FontColor(color).Bold();
                        Cell().Text(prev).FontSize(8);
                    }
                }
            });

            foreach (var note in ExternalNotes(o, L)) col.Item().PaddingTop(4).Text($"{note.Mark} {note.Text}").FontSize(8).FontColor("#555");
            var doctors = o.Tests.Select(t => t.Result?.VerifiedById).Where(v => v != null).Distinct().Select(v => m.Employees.TryGetValue(v!, out var n) ? n : v!).ToList();
            var autoCount = o.Tests.Count(t => t.Result?.IsAutoVerified == true);
            col.Item().PaddingTop(12).Text(t => { t.Span(Tx(L, "physician") + ": ").Bold(); t.Span(doctors.Count > 0 ? string.Join(", ", doctors) : "—"); });
            if (autoCount > 0) col.Item().Text(Tx(L, "auto", autoCount)).FontSize(8).FontColor("#555");
            col.Item().Text(t => { t.Span(Tx(L, "director") + ": ").Bold(); t.Span(m.Lab.DirectorName ?? ""); });
            col.Item().PaddingTop(6).Text(m.Lab.ReportFooter ?? "").FontSize(8).FontColor("#555");
            col.Item().PaddingTop(4).Text($"{Tx(L, "auth")}: {m.VerifyUrl}").FontSize(8).FontColor("#555");
        });
    }

    // ------------------------------------------------------------------ архів виданих бланків (FR-GAP-010/012)
    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    /// <summary>Фіксує незмінну копію бланка (PDF + HTML) із SHA-256. Перша версія — ISSUED, наступні (після повторного відкриття й нової видачі) — AMENDED.</summary>
    public async Task<LabReportArchive> ArchiveAsync(string orderId, string? host, string? reason = null, string? lang = null)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId) ?? throw NotFoundException.For("Замовлення", orderId);
        if (order.Status != OrderStatuses.Released) throw new ConflictException($"Архівувати бланк можна лише для виданого замовлення (зараз «{UkLabels.Status(LisEntities.Order, order.Status)}»)");
        var last = await _db.ReportArchive.AsNoTracking().Where(a => a.OrderId == orderId).OrderByDescending(a => a.VersionNo).Select(a => new { a.VersionNo, a.CreatedOn }).FirstOrDefaultAsync();
        var version = (last?.VersionNo ?? 0) + 1;
        if (version > 1 && string.IsNullOrWhiteSpace(reason))
        {
            // причина виправлення — з коментаря останнього повторного відкриття замовлення
            reason = await _db.AuditLog.AsNoTracking().Where(a => a.Entity == "lab_order" && a.EntityId == orderId && a.Action == "REOPEN").OrderByDescending(a => a.At).Select(a => a.Comment).FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(reason)) reason = "виправлення результатів";
        }
        var m = await BuildModelAsync(orderId, host, "final", lang, archivingVersion: version, archivingReason: reason);
        var html = RenderHtml(m);
        var pdf = RenderPdf(m);
        var entry = new LabReportArchive
        {
            OrderId = orderId, VersionNo = version, Kind = version == 1 ? "ISSUED" : "AMENDED", Language = m.Lang, Variant = "final",
            AmendsVersionNo = version > 1 ? version - 1 : null, AmendReason = version > 1 ? reason!.Trim() : null,
            Html = html, Pdf = pdf, ContentHash = Sha256(pdf ?? Encoding.UTF8.GetBytes(html)), IssuedById = _current.EmployeeId
        };
        _db.ReportArchive.Add(entry);
        _audit.Log(version == 1 ? "ARCHIVE_REPORT" : "ARCHIVE_REPORT_AMENDED", "lab_report_archive", entry.Id, null, new { orderId, version, entry.ContentHash, entry.Kind }, entry.AmendReason);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<List<object>> ArchiveListAsync(string orderId)
    {
        var items = await _db.ReportArchive.AsNoTracking().Where(a => a.OrderId == orderId).OrderBy(a => a.VersionNo)
            .Select(a => new { a.Id, a.VersionNo, a.Kind, a.Language, a.Variant, a.AmendsVersionNo, a.AmendReason, a.ContentHash, HasPdf = a.Pdf != null, a.CreatedOn, a.IssuedById, a.DuplicatesPrinted }).ToListAsync();
        var emp = await _db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Caption ?? "");
        var max = items.Count == 0 ? 0 : items.Max(x => x.VersionNo);
        return items.Select(a => (object)new
        {
            a.Id, a.VersionNo, a.Kind, a.Language, a.Variant, a.AmendsVersionNo, a.AmendReason, a.ContentHash, hasPdf = a.HasPdf, issuedAt = a.CreatedOn,
            issuedBy = a.IssuedById != null && emp.TryGetValue(a.IssuedById, out var n) ? n : null, a.DuplicatesPrinted, isCurrent = a.VersionNo == max
        }).ToList();
    }

    private async Task<LabReportArchive> LoadArchiveAsync(string orderId, int version) =>
        await _db.ReportArchive.FirstOrDefaultAsync(a => a.OrderId == orderId && a.VersionNo == version) ?? throw NotFoundException.For("Версія бланка", $"{orderId}/{version}");

    /// <summary>Дублікат: точна копія збереженого бланка з позначкою «ДУБЛІКАТ»; факт друку фіксується в аудиті й лічильнику.</summary>
    public async Task<string> DuplicateHtmlAsync(string orderId, int version)
    {
        var a = await LoadArchiveAsync(orderId, version);
        a.DuplicatesPrinted++;
        _audit.Log("PRINT_DUPLICATE", "lab_report_archive", a.Id, null, new { orderId, version, a.ContentHash });
        await _db.SaveChangesAsync();
        var L = a.Language;
        var banner = $"<div style=\"text-align:center;margin:8px 0\"><b style=\"display:inline-block;border:3px solid #b91c1c;color:#b91c1c;padding:3px 16px;font-size:16pt\">{WebUtility.HtmlEncode(Tx(L, "duplicate"))}</b>"
            + $"<div style=\"font-size:8.5pt;color:#555\">{WebUtility.HtmlEncode(Tx(L, "dupNote", a.VersionNo, a.CreatedOn.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), a.ContentHash[..16]))}</div></div>"
            + $"<div style=\"position:fixed;top:40%;left:10%;right:10%;text-align:center;font-size:64pt;color:rgba(185,28,28,.12);transform:rotate(-25deg);pointer-events:none;font-weight:bold\">{WebUtility.HtmlEncode(Tx(L, "duplicate"))}</div>";
        var i = a.Html.IndexOf("<body>", StringComparison.Ordinal);
        return i < 0 ? banner + a.Html : a.Html.Insert(i + 6, banner);
    }

    /// <summary>Оригінальний PDF із архіву (байт у байт); хеш перевіряється перед видачею.</summary>
    public async Task<(byte[] Pdf, string Hash)?> ArchivePdfAsync(string orderId, int version)
    {
        var a = await LoadArchiveAsync(orderId, version);
        if (a.Pdf == null) return null;
        if (Sha256(a.Pdf) != a.ContentHash) throw new ConflictException("Хеш збереженого бланка не збігається — копію пошкоджено");
        return (a.Pdf, a.ContentHash);
    }

    /// <summary>Перевірка цілісності: перерахунок SHA-256 збереженої копії.</summary>
    public async Task<object> ArchiveVerifyAsync(string orderId, int version)
    {
        var a = await LoadArchiveAsync(orderId, version);
        var actual = Sha256(a.Pdf ?? Encoding.UTF8.GetBytes(a.Html));
        return new { orderId, version, expected = a.ContentHash, actual, valid = actual == a.ContentHash };
    }
}
