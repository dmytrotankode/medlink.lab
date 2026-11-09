// Спільне для тестів: сервер імітатора в пам'яті, клієнт потоку «код + PKCE», підміна годинника.
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MedLink.IdentitySim;
using MedLink.LIS.Api.Data.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace MedLink.IdentitySim.Tests;

public sealed class OffsetTime : TimeProvider
{
    public TimeSpan Offset { get; set; }
    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Offset;
}

public sealed class SimFactory : WebApplicationFactory<SimMarker>
{
    public const string Issuer = "http://sim.test/id"; // як на стенді: префікс /id зрізає nginx
    public const string Password = "test-stand-password";
    public const string Redirect = "https://mis.example/lab/callback";
    public const string UserSingle = "11111111-1111-1111-1111-111111111111";
    public const string UserMulti = "22222222-2222-2222-2222-222222222222";
    public const string ProfileLab = "profile-lab";
    private readonly string _key = Path.Combine(Path.GetTempPath(), $"idsim-key-{Guid.NewGuid():N}.pem");
    public OffsetTime Time { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sim:Issuer"] = Issuer, ["Sim:Password"] = Password, ["Sim:KeyPath"] = _key,
            ["Sim:Clients:MisFront:AllowedRedirectUris:0"] = "{origin}/lab/callback",
            ["Sim:Clients:MisFront:AllowedRedirectUris:1"] = Redirect,
            ["Sim:Clients:MisFront:AllowedPostLogoutRedirectUris:0"] = "{origin}/lab/",
            ["Sim:Clients:MisFront:AllowedPostLogoutRedirectUris:1"] = "https://mis.example/lab/",
            ["Sim:Users:0:Sub"] = UserSingle, ["Sim:Users:0:Name"] = "Лаборант Олена Петрівна",
            ["Sim:Users:0:Profiles:0:Id"] = ProfileLab, ["Sim:Users:0:Profiles:0:Caption"] = "Лаборант eHealth вторинки",
            ["Sim:Users:1:Sub"] = UserMulti, ["Sim:Users:1:Name"] = "Адмін Іван",
            ["Sim:Users:1:Profiles:0:Id"] = "p-admin", ["Sim:Users:1:Profiles:0:Caption"] = "Адміністратор",
            ["Sim:Users:1:Profiles:1:Id"] = "p-reg", ["Sim:Users:1:Profiles:1:Caption"] = "Працівник реєстратури",
            // sub = org_employee.id демо-реєстратора Lab API (режим без моста зіставляє співробітника за sub)
            ["Sim:Users:2:Sub"] = DemoDataSeeder.Registrar, ["Sim:Users:2:Name"] = "Реєстратор демо",
            ["Sim:Users:2:Profiles:0:Id"] = "p-demo-reg", ["Sim:Users:2:Profiles:0:Caption"] = "Працівник реєстратури"
        }));
        builder.ConfigureServices(s => { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(Time); });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_key); } catch { /* ignore */ }
    }
}

/// <summary>Мінімальний клієнт OIDC (як oidc-client у SPA): PKCE S256, без автоматичних переадресацій.</summary>
public sealed class FlowClient
{
    public readonly HttpClient Http;
    public string Verifier { get; } = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
    public string Challenge => Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));
    public string Scope = "openid MisFrontScope IdentityApiScope MisApiScope offline_access";

    public FlowClient(SimFactory f) => Http = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public string AuthorizeUrl(string redirect = SimFactory.Redirect, string? challenge = null, string method = "S256", string state = "st-1") =>
        $"/connect/authorize?client_id=MisFront&redirect_uri={Uri.EscapeDataString(redirect)}&response_type=code&scope={Uri.EscapeDataString(Scope)}" +
        $"&state={state}&nonce=n-1&code_challenge={challenge ?? Challenge}&code_challenge_method={method}";

    public static string Ticket(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"ticket\" value=\"([^\"]+)\"").Groups[1].Value);

    /// <summary>Вхід користувача: повертає адресу переадресації (з кодом) або null, якщо показано вибір профілю.</summary>
    public async Task<(Uri? Redirect, string Html)> LoginAsync(string sub, string password = SimFactory.Password, string? ticket = null)
    {
        if (ticket == null)
        {
            var page = await Http.GetStringAsync(AuthorizeUrl());
            ticket = Ticket(page);
        }
        var r = await Http.PostAsync("/connect/authorize/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["ticket"] = ticket, ["sub"] = sub, ["password"] = password }));
        return r.StatusCode == HttpStatusCode.Redirect ? (r.Headers.Location, "") : (null, await r.Content.ReadAsStringAsync());
    }

    public static string CodeOf(Uri redirect) => System.Web.HttpUtility.ParseQueryString(redirect.Query)["code"]!;

    public Task<HttpResponseMessage> TokenAsync(string code, string? verifier = null, string redirect = SimFactory.Redirect) =>
        Http.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = "MisFront", ["code"] = code, ["redirect_uri"] = redirect, ["code_verifier"] = verifier ?? Verifier
        }));

    public Task<HttpResponseMessage> RefreshAsync(string refresh) =>
        Http.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["client_id"] = "MisFront", ["refresh_token"] = refresh }));

    /// <summary>Повний потік до токенів для користувача з одним профілем.</summary>
    public async Task<JsonNode> FullFlowAsync(string sub = SimFactory.UserSingle)
    {
        var (redirect, _) = await LoginAsync(sub);
        var r = await TokenAsync(CodeOf(redirect!));
        return JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
    }
}
