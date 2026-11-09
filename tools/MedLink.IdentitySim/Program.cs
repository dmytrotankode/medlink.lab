// Імітатор сервісу автентифікації (Identity) MedLink: OIDC, код авторизації + PKCE S256, JWT RS256, JWKS, refresh_token.
// Призначення: стенд, де справжнього Identity немає. Перемикання на справжній — лише Identity:Authority у Lab API (docs/MEDLINK_INTEGRATION_PLAN.md).
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MedLink.IdentitySim;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection("Sim").Get<SimOptions>() ?? new SimOptions());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new KeyStore(sp.GetRequiredService<SimOptions>().KeyPath));
builder.Services.AddSingleton<SimState>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IUserStore>(sp =>
{
    var o = sp.GetRequiredService<SimOptions>();
    return string.IsNullOrWhiteSpace(o.MedlinkConnectionString)
        ? new ConfigUserStore(o)
        : new MedLinkUserStore(o.MedlinkConnectionString, sp.GetRequiredService<TimeProvider>());
});

var app = builder.Build();

// Без пароля стенда й issuer сервіс не стартує (пароль у журнал не друкується)
{
    var o = app.Services.GetRequiredService<SimOptions>();
    if (string.IsNullOrWhiteSpace(o.Password)) throw new InvalidOperationException("Sim:Password не задано: імітатор не стартує без пароля стенда (змінна середовища Sim__Password).");
    if (!Uri.TryCreate(o.Issuer, UriKind.Absolute, out _)) throw new InvalidOperationException("Sim:Issuer має бути абсолютною адресою (на стенді https://mis.med-one.avowiz.com/id).");
    app.Services.GetRequiredService<KeyStore>();
    app.Logger.LogInformation("Імітатор Identity: issuer {Issuer}, користувачі: {Source}, клієнти: {Clients}", o.IssuerBase,
        string.IsNullOrWhiteSpace(o.MedlinkConnectionString) ? "Sim:Users" : "база МІС (лише читання)", string.Join(", ", o.Clients.Keys));
}

app.MapGet("/", (SimOptions o) => Results.Json(new { service = "MedLink.IdentitySim", issuer = o.IssuerBase, discovery = o.IssuerBase + "/.well-known/openid-configuration" }));

// ---------------------------------------------------------------- discovery і ключі
app.MapGet("/.well-known/openid-configuration", (SimOptions o) =>
{
    var b = o.IssuerBase;
    var allScopes = o.Clients.Values.SelectMany(c => c.AllowedScopes).Distinct().ToArray();
    return Results.Json(new Dictionary<string, object>
    {
        ["issuer"] = b,
        ["jwks_uri"] = b + "/.well-known/openid-configuration/jwks",
        ["authorization_endpoint"] = b + "/connect/authorize",
        ["token_endpoint"] = b + "/connect/token",
        ["userinfo_endpoint"] = b + "/connect/userinfo",
        ["end_session_endpoint"] = b + "/connect/endsession",
        ["frontchannel_logout_supported"] = false,
        ["scopes_supported"] = allScopes,
        ["claims_supported"] = new[] { "sub", "name", "sid", "auth_time", "idp", "amr" },
        ["grant_types_supported"] = new[] { "authorization_code", "refresh_token" },
        ["response_types_supported"] = new[] { "code" },
        ["response_modes_supported"] = new[] { "query" },
        ["token_endpoint_auth_methods_supported"] = new[] { "none" },
        ["subject_types_supported"] = new[] { "public" },
        ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
        ["code_challenge_methods_supported"] = new[] { "S256" }
    });
});
IResult Jwks(KeyStore keys) => Results.Json(new { keys = new[] { new { kty = "RSA", use = "sig", kid = keys.PublicJwk.Kid, alg = "RS256", n = keys.PublicJwk.N, e = keys.PublicJwk.E } } });
app.MapGet("/.well-known/openid-configuration/jwks", Jwks);
app.MapGet("/connect/jwks", Jwks);

// ---------------------------------------------------------------- authorize: запит → вхід → (профіль) → код
app.MapGet("/connect/authorize", async (HttpContext ctx, SimOptions o, SimState st, IUserStore users, TimeProvider time) =>
{
    var q = ctx.Request.Query;
    string P(string k) => q[k].ToString();
    // Клієнт і redirect_uri перевіряються до будь-яких переадресацій: помилка тут показується на сторінці, не переадресовується
    if (!o.Clients.TryGetValue(P("client_id"), out var client)) return Html(Pages.Error("Невідомий клієнт (client_id)."), 400);
    if (!client.RedirectAllowed(P("redirect_uri"), o.Origin)) return Html(Pages.Error("Недозволений redirect_uri: адреса повернення не зареєстрована для клієнта."), 400);
    var redirect = P("redirect_uri");
    string? state = q.ContainsKey("state") ? P("state") : null;

    if (P("response_type") != "code") return Err(redirect, state, "unsupported_response_type", "Підтримується лише response_type=code.");
    var scope = P("scope");
    var asked = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (!asked.Contains("openid")) return Err(redirect, state, "invalid_scope", "Потрібен scope openid.");
    if (asked.Any(s => !client.AllowedScopes.Contains(s))) return Err(redirect, state, "invalid_scope", "Запитано недозволений scope.");
    if (string.IsNullOrEmpty(P("code_challenge"))) return Err(redirect, state, "invalid_request", "Потрібен code_challenge (PKCE).");
    if (P("code_challenge_method") != "S256") return Err(redirect, state, "invalid_request", "Підтримується лише code_challenge_method=S256.");
    if (P("prompt") == "none") return Err(redirect, state, "login_required", "Сеансу входу немає.");

    var req = new AuthRequest(P("client_id"), redirect, scope, state, q.ContainsKey("nonce") ? P("nonce") : null, P("code_challenge"));
    var ticket = st.Seal(new LoginTicket(req, null, time.GetUtcNow().AddMinutes(15).ToUnixTimeSeconds()));
    return Html(Pages.Login(o.IssuerBase + "/connect/authorize/login", ticket, await users.ListAsync(ctx.RequestAborted), null));
});

app.MapPost("/connect/authorize/login", async (HttpContext ctx, SimOptions o, SimState st, IUserStore users, TimeProvider time) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var t = st.Unseal<LoginTicket>(form["ticket"]);
    if (t == null || t.Exp < time.GetUtcNow().ToUnixTimeSeconds()) return Html(Pages.Error("Сеанс входу минув або недійсний. Почніть вхід заново з програми."), 400);
    var list = await users.ListAsync(ctx.RequestAborted);
    if (st.TooManyFailures()) return Html(Pages.Login(o.IssuerBase + "/connect/authorize/login", form["ticket"]!, list, "Забагато невдалих спроб. Зачекайте хвилину."), 429);

    var user = await users.FindAsync(form["sub"].ToString(), ctx.RequestAborted);
    if (user == null || !PasswordOk(form["password"].ToString(), o.Password))
    {
        st.RegisterFailure();
        await Task.Delay(400);
        return Html(Pages.Login(o.IssuerBase + "/connect/authorize/login", form["ticket"]!, list, "Невірний пароль стенда або користувач не обраний."));
    }
    if (user.Profiles.Count <= 1) return Complete(st, o, time, t.Request, user, user.Profiles.FirstOrDefault()?.Id);
    // Кілька профілів безпеки: «Оберіть профіль», як у МІС після входу
    var next = st.Seal(new LoginTicket(t.Request, user.Sub, time.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds()));
    return Html(Pages.Profiles(o.IssuerBase + "/connect/authorize/profile", next, user));
});

app.MapPost("/connect/authorize/profile", async (HttpContext ctx, SimOptions o, SimState st, IUserStore users, TimeProvider time) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var t = st.Unseal<LoginTicket>(form["ticket"]);
    if (t?.Sub == null || t.Exp < time.GetUtcNow().ToUnixTimeSeconds()) return Html(Pages.Error("Сеанс входу минув або недійсний. Почніть вхід заново з програми."), 400);
    var user = await users.FindAsync(t.Sub, ctx.RequestAborted);
    var profile = user?.Profiles.FirstOrDefault(p => p.Id == form["profile"].ToString());
    if (user == null || profile == null) return Html(Pages.Error("Профіль не знайдено."), 400);
    return Complete(st, o, time, t.Request, user, profile.Id);
});

// ---------------------------------------------------------------- token
app.MapPost("/connect/token", async (HttpContext ctx, SimOptions o, SimState st, IUserStore users, TokenService tokens, TimeProvider time) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    var f = await ctx.Request.ReadFormAsync();
    var clientId = f["client_id"].ToString();
    if (clientId.Length == 0 && AuthenticationHeaderValue.TryParse(ctx.Request.Headers.Authorization, out var ah) && ah.Scheme == "Basic" && ah.Parameter != null)
        clientId = Encoding.UTF8.GetString(Convert.FromBase64String(ah.Parameter)).Split(':')[0];
    if (!o.Clients.ContainsKey(clientId)) return TokenError("invalid_client", "Невідомий клієнт.", 401);

    var now = time.GetUtcNow();
    switch (f["grant_type"].ToString())
    {
        case "authorization_code":
        {
            var c = st.TakeCode(f["code"].ToString());
            if (c == null || c.Expires < now) return TokenError("invalid_grant", "Код недійсний, використаний або прострочений.");
            if (c.Request.ClientId != clientId) return TokenError("invalid_grant", "Код видано іншому клієнту.");
            if (!string.Equals(c.Request.RedirectUri, f["redirect_uri"].ToString(), StringComparison.Ordinal)) return TokenError("invalid_grant", "redirect_uri не збігається із запитом авторизації.");
            if (!TokenService.VerifyPkce(f["code_verifier"], c.Request.Challenge)) return TokenError("invalid_grant", "Невірний code_verifier (PKCE).");
            var user = await users.FindAsync(c.Sub, ctx.RequestAborted);
            if (user == null) return TokenError("invalid_grant", "Користувача більше не існує.");
            return Results.Json(Issue(o, st, tokens, time, clientId, user, c.ProfileId, c.Request.Scope, c.Sid, c.AuthTime, c.Request.Nonce));
        }
        case "refresh_token":
        {
            var r = st.TakeRefresh(f["refresh_token"].ToString());
            if (r == null || r.Expires < now || r.ClientId != clientId) return TokenError("invalid_grant", "Refresh-токен недійсний або прострочений.");
            var user = await users.FindAsync(r.Sub, ctx.RequestAborted);
            if (user == null) return TokenError("invalid_grant", "Користувача більше не існує.");
            return Results.Json(Issue(o, st, tokens, time, clientId, user, r.ProfileId, r.Scope, r.Sid, r.AuthTime, null));
        }
        default: return TokenError("unsupported_grant_type", "Підтримуються authorization_code і refresh_token.");
    }
});

// ---------------------------------------------------------------- userinfo, endsession
app.MapMethods("/connect/userinfo", new[] { "GET", "POST" }, async (HttpContext ctx, TokenService tokens) =>
{
    var h = ctx.Request.Headers.Authorization.ToString();
    if (!h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return Results.Unauthorized();
    var r = await tokens.ValidateAsync(h[7..].Trim(), lifetime: true, audience: null);
    if (!r.IsValid) return Results.Unauthorized();
    var c = r.Claims;
    return Results.Json(new { sub = c["sub"], name = c.TryGetValue("name", out var n) ? n : null });
});

app.MapGet("/connect/endsession", async (HttpContext ctx, SimOptions o, SimState st, TokenService tokens) =>
{
    var q = ctx.Request.Query;
    var clientId = q["client_id"].ToString();
    var hint = q["id_token_hint"].ToString();
    if (hint.Length > 0)
    {
        var v = await tokens.ValidateAsync(hint, lifetime: false, audience: null); // підпис і issuer; термін id_token_hint не перевіряється
        if (v.IsValid)
        {
            if (clientId.Length == 0 && v.Claims.TryGetValue("aud", out var aud)) clientId = aud?.ToString() ?? "";
            if (v.Claims.TryGetValue("sid", out var sid) && sid != null) st.RevokeSession(sid.ToString()!);
        }
    }
    var post = q["post_logout_redirect_uri"].ToString();
    if (post.Length > 0 && o.Clients.TryGetValue(clientId, out var client) && client.PostLogoutAllowed(post, o.Origin))
    {
        var url = q.ContainsKey("state") ? post + (post.Contains('?') ? "&" : "?") + "state=" + Uri.EscapeDataString(q["state"].ToString()) : post;
        return Results.Redirect(url);
    }
    return Html(Pages.LoggedOut());
});

app.Run();

// ================================================================ допоміжні
static IResult Html(string html, int status = 200) => Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8, status);

static IResult TokenError(string error, string description, int status = 400) =>
    Results.Json(new { error, error_description = description }, statusCode: status);

static IResult Err(string redirect, string? state, string error, string description)
{
    var sep = redirect.Contains('?') ? "&" : "?";
    var url = $"{redirect}{sep}error={error}&error_description={Uri.EscapeDataString(description)}" + (state != null ? "&state=" + Uri.EscapeDataString(state) : "");
    return Results.Redirect(url);
}

static bool PasswordOk(string given, string expected) =>
    CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(given)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

static IResult Complete(SimState st, SimOptions o, TimeProvider time, AuthRequest req, SimUser user, string? profileId)
{
    var now = time.GetUtcNow();
    var code = st.PutCode(new IssuedCode(req, user.Sub, profileId, SimState.NewToken()[..32], now.ToUnixTimeSeconds(), now.AddSeconds(o.CodeSeconds)));
    var sep = req.RedirectUri.Contains('?') ? "&" : "?";
    var url = $"{req.RedirectUri}{sep}code={code}&scope={Uri.EscapeDataString(req.Scope)}" + (req.State != null ? "&state=" + Uri.EscapeDataString(req.State) : "");
    return Results.Redirect(url);
}

static Dictionary<string, object?> Issue(SimOptions o, SimState st, TokenService tokens, TimeProvider time, string clientId, SimUser user, string? profileId, string scope, string sid, long authTime, string? nonce)
{
    var access = tokens.AccessToken(clientId, user, profileId, scope, sid, authTime);
    var result = new Dictionary<string, object?>
    {
        ["access_token"] = access,
        ["expires_in"] = tokens.AccessSeconds,
        ["token_type"] = "Bearer",
        ["scope"] = scope
    };
    var scopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (scopes.Contains("openid")) result["id_token"] = tokens.IdToken(clientId, user, profileId, nonce, sid, authTime, access);
    if (scopes.Contains("offline_access"))
        result["refresh_token"] = st.PutRefresh(new RefreshEntry(clientId, scope, user.Sub, profileId, sid, authTime, time.GetUtcNow().AddHours(o.RefreshTokenHours)));
    return result;
}

// Тип-маркер для WebApplicationFactory у тестах (клас Program верхнього рівня тут internal, щоб не конфліктувати з Program Lab API)
public sealed class SimMarker { }
