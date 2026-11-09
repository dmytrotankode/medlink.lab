// Додаток E ТЗ: інтеграційні тести I-* (контекст, реєстрація, робочий лист, верифікація, портал, QR, проби, логістика, налаштування, норми)
// Ім'я тесту з Додатка E — у [Trait("TZ", ...)]; фільтр: dotnet test --filter "TZ=I-PORT-404-foreign"
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;
using static MedLink.LIS.Tests.Api.LabFlow;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class AppendixEApiTests
{
    private readonly LisApiFactory _f;
    public AppendixEApiTests(LisApiFactory f) => _f = f;

    private HttpClient Admin => _f.As(DemoDataSeeder.Admin);
    private HttpClient Tech => _f.As(DemoDataSeeder.Technician);
    private HttpClient Doctor => _f.As(DemoDataSeeder.Doctor);

    // ------------------------------------------------------------------ контекст
    [Fact, Trait("TZ", "I-CTX-header")]
    public async Task Context_follows_employee_header_and_reports_unresolved_unknown_employee()
    {
        var me = await Json(await _f.As(DemoDataSeeder.Registrar).GetAsync("/api/v1/lab/context/me"));
        Assert.Equal("REGISTRAR", me["employee"]!["labRole"]!.GetValue<string>());
        Assert.Equal(DemoDataSeeder.Registrar, me["requestedEmployeeId"]!.GetValue<string>());
        Assert.True(me["isResolved"]!.GetValue<bool>());
        Assert.NotNull(me["lab"]!["name"]);
        Assert.Equal(6, me["roles"]!.AsArray().Count);
        var ghost = await Json(await _f.As("no-such-employee").GetAsync("/api/v1/lab/context/me"));
        Assert.False(ghost["isResolved"]!.GetValue<bool>());
        Assert.Null(ghost["employee"]);
    }

    [Fact, Trait("TZ", "I-CTX-default-employee")]
    public async Task Context_without_header_uses_configured_default_employee_and_lists_active_staff()
    {
        var me = await Json(await _f.CreateClient().GetAsync("/api/v1/lab/context/me"));
        Assert.NotNull(me["requestedEmployeeId"]);
        var staff = (await Json(await _f.CreateClient().GetAsync("/api/v1/lab/context/employees"))).AsArray();
        Assert.True(staff.Count >= 6);
        foreach (var role in new[] { "LAB_ADMIN", "LAB_DOCTOR", "LAB_TECHNICIAN", "PHLEBOTOMIST", "LOGISTICS_COURIER", "REGISTRAR" })
            Assert.Contains(staff, s => s!["labRole"]!.GetValue<string>() == role);
    }

    // ------------------------------------------------------------------ реєстрація
    [Fact, Trait("TZ", "I-REG-create")]
    public async Task Order_create_validates_patient_tests_and_numbers_orders_sequentially()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var pid = await NewPatient(_f);
        await Problem(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { testIds = new[] { "GLU" } }), HttpStatusCode.BadRequest);              // без пацієнта
        await Problem(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = "no-such", testIds = new[] { "GLU" } }), HttpStatusCode.NotFound);
        await Problem(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid }), HttpStatusCode.BadRequest);                       // без тестів
        await Problem(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid, testIds = new[] { "NO_SUCH_TEST" } }), HttpStatusCode.NotFound);
        await Problem(await _f.As(DemoDataSeeder.Courier).PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid, testIds = new[] { "GLU" } }), HttpStatusCode.Forbidden);

        var a = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid, testIds = new[] { "GLU" } }));
        var b = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid, testIds = new[] { "GLU" }, isUrgentCito = true }));
        var na = a["orderNumber"]!.GetValue<string>(); var nb = b["orderNumber"]!.GetValue<string>();
        Assert.Matches(@"^\d{4}-\d{6}$", na);
        Assert.Equal(na[..4], nb[..4]);
        Assert.Equal(int.Parse(na[5..]) + 1, int.Parse(nb[5..]));   // нумерація без пропусків у межах періоду
        Assert.False(a["isUrgentCito"]!.GetValue<bool>());
        Assert.True(b["isUrgentCito"]!.GetValue<bool>());
        Assert.Equal("NEW", a["status"]!.GetValue<string>());
        Assert.Equal("PENDING", a["samples"]![0]!["status"]!.GetValue<string>());
        // новий пацієнт створюється разом із замовленням, дубль ПІБ+дата народження не плодить пацієнтів
        var np = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { newPatient = new { lastName = "Новенький" + Guid.NewGuid().ToString("N")[..5], firstName = "Іван", birthDate = "1990-01-01", gender = "male" }, testIds = new[] { "GLU" } }));
        Assert.Equal("M", (await Json(await reg.GetAsync($"/api/v1/lab/patients/{np["patientId"]}")))["gender"]!.GetValue<string>());
        await Problem(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { newPatient = new { lastName = "", firstName = "x" }, testIds = new[] { "GLU" } }), HttpStatusCode.BadRequest);
    }

    [Fact, Trait("TZ", "I-REG-batch")]
    public async Task Batch_registration_is_atomic_and_numbers_are_unique()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var p1 = await NewPatient(_f); var p2 = await NewPatient(_f);
        var ok = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders/batch", new[] { new { patientId = p1, testIds = new[] { "GLU" } }, new { patientId = p2, testIds = new[] { "K" } } }));
        Assert.Equal(2, ok.AsArray().Count);
        Assert.Equal(2, ok.AsArray().Select(o => o!["orderNumber"]!.GetValue<string>()).Distinct().Count());
        // одна помилкова позиція відкочує весь пакет
        var before = (await Json(await reg.GetAsync("/api/v1/lab/orders?pageSize=1")))["total"]!.GetValue<int>();
        await Problem(await reg.PostAsJsonAsync("/api/v1/lab/orders/batch", new[] { new { patientId = p1, testIds = new[] { "GLU" } }, new { patientId = "ghost", testIds = new[] { "GLU" } } }), HttpStatusCode.NotFound);
        Assert.Equal(before, (await Json(await reg.GetAsync("/api/v1/lab/orders?pageSize=1")))["total"]!.GetValue<int>());
        await Problem(await _f.As(DemoDataSeeder.Courier).PostAsJsonAsync("/api/v1/lab/orders/batch", new[] { new { patientId = p1, testIds = new[] { "GLU" } } }), HttpStatusCode.Forbidden);
    }

    [Fact, Trait("TZ", "U-OrderNumberMask")]
    public async Task Parallel_registrations_never_produce_duplicate_order_numbers()
    {
        var pids = new List<string>();
        for (var i = 0; i < 6; i++) pids.Add(await NewPatient(_f));
        var reg = _f.As(DemoDataSeeder.Registrar);
        var tasks = pids.Select(p => reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = p, testIds = new[] { "GLU" } })).ToList();
        var responses = await Task.WhenAll(tasks);
        var numbers = new List<string>();
        foreach (var r in responses)
        {
            var body = await r.Content.ReadAsStringAsync();
            Assert.True(r.StatusCode == HttpStatusCode.Created, $"{(int)r.StatusCode}: {body}");
            numbers.Add(JsonNode.Parse(body)!["orderNumber"]!.GetValue<string>());
        }
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    // ------------------------------------------------------------------ проби й логістика
    [Fact, Trait("TZ", "I-PHL-collect-404-409")]
    public async Task Collect_unknown_barcode_is_404_twice_is_409_and_courier_cannot_collect()
    {
        var nurse = _f.As(DemoDataSeeder.Nurse);
        await Problem(await nurse.PostAsJsonAsync("/api/v1/lab/samples/00000000/collect", new { }), HttpStatusCode.NotFound);
        var pid = await NewPatient(_f);
        var order = await Json(await Admin.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid, testIds = new[] { "GLU" } }));
        var barcode = order["samples"]![0]!["barcode"]!.GetValue<string>();
        Assert.Equal(8, barcode.Length);
        await Problem(await _f.As(DemoDataSeeder.Courier).PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/collect", new { }), HttpStatusCode.Forbidden);
        var got = await Json(await nurse.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/collect", new { volumeMl = 3.5 }));
        Assert.Equal("COLLECTED", got["status"]!.GetValue<string>());
        Assert.NotNull(got["collectedAt"]);
        await Problem(await nurse.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/collect", new { }), HttpStatusCode.Conflict);
        // паралельний подвійний забір: рівно один успіх
        var order2 = await Json(await Admin.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid, testIds = new[] { "GLU" } }));
        var bc2 = order2["samples"]![0]!["barcode"]!.GetValue<string>();
        var rs = await Task.WhenAll(nurse.PostAsJsonAsync($"/api/v1/lab/samples/{bc2}/collect", new { }), _f.As(DemoDataSeeder.Admin).PostAsJsonAsync($"/api/v1/lab/samples/{bc2}/collect", new { }));
        Assert.Equal(1, rs.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, rs.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact, Trait("TZ", "I-SAMPLE-status-transitions")]
    public async Task Sample_status_machine_allows_only_declared_transitions()
    {
        var pid = await NewPatient(_f);
        var order = await Json(await Admin.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid, testIds = new[] { "GLU" } }));
        var bc = order["samples"]![0]!["barcode"]!.GetValue<string>();
        async Task<HttpResponseMessage> T(string action) => await Admin.PostAsJsonAsync($"/api/v1/lab/samples/{bc}/transition", new { status = action });
        // PENDING: приймати/зберігати/обробляти/утилізувати не можна
        foreach (var action in new[] { "RECEIVE", "PROCESS", "STORE", "DISPOSE" }) await Problem(await T(action), HttpStatusCode.Conflict);
        await Problem(await Admin.PostAsJsonAsync($"/api/v1/lab/samples/{bc}/receive", new { }), HttpStatusCode.Conflict);
        await Json(await Admin.PostAsJsonAsync($"/api/v1/lab/samples/{bc}/collect", new { }));
        await Problem(await T("STORE"), HttpStatusCode.Conflict);     // COLLECTED → STORE недоступно
        Assert.Equal("RECEIVED", (await Json(await Admin.PostAsJsonAsync($"/api/v1/lab/samples/{bc}/receive", new { })))["status"]!.GetValue<string>());
        Assert.Equal("PROCESSING", (await Json(await T("PROCESS")))["status"]!.GetValue<string>());
        // забір скасувати вже не можна; повторний прийом — також
        await Problem(await Admin.PostAsync($"/api/v1/lab/samples/{bc}/uncollect", null), HttpStatusCode.Conflict);
        await Problem(await Admin.PostAsJsonAsync($"/api/v1/lab/samples/{bc}/receive", new { }), HttpStatusCode.Conflict);
        Assert.Equal("STORED", (await Json(await T("STORE")))["status"]!.GetValue<string>());
        Assert.Equal("DISPOSED", (await Json(await T("DISPOSE")))["status"]!.GetValue<string>());
        foreach (var action in new[] { "PROCESS", "STORE", "DISPOSE", "RECEIVE" }) await Problem(await T(action), HttpStatusCode.Conflict); // DISPOSED — кінцевий
        await Problem(await Admin.GetAsync("/api/v1/lab/samples/00000000"), HttpStatusCode.NotFound);
    }

    [Fact, Trait("TZ", "I-LOG-reject-repeat-order")]
    public async Task Rejecting_a_sample_rejects_its_tests_and_can_create_a_repeat_order()
    {
        var o = await ReceivedOrder(_f, new[] { "GLU" });
        await Problem(await Tech.PostAsJsonAsync($"/api/v1/lab/samples/{o.Barcode}/reject", new { reason = "" }), HttpStatusCode.BadRequest);
        await Problem(await _f.As(DemoDataSeeder.Nurse).PostAsJsonAsync($"/api/v1/lab/samples/{o.Barcode}/reject", new { reason = "Гемоліз" }), HttpStatusCode.Forbidden);
        var rej = await Json(await Tech.PostAsJsonAsync($"/api/v1/lab/samples/{o.Barcode}/reject", new { reason = "Гемоліз ++", createRepeatOrder = true }));
        Assert.Equal("REJECTED", rej["sample"]!["status"]!.GetValue<string>());
        Assert.Contains("GLU", rej["rejectedTests"]!.AsArray().Select(t => t!.GetValue<string>()));
        var repeatId = rej["repeatOrderId"]!.GetValue<string>();
        var repeat = await Json(await Admin.GetAsync($"/api/v1/lab/orders/{repeatId}"));
        Assert.Equal(o.PatientId, repeat["patientId"]!.GetValue<string>());
        Assert.Contains("Повторний забір", repeat["clinicalNotes"]!.GetValue<string>());
        Assert.Equal("NEW", repeat["status"]!.GetValue<string>());
        Assert.Contains(repeat["tests"]!.AsArray(), t => t!["testCode"]!.GetValue<string>() == "GLU");
        var original = await Json(await Admin.GetAsync($"/api/v1/lab/orders/{o.OrderId}"));
        Assert.Equal("REJECTED", original["tests"]![0]!["status"]!.GetValue<string>());
        // повторна відбраковка → 409 (кінцевий статус), результат у відбракованій пробірці ввести не можна
        await Problem(await Tech.PostAsJsonAsync($"/api/v1/lab/samples/{o.Barcode}/reject", new { reason = "ще раз" }), HttpStatusCode.Conflict);
        await Problem(await Tech.PutAsJsonAsync($"/api/v1/lab/results/{o.Test("GLU")}", new { numericValue = 5.0 }), HttpStatusCode.Conflict);
    }

    [Fact, Trait("TZ", "I-LOG-coldchain-flag")]
    public async Task Manifest_flags_cold_chain_violation_by_laboratory_limits_and_reject_rejects_samples()
    {
        var courier = _f.As(DemoDataSeeder.Courier);
        var tech = Tech;
        var settings = await Json(await Admin.GetAsync("/api/v1/lab/settings/lab"));
        var min = settings["coldChainMinC"]!.GetValue<double>(); var max = settings["coldChainMaxC"]!.GetValue<double>();

        async Task<(JsonNode Manifest, FlowOrder Order)> Ship(double dispatchT)
        {
            var pid = await NewPatient(_f);
            var order = await Json(await Admin.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid, departmentId = DemoDataSeeder.CollectionPoint, testIds = new[] { "GLU" } }));
            var bc = order["samples"]![0]!["barcode"]!.GetValue<string>();
            await Json(await Admin.PostAsJsonAsync($"/api/v1/lab/samples/{bc}/collect", new { }));
            var m = await Json(await courier.PostAsJsonAsync("/api/v1/lab/logistics/manifests", new { originDepartmentId = DemoDataSeeder.CollectionPoint, destinationDepartmentId = DemoDataSeeder.Dept, courierName = "Ткаченко", temperatureDispatch = dispatchT, barcodes = new[] { bc } }));
            return (m, new FlowOrder(order["id"]!.GetValue<string>(), pid, new List<string> { bc }, new Dictionary<string, string>()));
        }

        // у межах — порушення немає; поза межами — прапорець; лише прийом у лабораторії міняє статус проби
        var (m1, o1) = await Ship((min + max) / 2);
        var r1 = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/logistics/manifests/{m1["id"]}/receive", new { temperatureReceipt = (min + max) / 2, receivedBarcodes = new[] { o1.Barcode } }));
        Assert.False(r1["isColdChainViolated"]!.GetValue<bool>());
        var (m2, o2) = await Ship(max + 10);
        var r2 = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/logistics/manifests/{m2["id"]}/receive", new { temperatureReceipt = max + 10, receivedBarcodes = new[] { o2.Barcode } }));
        Assert.True(r2["isColdChainViolated"]!.GetValue<bool>());
        // маніфест з двох пробірок: прийнято одну — друга позначається MISSING (порожній перелік = прийнято все)
        async Task<string> Collected()
        {
            var ord = await Json(await Admin.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = await NewPatient(_f), departmentId = DemoDataSeeder.CollectionPoint, testIds = new[] { "GLU" } }));
            var bc = ord["samples"]![0]!["barcode"]!.GetValue<string>();
            await Json(await Admin.PostAsJsonAsync($"/api/v1/lab/samples/{bc}/collect", new { }));
            return bc;
        }
        var bc3a = await Collected();
        var pid3 = await NewPatient(_f);
        var order3b = await Json(await Admin.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = pid3, departmentId = DemoDataSeeder.CollectionPoint, testIds = new[] { "GLU" } }));
        var bc3b = order3b["samples"]![0]!["barcode"]!.GetValue<string>();
        await Json(await Admin.PostAsJsonAsync($"/api/v1/lab/samples/{bc3b}/collect", new { }));
        var m3b = await Json(await courier.PostAsJsonAsync("/api/v1/lab/logistics/manifests", new { originDepartmentId = DemoDataSeeder.CollectionPoint, destinationDepartmentId = DemoDataSeeder.Dept, temperatureDispatch = 5.0, barcodes = new[] { bc3b, bc3a } }));
        var r3 = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/logistics/manifests/{m3b["id"]}/receive", new { temperatureReceipt = 5.0, receivedBarcodes = new[] { bc3b } }));
        Assert.Equal("RECEIVED", r3["items"]!.AsArray().First(i => i!["barcode"]!.GetValue<string>() == bc3b)!["status"]!.GetValue<string>());
        Assert.Equal("MISSING", r3["items"]!.AsArray().First(i => i!["barcode"]!.GetValue<string>() == bc3a)!["status"]!.GetValue<string>());

        // відхилення маніфесту відбраковує всі його пробірки й тести; повторно — 409
        var (m4, o4) = await Ship(max);
        await Problem(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync($"/api/v1/lab/logistics/manifests/{m4["id"]}/reject", new { reason = "x" }), HttpStatusCode.Forbidden); // реєстратор не приймає
        var rejected = await Json(await tech.PostAsJsonAsync($"/api/v1/lab/logistics/manifests/{m4["id"]}/reject", new { reason = "Порушено холодовий ланцюг" }));
        Assert.Equal("REJECTED", rejected["status"]!.GetValue<string>());
        Assert.Equal("REJECTED", (await Json(await Admin.GetAsync($"/api/v1/lab/samples/{o4.Barcode}")))["status"]!.GetValue<string>());
        await Problem(await tech.PostAsJsonAsync($"/api/v1/lab/logistics/manifests/{m4["id"]}/reject", new { reason = "ще раз" }), HttpStatusCode.Conflict);
        await Problem(await tech.PostAsJsonAsync($"/api/v1/lab/logistics/manifests/{m4["id"]}/receive", new { temperatureReceipt = 5.0, receivedBarcodes = new[] { o4.Barcode } }), HttpStatusCode.Conflict);
    }

    // ------------------------------------------------------------------ робочий лист
    [Fact, Trait("TZ", "I-WS-batch")]
    public async Task Worklist_batch_assigns_tests_to_analyzer_follows_status_machine_and_prints()
    {
        var o = await ReceivedOrder(_f, new[] { "GLU", "UREA" });
        await Problem(await Tech.PostAsJsonAsync("/api/v1/lab/worklist/batches", new { orderTestIds = Array.Empty<string>() }), HttpStatusCode.BadRequest);
        await Problem(await Tech.PostAsJsonAsync("/api/v1/lab/worklist/batches", new { orderTestIds = new[] { "ghost" } }), HttpStatusCode.BadRequest);
        await Problem(await Tech.PostAsJsonAsync("/api/v1/lab/worklist/batches", new { analyzerId = "ghost", orderTestIds = new[] { o.Test("GLU") } }), HttpStatusCode.NotFound);
        await Problem(await _f.As(DemoDataSeeder.Courier).PostAsJsonAsync("/api/v1/lab/worklist/batches", new { orderTestIds = new[] { o.Test("GLU") } }), HttpStatusCode.Forbidden);

        var batch = await Json(await Tech.PostAsJsonAsync("/api/v1/lab/worklist/batches", new { analyzerId = DemoDataSeeder.Mindray, orderTestIds = new[] { o.Test("GLU"), o.Test("UREA"), o.Test("GLU") } }));
        Assert.Matches(@"^WL-\d{6}-\d{3}$", batch["batchCode"]!.GetValue<string>());
        Assert.Equal("OPEN", batch["status"]!.GetValue<string>());
        Assert.Equal(2, batch["items"]!.AsArray().Count); // дублі в запиті прибрано
        var row = await Json(await Tech.GetAsync($"/api/v1/lab/results/{o.Test("GLU")}"));
        Assert.Equal("IN_ANALYSIS", row["status"]!.GetValue<string>());
        Assert.Equal(DemoDataSeeder.Mindray, row["analyzerId"]!.GetValue<string>());

        var id = batch["id"]!.GetValue<string>();
        var html = await (await Tech.GetAsync($"/api/v1/lab/worklist/batches/{id}/print")).Content.ReadAsStringAsync();
        Assert.Contains(batch["batchCode"]!.GetValue<string>(), html);
        Assert.Contains(o.Barcode, html);
        // OPEN → SENT → COMPLETED; назад із COMPLETED, а також пропуск неіснуючого переходу — 409
        await Problem(await Tech.PutAsJsonAsync($"/api/v1/lab/worklist/batches/{id}", new { status = "BOGUS" }), HttpStatusCode.Conflict);
        Assert.Equal("SENT", (await Json(await Tech.PutAsJsonAsync($"/api/v1/lab/worklist/batches/{id}", new { status = "SENT" })))["status"]!.GetValue<string>());
        Assert.Equal("COMPLETED", (await Json(await Tech.PutAsJsonAsync($"/api/v1/lab/worklist/batches/{id}", new { status = "COMPLETED" })))["status"]!.GetValue<string>());
        await Problem(await Tech.PutAsJsonAsync($"/api/v1/lab/worklist/batches/{id}", new { status = "OPEN" }), HttpStatusCode.Conflict);
        await Problem(await Tech.DeleteAsync($"/api/v1/lab/worklist/batches/{id}"), HttpStatusCode.Conflict);
        var list = (await Json(await Tech.GetAsync("/api/v1/lab/worklist/batches?status=COMPLETED"))).AsArray();
        Assert.Contains(list, b => b!["id"]!.GetValue<string>() == id);
        // відкритий лист можна видалити
        var open = await Json(await Tech.PostAsJsonAsync("/api/v1/lab/worklist/batches", new { orderTestIds = new[] { o.Test("UREA") } }));
        Assert.Equal(HttpStatusCode.NoContent, (await Tech.DeleteAsync($"/api/v1/lab/worklist/batches/{open["id"]}")).StatusCode);
        await Problem(await Tech.DeleteAsync($"/api/v1/lab/worklist/batches/{open["id"]}"), HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------ верифікація
    [Fact, Trait("TZ", "I-VAL-crit-comment")]
    public async Task Critical_result_requires_comment_and_only_doctor_or_admin_verify()
    {
        var o = await ReceivedOrder(_f, new[] { "K" });
        var row = await Enter(_f, DemoDataSeeder.Technician, o, "K", 6.9);
        Assert.Equal("CRIT_HIGH", row["flag"]!.GetValue<string>());
        await Problem(await Doctor.PostAsJsonAsync($"/api/v1/lab/results/{o.Test("K")}/verify", new { }), HttpStatusCode.BadRequest);
        await Problem(await Doctor.PostAsJsonAsync($"/api/v1/lab/results/{o.Test("K")}/verify", new { comment = "   " }), HttpStatusCode.BadRequest);
        await Problem(await Tech.PostAsJsonAsync($"/api/v1/lab/results/{o.Test("K")}/verify", new { comment = "ок" }), HttpStatusCode.Forbidden);
        await Problem(await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync($"/api/v1/lab/results/{o.Test("K")}/verify", new { comment = "ок" }), HttpStatusCode.Forbidden);
        var v = await Json(await Doctor.PostAsJsonAsync($"/api/v1/lab/results/{o.Test("K")}/verify", new { comment = "Лікаря повідомлено телефоном" }));
        Assert.Equal("VERIFIED", v["status"]!.GetValue<string>());
        // повторна верифікація → 409
        await Problem(await Doctor.PostAsJsonAsync($"/api/v1/lab/results/{o.Test("K")}/verify", new { comment = "ще" }), HttpStatusCode.Conflict);
        var hist = (await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test("K")}/history"))).AsArray();
        Assert.Contains(hist, h => h!["action"]!.GetValue<string>() == "VERIFIED" && (h["comment"]?.GetValue<string>() ?? "").Contains("телефоном"));
    }

    [Fact, Trait("TZ", "I-VAL-batch-skipped")]
    public async Task Batch_verification_skips_blocked_rows_with_reasons_and_leaves_them_untouched()
    {
        var o = await ReceivedOrder(_f, new[] { "GLU", "K", "UREA" });
        await Enter(_f, DemoDataSeeder.Technician, o, "GLU", 8.0);     // вище норми — верифікується лікарем без коментаря
        await Enter(_f, DemoDataSeeder.Technician, o, "K", 6.9);       // критичне — потрібен коментар → буде пропущено
        // UREA без результату → пропущено
        var res = await Json(await Doctor.PostAsJsonAsync("/api/v1/lab/results/verify-batch", new { orderTestIds = new[] { o.Test("GLU"), o.Test("K"), o.Test("UREA"), o.Test("GLU"), "ghost" } }));
        Assert.Equal(1, res["verified"]!.GetValue<int>());
        var skipped = res["skipped"]!.AsArray();
        Assert.Equal(3, skipped.Count);
        Assert.Contains(skipped, s => s!["id"]!.GetValue<string>() == o.Test("K") && s["reason"]!.GetValue<string>().Length > 5);
        Assert.Equal("VERIFIED", (await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test("GLU")}")))["status"]!.GetValue<string>());
        Assert.Equal("NEEDS_REVIEW", (await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test("K")}")))["status"]!.GetValue<string>());
        Assert.Equal("PENDING", (await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test("UREA")}")))["status"]!.GetValue<string>());
        // загальний коментар пакету закриває вимогу до критичного
        var res2 = await Json(await Doctor.PostAsJsonAsync("/api/v1/lab/results/verify-batch", new { orderTestIds = new[] { o.Test("K") }, comment = "Критичне, повідомлено" }));
        Assert.Equal(1, res2["verified"]!.GetValue<int>());
        // лаборант не верифікує: пакет повертає 200, але жодного рядка не верифіковано, кожен — у «skipped»
        var forbidden = await Json(await Tech.PostAsJsonAsync("/api/v1/lab/results/verify-batch", new { orderTestIds = new[] { o.Test("UREA") } }));
        Assert.Equal(0, forbidden["verified"]!.GetValue<int>());
        Assert.Single(forbidden["skipped"]!.AsArray());
    }

    [Fact, Trait("TZ", "I-VAL-reopen-audit")]
    public async Task Reopen_of_verified_result_is_doctor_only_audited_and_reverts_release_state()
    {
        var o = await ReceivedOrder(_f, new[] { "GLU" });
        var auto = await Enter(_f, DemoDataSeeder.Technician, o, "GLU", 5.0);
        Assert.Equal("AUTO_VERIFIED", auto["status"]!.GetValue<string>());
        Assert.NotNull(auto["releasedAt"]);
        await Problem(await Tech.PostAsJsonAsync($"/api/v1/lab/results/{o.Test("GLU")}/reopen", new { reason = "помилка" }), HttpStatusCode.Forbidden);
        var reopened = await Json(await Doctor.PostAsJsonAsync($"/api/v1/lab/results/{o.Test("GLU")}/reopen", new { reason = "Підозра на плутанину пробірок" }));
        Assert.Equal("NEEDS_REVIEW", reopened["status"]!.GetValue<string>());
        Assert.Null(reopened["releasedAt"]);            // пацієнт більше не бачить результату
        Assert.False(reopened["isAutoVerified"]!.GetValue<bool>());
        var hist = (await Json(await Admin.GetAsync($"/api/v1/lab/results/{o.Test("GLU")}/history"))).AsArray();
        Assert.Contains(hist, h => h!["action"]!.GetValue<string>() == "REOPENED" && h["comment"]!.GetValue<string>() == "Підозра на плутанину пробірок" && h["actorId"]!.GetValue<string>() == DemoDataSeeder.Doctor);
        var audit = await Json(await Admin.GetAsync($"/api/v1/lab/audit?action=REOPEN&entityId={o.Test("GLU")}"));
        Assert.Single((audit["items"] ?? audit).AsArray());
        // вдруге з того ж стану (NEEDS_REVIEW) нічого повертати — 409
        await Problem(await Doctor.PostAsJsonAsync($"/api/v1/lab/results/{o.Test("GLU")}/reopen", new { reason = "ще" }), HttpStatusCode.Conflict);
        // портал: відкликаний результат зник
        var portal = await Json(await _f.CreateClient().GetAsync($"/api/v1/lab/portal/{o.PatientId}/orders/{o.OrderId}"));
        Assert.Empty(portal["tests"]!.AsArray());
        Assert.DoesNotContain("5.0", await _f.CreateClient().GetStringAsync($"/api/v1/lab/portal/{o.PatientId}/orders/{o.OrderId}"));
    }

    // ------------------------------------------------------------------ QR / портал
    private async Task<(FlowOrder Order, string Token)> ReleasedOrder()
    {
        var o = await ReceivedOrder(_f, new[] { "GLU" });
        await Enter(_f, DemoDataSeeder.Technician, o, "GLU", 5.0);
        var released = await Json(await Doctor.PostAsJsonAsync($"/api/v1/lab/orders/{o.OrderId}/release", new { }));
        return (o, released["verifyToken"]!.GetValue<string>());
    }

    [Fact, Trait("TZ", "I-VERIFY-public-no-pii")]
    public async Task Public_verification_by_token_shows_no_personal_data_and_dies_with_reopen()
    {
        var (o, token) = await ReleasedOrder();
        Assert.Matches("^[A-Za-z0-9_-]{20,}$", token);
        var raw = await _f.CreateClient().GetStringAsync($"/api/v1/lab/verify/{token}");
        var v = JsonNode.Parse(raw)!;
        Assert.True(v["valid"]!.GetValue<bool>());
        Assert.False(string.IsNullOrEmpty(v["orderNumber"]!.GetValue<string>()));
        var patient = await Json(await Admin.GetAsync($"/api/v1/lab/patients/{o.PatientId}"));
        var last = patient["lastName"]?.GetValue<string>() ?? patient["fullName"]?.GetValue<string>() ?? "Тестенко";
        Assert.DoesNotContain(last, raw);               // ПІБ пацієнта у відповіді немає
        foreach (var banned in new[] { "birth", "phone", "patient", "result", "value" }) Assert.DoesNotContain(banned, raw, StringComparison.OrdinalIgnoreCase);
        // підроблений/обрізаний токен → 404
        await Problem(await _f.CreateClient().GetAsync($"/api/v1/lab/verify/{token[..^2]}xx"), HttpStatusCode.NotFound);
        await Problem(await _f.CreateClient().GetAsync("/api/v1/lab/verify/not-a-token"), HttpStatusCode.NotFound);
        // повернення в роботу анулює токен (бланк перестає бути чинним)
        await Problem(await Tech.PostAsJsonAsync($"/api/v1/lab/orders/{o.OrderId}/reopen", new { reason = "x" }), HttpStatusCode.Forbidden);
        Assert.Equal("IN_PROGRESS", (await Json(await Doctor.PostAsJsonAsync($"/api/v1/lab/orders/{o.OrderId}/reopen", new { reason = "Уточнення результату" })))["status"]!.GetValue<string>());
        await Problem(await _f.CreateClient().GetAsync($"/api/v1/lab/verify/{token}"), HttpStatusCode.NotFound);
        Assert.Contains("REOPEN", await Admin.GetStringAsync($"/api/v1/lab/audit?entity=lab_order&entityId={o.OrderId}"));
    }

    [Fact, Trait("TZ", "I-PORT-no-values-before-release")]
    public async Task Portal_hides_values_of_tests_that_are_not_released_yet()
    {
        var o = await ReceivedOrder(_f, new[] { "GLU", "K" });
        await Enter(_f, DemoDataSeeder.Technician, o, "GLU", 5.0);   // нормальне → автоверифікація → відкрито одразу
        await Enter(_f, DemoDataSeeder.Technician, o, "K", 7.37);     // критичне → чекає лікаря → закрито
        var portal = await Json(await _f.CreateClient().GetAsync($"/api/v1/lab/portal/{o.PatientId}/orders/{o.OrderId}"));
        var glu = portal["tests"]!.AsArray().First(t => t!["testCode"]!.GetValue<string>() == "GLU")!;
        Assert.NotNull(glu["value"]);
        // закритий тест є лише в «очікуваних» — без значення, прапорця й норми
        Assert.DoesNotContain(portal["tests"]!.AsArray(), t => t!["testCode"]!.GetValue<string>() == "K");
        var pending = portal["pendingTests"]!.AsArray().First(t => t!["testCode"]!.GetValue<string>() == "K")!;
        Assert.Equal("PENDING", pending["status"]!.GetValue<string>());
        Assert.Null(pending["value"]);
        var raw = (await _f.CreateClient().GetStringAsync($"/api/v1/lab/portal/{o.PatientId}/orders/{o.OrderId}"));
        Assert.DoesNotContain("7.37", raw); Assert.DoesNotContain("CRIT_HIGH", raw);
        Assert.Equal(50, portal["progress"]!["percent"]!.GetValue<int>());
        // список замовлень і історія теж не містять закритого значення
        Assert.DoesNotContain("7.37", await _f.CreateClient().GetStringAsync($"/api/v1/lab/portal/{o.PatientId}/history"));
        // після верифікації лікарем значення відкривається
        await Json(await Doctor.PostAsJsonAsync($"/api/v1/lab/results/{o.Test("K")}/verify", new { comment = "повідомлено" }));
        var after = await Json(await _f.CreateClient().GetAsync($"/api/v1/lab/portal/{o.PatientId}/orders/{o.OrderId}"));
        Assert.NotNull(after["tests"]!.AsArray().First(t => t!["testCode"]!.GetValue<string>() == "K")!["value"]);
        Assert.Equal(100, after["progress"]!["percent"]!.GetValue<int>());
    }

    [Fact, Trait("TZ", "I-PORT-404-foreign")]
    public async Task Portal_does_not_expose_another_patients_order_or_unknown_patient()
    {
        var o = await ReceivedOrder(_f, new[] { "GLU" });
        await Enter(_f, DemoDataSeeder.Technician, o, "GLU", 5.0);
        var stranger = await NewPatient(_f);
        var anon = _f.CreateClient();
        await Problem(await anon.GetAsync($"/api/v1/lab/portal/{stranger}/orders/{o.OrderId}"), HttpStatusCode.NotFound);
        await Problem(await anon.GetAsync($"/api/v1/lab/portal/{stranger}/orders/{o.OrderId}/report.pdf"), HttpStatusCode.NotFound);
        await Problem(await anon.GetAsync("/api/v1/lab/portal/no-such-patient/orders"), HttpStatusCode.NotFound);
        await Problem(await anon.GetAsync("/api/v1/lab/portal/no-such-patient/trend/GLU"), HttpStatusCode.NotFound);
        Assert.DoesNotContain(o.OrderId, await anon.GetStringAsync($"/api/v1/lab/portal/{stranger}/orders"));
        Assert.DoesNotContain(o.OrderId, await anon.GetStringAsync($"/api/v1/lab/portal/{stranger}/history"));
        Assert.Contains(o.OrderId, await anon.GetStringAsync($"/api/v1/lab/portal/{o.PatientId}/orders"));
    }

    [Fact, Trait("TZ", "I-PORT-trend-points")]
    public async Task Portal_trend_returns_only_released_points_in_time_order()
    {
        var pid = await NewPatient(_f);
        var o1 = await ReceivedOrder(_f, new[] { "GLU" }, pid);
        await Enter(_f, DemoDataSeeder.Technician, o1, "GLU", 4.5);      // відкрито
        var o2 = await ReceivedOrder(_f, new[] { "GLU" }, pid);
        await Enter(_f, DemoDataSeeder.Technician, o2, "GLU", 4.8);      // відкрито
        var o3 = await ReceivedOrder(_f, new[] { "GLU" }, pid);
        await Enter(_f, DemoDataSeeder.Technician, o3, "GLU", 9.9);      // вище норми → не верифіковано → не в тренді
        var trend = (await Json(await _f.CreateClient().GetAsync($"/api/v1/lab/portal/{pid}/trend/GLU"))).AsArray();
        Assert.Equal(new[] { 4.5, 4.8 }, trend.Select(p => p!["value"]!.GetValue<double>()).ToArray());
        Assert.True(trend[0]!["at"]!.GetValue<DateTime>() <= trend[1]!["at"]!.GetValue<DateTime>());
        Assert.Empty((await Json(await _f.CreateClient().GetAsync($"/api/v1/lab/portal/{pid}/trend/UREA"))).AsArray());
    }

    // ------------------------------------------------------------------ налаштування, довідники, норми
    [Fact, Trait("TZ", "I-ADM-settings")]
    public async Task Lab_settings_are_admin_only_validated_and_audited()
    {
        var before = await Json(await Admin.GetAsync("/api/v1/lab/settings/lab"));
        var originalName = before["name"]!.GetValue<string>();
        await Problem(await Doctor.PutAsJsonAsync("/api/v1/lab/settings/lab", new { name = "Злом" }), HttpStatusCode.Forbidden);
        await Problem(await Admin.PutAsJsonAsync("/api/v1/lab/settings/lab", new { orderNumberMask = "NOCOUNTER" }), HttpStatusCode.BadRequest);
        var changed = await Json(await Admin.PutAsJsonAsync("/api/v1/lab/settings/lab", new { name = "Лабораторія-тест", panicPhone = "+380441112233" }));
        Assert.Equal("Лабораторія-тест", changed["name"]!.GetValue<string>());
        Assert.Equal("Лабораторія-тест", (await Json(await Tech.GetAsync("/api/v1/lab/settings/lab")))["name"]!.GetValue<string>());
        Assert.Equal("Лабораторія-тест", (await Json(await Tech.GetAsync("/api/v1/lab/context/me")))["lab"]!["name"]!.GetValue<string>());
        Assert.Contains("lab_settings", await Admin.GetStringAsync("/api/v1/lab/audit?entity=lab_settings"));
        await Json(await Admin.PutAsJsonAsync("/api/v1/lab/settings/lab", new { name = originalName }));
        // нумератори: лише адмін змінює, невідомий код → 404
        var nums = (await Json(await Admin.GetAsync("/api/v1/lab/settings/numerators"))).AsArray();
        Assert.Contains(nums, n => n!["code"]!.GetValue<string>() == "lab_order_number");
        await Problem(await Doctor.PutAsJsonAsync("/api/v1/lab/settings/numerators/lab_order_number", new { name = "x" }), HttpStatusCode.Forbidden);
        await Problem(await Admin.PutAsJsonAsync("/api/v1/lab/settings/numerators/no_such", new { name = "x" }), HttpStatusCode.NotFound);
        Assert.NotEmpty((await Json(await Admin.GetAsync("/api/v1/lab/settings/counters"))).AsArray());
    }

    [Fact, Trait("TZ", "I-NRM-validation-400")]
    public async Task Norm_layer_validation_rejects_inconsistent_limits_and_unknown_test_and_non_editors()
    {
        object Layer(double? nl, double? nh, double? cl, double? ch, string type = "BASELINE", string test = "GLU") => new { testCode = test, layerType = type, normName = "Тест", normLow = nl, normHigh = nh, critLow = cl, critHigh = ch };
        await Problem(await Admin.PostAsJsonAsync("/api/v1/lab/norms/combinations", Layer(7.0, 3.0, null, null)), HttpStatusCode.BadRequest);      // low > high
        await Problem(await Admin.PostAsJsonAsync("/api/v1/lab/norms/combinations", Layer(3.0, 6.0, 4.0, null)), HttpStatusCode.BadRequest);        // critLow > normLow
        await Problem(await Admin.PostAsJsonAsync("/api/v1/lab/norms/combinations", Layer(3.0, 6.0, null, 5.0)), HttpStatusCode.BadRequest);        // critHigh < normHigh
        await Problem(await Admin.PostAsJsonAsync("/api/v1/lab/norms/combinations", Layer(3.0, 6.0, null, null, "WEIRD")), HttpStatusCode.BadRequest);
        await Problem(await Admin.PostAsJsonAsync("/api/v1/lab/norms/combinations", Layer(3.0, 6.0, null, null, "BASELINE", "NO_SUCH")), HttpStatusCode.BadRequest);
        await Problem(await Admin.PostAsJsonAsync("/api/v1/lab/norms/combinations", new { layerType = "BASELINE" }), HttpStatusCode.BadRequest);
        await Problem(await Tech.PostAsJsonAsync("/api/v1/lab/norms/combinations", Layer(1.0, 2.0, null, null)), HttpStatusCode.Forbidden);
        await Problem(await Admin.GetAsync("/api/v1/lab/norms/layers"), HttpStatusCode.BadRequest);
        // коректний шар створюється, застосований у результаті — не видаляється, а деактивується
        var code = await NewCustomTest(_f);
        var layer = await Json(await Admin.PostAsJsonAsync("/api/v1/lab/norms/combinations", Layer(10.0, 20.0, 5.0, 40.0, "DEMOGRAPHIC", code)));
        Assert.True(layer["priorityOrder"]!.GetValue<int>() > 0);
        var o = await ReceivedOrder(_f, new[] { code });
        var row = await Enter(_f, DemoDataSeeder.Technician, o, code, 15);
        Assert.Equal("NORMAL", row["flag"]!.GetValue<string>());
        var del = await Json(await Admin.DeleteAsync($"/api/v1/lab/norms/combinations/{layer["id"]}"));
        Assert.True(del["deactivated"]!.GetValue<bool>() || del["deleted"]!.GetValue<bool>());
    }

    [Fact, Trait("TZ", "I-NRM-service-card")]
    public async Task Service_card_lists_tests_with_their_norm_layers()
    {
        var card = await Json(await Admin.GetAsync("/api/v1/lab/norms/service-card/PROF_BIOCHEM_BASE"));
        var tests = card["tests"]!.AsArray();
        Assert.NotEmpty(tests);
        Assert.Contains(tests, t => t!["code"]!.GetValue<string>() == "GLU");
        Assert.True(tests.First(t => t!["code"]!.GetValue<string>() == "GLU")!["layersCount"]!.GetValue<int>() >= 1);
        Assert.NotNull(card["norms"]);
        await Problem(await Admin.GetAsync("/api/v1/lab/norms/service-card/NO_SUCH_PROFILE"), HttpStatusCode.NotFound);
    }

    [Fact, Trait("TZ", "I-DIC-delete-409")]
    public async Task Dictionary_item_in_use_cannot_be_hard_deleted()
    {
        var code = "BMX" + Guid.NewGuid().ToString("N")[..5].ToUpper();
        var bm = await Json(await Admin.PostAsJsonAsync("/api/v1/lab/dictionaries/biomaterials", new { code, name = "Тестовий біоматеріал " + code, stabilityHours = 12 }));
        var id = bm["id"]!.ToString();
        var testCode = "DT" + Guid.NewGuid().ToString("N")[..5].ToUpper();
        await Json(await Admin.PostAsJsonAsync("/api/v1/lab/dictionaries/tests", new { code = testCode, name = "Залежний показник", unit = "од", biomaterialTypeId = int.Parse(id), category = "BIOCHEM" }));
        var res = await Admin.DeleteAsync($"/api/v1/lab/dictionaries/biomaterials/{id}");
        var body = await res.Content.ReadAsStringAsync();
        // запис із залежними показниками не видаляється фізично: або 409, або м'яке відключення
        if (res.StatusCode == HttpStatusCode.OK) { Assert.Contains("deactivated", body); Assert.DoesNotContain("\"deleted\":true", body); }
        else Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(id, (await Json(await Admin.GetAsync($"/api/v1/lab/dictionaries/biomaterials/{id}")))["id"]!.ToString());
        Assert.Equal(testCode, (await Json(await Admin.GetAsync($"/api/v1/lab/dictionaries/tests?search={testCode}")))["items"]![0]!["code"]!.GetValue<string>());
        // без залежностей — видаляється
        var lone = await Json(await Admin.PostAsJsonAsync("/api/v1/lab/dictionaries/biomaterials", new { code = code + "L", name = "Без залежностей", stabilityHours = 1 }));
        var del = await Json(await Admin.DeleteAsync($"/api/v1/lab/dictionaries/biomaterials/{lone["id"]}"));
        Assert.True(del["deleted"]!.GetValue<bool>());
    }
}
