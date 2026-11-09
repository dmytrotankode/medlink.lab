// Дублікати пацієнтів (пошук), згоди пацієнтів, реєстр СОП (версії), допуски персоналу
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class QualityRegistryApiTests
{
    private const string Patient = "0b000000-0000-0000-0000-000000000004";
    private readonly LisApiFactory _f;
    public QualityRegistryApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }
    private static string S(JsonNode? n) => n!.GetValue<string>();

    [Fact]
    public async Task Duplicate_search_finds_same_person_by_name_and_birthday_and_requires_criteria()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var card = await Json(await reg.GetAsync($"/api/v1/lab/patients/{Patient}"));
        var name = S(card["caption"] ?? card["fullName"] ?? card["name"]);
        var last = name.Split(' ')[0];
        var first = name.Split(' ').Length > 1 ? name.Split(' ')[1] : "";
        var birthday = (card["birthday"] ?? card["birthDate"])?.GetValue<DateTime>();
        Assert.NotNull(birthday);

        var found = await Json(await reg.GetAsync($"/api/v1/lab/patients/duplicates?lastName={Uri.EscapeDataString(last)}&firstName={Uri.EscapeDataString(first)}&birthday={birthday:yyyy-MM-dd}"));
        Assert.Contains(found["items"]!.AsArray(), i => S(i!["patientId"]) == Patient && i["reasons"]!.AsArray().Any(r => S(r).Contains("ПІБ")));
        var own = await Json(await reg.GetAsync($"/api/v1/lab/patients/{Patient}/duplicates"));
        Assert.DoesNotContain(own["items"]!.AsArray(), i => S(i!["patientId"]) == Patient);
        Assert.Equal(HttpStatusCode.BadRequest, (await reg.GetAsync("/api/v1/lab/patients/duplicates?firstName=Іван")).StatusCode);
        var none = await Json(await reg.GetAsync("/api/v1/lab/patients/duplicates?lastName=Неіснуючий&firstName=Ніхто&birthday=1900-01-01"));
        Assert.Equal(0, none["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Consents_grant_revoke_and_validate()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var initial = await Json(await reg.GetAsync($"/api/v1/lab/patients/{Patient}/consents"));
        Assert.Equal(5, initial["items"]!.AsArray().Count);

        var granted = await Json(await reg.PutAsJsonAsync($"/api/v1/lab/patients/{Patient}/consents", new { kind = "RESULTS_VIBER", isGranted = true, source = "PAPER", note = "Підписано при реєстрації" }));
        Assert.True(granted["isGranted"]!.GetValue<bool>());
        Assert.Null(granted["revokedAt"]);
        var revoked = await Json(await reg.PutAsJsonAsync($"/api/v1/lab/patients/{Patient}/consents", new { kind = "RESULTS_VIBER", isGranted = false }));
        Assert.False(revoked["isGranted"]!.GetValue<bool>());
        Assert.NotNull(revoked["revokedAt"]);
        var after = await Json(await reg.GetAsync($"/api/v1/lab/patients/{Patient}/consents"));
        Assert.True(after["items"]!.AsArray().Single(i => S(i!["kind"]) == "RESULTS_VIBER")["recorded"]!.GetValue<bool>());

        Assert.Equal(HttpStatusCode.BadRequest, (await reg.PutAsJsonAsync($"/api/v1/lab/patients/{Patient}/consents", new { kind = "TELEPATHY", isGranted = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reg.PutAsJsonAsync($"/api/v1/lab/patients/{Patient}/consents", new { kind = "RESULTS_SMS", isGranted = true, source = "FAX" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reg.GetAsync("/api/v1/lab/patients/no-such-patient/consents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.As(DemoDataSeeder.Courier).PutAsJsonAsync($"/api/v1/lab/patients/{Patient}/consents", new { kind = "RESULTS_SMS", isGranted = true })).StatusCode);
    }

    [Fact]
    public async Task Sop_versions_only_one_active_and_roles()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var code = "SOP-" + Guid.NewGuid().ToString("N")[..5];
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.As(DemoDataSeeder.Registrar).PostAsJsonAsync("/api/v1/lab/sops", new { code, title = "Забір крові" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/lab/sops", new { code, title = "" })).StatusCode);
        var v1 = await Json(await admin.PostAsJsonAsync("/api/v1/lab/sops", new { code, title = "Забір крові", version = "1.0", targetKind = "SECTION", targetCode = "BIOCHEM" }));
        Assert.Equal("DRAFT", S(v1["status"]));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/lab/sops", new { code, title = "Дубль", version = "1.0" })).StatusCode);
        await Json(await admin.PostAsJsonAsync($"/api/v1/lab/sops/{S(v1["id"])}/activate", new { }));
        var v2 = await Json(await admin.PostAsJsonAsync("/api/v1/lab/sops", new { code, title = "Забір крові (нова редакція)", version = "2.0" }));
        await Json(await admin.PostAsJsonAsync($"/api/v1/lab/sops/{S(v2["id"])}/activate", new { }));

        var list = await Json(await admin.GetAsync("/api/v1/lab/sops?targetKind=SECTION&targetCode=BIOCHEM"));
        Assert.Contains(list.AsArray(), s => S(s!["code"]) == code.ToUpper() && S(s["version"]) == "1.0" && S(s["status"]) == "ARCHIVED");
        var all = await Json(await admin.GetAsync("/api/v1/lab/sops?status=ACTIVE"));
        Assert.Single(all.AsArray().Where(s => S(s!["code"]) == code.ToUpper()));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/v1/lab/sops/{S(v1["id"])}/activate", new { })).StatusCode);
    }

    [Fact]
    public async Task Staff_competency_grant_check_expiry_and_revoke()
    {
        var admin = _f.As(DemoDataSeeder.Admin);
        var emp = DemoDataSeeder.Technician;
        Assert.False((await Json(await admin.GetAsync($"/api/v1/lab/competencies/check?employeeId={emp}&kind=ANALYZER&targetCode=MINDRAY-X")))["allowed"]!.GetValue<bool>());
        var g = await Json(await admin.PostAsJsonAsync("/api/v1/lab/competencies", new { employeeId = emp, kind = "ANALYZER", targetCode = "MINDRAY-X", validUntil = DateTime.UtcNow.AddMonths(6) }));
        Assert.True((await Json(await admin.GetAsync($"/api/v1/lab/competencies/check?employeeId={emp}&kind=ANALYZER&targetCode=MINDRAY-X")))["allowed"]!.GetValue<bool>());

        await Json(await admin.PostAsJsonAsync("/api/v1/lab/competencies", new { employeeId = emp, kind = "TEST", targetCode = "GLU", assessedAt = DateTime.UtcNow.AddYears(-2), validUntil = DateTime.UtcNow.AddDays(-1) }));
        var exp = await Json(await admin.GetAsync($"/api/v1/lab/competencies/check?employeeId={emp}&kind=TEST&targetCode=GLU"));
        Assert.False(exp["allowed"]!.GetValue<bool>());
        Assert.Contains("закінчився", S(exp["reason"]));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/lab/competencies", new { employeeId = emp, kind = "TEST", targetCode = "ALT", validUntil = DateTime.UtcNow.AddYears(-5) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/lab/competencies", new { employeeId = emp, kind = "MAGIC", targetCode = "ALT" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.As(DemoDataSeeder.Technician).PostAsJsonAsync("/api/v1/lab/competencies", new { employeeId = emp, kind = "TEST", targetCode = "ALT" })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/lab/competencies/{S(g["id"])}/revoke", new { reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/v1/lab/competencies/{S(g["id"])}/revoke", new { reason = "Не пройшов переоцінку" })).StatusCode);
        Assert.False((await Json(await admin.GetAsync($"/api/v1/lab/competencies/check?employeeId={emp}&kind=ANALYZER&targetCode=MINDRAY-X")))["allowed"]!.GetValue<bool>());
        var list = await Json(await admin.GetAsync($"/api/v1/lab/competencies?employeeId={emp}"));
        Assert.Contains(list.AsArray(), c => S(c!["targetCode"]) == "GLU" && !c["isValid"]!.GetValue<bool>());
    }
}
