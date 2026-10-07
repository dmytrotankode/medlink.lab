// =============================================================================
// MedLink LIS Analyzer Connector — COM-порт (System.IO.Ports) з мапінгом
// parity / stopBits / flowControl із конфігурації ЛІС та автоматичним повторним
// відкриттям порту при помилці.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.IO.Ports;
using MedLink.LIS.Core.Contracts;

namespace MedLink.LabConnector.Transport;

public sealed class SerialTransport : ITransport
{
    private readonly AnalyzerConnectionDto _c;
    private readonly ILogger _logger;
    private SerialPort? _port;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public SerialTransport(AnalyzerConnectionDto connection, ILogger logger)
    {
        _c = connection;
        _logger = logger;
    }

    public string Description => $"COM {_c.ComPort} {_c.BaudRate},{_c.DataBits},{_c.Parity},{_c.StopBits}";
    public bool IsConnected => _port?.IsOpen == true;

    public event Action<byte[]>? DataReceived;
    public event Action<bool>? ConnectionChanged;

    public static Parity MapParity(string? p) => (p ?? "None").Trim().ToLowerInvariant() switch
    {
        "even" or "e" or "peven" => Parity.Even,
        "odd" or "o" or "podd" => Parity.Odd,
        "mark" or "m" or "pmark" => Parity.Mark,
        "space" or "s" or "pspace" => Parity.Space,
        _ => Parity.None,
    };

    public static StopBits MapStopBits(string? s) => (s ?? "One").Trim().ToLowerInvariant() switch
    {
        "two" or "2" or "sbtwo" => StopBits.Two,
        "oneandhalf" or "1.5" or "sboneandhalf" => StopBits.OnePointFive,
        "none" or "0" => StopBits.None,
        _ => StopBits.One,
    };

    public static Handshake MapFlowControl(string? f) => (f ?? "None").Trim().ToLowerInvariant() switch
    {
        "xonxoff" or "software" or "spxonxoff" => Handshake.XOnXOff,
        "hardware" or "rtscts" or "sphardware" => Handshake.RequestToSend,
        "both" => Handshake.RequestToSendXOnXOff,
        _ => Handshake.None,
    };

    /// <summary>Нормалізує назву порту: "1" → COM1 (Windows) / /dev/ttyS0 (Linux); "COM3", "/dev/ttyUSB0" — як є.</summary>
    public static string NormalizePortName(string? name)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) return OperatingSystem.IsWindows() ? "COM1" : "/dev/ttyUSB0";
        if (int.TryParse(n, out var num)) return OperatingSystem.IsWindows() ? $"COM{num}" : $"/dev/ttyS{num - 1}";
        if (OperatingSystem.IsWindows() && n.StartsWith("COM", StringComparison.OrdinalIgnoreCase) && int.TryParse(n[3..], out var cn) && cn >= 10) return @"\\.\" + n.ToUpperInvariant();
        return n;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = Task.Run(() => OpenLoop(_cts.Token));
        return Task.CompletedTask;
    }

    private async Task OpenLoop(CancellationToken ct)
    {
        string? lastError = null;
        while (!ct.IsCancellationRequested)
        {
            SerialPort port;
            try
            {
                port = new SerialPort(NormalizePortName(_c.ComPort), _c.BaudRate <= 0 ? 9600 : _c.BaudRate, MapParity(_c.Parity), _c.DataBits is 5 or 6 or 7 or 8 ? _c.DataBits : 8, MapStopBits(_c.StopBits))
                {
                    Handshake = MapFlowControl(_c.FlowControl),
                    ReadTimeout = 500,
                    WriteTimeout = 5000,
                    DtrEnable = true,
                    RtsEnable = MapFlowControl(_c.FlowControl) != Handshake.RequestToSend,
                    Encoding = System.Text.Encoding.Latin1,
                };
                port.Open();
            }
            catch (Exception ex)
            {
                if (ex.Message != lastError)
                {
                    _logger.LogWarning("Не вдалося відкрити {Port}: {Error}. Повтор кожні 5 с.", NormalizePortName(_c.ComPort), ex.Message);
                    lastError = ex.Message;
                }
                try { await Task.Delay(5000, ct); } catch { break; }
                continue;
            }
            lastError = null;
            _port = port;
            _logger.LogInformation("Відкрито {Desc}", Description);
            ConnectionChanged?.Invoke(true);
            await ReadLoop(port, ct);
            _port = null;
            try { port.Close(); port.Dispose(); } catch { }
            ConnectionChanged?.Invoke(false);
            if (ct.IsCancellationRequested) break;
            _logger.LogWarning("Порт {Port} закрито через помилку; повторне відкриття через 5 с", _c.ComPort);
            try { await Task.Delay(5000, ct); } catch { break; }
        }
    }

    private async Task ReadLoop(SerialPort port, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            var stream = port.BaseStream;
            while (!ct.IsCancellationRequested && port.IsOpen)
            {
                int n;
                try { n = await stream.ReadAsync(buffer, ct); }
                catch (TimeoutException) { continue; }
                if (n <= 0) { await Task.Delay(20, ct); continue; }
                var data = new byte[n];
                Buffer.BlockCopy(buffer, 0, data, 0, n);
                DataReceived?.Invoke(data);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning("Помилка читання {Port}: {Error}", _c.ComPort, ex.Message);
        }
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var p = _port;
        if (p == null || !p.IsOpen) throw new InvalidOperationException($"COM-порт {_c.ComPort} не відкрито");
        await _sendLock.WaitAsync(ct);
        try
        {
            await p.BaseStream.WriteAsync(data, ct);
            await p.BaseStream.FlushAsync(ct);
        }
        finally { _sendLock.Release(); }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        try { _port?.Close(); } catch { }
        if (_loop != null) { try { await _loop; } catch { } }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
        _sendLock.Dispose();
    }
}
