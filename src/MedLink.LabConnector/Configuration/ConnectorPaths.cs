// =============================================================================
// MedLink LIS Analyzer Connector — шляхи до файлів даних.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
namespace MedLink.LabConnector.Configuration;

public sealed class ConnectorPaths
{
    public ConnectorPaths(string? dataDir)
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = string.IsNullOrWhiteSpace(dataDir) ? "data" : dataDir;
        DataDir = Path.IsPathRooted(dir) ? dir : Path.GetFullPath(Path.Combine(baseDir, dir));
    }

    public string DataDir { get; }
    public string LogsDir => Path.Combine(DataDir, "logs");
    public string LocalSettingsFile => Path.Combine(DataDir, "appsettings.local.json");
    public string ConfigCacheFile => Path.Combine(DataDir, "config.cache.json");
    public string OfflineBufferFile => Path.Combine(DataDir, "offline_buffer.db");
    public string FileDropArchiveDirName => "processed";

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogsDir);
    }

    /// <summary>Каталог даних за замовчуванням, визначений лише з аргументів/оточення (для CLI до побудови хоста).</summary>
    public static ConnectorPaths FromEnvironment(string? dataDirOverride = null)
    {
        var env = Environment.GetEnvironmentVariable("MEDLINK_CONNECTOR_DATA");
        return new ConnectorPaths(dataDirOverride ?? env);
    }
}
