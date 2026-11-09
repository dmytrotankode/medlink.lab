// Видача токенів: access (JWT RS256, typ at+jwt), id_token, перевірка PKCE. Склад claims — як у токенів MedLink Identity.
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MedLink.IdentitySim;

public sealed class TokenService
{
    /// <summary>Скопи, що стають аудиторією токена доступу (інші скопи лише потрапляють у claim scope).</summary>
    public static readonly string[] ApiScopes = { "IdentityApiScope", "MisApiScope" };

    private readonly SimOptions _o;
    private readonly KeyStore _keys;
    private readonly TimeProvider _time;
    private readonly JsonWebTokenHandler _handler = new();

    public TokenService(SimOptions o, KeyStore keys, TimeProvider time) { _o = o; _keys = keys; _time = time; }

    public int AccessSeconds => _o.AccessTokenMinutes * 60;

    public string AccessToken(string clientId, SimUser user, string? profileId, string scope, string sid, long authTime)
    {
        var scopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var claims = new Dictionary<string, object>
        {
            ["aud"] = ApiScopes.Where(scopes.Contains).ToArray(),
            ["client_id"] = clientId,
            ["sub"] = user.Sub,
            ["auth_time"] = authTime,
            ["idp"] = "local",
            ["name"] = user.Name,
            ["sid"] = sid,
            ["scope"] = scopes,
            ["amr"] = new[] { "pwd" }
        };
        if (profileId != null) claims["profile_id"] = profileId; // розширення імітатора: профіль, обраний після входу
        return Sign(claims, "at+jwt", _o.AccessTokenMinutes);
    }

    public string IdToken(string clientId, SimUser user, string? profileId, string? nonce, string sid, long authTime, string accessToken)
    {
        var claims = new Dictionary<string, object>
        {
            ["aud"] = clientId,
            ["sub"] = user.Sub,
            ["auth_time"] = authTime,
            ["idp"] = "local",
            ["name"] = user.Name,
            ["sid"] = sid,
            ["amr"] = new[] { "pwd" },
            ["at_hash"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(accessToken))[..16])
        };
        if (nonce != null) claims["nonce"] = nonce;
        if (profileId != null) claims["profile_id"] = profileId;
        return Sign(claims, "JWT", 5);
    }

    private string Sign(Dictionary<string, object> claims, string typ, int minutes)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _o.IssuerBase,
            Claims = claims,
            NotBefore = now, IssuedAt = now, Expires = now.AddMinutes(minutes),
            SigningCredentials = _keys.Credentials,
            TokenType = typ
        });
    }

    /// <summary>PKCE S256: BASE64URL(SHA256(code_verifier)) = code_challenge; довжина верифікатора 43–128 (RFC 7636).</summary>
    public static bool VerifyPkce(string? verifier, string challenge)
    {
        if (string.IsNullOrEmpty(verifier) || verifier.Length < 43 || verifier.Length > 128) return false;
        var computed = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(challenge));
    }

    /// <summary>Перевірка власного токена (userinfo, endsession); lifetime можна не перевіряти для id_token_hint.</summary>
    public async Task<TokenValidationResult> ValidateAsync(string token, bool lifetime, string? audience)
    {
        return await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _o.IssuerBase, IssuerSigningKey = _keys.Key,
            ValidateAudience = audience != null, ValidAudience = audience, ValidateLifetime = lifetime,
            ClockSkew = TimeSpan.FromSeconds(30)
        });
    }
}
