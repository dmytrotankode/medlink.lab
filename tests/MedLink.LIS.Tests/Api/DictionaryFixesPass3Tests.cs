// Виправлення візуального проходу №3: валідація довідників (в т.ч. через імпорт), CSV з BOM, публічна адреса в команді коннектора.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class DictionaryFixesPass3Tests
{
    private const string Dict = "/api/v1/lab/dictionaries";
    private readonly LisApiFactory _f;
    public DictionaryFixesPass3Tests(LisApiFactory f) => _f = f;

    private HttpClient Admin => _f.As(DemoDataSeeder.Admin);

    private static async Task<string> Detail(HttpResponseMessage r, HttpStatusCode status)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"очікувалось {(int)status}, отримано {(int)r.StatusCode}: {text}");
        return JsonNode.Parse(text)?["detail"]?.GetValue<string>() ?? "";
    }

    [Fact]
    public async Task Import_rejects_row_without_name()
    {
        var csv = "code;name;ageFrom;ageTo;ageUnit;sortOrder\nZZ_P3A;;1;2;YEARS;992\n";
        var res = JsonNode.Parse(await (await Admin.PostAsync($"{Dict}/import?name=age-groups&dryRun=false", new StringContent(csv, Encoding.UTF8, "text/csv"))).Content.ReadAsStringAsync())!;
        Assert.Equal(1, res["error"]!.GetValue<int>());
        Assert.Equal(0, res["create"]!.GetValue<int>());
        var list = JsonNode.Parse(await (await Admin.GetAsync($"{Dict}/age-groups?search=ZZ_P3A&pageSize=100")).Content.ReadAsStringAsync())!;
        Assert.Empty(list["items"]!.AsArray());
    }

    [Fact]
    public async Task Age_group_validation_messages()
    {
        Assert.Contains("від’ємним", (await Detail(await Admin.PostAsJsonAsync($"{Dict}/age-groups", new { code = "ZZ_P3B", name = "Тест", ageFrom = -5, ageTo = 2, ageUnit = "YEARS" }), HttpStatusCode.BadRequest)).Replace("'", "’"));
        var unit = await Detail(await Admin.PostAsJsonAsync($"{Dict}/age-groups", new { code = "ZZ_P3C", name = "Тест", ageFrom = 1, ageTo = 2, ageUnit = "WEEKS" }), HttpStatusCode.BadRequest);
        Assert.Contains("дні, місяці або роки", unit);
        Assert.DoesNotContain("DAYS", unit);
    }

    [Fact]
    public async Task Rejection_reason_category_is_restricted()
    {
        await Detail(await Admin.PostAsJsonAsync($"{Dict}/rejection-reasons", new { code = "ZZ_P3D", name = "Тест", category = "FOO" }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Work_calendar_without_date_is_validation_error_not_conflict()
    {
        var detail = await Detail(await Admin.PostAsJsonAsync($"{Dict}/work-calendar", new { name = "Без дати" }), HttpStatusCode.BadRequest);
        Assert.Contains("дату", detail);
    }

    [Fact]
    public async Task Csv_export_starts_with_utf8_bom()
    {
        var bytes = await (await Admin.GetAsync($"{Dict}/units/export?format=csv")).Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    }

    [Fact]
    public async Task Connector_setup_command_has_no_localhost_when_public_url_is_known()
    {
        var res = JsonNode.Parse(await (await Admin.PostAsJsonAsync("/api/v1/lab/connectors", new { name = "ZZ P3 коннектор" })).Content.ReadAsStringAsync())!;
        var cmd = res["setupCommand"]!.GetValue<string>();
        Assert.DoesNotContain("localhost:5055", cmd);
        await Admin.DeleteAsync($"/api/v1/lab/connectors/{res["id"]}");
    }
}
