// =============================================================================
// MedLink LIS Analyzer Connector — абстракція каналу зв'язку з приладом
// (TCP-сервер, TCP-клієнт, COM-порт, каталог файлів).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using MedLink.LIS.Core.Contracts;

namespace MedLink.LabConnector.Transport;

public interface ITransport : IAsyncDisposable
{
    /// <summary>Опис каналу для журналів/статусу.</summary>
    string Description { get; }
    bool IsConnected { get; }

    /// <summary>Отримані байти (викликається з потоку читання; обробник має бути швидким — лише покласти у чергу).</summary>
    event Action<byte[]>? DataReceived;
    /// <summary>Зміна стану підключення.</summary>
    event Action<bool>? ConnectionChanged;

    Task StartAsync(CancellationToken ct);
    Task StopAsync();
    /// <summary>Відправляє байти приладу. Кидає InvalidOperationException, якщо немає підключення.</summary>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct);
}

/// <summary>Параметри транспорту, що беруться з локальних налаштувань.</summary>
public sealed class TransportOptions
{
    public int ReconnectSec { get; init; } = 5;
    public int KeepAliveSec { get; init; } = 60;
    public string FileArchiveDirName { get; init; } = "processed";
}

public static class TransportFactory
{
    public static ITransport Create(AnalyzerConfigDto cfg, TransportOptions options, ILogger logger)
    {
        var c = cfg.Connection ?? new AnalyzerConnectionDto();
        var mode = (c.Mode ?? "TCP").Trim().ToUpperInvariant();
        return mode switch
        {
            "COM" or "SERIAL" => new SerialTransport(c, logger),
            "FILE" => new FileDropTransport(c, options.FileArchiveDirName, logger),
            _ => c.IsServer ? new TcpServerTransport(c.Port ?? 0, c.Host, logger) : new TcpClientTransport(c.Host ?? "127.0.0.1", c.Port ?? 0, options.ReconnectSec, options.KeepAliveSec, logger),
        };
    }
}
