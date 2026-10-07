// =============================================================================
// MedLink LIS Analyzer Connector — файловий обмін: опитування каталогу (кожні N с),
// кожен новий файл = одне вхідне повідомлення; оброблені файли переносяться у підкаталог
// processed/. Відправка (замовлення) — файл у підкаталог out/.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using MedLink.LIS.Core.Contracts;

namespace MedLink.LabConnector.Transport;

public sealed class FileDropTransport : ITransport
{
    private readonly AnalyzerConnectionDto _c;
    private readonly string _archiveDirName;
    private readonly ILogger _logger;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _connected;

    public FileDropTransport(AnalyzerConnectionDto connection, string archiveDirName, ILogger logger)
    {
        _c = connection;
        _archiveDirName = string.IsNullOrWhiteSpace(archiveDirName) ? "processed" : archiveDirName;
        _logger = logger;
    }

    public string Description => $"FILE {_c.FilePath}";
    public bool IsConnected => _connected;
    public string InputDir => _c.FilePath ?? "";
    public string OutputDir => Path.Combine(InputDir, "out");
    public string ArchiveDir => Path.Combine(InputDir, _archiveDirName);

    public event Action<byte[]>? DataReceived;
    public event Action<bool>? ConnectionChanged;

    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = Task.Run(() => PollLoop(_cts.Token));
        return Task.CompletedTask;
    }

    private async Task PollLoop(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(2, _c.FilePollSec));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(InputDir)) throw new InvalidOperationException("Не задано каталог (filePath)");
                Directory.CreateDirectory(InputDir);
                Directory.CreateDirectory(ArchiveDir);
                Directory.CreateDirectory(OutputDir);
                if (!_connected) { _connected = true; ConnectionChanged?.Invoke(true); _logger.LogInformation("Опитування каталогу {Dir} кожні {Sec} с", InputDir, _c.FilePollSec); }
                foreach (var file in Directory.EnumerateFiles(InputDir).OrderBy(File.GetLastWriteTimeUtc).ToList())
                {
                    if (ct.IsCancellationRequested) break;
                    if (!IsStable(file)) continue;
                    byte[] data;
                    try { data = await File.ReadAllBytesAsync(file, ct); }
                    catch (IOException) { continue; } // ще пишеться
                    var target = Path.Combine(ArchiveDir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(file)}");
                    try { File.Move(file, target, true); }
                    catch (Exception ex) { _logger.LogWarning("Не вдалося перенести файл {File}: {Error}", file, ex.Message); continue; }
                    _logger.LogInformation("Отримано файл {File} ({Bytes} байт)", Path.GetFileName(file), data.Length);
                    DataReceived?.Invoke(data);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (_connected) { _connected = false; ConnectionChanged?.Invoke(false); }
                _logger.LogWarning("Файловий обмін {Dir}: {Error}", InputDir, ex.Message);
            }
            try { await Task.Delay(interval, ct); } catch { break; }
        }
    }

    private static bool IsStable(string file)
    {
        try
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(file);
            return age > TimeSpan.FromSeconds(2);
        }
        catch { return false; }
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        Directory.CreateDirectory(OutputDir);
        var file = Path.Combine(OutputDir, $"order_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
        await File.WriteAllBytesAsync(file, data.ToArray(), ct);
        _logger.LogInformation("Замовлення записано у файл {File}", file);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_loop != null) { try { await _loop; } catch { } }
        if (_connected) { _connected = false; ConnectionChanged?.Invoke(false); }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
