// =============================================================================
// MedLink LIS Analyzer Connector — HTTP-ендпоінти агента друку етикеток для SPA:
// POST /print/zpl, GET /print/printers, POST /print/test (CORS — будь-яке походження).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text.Json;

namespace MedLink.LabConnector.Printing;

public static class PrintEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void Map(WebApplication app)
    {
        app.MapPost("/print/zpl", async (PrintRequest req, LabelPrintService svc, CancellationToken ct) =>
        {
            var r = await svc.PrintZplAsync(req, ct);
            return r.Ok ? Results.Json(r, Json) : Results.Json(r, Json, statusCode: 502);
        });

        app.MapGet("/print/printers", async (LabelPrintService svc, CancellationToken ct) =>
            Results.Json(new { printers = await LabelPrintService.ListPrintersAsync(ct), @default = svc.DefaultPrinter.ToString() }, Json));

        app.MapPost("/print/test", async (HttpRequest http, LabelPrintService svc, CancellationToken ct) =>
        {
            PrinterTarget? target = null;
            if (http.ContentLength > 0)
            {
                try { target = await http.ReadFromJsonAsync<PrinterTarget>(Json, ct); } catch { /* тіло необов'язкове */ }
            }
            var r = await svc.PrintZplAsync(new PrintRequest { Zpl = LabelPrintService.TestLabelZpl(Environment.MachineName), Printer = target }, ct);
            return r.Ok ? Results.Json(r, Json) : Results.Json(r, Json, statusCode: 502);
        });
    }
}
