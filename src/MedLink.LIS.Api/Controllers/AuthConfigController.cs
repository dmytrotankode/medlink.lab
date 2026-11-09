// Налаштування входу для SPA: режим, authority/клієнт OIDC (код + PKCE) і ознака моста з МІС. Секретів не містить. Доступно без входу.
using MedLink.LIS.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Controllers;

public sealed class AuthConfigController : LisControllerBase
{
    private readonly IConfiguration _cfg;
    public AuthConfigController(IConfiguration cfg) => _cfg = cfg;

    /// <summary>Режим входу та параметри OIDC-клієнта. У режимах None/Header клієнт OIDC не використовується («Працюю як…»).</summary>
    [HttpGet("auth/config")]
    public IActionResult Config()
    {
        var mode = AuthModes.Resolve(_cfg);
        var oidc = mode == AuthModes.Oidc;
        return Ok(new
        {
            mode,
            runAs = !oidc, // перемикач «Працюю як…» лише без входу через Identity
            oidc = oidc ? new
            {
                authority = (_cfg["Identity:Authority"] ?? "").TrimEnd('/'),
                clientId = _cfg["Auth:Oidc:ClientId"] ?? "MisFront",
                scope = _cfg["Auth:Oidc:Scope"] ?? "openid MisFrontScope IdentityApiScope MisApiScope offline_access",
                // Шлях повернення відносно бази застосунку (PathBase). Має збігатися із зареєстрованим у Identity redirect URI.
                callbackPath = _cfg["Auth:Oidc:CallbackPath"] ?? "callback",
                postLogoutPath = _cfg["Auth:Oidc:PostLogoutPath"] ?? ""
            } : null,
            bridge = new { enabled = !string.IsNullOrWhiteSpace(_cfg["Medlink:ConnectionString"]) },
            basePath = Request.PathBase.Value + "/"
        });
    }
}
