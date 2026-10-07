// =============================================================================
// MedLink LIS Analyzer Connector — локальна сторінка статусу http://localhost:5088/
// (HTML українською, без зовнішніх CDN) та GET /status.json; дії: перечитати конфіг,
// тестове повідомлення, очистити буфер.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Net;
using System.Text;
using System.Text.Json;
using MedLink.LabConnector.Configuration;
using MedLink.LabConnector.Server;
using MedLink.LabConnector.Services;
using MedLink.LabConnector.Storage;
using Microsoft.Extensions.Options;

namespace MedLink.LabConnector.Status;

public static class StatusEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void Map(WebApplication app)
    {
        app.MapGet("/", (StatusState st, ConfigStore cfg, LisApiClient api, OfflineBuffer buf, IOptionsMonitor<ConnectorOptions> opt, ConnectorPaths paths) =>
            Results.Content(RenderHtml(Snapshot(st, cfg, api, buf, opt.CurrentValue, paths)), "text/html; charset=utf-8"));

        app.MapGet("/status.json", (StatusState st, ConfigStore cfg, LisApiClient api, OfflineBuffer buf, IOptionsMonitor<ConnectorOptions> opt, ConnectorPaths paths) =>
            Results.Json(Snapshot(st, cfg, api, buf, opt.CurrentValue, paths), Json));

        app.MapGet("/api/messages", (StatusState st) => Results.Json(st.Messages, Json));

        app.MapPost("/api/reload-config", async (AnalyzerManager mgr, CancellationToken ct) =>
            Results.Json(new { ok = true, message = await mgr.ReloadAsync(ct) }, Json));

        app.MapPost("/api/test-message", async (AnalyzerManager mgr, HttpRequest req, CancellationToken ct) =>
            Results.Json(new { ok = true, message = await mgr.SendTestMessageAsync(req.Query["analyzer"].ToString(), ct) }, Json));

        app.MapPost("/api/clear-buffer", async (OfflineBuffer buf, StatusState st, CancellationToken ct) =>
        {
            var n = await buf.ClearAsync(ct);
            st.BufferedCount = 0;
            return Results.Json(new { ok = true, message = $"Буфер очищено: видалено {n} елемент(ів)" }, Json);
        });

        app.MapGet("/healthz", () => Results.Text("OK"));
    }

    public static StatusSnapshot Snapshot(StatusState st, ConfigStore cfg, LisApiClient api, OfflineBuffer buf, ConnectorOptions opt, ConnectorPaths paths)
    {
        var current = cfg.Current;
        return new StatusSnapshot
        {
            Version = ConnectorInfo.Version,
            ConnectorId = current.ConnectorId,
            ConnectorName = opt.Local.ConnectorName,
            HostName = Environment.MachineName,
            ServerUrl = opt.Server.BaseUrl,
            ServerConfigured = opt.Server.IsConfigured,
            Online = st.Online,
            LastHeartbeatAt = st.LastHeartbeatAt,
            LastServerError = st.LastServerError ?? api.LastError,
            ConfigVersion = current.ConfigVersion,
            ConfigSyncedAt = cfg.LastServerSyncAt,
            StartedAtUtc = ConnectorInfo.StartedAtUtc,
            UptimeSec = (long)(DateTime.UtcNow - ConnectorInfo.StartedAtUtc).TotalSeconds,
            BufferedCount = buf.Count,
            DataDir = paths.DataDir,
            Analyzers = st.Analyzers.ToList(),
            Messages = st.Messages.ToList(),
        };
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string T(DateTime? dt) => dt.HasValue ? dt.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") : "—";

    public static string RenderHtml(StatusSnapshot s)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"uk\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<title>MedLink LIS Analyzer Connector</title><meta http-equiv=\"refresh\" content=\"15\">");
        sb.Append("<style>");
        sb.Append("body{font-family:Segoe UI,Roboto,Arial,sans-serif;margin:0;background:#f4f6f9;color:#222}");
        sb.Append("header{background:linear-gradient(90deg,#0178BC,#318F94,#5EC58C);color:#fff;padding:14px 24px;display:flex;justify-content:space-between;align-items:center}");
        sb.Append("header h1{font-size:20px;margin:0}header small{opacity:.9}main{padding:16px 24px;max-width:1400px;margin:0 auto}");
        sb.Append(".cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:12px;margin-bottom:16px}");
        sb.Append(".card{background:#fff;border-radius:8px;padding:12px 16px;box-shadow:0 1px 3px rgba(0,0,0,.08)}.card .k{color:#666;font-size:12px}.card .v{font-size:16px;font-weight:600;word-break:break-all}");
        sb.Append(".ok{color:#1e8e3e}.bad{color:#c62828}.warn{color:#e68a00}");
        sb.Append("table{width:100%;border-collapse:collapse;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,.08);margin-bottom:16px}");
        sb.Append("th,td{padding:8px 10px;border-bottom:1px solid #eee;text-align:left;font-size:13px;vertical-align:top}th{background:#4274A7;color:#fff;font-weight:600}");
        sb.Append("td.mono{font-family:Consolas,monospace;font-size:12px;white-space:pre-wrap;word-break:break-all;max-width:700px}");
        sb.Append(".badge{display:inline-block;padding:2px 8px;border-radius:10px;font-size:12px;color:#fff}.b-ok{background:#1e8e3e}.b-bad{background:#c62828}.b-gray{background:#888}.b-blue{background:#4274A7}");
        sb.Append(".actions{margin:8px 0 16px}.actions button{background:#4274A7;color:#fff;border:0;border-radius:6px;padding:8px 14px;margin-right:8px;cursor:pointer;font-size:14px}.actions button:hover{background:#0178BC}");
        sb.Append("#msg{margin-left:8px;font-size:13px;color:#333}h2{font-size:16px;margin:16px 0 8px}footer{color:#777;font-size:12px;padding:12px 24px;text-align:center}");
        sb.Append("</style></head><body>");
        sb.Append("<header><h1>MedLink LIS Analyzer Connector <small>v").Append(H(s.Version)).Append("</small></h1><small>ТОВ «МедЛінк» · ").Append(H(s.HostName)).Append("</small></header><main>");

        var online = s.ServerConfigured ? (s.Online ? "<span class=ok>● онлайн</span>" : "<span class=bad>● офлайн</span>") : "<span class=warn>● сервер не налаштовано</span>";
        sb.Append("<div class=cards>");
        Card(sb, "Коннектор", string.IsNullOrEmpty(s.ConnectorId) ? "— (не зареєстровано)" : H(s.ConnectorName ?? "") + (s.ConnectorName != null ? "<br>" : "") + "<span style='font-weight:400;font-size:12px'>" + H(s.ConnectorId) + "</span>");
        Card(sb, "Сервер ЛІС", H(s.ServerUrl ?? "—") + "<br>" + online);
        Card(sb, "Останній heartbeat", T(s.LastHeartbeatAt) + (s.LastServerError != null ? "<br><span class=bad style='font-weight:400;font-size:12px'>" + H(s.LastServerError) + "</span>" : ""));
        Card(sb, "Конфігурація", $"версія {s.ConfigVersion}<br><span style='font-weight:400;font-size:12px'>синхронізовано {T(s.ConfigSyncedAt)}</span>");
        Card(sb, "Офлайн-буфер", s.BufferedCount == 0 ? "<span class=ok>порожній</span>" : $"<span class=warn>{s.BufferedCount} елемент(ів) очікують</span>");
        Card(sb, "Час роботи", TimeSpan.FromSeconds(s.UptimeSec).ToString(@"d\д\ hh\:mm\:ss") + "<br><span style='font-weight:400;font-size:12px'>з " + T(s.StartedAtUtc) + "</span>");
        sb.Append("</div>");

        sb.Append("<div class=actions>");
        sb.Append("<button onclick=\"act('/api/reload-config')\">Перечитати конфігурацію</button>");
        sb.Append("<button onclick=\"act('/api/test-message?analyzer='+encodeURIComponent(document.getElementById('an').value))\">Відправити тестове повідомлення</button>");
        sb.Append("<select id=an style='padding:7px;border-radius:6px;border:1px solid #ccc;margin-right:8px'>");
        foreach (var a in s.Analyzers) sb.Append("<option value=\"").Append(H(a.AnalyzerId)).Append("\">").Append(H(a.Code)).Append(" — ").Append(H(a.Name)).Append("</option>");
        sb.Append("</select>");
        sb.Append("<button onclick=\"if(confirm('Видалити всі невідправлені результати з буфера?'))act('/api/clear-buffer')\" style='background:#c62828'>Очистити буфер</button>");
        sb.Append("<span id=msg></span></div>");

        sb.Append("<h2>Аналізатори</h2><table><tr><th>Код / назва</th><th>Тип</th><th>Протокол</th><th>Підключення</th><th>Стан</th><th>Останнє повідомлення</th><th>Вх / Вих / Результати</th><th>Остання помилка</th></tr>");
        if (s.Analyzers.Count == 0) sb.Append("<tr><td colspan=8>Аналізаторів не налаштовано. Додайте їх у ЛІС (Аналізатори → Коннектор) або у appsettings.json → Analyzers.</td></tr>");
        foreach (var a in s.Analyzers)
        {
            sb.Append("<tr><td><b>").Append(H(a.Code)).Append("</b><br>").Append(H(a.Name)).Append("</td>");
            sb.Append("<td>").Append(H(a.TypeCode)).Append("<br><span style='color:#777;font-size:11px'>").Append(H(a.ParserKind)).Append(" / ").Append(H(a.OrderTemplate)).Append("</span></td>");
            sb.Append("<td>").Append(H(a.Protocol)).Append("</td><td>").Append(H(a.Connection)).Append("</td>");
            sb.Append("<td>").Append(a.IsConnected ? "<span class='badge b-ok'>підключено</span>" : a.IsRunning ? "<span class='badge b-gray'>очікує</span>" : "<span class='badge b-bad'>не запущено</span>").Append("</td>");
            sb.Append("<td>").Append(T(a.LastMessageAt)).Append("</td>");
            sb.Append("<td>").Append(a.MessagesIn).Append(" / ").Append(a.MessagesOut).Append(" / ").Append(a.ResultsSent).Append("</td>");
            sb.Append("<td class=bad style='font-size:12px'>").Append(H(a.LastError)).Append(a.LastErrorAt.HasValue ? "<br><span style='color:#777'>" + T(a.LastErrorAt) + "</span>" : "").Append("</td></tr>");
        }
        sb.Append("</table>");

        sb.Append("<h2>Останні повідомлення (50)</h2><table><tr><th>Час</th><th>Аналізатор</th><th>Напрям</th><th>Вид</th><th>Рез.</th><th>Вміст</th></tr>");
        if (s.Messages.Count == 0) sb.Append("<tr><td colspan=6>Повідомлень ще не було.</td></tr>");
        foreach (var m in s.Messages)
        {
            var dir = m.Direction == "IN" ? "<span class='badge b-blue'>← IN</span>" : m.Direction == "OUT" ? "<span class='badge b-ok'>→ OUT</span>" : "<span class='badge b-gray'>SYS</span>";
            sb.Append("<tr><td>").Append(T(m.At)).Append("</td><td>").Append(H(m.AnalyzerCode)).Append("</td><td>").Append(dir).Append("</td>");
            sb.Append("<td>").Append(H(m.Kind)).Append(m.ParsedOk ? "" : " <span class=bad>✖</span>").Append("</td><td>").Append(m.ResultsCount > 0 ? m.ResultsCount.ToString() : "").Append("</td>");
            sb.Append("<td class=mono>").Append(H(m.Preview)).Append(m.Error != null ? "<br><span class=bad>" + H(m.Error) + "</span>" : "").Append("</td></tr>");
        }
        sb.Append("</table>");
        sb.Append("<p style='color:#777;font-size:12px'>Каталог даних: ").Append(H(s.DataDir)).Append(" · JSON: <a href='/status.json'>/status.json</a> · сторінка оновлюється кожні 15 с</p>");
        sb.Append("</main><footer>MedLink LIS 4.0 · Analyzer Connector · © 2026 ТОВ «МедЛінк»</footer>");
        sb.Append("<script>function act(u){var m=document.getElementById('msg');m.textContent='…';fetch(u,{method:'POST'}).then(r=>r.json()).then(j=>{m.textContent=j.message||'OK';setTimeout(()=>location.reload(),1500)}).catch(e=>{m.textContent='Помилка: '+e})}</script>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void Card(StringBuilder sb, string k, string vHtml)
        => sb.Append("<div class=card><div class=k>").Append(H(k)).Append("</div><div class=v>").Append(vHtml).Append("</div></div>");
}
