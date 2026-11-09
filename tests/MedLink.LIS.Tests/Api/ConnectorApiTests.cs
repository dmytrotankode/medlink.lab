// I-GW-* / C-*: коннектор і журнал обміну (FR-CON, FR-GW) — реєстрація, ключі, heartbeat, команди, прийом результатів, незв'язані, ВКЯ через коннектор
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;
using static MedLink.LIS.Tests.Api.LabFlow;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class ConnectorApiTests
{
    private readonly LisApiFactory _f;
    public ConnectorApiTests(LisApiFactory f) => _f = f;

    private HttpClient Admin => _f.As(DemoDataSeeder.Admin);

    [Fact]
    public async Task Install_key_registers_once_and_only_admin_creates_installations()
    {
        await Problem(await _f.As(DemoDataSeeder.Technician).PostAsJsonAsync("/api/v1/lab/connectors", new { name = "x" }), HttpStatusCode.Forbidden);
        await Problem(await Admin.PostAsJsonAsync("/api/v1/lab/connectors", new { name = " " }), HttpStatusCode.BadRequest);
        var created = await Json(await Admin.PostAsJsonAsync("/api/v1/lab/connectors", new { name = "Реєстрація-" + Guid.NewGuid().ToString("N")[..5] }));
        var installKey = created["installKey"]!.GetValue<string>();
        Assert.StartsWith("MLK-", installKey);
        Assert.Contains(installKey, created["setupCommand"]!.GetValue<string>());
        Assert.Equal("PENDING", created["connector"]!["status"]!.GetValue<string>());

        var anon = _f.CreateClient();
        await Problem(await anon.PostAsJsonAsync("/api/v1/lab/connector/register", new { installKey = "" }), HttpStatusCode.BadRequest);
        await Problem(await anon.PostAsJsonAsync("/api/v1/lab/connector/register", new { installKey = "MLK-NO-SUCH-KEY" }), HttpStatusCode.Unauthorized);
        var reg = await Json(await anon.PostAsJsonAsync("/api/v1/lab/connector/register", new { installKey, hostName = "LAB-PC", osDescription = "Windows 11", version = "4.0.1" }));
        Assert.False(string.IsNullOrEmpty(reg["apiKey"]!.GetValue<string>()));
        // ключ інсталяції одноразовий
        await Problem(await anon.PostAsJsonAsync("/api/v1/lab/connector/register", new { installKey }), HttpStatusCode.Conflict);
        var dto = await Json(await Admin.GetAsync($"/api/v1/lab/connectors/{created["id"]}"));
        Assert.Equal("ACTIVE", dto["status"]!.GetValue<string>());
        Assert.True(dto["installKeyUsed"]!.GetValue<bool>());
        Assert.Equal("LAB-PC", dto["hostName"]!.GetValue<string>());
        Assert.Equal("4.0.1", dto["version"]!.GetValue<string>());
        Assert.True(dto["hasApiKey"]!.GetValue<bool>());
        // у відповіді адміністратору немає хешу ключа
        Assert.Null(dto["apiKeyHash"]);
        Assert.Contains("REGISTER", await Admin.GetStringAsync($"/api/v1/lab/audit?entity=lab_connector_installation&entityId={created["id"]}"));
    }

    [Fact]
    public async Task Connector_endpoints_reject_missing_wrong_rotated_and_disabled_keys()
    {
        var (id, key, installKey) = await NewConnector(_f);
        var anon = _f.CreateClient();
        foreach (var path in new[] { "/api/v1/lab/connector/config", "/api/v1/lab/connector/orders/pending" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await ConnectorClient(_f, "wrong-key").GetAsync(path)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/lab/connector/heartbeat", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/lab/connector/results", new { analyzerId = "x", results = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ConnectorClient(_f, key).GetAsync("/api/v1/lab/connector/config")).StatusCode);

        // перевипуск ключа: старий недійсний, новий працює; лише адмін
        await Problem(await _f.As(DemoDataSeeder.Doctor).PostAsync($"/api/v1/lab/connectors/{id}/rotate-key", null), HttpStatusCode.Forbidden);
        var rotated = await Json(await Admin.PostAsync($"/api/v1/lab/connectors/{id}/rotate-key", null));
        var newKey = rotated["apiKey"]!.GetValue<string>();
        Assert.NotEqual(key, newKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ConnectorClient(_f, key).GetAsync("/api/v1/lab/connector/config")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ConnectorClient(_f, newKey).GetAsync("/api/v1/lab/connector/config")).StatusCode);
        // у переліку ключ не повертається
        Assert.DoesNotContain(newKey, await Admin.GetStringAsync($"/api/v1/lab/connectors/{id}"));

        // вимкнений коннектор не приймається; повторне вмикання видає новий install key й анулює apiKey
        await Json(await Admin.PostAsync($"/api/v1/lab/connectors/{id}/disable", null));
        Assert.Equal(HttpStatusCode.Unauthorized, (await ConnectorClient(_f, newKey).GetAsync("/api/v1/lab/connector/config")).StatusCode);
        var enabled = await Json(await Admin.PostAsync($"/api/v1/lab/connectors/{id}/enable", null));
        Assert.Equal("PENDING", enabled["status"]!.GetValue<string>());
        Assert.NotEqual(installKey, enabled["installKey"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Unauthorized, (await ConnectorClient(_f, newKey).GetAsync("/api/v1/lab/connector/config")).StatusCode);
        await Problem(await anon.PostAsJsonAsync("/api/v1/lab/connector/register", new { installKey }), HttpStatusCode.Unauthorized); // старий install key недійсний
        var again = await Json(await anon.PostAsJsonAsync("/api/v1/lab/connector/register", new { installKey = enabled["installKey"]!.GetValue<string>() }));
        Assert.Equal(HttpStatusCode.OK, (await ConnectorClient(_f, again["apiKey"]!.GetValue<string>()).GetAsync("/api/v1/lab/connector/config")).StatusCode);
        // ENABLE для не вимкненого — 409
        await Problem(await Admin.PostAsync($"/api/v1/lab/connectors/{id}/enable", null), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Config_contains_only_own_active_analyzers_with_parameter_map_and_version_bumps_on_change()
    {
        var code = await NewCustomTest(_f);
        var (id, key, _) = await NewConnector(_f);
        var (otherId, _, _) = await NewConnector(_f);
        var mine = await NewAnalyzer(_f, id, ("AA1", code, 2.0, 1.0));
        await NewAnalyzer(_f, otherId, ("BB1", code, 1.0, 0.0));
        var con = ConnectorClient(_f, key);
        var cfg = await Json(await con.GetAsync("/api/v1/lab/connector/config"));
        Assert.Equal(id, cfg["connectorId"]!.GetValue<string>());
        var analyzers = cfg["analyzers"]!.AsArray();
        Assert.Single(analyzers);
        Assert.Equal(mine, analyzers[0]!["analyzerId"]!.GetValue<string>());
        var map = analyzers[0]!["parameterMap"]![0]!;
        Assert.Equal("AA1", map["analyzerCode"]!.GetValue<string>());
        Assert.Equal(2.0, map["factor"]!.GetValue<double>());
        Assert.Equal(1.0, map["offset"]!.GetValue<double>());
        Assert.Equal(code, map["testCode"]!.GetValue<string>());
        var v1 = cfg["configVersion"]!.GetValue<int>();

        // зміна налаштувань інсталяції збільшує версію конфігурації — її бачить heartbeat
        await Json(await Admin.PutAsJsonAsync($"/api/v1/lab/connectors/{id}", new { name = "Перейменовано", pollIntervalSec = 15, heartbeatIntervalSec = 20 }));
        var hb = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/heartbeat", new { version = "4.0.2", uptimeSec = 10, bufferedCount = 0, analyzers = Array.Empty<object>() }));
        Assert.True(hb["configVersion"]!.GetValue<int>() > v1);
        var cfg2 = await Json(await con.GetAsync("/api/v1/lab/connector/config"));
        Assert.Equal(15, cfg2["pollIntervalSec"]!.GetValue<int>());
        Assert.Equal(20, cfg2["heartbeatIntervalSec"]!.GetValue<int>());
    }

    [Fact]
    public async Task Heartbeat_updates_buffer_uptime_analyzer_state_and_delivers_each_command_once()
    {
        var (id, key, _) = await NewConnector(_f);
        var analyzerId = await NewAnalyzer(_f, id);
        var con = ConnectorClient(_f, key);

        var seen = DateTime.UtcNow.AddMinutes(-1);
        await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/heartbeat", new
        {
            version = "4.1.0", uptimeSec = 3600, bufferedCount = 17,
            analyzers = new[] { new { analyzerId, isConnected = false, lastError = "Тайм-аут TCP 127.0.0.1:5100", lastMessageAt = seen } }
        }));
        var dto = await Json(await Admin.GetAsync($"/api/v1/lab/connectors/{id}"));
        Assert.Equal(17, dto["bufferedCount"]!.GetValue<int>()); // буфер офлайн-результатів видно адміністратору
        Assert.Equal(3600, dto["uptimeSec"]!.GetValue<long>());
        Assert.Equal("4.1.0", dto["version"]!.GetValue<string>());
        Assert.True(dto["isOnline"]!.GetValue<bool>());
        var an = await Json(await Admin.GetAsync($"/api/v1/lab/analyzers/{analyzerId}"));
        Assert.False(an["isOnline"]!.GetValue<bool>());
        Assert.Equal("Тайм-аут TCP 127.0.0.1:5100", an["lastError"]!.GetValue<string>());
        // невідомий аналізатор у heartbeat ігнорується без помилки
        await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/heartbeat", new { analyzers = new[] { new { analyzerId = "ghost", isConnected = true } } }));
        // відновлення зв'язку знімає помилку/офлайн
        await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/heartbeat", new { bufferedCount = 0, analyzers = new[] { new { analyzerId, isConnected = true } } }));
        Assert.True((await Json(await Admin.GetAsync($"/api/v1/lab/analyzers/{analyzerId}")))["isOnline"]!.GetValue<bool>());
        Assert.Equal(0, (await Json(await Admin.GetAsync($"/api/v1/lab/connectors/{id}")))["bufferedCount"]!.GetValue<int>());

        // команди: лише адмін; кожна віддається один раз у порядку створення
        await Problem(await _f.As(DemoDataSeeder.Technician).PostAsJsonAsync($"/api/v1/lab/connectors/{id}/commands", new { type = "RESTART" }), HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Accepted, (await Admin.PostAsJsonAsync($"/api/v1/lab/connectors/{id}/commands", new { type = "RESTART" })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await Admin.PostAsJsonAsync($"/api/v1/lab/connectors/{id}/commands", new { type = "SEND_TEST_MESSAGE", payload = analyzerId })).StatusCode);
        await Problem(await Admin.PostAsJsonAsync("/api/v1/lab/connectors/nope/commands", new { type = "RESTART" }), HttpStatusCode.NotFound);
        var hb = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/heartbeat", new { }));
        var cmds = hb["commands"]!.AsArray();
        Assert.Equal(new[] { "RESTART", "SEND_TEST_MESSAGE" }, cmds.Select(c => c!["type"]!.GetValue<string>()).ToArray());
        Assert.Equal(analyzerId, cmds[1]!["payload"]!.GetValue<string>());
        Assert.Empty((await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/heartbeat", new { })))["commands"]!.AsArray());

        // ручне «офлайн» → наступний heartbeat повертає в ACTIVE
        await Json(await Admin.PostAsync($"/api/v1/lab/connectors/{id}/mark-offline", null));
        Assert.Equal("OFFLINE", (await Json(await Admin.GetAsync($"/api/v1/lab/connectors/{id}")))["status"]!.GetValue<string>());
        await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/heartbeat", new { }));
        Assert.Equal("ACTIVE", (await Json(await Admin.GetAsync($"/api/v1/lab/connectors/{id}")))["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Results_apply_parameter_factor_offset_and_unmatched_reasons_are_recorded()
    {
        var code = await NewCustomTest(_f);
        var (id, key, _) = await NewConnector(_f);
        var analyzerId = await NewAnalyzer(_f, id, ("XQ1", code, 2.0, 1.0));
        var con = ConnectorClient(_f, key);
        var o = await ReceivedOrder(_f, new[] { code });

        var batch = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/results", new
        {
            analyzerId, receivedAt = DateTime.UtcNow, rawMessageId = "MSG-" + Guid.NewGuid().ToString("N")[..6],
            results = new object[]
            {
                new { barcode = o.Barcode, analyzerCode = "XQ1", value = "10,5", flags = "N" },        // 10.5·2+1 = 22 (кома як десятковий роздільник)
                new { barcode = o.Barcode, analyzerCode = "NOPE", value = "1" },                        // код приладу не зіставлено
                new { barcode = "00000000", analyzerCode = "XQ1", value = "1" },                         // невідомий штрихкод
            }
        }));
        Assert.Equal(3, batch["accepted"]!.GetValue<int>());
        Assert.Equal(1, batch["matched"]!.GetValue<int>());
        var reasons = batch["unmatched"]!.AsArray().Select(u => u!["reason"]!.GetValue<string>()).ToList();
        Assert.Contains(reasons, r => r.Contains("не зіставлено"));
        Assert.Contains(reasons, r => r.Contains("Штрихкод") && r.Contains("не знайдено"));

        var row = await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test(code)}"));
        Assert.Equal(22.0, row["numericValue"]!.GetValue<double>());
        Assert.Equal(analyzerId, row["analyzerId"]!.GetValue<string>());
        Assert.Equal("AUTO_VERIFIED", row["status"]!.GetValue<string>());
        // незв'язані потрапили в чергу з причиною
        var unmatched = (await Json(await Admin.GetAsync("/api/v1/lab/results/unmatched"))).AsArray();
        Assert.Contains(unmatched, u => u!["barcode"]!.GetValue<string>() == "00000000" && u["analyzerId"]?.GetValue<string>() == analyzerId);
    }

    [Fact]
    public async Task Redelivery_of_buffered_results_does_not_overwrite_verified_result_or_create_duplicates()
    {
        var code = await NewCustomTest(_f);
        var (id, key, _) = await NewConnector(_f);
        var analyzerId = await NewAnalyzer(_f, id, ("XQ1", code, 1.0, 0.0));
        var con = ConnectorClient(_f, key);
        var o = await ReceivedOrder(_f, new[] { code });
        var payload = new { analyzerId, receivedAt = DateTime.UtcNow, rawMessageId = "BUF-1", results = new[] { new { barcode = o.Barcode, analyzerCode = "XQ1", value = "7", flags = "N" } } };

        var first = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/results", payload));
        Assert.Equal(1, first["matched"]!.GetValue<int>());
        Assert.Equal("AUTO_VERIFIED", (await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test(code)}")))["status"]!.GetValue<string>());

        // той самий пакет після відновлення зв'язку (буфер дослав удруге)
        var second = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/results", payload));
        Assert.Equal(0, second["matched"]!.GetValue<int>());
        Assert.Contains("уже верифіковано", second["unmatched"]![0]!["reason"]!.GetValue<string>());
        var row = await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test(code)}"));
        Assert.Equal("AUTO_VERIFIED", row["status"]!.GetValue<string>());
        Assert.Equal(7.0, row["numericValue"]!.GetValue<double>());
        var hist = (await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test(code)}/history"))).AsArray();
        Assert.Single(hist); // повторна доставка не створила другої версії
        // повторна доставка іншого значення також не змінює верифікований результат
        var changed = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/results", new { analyzerId, receivedAt = DateTime.UtcNow, results = new[] { new { barcode = o.Barcode, analyzerCode = "XQ1", value = "99" } } }));
        Assert.Equal(0, changed["matched"]!.GetValue<int>());
        Assert.Equal(7.0, (await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test(code)}")))["numericValue"]!.GetValue<double>());
    }

    [Fact]
    public async Task Results_for_foreign_or_unknown_analyzer_are_rejected_and_message_log_is_kept()
    {
        var (idA, keyA, _) = await NewConnector(_f);
        var (idB, _, _) = await NewConnector(_f);
        var analyzerB = await NewAnalyzer(_f, idB);
        var conA = ConnectorClient(_f, keyA);
        // аналізатор іншої інсталяції → 401 (коннектор не має права), невідомий → 400
        Assert.Equal(HttpStatusCode.Unauthorized, (await conA.PostAsJsonAsync("/api/v1/lab/connector/results", new { analyzerId = analyzerB, receivedAt = DateTime.UtcNow, results = Array.Empty<object>() })).StatusCode);
        await Problem(await conA.PostAsJsonAsync("/api/v1/lab/connector/results", new { analyzerId = "ghost", receivedAt = DateTime.UtcNow, results = Array.Empty<object>() }), HttpStatusCode.BadRequest);

        // журнал обміну: сирі повідомлення, помилка розбору потрапляє в lastError, фільтр за напрямком
        var analyzerA = await NewAnalyzer(_f, idA);
        Assert.Equal(HttpStatusCode.Accepted, (await conA.PostAsJsonAsync("/api/v1/lab/connector/messages", new { analyzerId = analyzerA, direction = "IN", protocol = "ASTM", rawText = "H|\\^&|||Sysmex", parsedOk = true, resultsCount = 3, receivedAt = DateTime.UtcNow })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await conA.PostAsJsonAsync("/api/v1/lab/connector/messages", new { analyzerId = analyzerA, direction = "OUT", protocol = "ASTM", rawText = "O|1|", parsedOk = true, receivedAt = DateTime.UtcNow })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await conA.PostAsJsonAsync("/api/v1/lab/connector/messages", new { analyzerId = analyzerA, direction = "IN", protocol = "ASTM", rawText = "garbage", parsedOk = false, error = "Невірна контрольна сума" })).StatusCode);
        var inbound = await Json(await Admin.GetAsync($"/api/v1/lab/analyzers/{analyzerA}/messages?direction=in"));
        var items = inbound["items"]!.AsArray();
        Assert.Equal(2, items.Count);
        Assert.All(items, m => Assert.Equal("IN", m!["direction"]!.GetValue<string>()));
        Assert.Contains(items, m => m!["rawText"]!.GetValue<string>() == "garbage" && !m["parsedOk"]!.GetValue<bool>() && m["error"]!.GetValue<string>() == "Невірна контрольна сума");
        Assert.Equal("Невірна контрольна сума", (await Json(await Admin.GetAsync($"/api/v1/lab/analyzers/{analyzerA}")))["lastError"]!.GetValue<string>());
        Assert.Equal(3, (await Json(await Admin.GetAsync($"/api/v1/lab/analyzers/{analyzerA}/messages")))["items"]!.AsArray().Count);
        await Problem(await conA.PostAsJsonAsync("/api/v1/lab/connector/messages", new { analyzerId = "ghost", rawText = "x" }), HttpStatusCode.BadRequest);

        // логи інсталяції
        Assert.Equal(HttpStatusCode.Accepted, (await conA.PostAsJsonAsync("/api/v1/lab/connector/logs", new { entries = new[] { new { at = DateTime.UtcNow, level = "Error", message = "COM3 недоступний", analyzerId = analyzerA } } })).StatusCode);
        var logs = await Json(await Admin.GetAsync($"/api/v1/lab/connectors/{idA}/logs"));
        Assert.Contains(logs["items"]!.AsArray(), l => l!["message"]!.GetValue<string>() == "COM3 недоступний" && l["level"]!.GetValue<string>() == "Error");
    }

    [Fact]
    public async Task Order_lookup_by_barcode_and_pending_queue_follow_analyzer_parameter_map()
    {
        var mapped = await NewCustomTest(_f);
        var unmapped = await NewCustomTest(_f);
        var (id, key, _) = await NewConnector(_f);
        var analyzerId = await NewAnalyzer(_f, id, ("XQ1", mapped, 1.0, 0.0));
        var con = ConnectorClient(_f, key);
        var o = await ReceivedOrder(_f, new[] { mapped, unmapped });

        var dto = await Json(await con.GetAsync($"/api/v1/lab/connector/orders/by-barcode/{o.Barcodes[0]}?analyzerId={analyzerId}"));
        Assert.Equal(o.Barcodes[0], dto["barcode"]!.GetValue<string>());
        Assert.Equal("R", dto["priority"]!.GetValue<string>());
        var tests = dto["tests"]!.AsArray();
        if (o.TestIds.Count == 2 && o.Barcodes.Count == 1)
        {
            // на прилад іде лише те, що є в його мапі параметрів, з кодом приладу
            Assert.Single(tests);
            Assert.Equal("XQ1", tests[0]!["analyzerCode"]!.GetValue<string>());
            Assert.Equal(mapped, tests[0]!["testCode"]!.GetValue<string>());
        }
        await Problem(await con.GetAsync("/api/v1/lab/connector/orders/by-barcode/00000000"), HttpStatusCode.NotFound);

        var pending = (await Json(await con.GetAsync($"/api/v1/lab/connector/orders/pending?analyzerId={analyzerId}"))).AsArray();
        Assert.Contains(pending, p => p!["barcode"]!.GetValue<string>() == o.Barcodes[0]);
        // скасоване замовлення зникає з черги й не віддається за штрихкодом
        await Json(await Admin.PostAsJsonAsync($"/api/v1/lab/orders/{o.OrderId}/cancel", new { reason = "тест" }));
        Assert.DoesNotContain((await Json(await con.GetAsync($"/api/v1/lab/connector/orders/pending?analyzerId={analyzerId}"))).AsArray(), p => p!["barcode"]!.GetValue<string>() == o.Barcodes[0]);
        await Problem(await con.GetAsync($"/api/v1/lab/connector/orders/by-barcode/{o.Barcodes[0]}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Qc_results_through_connector_are_evaluated_and_lock_the_analyzer()
    {
        var code = await NewCustomTest(_f);
        var (id, key, _) = await NewConnector(_f);
        var analyzerId = await NewAnalyzer(_f, id, ("XQ1", code, 1.0, 0.0));
        var lot = "LOT" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        await Json(await Admin.PostAsJsonAsync("/api/v1/lab/qc/materials", new
        {
            analyzerId, name = "Контроль тест", level = "LEVEL_1_NORMAL", lotNumber = lot, expiryDate = DateTime.UtcNow.AddMonths(6),
            targets = new[] { new { testCode = code, targetMean = 10.0, targetSd = 0.5 } }
        }));
        var con = ConnectorClient(_f, key);
        // в межах — ВКЯ пройдено
        var ok = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/results", new { analyzerId, receivedAt = DateTime.UtcNow, isQc = true, results = new[] { new { barcode = lot, analyzerCode = "XQ1", value = "10.1" } } }));
        Assert.Equal(1, ok["matched"]!.GetValue<int>());
        Assert.Empty((await Json(await Admin.GetAsync($"/api/v1/lab/qc/lockouts?active=true&analyzerId={analyzerId}"))).AsArray());
        // невідомий лот → незв'язаний, нічого не створено
        var unknownLot = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/results", new { analyzerId, receivedAt = DateTime.UtcNow, isQc = true, results = new[] { new { barcode = "NOLOT", analyzerCode = "XQ1", value = "10" } } }));
        Assert.Equal(0, unknownLot["matched"]!.GetValue<int>());
        Assert.Contains("не знайдено", unknownLot["unmatched"]![0]!["reason"]!.GetValue<string>());
        // 1_3s (z = +4) → блокування аналізатора; результати пацієнтів по цьому показнику більше не автоверифікуються
        var bad = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/results", new { analyzerId, receivedAt = DateTime.UtcNow, isQc = true, results = new[] { new { barcode = "x", qcLotNumber = lot, analyzerCode = "XQ1", value = "12.0" } } }));
        Assert.Equal(1, bad["matched"]!.GetValue<int>());
        Assert.Single((await Json(await Admin.GetAsync($"/api/v1/lab/qc/lockouts?active=true&analyzerId={analyzerId}"))).AsArray());
        var o = await ReceivedOrder(_f, new[] { code });
        var sent = await Json(await con.PostAsJsonAsync("/api/v1/lab/connector/results", new { analyzerId, receivedAt = DateTime.UtcNow, results = new[] { new { barcode = o.Barcode, analyzerCode = "XQ1", value = "10", flags = "N" } } }));
        Assert.Equal(1, sent["matched"]!.GetValue<int>());
        var row = await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test(code)}"));
        Assert.Equal("RESULTED", row["status"]!.GetValue<string>());
        Assert.Contains("заблоковано контролем якості", row["autoVerifyBlockReason"]!.GetValue<string>());
    }
}
