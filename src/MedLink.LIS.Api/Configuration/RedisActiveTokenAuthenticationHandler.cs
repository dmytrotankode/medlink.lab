using System.Security.Claims;
using System.Text.Encodings.Web;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MedLink.LIS.Api.Configuration;

public sealed class RedisActiveTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IRedisDatabaseProvider _redisDatabaseProvider;
    private readonly HybridTokenAuthOptions _options;

    public RedisActiveTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IRedisDatabaseProvider redisDatabaseProvider,
        IOptions<HybridTokenAuthOptions> hybridOptions)
        : base(options, logger, encoder)
    {
        _redisDatabaseProvider = redisDatabaseProvider;
        _options = hybridOptions?.Value ?? new HybridTokenAuthOptions();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (_options.ForceOnlineIntrospectionPerRequest || !_options.UseRedisActiveTokenValidation)
        {
            return await Context.AuthenticateAsync(AuthenticationExtensions.IntrospectionScheme).ConfigureAwait(false);
        }

        var token = GetBearerToken(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        if (token.IndexOf('.') >= 0)
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            var validation = await RedisActiveTokenValidator
                .ValidateAsync(token, _options, _redisDatabaseProvider.Database)
                .ConfigureAwait(false);

            if (validation.Status == RedisActiveTokenValidationStatus.Success)
            {
                LogDiag("Redis active token authenticated. subject={SubjectId}; path={Path}", validation.SubjectId, Request.Path.Value);
                return AuthenticateResult.Success(CreateTicket(validation.SubjectId!));
            }

            if (validation.Status == RedisActiveTokenValidationStatus.Inactive)
            {
                LogDiag("Redis active token mismatch. subject={SubjectId}; path={Path}", validation.SubjectId, Request.Path.Value);
                return AuthenticateResult.Fail(ApiUnauthorizedResponseWriter.UnauthorizedDetail);
            }

            LogDiag("Redis active token miss; fallback={Fallback}. path={Path}", _options.RedisActiveTokenFallbackToIntrospection, Request.Path.Value);

            if (!_options.RedisActiveTokenFallbackToIntrospection)
            {
                return AuthenticateResult.Fail(ApiUnauthorizedResponseWriter.UnauthorizedDetail);
            }

            return await Context.AuthenticateAsync(AuthenticationExtensions.IntrospectionScheme).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Redis token authentication failed for path={Path}", Request.Path.Value);
            if (!_options.RedisActiveTokenFallbackToIntrospection)
            {
                return AuthenticateResult.Fail(ApiUnauthorizedResponseWriter.UnauthorizedDetail);
            }

            return await Context.AuthenticateAsync(AuthenticationExtensions.IntrospectionScheme).ConfigureAwait(false);
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        return ApiUnauthorizedResponseWriter.WriteAsync(Response, StatusCodes.Status401Unauthorized);
    }

    private AuthenticationTicket CreateTicket(string subjectId)
    {
        var claims = new[]
        {
            new Claim("sub", subjectId),
            new Claim(ClaimTypes.NameIdentifier, subjectId)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.NameIdentifier, null);
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, Scheme.Name);
    }

    private void LogDiag(string message, params object?[] args)
    {
        if (_options.AuthDiagnosticsEnabled)
        {
            Logger.LogInformation(message, args);
        }
        else
        {
            Logger.LogDebug(message, args);
        }
    }

    private static string? GetBearerToken(HttpRequest request)
    {
        const string bearerPrefix = "Bearer ";
        var header = request.Headers["Authorization"].ToString();
        if (!string.IsNullOrWhiteSpace(header) &&
            header.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return header.Substring(bearerPrefix.Length).Trim();
        }

        if (request.Path.StartsWithSegments("/api/hub"))
        {
            var queryToken = request.Query["access_token"].ToString();
            if (!string.IsNullOrWhiteSpace(queryToken))
            {
                return queryToken;
            }
        }

        return null;
    }
}
