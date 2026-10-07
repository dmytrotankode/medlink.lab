// Коннектор: реєстрація за install key → apiKey (SHA-256 hash), конфіг, heartbeat, замовлення для приладів,
// прийом результатів через ResultPipelineService, журнал обміну, логи, ZIP для інсталяції
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Core.Common;
using MedLink.LIS.Core.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class ConnectorCreateRequest
{
    public string Name { get; set; } = "";
    public int PollIntervalSec { get; set; } = 30;
    public int HeartbeatIntervalSec { get; set; } = 30;
}

public sealed class ConnectorService
{
    public const string ApiKeyHeader = "X-MedLink-ApiKey";
    private readonly LisDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRolePolicy _policy;
    private readonly ResultPipelineService _pipeline;
    private readonly QcService _qc;
    private readonly IConfiguration _config;

    public ConnectorService(LisDbContext db, IAuditService audit, IRolePolicy policy, ResultPipelineService pipeline, QcService qc, IConfiguration config)
    {
        _db = db; _audit = audit; _policy = policy; _pipeline = pipeline; _qc = qc; _config = config;
    }

    public static string Hash(string apiKey) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey))).ToLowerInvariant();

    public static string NewApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string NewInstallKey() => "MLK-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToUpperInvariant();

    public async Task<LabConnectorInstallation?> ResolveByApiKeyAsync(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;
        var hash = Hash(apiKey);
        var c = await _db.Connectors.FirstOrDefaultAsync(x => x.ApiKeyHash == hash && !x.IsDeleted);
        return c == null || c.Status == LisStateMachine.ConnectorDisabled ? null : c;
    }

    // ------------------------------------------------------------------ admin CRUD
    public async Task<List<object>> ListAsync()
    {
        var items = await _db.Connectors.AsNoTracking().Include(c => c.Analyzers).Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToListAsync();
        return items.Select(ToDto).ToList<object>();
    }

    public async Task<object> GetAsync(string id) => ToDto(await LoadAsync(id));

    private async Task<LabConnectorInstallation> LoadAsync(string id) =>
        await _db.Connectors.Include(c => c.Analyzers).ThenInclude(a => a.AnalyzerType).FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted) ?? throw NotFoundException.For("Коннектор", id);

    private object ToDto(LabConnectorInstallation c) => new
    {
        c.Id, c.Name, c.HostName, c.OsDescription, c.Version, c.Status, c.InstallKey, c.InstallKeyUsed, hasApiKey = c.ApiKeyHash != null,
        c.LastHeartbeatAt, c.RegisteredAt, c.BufferedCount, c.UptimeSec, c.ConfigVersion, c.PollIntervalSec, c.HeartbeatIntervalSec, c.CreatedOn,
        isOnline = c.LastHeartbeatAt.HasValue && (DateTime.UtcNow - c.LastHeartbeatAt.Value).TotalSeconds < c.HeartbeatIntervalSec * 3,
        setupCommand = SetupCommand(c), analyzers = c.Analyzers.Select(a => new { a.Id, a.Code, a.Name, a.IsOnline, a.LastMessageAt, a.LastError, typeCode = a.AnalyzerType?.Code }),
        allowedActions = _policy.AllowedActions(LisEntities.Connector, c.Status), stateMachine = LisStateMachine.Describe(LisEntities.Connector)
    };

    private string PublicBaseUrl() => (_config["Lab:PublicBaseUrl"] ?? "http://localhost:5055").TrimEnd('/');
    private string SetupCommand(LabConnectorInstallation c) => $"MedLink.LabConnector setup --server {PublicBaseUrl()} --install-key {c.InstallKey} --name \"{c.Name}\"";

    public async Task<object> CreateAsync(ConnectorCreateRequest req)
    {
        _policy.Require("Створення інсталяції коннектора", LabRoles.Admin);
        if (string.IsNullOrWhiteSpace(req.Name)) throw ValidationException.Field("name", "Вкажіть назву інсталяції");
        var c = new LabConnectorInstallation { Name = req.Name.Trim(), InstallKey = NewInstallKey(), Status = LisStateMachine.ConnectorPending, PollIntervalSec = req.PollIntervalSec, HeartbeatIntervalSec = req.HeartbeatIntervalSec };
        _db.Connectors.Add(c);
        _audit.Log("CREATE", "lab_connector_installation", c.Id, null, new { c.Name });
        await _db.SaveChangesAsync();
        return new { id = c.Id, installKey = c.InstallKey, setupCommand = SetupCommand(c), connector = ToDto(c) };
    }

    public async Task<object> UpdateAsync(string id, ConnectorCreateRequest req)
    {
        var c = await LoadAsync(id);
        _policy.Ensure(LisEntities.Connector, ConnectorActions.Edit, c.Status, $"Коннектор {c.Name}");
        var before = new { c.Name, c.PollIntervalSec, c.HeartbeatIntervalSec };
        if (!string.IsNullOrWhiteSpace(req.Name)) c.Name = req.Name.Trim();
        if (req.PollIntervalSec > 0) c.PollIntervalSec = req.PollIntervalSec;
        if (req.HeartbeatIntervalSec > 0) c.HeartbeatIntervalSec = req.HeartbeatIntervalSec;
        c.ConfigVersion++;
        _audit.Log("UPDATE", "lab_connector_installation", c.Id, before, req);
        await _db.SaveChangesAsync();
        return ToDto(c);
    }

    public async Task<object> TransitionAsync(string id, string action)
    {
        var c = await LoadAsync(id);
        var rule = _policy.Ensure(LisEntities.Connector, action, c.Status, $"Коннектор {c.Name}");
        var before = c.Status;
        if (rule.ToStatus != null) c.Status = rule.ToStatus;
        if (action == ConnectorActions.Enable) { c.InstallKey = NewInstallKey(); c.InstallKeyUsed = false; c.ApiKeyHash = null; }
        _audit.Log(action, "lab_connector_installation", c.Id, new { status = before }, new { c.Status });
        await _db.SaveChangesAsync();
        return ToDto(c);
    }

    /// <summary>Перевипуск apiKey: новий ключ повертається один раз; старий — недійсний.</summary>
    public async Task<object> RotateKeyAsync(string id)
    {
        var c = await LoadAsync(id);
        _policy.Ensure(LisEntities.Connector, ConnectorActions.RotateKey, c.Status, $"Коннектор {c.Name}");
        var apiKey = NewApiKey();
        c.ApiKeyHash = Hash(apiKey);
        c.ConfigVersion++;
        if (c.Status == LisStateMachine.ConnectorDisabled) c.Status = LisStateMachine.ConnectorOffline;
        _db.ConnectorCommands.Add(new LabConnectorCommand { ConnectorId = c.Id, Type = "RELOAD_CONFIG" });
        _audit.Log("ROTATE_KEY", "lab_connector_installation", c.Id, null, new { c.ConfigVersion });
        await _db.SaveChangesAsync();
        return new { connectorId = c.Id, apiKey, serverTimeUtc = DateTime.UtcNow, note = "Збережіть ключ: він показується лише один раз" };
    }

    public async Task DeleteAsync(string id)
    {
        var c = await LoadAsync(id);
        _policy.Ensure(LisEntities.Connector, ConnectorActions.Delete, c.Status, $"Коннектор {c.Name}");
        foreach (var a in c.Analyzers) a.ConnectorId = null;
        _db.Connectors.Remove(c);
        _audit.Log("DELETE", "lab_connector_installation", id, new { c.Name }, null);
        await _db.SaveChangesAsync();
    }

    public async Task<PagedResult<LabConnectorLog>> LogsAsync(string id, PagingQuery paging) =>
        await _db.ConnectorLogs.AsNoTracking().Where(l => l.ConnectorId == id).OrderByDescending(l => l.At).ToPagedAsync(paging);

    public async Task QueueCommandAsync(string id, string type, string? payload)
    {
        _policy.Require("Команда коннектору", LabRoles.Admin);
        await LoadAsync(id);
        _db.ConnectorCommands.Add(new LabConnectorCommand { ConnectorId = id, Type = type, Payload = payload });
        _audit.Log("COMMAND", "lab_connector_installation", id, null, new { type, payload });
        await _db.SaveChangesAsync();
    }

    public async Task<byte[]> DownloadZipAsync(string id, string serverUrl)
    {
        var c = await LoadAsync(id);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var settings = System.Text.Json.JsonSerializer.Serialize(new
            {
                Server = new { Url = serverUrl, InstallKey = c.InstallKey, ConnectorName = c.Name, PollIntervalSec = c.PollIntervalSec, HeartbeatIntervalSec = c.HeartbeatIntervalSec },
                Connector = new { ApiKey = (string?)null, DataDirectory = "data", StatusPort = 5088 }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await WriteEntryAsync(zip, "appsettings.local.json", settings);
            await WriteEntryAsync(zip, "README_UA.txt", ReadmeText(c, serverUrl));
            await WriteEntryAsync(zip, "install-windows.cmd",
                $"@echo off\r\nREM MedLink LabConnector — реєстрація та встановлення служби Windows\r\nMedLink.LabConnector.exe setup --server {serverUrl} --install-key {c.InstallKey} --name \"{c.Name}\"\r\nMedLink.LabConnector.exe install-service\r\nsc start MedLinkLabConnector\r\n");
            await WriteEntryAsync(zip, "install-linux.sh",
                $"#!/usr/bin/env bash\nset -e\n# MedLink LabConnector — реєстрація та systemd\n./MedLink.LabConnector setup --server {serverUrl} --install-key {c.InstallKey} --name \"{c.Name}\"\nsudo ./MedLink.LabConnector install-service\nsudo systemctl enable --now medlink-labconnector\n");
        }
        return ms.ToArray();
    }

    private static async Task WriteEntryAsync(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var s = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        await s.WriteAsync(bytes);
    }

    private static string ReadmeText(LabConnectorInstallation c, string serverUrl) =>
$@"MedLink LabConnector — інструкція з встановлення (інсталяція «{c.Name}»)
=====================================================================

Сервер ЛІС:      {serverUrl}
Ключ інсталяції: {c.InstallKey}   (одноразовий — після реєстрації стає недійсним)

1. Розпакуйте архів у каталог поруч із виконуваним файлом MedLink.LabConnector
   (self-contained збірка з installers/labconnector або артефакт publish).
   Файл appsettings.local.json з цього архіву вже містить адресу сервера та ключ.

2. Реєстрація (виконати один раз, потрібен доступ до сервера):
   MedLink.LabConnector setup --server {serverUrl} --install-key {c.InstallKey} --name ""{c.Name}""
   Команда отримає apiKey і збереже його в appsettings.local.json.

3. Windows (служба):
   - запустіть install-windows.cmd від імені адміністратора, або
   - MedLink.LabConnector install-service  →  sc start MedLinkLabConnector
   Інсталятор Inno Setup: installers/labconnector/MedLink_LabConnector_Setup.iss

4. Linux (systemd):
   - chmod +x install-linux.sh && sudo ./install-linux.sh
   - або: sudo ./MedLink.LabConnector install-service && sudo systemctl enable --now medlink-labconnector

5. Перевірка:
   - локальна сторінка статусу: http://localhost:5088/ (GET /status.json)
   - у ЛІС: Аналізатори → Коннектори → «{c.Name}» має статус ACTIVE і свіжий heartbeat.
   - тест парсера: MedLink.LabConnector test --analyzer SYSMEX_XN1000 --file sample.txt

Мережа: для TCP-серверних приладів відкрийте вхідні порти (напр. 5100 для Sysmex XN),
для TCP-клієнтів — доступ до IP приладу. Для COM — права на /dev/ttyS* (Linux, група dialout).
Офлайн: результати буферизуються у data/offline_buffer.db і досилаються при відновленні зв'язку.
";

    // ------------------------------------------------------------------ connector protocol
    public async Task<ConnectorRegisterResponse> RegisterAsync(ConnectorRegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.InstallKey)) throw ValidationException.Field("installKey", "Ключ інсталяції обов'язковий");
        var c = await _db.Connectors.FirstOrDefaultAsync(x => x.InstallKey == req.InstallKey.Trim() && !x.IsDeleted)
                ?? throw new ForbiddenConnectorException("Невідомий ключ інсталяції");
        if (c.InstallKeyUsed || c.Status != LisStateMachine.ConnectorPending)
            throw new ConflictException($"Ключ інсталяції вже використано (статус {c.Status}). Адміністратор може перевипустити ключ (Enable/Rotate key)");
        LisStateMachine.Ensure(LisEntities.Connector, ConnectorActions.Register, c.Status, LabRoles.System);
        var apiKey = NewApiKey();
        c.ApiKeyHash = Hash(apiKey);
        c.InstallKeyUsed = true;
        c.Status = LisStateMachine.ConnectorActive;
        c.HostName = req.HostName; c.OsDescription = req.OsDescription; c.Version = req.Version;
        c.RegisteredAt = DateTime.UtcNow; c.LastHeartbeatAt = DateTime.UtcNow;
        _db.AuditLog.Add(new LabAuditLog { UserId = "CONNECTOR:" + c.Id, UserName = c.Name, Action = "REGISTER", Entity = "lab_connector_installation", EntityId = c.Id, AfterJson = System.Text.Json.JsonSerializer.Serialize(new { req.HostName, req.OsDescription, req.Version }) });
        await _db.SaveChangesAsync();
        return new ConnectorRegisterResponse { ConnectorId = c.Id, ApiKey = apiKey, ServerTimeUtc = DateTime.UtcNow };
    }

    public async Task<ConnectorConfigDto> ConfigAsync(LabConnectorInstallation c)
    {
        var analyzers = await _db.Analyzers.AsNoTracking().Include(a => a.AnalyzerType).Include(a => a.ParameterMap)
            .Where(a => a.ConnectorId == c.Id && a.IsActive && !a.IsDeleted).OrderBy(a => a.Code).ToListAsync();
        return new ConnectorConfigDto
        {
            ConnectorId = c.Id, ConfigVersion = c.ConfigVersion, PollIntervalSec = c.PollIntervalSec, HeartbeatIntervalSec = c.HeartbeatIntervalSec,
            Analyzers = analyzers.Select(a => new AnalyzerConfigDto
            {
                AnalyzerId = a.Id, Code = a.Code, Name = a.Name, TypeCode = a.AnalyzerType?.Code ?? "",
                Protocol = a.ConnectionMode == "FILE" ? "FILE" : (a.AnalyzerType?.ExchType ?? "ASTM"),
                Connection = new AnalyzerConnectionDto
                {
                    Mode = a.ConnectionMode, Host = a.TcpHost, Port = a.TcpPort, IsServer = a.IsTcpServer, ComPort = a.ComPort, BaudRate = a.BaudRate, Parity = a.Parity,
                    DataBits = a.DataBits, StopBits = a.StopBits, FlowControl = a.FlowControl, FilePath = a.FilePath, FilePollSec = a.FilePollSec
                },
                Framing = new AnalyzerFramingDto
                {
                    BopBase64 = a.AnalyzerType?.BopBase64, EopBase64 = a.AnalyzerType?.EopBase64, Checksum = a.AnalyzerType?.ControlSum ?? true,
                    SleepMs = a.AnalyzerType?.SleepMs ?? 100, MaxFrameLen = 240, AckAfterRecord = true
                },
                AutoQueryOrders = a.AutoQueryOrders, OrderTemplate = a.AnalyzerType?.OrderTemplate ?? "ASTM_GENERIC",
                ParameterMap = a.ParameterMap.Select(m => new AnalyzerParameterMapDto { AnalyzerCode = m.AnalyzerCode, TestCode = m.TestCode, Factor = m.Factor, Offset = m.Offset, UnitOverride = m.UnitOverride }).ToList()
            }).ToList()
        };
    }

    public async Task<ConnectorHeartbeatResponse> HeartbeatAsync(LabConnectorInstallation c, ConnectorHeartbeatRequest req)
    {
        var now = DateTime.UtcNow;
        c.LastHeartbeatAt = now; c.Version = req.Version ?? c.Version; c.UptimeSec = req.UptimeSec; c.BufferedCount = req.BufferedCount;
        if (c.Status == LisStateMachine.ConnectorOffline) c.Status = LisStateMachine.ConnectorActive;
        var ids = req.Analyzers.Select(a => a.AnalyzerId).ToList();
        var analyzers = await _db.Analyzers.Where(a => ids.Contains(a.Id)).ToListAsync();
        foreach (var hb in req.Analyzers)
        {
            var a = analyzers.FirstOrDefault(x => x.Id == hb.AnalyzerId);
            if (a == null) continue;
            a.IsOnline = hb.IsConnected; a.LastError = hb.LastError;
            if (hb.LastMessageAt.HasValue) a.LastMessageAt = hb.LastMessageAt;
        }
        var commands = await _db.ConnectorCommands.Where(x => x.ConnectorId == c.Id && !x.IsDelivered).OrderBy(x => x.CreatedOn).ToListAsync();
        foreach (var cmd in commands) { cmd.IsDelivered = true; cmd.DeliveredAt = now; }
        await _db.SaveChangesAsync();
        return new ConnectorHeartbeatResponse { ServerTimeUtc = now, ConfigVersion = c.ConfigVersion, Commands = commands.Select(x => new ConnectorCommandDto { Type = x.Type, Payload = x.Payload }).ToList() };
    }

    public async Task<AnalyzerOrderDto?> OrderByBarcodeAsync(LabConnectorInstallation c, string barcode, string? analyzerId)
    {
        var sample = await _db.Samples.AsNoTracking().Include(s => s.BiomaterialType)
            .Include(s => s.Order).ThenInclude(o => o!.Patient)
            .Include(s => s.Order).ThenInclude(o => o!.Tests)
            .FirstOrDefaultAsync(s => s.Barcode == barcode);
        if (sample?.Order == null || OrderStatuses.Terminal.Contains(sample.Order.Status)) return null;
        var map = analyzerId == null ? new List<LabAnalyzerParameterMap>() : await _db.AnalyzerParameters.AsNoTracking().Where(m => m.AnalyzerId == analyzerId).ToListAsync();
        return BuildOrderDto(sample, sample.Order, map);
    }

    public async Task<List<AnalyzerOrderDto>> PendingOrdersAsync(LabConnectorInstallation c, string? analyzerId)
    {
        var map = analyzerId == null ? new List<LabAnalyzerParameterMap>() : await _db.AnalyzerParameters.AsNoTracking().Where(m => m.AnalyzerId == analyzerId).ToListAsync();
        var codes = map.Select(m => m.TestCode).Distinct().ToList();
        var samples = await _db.Samples.AsNoTracking().Include(s => s.BiomaterialType)
            .Include(s => s.Order).ThenInclude(o => o!.Patient)
            .Include(s => s.Order).ThenInclude(o => o!.Tests)
            .Where(s => (s.Status == SampleStatuses.Collected || s.Status == SampleStatuses.InTransit || s.Status == SampleStatuses.Received || s.Status == SampleStatuses.Processing)
                        && s.Order!.Status != OrderStatuses.Cancelled && s.Order.Status != OrderStatuses.Rejected
                        && s.Order.Tests.Any(t => t.SampleId == s.Id && (t.Status == OrderTestStatuses.Pending || t.Status == OrderTestStatuses.InAnalysis || t.Status == OrderTestStatuses.Rerun)
                                                  && (analyzerId == null || t.AssignedAnalyzerId == analyzerId || t.AssignedAnalyzerId == null) && (codes.Count == 0 || codes.Contains(t.TestCode))))
            .OrderByDescending(s => s.Order!.IsUrgentCito).ThenBy(s => s.Order!.OrderDatetime).Take(200).ToListAsync();
        return samples.Select(s => BuildOrderDto(s, s.Order!, map)).Where(d => d.Tests.Count > 0).ToList();
    }

    private static AnalyzerOrderDto BuildOrderDto(LabOrderSample sample, LabOrder order, List<LabAnalyzerParameterMap> map)
    {
        var tests = order.Tests.Where(t => t.SampleId == sample.Id && t.Status is OrderTestStatuses.Pending or OrderTestStatuses.InAnalysis or OrderTestStatuses.Rerun).ToList();
        if (map.Count > 0) tests = tests.Where(t => map.Any(m => m.TestCode == t.TestCode)).ToList();
        var p = order.Patient;
        return new AnalyzerOrderDto
        {
            Barcode = sample.Barcode, OrderNumber = order.OrderNumber, Priority = order.IsUrgentCito ? "S" : "R",
            SampleType = SampleTypeCode(sample.BiomaterialType?.Name),
            Patient = new AnalyzerOrderPatientDto
            {
                Id = order.PatientId, LastName = p?.LastName ?? "", FirstName = p?.FirstName ?? "", BirthDate = p?.BirthDate, Gender = p?.Gender ?? "U",
                LastNameLatin = p?.LastNameLatin ?? TransliterationKmu2010.ToLatin(p?.LastName ?? ""), FirstNameLatin = p?.FirstNameLatin ?? TransliterationKmu2010.ToLatin(p?.FirstName ?? "")
            },
            Tests = tests.Select(t => new AnalyzerOrderTestDto { TestCode = t.TestCode, AnalyzerCode = map.FirstOrDefault(m => m.TestCode == t.TestCode)?.AnalyzerCode ?? t.TestCode }).ToList()
        };
    }

    public static string SampleTypeCode(string? biomaterialName)
    {
        var n = (biomaterialName ?? "").ToLowerInvariant();
        if (n.Contains("сеч")) return "Urine";
        if (n.Contains("сироват")) return "Serum";
        if (n.Contains("плазм")) return "Plasma";
        if (n.Contains("кров")) return "Blood";
        if (n.Contains("лікв")) return "CSF";
        return "Other";
    }

    public async Task<AnalyzerResultsAcceptedDto> AcceptResultsAsync(LabConnectorInstallation c, AnalyzerResultsBatchDto batch)
    {
        var analyzer = await _db.Analyzers.Include(a => a.ParameterMap).FirstOrDefaultAsync(a => a.Id == batch.AnalyzerId)
                       ?? throw new ValidationException($"Аналізатор {batch.AnalyzerId} не знайдено");
        if (analyzer.ConnectorId != null && analyzer.ConnectorId != c.Id) throw new ForbiddenConnectorException("Аналізатор належить іншій інсталяції коннектора");
        analyzer.LastMessageAt = DateTime.UtcNow; analyzer.IsOnline = true;
        var response = new AnalyzerResultsAcceptedDto { Accepted = batch.Results.Count };

        if (batch.IsQc)
        {
            foreach (var item in batch.Results)
            {
                var lot = item.QcLotNumber ?? item.Barcode;
                var material = await _db.QcMaterials.Include(m => m.Targets).FirstOrDefaultAsync(m => m.AnalyzerId == analyzer.Id && m.LotNumber == lot && m.IsActive);
                var testCode = MapTestCode(analyzer, item.AnalyzerCode);
                if (material == null || testCode == null || !material.Targets.Any(t => t.TestCode == testCode) || !TryParse(item.Value, out var qv))
                { response.Unmatched.Add(new UnmatchedResultDto { Barcode = item.Barcode, AnalyzerCode = item.AnalyzerCode, Reason = material == null ? $"Лот ВКЯ '{lot}' не знайдено" : "Немає цільового значення або нечислове значення" }); continue; }
                var factor = analyzer.ParameterMap.FirstOrDefault(m => m.AnalyzerCode.Equals(item.AnalyzerCode, StringComparison.OrdinalIgnoreCase));
                var value = factor == null ? qv : qv * factor.Factor + factor.Offset;
                var r = await _qc.AddResultAsync(new QcResultRequest { QcMaterialId = material.Id, TestCode = testCode, MeasuredValue = value, RunAt = item.MeasuredAt ?? batch.ReceivedAt }, isSystem: true);
                response.Matched++; response.Created.Add(r.Id);
            }
            await _db.SaveChangesAsync();
            return response;
        }

        foreach (var item in batch.Results)
        {
            var testCode = MapTestCode(analyzer, item.AnalyzerCode);
            if (testCode == null) { await StoreUnmatched(analyzer, batch, item, response, $"Код приладу '{item.AnalyzerCode}' не зіставлено з тестом"); continue; }
            var sample = await _db.Samples.AsNoTracking().FirstOrDefaultAsync(s => s.Barcode == item.Barcode);
            if (sample == null) { await StoreUnmatched(analyzer, batch, item, response, $"Штрихкод '{item.Barcode}' не знайдено"); continue; }
            var test = await _db.OrderTests.Where(t => t.OrderId == sample.OrderId && t.TestCode == testCode && t.Status != OrderTestStatuses.Rejected)
                .OrderBy(t => t.SampleId == sample.Id ? 0 : 1).ThenBy(t => OrderTestStatuses.VerifiedAny.Contains(t.Status) ? 1 : 0).FirstOrDefaultAsync();
            if (test == null) { await StoreUnmatched(analyzer, batch, item, response, $"У замовленні немає тесту {testCode}"); continue; }
            if (OrderTestStatuses.VerifiedAny.Contains(test.Status)) { await StoreUnmatched(analyzer, batch, item, response, $"Тест {testCode} уже верифіковано — повтор потребує RERUN"); continue; }

            var mapRow = analyzer.ParameterMap.FirstOrDefault(m => m.AnalyzerCode.Equals(item.AnalyzerCode, StringComparison.OrdinalIgnoreCase));
            double? numeric = null; string? text = null;
            if (TryParse(item.Value, out var v)) numeric = mapRow == null ? v : v * mapRow.Factor + mapRow.Offset; else text = item.Value;
            try
            {
                var row = await _pipeline.ApplyAsync(new ResultEntry
                {
                    OrderTestId = test.Id, NumericValue = numeric, StringValue = text, Unit = mapRow?.UnitOverride ?? (numeric.HasValue ? null : item.Unit), AnalyzerId = analyzer.Id,
                    AnalyzerFlags = item.Flags, RawMessageId = batch.RawMessageId, MeasuredAt = item.MeasuredAt, IsSystem = true
                });
                response.Matched++; response.Created.Add(row.OrderTestId);
            }
            catch (LisException ex) { await StoreUnmatched(analyzer, batch, item, response, ex.Message); }
        }
        await _db.SaveChangesAsync();
        return response;
    }

    private static bool TryParse(string? s, out double v) =>
        double.TryParse((s ?? "").Trim().Replace(',', '.').TrimStart('<', '>', ' '), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);

    private string? MapTestCode(LabAnalyzer analyzer, string analyzerCode)
    {
        var m = analyzer.ParameterMap.FirstOrDefault(x => x.AnalyzerCode.Equals(analyzerCode, StringComparison.OrdinalIgnoreCase));
        if (m != null) return m.TestCode;
        // fallback: LabReferenceLayer.analyzerCode → LabTestDefinition.code
        var byLayer = _db.ReferenceLayers.AsNoTracking().Where(l => l.AnalyzerCode == analyzerCode && l.IsActive).Select(l => l.TestCode).FirstOrDefault();
        if (byLayer != null) return byLayer;
        var byCode = _db.Tests.AsNoTracking().Where(t => t.Code == analyzerCode.ToUpper() && t.IsActive).Select(t => t.Code).FirstOrDefault();
        return byCode;
    }

    private async Task StoreUnmatched(LabAnalyzer analyzer, AnalyzerResultsBatchDto batch, AnalyzerResultItemDto item, AnalyzerResultsAcceptedDto response, string reason)
    {
        _db.UnmatchedResults.Add(new LabUnmatchedResult
        {
            AnalyzerId = analyzer.Id, Barcode = item.Barcode, AnalyzerCode = item.AnalyzerCode, Value = item.Value, Unit = item.Unit, Flags = item.Flags,
            MeasuredAt = item.MeasuredAt, RawMessageId = batch.RawMessageId, Reason = reason, ReceivedAt = batch.ReceivedAt == default ? DateTime.UtcNow : batch.ReceivedAt
        });
        response.Unmatched.Add(new UnmatchedResultDto { Barcode = item.Barcode, AnalyzerCode = item.AnalyzerCode, Reason = reason });
        await Task.CompletedTask;
    }

    public async Task<LabAnalyzerMessage> StoreMessageAsync(LabConnectorInstallation c, AnalyzerMessageLogDto dto)
    {
        var analyzer = await _db.Analyzers.FirstOrDefaultAsync(a => a.Id == dto.AnalyzerId) ?? throw new ValidationException($"Аналізатор {dto.AnalyzerId} не знайдено");
        var msg = new LabAnalyzerMessage { AnalyzerId = analyzer.Id, Direction = dto.Direction, Protocol = dto.Protocol, RawText = dto.RawText, ParsedOk = dto.ParsedOk, ReceivedAt = dto.ReceivedAt == default ? DateTime.UtcNow : dto.ReceivedAt, ResultsCount = dto.ResultsCount, Error = dto.Error };
        analyzer.LastMessageAt = msg.ReceivedAt;
        if (!string.IsNullOrEmpty(dto.Error)) analyzer.LastError = dto.Error;
        _db.AnalyzerMessages.Add(msg);
        await _db.SaveChangesAsync();
        return msg;
    }

    public async Task StoreLogsAsync(LabConnectorInstallation c, ConnectorLogBatchDto batch)
    {
        foreach (var e in batch.Entries)
            _db.ConnectorLogs.Add(new LabConnectorLog { ConnectorId = c.Id, At = e.At == default ? DateTime.UtcNow : e.At, Level = e.Level, Message = e.Message, AnalyzerId = e.AnalyzerId });
        await _db.SaveChangesAsync();
    }
}
