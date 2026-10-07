// =============================================================================
// MedLink LIS Analyzer Connector — локальний агент друку етикеток (ZPL):
// мережевий принтер TCP:9100 (таймаут 5 с) або локальний RAW-принтер
// (Windows: winspool OpenPrinter/StartDocPrinter/WritePrinter; Linux: lp -d NAME -o raw).
// Список принтерів: Windows — PowerShell Get-Printer / wmic; Linux — lpstat -a.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using MedLink.LabConnector.Configuration;
using Microsoft.Extensions.Options;

namespace MedLink.LabConnector.Printing;

public sealed class PrinterTarget
{
    /// <summary>Мережевий принтер: IP/ім'я хоста.</summary>
    public string? Host { get; set; }
    public int? Port { get; set; }
    /// <summary>Локальний принтер (черга друку ОС).</summary>
    public string? Name { get; set; }

    public bool IsNetwork => !string.IsNullOrWhiteSpace(Host);
    public bool IsEmpty => string.IsNullOrWhiteSpace(Host) && string.IsNullOrWhiteSpace(Name);
    public override string ToString() => IsNetwork ? $"{Host}:{Port ?? 9100}" : Name ?? "(не задано)";
}

public sealed class PrintRequest
{
    public string Zpl { get; set; } = "";
    public PrinterTarget? Printer { get; set; }
    /// <summary>Кількість копій (ZPL ^PQ додається, якщо > 1 і у коді немає ^PQ).</summary>
    public int Copies { get; set; } = 1;
}

public sealed class PrintResult
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public string Printer { get; set; } = "";
    public int Bytes { get; set; }
}

public sealed class LabelPrintService
{
    private readonly IOptionsMonitor<ConnectorOptions> _options;
    private readonly ILogger<LabelPrintService> _logger;
    public DateTime? LastPrintAt { get; private set; }
    public string? LastPrintResult { get; private set; }
    public long PrintedCount { get; private set; }

    public LabelPrintService(IOptionsMonitor<ConnectorOptions> options, ILogger<LabelPrintService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public PrinterTarget DefaultPrinter
    {
        get
        {
            var p = _options.CurrentValue.Printing;
            return new PrinterTarget { Host = p.Host, Port = p.Port, Name = p.DefaultLabelPrinter };
        }
    }

    public async Task<PrintResult> PrintZplAsync(PrintRequest req, CancellationToken ct)
    {
        var target = req.Printer == null || req.Printer.IsEmpty ? DefaultPrinter : req.Printer;
        var result = new PrintResult { Printer = target.ToString() };
        try
        {
            if (string.IsNullOrWhiteSpace(req.Zpl)) throw new ArgumentException("Порожній ZPL");
            if (target.IsEmpty) throw new InvalidOperationException("Принтер не задано: вкажіть printer у запиті або Printing:DefaultLabelPrinter у налаштуваннях");
            var zpl = req.Zpl;
            if (req.Copies > 1 && !zpl.Contains("^PQ", StringComparison.OrdinalIgnoreCase))
                zpl = zpl.Replace("^XZ", $"^PQ{req.Copies}^XZ", StringComparison.OrdinalIgnoreCase);
            var bytes = Encoding.UTF8.GetBytes(zpl);
            result.Bytes = bytes.Length;
            if (target.IsNetwork) await SendTcpAsync(target.Host!, target.Port ?? 9100, bytes, ct);
            else await SendRawToLocalPrinterAsync(target.Name!, bytes, ct);
            result.Ok = true;
            result.Message = $"Надруковано ({bytes.Length} байт) на {target}";
            PrintedCount++;
            _logger.LogInformation("Друк етикетки: {Bytes} байт → {Printer}", bytes.Length, target);
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Message = "Помилка друку: " + ex.Message;
            _logger.LogWarning("Друк етикетки на {Printer} не вдався: {Error}", target, ex.Message);
        }
        LastPrintAt = DateTime.UtcNow;
        LastPrintResult = result.Message;
        return result;
    }

    /// <summary>Тестова етикетка 40×25 мм (203 dpi).</summary>
    public static string TestLabelZpl(string hostName) =>
        "^XA^CI28^PW320^LL200^LH0,0" +
        "^FO15,15^A0N,28,28^FDMedLink LIS^FS" +
        $"^FO15,50^A0N,20,20^FDТест друку {DateTime.Now:dd.MM.yyyy HH:mm}^FS" +
        $"^FO15,78^A0N,18,18^FD{hostName}^FS" +
        "^FO15,105^BY2^BCN,60,Y,N,N^FD10260048^FS" +
        "^XZ";

    public static async Task SendTcpAsync(string host, int port, byte[] data, CancellationToken ct)
    {
        using var client = new TcpClient { NoDelay = true, SendTimeout = 5000, ReceiveTimeout = 5000 };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            var stream = client.GetStream();
            await stream.WriteAsync(data, cts.Token);
            await stream.FlushAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Принтер {host}:{port} не відповідає (таймаут 5 с)");
        }
    }

    public static async Task SendRawToLocalPrinterAsync(string printerName, byte[] data, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            RawPrinterHelper.SendBytes(printerName, data, "MedLink label");
            return;
        }
        // Linux/macOS: CUPS lp -d NAME -o raw
        var tmp = Path.Combine(Path.GetTempPath(), $"medlink_label_{Guid.NewGuid():N}.zpl");
        await File.WriteAllBytesAsync(tmp, data, ct);
        try
        {
            var psi = new ProcessStartInfo("lp") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
            psi.ArgumentList.Add("-d"); psi.ArgumentList.Add(printerName);
            psi.ArgumentList.Add("-o"); psi.ArgumentList.Add("raw");
            psi.ArgumentList.Add(tmp);
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Не вдалося запустити lp (CUPS не встановлено?)");
            var err = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0) throw new InvalidOperationException($"lp завершився з кодом {proc.ExitCode}: {err.Trim()}");
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    /// <summary>Перелік принтерів ОС (без System.Drawing — крос-платформно).</summary>
    public static async Task<List<string>> ListPrintersAsync(CancellationToken ct)
    {
        var list = new List<string>();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var output = await RunAsync("powershell", new[] { "-NoProfile", "-NonInteractive", "-Command", "Get-Printer | Select-Object -ExpandProperty Name" }, ct);
                if (string.IsNullOrWhiteSpace(output))
                {
                    output = await RunAsync("wmic", new[] { "printer", "get", "name" }, ct);
                    output = string.Join('\n', output.Split('\n').Skip(1));
                }
                list.AddRange(output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
            }
            else
            {
                var output = await RunAsync("lpstat", new[] { "-a" }, ct);
                list.AddRange(output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Select(l => l.Split(' ')[0]));
            }
        }
        catch (Exception ex)
        {
            list.Add($"(не вдалося отримати список принтерів: {ex.Message})");
        }
        return list;
    }

    private static async Task<string> RunAsync(string file, string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi);
        if (proc == null) return "";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        var output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
        await proc.WaitForExitAsync(cts.Token);
        return output;
    }
}

/// <summary>Windows winspool.drv: RAW-друк байтів у чергу принтера.</summary>
public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOC_INFO_1
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string printerName, out IntPtr hPrinter, IntPtr pDefault);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);
    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr hPrinter, int level, ref DOC_INFO_1 docInfo);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int count, out int written);

    public static void SendBytes(string printerName, byte[] data, string docName)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("winspool доступний лише на Windows");
        if (!OpenPrinter(printerName, out var h, IntPtr.Zero))
            throw new InvalidOperationException($"OpenPrinter('{printerName}') не вдався: код {Marshal.GetLastWin32Error()}");
        try
        {
            var di = new DOC_INFO_1 { pDocName = docName, pOutputFile = null, pDataType = "RAW" };
            if (StartDocPrinter(h, 1, ref di) == 0) throw new InvalidOperationException($"StartDocPrinter: код {Marshal.GetLastWin32Error()}");
            try
            {
                if (!StartPagePrinter(h)) throw new InvalidOperationException($"StartPagePrinter: код {Marshal.GetLastWin32Error()}");
                var p = Marshal.AllocHGlobal(data.Length);
                try
                {
                    Marshal.Copy(data, 0, p, data.Length);
                    if (!WritePrinter(h, p, data.Length, out var written) || written != data.Length)
                        throw new InvalidOperationException($"WritePrinter: записано {written} з {data.Length}, код {Marshal.GetLastWin32Error()}");
                }
                finally { Marshal.FreeHGlobal(p); }
                EndPagePrinter(h);
            }
            finally { EndDocPrinter(h); }
        }
        finally { ClosePrinter(h); }
    }
}
