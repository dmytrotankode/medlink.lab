// Lab API (режим Oidc) приймає токен імітатора: метадані й ключі підпису береться з самого імітатора (discovery → JWKS), як у справжнього Identity.
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using MedLink.LIS.Api.Data.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedLink.IdentitySim.Tests;

/// <summary>Спрямовує запити Lab API до метаданих у сервер імітатора в пам'яті; префікс /id зрізається, як це робить nginx стенда.</summary>
internal sealed class StripPrefixHandler : DelegatingHandler
{
    public StripPrefixHandler(HttpMessageHandler inner) : base(inner) { }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var u = request.RequestUri!;
        if (u.AbsolutePath.StartsWith("/id/")) request.RequestUri = new Uri(u.GetLeftPart(UriPartial.Authority) + u.PathAndQuery[3..]);
        return base.SendAsync(request, ct);
    }
}

public sealed class LabWithSimFactory : WebApplicationFactory<Program>
{
    private readonly SimFactory _sim;
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"idsim-lab-{Guid.NewGuid():N}.db");
    private readonly string _guide = Path.Combine(Path.GetTempPath(), $"idsim-guide-{Guid.NewGuid():N}.db");

    public LabWithSimFactory(SimFactory sim) => _sim = sim;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:GuideSqlitePath", _guide);
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite", ["Database:SqlitePath"] = _db, ["Database:GuideSqlitePath"] = _guide,
            ["Lab:SeedDemoData"] = "true", ["Lab:PublicBaseUrl"] = "http://localhost:5055", ["Logging:LogLevel:Default"] = "Warning",
            ["Auth:Mode"] = "Oidc", ["Identity:Authority"] = SimFactory.Issuer, ["Identity:JwtBearerAudience"] = "IdentityApiScope;MisApiScope",
            ["Auth:Oidc:ClientId"] = "MisFront"
        }));
        builder.ConfigureServices(s => s.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
            o => o.BackchannelHttpHandler = new StripPrefixHandler(_sim.Server.CreateHandler())));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var f in new[] { _db, _guide }) try { File.Delete(f); } catch { /* ignore */ }
    }
}

public class LabApiWithSimTests : IClassFixture<SimFactory>
{
    private readonly SimFactory _sim;
    public LabApiWithSimTests(SimFactory sim) => _sim = sim;

    [Fact]
    public async Task Simulator_token_is_accepted_by_lab_api_and_resolves_employee_role()
    {
        // Користувач імітатора з sub = org_employee.id демо-реєстратора (режим Lab API без моста: співробітник за sub)
        using var lab = new LabWithSimFactory(_sim);
        var tokens = await SignInAsDemoRegistrar(_sim);

        var c = lab.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/lab/context/me")).StatusCode); // без токена

        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens["access_token"]!.GetValue<string>());
        var r = await c.GetAsync("/api/v1/lab/context/me");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var me = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.Equal(DemoDataSeeder.Registrar, me["employee"]!["id"]!.GetValue<string>());
        Assert.Equal("REGISTRAR", me["employee"]!["labRole"]!.GetValue<string>());
    }

    [Fact]
    public async Task Token_signed_by_another_key_is_rejected_by_lab_api()
    {
        // Другий екземпляр імітатора має свій ключ: його токен не проходить перевірку підпису за JWKS першого
        using var simA = new SimFactory();
        using var simB = new SimFactory();
        using var lab = new LabWithSimFactory(simA);
        var foreign = await SignInAsDemoRegistrar(simB);
        var c = lab.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", foreign["access_token"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/lab/context/me")).StatusCode);
    }

    private static async Task<JsonNode> SignInAsDemoRegistrar(SimFactory sim)
    {
        var flow = new FlowClient(sim);
        var page = await flow.Http.GetStringAsync(flow.AuthorizeUrl());
        var ticket = FlowClient.Ticket(page);
        var login = await flow.Http.PostAsync("/connect/authorize/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["ticket"] = ticket, ["sub"] = DemoDataSeeder.Registrar, ["password"] = SimFactory.Password }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var r = await flow.TokenAsync(FlowClient.CodeOf(login.Headers.Location!));
        return JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
    }
}
