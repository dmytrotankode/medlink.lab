// FR-REF-001: типи направлень; життєвий цикл е-направлення (перевірка → в роботі → погашено висновком / звільнено при скасуванні);
// паперове направлення в ehe_paper_medical_referral; журнал направлень
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
public class ReferralApiTests
{
    private const string Patient = "0b000000-0000-0000-0000-000000000006";
    private readonly LisApiFactory _f;
    public ReferralApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static string S(JsonNode? n) => n!.GetValue<string>();

    private async Task<T> Db<T>(Func<LisDbContext, Task<T>> f)
    {
        using var scope = _f.Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<LisDbContext>());
    }

    private Task<string> NewReferralAsync(string number, string? patientId = Patient, DateTime? expires = null) => Db(async db =>
    {
        var r = new EheIncomingMedicalReferral
        {
            RegNumber = number, Caption = "Біохімічний аналіз крові", OrganizationId = MedLinkDefaults.OrganizationId, PatientCardId = patientId,
            ServiceCatalogServiceId = await db.ServiceCatalog.Where(s => s.Code == "LAB-BIOCHEM").Select(s => s.Id).FirstAsync(),
            MedicalReferralCategoryId = MedLinkDefaults.ReferralCategoryLaboratoryId, StatusId = MedLinkEnums.ReferralStatusActive,
            ProcessingStatusInEhealthId = MedLinkEnums.ProcessingNew, PriorityId = MedLinkEnums.EmptyGuid, ExpirationDate = expires ?? DateTime.UtcNow.AddDays(30)
        };
        db.Referrals.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    });

    [Fact]
    public async Task Lookup_reports_eligibility_and_matching_profiles()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var number = "T-" + Guid.NewGuid().ToString("N")[..8];
        await NewReferralAsync(number);
        var ok = await Json(await reg.GetAsync($"/api/v1/lab/referrals/ehealth/lookup?number={number}&patientId={Patient}"));
        Assert.True(ok["canUse"]!.GetValue<bool>());
        Assert.Contains(ok["matchingProfiles"]!.AsArray(), p => S(p!["code"]) == "PROF_BIOCHEM_BASE");
        var other = await Json(await reg.GetAsync($"/api/v1/lab/referrals/ehealth/lookup?number={number}&patientId=0b000000-0000-0000-0000-000000000001"));
        Assert.False(other["canUse"]!.GetValue<bool>());
        Assert.Contains(other["problems"]!.AsArray(), p => S(p).Contains("іншому пацієнту"));

        var expired = "T-" + Guid.NewGuid().ToString("N")[..8];
        await NewReferralAsync(expired, expires: DateTime.UtcNow.AddDays(-2));
        var exp = await Json(await reg.GetAsync($"/api/v1/lab/referrals/ehealth/lookup?number={expired}"));
        Assert.Contains(exp["problems"]!.AsArray(), p => S(p).Contains("Термін дії"));
        Assert.Equal(HttpStatusCode.NotFound, (await reg.GetAsync("/api/v1/lab/referrals/ehealth/lookup?number=NO-SUCH")).StatusCode);
    }

    [Fact]
    public async Task Ehealth_referral_is_taken_released_and_completed_by_diagnostic_report()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var tech = _f.As(DemoDataSeeder.Technician);
        var doctor = _f.As(DemoDataSeeder.Doctor);
        var number = "T-" + Guid.NewGuid().ToString("N")[..8];
        var refId = await NewReferralAsync(number);

        // Взяти в роботу
        var o1 = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, ehealthReferralNumber = number, profileIds = new[] { "PROF_BIOCHEM_BASE" } }));
        Assert.Equal("EHEALTH", S(o1["referralType"]));
        Assert.Equal(number, S(o1["referralNumber"]));
        Assert.Equal(MedLinkEnums.ProcessingInProgress, await Db(db => db.Referrals.Where(r => r.Id == refId).Select(r => r.ProcessingStatusInEhealthId).FirstAsync()));
        // Повторне використання — 409
        var dup = await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, ehealthReferralNumber = number, testIds = new[] { "GLU" } });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        // Скасування замовлення звільняє направлення
        await Json(await reg.PostAsJsonAsync($"/api/v1/lab/orders/{S(o1["id"])}/cancel", new { reason = "Помилка реєстрації" }));
        Assert.Equal(MedLinkEnums.ProcessingNew, await Db(db => db.Referrals.Where(r => r.Id == refId).Select(r => r.ProcessingStatusInEhealthId).FirstAsync()));

        // Нове замовлення → результат → видача → погашення висновком
        var o2 = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, ehealthReferralId = refId, testIds = new[] { "GLU" } }));
        var orderId = S(o2["id"]);
        var barcode = S(o2["samples"]![0]!["barcode"]);
        await Json(await tech.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/collect", new { volumeMl = 4.0 }));
        await Json(await tech.PostAsJsonAsync($"/api/v1/lab/samples/{barcode}/receive", new { }));
        var testId = S(o2["tests"]![0]!["id"]);
        var row = await Json(await tech.PutAsJsonAsync($"/api/v1/lab/results/{testId}", new { numericValue = 5.0 }));
        if (S(row["status"]) != "AUTO_VERIFIED") await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/results/{testId}/verify", new { }));
        await Json(await doctor.PostAsJsonAsync($"/api/v1/lab/orders/{orderId}/release", new { }));
        var (r, reportId) = await Db(async db => (await db.Referrals.AsNoTracking().FirstAsync(x => x.Id == refId), await db.Orders.Where(o => o.Id == orderId).Select(o => o.DiagnosticReportId).FirstAsync()));
        Assert.Equal(MedLinkEnums.ReferralStatusCompleted, r.StatusId);
        Assert.Equal(MedLinkEnums.ProcessingCompleted, r.ProcessingStatusInEhealthId);
        Assert.Equal(reportId, r.CompletedWithItemEntityId);
        Assert.Equal("DiagnosticReport", r.CompletedWithItemEntityName);
        Assert.NotNull(r.CompleteDateInEhealth);
        var report = await Db(db => db.DiagnosticReports.AsNoTracking().FirstAsync(x => x.Id == reportId));
        Assert.Equal(refId, report.EhealthIncomingMedicalReferralId);
        // Реальні виклики API ЕСОЗ (обслуговані моком) — у журналі обміну
        var log = (await Json(await doctor.GetAsync("/api/v1/lab/ehealth/exchange-log?take=500")))["items"]!.AsArray();
        Assert.True(log.Count > 0, "exchange log empty");
        string[] Urls(string method) => log.Where(l => S(l!["method"]) == method).Select(l => S(l!["url"])).ToArray();
        Assert.Contains(Urls("PATCH"), u => u == $"patients/service_requests/{refId}/actions/use");
        Assert.Contains(Urls("PATCH"), u => u == $"patients/service_requests/{refId}/actions/release");
        Assert.Contains(Urls("PATCH"), u => u == $"patients/service_requests/{refId}/actions/complete");
        Assert.Contains(Urls("GET"), u => u.StartsWith($"patients/service_requests?requisition={number}"));
        var complete = log.First(l => S(l!["url"]) == $"patients/service_requests/{refId}/actions/complete")!;
        Assert.Contains("\"completed_with\"", S(complete["requestJson"]));
        Assert.Contains(reportId!, S(complete["requestJson"]));
        Assert.Equal(202, complete["statusCode"]!.GetValue<int>());
        // Погашене направлення використати вже не можна
        var again = await Json(await reg.GetAsync($"/api/v1/lab/referrals/ehealth/lookup?number={number}"));
        Assert.False(again["canUse"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Paper_internal_external_and_self_referrals_and_journal()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var paperNo = "П-" + Guid.NewGuid().ToString("N")[..6];
        var paper = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new
        {
            patientId = Patient, testIds = new[] { "GLU" },
            paperReferral = new { number = paperNo, date = "2026-10-01", requesterEmployeeName = "Петренко І.І.", requesterLegalEntityName = "КНП «ЦПМСД №5»", requesterLegalEntityEdrpou = "01234567" }
        }));
        Assert.Equal("PAPER", S(paper["referralType"]));
        Assert.Equal(paperNo, S(paper["referralNumber"]));
        Assert.Equal("КНП «ЦПМСД №5»", S(paper["referrerOrganizationName"]));
        var pr = await Db(db => db.PaperReferrals.AsNoTracking().FirstAsync(p => p.RegNumber == paperNo));
        Assert.Equal(PaperReferralStatuses.Active, pr.Status);
        Assert.Equal(Patient, pr.PatientCardId);

        var internalOrder = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, doctorId = DemoDataSeeder.Doctor, testIds = new[] { "GLU" } }));
        Assert.Equal("INTERNAL", S(internalOrder["referralType"]));
        var ext = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, referralType = "EXTERNAL_CLINIC", referrerOrganizationName = "Клініка «Здоров'я»", referrerDoctorName = "Іваненко О.П.", referrerNumber = "ZD-778", testIds = new[] { "GLU" } }));
        Assert.Equal("EXTERNAL_CLINIC", S(ext["referralType"]));
        Assert.Equal("ZD-778", S(ext["referralNumber"]));
        var self = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, testIds = new[] { "GLU" } }));
        Assert.Equal("SELF", S(self["referralType"]));

        Assert.Equal(HttpStatusCode.BadRequest, (await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, referralType = "INTERNAL", testIds = new[] { "GLU" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = Patient, referralType = "EXTERNAL_CLINIC", testIds = new[] { "GLU" } })).StatusCode);

        var journal = await Json(await reg.GetAsync($"/api/v1/lab/referrals/journal?search={Uri.EscapeDataString(paperNo)}"));
        var item = Assert.Single(journal["items"]!.AsArray());
        Assert.Equal("Паперове направлення", S(item!["referralTypeName"]));
        var all = await Json(await reg.GetAsync("/api/v1/lab/referrals/journal"));
        foreach (var t in new[] { "PAPER", "INTERNAL", "EXTERNAL_CLINIC", "SELF" })
            Assert.True(all["summary"]!.AsArray().First(s => S(s!["type"]) == t)!["count"]!.GetValue<int>() >= 1, t);
    }
}
