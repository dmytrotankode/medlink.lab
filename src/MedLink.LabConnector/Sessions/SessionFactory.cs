// =============================================================================
// MedLink LIS Analyzer Connector — вибір сесії за протоколом профілю.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using MedLink.LabConnector.Transport;
using MedLink.LIS.Core.Contracts;

namespace MedLink.LabConnector.Sessions;

public static class SessionFactory
{
    public static IAnalyzerSession Create(AnalyzerConfigDto cfg, ITransport transport, IOrderSource orders, IInboundSink sink, SessionOptions options, ILogger logger)
    {
        var protocol = (cfg.Protocol ?? "ASTM").Trim().ToUpperInvariant();
        return protocol switch
        {
            "HL7" => new Hl7MllpSession(cfg, transport, orders, sink, options, logger),
            "ASTM" or "ASTM_ASK" or "ASTM2" => new AstmSession(cfg, transport, orders, sink, options, logger),
            _ => new TextSession(cfg, transport, orders, sink, options, logger),
        };
    }

    public static string SessionKind(string? protocol) => (protocol ?? "ASTM").Trim().ToUpperInvariant() switch
    {
        "HL7" => "HL7/MLLP",
        "ASTM" or "ASTM_ASK" or "ASTM2" => "ASTM E1381",
        var p => $"TEXT ({p})",
    };
}
