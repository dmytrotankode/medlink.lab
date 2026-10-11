// Аналізатори: CRUD з мапою параметрів, журнал обміну, симуляція парсингу, попередній перегляд замовлення
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Domain;
using MedLink.LIS.Api.Infrastructure;
using MedLink.LIS.Api.Services.Parsing;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public sealed class AnalyzerParameterMapRequest
{
    public string AnalyzerCode { get; set; } = "";
    public string TestCode { get; set; } = "";
    public double Factor { get; set; } = 1.0;
    public double Offset { get; set; }
    public string? UnitOverride { get; set; }
}

public sealed class AnalyzerRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int AnalyzerTypeId { get; set; }
    public string? ConnectorId { get; set; }
    public string? DepartmentId { get; set; }
    public string ConnectionMode { get; set; } = "TCP";
    public string? TcpHost { get; set; }
    public int? TcpPort { get; set; }
    public bool IsTcpServer { get; set; }
    public string? ComPort { get; set; }
    public int BaudRate { get; set; } = 9600;
    public string Parity { get; set; } = "None";
    public int DataBits { get; set; } = 8;
    public string StopBits { get; set; } = "One";
    public string FlowControl { get; set; } = "None";
    public string? FilePath { get; set; }
    public int FilePollSec { get; set; } = 10;
    public bool AutoQueryOrders { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public List<AnalyzerParameterMapRequest>? ParameterMap { get; set; }
}

public sealed class AnalyzerService
{
    private readonly LisDbContext _db;
    private readonly IRolePolicy _policy;
    private readonly IAuditService _audit;
    private readonly IAnalyzerMessageParser _parser;
    private readonly ConnectorService _connectors;

    public AnalyzerService(LisDbContext db, IRolePolicy policy, IAuditService audit, IAnalyzerMessageParser parser, ConnectorService connectors)
    {
        _db = db; _policy = policy; _audit = audit; _parser = parser; _connectors = connectors;
    }

    private IQueryable<LabAnalyzer> Query() => _db.Analyzers.Include(a => a.AnalyzerType).Include(a => a.ParameterMap).Include(a => a.Connector).Include(a => a.Department);

    public async Task<List<object>> ListAsync(bool? isActive)
    {
        var items = await Query().AsNoTracking().Where(a => a.RecordState != RecordStates.Deleted && (isActive == null || a.IsActive == isActive)).OrderBy(a => a.Name).ToListAsync();
        var lockouts = await _db.Lockouts.AsNoTracking().Where(l => l.ResolvedAt == null).ToListAsync();
        return items.Select(a => ToDto(a, lockouts.Where(l => l.AnalyzerId == a.Id).ToList())).ToList();
    }

    public async Task<object> GetAsync(string id)
    {
        var a = await LoadAsync(id);
        var lockouts = await _db.Lockouts.AsNoTracking().Where(l => l.ResolvedAt == null && l.AnalyzerId == id).ToListAsync();
        return ToDto(a, lockouts);
    }

    public async Task<LabAnalyzer> LoadAsync(string id) => await Query().FirstOrDefaultAsync(a => a.Id == id && a.RecordState != RecordStates.Deleted) ?? throw NotFoundException.For("Аналізатор", id);

    private static object ToDto(LabAnalyzer a, List<LabAnalyzerLockout> lockouts) => new
    {
        a.Id, a.Code, a.Name, a.AnalyzerTypeId, analyzerTypeCode = a.AnalyzerType?.Code, analyzerTypeName = a.AnalyzerType?.Name, category = a.AnalyzerType?.Category, protocol = a.AnalyzerType?.ExchType,
        orderTemplate = a.AnalyzerType?.OrderTemplate, a.ConnectorId, connectorName = a.Connector?.Name, a.DepartmentId, departmentName = a.Department?.Caption,
        a.ConnectionMode, a.TcpHost, a.TcpPort, a.IsTcpServer, a.ComPort, a.BaudRate, a.Parity, a.DataBits, a.StopBits, a.FlowControl, a.FilePath, a.FilePollSec,
        a.AutoQueryOrders, a.IsActive, a.IsOnline, a.LastMessageAt, a.LastError, a.CreatedOn,
        isLockedOut = lockouts.Count > 0, activeLockouts = lockouts.Select(l => new { l.Id, l.TestCode, l.Reason, l.StartedAt }),
        parameterMap = a.ParameterMap.OrderBy(m => m.AnalyzerCode).Select(m => new { m.Id, m.AnalyzerCode, m.TestCode, m.Factor, m.Offset, m.UnitOverride })
    };

    public async Task<object> CreateAsync(AnalyzerRequest req)
    {
        _policy.Require("Створення аналізатора", LabRoles.Admin);
        await ValidateAsync(req, null);
        var a = new LabAnalyzer();
        Apply(a, req);
        _db.Analyzers.Add(a);
        await BumpConnectorConfig(a.ConnectorId);
        _audit.Log("CREATE", "lab_analyzer", a.Id, null, req);
        await _db.SaveChangesAsync();
        return await GetAsync(a.Id);
    }

    public async Task<object> UpdateAsync(string id, AnalyzerRequest req)
    {
        _policy.Require("Редагування аналізатора", LabRoles.Admin);
        var a = await LoadAsync(id);
        await ValidateAsync(req, id);
        var before = new { a.Code, a.Name, a.AnalyzerTypeId, a.ConnectorId, a.ConnectionMode, a.TcpHost, a.TcpPort, a.IsActive, map = a.ParameterMap.Count };
        var oldConnector = a.ConnectorId;
        Apply(a, req);
        await BumpConnectorConfig(a.ConnectorId);
        if (oldConnector != a.ConnectorId) await BumpConnectorConfig(oldConnector);
        _audit.Log("UPDATE", "lab_analyzer", a.Id, before, req);
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task DeleteAsync(string id)
    {
        _policy.Require("Видалення аналізатора", LabRoles.Admin);
        var a = await LoadAsync(id);
        var hasData = await _db.Results.AnyAsync(r => r.AnalyzerId == id) || await _db.QcMaterials.AnyAsync(m => m.AnalyzerId == id) || await _db.AnalyzerMessages.AnyAsync(m => m.AnalyzerId == id);
        if (hasData) { a.IsActive = false; a.RecordState = RecordStates.Deleted; _audit.Log("SOFT_DELETE", "lab_analyzer", id, null, null, "Є пов'язані результати/ВКЯ — деактивовано"); }
        else { _db.Analyzers.Remove(a); _audit.Log("DELETE", "lab_analyzer", id, new { a.Code }, null); }
        await BumpConnectorConfig(a.ConnectorId);
        await _db.SaveChangesAsync();
    }

    private async Task ValidateAsync(AnalyzerRequest req, string? id)
    {
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Код та назва аналізатора обов'язкові");
        if (await _db.Analyzers.AnyAsync(a => a.Code == req.Code && a.Id != id && a.RecordState != RecordStates.Deleted)) throw new ConflictException($"Аналізатор із кодом {req.Code} вже існує");
        if (!await _db.AnalyzerTypes.AnyAsync(t => t.Id == req.AnalyzerTypeId)) throw ValidationException.Field("analyzerTypeId", "Тип аналізатора не знайдено");
        if (req.ConnectorId != null && !await _db.Connectors.AnyAsync(c => c.Id == req.ConnectorId)) throw ValidationException.Field("connectorId", "Коннектор не знайдено");
        if (req.DepartmentId != null && !await _db.Departments.AnyAsync(d => d.Id == req.DepartmentId)) throw ValidationException.Field("departmentId", "Підрозділ не знайдено");
        if (!new[] { "TCP", "COM", "FILE" }.Contains(req.ConnectionMode)) throw ValidationException.Field("connectionMode", "Режим: TCP | COM | FILE");
        if (req.ConnectionMode == "TCP" && (req.TcpPort is null or <= 0)) throw ValidationException.Field("tcpPort", "Вкажіть TCP-порт");
        if (req.ConnectionMode == "COM" && string.IsNullOrWhiteSpace(req.ComPort)) throw ValidationException.Field("comPort", "Вкажіть COM-порт");
        if (req.ConnectionMode == "FILE" && string.IsNullOrWhiteSpace(req.FilePath)) throw ValidationException.Field("filePath", "Вкажіть шлях до каталогу файлів");
        if (req.ParameterMap != null)
        {
            var codes = await _db.Tests.AsNoTracking().Select(t => t.Code).ToListAsync();
            var missing = req.ParameterMap.Select(m => m.TestCode).Where(c => !codes.Contains(c)).Distinct().ToList();
            if (missing.Count > 0) throw ValidationException.Field("parameterMap", $"Невідомі коди тестів: {string.Join(", ", missing)}");
        }
    }

    private void Apply(LabAnalyzer a, AnalyzerRequest req)
    {
        a.Code = req.Code.Trim(); a.Name = req.Name.Trim(); a.AnalyzerTypeId = req.AnalyzerTypeId; a.ConnectorId = req.ConnectorId; a.DepartmentId = req.DepartmentId;
        a.ConnectionMode = req.ConnectionMode; a.TcpHost = req.TcpHost; a.TcpPort = req.TcpPort; a.IsTcpServer = req.IsTcpServer; a.ComPort = req.ComPort; a.BaudRate = req.BaudRate;
        a.Parity = req.Parity; a.DataBits = req.DataBits; a.StopBits = req.StopBits; a.FlowControl = req.FlowControl; a.FilePath = req.FilePath; a.FilePollSec = req.FilePollSec;
        a.AutoQueryOrders = req.AutoQueryOrders; a.IsActive = req.IsActive;
        if (req.ParameterMap != null)
        {
            _db.AnalyzerParameters.RemoveRange(a.ParameterMap);
            a.ParameterMap = req.ParameterMap.Select(m => new LabAnalyzerParameterMap { AnalyzerId = a.Id, AnalyzerCode = m.AnalyzerCode.Trim(), TestCode = m.TestCode.Trim(), Factor = m.Factor, Offset = m.Offset, UnitOverride = m.UnitOverride }).ToList();
        }
    }

    private async Task BumpConnectorConfig(string? connectorId)
    {
        if (connectorId == null) return;
        var c = await _db.Connectors.FirstOrDefaultAsync(x => x.Id == connectorId);
        if (c == null) return;
        c.ConfigVersion++;
        _db.ConnectorCommands.Add(new LabConnectorCommand { ConnectorId = c.Id, Type = "RELOAD_CONFIG" });
    }

    public async Task<PagedResult<LabAnalyzerMessage>> MessagesAsync(string id, string? direction, PagingQuery paging)
    {
        await LoadAsync(id);
        var q = _db.AnalyzerMessages.AsNoTracking().Where(m => m.AnalyzerId == id);
        if (!string.IsNullOrWhiteSpace(direction)) q = q.Where(m => m.Direction == direction.ToUpper());
        return await q.OrderByDescending(m => m.ReceivedAt).ToPagedAsync(paging);
    }

    public async Task DeleteMessageAsync(string analyzerId, string messageId)
    {
        _policy.Require("Видалення запису журналу", LabRoles.Admin);
        var m = await _db.AnalyzerMessages.FirstOrDefaultAsync(x => x.Id == messageId && x.AnalyzerId == analyzerId) ?? throw NotFoundException.For("Повідомлення", messageId);
        _db.AnalyzerMessages.Remove(m);
        await _db.SaveChangesAsync();
    }

    /// <summary>Парсинг тестового повідомлення без збереження: результати мапляться через parameterMap.</summary>
    public async Task<object> SimulateAsync(string id, string rawMessage)
    {
        var a = await LoadAsync(id);
        if (string.IsNullOrWhiteSpace(rawMessage)) throw ValidationException.Field("rawMessage", "Порожнє повідомлення");
        var parsed = _parser.Parse(rawMessage, a.AnalyzerType?.ExchType);
        var codes = await _db.Tests.AsNoTracking().Select(t => t.Code).ToListAsync();
        var results = parsed.Results.Select(r =>
        {
            var map = a.ParameterMap.FirstOrDefault(m => m.AnalyzerCode.Equals(r.AnalyzerCode, StringComparison.OrdinalIgnoreCase));
            var testCode = map?.TestCode ?? (codes.Contains(r.AnalyzerCode.ToUpperInvariant()) ? r.AnalyzerCode.ToUpperInvariant() : null);
            double? numeric = double.TryParse(r.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? (map == null ? v : v * map.Factor + map.Offset) : null;
            return new { r.Barcode, r.AnalyzerCode, testCode, mapped = testCode != null, rawValue = r.Value, value = numeric, unit = map?.UnitOverride ?? r.Unit, r.Flags, r.ReferenceText, r.MeasuredAt, r.ResultStatus };
        }).ToList();
        var sampleKnown = parsed.Barcode != null && await _db.Samples.AnyAsync(s => s.Barcode == parsed.Barcode);
        var warnings = new List<string>(parsed.Warnings);
        foreach (var r in results.Where(r => !r.mapped)) warnings.Add($"Код приладу '{r.AnalyzerCode}' не зіставлено у parameterMap аналізатора {a.Code}");
        if (parsed.Barcode != null && !sampleKnown) warnings.Add($"Штрихкод {parsed.Barcode} не знайдено серед пробірок (результат потрапить до незв'язаних)");
        return new { analyzerId = a.Id, analyzerCode = a.Code, protocol = parsed.Protocol, kind = parsed.Kind, sender = parsed.SenderName, barcode = parsed.Barcode, sampleKnown, patientId = parsed.PatientId, patientName = parsed.PatientName,
            requestedTests = parsed.RequestedTests, parsedResults = results, warnings, records = parsed.Records };
    }

    public async Task<object> OrderPreviewAsync(string id, string barcode)
    {
        var a = await LoadAsync(id);
        var connector = a.Connector ?? new LabConnectorInstallation();
        var order = await _connectors.OrderByBarcodeAsync(connector, barcode, a.Id) ?? throw NotFoundException.For("Пробірка/замовлення для штрихкоду", barcode);
        var protocol = a.AnalyzerType?.ExchType ?? "ASTM";
        return new { analyzerId = a.Id, analyzerCode = a.Code, protocol, orderTemplate = a.AnalyzerType?.OrderTemplate, order, text = AnalyzerOrderPreviewBuilder.Build(order, protocol) };
    }
}
