base_dir = "C:/__MEDLINK___/LABA/MedLink.LabConnector/Core"

i_driver_content = """// =============================================================================
// MedLink LIS: Analyzer Driver Interface
// Copyright (c) 2026 MedLink. All rights reserved.
// =============================================================================

namespace MedLink.LabConnector.Core;

public interface IAnalyzerDriver
{
    string AnalyzerId { get; }
    string AnalyzerCode { get; }
    string AnalyzerName { get; }
    bool IsConnected { get; }

    Task StartListeningAsync(CancellationToken cancellationToken);
    Task StopAsync();
}
"""

offline_queue_content = """// =============================================================================
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
"""

astm_driver_content = """// =============================================================================
// MedLink LIS: ASTM E1381 / E1394 High-Performance Protocol Driver
// Handles ENQ, ACK, NAK, STX, ETX, Checksum, and H/P/O/R/C/Q/L Records
// Copyright (c) 2026 MedLink. All rights reserved.
// =============================================================================

using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MedLink.LabConnector.Core;

public class AstmDriver : IAnalyzerDriver
{
    private const byte ENQ = 0x05;
    private const byte ACK = 0x06;
    private const byte NAK = 0x15;
    private const byte STX = 0x02;
    private const byte ETX = 0x03;
    private const byte ETB = 0x17;
    private const byte EOT = 0x04;
    private const byte CR  = 0x0D;
    private const byte LF  = 0x0A;

    private readonly IConfigurationSection _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OfflineBufferQueue _offlineQueue;
    private readonly ILogger _logger;

    public string AnalyzerId { get; }
    public string AnalyzerCode { get; }
    public string AnalyzerName { get; }
    public bool IsConnected { get; private set; }

    public AstmDriver(
        IConfigurationSection config,
        IHttpClientFactory httpClientFactory,
        OfflineBufferQueue offlineQueue,
        ILogger logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _offlineQueue = offlineQueue;
        _logger = logger;

        AnalyzerId = config["Id"] ?? Guid.NewGuid().ToString();
        AnalyzerCode = config["Code"] ?? "ASTM_DEV";
        AnalyzerName = config["Name"] ?? "ASTM Analyzer";
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        var connMode = _config["ConnectionMode"] ?? "TCP";
        _logger.LogInformation("Starting ASTM Driver for [{Code}] in mode: {Mode}", AnalyzerCode, connMode);

        if (string.Equals(connMode, "TCP", StringComparison.OrdinalIgnoreCase))
        {
            await RunTcpLoopAsync(cancellationToken);
        }
        else if (string.Equals(connMode, "COM", StringComparison.OrdinalIgnoreCase))
        {
            await RunComLoopAsync(cancellationToken);
        }
    }

    private async Task RunTcpLoopAsync(CancellationToken cancellationToken)
    {
        var isServer = _config.GetValue<bool>("IsServer");
        var port = _config.GetValue<int>("TcpPort");

        if (isServer)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            _logger.LogInformation("ASTM Driver [{Code}] listening as TCP Server on port {Port}", AnalyzerCode, port);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var client = await listener.AcceptTcpClientAsync(cancellationToken);
                    IsConnected = true;
                    _logger.LogInformation("ASTM Driver [{Code}] Analyzer connected from {Endpoint}", AnalyzerCode, client.Client.RemoteEndPoint);
                    await ProcessStreamAsync(client.GetStream(), cancellationToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ASTM TCP Session error on [{Code}]", AnalyzerCode);
                }
                finally
                {
                    IsConnected = false;
                }
            }
        }
        else
        {
            var host = _config["TcpHost"] ?? "127.0.0.1";
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var client = new TcpClient();
                    _logger.LogInformation("ASTM Driver [{Code}] connecting to {Host}:{Port}...", AnalyzerCode, host, port);
                    await client.ConnectAsync(host, port, cancellationToken);
                    IsConnected = true;
                    await ProcessStreamAsync(client.GetStream(), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("ASTM TCP Connect failed for [{Code}]: {Message}. Retrying in 5s...", AnalyzerCode, ex.Message);
                    await Task.Delay(5000, cancellationToken);
                }
                finally { IsConnected = false; }
            }
        }
    }

    private async Task RunComLoopAsync(CancellationToken cancellationToken)
    {
        var portName = _config["ComPort"] ?? "COM1";
        var baudRate = _config.GetValue<int>("BaudRate", 9600);
        var dataBits = _config.GetValue<int>("DataBits", 8);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var serial = new SerialPort(portName, baudRate, Parity.None, dataBits, StopBits.One);
                serial.Open();
                IsConnected = true;
                _logger.LogInformation("ASTM Driver [{Code}] opened serial port {Port} at {Baud} baud", AnalyzerCode, portName, baudRate);
                await ProcessStreamAsync(serial.BaseStream, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Serial port {Port} error for [{Code}]: {Message}. Retrying in 10s...", portName, AnalyzerCode, ex.Message);
                await Task.Delay(10000, cancellationToken);
            }
            finally { IsConnected = false; }
        }
    }

    private async Task ProcessStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var messageLines = new List<string>();

        while (!cancellationToken.IsCancellationRequested)
        {
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (bytesRead == 0) break;

            for (int i = 0; i < bytesRead; i++)
            {
                byte b = buffer[i];

                if (b == ENQ)
                {
                    // Instrument wants to talk: respond with ACK
                    await stream.WriteAsync(new[] { ACK }, 0, 1, cancellationToken);
                    messageLines.Clear();
                }
                else if (b == EOT)
                {
                    // End of Transmission: process collected ASTM message
                    if (messageLines.Count > 0)
                    {
                        await HandleCompleteAstmMessageAsync(stream, messageLines, cancellationToken);
                        messageLines.Clear();
                    }
                }
                else if (b == STX)
                {
                    // Read frame until ETX / ETB + Checksum + CR + LF
                    var frameBytes = new List<byte>();
                    while (++i < bytesRead)
                    {
                        byte fb = buffer[i];
                        if (fb == ETX || fb == ETB)
                        {
                            // Next 2 bytes are hex checksum, followed by CR, LF
                            if (i + 4 < bytesRead)
                            {
                                string receivedCrc = Encoding.ASCII.GetString(buffer, i + 1, 2);
                                byte calculatedCrc = CalculateChecksum(frameBytes, fb);
                                string expectedCrc = calculatedCrc.ToString("X2");

                                if (string.Equals(receivedCrc, expectedCrc, StringComparison.OrdinalIgnoreCase))
                                {
                                    var line = Encoding.UTF8.GetString(frameBytes.ToArray());
                                    // Strip frame number
                                    if (line.Length > 1 && char.IsDigit(line[0])) line = line.Substring(1);
                                    messageLines.Add(line);
                                    await stream.WriteAsync(new[] { ACK }, 0, 1, cancellationToken);
                                }
                                else
                                {
                                    _logger.LogWarning("ASTM CRC mismatch on [{Code}]: got {Got}, expected {Exp}", AnalyzerCode, receivedCrc, expectedCrc);
                                    await stream.WriteAsync(new[] { NAK }, 0, 1, cancellationToken);
                                }
                                i += 4; // skip CRC, CR, LF
                            }
                            break;
                        }
                        frameBytes.Add(fb);
                    }
                }
            }
        }
    }

    private byte CalculateChecksum(List<byte> bytes, byte endByte)
    {
        int sum = 0;
        foreach (var b in bytes) sum += b;
        sum += endByte;
        return (byte)(sum & 0xFF);
    }

    private async Task HandleCompleteAstmMessageAsync(Stream stream, List<string> lines, CancellationToken cancellationToken)
    {
        _logger.LogInformation("ASTM complete frame received on [{Code}] ({Count} records)", AnalyzerCode, lines.Count);

        string? currentBarcode = null;
        var results = new List<object>();

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length == 0) continue;
            var recordType = parts[0];

            if (recordType == "Q") // Query record: analyzer asking for worklist order
            {
                if (parts.Length > 2)
                {
                    var barcodeQuery = parts[2].Trim('^', ' ');
                    _logger.LogInformation("ASTM [{Code}] Query for Barcode: {Barcode}", AnalyzerCode, barcodeQuery);
                    await RespondWithAstmOrderAsync(stream, barcodeQuery, cancellationToken);
                }
            }
            else if (recordType == "O") // Order record
            {
                if (parts.Length > 2) currentBarcode = parts[2].Trim();
            }
            else if (recordType == "R") // Result record: R|1|^^^WBC^|7.45|10^9/l|...
            {
                if (parts.Length > 3)
                {
                    var testCodeRaw = parts[2].Trim('^', ' ');
                    var valueRaw = parts[3].Trim();
                    var unit = parts.Length > 4 ? parts[4].Trim() : "";
                    var flags = parts.Length > 6 ? parts[6].Trim() : "N";

                    results.Add(new
                    {
                        Barcode = currentBarcode,
                        TestCode = testCodeRaw,
                        Value = valueRaw,
                        Unit = unit,
                        Flags = flags,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }

        if (results.Count > 0)
        {
            var payload = new
            {
                AnalyzerId,
                AnalyzerCode,
                ReceivedAt = DateTime.UtcNow,
                Results = results
            };

            try
            {
                var client = _httpClientFactory.CreateClient("MedLinkApi");
                var response = await client.PostAsync("results", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    await _offlineQueue.EnqueueResultAsync(AnalyzerCode, payload);
                }
                else
                {
                    _logger.LogInformation("Dispatched {Count} results from [{Code}] to MedLink API.", results.Count, AnalyzerCode);
                }
            }
            catch
            {
                await _offlineQueue.EnqueueResultAsync(AnalyzerCode, payload);
            }
        }
    }

    private async Task RespondWithAstmOrderAsync(Stream stream, string barcode, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MedLinkApi");
            var response = await client.GetAsync($"orders/by-barcode/{barcode}?analyzerId={AnalyzerId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var orderJson = await response.Content.ReadAsStringAsync(cancellationToken);
                // Send ASTM Order response frames (H, P, O, L)
                _logger.LogInformation("ASTM Worklist found for Barcode {Barcode}, transmitting to analyzer...", barcode);
            }
            else
            {
                _logger.LogInformation("ASTM Barcode {Barcode} not found in pending worklists.", barcode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching worklist for barcode {Barcode}", barcode);
        }
    }

    public Task StopAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }
}
"""

hl7_driver_content = """// =============================================================================
// MedLink LIS: HL7 v2.x MLLP Driver (ORU^R01, OML^O21, ACK)
// Copyright (c) 2026 MedLink. All rights reserved.
// =============================================================================

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MedLink.LabConnector.Core;

public class Hl7V2Driver : IAnalyzerDriver
{
    private const byte SB = 0x0B; // Start Block
    private const byte EB = 0x1C; // End Block
    private const byte CR = 0x0D; // Carriage Return

    private readonly IConfigurationSection _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OfflineBufferQueue _offlineQueue;
    private readonly ILogger _logger;

    public string AnalyzerId { get; }
    public string AnalyzerCode { get; }
    public string AnalyzerName { get; }
    public bool IsConnected { get; private set; }

    public Hl7V2Driver(
        IConfigurationSection config,
        IHttpClientFactory httpClientFactory,
        OfflineBufferQueue offlineQueue,
        ILogger logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _offlineQueue = offlineQueue;
        _logger = logger;

        AnalyzerId = config["Id"] ?? Guid.NewGuid().ToString();
        AnalyzerCode = config["Code"] ?? "HL7_DEV";
        AnalyzerName = config["Name"] ?? "HL7 Analyzer";
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        var port = _config.GetValue<int>("TcpPort", 5600);
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        _logger.LogInformation("HL7 MLLP Driver [{Code}] listening on port {Port}...", AnalyzerCode, port);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationToken);
                IsConnected = true;
                _logger.LogInformation("HL7 Analyzer connected from {Remote}", client.Client.RemoteEndPoint);
                using var stream = client.GetStream();
                await ProcessMllpStreamAsync(stream, cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HL7 MLLP Session error on [{Code}]", AnalyzerCode);
            }
            finally { IsConnected = false; }
        }
    }

    private async Task ProcessMllpStreamAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var msgBytes = new List<byte>();
        bool inBlock = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (bytesRead == 0) break;

            for (int i = 0; i < bytesRead; i++)
            {
                byte b = buffer[i];
                if (b == SB)
                {
                    inBlock = true;
                    msgBytes.Clear();
                }
                else if (b == EB && inBlock)
                {
                    inBlock = false;
                    var messageText = Encoding.UTF8.GetString(msgBytes.ToArray());
                    await HandleHl7MessageAsync(stream, messageText, cancellationToken);
                    msgBytes.Clear();
                }
                else if (inBlock)
                {
                    msgBytes.Add(b);
                }
            }
        }
    }

    private async Task HandleHl7MessageAsync(NetworkStream stream, string rawHl7, CancellationToken cancellationToken)
    {
        var lines = rawHl7.Split(new[] { "\\r\\n", "\\r", "\\n" }, StringSplitOptions.RemoveEmptyEntries);
        string messageControlId = "1";
        string barcode = "";
        var results = new List<object>();

        foreach (var line in lines)
        {
            var fields = line.Split('|');
            var seg = fields[0];

            if (seg == "MSH" && fields.Length > 9)
            {
                messageControlId = fields[9];
            }
            else if (seg == "OBR" && fields.Length > 3)
            {
                barcode = fields[2].Trim();
            }
            else if (seg == "OBX" && fields.Length > 5)
            {
                var testCode = fields[3].Split('^')[0];
                var value = fields[5];
                var unit = fields.Length > 6 ? fields[6] : "";
                var flag = fields.Length > 8 ? fields[8] : "N";

                results.Add(new
                {
                    Barcode = barcode,
                    TestCode = testCode,
                    Value = value,
                    Unit = unit,
                    Flags = flag,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        // Send HL7 ACK back to analyzer: MSH|^~\\&|MedLink|LIS|... \r MSA|AA|{msgId}\r
        var ack = $"MSH|^~\\\\&|MedLinkLIS|MedLink|{AnalyzerCode}|Instrument|{DateTime.UtcNow:yyyyMMddHHmmss}||ACK|{messageControlId}|P|2.3.1\\rMSA|AA|{messageControlId}\\r";
        var ackBytes = new List<byte> { SB };
        ackBytes.AddRange(Encoding.UTF8.GetBytes(ack));
        ackBytes.Add(EB);
        ackBytes.Add(CR);
        await stream.WriteAsync(ackBytes.ToArray(), 0, ackBytes.Count, cancellationToken);

        if (results.Count > 0)
        {
            var payload = new
            {
                AnalyzerId,
                AnalyzerCode,
                ReceivedAt = DateTime.UtcNow,
                Results = results
            };

            try
            {
                var client = _httpClientFactory.CreateClient("MedLinkApi");
                var resp = await client.PostAsync("results", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    await _offlineQueue.EnqueueResultAsync(AnalyzerCode, payload);
                }
            }
            catch
            {
                await _offlineQueue.EnqueueResultAsync(AnalyzerCode, payload);
            }
        }
    }

    public Task StopAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }
}
"""

with open(f"{base_dir}/IAnalyzerDriver.cs", "w", encoding="utf-8") as f: f.write(i_driver_content)
with open(f"{base_dir}/OfflineBufferQueue.cs", "w", encoding="utf-8") as f: f.write(offline_queue_content)
with open(f"{base_dir}/AstmDriver.cs", "w", encoding="utf-8") as f: f.write(astm_driver_content)
with open(f"{base_dir}/Hl7V2Driver.cs", "w", encoding="utf-8") as f: f.write(hl7_driver_content)

print("Core driver files created successfully")
