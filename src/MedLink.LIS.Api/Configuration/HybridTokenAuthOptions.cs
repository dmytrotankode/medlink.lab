namespace MedLink.LIS.Api.Configuration;

public class HybridTokenAuthOptions
{
    /// <summary>Допустимі audience JWT через «;» (напр. IdentityApiScope;MisApiScope).</summary>
    public string? JwtBearerAudience { get; set; }

    public string? Authority { get; set; }

    public string? IntrospectionClientId { get; set; }

    public string? IntrospectionClientSecret { get; set; }

    public string? IntrospectionAudiences { get; set; }

    public bool UseRefreshTokenFlow { get; set; }

    public bool ForceOnlineIntrospectionPerRequest { get; set; }

    /// <summary>Перевіряти reference-токени (без крапки) за записами Redis Identity. Потребує <see cref="RedisConnectionString"/>.</summary>
    public bool UseRedisActiveTokenValidation { get; set; }

    /// <summary>Рядок підключення до Redis, який наповнює сервер Identity (IDS:ACTIVE_TOKEN:*). Секрет — лише зі змінних середовища.</summary>
    public string? RedisConnectionString { get; set; }

    public string RedisActiveTokenKeyPrefix { get; set; } = SessionControlOptions.DefaultActiveTokenKeyPrefix;

    public string RedisTokenByHashKeyPrefix { get; set; } = SessionControlOptions.DefaultTokenByHashKeyPrefix;

    public bool RedisActiveTokenFallbackToIntrospection { get; set; } = true;

    public bool AuthDiagnosticsEnabled { get; set; }
}
