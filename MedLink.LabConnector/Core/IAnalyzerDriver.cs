// =============================================================================
// MedLink LIS: Analyzer Driver Interface
// Copyright (c) 2026 MedLink. All rights reserved.
// =============================================================================

namespace MedLink.LabConnector.Core;

public interface IAnalyzerDriver
{
    string AnalyzerId { get; }
    string AnalyzerCode { get; }
    string AnalyzerName { get; }
    bool IsConnected { get; }

    Task StartListeningAsync(CancellationToken cancellationToken);
    Task StopAsync();
}
