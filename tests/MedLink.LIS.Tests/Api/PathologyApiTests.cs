// ТЗ розд. 22: модуль «Патоморфологія» — приймальні сценарії S-40…S-48 (прийом, відмова, вирізка, reflex ІГХ, підпис з МКХ-О,
// доповнення, CITO з read-back, друк 014/о, консультація препаратів) і демо-дані 22.14 п. 10
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Data.Seed;
using MedLink.LIS.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class PathologyApiTests
{
    private const string Patient = "0b000000-0000-0000-0000-000000000002";
    private const string B = "/api/v1/lab/pathology";
    private readonly LisApiFactory _f;
    public PathologyApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static async Task<string> Problem(HttpResponseMessage r, HttpStatusCode status)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"очікувалось {(int)status}, отримано {(int)r.StatusCode}: {text}");
        return JsonNode.Parse(text)!["code"]?.GetValue<string>() ?? "";
    }

    private static string S(JsonNode? n) => n!.GetValue<string>();

    private async Task<T> Db<T>(Func<LisDbContext, Task<T>> f)
    {
        using var scope = _f.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<LisDbContext>());
    }

    private HttpClient Registrar => _f.As(DemoDataSeeder.Registrar);
    private HttpClient Pathologist => _f.As(DemoDataSeeder.Doctor);
    private HttpClient Histotech => _f.As(DemoDataSeeder.Technician);

    private static object Container(string mark, string what, bool labeled = true) =>
        new { mark, description = what, fixative = "10% нейтральний формалін", fixationStartAt = DateTime.UtcNow.AddHours(-12), labeled };

    /// <summary>Направлення ПГД з МІС (POST /mis/referrals, type=PATHOLOGY).</summary>
    private async Task<JsonNode> Refer(string kind, object[] containers, object? external = null, string diagnosis = "C50.4 Злоякісне новоутворення молочної залози")
    {
        var body = new
        {
            type = "PATHOLOGY", misReferralId = Guid.NewGuid().ToString(), referralNumber = "ПГД-НП-" + Random.Shared.Next(1000, 9999), patientId = Patient,
            department = "Онкохірургія", doctorName = "Ткаченко В. Ю.", priority = kind == PathKinds.Cito ? "CITO" : "ROUTINE",
            pathology = new
            {
                kind, operationId = Guid.NewGuid().ToString(), clinicalDiagnosis = diagnosis, clinicalData = "Пухлина 2 см", operationText = "Секторальна резекція",
                markingText = $"{containers.Length} контейнери", objects = containers, external, form014 = new { facility = "КНП «Обласний онкологічний центр»" }
            }
        };
        var r = await _f.As(DemoDataSeeder.Doctor).PostAsJsonAsync("/api/v1/lab/mis/referrals", body);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return await Json(r);
    }

    private async Task<JsonNode> Accept(JsonNode referral)
    {
        var codes = referral["objects"]!.AsArray().Select(o => S(o!["barcode"])).ToList();
        return await Json(await Registrar.PostAsJsonAsync($"{B}/incoming/{S(referral["id"])}/accept", new { scannedBarcodes = codes }));
    }

    private async Task<List<string>> Events(string orderId) => await Db(db => db.MisEvents.Where(e => e.OrderId == orderId).OrderBy(e => e.At).Select(e => e.Event).ToListAsync());

    /// <summary>Справа після вирізки й обробки: blocks — кількість шматочків на блок для об'єкта №1.</summary>
    private async Task<string> CaseAtMicroscopy(int[] blocks, string diagnosis = "C50.4 Злоякісне новоутворення молочної залози")
    {
        var c = await Accept(await Refer(PathKinds.Operative, new[] { Container("1", "Сектор молочної залози") }, diagnosis: diagnosis));
        var id = S(c["id"]);
        await Json(await Pathologist.PutAsJsonAsync($"{B}/cases/{id}/gross", new
        {
            macro = "Сектор молочної залози 7×5×3 см", finish = true,
            objects = new[] { new { objectId = S(c["objects"]![0]!["id"]), blocks = blocks.Select(p => new { pieces = p }) } }
        }));
        foreach (var stage in new[] { "PROC", "EMB", "SECT", "STAIN" })
            await Json(await Histotech.PostAsJsonAsync($"{B}/cases/{id}/advance", new { stage }));
        return id;
    }

    private async Task<JsonNode> Cert(HttpClient c) => (await Json(await c.GetAsync("/api/v1/lab/ehealth/session")))["testCertificate"]!;

    [Fact]
    public async Task S40_two_labelled_containers_accepted_case_created_and_mis_notified()
    {
        var referral = await Refer(PathKinds.Operative, new[] { Container("1", "Сектор молочної залози"), Container("2", "Сторожовий лімфовузол") });
        var id = S(referral["id"]);
        Assert.Equal(PathIntake.Incoming, S(referral["intakeStatus"]));
        var incoming = (await Json(await Registrar.GetAsync($"{B}/incoming"))).AsArray();
        var row = incoming.First(i => S(i!["id"]) == id)!;
        Assert.Equal(2, row["containers"]!.GetValue<int>());
        Assert.Equal("Операційний матеріал", S(row["kindLabel"]));

        // Не всі контейнери відскановано (FR-PAT-003)
        var first = S(referral["objects"]![0]!["barcode"]);
        Assert.Equal("containers-not-scanned", await Problem(await Registrar.PostAsJsonAsync($"{B}/incoming/{id}/accept", new { scannedBarcodes = new[] { first } }), HttpStatusCode.UnprocessableEntity));
        // Лікар-патологоанатом не приймає матеріал (22.11)
        Assert.Equal(HttpStatusCode.Forbidden, (await Pathologist.PostAsJsonAsync($"{B}/incoming/{id}/accept", new { scannedBarcodes = Array.Empty<string>() })).StatusCode);

        var c = await Accept(referral);
        Assert.Matches(@"^ПГД-\d{2}-\d{4}$", S(c["caseNo"]));
        Assert.Equal(2, c["objects"]!.AsArray().Count);
        Assert.Equal(PathStages.Received, S(c["stage"]));
        Assert.True(DateTime.Parse(S(c["slaDueAt"])) > DateTime.UtcNow.AddDays(6));
        Assert.Equal(new[] { MisEvents.Received }, await Events(id));
        // Статус для протоколу операції — через GET /mis/referrals/{misReferralId}
        var cases = await Json(await Pathologist.GetAsync($"{B}/cases?q={Uri.EscapeDataString(S(c["caseNo"]))}"));
        Assert.Single(cases["items"]!.AsArray());
        // Повторне надходження того самого направлення — 409
        Assert.Equal(HttpStatusCode.Conflict, (await Registrar.PostAsJsonAsync($"{B}/incoming/{id}/accept", new { scannedBarcodes = Array.Empty<string>() })).StatusCode);
    }

    [Fact]
    public async Task S41_unlabelled_container_blocks_acceptance_reject_sends_reason()
    {
        var referral = await Refer(PathKinds.Biopsy, new[] { Container("?", "Зскрібок ендометрію", labeled: false) }, diagnosis: "N95.0 Постменопаузальна кровотеча");
        var id = S(referral["id"]);
        Assert.Equal("container-unlabeled", await Problem(await Registrar.PostAsJsonAsync($"{B}/incoming/{id}/accept",
            new { scannedBarcodes = referral["objects"]!.AsArray().Select(o => S(o!["barcode"])) }), HttpStatusCode.UnprocessableEntity));
        Assert.Equal(HttpStatusCode.BadRequest, (await Registrar.PostAsJsonAsync($"{B}/incoming/{id}/reject", new { reason = "" })).StatusCode);

        var rejected = await Json(await Registrar.PostAsJsonAsync($"{B}/incoming/{id}/reject", new { reason = "NO_LABEL", comment = "Повторно промаркувати й доставити" }));
        Assert.Equal("Відсутнє або нечитабельне маркування контейнера", S(rejected["rejectReason"]));
        var ev = await Db(db => db.MisEvents.Where(e => e.OrderId == id).ToListAsync());
        Assert.Equal(MisEvents.Rejected, Assert.Single(ev).Event);
        Assert.Contains("NO_LABEL", ev[0].Payload);
        Assert.Equal(OrderStatuses.Rejected, await Db(db => db.Orders.Where(o => o.Id == id).Select(o => o.Status).FirstAsync()));
        Assert.DoesNotContain((await Json(await Registrar.GetAsync($"{B}/incoming"))).AsArray(), i => S(i!["id"]) == id);
    }

    [Fact]
    public async Task S42_gross_with_five_blocks_creates_he_slides_and_price()
    {
        var c = await Accept(await Refer(PathKinds.Operative, new[] { Container("1", "Сектор молочної залози") }));
        var id = S(c["id"]);
        var obj = S(c["objects"]![0]!["id"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await Histotech.PutAsJsonAsync($"{B}/cases/{id}/gross", new { macro = "x", objects = Array.Empty<object>() })).StatusCode);

        var draft = await Json(await Pathologist.PutAsJsonAsync($"{B}/cases/{id}/gross", new { macro = "Сектор 7×5×3 см", objects = new[] { new { objectId = obj, blocks = new[] { new { pieces = 2 }, new { pieces = 1 } } } } }));
        Assert.Equal(PathStages.Gross, S(draft["stage"]));
        var done = await Json(await Pathologist.PutAsJsonAsync($"{B}/cases/{id}/gross", new
        {
            finish = true, objects = new[] { new { objectId = obj, blocks = new[] { 2, 2, 1, 2, 3 }.Select(p => new { pieces = p }) } }
        }));
        var blocks = done["objects"]![0]!["blocks"]!.AsArray();
        Assert.Equal(new[] { "A1", "A2", "A3", "A4", "A5" }, blocks.Select(b => S(b!["code"])));
        Assert.All(blocks, b => Assert.Equal("Гематоксилін-еозин (Г-Е)", S(Assert.Single(b!["slides"]!.AsArray())!["stainMethod"])));
        Assert.Equal(PathPrices.FirstBlock + 4 * PathPrices.NextBlock, done["price"]!.GetValue<decimal>());
        Assert.Equal(10, done["totals"]!["pieces"]!.GetValue<int>());
        Assert.Equal(PathStages.Processing, S(done["stage"]));
        Assert.Contains(MisEvents.StageChanged, await Events(id));
        // Після завершення вирізки блоки не змінюються
        Assert.Equal(HttpStatusCode.Conflict, (await Pathologist.PutAsJsonAsync($"{B}/cases/{id}/gross", new { finish = true, objects = Array.Empty<object>() })).StatusCode);

        // Етапи скануванням касети (FR-PAT-023): справа рухається за найменш просунутою касетою
        var scan = await Json(await Histotech.PostAsJsonAsync($"{B}/scan", new { barcode = S(blocks[0]!["barcode"]), device = "Leica TP1020" }));
        Assert.Equal("PROC", S(scan["stage"]));
        Assert.Equal(PathStages.Processing, S(scan["caseStage"]));
        foreach (var stage in new[] { "PROC", "EMB", "SECT", "STAIN" })
            await Json(await Histotech.PostAsJsonAsync($"{B}/cases/{id}/advance", new { stage }));
        var micro = await Json(await Pathologist.GetAsync($"{B}/cases/{id}"));
        Assert.Equal(PathStages.Microscopy, S(micro["stage"]));
        Assert.Contains(micro["stageLog"]!.AsArray(), l => S(l!["stage"]) == "PROC" && S(l["device"]) == "Leica TP1020" && l["performedByName"] != null);
    }

    [Fact]
    public async Task S43_reflex_ihc_panel_for_breast_carcinoma_extends_sla()
    {
        var id = await CaseAtMicroscopy(new[] { 2, 2 });
        var c = await Json(await Pathologist.PutAsJsonAsync($"{B}/cases/{id}/report", new { micro = "Інвазивна карцинома НСТ", icdoTopo = "C50.4", icdoMorph = "8500/3", grade = "G2" }));
        var reflex = Assert.Single(c["reflex"]!.AsArray())!;
        Assert.Equal(new[] { "ER", "PR", "HER2", "Ki-67" }, reflex["panel"]!.AsArray().Select(S));
        var sla = DateTime.Parse(S(c["slaDueAt"])).ToUniversalTime();
        var price = c["price"]!.GetValue<decimal>();

        var block = S(c["objects"]![0]!["blocks"]![0]!["id"]);
        var after = await Json(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/additional", new { blockId = block, methods = new[] { "ER", "PR", "HER2", "Ki-67" }, source = "LAB" }));
        Assert.Equal(PathologyService.AddWorkdays(sla, 3), DateTime.Parse(S(after["slaDueAt"])).ToUniversalTime());
        Assert.Equal(price + 4 * PathPrices.Ihc, after["price"]!.GetValue<decimal>());
        var ihc = after["objects"]![0]!["blocks"]![0]!["slides"]!.AsArray().Where(s => s!["isIhc"]!.GetValue<bool>()).ToList();
        Assert.Equal(4, ihc.Count);
        Assert.All(ihc, s => Assert.Equal("IHC_IN_WORK", S(s!["status"])));
        Assert.Empty(after["reflex"]![0]!["missing"]!.AsArray());
        Assert.Contains(MisEvents.AdditionalStudyOrdered, await Events(id));
        Assert.Equal(HttpStatusCode.BadRequest, (await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/additional", new { blockId = block, methods = new[] { "НЕІСНУЄ" } })).StatusCode);
    }

    [Fact]
    public async Task S44_S45_sign_requires_icdo_then_addendum_creates_v2_and_keeps_v1()
    {
        var id = await CaseAtMicroscopy(new[] { 2 }, diagnosis: "N60.1 Дифузна кістозна мастопатія");
        var cert = await Cert(Pathologist);
        await Json(await Pathologist.PutAsJsonAsync($"{B}/cases/{id}/report", new { micro = "Інвазивна карцинома НСТ", icd10 = "C50.4" }));
        Assert.Equal("icdo-required", await Problem(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/report/sign", new { signer = cert }), HttpStatusCode.UnprocessableEntity));

        await Json(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/report/preliminary", new { text = "Попередньо: карцинома, остаточний висновок після ІГХ" }));
        await Json(await Pathologist.PutAsJsonAsync($"{B}/cases/{id}/report", new { icdoTopo = "C50.4", icdoMorph = "8500/3", grade = "G2", pt = "pT1c", pn = "pN0", lnTotal = 1, lnPositive = 0, margin = "R0" }));
        Assert.Equal("signer-mismatch", await Problem(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/report/sign", new { signer = new { drfo = "0000000000" } }), HttpStatusCode.UnprocessableEntity));
        var signed = await Json(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/report/sign", new { signer = cert }));
        var c = signed["case"]!;
        Assert.Equal(PathStages.Signed, S(c["stage"]));
        Assert.Equal(OrderStatuses.Released, S(c["status"]));
        var v1 = Assert.Single(c["versions"]!.AsArray())!;
        Assert.Contains("pT1c", S(v1["conclusionText"]));
        // Неочікувана злоякісність при доброякісному клінічному діагнозі — критична знахідка (FR-PAT-047)
        var events = await Events(id);
        Assert.Contains(MisEvents.ResultPreliminary, events);
        Assert.Contains(MisEvents.ResultFinal, events);
        Assert.Contains(MisEvents.CriticalResult, events);
        var final = await Db(db => db.MisEvents.Where(e => e.OrderId == id && e.Event == MisEvents.ResultFinal).Select(e => e.Payload).FirstAsync());
        var payload = JsonNode.Parse(final!)!;
        Assert.Equal("8500/3", S(payload["icdoMorph"]));
        Assert.Equal("pT1c", S(payload["pT"]));

        // Підписаний висновок незмінний (FR-PAT-045)
        Assert.Equal("report-signed", await Problem(await Pathologist.PutAsJsonAsync($"{B}/cases/{id}/report", new { micro = "інше" }), HttpStatusCode.Conflict));
        Assert.Equal(HttpStatusCode.BadRequest, (await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/report/addendum", new { type = "CORRECTION", text = "Виправлення", signer = cert })).StatusCode);
        var add = await Json(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/report/addendum", new
        {
            type = "REVIEW", text = "Після перегляду: інвазивна часточкова карцинома", changesDiagnosis = true, icdoMorph = "8520/3", signer = cert
        }));
        var versions = add["versions"]!.AsArray();
        Assert.Equal(new[] { 1, 2 }, versions.Select(v => v!["version"]!.GetValue<int>()));
        Assert.Equal(S(v1["conclusionText"]), S(versions[0]!["conclusionText"]));
        Assert.Equal("8500/3", S(versions[0]!["icdoMorph"]));
        Assert.Equal("8520/3", S(versions[1]!["icdoMorph"]));
        var addendum = await Db(db => db.MisEvents.Where(e => e.OrderId == id && e.Event == MisEvents.Addendum).Select(e => e.Payload).FirstAsync());
        var ap = JsonNode.Parse(addendum!)!;
        Assert.True(ap["notifyMdk"]!.GetValue<bool>());
        Assert.True(ap["payload"]!["changesDiagnosis"]!.GetValue<bool>());
        Assert.Equal(2, ap["payload"]!["version"]!.GetValue<int>());
    }

    [Fact]
    public async Task S46_cito_notice_requires_read_back_and_records_minutes()
    {
        var c = await Accept(await Refer(PathKinds.Cito, new[] { Container("1", "Лімфовузол (нативний)") }));
        var id = S(c["id"]);
        Assert.True(c["cito"]!.GetValue<bool>());
        Assert.Equal(20, c["citoTimer"]!["targetMinutes"]!.GetValue<int>());
        var top = (await Json(await Pathologist.GetAsync($"{B}/cases?stage=cito")))["items"]!.AsArray();
        Assert.Contains(top, i => S(i!["id"]) == id);

        Assert.Equal("read-back-required", await Problem(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/cito-notice",
            new { text = "Метастаз аденокарциноми в лімфовузлі", notifiedPerson = "Хірург Ельшаавер А. А.", method = "PHONE", readBack = false }), HttpStatusCode.UnprocessableEntity));
        var done = await Json(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/cito-notice",
            new { text = "Метастаз аденокарциноми в лімфовузлі", notifiedPerson = "Хірург Ельшаавер А. А.", method = "PHONE", readBack = true }));
        var notice = Assert.Single(done["citoNotices"]!.AsArray())!;
        Assert.True(notice["readBack"]!.GetValue<bool>());
        Assert.True(notice["minutesFromReceipt"]!.GetValue<int>() >= 0);
        var critical = await Db(db => db.MisEvents.Where(e => e.OrderId == id && e.Event == MisEvents.CriticalResult).Select(e => e.Payload).FirstAsync());
        Assert.NotNull(JsonNode.Parse(critical!)!["operationId"]);
        Assert.True(await Db(db => db.AuditLog.AnyAsync(a => a.EntityId == id && a.Action == "PATH_CITO_NOTICE" && a.AfterJson!.Contains("minutesFromReceipt"))));
        // Не-CITO справа — 409
        var routine = await Accept(await Refer(PathKinds.Biopsy, new[] { Container("1", "Біоптат") }));
        Assert.Equal(HttpStatusCode.Conflict, (await Pathologist.PostAsJsonAsync($"{B}/cases/{S(routine["id"])}/cito-notice", new { text = "x", notifiedPerson = "y", readBack = true })).StatusCode);
    }

    [Fact]
    public async Task S47_form014_side2_item18_empty_until_signed_and_pdf_printed()
    {
        var id = await CaseAtMicroscopy(new[] { 2, 1 });
        await Json(await Pathologist.PutAsJsonAsync($"{B}/cases/{id}/report", new { micro = "Фіброаденома", icd10 = "D24", icdoTopo = "C50.4", icdoMorph = "9010/0" }));
        var before = await Json(await Pathologist.GetAsync($"{B}/cases/{id}/form014"));
        Assert.False(before["signed"]!.GetValue<bool>());
        Assert.Equal("", S(before["side2"]!["18. Патогістологічний висновок"]));
        Assert.Equal("", S(before["side2"]!["Лікар-патологоанатом"]));
        Assert.Equal("3 / 2", S(before["side2"]!["15. Кількість шматочків / блоків"]));
        Assert.Contains("КНП «Обласний онкологічний центр»", S(before["side1"]!["1. Заклад, що направляє"]));

        await Json(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/report/sign", new { signer = await Cert(Pathologist) }));
        var after = await Json(await Pathologist.GetAsync($"{B}/cases/{id}/form014"));
        Assert.True(after["signed"]!.GetValue<bool>());
        Assert.Contains("Фіброаденома", S(after["side2"]!["18. Патогістологічний висновок"]));
        Assert.Contains("(КЕП)", S(after["side2"]!["Лікар-патологоанатом"]));
        Assert.False(string.IsNullOrEmpty(S(after["side2"]!["Лаборант"])));

        var pdf = await Pathologist.GetAsync($"{B}/cases/{id}/form014.pdf?sides=1,2");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..4]));
    }

    [Fact]
    public async Task S48_consultation_six_external_slides_priced_per_slide()
    {
        var referral = await Refer(PathKinds.Consult, Array.Empty<object>(), external: new { institution = "КНП «Міська лікарня №7»", number = "1187/26", date = DateTime.UtcNow.AddDays(-10), slides = 6, blocks = 2 });
        var c = await Accept(referral);
        Assert.Equal(6 * PathPrices.ConsultSlide, c["price"]!.GetValue<decimal>());
        Assert.Equal(PathStages.Microscopy, S(c["stage"]));
        var blocks = c["objects"]![0]!["blocks"]!.AsArray();
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.True(b!["isExternal"]!.GetValue<bool>()));
        Assert.Equal(6, blocks.Sum(b => b!["slides"]!.AsArray().Count));
        Assert.All(blocks.SelectMany(b => b!["slides"]!.AsArray()), s => Assert.True(s!["isExternal"]!.GetValue<bool>()));
        Assert.Equal(6, blocks.SelectMany(b => b!["slides"]!.AsArray()).Select(s => S(s!["barcode"])).Distinct().Count());
    }

    [Fact]
    public async Task Archive_issue_blocks_additional_studies_until_return()
    {
        var id = await CaseAtMicroscopy(new[] { 1 });
        await Json(await Pathologist.PutAsJsonAsync($"{B}/cases/{id}/report", new { micro = "Фіброаденома", icdoTopo = "C50.4", icdoMorph = "9010/0" }));
        await Json(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/report/sign", new { signer = await Cert(Pathologist) }));
        var c = await Json(await Pathologist.GetAsync($"{B}/cases/{id}"));
        var block = c["objects"]![0]!["blocks"]![0]!;
        Assert.Equal("ARCHIVED", S(block["status"]));
        Assert.False(string.IsNullOrEmpty(S(block["storageLocation"])));
        var blockId = S(block["id"]);

        Assert.Equal(HttpStatusCode.Forbidden, (await Pathologist.PostAsJsonAsync($"{B}/blocks/{blockId}/issue", new { recipient = "x", purpose = "y" })).StatusCode);
        var issued = await Json(await Registrar.PostAsJsonAsync($"{B}/blocks/{blockId}/issue", new { recipient = "НІР, молекулярна лабораторія", document = "Акт №1", purpose = "Друга думка", dueBack = DateTime.UtcNow.AddDays(7) }));
        Assert.Equal("ISSUED", S(issued["status"]));
        Assert.Equal("block-issued", await Problem(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/additional", new { blockId, methods = new[] { "ER" } }), HttpStatusCode.Conflict));
        Assert.Contains((await Json(await Registrar.GetAsync($"{B}/archive?status=ISSUED"))).AsArray(), b => S(b!["id"]) == blockId);
        Assert.Equal("ARCHIVED", S((await Json(await Registrar.PostAsync($"{B}/blocks/{blockId}/return", null)))["status"]));
        await Json(await Pathologist.PostAsJsonAsync($"{B}/cases/{id}/additional", new { blockId, methods = new[] { "ER" } }));
    }

    [Fact]
    public async Task Demo_data_has_six_cases_three_incoming_one_unlabelled()
    {
        var cases = await Json(await Pathologist.GetAsync($"{B}/cases"));
        var demo = cases["items"]!.AsArray().Where(i => S(i!["caseNo"]).StartsWith("ПГД-26-04") || S(i!["caseNo"]).StartsWith("ПГД-26-03")).ToList();
        Assert.True(demo.Count >= 6, $"демо-справ {demo.Count}");
        Assert.Equal(new[] { "BIOPSY", "CITO", "CONSULT", "OPERATIVE" }, demo.Select(i => S(i!["kind"])).Distinct().OrderBy(x => x));
        Assert.True(demo.Select(i => S(i!["stage"])).Distinct().Count() >= 4);
        Assert.True(cases["items"]![0]!["cito"]!.GetValue<bool>()); // CITO — угорі журналу
        var incoming = (await Json(await Registrar.GetAsync($"{B}/incoming"))).AsArray();
        Assert.True(incoming.Count(i => S(i!["patient"]) != "") >= 3);
        Assert.Contains(incoming, i => i!["unlabeled"]!.GetValue<int>() > 0);
        var dict = await Json(await Pathologist.GetAsync($"{B}/dictionaries"));
        Assert.Equal(6, dict["rejectReasons"]!.AsArray().Count);
        Assert.Contains(dict["reflex"]!.AsArray(), r => S(r!["panel"]) == "MLH1,PMS2,MSH2,MSH6");
        var summary = await Json(await Pathologist.GetAsync($"{B}/summary"));
        Assert.True(summary["incoming"]!.GetValue<int>() >= 3);
    }
}
