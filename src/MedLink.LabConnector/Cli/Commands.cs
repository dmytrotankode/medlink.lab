// =============================================================================
// MedLink LIS Analyzer Connector — CLI-команди: setup, test, preview-order, status,
// install-service / uninstall-service, list-profiles, help.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MedLink.LabConnector.Configuration;
using MedLink.LabConnector.Pipeline;
using MedLink.LabConnector.Server;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;
using MedLink.LIS.Core.Protocols.Orders;
using MedLink.LIS.Core.Protocols.Parsers;

namespace MedLink.LabConnector.Cli;

public static class Commands
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static void PrintHelp()
    {
        Console.WriteLine($"""
        MedLink LIS Analyzer Connector {ConnectorInfo.Version} — ТОВ «МедЛінк»

        Використання: MedLink.LabConnector <команда> [параметри]

          run                      Запуск служби (за замовчуванням). Сторінка статусу: http://localhost:5088/
          setup --server URL --install-key KEY [--name NAME] [--data-dir DIR] [--status-port 5088] [--insecure]
                                   Реєстрація на сервері ЛІС, запис data/appsettings.local.json, завантаження конфігурації
          test --analyzer CODE --file PATH [--json]
                                   Прогнати файл повідомлення через парсер профілю (CODE — код типу з каталогу або код аналізатора)
          preview-order --analyzer CODE --barcode B [--tests GLU,ALT] [--position "S1^SC"]
                                   Побудувати замовлення для приладу з тестового AnalyzerOrderDto
          status [--status-port 5088]
                                   Показати стан запущеної служби (GET /status.json)
          install-service [--data-dir DIR]     Зареєструвати службу (Windows: sc.exe; Linux: systemd unit)
          uninstall-service                    Видалити службу
          list-profiles [--json]               Перелік профілів типів аналізаторів (parserKind / orderTemplate)
          version | help

        Змінна оточення MEDLINK_CONNECTOR_DATA задає каталог даних (за замовчуванням ./data біля програми).
        """);
    }

    // ------------------------------------------------------------------ setup

    public static async Task<int> SetupAsync(CommandLine cli)
    {
        var server = cli.Require("server");
        var key = cli.Require("install-key");
        var name = cli.Get("name");
        var paths = ConnectorPaths.FromEnvironment(cli.Get("data-dir"));
        bool insecure = cli.Has("insecure");
        int? statusPort = int.TryParse(cli.Get("status-port"), out var sp) ? sp : null;

        Console.WriteLine($"Реєстрація коннектора на {ServerOptions.NormalizeBaseUrl(server)} …");
        var resp = await LisApiClient.RegisterAsync(server, key, name, insecure, CancellationToken.None);
        Console.WriteLine($"  ✔ Зареєстровано. ConnectorId = {resp.ConnectorId}");
        ConfigStore.WriteLocalSettings(paths, server, resp.ApiKey, resp.ConnectorId, name, statusPort);
        Console.WriteLine($"  ✔ Налаштування збережено: {paths.LocalSettingsFile}");

        // Завантажуємо конфігурацію та показуємо підсумок
        try
        {
            using var client = LisApiClient.CreateClient(server, resp.ApiKey, 30, insecure);
            var cfg = await client.GetFromJsonAsync<ConnectorConfigDto>("connector/config", LisApiClient.Json);
            if (cfg != null)
            {
                File.WriteAllText(paths.ConfigCacheFile, JsonSerializer.Serialize(cfg, Json));
                Console.WriteLine($"  ✔ Конфігурація отримана (версія {cfg.ConfigVersion}), аналізаторів: {cfg.Analyzers.Count}");
                foreach (var a in cfg.Analyzers)
                {
                    ConfigStore.ApplyProfileDefaults(a);
                    Console.WriteLine($"     • {a.Code,-16} {a.Name,-28} {a.Protocol,-8} {Status.StatusState.DescribeConnection(a.Connection)}  шаблон {a.OrderTemplate}");
                }
                if (cfg.Analyzers.Count == 0) Console.WriteLine("     (аналізаторів ще не призначено — додайте їх у ЛІС: Аналізатори → Коннектор)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ! Конфігурацію не завантажено: {ex.Message} (буде отримано при запуску служби)");
        }
        Console.WriteLine();
        Console.WriteLine("Готово. Запустіть службу: MedLink.LabConnector run  (або install-service для автозапуску).");
        Console.WriteLine($"Сторінка статусу: http://localhost:{statusPort ?? 5088}/");
        return 0;
    }

    // ------------------------------------------------------------------ test

    public static int Test(CommandLine cli)
    {
        var code = cli.Require("analyzer");
        var file = cli.Require("file");
        if (!File.Exists(file)) throw new FileNotFoundException("Файл не знайдено", file);
        var cfg = ResolveConfig(code, cli.Get("data-dir"));
        var text = ProtocolText.LoadSample(File.ReadAllText(file, Encoding.UTF8));
        var parser = ParserFactory.ForConfig(cfg);
        var sw = Stopwatch.StartNew();
        var parsed = parser.Parse(cfg, text);
        sw.Stop();
        if (cli.Has("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { analyzer = cfg.Code, typeCode = cfg.TypeCode, parser = parser.Kind, parsed.Kind, parsed.Barcodes, parsed.SamplePosition, parsed.Patient, parsed.IsQc, parsed.Results, parsed.Warnings, immediateReply = parsed.ImmediateReply == null ? null : ProtocolText.Escape(parsed.ImmediateReply), batch = ResultPipeline.BuildBatch(cfg, parsed, DateTime.UtcNow) }, Json));
            return parsed.Kind == InboundMessageKind.Other ? 1 : 0;
        }
        Console.WriteLine($"Аналізатор: {cfg.Code} ({cfg.TypeCode}), протокол {cfg.Protocol}, парсер {parser.Kind}, {sw.ElapsedMilliseconds} мс");
        Console.WriteLine($"Вид повідомлення: {parsed.Kind}; штрихкоди: {string.Join(", ", parsed.Barcodes)}{(parsed.SamplePosition != null ? $"; позиція: {parsed.SamplePosition}" : "")}{(parsed.IsQc ? "; QC" : "")}");
        if (parsed.Patient != null) Console.WriteLine($"Пацієнт: {parsed.Patient.Id} {parsed.Patient.LastName} {parsed.Patient.FirstName} {parsed.Patient.BirthDate:yyyy-MM-dd} {parsed.Patient.Gender}");
        if (parsed.Results.Count > 0)
        {
            Console.WriteLine($"Результати ({parsed.Results.Count}):");
            foreach (var r in parsed.Results)
                Console.WriteLine($"  {r.Barcode,-14} {r.AnalyzerCode,-20} = {r.Value,-14} {r.Unit,-12} {r.Flags,-4} {r.ReferenceText}");
        }
        if (parsed.ImmediateReply != null) Console.WriteLine("Негайна відповідь приладу: " + ProtocolText.Escape(parsed.ImmediateReply));
        foreach (var w in parsed.Warnings) Console.WriteLine("  ! " + w);
        return parsed.Kind == InboundMessageKind.Other ? 1 : 0;
    }

    // ------------------------------------------------------------------ preview-order

    public static int PreviewOrder(CommandLine cli)
    {
        var code = cli.Require("analyzer");
        var barcode = cli.Require("barcode");
        var cfg = ResolveConfig(code, cli.Get("data-dir"));
        var tests = (cli.Get("tests") ?? "GLU,ALT,AST,CREA,UREA").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var order = new AnalyzerOrderDto
        {
            Barcode = barcode,
            OrderNumber = $"{DateTime.Now:yyMM}-000001",
            Priority = cli.Has("cito") ? "S" : "R",
            SampleType = cli.Get("sample-type") ?? "Serum",
            Patient = new AnalyzerOrderPatientDto { Id = "108291", LastName = "Коваленко", FirstName = "Олена", BirthDate = new DateTime(1985, 4, 12), Gender = "F" },
            Tests = tests.Select(t => new AnalyzerOrderTestDto { TestCode = t, AnalyzerCode = t }).ToList(),
        };
        var builder = OrderBuilderFactory.ForConfig(cfg);
        var ctx = new OrderBuildContext { RawQueryToken = cli.Get("query-token"), SamplePosition = cli.Get("position"), SendNoOrderReply = true };
        var records = builder.BuildRecords(order, cfg, DateTime.Now, ctx);
        Console.WriteLine($"Аналізатор: {cfg.Code} ({cfg.TypeCode}); шаблон {builder.Template}; вихід {builder.Output}");
        Console.WriteLine("Записи:");
        foreach (var r in records) Console.WriteLine("  " + ProtocolText.Escape(r));
        if (builder.Output == OrderOutputKind.AstmRecords)
        {
            var frames = AstmFrameCodec.BuildFrames(records, cfg.Framing.MaxFrameLen, cfg.Framing.Checksum);
            Console.WriteLine($"Кадри ASTM ({frames.Count}):");
            foreach (var f in frames) Console.WriteLine("  " + ProtocolText.Escape(f));
        }
        else if (builder.Output == OrderOutputKind.Hl7Messages)
        {
            Console.WriteLine("MLLP:");
            foreach (var m in records) Console.WriteLine("  " + ProtocolText.Escape(MllpCodec.WrapString(m)));
        }
        return 0;
    }

    // ------------------------------------------------------------------ status

    public static async Task<int> StatusAsync(CommandLine cli)
    {
        int port = int.TryParse(cli.Get("status-port"), out var p) ? p : 5088;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            var json = await client.GetStringAsync($"http://localhost:{port}/status.json");
            if (cli.Has("json")) { Console.WriteLine(json); return 0; }
            var s = JsonSerializer.Deserialize<Status.StatusSnapshot>(json, Json)!;
            Console.WriteLine($"MedLink LIS Analyzer Connector {s.Version} на {s.HostName}");
            Console.WriteLine($"  Коннектор:   {s.ConnectorId} {s.ConnectorName}");
            Console.WriteLine($"  Сервер:      {s.ServerUrl} — {(s.ServerConfigured ? (s.Online ? "онлайн" : "ОФЛАЙН") : "не налаштовано")}  heartbeat {s.LastHeartbeatAt?.ToLocalTime():HH:mm:ss}  {s.LastServerError}");
            Console.WriteLine($"  Конфігурація: версія {s.ConfigVersion}, синхронізовано {s.ConfigSyncedAt?.ToLocalTime():dd.MM HH:mm:ss}; буфер: {s.BufferedCount}; uptime {TimeSpan.FromSeconds(s.UptimeSec)}");
            Console.WriteLine("  Аналізатори:");
            foreach (var a in s.Analyzers)
                Console.WriteLine($"    {a.Code,-14} {a.Protocol,-8} {a.Connection,-30} {(a.IsConnected ? "підключено" : "—"),-12} ост.повід. {a.LastMessageAt?.ToLocalTime():HH:mm:ss}  вх {a.MessagesIn} вих {a.MessagesOut} рез {a.ResultsSent}  {a.LastError}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Служба не відповідає на http://localhost:{port}/status.json: {ex.Message}");
            return 1;
        }
    }

    // ------------------------------------------------------------------ service

    public static int InstallService(CommandLine cli)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Не вдалося визначити шлях до виконуваного файлу");
        var dataDir = cli.Get("data-dir");
        if (OperatingSystem.IsWindows())
        {
            var bin = $"\"{exe}\" run" + (dataDir != null ? $" --data-dir \"{dataDir}\"" : "");
            Run("sc.exe", $"stop {ConnectorInfo.ServiceName}", ignoreErrors: true);
            Run("sc.exe", $"delete {ConnectorInfo.ServiceName}", ignoreErrors: true);
            Run("sc.exe", $"create {ConnectorInfo.ServiceName} binPath= \"{bin.Replace("\"", "\\\"")}\" start= delayed-auto DisplayName= \"{ConnectorInfo.DisplayName}\"");
            Run("sc.exe", $"description {ConnectorInfo.ServiceName} \"Коннектор лабораторних аналізаторів MedLink LIS (ASTM/HL7). ТОВ «МедЛінк».\"");
            Run("sc.exe", $"failure {ConnectorInfo.ServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000");
            Run("sc.exe", $"start {ConnectorInfo.ServiceName}", ignoreErrors: true);
            Console.WriteLine($"Службу {ConnectorInfo.ServiceName} зареєстровано та запущено.");
            return 0;
        }
        var unit = $"""
        [Unit]
        Description=MedLink LIS Analyzer Connector
        Documentation=https://medlink.ua/docs/lis/connector
        After=network-online.target
        Wants=network-online.target

        [Service]
        Type=notify
        WorkingDirectory={Path.GetDirectoryName(exe)}
        ExecStart={exe} run{(dataDir != null ? $" --data-dir {dataDir}" : "")}
        Restart=always
        RestartSec=10
        KillSignal=SIGINT
        SyslogIdentifier=medlink-labconnector
        User=medlink
        Group=medlink
        SupplementaryGroups=dialout
        Environment=DOTNET_ENVIRONMENT=Production
        Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1

        [Install]
        WantedBy=multi-user.target
        """;
        var unitPath = "/etc/systemd/system/medlink-labconnector.service";
        File.WriteAllText(unitPath, unit);
        Run("systemctl", "daemon-reload");
        Run("systemctl", "enable --now medlink-labconnector");
        Console.WriteLine($"systemd unit записано у {unitPath}, службу увімкнено та запущено.");
        return 0;
    }

    public static int UninstallService(CommandLine cli)
    {
        if (OperatingSystem.IsWindows())
        {
            Run("sc.exe", $"stop {ConnectorInfo.ServiceName}", ignoreErrors: true);
            Run("sc.exe", $"delete {ConnectorInfo.ServiceName}");
            Console.WriteLine("Службу видалено.");
            return 0;
        }
        Run("systemctl", "disable --now medlink-labconnector", ignoreErrors: true);
        var unitPath = "/etc/systemd/system/medlink-labconnector.service";
        if (File.Exists(unitPath)) File.Delete(unitPath);
        Run("systemctl", "daemon-reload", ignoreErrors: true);
        Console.WriteLine("systemd-службу видалено.");
        return 0;
    }

    private static void Run(string file, string args, bool ignoreErrors = false)
    {
        Console.WriteLine($"> {file} {args}");
        var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Не вдалося запустити {file}");
        var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (!string.IsNullOrWhiteSpace(output)) Console.WriteLine(output.Trim());
        if (p.ExitCode != 0 && !ignoreErrors) throw new InvalidOperationException($"{file} завершився з кодом {p.ExitCode}");
    }

    // ------------------------------------------------------------------ list-profiles

    public static int ListProfiles(CommandLine cli)
    {
        var all = AnalyzerProfileCatalog.Default.All;
        if (cli.Has("json")) { Console.WriteLine(JsonSerializer.Serialize(all, Json)); return 0; }
        Console.WriteLine($"{"ID",3} {"Код",-18} {"Назва",-24} {"Категорія",-10} {"Протокол",-9} {"Парсер",-15} {"Шаблон замовлення",-16} bop/eop");
        foreach (var p in all)
            Console.WriteLine($"{p.Id,3} {p.Code,-18} {Trunc(p.Name, 24),-24} {p.Category,-10} {p.ExchType,-9} {p.ParserKind,-15} {p.OrderTemplate,-16} {Describe(p.Bop)}/{Describe(p.Eop)}");
        Console.WriteLine($"Усього профілів: {all.Count}");
        return 0;
    }

    private static string Trunc(string s, int n) => s.Length > n ? s[..(n - 1)] + "…" : s;
    private static string Describe(byte[]? b) => b == null ? "-" : ProtocolText.Escape(Encoding.Latin1.GetString(b));

    /// <summary>Конфігурація для CLI: із кешу/локальних налаштувань за кодом аналізатора, інакше — профіль типу за замовчуванням.</summary>
    private static AnalyzerConfigDto ResolveConfig(string code, string? dataDir)
    {
        var paths = ConnectorPaths.FromEnvironment(dataDir);
        try
        {
            if (File.Exists(paths.ConfigCacheFile))
            {
                var cfg = JsonSerializer.Deserialize<ConnectorConfigDto>(File.ReadAllText(paths.ConfigCacheFile), LisApiClient.Json);
                var a = cfg?.Analyzers.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase) || x.AnalyzerId == code);
                if (a != null) { ConfigStore.ApplyProfileDefaults(a); return a; }
            }
        }
        catch { /* кеш необов'язковий */ }
        try
        {
            var appsettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(appsettings))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(appsettings), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (doc.RootElement.TryGetProperty("Analyzers", out var arr))
                {
                    var list = JsonSerializer.Deserialize<List<AnalyzerConfigDto>>(arr.GetRawText(), LisApiClient.Json) ?? new();
                    var a = list.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
                    if (a != null) { ConfigStore.ApplyProfileDefaults(a); return a; }
                }
            }
        }
        catch { }
        return AnalyzerProfileCatalog.Default.CreateDefaultConfig(code, "cli");
    }
}
