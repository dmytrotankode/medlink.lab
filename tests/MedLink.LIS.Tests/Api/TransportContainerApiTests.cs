// Транспортні контейнери (FR-LOG-101…): укладка → запечатування → відправка → прийом з розбіжностями → повернення тари; негативні сценарії; ролі
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class TransportContainerApiTests
{
    private const string Patient = "0b000000-0000-0000-0000-000000000003";
    private const string Base = "/api/v1/lab/logistics";
    private readonly LisApiFactory _f;
    public TransportContainerApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static async Task AssertProblem(HttpResponseMessage r, HttpStatusCode status, string? contains = null)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"очікувалось {(int)status}, отримано {(int)r.StatusCode}: {text}");
        Assert.Contains("\"title\"", text);
        if (contains != null) Assert.Contains(contains, JsonNode.Parse(text)!["detail"]!.GetValue<string>()); // JSON екранує кирилицю, тому порівнюємо розібране поле
    }

    private static string[] Strings(JsonNode? arr) => arr!.AsArray().Select(x => x!.GetValue<string>()).ToArray();

    /// <summary>Нове замовлення у відділенні + забір його першої пробірки → штрихкод пробірки у статусі COLLECTED.</summary>
    private async Task<string> CollectedTubeAsync(string departmentId = DemoDataSeeder.CollectionPoint)
    {
        var order = await Json(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync("/api/v1/lab/orders",
            new { patientId = Patient, doctorId = DemoDataSeeder.Doctor, departmentId, profileIds = new[] { "PROF_BIOCHEM_BASE" } }));
        var barcode = order["samples"]![0]!["barcode"]!.GetValue<string>();
        await Json(await _f.As(DemoDataSeeder.Nurse).PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/collect",
            new { checklist = new { idVerified = true, fasting = true, orderOfDraw = true, mixing = true }, volumeMl = 4.0 }));
        return barcode;
    }

    private async Task<string> TypeIdAsync(string code)
    {
        var types = await Json(await _f.CreateClient().GetAsync($"{Base}/container-types"));
        return types.AsArray().Single(t => t!["code"]!.GetValue<string>() == code)!["id"]!.GetValue<string>();
    }

    private async Task<JsonNode> NewContainerAsync(string typeCode = "TB-24", string origin = DemoDataSeeder.CollectionPoint, string destination = DemoDataSeeder.Dept)
    {
        var typeId = await TypeIdAsync(typeCode);
        return await Json(await _f.As(DemoDataSeeder.Courier).PostAsJsonAsync($"{Base}/containers", new { typeId, originDepartmentId = origin, destinationDepartmentId = destination }));
    }

    private async Task<string> SampleStatusAsync(string barcode) =>
        (await Json(await _f.As(DemoDataSeeder.Admin).GetAsync($"/api/v1/lab/samples/{barcode}")))["status"]!.GetValue<string>();

    // ------------------------------------------------------------------ S-130

    [Fact]
    public async Task S130_full_cycle_pack_seal_dispatch_receive_with_discrepancies_return()
    {
        var courier = _f.As(DemoDataSeeder.Courier);
        var nurse = _f.As(DemoDataSeeder.Nurse);
        var tech = _f.As(DemoDataSeeder.Technician);
        var admin = _f.As(DemoDataSeeder.Admin);
        var a = await CollectedTubeAsync(); var b = await CollectedTubeAsync(); var c = await CollectedTubeAsync();

        var created = await NewContainerAsync();
        Assert.Matches(@"^TC\d{8}$", created["barcode"]!.GetValue<string>());
        Assert.Equal("FREE", created["status"]!.GetValue<string>());
        Assert.Equal(24, created["capacity"]!.GetValue<int>());
        var id = created["id"]!.GetValue<string>();

        // Укладка: перше вільне місце, потім явне
        var p1 = await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/pack", new { barcode = a }));
        Assert.Equal("PACKING", p1["status"]!.GetValue<string>());
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/pack", new { barcode = b }));
        var p3 = await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/pack", new { barcode = c, position = 10 }));
        Assert.Equal(3, p3["itemsCount"]!.GetValue<int>());
        Assert.Equal(21, p3["freeSlots"]!.GetValue<int>());
        Assert.Equal(new[] { 1, 2, 10 }, p3["items"]!.AsArray().Select(i => i!["position"]!.GetValue<int>()).ToArray());

        // Зняти й покласти знову — місце звільняється
        var unpacked = await Json(await nurse.DeleteAsync($"{Base}/containers/{id}/items/{b}"));
        Assert.Equal(2, unpacked["itemsCount"]!.GetValue<int>());
        var repacked = await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/pack", new { barcode = b }));
        Assert.Equal(2, repacked["items"]!.AsArray().Single(i => i!["barcode"]!.GetValue<string>() == b)!["position"]!.GetValue<int>());

        // Запечатування: температура + маніфест (чернетка), проби ще COLLECTED
        var sealedC = await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/seal", new { temperatureSeal = 4.5 }));
        Assert.Equal("SEALED", sealedC["status"]!.GetValue<string>());
        Assert.Equal(4.5, sealedC["temperatureSeal"]!.GetValue<double>());
        Assert.False(sealedC["isColdChainViolated"]!.GetValue<bool>());
        var manifestId = sealedC["manifestId"]!.GetValue<string>();
        Assert.Matches(@"^MAN-", sealedC["manifestNumber"]!.GetValue<string>());
        var manifest = await Json(await admin.GetAsync($"{Base}/manifests/{manifestId}"));
        Assert.Equal("CREATED", manifest["status"]!.GetValue<string>());
        Assert.Equal(3, manifest["items"]!.AsArray().Count);
        Assert.Equal("COLLECTED", await SampleStatusAsync(a));
        // Запечатаний контейнер не змінюється
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/pack", new { barcode = await CollectedTubeAsync() }), HttpStatusCode.Conflict);
        await AssertProblem(await nurse.DeleteAsync($"{Base}/containers/{id}/items/{a}"), HttpStatusCode.Conflict);

        // Відправка: маніфест DISPATCHED, проби IN_TRANSIT
        var sent = await Json(await courier.PostAsJsonAsync($"{Base}/containers/{id}/dispatch", new { courierName = "Ткаченко А.М.", courierPhone = "+380671000005" }));
        Assert.Equal("IN_TRANSIT", sent["status"]!.GetValue<string>());
        Assert.Equal("DISPATCHED", (await Json(await admin.GetAsync($"{Base}/manifests/{manifestId}")))["status"]!.GetValue<string>());
        Assert.Equal("IN_TRANSIT", await SampleStatusAsync(a));

        // Прийом контейнера: скан контейнера → очікуваний склад
        var lookup = await Json(await tech.GetAsync($"{Base}/containers/by-barcode/{created["barcode"]!.GetValue<string>()}"));
        Assert.Equal(id, lookup["id"]!.GetValue<string>());
        Assert.Equal(3, lookup["items"]!.AsArray().Count);

        // a — прийнято, b — пошкоджено, c — не відскановано (відсутня), 99999999 — зайва
        var received = await Json(await tech.PostAsJsonAsync($"{Base}/containers/{id}/receive",
            new { temperatureReceipt = 5.0, scannedBarcodes = new[] { a, b, "99999999" }, damagedBarcodes = new[] { b }, notes = "Пакет мокрий" }));
        Assert.Equal("RECEIVED", received["status"]!.GetValue<string>());
        Assert.True(received["hasDiscrepancies"]!.GetValue<bool>());
        Assert.Equal(new[] { c }, Strings(received["discrepancies"]!["missing"]));
        Assert.Equal(new[] { b }, Strings(received["discrepancies"]!["damaged"]));
        Assert.Equal(new[] { "99999999" }, Strings(received["discrepancies"]!["extra"]));
        Assert.Equal(1, received["missingCount"]!.GetValue<int>());
        Assert.False(received["isColdChainViolated"]!.GetValue<bool>());
        string ItemStatus(string bc) => received["items"]!.AsArray().Single(i => i!["barcode"]!.GetValue<string>() == bc)!["status"]!.GetValue<string>();
        Assert.Equal("RECEIVED", ItemStatus(a)); Assert.Equal("DAMAGED", ItemStatus(b)); Assert.Equal("MISSING", ItemStatus(c));
        Assert.Equal("RECEIVED", await SampleStatusAsync(a));
        Assert.Equal("REJECTED", await SampleStatusAsync(b));
        Assert.Equal("IN_TRANSIT", await SampleStatusAsync(c));
        var mf = await Json(await admin.GetAsync($"{Base}/manifests/{manifestId}"));
        Assert.Equal("RECEIVED", mf["status"]!.GetValue<string>());
        Assert.Equal("MISSING", mf["items"]!.AsArray().Single(i => i!["barcode"]!.GetValue<string>() == c)!["status"]!.GetValue<string>());

        // Повернення багаторазової тари
        var returned = await Json(await tech.PostAsJsonAsync($"{Base}/containers/{id}/return", new { notes = "Здано на мийку" }));
        Assert.Equal("RETURNED", returned["status"]!.GetValue<string>());
        Assert.NotNull(returned["returnedAt"]);

        // Історія подій
        var events = await Json(await admin.GetAsync($"{Base}/containers/{id}/events"));
        var types = events.AsArray().Select(e => e!["eventType"]!.GetValue<string>()).ToList();
        Assert.Equal("CREATED", types.First());
        Assert.Equal("RETURNED", types.Last());
        foreach (var t in new[] { "PACKED", "UNPACKED", "SEALED", "DISPATCHED", "RECEIVED" }) Assert.Contains(t, types);
        Assert.Equal(4.5, events.AsArray().Single(e => e!["eventType"]!.GetValue<string>() == "SEALED")!["temperature"]!.GetValue<double>());
    }

    // ------------------------------------------------------------------ S-131

    [Fact]
    public async Task S131_pack_rejects_duplicate_other_container_other_department_and_overflow()
    {
        var nurse = _f.As(DemoDataSeeder.Nurse);
        var admin = _f.As(DemoDataSeeder.Admin);
        var x = await CollectedTubeAsync();
        var container = await NewContainerAsync();
        var id = container["id"]!.GetValue<string>();
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/pack", new { barcode = x }));

        // Дубль у тому самому контейнері та проба в іншому контейнері
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/pack", new { barcode = x }), HttpStatusCode.Conflict, "уже лежить");
        var second = await NewContainerAsync();
        var secondId = second["id"]!.GetValue<string>();
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{secondId}/pack", new { barcode = x }), HttpStatusCode.Conflict, container["barcode"]!.GetValue<string>());

        // Проба з іншого пункту відправлення
        var foreign = await CollectedTubeAsync(DemoDataSeeder.Dept);
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{secondId}/pack", new { barcode = foreign }), HttpStatusCode.Conflict, "іншому пункту");

        // Проба не зібрана (замовлення без забору) та невідомий штрихкод
        var fresh = await Json(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, departmentId = DemoDataSeeder.CollectionPoint, profileIds = new[] { "PROF_BIOCHEM_BASE" } }));
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{secondId}/pack", new { barcode = fresh["samples"]![0]!["barcode"]!.GetValue<string>() }), HttpStatusCode.Conflict, "ще не забрана");
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{secondId}/pack", new { barcode = "00000000" }), HttpStatusCode.NotFound);
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{secondId}/pack", new { barcode = "" }), HttpStatusCode.BadRequest);

        // Переповнення: тип на 2 місця, явні позиції
        var tiny = await Json(await admin.PostAsJsonAsync($"{Base}/container-types", new { code = "T2-S131", name = "Міні на 2 місця", capacity = 2, tempMinC = 2.0, tempMaxC = 8.0, isReusable = true }));
        var tinyContainer = await Json(await _f.As(DemoDataSeeder.Courier).PostAsJsonAsync($"{Base}/containers", new { typeId = tiny["id"]!.GetValue<string>(), originDepartmentId = DemoDataSeeder.CollectionPoint, destinationDepartmentId = DemoDataSeeder.Dept }));
        var tinyId = tinyContainer["id"]!.GetValue<string>();
        var t1 = await CollectedTubeAsync(); var t2 = await CollectedTubeAsync(); var t3 = await CollectedTubeAsync();
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{tinyId}/pack", new { barcode = t1, position = 3 }), HttpStatusCode.BadRequest);
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{tinyId}/pack", new { barcode = t1, position = 2 }));
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{tinyId}/pack", new { barcode = t2, position = 2 }), HttpStatusCode.Conflict, "зайняте");
        var full = await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{tinyId}/pack", new { barcode = t2 }));
        Assert.Equal(1, full["items"]!.AsArray().Single(i => i!["barcode"]!.GetValue<string>() == t2)!["position"]!.GetValue<int>());
        Assert.Equal(0, full["freeSlots"]!.GetValue<int>());
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{tinyId}/pack", new { barcode = t3 }), HttpStatusCode.Conflict, "заповнений");

        // Порожній контейнер можна видалити, непорожній — ні
        await AssertProblem(await nurse.DeleteAsync($"{Base}/containers/{tinyId}"), HttpStatusCode.Conflict);
        var empty = await NewContainerAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await nurse.DeleteAsync($"{Base}/containers/{empty["id"]!.GetValue<string>()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await nurse.GetAsync($"{Base}/containers/{empty["id"]!.GetValue<string>()}")).StatusCode);
    }

    // ------------------------------------------------------------------ S-132

    [Fact]
    public async Task S132_receive_and_dispatch_require_sealed_container()
    {
        var nurse = _f.As(DemoDataSeeder.Nurse);
        var tech = _f.As(DemoDataSeeder.Technician);
        var container = await NewContainerAsync();
        var id = container["id"]!.GetValue<string>();

        // Порожній і не запечатаний: ні відправити, ні запечатати, ні прийняти
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/seal", new { temperatureSeal = 5.0 }), HttpStatusCode.Conflict);
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/dispatch", new { }), HttpStatusCode.Conflict);
        await AssertProblem(await tech.PostAsJsonAsync($"{Base}/containers/{id}/receive", new { temperatureReceipt = 5.0, scannedBarcodes = Array.Empty<string>() }), HttpStatusCode.Conflict);

        var x = await CollectedTubeAsync();
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/pack", new { barcode = x }));
        // Укладка триває (PACKING): прийом і відправка неможливі, запечатування без температури — 400
        await AssertProblem(await tech.PostAsJsonAsync($"{Base}/containers/{id}/receive", new { temperatureReceipt = 5.0, scannedBarcodes = new[] { x } }), HttpStatusCode.Conflict);
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/dispatch", new { }), HttpStatusCode.Conflict);
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/seal", new { }), HttpStatusCode.BadRequest);

        // Запечатаний, але не відправлений: прийом ще неможливий; повернення тари до прийому неможливе
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/seal", new { temperatureSeal = 5.0 }));
        await AssertProblem(await tech.PostAsJsonAsync($"{Base}/containers/{id}/receive", new { temperatureReceipt = 5.0, scannedBarcodes = new[] { x } }), HttpStatusCode.Conflict);
        await AssertProblem(await tech.PostAsJsonAsync($"{Base}/containers/{id}/return", new { }), HttpStatusCode.Conflict);

        // У дорозі: прийом без температури — 400, після прийому повторний — 409
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/dispatch", new { }));
        await AssertProblem(await tech.PostAsJsonAsync($"{Base}/containers/{id}/receive", new { scannedBarcodes = new[] { x } }), HttpStatusCode.BadRequest);
        var ok = await Json(await tech.PostAsJsonAsync($"{Base}/containers/{id}/receive", new { temperatureReceipt = 5.0, scannedBarcodes = new[] { x } }));
        Assert.False(ok["hasDiscrepancies"]!.GetValue<bool>());
        Assert.Equal("RECEIVED", await SampleStatusAsync(x));
        await AssertProblem(await tech.PostAsJsonAsync($"{Base}/containers/{id}/receive", new { temperatureReceipt = 5.0, scannedBarcodes = new[] { x } }), HttpStatusCode.Conflict);
    }

    // ------------------------------------------------------------------ S-133

    [Fact]
    public async Task S133_cold_chain_violation_by_seal_receipt_and_type_regime()
    {
        var nurse = _f.As(DemoDataSeeder.Nurse);
        var tech = _f.As(DemoDataSeeder.Technician);

        // Запечатування поза +2…+8 — порушення фіксується одразу і зберігається після прийому
        var hot = await NewContainerAsync();
        var hotId = hot["id"]!.GetValue<string>();
        var h = await CollectedTubeAsync();
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{hotId}/pack", new { barcode = h }));
        var sealedHot = await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{hotId}/seal", new { temperatureSeal = 12.0 }));
        Assert.True(sealedHot["isColdChainViolated"]!.GetValue<bool>());
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{hotId}/dispatch", new { }));
        var recvHot = await Json(await tech.PostAsJsonAsync($"{Base}/containers/{hotId}/receive", new { temperatureReceipt = 5.0, scannedBarcodes = new[] { h } }));
        Assert.True(recvHot["isColdChainViolated"]!.GetValue<bool>());
        Assert.Equal("RECEIVED", await SampleStatusAsync(h)); // порушення не блокує прийом (FR-LOG-002)
        Assert.Contains("холодового ланцюга", recvHot["notes"]!.GetValue<string>());

        // Нормальне запечатування, порушення при прийомі
        var warm = await NewContainerAsync();
        var warmId = warm["id"]!.GetValue<string>();
        var w = await CollectedTubeAsync();
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{warmId}/pack", new { barcode = w }));
        var sealedWarm = await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{warmId}/seal", new { temperatureSeal = 5.0 }));
        Assert.False(sealedWarm["isColdChainViolated"]!.GetValue<bool>());
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{warmId}/dispatch", new { }));
        var recvWarm = await Json(await tech.PostAsJsonAsync($"{Base}/containers/{warmId}/receive", new { temperatureReceipt = 11.0, scannedBarcodes = new[] { w } }));
        Assert.True(recvWarm["isColdChainViolated"]!.GetValue<bool>());

        // Власний режим типу тари (15…25 °C) замість коридору +2…+8
        var typeCode = "AMB-S133";
        var amb = await Json(await _f.As(DemoDataSeeder.Admin).PostAsJsonAsync($"{Base}/container-types", new { code = typeCode, name = "Кімнатна температура", capacity = 6, tempMinC = 15.0, tempMaxC = 25.0, isReusable = false }));
        var ambC = await Json(await _f.As(DemoDataSeeder.Courier).PostAsJsonAsync($"{Base}/containers", new { typeId = amb["id"]!.GetValue<string>(), originDepartmentId = DemoDataSeeder.CollectionPoint, destinationDepartmentId = DemoDataSeeder.Dept }));
        var ambId = ambC["id"]!.GetValue<string>();
        var a = await CollectedTubeAsync();
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{ambId}/pack", new { barcode = a }));
        var sealedAmb = await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{ambId}/seal", new { temperatureSeal = 20.0 }));
        Assert.False(sealedAmb["isColdChainViolated"]!.GetValue<bool>());
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{ambId}/dispatch", new { }));
        var recvAmb = await Json(await tech.PostAsJsonAsync($"{Base}/containers/{ambId}/receive", new { temperatureReceipt = 5.0, scannedBarcodes = new[] { a } }));
        Assert.True(recvAmb["isColdChainViolated"]!.GetValue<bool>()); // 5 °C поза режимом 15…25
        // Одноразова тара не повертається
        await AssertProblem(await tech.PostAsJsonAsync($"{Base}/containers/{ambId}/return", new { }), HttpStatusCode.Conflict, "одноразовий");
    }

    // ------------------------------------------------------------------ S-134

    [Fact]
    public async Task S134_roles_for_types_packing_and_receiving()
    {
        var doctor = _f.As(DemoDataSeeder.Doctor);
        var nurse = _f.As(DemoDataSeeder.Nurse);
        var registrar = _f.As(DemoDataSeeder.Registrar);
        var courier = _f.As(DemoDataSeeder.Courier);
        var admin = _f.As(DemoDataSeeder.Admin);
        var typeId = await TypeIdAsync("TB-24");
        var body = new { typeId, originDepartmentId = DemoDataSeeder.CollectionPoint, destinationDepartmentId = DemoDataSeeder.Dept };

        // Довідник типів: змінює лише адміністратор
        var typeBody = new { code = "ROLE-S134", name = "Тест ролей", capacity = 4, isReusable = true };
        await AssertProblem(await _f.As(DemoDataSeeder.Technician).PostAsJsonAsync($"{Base}/container-types", typeBody), HttpStatusCode.Forbidden);
        await AssertProblem(await courier.PostAsJsonAsync($"{Base}/container-types", typeBody), HttpStatusCode.Forbidden);
        var createdType = await Json(await admin.PostAsJsonAsync($"{Base}/container-types", typeBody));
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/container-types", typeBody), HttpStatusCode.Conflict);
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/container-types", new { code = "BAD-S134", name = "Без місць", capacity = 0 }), HttpStatusCode.BadRequest);
        await AssertProblem(await admin.PostAsJsonAsync($"{Base}/container-types", new { code = "BAD2-S134", name = "Режим", capacity = 3, tempMinC = 9.0, tempMaxC = 3.0 }), HttpStatusCode.BadRequest);
        var edited = await Json(await admin.PutAsJsonAsync($"{Base}/container-types/{createdType["id"]!.GetValue<string>()}", new { name = "Тест ролей (змінено)", capacity = 8, isActive = false }));
        Assert.Equal(8, edited["capacity"]!.GetValue<int>());
        string[] TypeCodes(JsonNode list) => list.AsArray().Select(t => t!["code"]!.GetValue<string>()).ToArray();
        Assert.DoesNotContain("ROLE-S134", TypeCodes(await Json(await doctor.GetAsync($"{Base}/container-types")))); // вимкнений тип не пропонується
        Assert.Contains("ROLE-S134", TypeCodes(await Json(await doctor.GetAsync($"{Base}/container-types?includeInactive=true"))));
        await AssertProblem(await courier.PostAsJsonAsync($"{Base}/containers", new { typeId = createdType["id"]!.GetValue<string>(), originDepartmentId = DemoDataSeeder.CollectionPoint, destinationDepartmentId = DemoDataSeeder.Dept }), HttpStatusCode.BadRequest);

        // Лікар не працює з контейнерами; пункт відправлення має відрізнятися від призначення
        await AssertProblem(await doctor.PostAsJsonAsync($"{Base}/containers", body), HttpStatusCode.Forbidden);
        await AssertProblem(await courier.PostAsJsonAsync($"{Base}/containers", new { typeId, originDepartmentId = DemoDataSeeder.Dept, destinationDepartmentId = DemoDataSeeder.Dept }), HttpStatusCode.BadRequest);
        var container = await Json(await registrar.PostAsJsonAsync($"{Base}/containers", body)); // реєстратор може
        var id = container["id"]!.GetValue<string>();
        var x = await CollectedTubeAsync();
        await AssertProblem(await doctor.PostAsJsonAsync($"{Base}/containers/{id}/pack", new { barcode = x }), HttpStatusCode.Forbidden);
        await Json(await registrar.PostAsJsonAsync($"{Base}/containers/{id}/pack", new { barcode = x }));
        await Json(await registrar.PostAsJsonAsync($"{Base}/containers/{id}/seal", new { temperatureSeal = 6.0 }));
        await AssertProblem(await doctor.PostAsJsonAsync($"{Base}/containers/{id}/dispatch", new { }), HttpStatusCode.Forbidden);
        await Json(await courier.PostAsJsonAsync($"{Base}/containers/{id}/dispatch", new { courierName = "Ткаченко А.М." }));

        // Прийом: не медсестра пункту забору й не лікар; кур'єр, лаборант, реєстратор — можуть
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/receive", new { temperatureReceipt = 6.0, scannedBarcodes = new[] { x } }), HttpStatusCode.Forbidden);
        await AssertProblem(await doctor.PostAsJsonAsync($"{Base}/containers/{id}/receive", new { temperatureReceipt = 6.0, scannedBarcodes = new[] { x } }), HttpStatusCode.Forbidden);
        await AssertProblem(await doctor.PostAsJsonAsync($"{Base}/containers/{id}/lost", new { reason = "Не знайдено" }), HttpStatusCode.Forbidden);
        var done = await Json(await registrar.PostAsJsonAsync($"{Base}/containers/{id}/receive", new { temperatureReceipt = 6.0, scannedBarcodes = new[] { x } }));
        Assert.Equal("RECEIVED", done["status"]!.GetValue<string>());
        Assert.Contains(done["allowedActions"]!.AsArray(), a => a!["action"]!.GetValue<string>() == "RETURN");
        await AssertProblem(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/return", new { }), HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ S-135

    [Fact]
    public async Task S135_journal_filters_search_by_tube_barcode_and_lost_container()
    {
        var nurse = _f.As(DemoDataSeeder.Nurse);
        var tech = _f.As(DemoDataSeeder.Technician);
        var container = await NewContainerAsync("BAG-12");
        var id = container["id"]!.GetValue<string>();
        var barcode = container["barcode"]!.GetValue<string>();
        var x = await CollectedTubeAsync();
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/pack", new { barcode = x }));

        // Пошук за штрихкодом контейнера та за штрихкодом пробірки всередині; фільтр статусу
        var byContainer = await Json(await tech.GetAsync($"{Base}/containers?search={barcode}"));
        Assert.Single(byContainer.AsArray());
        var byTube = await Json(await tech.GetAsync($"{Base}/containers?search={x}"));
        Assert.Equal(id, byTube[0]!["id"]!.GetValue<string>());
        Assert.Contains(id, (await Json(await tech.GetAsync($"{Base}/containers?status=PACKING"))).AsArray().Select(c => c!["id"]!.GetValue<string>()));
        Assert.DoesNotContain(id, (await Json(await tech.GetAsync($"{Base}/containers?status=SEALED"))).AsArray().Select(c => c!["id"]!.GetValue<string>()));
        Assert.Empty((await Json(await tech.GetAsync($"{Base}/containers?search=TC00000000x"))).AsArray());
        await AssertProblem(await tech.GetAsync($"{Base}/containers/by-barcode/TC99999999"), HttpStatusCode.NotFound);
        await AssertProblem(await tech.GetAsync($"{Base}/containers/00000000-0000-0000-0000-00000000dead/events"), HttpStatusCode.NotFound);

        // Втрата контейнера в дорозі: причина обов'язкова
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/seal", new { temperatureSeal = 5.0 }));
        await Json(await nurse.PostAsJsonAsync($"{Base}/containers/{id}/dispatch", new { }));
        await AssertProblem(await tech.PostAsJsonAsync($"{Base}/containers/{id}/lost", new { reason = "" }), HttpStatusCode.BadRequest);
        var lost = await Json(await tech.PostAsJsonAsync($"{Base}/containers/{id}/lost", new { reason = "Не доставлено кур'єром" }));
        Assert.Equal("LOST", lost["status"]!.GetValue<string>());
        await AssertProblem(await tech.PostAsJsonAsync($"{Base}/containers/{id}/receive", new { temperatureReceipt = 5.0, scannedBarcodes = new[] { x } }), HttpStatusCode.Conflict);
    }
}
