// Коннектор та аналізатори: інсталяції, прилади, мапа параметрів, журнал обміну, логи, команди
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

[Table("lab_connector_installation")]
public class LabConnectorInstallation : GuidEntity
{
    [MaxLength(256)] public string Name { get; set; } = "";
    [MaxLength(128)] public string? HostName { get; set; }
    [MaxLength(256)] public string? OsDescription { get; set; }
    [MaxLength(64)] public string? Version { get; set; }
    /// <summary>Одноразовий ключ інсталяції (стає використаним після реєстрації).</summary>
    [MaxLength(64)] public string InstallKey { get; set; } = "";
    public bool InstallKeyUsed { get; set; }
    /// <summary>SHA-256 (hex) від apiKey.</summary>
    [MaxLength(128)] public string? ApiKeyHash { get; set; }
    /// <summary>PENDING | ACTIVE | OFFLINE | DISABLED</summary>
    [MaxLength(16)] public string Status { get; set; } = "PENDING";
    public DateTime? LastHeartbeatAt { get; set; }
    public DateTime? RegisteredAt { get; set; }
    public int BufferedCount { get; set; }
    public long UptimeSec { get; set; }
    public int ConfigVersion { get; set; } = 1;
    public int PollIntervalSec { get; set; } = 30;
    public int HeartbeatIntervalSec { get; set; } = 30;

    public List<LabAnalyzer> Analyzers { get; set; } = new();
}

[Table("lab_analyzer")]
public class LabAnalyzer : GuidEntity
{
    [MaxLength(64)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    public int AnalyzerTypeId { get; set; }
    [MaxLength(64)] public string? ConnectorId { get; set; }
    [MaxLength(64)] public string? DepartmentId { get; set; }
    /// <summary>TCP | COM | FILE</summary>
    [MaxLength(8)] public string ConnectionMode { get; set; } = "TCP";
    [MaxLength(128)] public string? TcpHost { get; set; }
    public int? TcpPort { get; set; }
    public bool IsTcpServer { get; set; }
    [MaxLength(32)] public string? ComPort { get; set; }
    public int BaudRate { get; set; } = 9600;
    /// <summary>None | Even | Odd | Mark | Space</summary>
    [MaxLength(8)] public string Parity { get; set; } = "None";
    public int DataBits { get; set; } = 8;
    /// <summary>One | OneAndHalf | Two</summary>
    [MaxLength(16)] public string StopBits { get; set; } = "One";
    /// <summary>None | XonXoff | Hardware</summary>
    [MaxLength(16)] public string FlowControl { get; set; } = "None";
    [MaxLength(512)] public string? FilePath { get; set; }
    public int FilePollSec { get; set; } = 10;
    public bool AutoQueryOrders { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool IsOnline { get; set; }
    public DateTime? LastMessageAt { get; set; }
    [MaxLength(1024)] public string? LastError { get; set; }

    [ForeignKey(nameof(AnalyzerTypeId))] public LabAnalyzerType? AnalyzerType { get; set; }
    [ForeignKey(nameof(ConnectorId))] public LabConnectorInstallation? Connector { get; set; }
    [ForeignKey(nameof(DepartmentId))] public OrgDepartment? Department { get; set; }
    public List<LabAnalyzerParameterMap> ParameterMap { get; set; } = new();
}

[Table("lab_analyzer_parameter_map")]
public class LabAnalyzerParameterMap : GuidEntity
{
    [MaxLength(64)] public string AnalyzerId { get; set; } = "";
    [MaxLength(64)] public string AnalyzerCode { get; set; } = "";
    [MaxLength(64)] public string TestCode { get; set; } = "";
    public double Factor { get; set; } = 1.0;
    public double Offset { get; set; }
    [MaxLength(64)] public string? UnitOverride { get; set; }

    [ForeignKey(nameof(AnalyzerId))] public LabAnalyzer? Analyzer { get; set; }
}

[Table("lab_analyzer_message")]
public class LabAnalyzerMessage : GuidEntity
{
    [MaxLength(64)] public string AnalyzerId { get; set; } = "";
    /// <summary>IN | OUT</summary>
    [MaxLength(4)] public string Direction { get; set; } = "IN";
    [MaxLength(16)] public string Protocol { get; set; } = "ASTM";
    public string RawText { get; set; } = "";
    public bool ParsedOk { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public int ResultsCount { get; set; }
    [MaxLength(1024)] public string? Error { get; set; }

    [ForeignKey(nameof(AnalyzerId))] public LabAnalyzer? Analyzer { get; set; }
}

[Table("lab_connector_log")]
public class LabConnectorLog : GuidEntity
{
    [MaxLength(64)] public string ConnectorId { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
    [MaxLength(16)] public string Level { get; set; } = "Information";
    public string Message { get; set; } = "";
    [MaxLength(64)] public string? AnalyzerId { get; set; }
}

[Table("lab_connector_command")]
public class LabConnectorCommand : GuidEntity
{
    [MaxLength(64)] public string ConnectorId { get; set; } = "";
    /// <summary>RELOAD_CONFIG | RESTART | SEND_TEST_MESSAGE</summary>
    [MaxLength(32)] public string Type { get; set; } = "RELOAD_CONFIG";
    public string? Payload { get; set; }
    public bool IsDelivered { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
