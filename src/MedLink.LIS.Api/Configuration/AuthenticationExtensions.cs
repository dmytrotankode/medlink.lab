using System.Reflection;
using System.Security.Claims;
using AspNet.Security.OAuth.Introspection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

namespace MedLink.LIS.Api.Configuration;

public static class AuthenticationExtensions
{
    public const string TokenSelectorScheme = "token_selector";
    public const string JwtScheme = "jwt";
    public const string IntrospectionScheme = "introspection";
    public const string RedisActiveTokenScheme = "redis_active_token";
    public const string IdentityChallengeHeader = "X-Auth-Source";
    public const string IdentityChallengeHeaderValue = "IdentityServer";

    private const string DefaultIntrospectionClientId = "MisApiScope";
    private const string IntrospectionSecretEnvVar = "REFERENCE_TOKEN_API_SECRET";
    internal const string TokenSessionIdHttpContextItemKey = "TokenSessionId";

    public static IServiceCollection AddHybridJwtAndReferenceTokenAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HybridTokenAuthOptions>(configuration.GetSection("Identity"));
        services.AddSingleton<IRedisDatabaseProvider, RedisDatabaseProvider>();
        var options = configuration.GetSection("Identity").Get<HybridTokenAuthOptions>() ?? new HybridTokenAuthOptions();

        var audiences = SplitBySemicolon(options.JwtBearerAudience);
        var introspectionAudiences = SplitBySemicolon(options.IntrospectionAudiences);
        var identityAuthority = options.Authority;
        var useRefreshTokenFlow = options.UseRefreshTokenFlow;
        var authDiagnosticsEnabled = options.AuthDiagnosticsEnabled;

        if (!useRefreshTokenFlow)
        {
            // Лише JWT: authority і audience читаються з остаточної конфігурації при першому запиті (а не при реєстрації) —
            // так працюють і змінні середовища, і перевизначення тестів. Порожній authority = жоден токен не пройде (401).
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IConfiguration>((jwt, cfg) =>
                ConfigureJwtBearer(jwt, cfg["Identity:Authority"]?.Trim() ?? "", SplitBySemicolon(cfg["Identity:JwtBearerAudience"])));
            return services;
        }

        if (string.IsNullOrWhiteSpace(identityAuthority))
        {
            throw new InvalidOperationException("Identity:Authority не задано: режим Auth:Mode=Oidc потребує адреси сервера Identity MedLink.");
        }

        var introspectionClientId = options.IntrospectionClientId;
        var introspectionClientSecret = FirstNonWhiteSpace(options.IntrospectionClientSecret, Environment.GetEnvironmentVariable(IntrospectionSecretEnvVar));

        if (!Uri.TryCreate(identityAuthority.Trim(), UriKind.Absolute, out var identityAuthorityUri))
        {
            throw new InvalidOperationException(
                $"Identity:Authority must be an absolute URL (e.g. https://identity.example). Current value: '{identityAuthority}'.");
        }

        if (string.IsNullOrWhiteSpace(introspectionClientId))
        {
            introspectionClientId = DefaultIntrospectionClientId;
        }

        if (string.IsNullOrWhiteSpace(introspectionClientSecret))
        {
            throw new InvalidOperationException(
                "Identity:IntrospectionClientSecret must be configured for reference token validation " +
                $"(or set environment variable {IntrospectionSecretEnvVar}).");
        }

        var useRedisActiveTokenValidation = options.UseRedisActiveTokenValidation;

        services.AddAuthentication(authOptions =>
            {
                authOptions.DefaultScheme = TokenSelectorScheme;
                authOptions.DefaultAuthenticateScheme = TokenSelectorScheme;
                authOptions.DefaultChallengeScheme = useRedisActiveTokenValidation ? RedisActiveTokenScheme : IntrospectionScheme;
            })
            .AddPolicyScheme(TokenSelectorScheme, "Reference token selector", policy =>
            {
                policy.ForwardDefaultSelector = context =>
                {
                    if (!options.UseRedisActiveTokenValidation)
                    {
                        return IntrospectionScheme;
                    }

                    var token = GetBearerTokenFromRequest(context.Request);
                    if (!string.IsNullOrWhiteSpace(token) && token.IndexOf('.') < 0)
                    {
                        return RedisActiveTokenScheme;
                    }

                    return IntrospectionScheme;
                };
            })
            .AddScheme<AuthenticationSchemeOptions, RedisActiveTokenAuthenticationHandler>(RedisActiveTokenScheme, _ => { })
            .AddOAuthIntrospection(IntrospectionScheme, introspection =>
            {
                introspection.Authority = identityAuthorityUri;
                introspection.RequireHttpsMetadata = false;
                DisableIntrospectionCachingIfSupported(introspection);
                introspection.ClientId = introspectionClientId;
                introspection.ClientSecret = introspectionClientSecret;
                introspection.Events = new OAuthIntrospectionEvents
                {
                    OnSendIntrospectionRequest = context =>
                    {
                        if (!authDiagnosticsEnabled)
                        {
                            return Task.CompletedTask;
                        }

                        var logger = context.HttpContext?.RequestServices?.GetService<ILoggerFactory>()?.CreateLogger("Auth.Introspection");
                        logger?.LogInformation(
                            "Introspection HTTP request to IDS. tokenLength={TokenLength}; cachingPolicyDisabled={CachingDisabled}",
                            context.Token?.Length ?? 0,
                            introspection.CachingPolicy == null);
                        return Task.CompletedTask;
                    },
                    OnCreateTicket = context =>
                    {
                        EnsureSessionClaimsFromIntrospectionPayload(context.Identity, context.Payload);
                        var sessionId = GetSessionIdFromClaims(context.Identity);
                        if (!string.IsNullOrWhiteSpace(sessionId))
                        {
                            context.HttpContext.Items[TokenSessionIdHttpContextItemKey] = sessionId;
                        }

                        if (authDiagnosticsEnabled)
                        {
                            var logger = context.HttpContext?.RequestServices?.GetService<ILoggerFactory>()?.CreateLogger("Auth.Introspection");
                            var payloadSession = context.Payload?.Value<string>(SessionControlOptions.CurrentSessionClaimType)
                                                 ?? context.Payload?.Value<string>(SessionControlOptions.SidClaimType);
                            var payloadActive = context.Payload?.Value<bool?>("active");
                            var claimTypes = context.Identity?.Claims?.Select(c => c.Type).Distinct().ToArray() ?? Array.Empty<string>();
                            logger?.LogInformation(
                                "Introspection CreateTicket. payloadActive={PayloadActive}; payloadSessionId={PayloadSessionId}; identitySessionId={IdentitySessionId}; claimTypes=[{ClaimTypes}]",
                                payloadActive, payloadSession ?? "null", sessionId ?? "null", string.Join(", ", claimTypes));
                        }

                        return Task.CompletedTask;
                    },
                    OnValidateToken = context =>
                    {
                        EnsureIdentityUserIdClaim(context.Principal);
                        EnsureSessionClaim(context.Principal);
                        var sessionId = GetSessionIdFromClaims(context.Principal);
                        if (!string.IsNullOrWhiteSpace(sessionId))
                        {
                            context.HttpContext.Items[TokenSessionIdHttpContextItemKey] = sessionId;
                        }

                        if (authDiagnosticsEnabled)
                        {
                            var logger = context.HttpContext?.RequestServices?.GetService<ILoggerFactory>()?.CreateLogger("Auth.Introspection");
                            var subjectId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.Principal?.FindFirst("sub")?.Value ?? "n/a";
                            var path = context.HttpContext?.Request?.Path.Value ?? "n/a";
                            var claimTypes = context.Principal?.Claims?.Select(c => c.Type).Distinct().ToArray() ?? Array.Empty<string>();
                            logger?.LogInformation(
                                "Introspection validated token. subject={SubjectId}; path={Path}; authType={AuthType}; tokenSessionId={TokenSessionId}; claimTypes=[{ClaimTypes}]",
                                subjectId, path, context.Principal?.Identity?.AuthenticationType ?? "n/a", sessionId ?? "null", string.Join(", ", claimTypes));
                        }

                        return Task.CompletedTask;
                    },
                    OnApplyChallenge = context =>
                    {
                        context.Response.Headers[IdentityChallengeHeader] = IdentityChallengeHeaderValue;
                        if (authDiagnosticsEnabled)
                        {
                            var logger = context.HttpContext?.RequestServices?.GetService<ILoggerFactory>()?.CreateLogger("Auth.Introspection");
                            logger?.LogWarning("Introspection challenge applied. path={Path}; statusCode={StatusCode}",
                                context.HttpContext?.Request?.Path.Value ?? "n/a", context.Response?.StatusCode);
                        }

                        return Task.CompletedTask;
                    }
                };

                if (introspectionAudiences != null)
                {
                    foreach (var aud in introspectionAudiences.Where(a => !string.IsNullOrWhiteSpace(a)))
                    {
                        introspection.Audiences.Add(aud.Trim());
                    }
                }
            });

        services.AddHttpClient("IdentityIntrospectionEnforcer", client =>
        {
            client.BaseAddress = identityAuthorityUri;
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions jwt, string identityAuthority, string[]? audiences)
    {
        if (!string.IsNullOrWhiteSpace(identityAuthority)) jwt.Authority = identityAuthority;
        jwt.RequireHttpsMetadata = false;
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = audiences is { Length: > 0 }, // без налаштованого audience перевірка вимикається (є попередження OIDC_AUDIENCE)
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            RequireExpirationTime = true,
            ValidAudiences = audiences
        };

        jwt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/api/hub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = _ => Task.CompletedTask,
            OnChallenge = context =>
            {
                context.Response.Headers[IdentityChallengeHeader] = IdentityChallengeHeaderValue;
                return Task.CompletedTask;
            }
        };
    }

    private static string? FirstNonWhiteSpace(params string?[]? values)
    {
        if (values == null)
        {
            return null;
        }

        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v;
            }
        }

        return null;
    }

    private static string[]? SplitBySemicolon(string? value) =>
        value?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? GetBearerTokenFromRequest(HttpRequest? request)
    {
        const string bearerPrefix = "Bearer ";
        var header = request?.Headers["Authorization"].ToString();
        if (!string.IsNullOrWhiteSpace(header) && header.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return header.Substring(bearerPrefix.Length).Trim();
        }

        return null;
    }

    private static void DisableIntrospectionCachingIfSupported(OAuthIntrospectionOptions introspection)
    {
        introspection.CachingPolicy = null;

        var optionsType = introspection.GetType();

        var enableCachingProperty = optionsType.GetProperty("EnableCaching", BindingFlags.Instance | BindingFlags.Public);
        if (enableCachingProperty?.CanWrite == true && enableCachingProperty.PropertyType == typeof(bool))
        {
            enableCachingProperty.SetValue(introspection, false);
        }

        var cacheDurationProperty = optionsType.GetProperty("CacheDuration", BindingFlags.Instance | BindingFlags.Public);
        if (cacheDurationProperty?.CanWrite == true && cacheDurationProperty.PropertyType == typeof(TimeSpan))
        {
            cacheDurationProperty.SetValue(introspection, TimeSpan.Zero);
        }
    }

    private static void EnsureIdentityUserIdClaim(ClaimsPrincipal? principal)
    {
        var identity = principal?.Identities?.FirstOrDefault(i => i.IsAuthenticated);
        if (identity == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value))
        {
            return;
        }

        var subject = identity.FindFirst("sub")?.Value ?? identity.FindFirst("subject")?.Value ?? identity.FindFirst("nameid")?.Value;
        if (!string.IsNullOrWhiteSpace(subject))
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subject));
        }
    }

    private static void EnsureSessionClaim(ClaimsPrincipal? principal)
    {
        var identity = principal?.Identities?.FirstOrDefault(i => i.IsAuthenticated);
        if (identity == null)
        {
            return;
        }

        EnsureSessionClaimsOnIdentity(identity, GetSessionIdFromClaims(principal));
    }

    private static void EnsureSessionClaimsFromIntrospectionPayload(ClaimsIdentity? identity, JObject? payload)
    {
        if (identity == null || payload == null)
        {
            return;
        }

        var sessionId = payload.Value<string>(SessionControlOptions.CurrentSessionClaimType) ?? payload.Value<string>(SessionControlOptions.SidClaimType);
        EnsureSessionClaimsOnIdentity(identity, sessionId);
    }

    private static void EnsureSessionClaimsOnIdentity(ClaimsIdentity? identity, string? sessionId)
    {
        if (identity == null || string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(identity.FindFirst(SessionControlOptions.CurrentSessionClaimType)?.Value))
        {
            identity.AddClaim(new Claim(SessionControlOptions.CurrentSessionClaimType, sessionId));
        }

        if (string.IsNullOrWhiteSpace(identity.FindFirst(SessionControlOptions.SidClaimType)?.Value))
        {
            identity.AddClaim(new Claim(SessionControlOptions.SidClaimType, sessionId));
        }
    }

    private static string? GetSessionIdFromClaims(ClaimsPrincipal? principal)
    {
        if (principal == null)
        {
            return null;
        }

        foreach (var identity in principal.Identities)
        {
            var sessionId = GetSessionIdFromClaims(identity);
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                return sessionId;
            }
        }

        return principal.Claims.FirstOrDefault(IsSessionClaim)?.Value;
    }

    private static string? GetSessionIdFromClaims(ClaimsIdentity? identity)
    {
        if (identity == null)
        {
            return null;
        }

        var sessionId = identity.FindFirst(SessionControlOptions.CurrentSessionClaimType)?.Value ?? identity.FindFirst(SessionControlOptions.SidClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            return sessionId;
        }

        return identity.Claims.FirstOrDefault(IsSessionClaim)?.Value;
    }

    private static bool IsSessionClaim(Claim c) =>
        c.Type != null &&
        (c.Type.Equals(SessionControlOptions.CurrentSessionClaimType, StringComparison.Ordinal) ||
         c.Type.Equals(SessionControlOptions.SidClaimType, StringComparison.Ordinal) ||
         c.Type.IndexOf("current_session_id", StringComparison.OrdinalIgnoreCase) >= 0);
}
