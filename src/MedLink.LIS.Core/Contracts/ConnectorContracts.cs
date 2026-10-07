// =============================================================================
// MedLink LIS 4.0 — спільні DTO протоколу «Коннектор ⇄ Сервер ЛІС»
// Використовуються MedLink.LIS.Api (сервер) та MedLink.LabConnector (клієнт).
// Джерело істини: docs/API_CONTRACT.md, розділ 3.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
namespace MedLink.LIS.Core.Contracts;

public sealed class ConnectorRegisterRequest
{
    public string InstallKey { get; set; } = "";
    public string HostName { get; set; } = "";
    public string? OsDescription { get; set; }
    public string? Version { get; set; }
}

public sealed class ConnectorRegisterResponse
{
    public string ConnectorId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public DateTime ServerTimeUtc { get; set; }
}

public sealed class ConnectorConfigDto
{
    public string ConnectorId { get; set; } = "";
    public int ConfigVersion { get; set; }
    public int PollIntervalSec { get; set; } = 30;
    public int HeartbeatIntervalSec { get; set; } = 30;
    public List<AnalyzerConfigDto> Analyzers { get; set; } = new();
}

public sealed class AnalyzerConfigDto
{
    public string AnalyzerId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Код типу з каталогу (ac_analyzer_type.analyzer_type_code), напр. SYSMEXXN, COBAS411.</summary>
    public string TypeCode { get; set; } = "";
    /// <summary>ASTM | ASTM_ASK | ASTM2 | HL7 | TEXT | HUMA5L | UC1000 | CYAN | JUNIOR | IRIS | RAPID | FUJI | TXT | FILE</summary>
    public string Protocol { get; set; } = "ASTM";
    public AnalyzerConnectionDto Connection { get; set; } = new();
    public AnalyzerFramingDto Framing { get; set; } = new();
    public bool AutoQueryOrders { get; set; } = true;
    /// <summary>Ключ побудовника замовлення (IAnalyzerOrderBuilder), напр. COBAS411, SYSMEX_XN, MINDRAY_HL7.</summary>
    public string OrderTemplate { get; set; } = "ASTM_GENERIC";
    public List<AnalyzerParameterMapDto> ParameterMap { get; set; } = new();
}

public sealed class AnalyzerConnectionDto
{
    /// <summary>TCP | COM | FILE</summary>
    public string Mode { get; set; } = "TCP";
    public string? Host { get; set; }
    public int? Port { get; set; }
    public bool IsServer { get; set; }
    public string? ComPort { get; set; }
    public int BaudRate { get; set; } = 9600;
    /// <summary>None | Even | Odd | Mark | Space</summary>
    public string Parity { get; set; } = "None";
    public int DataBits { get; set; } = 8;
    /// <summary>One | OneAndHalf | Two</summary>
    public string StopBits { get; set; } = "One";
    /// <summary>None | XonXoff | Hardware</summary>
    public string FlowControl { get; set; } = "None";
    public string? FilePath { get; set; }
    public int FilePollSec { get; set; } = 10;
}

public sealed class AnalyzerFramingDto
{
    /// <summary>Початок пакета (base64 байтів), null = ENQ (0x05) для ASTM.</summary>
    public string? BopBase64 { get; set; }
    /// <summary>Кінець пакета (base64 байтів), null = EOT (0x04) для ASTM.</summary>
    public string? EopBase64 { get; set; }
    public bool Checksum { get; set; } = true;
    public int SleepMs { get; set; } = 100;
    public int MaxFrameLen { get; set; } = 240;
    public bool AckAfterRecord { get; set; } = true;
}

public sealed class AnalyzerParameterMapDto
{
    public string AnalyzerCode { get; set; } = "";
    public string TestCode { get; set; } = "";
    public double Factor { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public string? UnitOverride { get; set; }
}

public sealed class ConnectorHeartbeatRequest
{
    public string? Version { get; set; }
    public long UptimeSec { get; set; }
    public int BufferedCount { get; set; }
    public List<AnalyzerHeartbeatDto> Analyzers { get; set; } = new();
}

public sealed class AnalyzerHeartbeatDto
{
    public string AnalyzerId { get; set; } = "";
    public bool IsConnected { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastError { get; set; }
}

public sealed class ConnectorHeartbeatResponse
{
    public DateTime ServerTimeUtc { get; set; }
    public int ConfigVersion { get; set; }
    public List<ConnectorCommandDto> Commands { get; set; } = new();
}

public sealed class ConnectorCommandDto
{
    /// <summary>RELOAD_CONFIG | RESTART | SEND_TEST_MESSAGE</summary>
    public string Type { get; set; } = "";
    public string? Payload { get; set; }
}

public sealed class AnalyzerOrderDto
{
    public string Barcode { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    /// <summary>R — routine, S — stat/CITO</summary>
    public string Priority { get; set; } = "R";
    public string SampleType { get; set; } = "Serum";
    public AnalyzerOrderPatientDto Patient { get; set; } = new();
    public List<AnalyzerOrderTestDto> Tests { get; set; } = new();
}

public sealed class AnalyzerOrderPatientDto
{
    public string Id { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? LastNameLatin { get; set; }
    public string? FirstNameLatin { get; set; }
    public DateTime? BirthDate { get; set; }
    /// <summary>M | F | U</summary>
    public string Gender { get; set; } = "U";
}

public sealed class AnalyzerOrderTestDto
{
    public string TestCode { get; set; } = "";
    public string AnalyzerCode { get; set; } = "";
    public string? Dilution { get; set; }
}

public sealed class AnalyzerResultsBatchDto
{
    public string AnalyzerId { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public string? RawMessageId { get; set; }
    public bool IsQc { get; set; }
    public List<AnalyzerResultItemDto> Results { get; set; } = new();
}

public sealed class AnalyzerResultItemDto
{
    public string Barcode { get; set; } = "";
    public string AnalyzerCode { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Unit { get; set; }
    public string? Flags { get; set; }
    public DateTime? MeasuredAt { get; set; }
    public string? ReferenceText { get; set; }
    public string? QcLotNumber { get; set; }
}

public sealed class AnalyzerResultsAcceptedDto
{
    public int Accepted { get; set; }
    public int Matched { get; set; }
    public List<UnmatchedResultDto> Unmatched { get; set; } = new();
    public List<string> Created { get; set; } = new();
}

public sealed class UnmatchedResultDto
{
    public string Barcode { get; set; } = "";
    public string AnalyzerCode { get; set; } = "";
    public string Reason { get; set; } = "";
}

public sealed class AnalyzerMessageLogDto
{
    public string AnalyzerId { get; set; } = "";
    /// <summary>IN | OUT</summary>
    public string Direction { get; set; } = "IN";
    public string Protocol { get; set; } = "ASTM";
    public string RawText { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public bool ParsedOk { get; set; }
    public int ResultsCount { get; set; }
    public string? Error { get; set; }
}

public sealed class ConnectorLogBatchDto
{
    public List<ConnectorLogEntryDto> Entries { get; set; } = new();
}

public sealed class ConnectorLogEntryDto
{
    public DateTime At { get; set; }
    /// <summary>Trace|Debug|Information|Warning|Error|Critical</summary>
    public string Level { get; set; } = "Information";
    public string Message { get; set; } = "";
    public string? AnalyzerId { get; set; }
}
