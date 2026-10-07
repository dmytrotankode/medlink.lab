// =============================================================================
// MedLink LIS Analyzer Connector — TCP-сервер: приймає кілька клієнтів;
// дані від усіх клієнтів зливаються в один потік; відправка — активному клієнту
// (той, що надсилав останнім), інакше всім.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace MedLink.LabConnector.Transport;

public sealed class TcpServerTransport : ITransport
{
    private readonly int _port;
    private readonly IPAddress _bind;
    private readonly ILogger _logger;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private readonly ConcurrentDictionary<TcpClient, Task> _clients = new();
    private volatile TcpClient? _active;

    public TcpServerTransport(int port, string? bindHost, ILogger logger)
    {
        _port = port;
        _logger = logger;
        _bind = IPAddress.Any;
        if (!string.IsNullOrWhiteSpace(bindHost) && IPAddress.TryParse(bindHost, out var ip) && !IPAddress.IsLoopback(ip) && ip.ToString() != "0.0.0.0")
        {
            // Хост у конфігурації сервера — лише інформаційний (адреса приладу); слухаємо всі інтерфейси.
        }
    }

    public string Description => $"TCP сервер :{_port}";
    public bool IsConnected => !_clients.IsEmpty;
    public int ClientCount => _clients.Count;

    public event Action<byte[]>? DataReceived;
    public event Action<bool>? ConnectionChanged;

    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listener = new TcpListener(_bind, _port);
        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Start();
        _logger.LogInformation("TCP-сервер слухає порт {Port}", _port);
        _acceptTask = Task.Run(() => AcceptLoop(_cts.Token));
        return Task.CompletedTask;
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning("Помилка прийому TCP-з'єднання: {Error}", ex.Message);
                try { await Task.Delay(1000, ct); } catch { break; }
                continue;
            }
            client.NoDelay = true;
            _logger.LogInformation("Прилад підключився: {Remote} (порт {Port})", client.Client.RemoteEndPoint, _port);
            _active = client;
            var task = Task.Run(() => ReadLoop(client, ct), ct);
            _clients[client] = task;
            ConnectionChanged?.Invoke(true);
        }
    }

    private async Task ReadLoop(TcpClient client, CancellationToken ct)
    {
        var buffer = new byte[8192];
        try
        {
            var stream = client.GetStream();
            while (!ct.IsCancellationRequested)
            {
                int n = await stream.ReadAsync(buffer, ct);
                if (n <= 0) break;
                _active = client;
                var data = new byte[n];
                Buffer.BlockCopy(buffer, 0, data, 0, n);
                DataReceived?.Invoke(data);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception ex)
        {
            _logger.LogWarning("Помилка читання TCP: {Error}", ex.Message);
        }
        finally
        {
            _clients.TryRemove(client, out _);
            if (ReferenceEquals(_active, client)) _active = _clients.Keys.FirstOrDefault();
            try { client.Close(); } catch { }
            _logger.LogInformation("Прилад відключився (порт {Port}); активних з'єднань: {Count}", _port, _clients.Count);
            ConnectionChanged?.Invoke(!_clients.IsEmpty);
        }
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var target = _active;
        if (target == null || !target.Connected)
        {
            target = _clients.Keys.FirstOrDefault(c => c.Connected);
            if (target == null) throw new InvalidOperationException("Немає підключеного приладу (TCP-сервер без клієнтів)");
        }
        await target.GetStream().WriteAsync(data, ct);
        await target.GetStream().FlushAsync(ct);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        foreach (var c in _clients.Keys) { try { c.Close(); } catch { } }
        if (_acceptTask != null) { try { await _acceptTask; } catch { } }
        _clients.Clear();
        ConnectionChanged?.Invoke(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
