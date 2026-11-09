// Імітатор Identity: discovery, JWKS, код + PKCE, помилки, refresh, вибір профілю, вихід. Сервер у пам'яті, без мережі.
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MedLink.IdentitySim.Tests;

public class IdentitySimFlowTests : IClassFixture<SimFactory>
{
    private readonly SimFactory _f;
    public IdentitySimFlowTests(SimFactory f) => _f = f;

    [Fact]
    public async Task Discovery_lists_endpoints_under_issuer_and_only_S256()
    {
        var c = new FlowClient(_f).Http;
        var d = JsonNode.Parse(await c.GetStringAsync("/.well-known/openid-configuration"))!;
        Assert.Equal(SimFactory.Issuer, d["issuer"]!.GetValue<string>());
        Assert.Equal(SimFactory.Issuer + "/connect/token", d["token_endpoint"]!.GetValue<string>());
        Assert.Equal(SimFactory.Issuer + "/.well-known/openid-configuration/jwks", d["jwks_uri"]!.GetValue<string>());
        Assert.Equal("S256", d["code_challenge_methods_supported"]![0]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/connect/jwks")).StatusCode);
    }

    [Fact]
    public async Task Full_code_pkce_flow_gives_signed_tokens_with_mis_claims()
    {
        var flow = new FlowClient(_f);
        var tokens = await flow.FullFlowAsync();
        Assert.Equal("Bearer", tokens["token_type"]!.GetValue<string>());
        Assert.Equal(3600, tokens["expires_in"]!.GetValue<int>());
        Assert.NotNull(tokens["refresh_token"]);
        Assert.NotNull(tokens["id_token"]);

        // Підпис перевіряється ключами з JWKS (а не внутрішнім ключем сервера)
        var jwks = new JsonWebKeySet(await flow.Http.GetStringAsync("/.well-known/openid-configuration/jwks"));
        var access = tokens["access_token"]!.GetValue<string>();
        var v = await new JsonWebTokenHandler().ValidateTokenAsync(access, new TokenValidationParameters
        {
            ValidIssuer = SimFactory.Issuer, IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidAudiences = new[] { "IdentityApiScope", "MisApiScope" }
        });
        Assert.True(v.IsValid, v.Exception?.Message);
        var jwt = (JsonWebToken)v.SecurityToken;
        Assert.Equal("RS256", jwt.Alg);
        Assert.Equal(SimFactory.UserSingle, jwt.GetClaim("sub").Value);
        Assert.Equal("Лаборант Олена Петрівна", jwt.GetClaim("name").Value);
        Assert.Equal("local", jwt.GetClaim("idp").Value);
        Assert.Contains("pwd", jwt.GetPayloadValue<string[]>("amr"));
        Assert.NotEmpty(jwt.GetClaim("sid").Value);
        Assert.NotEmpty(jwt.GetClaim("auth_time").Value);
        Assert.Contains("IdentityApiScope", jwt.Audiences);
        Assert.Contains("MisApiScope", jwt.Audiences);
        Assert.InRange((jwt.ValidTo - jwt.ValidFrom).TotalMinutes, 59.9, 60.1);

        // userinfo по токену доступу
        flow.Http.DefaultRequestHeaders.Authorization = new("Bearer", access);
        var ui = JsonNode.Parse(await flow.Http.GetStringAsync("/connect/userinfo"))!;
        Assert.Equal(SimFactory.UserSingle, ui["sub"]!.GetValue<string>());
    }

    [Fact]
    public async Task Wrong_redirect_uri_is_rejected_on_authorize_without_redirecting()
    {
        var flow = new FlowClient(_f);
        var r = await flow.Http.GetAsync(flow.AuthorizeUrl(redirect: "https://evil.example/lab/callback"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); // сторінка помилки, не переадресація на чужу адресу
        Assert.Null(r.Headers.Location);
        // Точне співпадіння: навіть слеш у кінці — інша адреса
        Assert.Equal(HttpStatusCode.BadRequest, (await flow.Http.GetAsync(flow.AuthorizeUrl(redirect: SimFactory.Redirect + "/"))).StatusCode);
    }

    [Fact]
    public async Task Redirect_uri_must_match_at_token_endpoint_too()
    {
        var flow = new FlowClient(_f);
        var (redirect, _) = await flow.LoginAsync(SimFactory.UserSingle);
        var r = await flow.TokenAsync(FlowClient.CodeOf(redirect!), redirect: "https://mis.example/lab/other");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("invalid_grant", JsonNode.Parse(await r.Content.ReadAsStringAsync())!["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task Wrong_code_verifier_is_rejected_and_code_is_single_use()
    {
        var flow = new FlowClient(_f);
        var (redirect, _) = await flow.LoginAsync(SimFactory.UserSingle);
        var code = FlowClient.CodeOf(redirect!);
        var bad = await flow.TokenAsync(code, verifier: new string('a', 50));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        // Код погашено першою спробою: навіть із вірним верифікатором вдруге він не діє
        Assert.Equal(HttpStatusCode.BadRequest, (await flow.TokenAsync(code)).StatusCode);
    }

    [Fact]
    public async Task Expired_code_is_rejected()
    {
        var flow = new FlowClient(_f);
        var (redirect, _) = await flow.LoginAsync(SimFactory.UserSingle);
        _f.Time.Offset = TimeSpan.FromMinutes(5);
        try
        {
            var r = await flow.TokenAsync(FlowClient.CodeOf(redirect!));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Equal("invalid_grant", JsonNode.Parse(await r.Content.ReadAsStringAsync())!["error"]!.GetValue<string>());
        }
        finally { _f.Time.Offset = TimeSpan.Zero; }
    }

    [Fact]
    public async Task Wrong_password_does_not_issue_code_and_plain_pkce_is_refused()
    {
        var flow = new FlowClient(_f);
        var (redirect, html) = await flow.LoginAsync(SimFactory.UserSingle, password: "wrong");
        Assert.Null(redirect);
        Assert.Contains("Невірний пароль", html);
        Assert.DoesNotContain("wrong", html);
        var plain = await flow.Http.GetAsync(flow.AuthorizeUrl(method: "plain"));
        Assert.Equal(HttpStatusCode.Redirect, plain.StatusCode);
        Assert.Contains("error=invalid_request", plain.Headers.Location!.Query);
    }

    [Fact]
    public async Task Refresh_token_rotates_and_old_one_stops_working()
    {
        var flow = new FlowClient(_f);
        var first = await flow.FullFlowAsync();
        var r1 = await flow.RefreshAsync(first["refresh_token"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var second = JsonNode.Parse(await r1.Content.ReadAsStringAsync())!;
        Assert.NotEqual(first["refresh_token"]!.GetValue<string>(), second["refresh_token"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await flow.RefreshAsync(first["refresh_token"]!.GetValue<string>())).StatusCode);
    }

    [Fact]
    public async Task User_with_several_profiles_must_choose_one()
    {
        var flow = new FlowClient(_f);
        var (redirect, html) = await flow.LoginAsync(SimFactory.UserMulti);
        Assert.Null(redirect);
        Assert.Contains("Оберіть профіль", html);
        Assert.Contains("Працівник реєстратури", html);
        var pick = await flow.Http.PostAsync("/connect/authorize/profile", new FormUrlEncodedContent(new Dictionary<string, string> { ["ticket"] = FlowClient.Ticket(html), ["profile"] = "p-reg" }));
        Assert.Equal(HttpStatusCode.Redirect, pick.StatusCode);
        var tokens = JsonNode.Parse(await (await flow.TokenAsync(FlowClient.CodeOf(pick.Headers.Location!))).Content.ReadAsStringAsync())!;
        Assert.Equal("p-reg", new JsonWebToken(tokens["access_token"]!.GetValue<string>()).GetClaim("profile_id").Value);
        // Профіль не з їхнього переліку не приймається
        var bad = await flow.Http.PostAsync("/connect/authorize/profile", new FormUrlEncodedContent(new Dictionary<string, string> { ["ticket"] = FlowClient.Ticket(html), ["profile"] = "foreign" }));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Endsession_redirects_only_to_registered_post_logout_uri_and_kills_refresh()
    {
        var flow = new FlowClient(_f);
        var tokens = await flow.FullFlowAsync();
        var idt = tokens["id_token"]!.GetValue<string>();
        var ok = await flow.Http.GetAsync($"/connect/endsession?id_token_hint={idt}&post_logout_redirect_uri={Uri.EscapeDataString("https://mis.example/lab/")}&state=s9");
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        Assert.Equal("https://mis.example/lab/?state=s9", ok.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await flow.RefreshAsync(tokens["refresh_token"]!.GetValue<string>())).StatusCode);

        var evil = await flow.Http.GetAsync($"/connect/endsession?id_token_hint={idt}&post_logout_redirect_uri={Uri.EscapeDataString("https://evil.example/")}");
        Assert.Equal(HttpStatusCode.OK, evil.StatusCode); // сторінка «Ви вийшли», без переадресації
    }
}
