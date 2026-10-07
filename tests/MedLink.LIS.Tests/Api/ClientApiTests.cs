// FR-REF-002: лабораторія як виконавець для клінік — реєстр клініки (перевірка → створення), договірні ціни,
// замовлення клієнта, зведений акт/рахунок за період
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;

namespace MedLink.LIS.Tests.Api;

[Collection("api")]
public class ClientApiTests
{
    private readonly LisApiFactory _f;
    public ClientApiTests(LisApiFactory f) => _f = f;

    private static async Task<JsonNode> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    private static MultipartFormDataContent Csv(string csv) => new() { { new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "registry.csv" } };

    [Fact]
    public async Task Clinic_registry_import_contract_prices_client_orders_and_period_act()
    {
        var reg = _f.As(DemoDataSeeder.Registrar);
        var admin = _f.As(DemoDataSeeder.Admin);
        var clients = (await Json(await reg.GetAsync("/api/v1/lab/clients"))).AsArray();
        Assert.Contains(clients, c => c!["code"]!.GetValue<string>() == "CLINIC-ZD");

        var suffix = Guid.NewGuid().ToString("N")[..5];
        var csv = "lastName;firstName;middleName;birthDate;gender;phone;clinicNumber;doctor;tests;cito\n" +
                  $"Клієнтенко{suffix};Олег;Іванович;15.03.1980;M;+380501110000;ZD-{suffix}-1;Іваненко О.П.;PROF_CBC,VITD;\n" +
                  $"Мельник;Юрій;Володимирович;12.04.1982;M;;ZD-{suffix}-2;Іваненко О.П.;GLU;так\n" +
                  $"Помилка{suffix};Анна;;01.01.1990;F;;ZD-{suffix}-3;;NO_SUCH_CODE;\n";
        var dry = await Json(await reg.PostAsync("/api/v1/lab/clients/CLINIC-ZD/registry?dryRun=true", Csv(csv)));
        Assert.Equal(3, dry["total"]!.GetValue<int>());
        Assert.Equal(2, dry["ok"]!.GetValue<int>());
        var rows = dry["rows"]!.AsArray();
        Assert.True(rows[0]!["newPatient"]!.GetValue<bool>());
        Assert.False(rows[1]!["newPatient"]!.GetValue<bool>()); // існуючий пацієнт знайдено за ПІБ + датою народження
        Assert.Contains("NO_SUCH_CODE", rows[2]!["message"]!.GetValue<string>());

        var created = await Json(await reg.PostAsync("/api/v1/lab/clients/CLINIC-ZD/registry?dryRun=false", Csv(csv)));
        var r0 = created["rows"]![0]!;
        Assert.Equal("CREATED", r0["status"]!.GetValue<string>());
        Assert.Equal(200m + 360m, r0["payerAmount"]!.GetValue<decimal>()); // договірний прайс клініки
        var order = await Json(await reg.GetAsync($"/api/v1/lab/orders/{r0["orderId"]!.GetValue<string>()}"));
        Assert.Equal("EXTERNAL_CLINIC", order["referralType"]!.GetValue<string>());
        Assert.Equal($"ZD-{suffix}-1", order["referralNumber"]!.GetValue<string>());
        Assert.Equal("CLINIC", order["payerKind"]!.GetValue<string>());
        Assert.True((await Json(await reg.GetAsync($"/api/v1/lab/orders/{created["rows"]![1]!["orderId"]!.GetValue<string>()}")))["isUrgentCito"]!.GetValue<bool>());

        var clientOrders = (await Json(await reg.GetAsync("/api/v1/lab/clients/CLINIC-ZD/orders")))["items"]!.AsArray();
        Assert.Contains(clientOrders, o => o!["clinicNumber"]?.GetValue<string>() == $"ZD-{suffix}-1");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var act = await Json(await admin.PostAsJsonAsync("/api/v1/lab/payers/CLINIC-ZD/invoices/period", new { from = today, to = today }));
        Assert.StartsWith("ACT-", act["number"]!.GetValue<string>());
        Assert.True(act["amount"]!.GetValue<decimal>() >= 560m);
        var print = await (await admin.GetAsync(act["printUrl"]!.GetValue<string>())).Content.ReadAsStringAsync();
        Assert.Contains($"Клієнтенко{suffix}", print);
        // Повторно за той самий період — немає невиставлених нарахувань
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/lab/payers/CLINIC-ZD/invoices/period", new { from = today, to = today })).StatusCode);
        Assert.NotEmpty((await Json(await admin.GetAsync("/api/v1/lab/payers/CLINIC-ZD/invoices"))).AsArray());
    }
}
