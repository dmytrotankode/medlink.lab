// =============================================================================
// MedLink LIS Analyzer Connector — простий файловий логер із щоденною ротацією
// (data/logs/connector-yyyyMMdd.log) та окремими файлами «сирого» трафіку
// (data/logs/raw-{code}-yyyyMMdd.log). Без зовнішніх залежностей.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Collections.Concurrent;
using System.Text;

namespace MedLink.LabConnector.Logging;

public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly RollingFileWriter _writer;
    private readonly LogLevel _minLevel;

    public RollingFileLoggerProvider(string logsDir, int retentionDays, LogLevel minLevel = LogLevel.Information)
    {
        _writer = new RollingFileWriter(logsDir, "connector", retentionDays);
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _writer, _minLevel);

    public void Dispose() => _writer.Dispose();

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly RollingFileWriter _writer;
        private readonly LogLevel _min;

        public FileLogger(string category, RollingFileWriter writer, LogLevel min)
        {
            _category = category.Contains('.') ? category[(category.LastIndexOf('.') + 1)..] : category;
            _writer = writer;
            _min = min;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= _min && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(' ')
              .Append(Level(logLevel)).Append(" [").Append(_category).Append("] ")
              .Append(formatter(state, exception));
            if (exception != null) sb.Append(" | ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);
            _writer.Write(sb.ToString());
        }

        private static string Level(LogLevel l) => l switch
        {
            LogLevel.Trace => "TRC", LogLevel.Debug => "DBG", LogLevel.Information => "INF",
            LogLevel.Warning => "WRN", LogLevel.Error => "ERR", LogLevel.Critical => "CRT", _ => "???",
        };
    }
}

/// <summary>Потокобезпечний запис у файл із щоденною ротацією та очищенням старих файлів.</summary>
public sealed class RollingFileWriter : IDisposable
{
    private readonly string _dir;
    private readonly string _prefix;
    private readonly int _retentionDays;
    private readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>(), 20000);
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();
    private DateTime _lastCleanup = DateTime.MinValue;

    public RollingFileWriter(string dir, string prefix, int retentionDays)
    {
        _dir = dir;
        _prefix = prefix;
        _retentionDays = Math.Max(1, retentionDays);
        Directory.CreateDirectory(dir);
        _thread = new Thread(Run) { IsBackground = true, Name = $"log-{prefix}" };
        _thread.Start();
    }

    public void Write(string line)
    {
        if (_cts.IsCancellationRequested) return;
        _queue.TryAdd(line);
    }

    private void Run()
    {
        try
        {
            foreach (var line in _queue.GetConsumingEnumerable(_cts.Token))
            {
                try
                {
                    var file = Path.Combine(_dir, $"{_prefix}-{DateTime.Now:yyyyMMdd}.log");
                    using var sw = new StreamWriter(file, true, new UTF8Encoding(false));
                    sw.WriteLine(line);
                    // дописуємо все, що вже у черзі
                    while (_queue.TryTake(out var more)) sw.WriteLine(more);
                    Cleanup();
                }
                catch { /* журнал не повинен валити службу */ }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void Cleanup()
    {
        if ((DateTime.Now - _lastCleanup).TotalHours < 1) return;
        _lastCleanup = DateTime.Now;
        try
        {
            foreach (var f in Directory.EnumerateFiles(_dir, "*.log"))
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-_retentionDays)) File.Delete(f);
        }
        catch { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.CompleteAdding();
        _thread.Join(2000);
    }
}

/// <summary>Журнал «сирого» трафіку приладів (по одному файлу на код аналізатора на день).</summary>
public sealed class RawTrafficLog : IDisposable
{
    private readonly string _dir;
    private readonly int _retentionDays;
    private readonly bool _enabled;
    private readonly ConcurrentDictionary<string, RollingFileWriter> _writers = new();

    public RawTrafficLog(string logsDir, int retentionDays, bool enabled)
    {
        _dir = logsDir;
        _retentionDays = retentionDays;
        _enabled = enabled;
    }

    public void Write(string analyzerCode, string direction, string raw)
    {
        if (!_enabled) return;
        var safe = new string((analyzerCode ?? "analyzer").Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        var w = _writers.GetOrAdd(safe, k => new RollingFileWriter(_dir, "raw-" + k, _retentionDays));
        w.Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {direction,-3} {MedLink.LIS.Core.Protocols.ProtocolText.Escape(raw, keepTab: true)}");
    }

    public void Dispose()
    {
        foreach (var w in _writers.Values) w.Dispose();
    }
}
