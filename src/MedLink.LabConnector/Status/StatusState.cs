// =============================================================================
// MedLink LIS Analyzer Connector — стан для сторінки статусу та heartbeat.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Collections.Concurrent;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;

namespace MedLink.LabConnector.Status;

public sealed class AnalyzerStatus
{
    public string AnalyzerId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string TypeCode { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string Connection { get; set; } = "";
    public bool IsConnected { get; set; }
    public bool IsRunning { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public long MessagesIn { get; set; }
    public long MessagesOut { get; set; }
    public long ResultsSent { get; set; }
    public long OrdersSent { get; set; }
    public long Errors { get; set; }
    public string OrderTemplate { get; set; } = "";
    public string ParserKind { get; set; } = "";

    public AnalyzerStatus Clone() => (AnalyzerStatus)MemberwiseClone();
}

public sealed class MessageLogEntry
{
    public DateTime At { get; set; }
    public string AnalyzerId { get; set; } = "";
    public string AnalyzerCode { get; set; } = "";
    /// <summary>IN | OUT | SYS</summary>
    public string Direction { get; set; } = "IN";
    public string Protocol { get; set; } = "";
    public string Preview { get; set; } = "";
    public bool ParsedOk { get; set; } = true;
    public string? Kind { get; set; }
    public int ResultsCount { get; set; }
    public string? Error { get; set; }
}

public sealed class StatusSnapshot
{
    public string Product { get; set; } = "MedLink LIS Analyzer Connector";
    public string Version { get; set; } = "";
    public string ConnectorId { get; set; } = "";
    public string? ConnectorName { get; set; }
    public string HostName { get; set; } = "";
    public string? ServerUrl { get; set; }
    public bool ServerConfigured { get; set; }
    public bool Online { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
    public string? LastServerError { get; set; }
    public int ConfigVersion { get; set; }
    public DateTime? ConfigSyncedAt { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public long UptimeSec { get; set; }
    public int BufferedCount { get; set; }
    public string DataDir { get; set; } = "";
    public List<AnalyzerStatus> Analyzers { get; set; } = new();
    public List<MessageLogEntry> Messages { get; set; } = new();
}

/// <summary>Потокобезпечний стан коннектора.</summary>
public sealed class StatusState
{
    private readonly ConcurrentDictionary<string, AnalyzerStatus> _analyzers = new();
    private readonly ConcurrentQueue<MessageLogEntry> _messages = new();
    private const int MaxMessages = 50;

    public DateTime? LastHeartbeatAt { get; set; }
    public bool Online { get; set; }
    public string? LastServerError { get; set; }
    public int BufferedCount { get; set; }

    public IReadOnlyCollection<AnalyzerStatus> Analyzers => _analyzers.Values.Select(a => a.Clone()).OrderBy(a => a.Name).ToList();

    public AnalyzerStatus Register(AnalyzerConfigDto cfg)
    {
        var st = _analyzers.GetOrAdd(cfg.AnalyzerId, _ => new AnalyzerStatus { AnalyzerId = cfg.AnalyzerId });
        lock (st)
        {
            st.Code = cfg.Code;
            st.Name = cfg.Name;
            st.TypeCode = cfg.TypeCode;
            st.Protocol = cfg.Protocol;
            st.OrderTemplate = cfg.OrderTemplate;
            st.Connection = DescribeConnection(cfg.Connection);
        }
        return st;
    }

    public void Remove(string analyzerId) => _analyzers.TryRemove(analyzerId, out _);

    public void Update(string analyzerId, Action<AnalyzerStatus> update)
    {
        if (_analyzers.TryGetValue(analyzerId, out var st)) lock (st) update(st);
    }

    public void SetConnected(string analyzerId, bool connected) => Update(analyzerId, s =>
    {
        s.IsConnected = connected;
        if (connected) s.LastConnectedAt = DateTime.UtcNow;
    });

    public void SetError(string analyzerId, string error) => Update(analyzerId, s =>
    {
        s.LastError = error;
        s.LastErrorAt = DateTime.UtcNow;
        s.Errors++;
    });

    public void AddMessage(MessageLogEntry entry)
    {
        _messages.Enqueue(entry);
        while (_messages.Count > MaxMessages && _messages.TryDequeue(out _)) { }
        Update(entry.AnalyzerId, s =>
        {
            s.LastMessageAt = entry.At;
            if (entry.Direction == "IN") s.MessagesIn++;
            else if (entry.Direction == "OUT") s.MessagesOut++;
            if (entry.Error != null) { s.LastError = entry.Error; s.LastErrorAt = entry.At; s.Errors++; }
        });
    }

    public void LogRaw(AnalyzerConfigDto cfg, string direction, string raw, bool parsedOk = true, string? kind = null, int results = 0, string? error = null)
        => AddMessage(new MessageLogEntry
        {
            At = DateTime.UtcNow,
            AnalyzerId = cfg.AnalyzerId,
            AnalyzerCode = cfg.Code,
            Direction = direction,
            Protocol = cfg.Protocol,
            Preview = Preview(raw),
            ParsedOk = parsedOk,
            Kind = kind,
            ResultsCount = results,
            Error = error,
        });

    public IReadOnlyList<MessageLogEntry> Messages => _messages.Reverse().ToList();

    public static string Preview(string raw, int max = 600)
    {
        var s = ProtocolText.Escape(raw ?? "", keepTab: true);
        return s.Length > max ? s[..max] + "…" : s;
    }

    public static string DescribeConnection(AnalyzerConnectionDto c)
    {
        var mode = (c.Mode ?? "TCP").ToUpperInvariant();
        return mode switch
        {
            "COM" => $"COM {c.ComPort} {c.BaudRate},{c.DataBits},{c.Parity},{c.StopBits},{c.FlowControl}",
            "FILE" => $"FILE {c.FilePath} (кожні {c.FilePollSec} с)",
            _ => c.IsServer ? $"TCP сервер :{c.Port}" : $"TCP клієнт {c.Host}:{c.Port}",
        };
    }

    public List<AnalyzerHeartbeatDto> ToHeartbeat() => _analyzers.Values.Select(a =>
    {
        lock (a) return new AnalyzerHeartbeatDto { AnalyzerId = a.AnalyzerId, IsConnected = a.IsConnected, LastMessageAt = a.LastMessageAt, LastError = a.LastError };
    }).ToList();
}
