// FR-GAP-060: каса з пакетом і оплатами; страхова програма з полісом, частковим покриттям і непокритими позиціями;
// гарантійний лист і франшиза; вибір пацієнта; НСЗУ з е-направлення + платні позиції в одному замовленні; рахунок і друк
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
public class BillingApiTests
{
    private readonly LisApiFactory _f;
    public BillingApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static decimal D(JsonNode? n) => n!.GetValue<decimal>();
    private static string S(JsonNode? n) => n!.GetValue<string>();

    [Fact]
    public async Task Cash_order_applies_retail_package_and_takes_payments()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var order = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = "0b000000-0000-0000-0000-000000000005", profileIds = new[] { "PROF_CBC", "PROF_BIOCHEM_BASE", "PROF_URINE" } }));
        var id = S(order["id"]);
        var bill = await Json(await reg.GetAsync($"/api/v1/lab/orders/{id}/billing"));
        Assert.Equal("PATIENT", S(bill["payerKind"]));
        var line = Assert.Single(bill["charges"]!.AsArray());
        Assert.True(line!["isPackage"]!.GetValue<bool>());
        Assert.Equal(800m, D(bill["patientAmount"]));
        bill = await Json(await reg.PostAsJsonAsync($"/api/v1/lab/orders/{id}/payments", new { amount = 500, method = "CARD" }));
        Assert.Equal(300m, D(bill["due"]));
        Assert.Equal(HttpStatusCode.Conflict, (await reg.PostAsJsonAsync($"/api/v1/lab/orders/{id}/payments", new { amount = 301, method = "CASH" })).StatusCode);
        bill = await Json(await reg.PostAsJsonAsync($"/api/v1/lab/orders/{id}/payments", new { amount = 300, method = "CASH" }));
        Assert.Equal(0m, D(bill["due"]));
        Assert.Matches(@"^Q-\d{4}-\d{5}$", S(bill["payments"]![0]!["receiptNumber"]));
        var html = await (await reg.GetAsync($"/api/v1/lab/orders/{id}/receipt")).Content.ReadAsStringAsync();
        Assert.Contains("Check-up базовий", html);
        Assert.Contains("До сплати", html);
        // Рахунок платнику для каси не виставляється
        Assert.Equal(HttpStatusCode.Conflict, (await reg.PostAsync($"/api/v1/lab/orders/{id}/invoice", null)).StatusCode);
    }

    [Fact]
    public async Task Insurance_program_requires_policy_splits_coverage_and_issues_invoice()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        // Пацієнт без полісу — програма недоступна
        var noPolicy = await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = "0b000000-0000-0000-0000-000000000001", payerId = "UNIQA-GOLD", profileIds = new[] { "PROF_CBC" } });
        Assert.Equal(HttpStatusCode.Conflict, noPolicy.StatusCode);

        var quote = await Json(await reg.PostAsJsonAsync("/api/v1/lab/billing/quote", new { patientId = "0b000000-0000-0000-0000-000000000003", payerId = "UNIQA-GOLD", profileIds = new[] { "PROF_CBC", "PROF_LIPID" }, testIds = new[] { "VITD" } }));
        var order = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = "0b000000-0000-0000-0000-000000000003", payerId = "UNIQA-GOLD", profileIds = new[] { "PROF_CBC", "PROF_LIPID" }, testIds = new[] { "VITD" } }));
        Assert.Equal("UQ-778812", S(order["insurancePolicyNumber"]));
        var id = S(order["id"]);
        var bill = await Json(await reg.GetAsync($"/api/v1/lab/orders/{id}/billing"));
        var cbc = bill["charges"]!.AsArray().First(c => S(c!["code"]) == "PROF_CBC")!;
        Assert.Equal(230m, D(cbc["amount"]));
        Assert.Equal(207m, D(cbc["payerAmount"]));   // 90 %
        Assert.Equal(23m, D(cbc["patientAmount"]));
        var vitd = bill["charges"]!.AsArray().First(c => S(c!["code"]) == "VITD")!;
        Assert.True(vitd["isNotCovered"]!.GetValue<bool>());
        Assert.Equal(D(vitd["amount"]), D(vitd["patientAmount"]));
        Assert.Equal(207m + 315m, D(bill["payerAmount"]));
        Assert.Equal(D(quote["payerAmount"]), D(bill["payerAmount"]));
        Assert.Equal(D(quote["patientAmount"]), D(bill["patientAmount"]));

        var inv = await Json(await reg.PostAsync($"/api/v1/lab/orders/{id}/invoice", null));
        Assert.Equal(522m, D(inv["amount"]));
        var print = await (await reg.GetAsync($"/api/v1/lab/invoices/{S(inv["id"])}/print")).Content.ReadAsStringAsync();
        Assert.Contains("УНІКА", print);
        Assert.Contains("UQ-778812", print);
        // Після рахунку змінити платника не можна
        Assert.Equal(HttpStatusCode.Conflict, (await reg.PutAsJsonAsync($"/api/v1/lab/orders/{id}/payer", new { payerId = "PATIENT" })).StatusCode);
    }

    [Fact]
    public async Task Authorization_franchise_and_patient_choice()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        const string patient = "0b000000-0000-0000-0000-000000000002";
        Assert.Equal(HttpStatusCode.BadRequest, (await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = patient, payerId = "INGO-BASIC", profileIds = new[] { "PROF_THYROID" } })).StatusCode);
        var order = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = patient, payerId = "INGO-BASIC", authorizationNumber = "ГЛ-2026-0915", profileIds = new[] { "PROF_THYROID", "PROF_URINE" } }));
        var id = S(order["id"]);
        var bill = await Json(await reg.GetAsync($"/api/v1/lab/orders/{id}/billing"));
        Assert.Equal(330m + 115m - 150m, D(bill["payerAmount"])); // франшиза 150
        Assert.Equal(150m, D(bill["patientAmount"]));
        var urine = bill["charges"]!.AsArray().First(c => S(c!["code"]) == "PROF_URINE")!;
        bill = await Json(await reg.PutAsync($"/api/v1/lab/orders/{id}/charges/{S(urine["id"])}/patient-choice?patientPays=true", null));
        var urine2 = bill["charges"]!.AsArray().First(c => S(c!["code"]) == "PROF_URINE")!;
        Assert.True(urine2["isPatientChoice"]!.GetValue<bool>());
        Assert.Equal(D(urine2["amount"]), D(urine2["patientAmount"]));
    }

    [Fact]
    public async Task Nszu_referral_order_mixes_free_and_paid_items()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        const string patient = "0b000000-0000-0000-0000-000000000006";
        var number = "NSZU-" + Guid.NewGuid().ToString("N")[..6];
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LisDbContext>();
            var program = await db.Payers.Where(p => p.Code == "NSZU-PMG").Select(p => p.MedicalProgramId).FirstAsync();
            db.Referrals.Add(new EheIncomingMedicalReferral
            {
                RegNumber = number, OrganizationId = MedLinkDefaults.OrganizationId, PatientCardId = patient, MedicalReferralCategoryId = MedLinkDefaults.ReferralCategoryLaboratoryId,
                StatusId = MedLinkEnums.ReferralStatusActive, ProcessingStatusInEhealthId = MedLinkEnums.ProcessingNew, PriorityId = MedLinkEnums.EmptyGuid,
                ExpirationDate = DateTime.UtcNow.AddDays(10), MedicalServiceProgramId = program
            });
            await db.SaveChangesAsync();
        }
        var order = await Json(await reg.PostAsJsonAsync("/api/v1/lab/orders", new { patientId = patient, ehealthReferralNumber = number, profileIds = new[] { "PROF_CBC" }, testIds = new[] { "TSH" } }));
        Assert.Equal("NSZU", S(order["payerKind"]));
        var bill = await Json(await reg.GetAsync($"/api/v1/lab/orders/{S(order["id"])}/billing"));
        var cbc = bill["charges"]!.AsArray().First(c => S(c!["code"]) == "PROF_CBC")!;
        Assert.Equal(0m, D(cbc["patientAmount"]));
        var tsh = bill["charges"]!.AsArray().First(c => S(c!["code"]) == "TSH")!;
        Assert.True(tsh["isNotCovered"]!.GetValue<bool>());
        Assert.True(D(tsh["patientAmount"]) > 0);
        Assert.Equal(D(tsh["patientAmount"]), D(bill["patientAmount"]));
        var receipt = await (await reg.GetAsync($"/api/v1/lab/orders/{S(order["id"])}/receipt")).Content.ReadAsStringAsync();
        Assert.Contains("безоплатно (ПМГ)", receipt);
        Assert.Contains("не покривається програмою", receipt);
    }
}
