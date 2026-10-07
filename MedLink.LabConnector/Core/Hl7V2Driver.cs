// =============================================================================
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
        var lines = rawHl7.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
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

        // Send HL7 ACK back to analyzer: MSH|^~\&|MedLink|LIS|... 

        var ack = $"MSH|^~\\&|MedLinkLIS|MedLink|{AnalyzerCode}|Instrument|{DateTime.UtcNow:yyyyMMddHHmmss}||ACK|{messageControlId}|P|2.3.1\rMSA|AA|{messageControlId}\r";
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
