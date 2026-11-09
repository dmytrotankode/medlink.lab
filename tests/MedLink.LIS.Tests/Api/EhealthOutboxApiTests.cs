// Контрактні інтеграційні тести відправки в ЕСОЗ (режим Mock = двійник контракту OpenAPI): повний цикл «пакет звіту → job → окреме погашення направлення»,
// відхилений job і новий пакет, ідемпотентність, недопустимі переходи, витримки й DEAD, відновлення воркера, скасування, анулювання, перелік задач.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Data.Seed;
using MedLink.LIS.Api.Services.Ehealth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedLink.LIS.Tests.Api.EhealthKit;

namespace MedLink.LIS.Tests.Api;

// Collection "api": БД довідника Guide спільна для всіх фабрик (шлях читається до конфігурації хоста), тому класи з хостом виконуються послідовно
[Collection("api")]
public class EhealthOutboxApiTests : IClassFixture<EhealthMockFactory>
{
    private readonly EhealthMockFactory _f;
    public EhealthOutboxApiTests(EhealthMockFactory f) => _f = f;

    private sealed record Ctx(string OrderId, string Number, string SrId, HttpClient Doc, JsonNode Cert);

    /// <summary>Нове е-направлення в реєстрі мок-ЕСОЗ + видане замовлення за ним + лікар із входом L2.</summary>
    private async Task<Ctx> NewAsync()
    {
        var number = "E-" + Guid.NewGuid().ToString("N")[..8];
        var sr = _f.Mock().Register(number, Patient, "LAB-BIOCHEM");
        var orderId = await _f.ReleasedOrderAsync(_f.As, number);
        var doc = _f.As(DemoDataSeeder.Doctor);
        return new Ctx(orderId, number, sr.Id, doc, await LoginAsync(doc));
    }

    private static async Task<HttpResponseMessage> Prepare(Ctx c) => await c.Doc.PostAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/prepare", null);

    private async Task<JsonNode> PrepareOk(Ctx c) => await Json(await Prepare(c));

    private static async Task<JsonNode> SubmitOk(Ctx c)
    {
        var signed = await Json(await c.Doc.PostAsJsonAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/mock-sign", new { kind = "submit" }));
        return await Json(await c.Doc.PostAsJsonAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/submit", new { signedData = S(signed["signedData"]), signer = c.Cert }));
    }

    private static async Task<JsonNode> Status(Ctx c) => await Json(await c.Doc.GetAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/status"));

    private Task<List<EheOutbox>> Rows(string orderId) => _f.Db(db => db.EhealthOutbox.AsNoTracking().Where(x => x.OrderId == orderId).OrderBy(x => x.CreatedOn).ThenBy(x => x.Kind).ToListAsync());

    private static string ReportId(JsonNode prepared) => S(prepared["submission"]!["package"]!["diagnostic_report"]!["id"]);

    private async Task<int> PostsSince(int mark) => await Task.FromResult(_f.Mock().Calls.Skip(mark).Count(x => x.StartsWith("POST") && x.EndsWith("/diagnostic_report_package")));

    private int Mark() => _f.Mock().Calls.Count;

    // ------------------------------------------------------------------ повний цикл і контракт пакета
    [Fact]
    public async Task Full_cycle_registers_report_then_completes_referral_with_a_separate_task()
    {
        var c = await NewAsync();
        var prepared = await PrepareOk(c);
        var pkg = JsonNode.Parse(prepared["submission"]!["package"]!.ToJsonString())!;
        Assert.Empty(EhealthPackageContract.Validate(pkg));
        Assert.True(Uuid1.IsV1(ReportId(prepared)));
        Assert.All(pkg["observations"]!.AsArray(), o => Assert.True(Uuid1.IsV1(S(o!["id"]))));

        // порушення контракту ловляться до відправки (кожне окремо)
        JsonNode Mutate(Action<JsonNode> change) { var m = JsonNode.Parse(pkg.ToJsonString())!; change(m); return m; }
        Assert.Contains(EhealthPackageContract.Validate(Mutate(m => m["diagnostic_report"]!["id"] = Guid.NewGuid().ToString())), e => e.Contains("UUID версії 1"));
        Assert.Contains(EhealthPackageContract.Validate(Mutate(m => m["diagnostic_report"]!.AsObject().Remove("recorded_by"))), e => e.Contains("recorded_by"));
        Assert.Contains(EhealthPackageContract.Validate(Mutate(m => m["observations"]![0]!.AsObject().Remove("code"))), e => e.Contains("observations[0].code"));
        Assert.Contains(EhealthPackageContract.Validate(Mutate(m => m["diagnostic_report"]!["paper_referral"] = new JsonObject())), e => e.Contains("взаємовиключні"));

        // погашення до реєстрації звіту двійник відхиляє (це і був помилковий порядок)
        using (var scope = _f.Services.CreateScope())
        {
            var client = scope.ServiceProvider.GetRequiredService<IEhealthClient>();
            var ex = await Assert.ThrowsAsync<EhealthException>(() => client.CompleteServiceRequestAsync(c.SrId, "diagnostic_report", ReportId(prepared)));
            Assert.Equal(EhealthErrorKind.Validation, ex.Kind);
        }

        var mark = Mark();
        var queued = await SubmitOk(c);
        Assert.Equal(EhealthSubmitStatuses.Processing, S(queued["ehealthStatus"]));
        var first = Assert.Single(await Rows(c.OrderId));
        Assert.Equal((EhealthOutboxKinds.SubmitDr, EhealthOutboxStatuses.New), (first.Kind, first.Status));
        Assert.StartsWith("SUBMIT_DR:", first.IdempotencyKey);
        Assert.DoesNotContain(_f.Mock().Calls.Skip(mark), x => x.Contains("/actions/complete")); // до відправки пакета погашення немає

        await _f.Pump();
        var rows = await Rows(c.OrderId);
        Assert.Equal(2, rows.Count);
        var submit = rows.Single(r => r.Kind == EhealthOutboxKinds.SubmitDr);
        var complete = rows.Single(r => r.Kind == EhealthOutboxKinds.CompleteSr);
        Assert.All(rows, r => Assert.Equal(EhealthOutboxStatuses.Done, r.Status));
        Assert.Equal($"COMPLETE_SR:{c.SrId}:{ReportId(prepared)}", complete.IdempotencyKey);
        Assert.NotNull(submit.JobId);
        Assert.Equal(EhealthSubmitStatuses.Completed, S((await Status(c))["status"]));

        var calls = _f.Mock().Calls.Skip(mark).ToList();
        var iPost = calls.FindIndex(x => x.StartsWith("POST") && x.EndsWith("/diagnostic_report_package"));
        var iJob = calls.FindIndex(x => x.StartsWith("GET") && x.Contains("/jobs/"));
        var iDone = calls.FindIndex(x => x.StartsWith("PATCH") && x.EndsWith($"/service_requests/{c.SrId}/actions/complete"));
        Assert.True(iPost >= 0 && iJob > iPost && iDone > iJob, string.Join(" | ", calls));
        Assert.Single(calls, x => x.EndsWith("/actions/complete"));
        Assert.True(_f.Mock().Reports[ReportId(prepared)].Registered);
        Assert.Equal("completed", _f.Mock().ServiceRequests[c.SrId].Status);
        Assert.Equal(ReportId(prepared), _f.Mock().ServiceRequests[c.SrId].CompletedWithId);
    }

    // ------------------------------------------------------------------ відхилений job → новий пакет
    [Fact]
    public async Task Rejected_job_stops_the_task_and_a_new_package_with_new_ids_is_submitted()
    {
        var c = await NewAsync();
        _f.Mock().QueueJobFailure("submit", 422, "Validation failed", new[] { "$.diagnostic_report.issued: Issued date must be in the past" });
        var p1 = await PrepareOk(c);
        await SubmitOk(c);
        await _f.Pump();

        var st = await Status(c);
        Assert.Equal(EhealthSubmitStatuses.Rejected, S(st["status"]));
        var failed = Assert.Single(await Rows(c.OrderId));
        Assert.Equal(EhealthOutboxStatuses.Failed, failed.Status);
        Assert.Equal(EhealthOutboxErrors.JobFailed, failed.ErrorCode);
        Assert.Contains("Issued date must be in the past", failed.LastError);
        Assert.True(EhealthOutboxService.CanRetry(failed));
        Assert.False(_f.Mock().Reports.TryGetValue(ReportId(p1), out var r1) && r1.Registered);
        Assert.DoesNotContain(_f.Mock().Calls, x => x.EndsWith($"/service_requests/{c.SrId}/actions/complete")); // направлення не погашено

        var p2 = await PrepareOk(c);
        Assert.NotEqual(ReportId(p1), ReportId(p2));
        Assert.NotEqual(S(p1["submission"]!["packageHash"]), S(p2["submission"]!["packageHash"]));
        await SubmitOk(c);
        await _f.Pump();

        Assert.Equal(EhealthSubmitStatuses.Completed, S((await Status(c))["status"]));
        var rows = await Rows(c.OrderId);
        Assert.Equal(2, rows.Count(r => r.Kind == EhealthOutboxKinds.SubmitDr));
        Assert.Equal(EhealthOutboxStatuses.Failed, rows.First(r => r.Kind == EhealthOutboxKinds.SubmitDr).Status); // історія спроби збережена
        Assert.True(_f.Mock().Reports[ReportId(p2)].Registered);
        Assert.Equal(ReportId(p2), _f.Mock().ServiceRequests[c.SrId].CompletedWithId);
    }

    // ------------------------------------------------------------------ ідемпотентність
    [Fact]
    public async Task Resubmit_does_not_create_a_second_task_and_a_lost_response_is_detected_by_get_report()
    {
        var c = await NewAsync();
        await PrepareOk(c);
        await SubmitOk(c);
        // повторна відправка того самого пакета — 409; повторна постановка з тим самим ключем повертає той самий запис
        var again = await c.Doc.PostAsJsonAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/submit", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var row = Assert.Single(await Rows(c.OrderId));
        using (var scope = _f.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<EhealthOutboxService>();
            var same = await svc.EnqueueAsync(row.Kind, row.EntityId, row.OrderId, row.IdempotencyKey, new { x = 1 });
            await scope.ServiceProvider.GetRequiredService<MedLink.LIS.Api.Data.LisDbContext>().SaveChangesAsync();
            Assert.Equal(row.Id, same.Id);
        }
        Assert.Single(await Rows(c.OrderId));
        await _f.Pump();

        // перша спроба втрачає відповідь (ЕСОЗ прийняла пакет, мережа обірвалась): повтор не шле пакет вдруге
        var c2 = await NewAsync();
        await PrepareOk(c2);
        _f.Mock().QueueFailure("POST", "diagnostic_report_package", 503, "ЕСОЗ не відповідає");
        var mark = Mark();
        await SubmitOk(c2);
        await _f.Pump(1);
        var pending = Assert.Single(await Rows(c2.OrderId));
        Assert.Equal((EhealthOutboxStatuses.New, 1), (pending.Status, pending.Attempts));
        var signedData = await _f.Db(db => db.EhealthSubmissions.Where(s => s.OrderId == c2.OrderId).Select(s => s.SignedData).FirstAsync());
        using (var scope = _f.Services.CreateScope())
        {
            var client = scope.ServiceProvider.GetRequiredService<IEhealthClient>();
            var job = await client.SubmitDiagnosticReportPackageAsync(Patient, signedData!); // «втрачена» відправка
            Assert.True((await client.WaitForJobAsync(job)).IsProcessed);
        }
        await _f.Pump();
        Assert.Equal(EhealthSubmitStatuses.Completed, S((await Status(c2))["status"]));
        Assert.Equal(2, await PostsSince(mark)); // 503 + «втрачена»; без третього POST
    }

    // ------------------------------------------------------------------ недопустимі переходи
    [Fact]
    public async Task Invalid_state_transitions_are_conflicts_and_roles_are_checked()
    {
        var c = await NewAsync();
        // анулювати і відправити без підготовки/реєстрації не можна
        Assert.Equal(HttpStatusCode.Conflict, (await c.Doc.PostAsJsonAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/annul/prepare", new { reason = "Помилка" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Doc.PostAsJsonAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/submit", new { signedData = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Doc.PostAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/retry", null)).StatusCode); // немає зупиненої задачі

        await PrepareOk(c);
        await SubmitOk(c);
        var row = Assert.Single(await Rows(c.OrderId));
        Assert.Equal(HttpStatusCode.Conflict, (await c.Doc.PostAsync($"/api/v1/lab/ehealth/outbox/{row.Id}/retry", null)).StatusCode); // NEW не повторюють
        await _f.Pump(1);
        Assert.Equal(EhealthOutboxStatuses.WaitingJob, (await Rows(c.OrderId)).Single(r => r.Id == row.Id).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Doc.PostAsync($"/api/v1/lab/ehealth/outbox/{row.Id}/cancel", null)).StatusCode); // уже передано в ЕСОЗ
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.As(DemoDataSeeder.Registrar).PostAsync($"/api/v1/lab/ehealth/outbox/{row.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Doc.PostAsync($"/api/v1/lab/ehealth/outbox/{Guid.NewGuid()}/retry", null)).StatusCode);

        await _f.Pump();
        foreach (var r in await Rows(c.OrderId))
        {
            Assert.Equal(EhealthOutboxStatuses.Done, r.Status);
            Assert.Equal(HttpStatusCode.Conflict, (await c.Doc.PostAsync($"/api/v1/lab/ehealth/outbox/{r.Id}/retry", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await c.Doc.PostAsync($"/api/v1/lab/ehealth/outbox/{r.Id}/cancel", null)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await c.Doc.PostAsJsonAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/submit", new { })).StatusCode); // уже погашено
    }

    // ------------------------------------------------------------------ витримки, DEAD, ручний повтор
    [Fact]
    public async Task Transient_errors_back_off_exponentially_then_die_and_manual_retry_recovers()
    {
        var c = await NewAsync();
        var max = _f.Services.GetRequiredService<EhealthOptions>().Outbox;
        _f.Mock().QueueFailure("POST", "diagnostic_report_package", 503, "ЕСОЗ не відповідає", times: max.MaxAttempts);
        await PrepareOk(c);
        await SubmitOk(c);
        var t = DateTime.UtcNow;
        for (var n = 1; n <= max.MaxAttempts; n++)
        {
            t = t.AddMinutes(31); // більше за максимальну витримку (30 хв), але в межах доби життя задачі
            await _f.Engine().RunDueAsync(t);
            var row = Assert.Single(await Rows(c.OrderId));
            Assert.Equal(n, row.Attempts);
            Assert.Equal("TRANSIENT", row.ErrorCode);
            Assert.Contains("503", row.LastError);
            if (n < max.MaxAttempts)
            {
                Assert.Equal(EhealthOutboxStatuses.New, row.Status);
                var expected = t + EhealthBackoff.RetryDelay(n, max);
                Assert.True(Math.Abs((row.NextAttemptAt!.Value - expected).TotalSeconds) < 1, $"спроба {n}: очікувалась пауза {EhealthBackoff.RetryDelay(n, max)}");
            }
            else
            {
                Assert.Equal(EhealthOutboxStatuses.Dead, row.Status);
                Assert.Null(row.NextAttemptAt);
            }
        }
        Assert.Equal(EhealthSubmitStatuses.Error, S((await Status(c))["status"]));
        Assert.True((await Status(c))["canRetry"]!.GetValue<bool>());

        // ручний повтор (кнопка «Повторити») відновлює ту саму задачу; ЕСОЗ вже відповідає
        var retried = await Json(await c.Doc.PostAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/retry", null));
        Assert.Equal(EhealthSubmitStatuses.Processing, S(retried["submission"]!["status"]));
        await _f.Pump();
        Assert.Equal(EhealthSubmitStatuses.Completed, S((await Status(c))["status"]));
        Assert.Equal(2, (await Rows(c.OrderId)).Count); // той самий SUBMIT_DR + COMPLETE_SR
    }

    // ------------------------------------------------------------------ збій воркера
    [Fact]
    public async Task Stale_sending_rows_are_recovered_and_a_known_job_is_not_resubmitted()
    {
        var c = await NewAsync();
        await PrepareOk(c);
        await SubmitOk(c);
        var id = (await Rows(c.OrderId)).Single().Id;
        Task Crash(DateTime lockedUntil) => _f.Db(async db => await db.EhealthOutbox.Where(x => x.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, EhealthOutboxStatuses.Sending).SetProperty(x => x.LockedBy, "dead-worker").SetProperty(x => x.LockedUntil, (DateTime?)lockedUntil)));

        // запис «у роботі» з ще дійсним замком не чіпаємо
        await Crash(DateTime.UtcNow.AddHours(5));
        await _f.Engine().RecoverStuckAsync(DateTime.UtcNow);
        Assert.Equal(EhealthOutboxStatuses.Sending, (await Rows(c.OrderId)).Single().Status);

        // воркер упав до відправки (замок прострочено): запис повертається в чергу
        await Crash(DateTime.UtcNow.AddMinutes(-1));
        Assert.True(await _f.Engine().RecoverStuckAsync(_f.Now()) >= 1);
        var r = (await Rows(c.OrderId)).Single();
        Assert.Equal((EhealthOutboxStatuses.New, EhealthOutboxErrors.Recovered), (r.Status, r.ErrorCode));
        Assert.Null(r.LockedBy);

        // воркер упав після відправки (job відомий): опитування job, а не повторний POST
        var mark = Mark();
        await _f.Pump(1);
        Assert.Equal(1, await PostsSince(mark));
        var waiting = (await Rows(c.OrderId)).Single();
        Assert.Equal(EhealthOutboxStatuses.WaitingJob, waiting.Status);
        Assert.NotNull(waiting.JobId);
        await Crash(DateTime.UtcNow.AddMinutes(-1));
        Assert.True(await _f.Engine().RecoverStuckAsync(_f.Now()) >= 1);
        Assert.Equal(EhealthOutboxStatuses.WaitingJob, (await Rows(c.OrderId)).Single().Status);
        await _f.Pump();
        Assert.Equal(1, await PostsSince(mark));
        Assert.Equal(EhealthSubmitStatuses.Completed, S((await Status(c))["status"]));
    }

    [Fact]
    public async Task Only_one_worker_can_claim_a_row_and_parallel_passes_send_once()
    {
        var c = await NewAsync();
        await PrepareOk(c);
        await SubmitOk(c);
        var id = (await Rows(c.OrderId)).Single().Id;
        var now = _f.Now().AddMinutes(1);
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
        {
            using var scope = _f.Services.CreateScope();
            return await _f.Engine().TryClaimAsync(scope.ServiceProvider.GetRequiredService<MedLink.LIS.Api.Data.LisDbContext>(), id, now, "w" + i);
        })));
        Assert.Single(claims, x => x);
        Assert.Equal(EhealthOutboxStatuses.Sending, (await Rows(c.OrderId)).Single().Status);
        Assert.True(await _f.Engine().RecoverStuckAsync(now.AddHours(1)) >= 1); // замок знято, запис знову у черзі

        var mark = Mark();
        var t = _f.Now().AddMinutes(65); // пізніше за NextAttemptAt, який виставило відновлення (now + 1 год)
        await Task.WhenAll(_f.Engine().RunDueAsync(t), _f.Engine().RunDueAsync(t), _f.Engine().RunDueAsync(t));
        Assert.Equal(1, await PostsSince(mark));
        await _f.Pump(40);
        Assert.Equal(EhealthSubmitStatuses.Completed, S((await Status(c))["status"]));
    }

    // ------------------------------------------------------------------ скасування користувачем
    [Fact]
    public async Task User_can_cancel_a_queued_task_and_restore_it_with_retry()
    {
        var c = await NewAsync();
        await PrepareOk(c);
        _f.Mock().QueueFailure("POST", "diagnostic_report_package", 503, "ЕСОЗ не відповідає");
        var mark = Mark();
        await SubmitOk(c);
        await _f.Pump(1);
        var row = Assert.Single(await Rows(c.OrderId));
        var dto = (await Status(c))["outbox"]![0]!;
        Assert.True(dto["canCancel"]!.GetValue<bool>());
        Assert.False(dto["canRetry"]!.GetValue<bool>());

        var cancelled = await Json(await c.Doc.PostAsync($"/api/v1/lab/ehealth/outbox/{row.Id}/cancel", null));
        Assert.Equal(EhealthOutboxStatuses.Dead, S(cancelled["status"]));
        Assert.Equal(EhealthOutboxErrors.CancelledByUser, S(cancelled["errorCode"]));
        Assert.False(cancelled["canCancel"]!.GetValue<bool>());
        Assert.True(cancelled["canRetry"]!.GetValue<bool>());
        Assert.Equal(EhealthSubmitStatuses.Signed, S((await Status(c))["status"])); // підписаний пакет збережено
        await _f.Pump();
        Assert.Equal(1, await PostsSince(mark)); // скасована задача більше нічого не шле
        Assert.Equal(HttpStatusCode.Conflict, (await c.Doc.PostAsync($"/api/v1/lab/ehealth/outbox/{row.Id}/cancel", null)).StatusCode);

        await Json(await c.Doc.PostAsync($"/api/v1/lab/ehealth/outbox/{row.Id}/retry", null));
        await _f.Pump();
        Assert.Equal(EhealthSubmitStatuses.Completed, S((await Status(c))["status"]));
    }

    // ------------------------------------------------------------------ збій погашення → анулювання → новий пакет
    [Fact]
    public async Task Failed_completion_is_fixed_by_cancelling_the_report_and_submitting_a_new_package()
    {
        var c = await NewAsync();
        _f.Mock().QueueJobFailure("complete", 409, "Service request is not active");
        var p1 = await PrepareOk(c);
        await SubmitOk(c);
        await _f.Pump();

        var rows = await Rows(c.OrderId);
        Assert.Equal(EhealthOutboxStatuses.Done, rows.Single(r => r.Kind == EhealthOutboxKinds.SubmitDr).Status);
        var complete = rows.Single(r => r.Kind == EhealthOutboxKinds.CompleteSr);
        Assert.Equal((EhealthOutboxStatuses.Failed, EhealthOutboxErrors.JobFailed), (complete.Status, complete.ErrorCode));
        var st = await Status(c);
        Assert.Equal(EhealthSubmitStatuses.Registered, S(st["status"])); // звіт є в ЕСОЗ, направлення не погашено
        Assert.True(st["canAnnul"]!.GetValue<bool>());
        Assert.Equal("active", _f.Mock().ServiceRequests[c.SrId].Status);

        // анулювання: пакет скасування (entered_in_error) → тестовий підпис → задача CANCEL_DR
        var cancelPkg = await Json(await c.Doc.PostAsJsonAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/annul/prepare", new { reason = "Помилка в одиницях" }));
        Assert.Equal("entered_in_error", S(cancelPkg["package"]!["diagnostic_report"]!["status"]));
        Assert.Empty(EhealthPackageContract.Validate(JsonNode.Parse(cancelPkg["package"]!.ToJsonString()), cancel: true));
        var sig = await Json(await c.Doc.PostAsJsonAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/mock-sign", new { kind = "annul", reason = "Помилка в одиницях" }));
        await Json(await c.Doc.PostAsJsonAsync($"/api/v1/lab/orders/{c.OrderId}/ehealth/annul", new { signedData = S(sig["signedData"]), signer = c.Cert, reason = "Помилка в одиницях" }));
        await _f.Pump();

        rows = await Rows(c.OrderId);
        Assert.Equal(EhealthOutboxStatuses.Done, rows.Single(r => r.Kind == EhealthOutboxKinds.CancelDr).Status);
        Assert.Equal(EhealthOutboxStatuses.Dead, rows.Single(r => r.Kind == EhealthOutboxKinds.CompleteSr).Status); // застаріле погашення знято
        Assert.Equal(EhealthSubmitStatuses.Annulled, S((await Status(c))["status"]));
        Assert.Equal("entered_in_error", _f.Mock().Reports[ReportId(p1)].Status);

        // новий пакет: нові ідентифікатори звіту й спостережень → відправка → погашення
        var p2 = await PrepareOk(c);
        Assert.NotEqual(ReportId(p1), ReportId(p2));
        var oldObs = p1["submission"]!["package"]!["observations"]!.AsArray().Select(o => S(o!["id"])).ToHashSet();
        Assert.All(p2["submission"]!["package"]!["observations"]!.AsArray(), o => Assert.DoesNotContain(S(o!["id"]), oldObs));
        await SubmitOk(c);
        await _f.Pump();
        Assert.Equal(EhealthSubmitStatuses.Completed, S((await Status(c))["status"]));
        Assert.Equal(ReportId(p2), _f.Mock().ServiceRequests[c.SrId].CompletedWithId);
    }

    // ------------------------------------------------------------------ перелік (грід «Відправка в ЕСОЗ»)
    [Fact]
    public async Task Submissions_list_filters_by_status_date_and_referral_number()
    {
        var done = await NewAsync();
        await PrepareOk(done); await SubmitOk(done);
        var stopped = await NewAsync();
        await PrepareOk(stopped); await SubmitOk(stopped);
        var stoppedRow = (await Rows(stopped.OrderId)).Single();
        await Json(await stopped.Doc.PostAsync($"/api/v1/lab/ehealth/outbox/{stoppedRow.Id}/cancel", null));
        await _f.Pump();

        var doc = done.Doc;
        async Task<JsonArray> List(string query) => (await Json(await doc.GetAsync("/api/v1/lab/ehealth/submissions" + query)))["items"]!.AsArray();

        var byNumber = await List($"?search={done.Number}");
        Assert.Equal(new[] { "COMPLETE_SR", "SUBMIT_DR" }, byNumber.Select(i => S(i!["kind"])).OrderBy(x => x).ToArray());
        Assert.All(byNumber, i => { Assert.Equal(done.Number, S(i!["referralNumber"])); Assert.False(i["canRetry"]!.GetValue<bool>()); Assert.False(i["canCancel"]!.GetValue<bool>()); });

        var dead = Assert.Single(await List($"?status=DEAD&search={stopped.Number}"));
        Assert.Equal("SUBMIT_DR", S(dead!["kind"]));
        Assert.Equal(EhealthOutboxErrors.CancelledByUser, S(dead["errorCode"]));
        Assert.True(dead["canRetry"]!.GetValue<bool>());
        Assert.False(dead["canCancel"]!.GetValue<bool>());
        Assert.Empty(await List($"?status=FAILED&search={stopped.Number}"));
        Assert.Empty(await List($"?status=DONE&search={stopped.Number}"));
        Assert.Single(await List($"?kind=COMPLETE_SR&search={done.Number}"));

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        Assert.NotEmpty(await List($"?from={today}&to={today}&search={done.Number}"));
        Assert.Empty(await List($"?from={DateTime.UtcNow.AddDays(2):yyyy-MM-dd}&search={done.Number}"));
        Assert.Empty(await List($"?to={DateTime.UtcNow.AddDays(-2):yyyy-MM-dd}&search={done.Number}"));

        var whole = await Json(await doc.GetAsync("/api/v1/lab/ehealth/submissions"));
        Assert.Equal("MOCK", S(whole["mode"]));
        Assert.True(whole["summary"]!.AsObject().ContainsKey("DONE"));
        Assert.True((await Json(await doc.PostAsync("/api/v1/lab/ehealth/outbox/run", null)))["processed"]!.GetValue<int>() >= 0);

        // відновлення скасованої задачі
        await Json(await stopped.Doc.PostAsync($"/api/v1/lab/ehealth/outbox/{stoppedRow.Id}/retry", null));
        await _f.Pump();
        Assert.Equal(EhealthSubmitStatuses.Completed, S((await Status(stopped))["status"]));
    }
}
