// =============================================================================
// MedLink LIS: Offline Store-and-Forward Buffer Queue (SQLite)
// Copyright (c) 2026 MedLink. All rights reserved.
// =============================================================================

using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MedLink.LabConnector.Core;

public class OfflineBufferQueue
{
    private readonly ILogger<OfflineBufferQueue> _logger;
    private readonly string _connectionString;

    public OfflineBufferQueue(ILogger<OfflineBufferQueue> logger)
    {
        _logger = logger;
        var dbPath = Path.Combine(AppContext.BaseDirectory, "medlink_offline_buffer.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task InitializeAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS offline_results (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                analyzer_code TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                created_at TEXT NOT NULL,
                retry_count INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_offline_retry ON offline_results(retry_count, created_at);
        ";
        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("Offline buffer initialized at {DbPath}", _connectionString);
    }

    public async Task EnqueueResultAsync(string analyzerCode, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO offline_results (analyzer_code, payload_json, created_at) VALUES (@code, @json, @dt)";
        cmd.Parameters.AddWithValue("@code", analyzerCode);
        cmd.Parameters.AddWithValue("@json", json);
        cmd.Parameters.AddWithValue("@dt", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
        _logger.LogWarning("Network down or API unavailable. Buffered result for analyzer {Code} into local storage.", analyzerCode);
    }

    public async Task FlushPendingResultsAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT id, analyzer_code, payload_json, retry_count FROM offline_results ORDER BY id ASC LIMIT 50";
        using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);

        var itemsToRetry = new List<(long id, string code, string json, int retries)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            itemsToRetry.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
        }
        reader.Close();

        if (itemsToRetry.Count == 0) return;

        _logger.LogInformation("Attempting to flush {Count} offline buffered results to MedLink server...", itemsToRetry.Count);

        foreach (var item in itemsToRetry)
        {
            try
            {
                var content = new StringContent(item.json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("results", content, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var delCmd = conn.CreateCommand();
                    delCmd.CommandText = "DELETE FROM offline_results WHERE id = @id";
                    delCmd.Parameters.AddWithValue("@id", item.id);
                    await delCmd.ExecuteNonQueryAsync(cancellationToken);
                    _logger.LogInformation("Successfully synced buffered result #{Id} ({Code}) to MedLink LIS.", item.id, item.code);
                }
                else
                {
                    var updCmd = conn.CreateCommand();
                    updCmd.CommandText = "UPDATE offline_results SET retry_count = retry_count + 1 WHERE id = @id";
                    updCmd.Parameters.AddWithValue("@id", item.id);
                    await updCmd.ExecuteNonQueryAsync(cancellationToken);
                    break; // stop batch on HTTP error
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Connection to MedLink LIS still offline: {Message}", ex.Message);
                break;
            }
        }
    }
}
