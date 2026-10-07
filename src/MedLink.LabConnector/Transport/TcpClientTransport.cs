// =============================================================================
// MedLink LIS Analyzer Connector — TCP-клієнт із автоперепідключенням (кожні 5 с)
// та keep-alive NUL кожні 60 с тиші (як TimerConnect у legacy Delphi-коннекторі).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Net.Sockets;

namespace MedLink.LabConnector.Transport;

public sealed class TcpClientTransport : ITransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _reconnectSec;
    private readonly int _keepAliveSec;
    private readonly ILogger _logger;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private volatile TcpClient? _client;
    private DateTime _lastRx = DateTime.UtcNow;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public TcpClientTransport(string host, int port, int reconnectSec, int keepAliveSec, ILogger logger)
    {
        _host = host;
        _port = port;
        _reconnectSec = Math.Max(1, reconnectSec);
        _keepAliveSec = keepAliveSec;
        _logger = logger;
    }

    public string Description => $"TCP клієнт {_host}:{_port}";
    public bool IsConnected => _client?.Connected == true;

    public event Action<byte[]>? DataReceived;
    public event Action<bool>? ConnectionChanged;

    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = Task.Run(() => ConnectLoop(_cts.Token));
        return Task.CompletedTask;
    }

    private async Task ConnectLoop(CancellationToken ct)
    {
        string? lastError = null;
        while (!ct.IsCancellationRequested)
        {
            TcpClient client = new() { NoDelay = true };
            try
            {
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(TimeSpan.FromSeconds(10));
                await client.ConnectAsync(_host, _port, connectCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { client.Dispose(); break; }
            catch (Exception ex)
            {
                client.Dispose();
                if (ex.Message != lastError)
                {
                    _logger.LogWarning("Не вдалося підключитися до {Host}:{Port}: {Error}. Повтор кожні {Sec} с.", _host, _port, ex.Message, _reconnectSec);
                    lastError = ex.Message;
                }
                try { await Task.Delay(TimeSpan.FromSeconds(_reconnectSec), ct); } catch { break; }
                continue;
            }
            lastError = null;
            _client = client;
            _lastRx = DateTime.UtcNow;
            _logger.LogInformation("Підключено до приладу {Host}:{Port}", _host, _port);
            ConnectionChanged?.Invoke(true);
            await ReadLoop(client, ct);
            _client = null;
            try { client.Close(); } catch { }
            ConnectionChanged?.Invoke(false);
            if (ct.IsCancellationRequested) break;
            _logger.LogWarning("З'єднання з {Host}:{Port} втрачено; перепідключення через {Sec} с", _host, _port, _reconnectSec);
            try { await Task.Delay(TimeSpan.FromSeconds(_reconnectSec), ct); } catch { break; }
        }
    }

    private async Task ReadLoop(TcpClient client, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var stream = client.GetStream();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var readTask = stream.ReadAsync(buffer, ct).AsTask();
                var delay = _keepAliveSec > 0 ? Task.Delay(TimeSpan.FromSeconds(_keepAliveSec), ct) : Task.Delay(Timeout.Infinite, ct);
                var done = await Task.WhenAny(readTask, delay);
                if (done == readTask)
                {
                    int n = await readTask;
                    if (n <= 0) return;
                    _lastRx = DateTime.UtcNow;
                    var data = new byte[n];
                    Buffer.BlockCopy(buffer, 0, data, 0, n);
                    DataReceived?.Invoke(data);
                }
                else
                {
                    // тиша понад keepAlive → NUL (legacy: SendChar(#0)); помилка запису = розрив
                    if ((DateTime.UtcNow - _lastRx).TotalSeconds >= _keepAliveSec)
                    {
                        try { await SendAsync(new byte[] { 0 }, ct); }
                        catch { return; }
                        _lastRx = DateTime.UtcNow;
                    }
                    // чекаємо завершення читання, що триває
                    var n2 = await readTask;
                    if (n2 <= 0) return;
                    _lastRx = DateTime.UtcNow;
                    var data = new byte[n2];
                    Buffer.BlockCopy(buffer, 0, data, 0, n2);
                    DataReceived?.Invoke(data);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception ex)
        {
            _logger.LogWarning("Помилка читання TCP {Host}:{Port}: {Error}", _host, _port, ex.Message);
        }
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var c = _client;
        if (c == null || !c.Connected) throw new InvalidOperationException($"Немає з'єднання з приладом {_host}:{_port}");
        await _sendLock.WaitAsync(ct);
        try
        {
            var s = c.GetStream();
            await s.WriteAsync(data, ct);
            await s.FlushAsync(ct);
        }
        finally { _sendLock.Release(); }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        try { _client?.Close(); } catch { }
        if (_loop != null) { try { await _loop; } catch { } }
        _client = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
        _sendLock.Dispose();
    }
}
