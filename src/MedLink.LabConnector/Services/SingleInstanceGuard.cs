// FR-CON-007: один запущений екземпляр коннектора на ПК. Глобальний mutex (Windows) + блокувальний файл у каталозі даних
// (усі ОС) + перевірка зайнятості порту сторінки статусу (той самий порт = той самий прилад/порт підключення).
using System.Net;
using System.Net.Sockets;

namespace MedLink.LabConnector.Services;

public sealed class SingleInstanceGuard : IDisposable
{
    private Mutex? _mutex;
    private FileStream? _lock;

    /// <summary>true — цей процес перший; false — екземпляр уже запущено (повідомлення в <paramref name="error"/>).</summary>
    public static bool TryAcquire(string dataDir, int statusPort, out SingleInstanceGuard? guard, out string? error)
    {
        guard = null; error = null;
        var g = new SingleInstanceGuard();
        const string msg = "Коннектор уже запущено на цьому ПК. Закрийте інший екземпляр (службу або консольний запуск) і повторіть.";
        try
        {
            if (OperatingSystem.IsWindows())
            {
                g._mutex = new Mutex(false, @"Global\MedLinkLabConnector_SingleInstance");
                if (!g._mutex.WaitOne(0)) { g._mutex.Dispose(); g._mutex = null; error = msg; return false; }
            }
            Directory.CreateDirectory(dataDir);
            g._lock = new FileStream(Path.Combine(dataDir, "connector.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException) { g.Dispose(); error = msg; return false; }
        catch (AbandonedMutexException) { /* попередній екземпляр аварійно завершився — забираємо mutex */ }

        if (!IsPortFree(statusPort)) { g.Dispose(); error = $"Порт {statusPort} (сторінка статусу) зайнятий іншою програмою або іншим екземпляром коннектора."; return false; }
        guard = g;
        return true;
    }

    private static bool IsPortFree(int port)
    {
        try { var l = new TcpListener(IPAddress.Loopback, port); l.Start(); l.Stop(); return true; }
        catch (SocketException) { return false; }
    }

    public void Dispose()
    {
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose(); _lock?.Dispose();
    }
}
