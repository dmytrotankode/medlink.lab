// =============================================================================
// MedLink LIS Analyzer Connector — офлайн-буфер (SQLite) для результатів та журналу
// обміну, які не вдалося відправити на сервер (store-and-forward).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using MedLink.LabConnector.Configuration;
using Microsoft.Data.Sqlite;

namespace MedLink.LabConnector.Storage;

public sealed class BufferedItem
{
    public long Id { get; set; }
    public string Kind { get; set; } = "";       // results | message | logs
    public string Path { get; set; } = "";       // відносний шлях API
    public string AnalyzerId { get; set; } = "";
    public string Json { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}

public sealed class OfflineBuffer
{
    private readonly string _connectionString;
    private readonly ILogger<OfflineBuffer> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile int _count;

    public OfflineBuffer(ConnectorPaths paths, ILogger<OfflineBuffer> logger)
    {
        paths.EnsureDirectories();
        _connectionString = new SqliteConnectionStringBuilder { DataSource = paths.OfflineBufferFile, Cache = SqliteCacheMode.Shared }.ToString();
        _logger = logger;
    }

    /// <summary>Кількість елементів у буфері (кешована).</summary>
    public int Count => _count;

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode=WAL;
                CREATE TABLE IF NOT EXISTS outbox (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    kind TEXT NOT NULL,
                    path TEXT NOT NULL,
                    analyzer_id TEXT NOT NULL DEFAULT '',
                    payload_json TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    retry_count INTEGER NOT NULL DEFAULT 0,
                    last_error TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_outbox_created ON outbox(created_at);";
            await cmd.ExecuteNonQueryAsync(ct);
            _count = await CountInternalAsync(conn, ct);
            _logger.LogInformation("Офлайн-буфер ініціалізовано: {File}, елементів у черзі: {Count}", conn.DataSource, _count);
        }
        finally { _gate.Release(); }
    }

    public async Task EnqueueAsync(string kind, string path, string analyzerId, string json, string? error, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO outbox(kind, path, analyzer_id, payload_json, created_at, last_error) VALUES(@k, @p, @a, @j, @c, @e)";
            cmd.Parameters.AddWithValue("@k", kind);
            cmd.Parameters.AddWithValue("@p", path);
            cmd.Parameters.AddWithValue("@a", analyzerId ?? "");
            cmd.Parameters.AddWithValue("@j", json);
            cmd.Parameters.AddWithValue("@c", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@e", (object?)error ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            _count = await CountInternalAsync(conn, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task<List<BufferedItem>> PeekAsync(int limit, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, kind, path, analyzer_id, payload_json, created_at, retry_count, last_error FROM outbox ORDER BY id LIMIT @l";
            cmd.Parameters.AddWithValue("@l", limit);
            var list = new List<BufferedItem>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new BufferedItem
                {
                    Id = r.GetInt64(0),
                    Kind = r.GetString(1),
                    Path = r.GetString(2),
                    AnalyzerId = r.GetString(3),
                    Json = r.GetString(4),
                    CreatedAt = DateTime.TryParse(r.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.UtcNow,
                    RetryCount = r.GetInt32(6),
                    LastError = r.IsDBNull(7) ? null : r.GetString(7),
                });
            }
            return list;
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAsync(long id, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM outbox WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
            _count = await CountInternalAsync(conn, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task MarkFailedAsync(long id, string error, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE outbox SET retry_count = retry_count + 1, last_error = @e WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@e", error.Length > 500 ? error[..500] : error);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task<int> ClearAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM outbox";
            var n = await cmd.ExecuteNonQueryAsync(ct);
            _count = 0;
            return n;
        }
        finally { _gate.Release(); }
    }

    private static async Task<int> CountInternalAsync(SqliteConnection conn, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM outbox";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }
}
