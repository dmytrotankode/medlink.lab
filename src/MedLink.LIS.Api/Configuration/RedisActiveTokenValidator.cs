using System.Text.Json;
using StackExchange.Redis;

namespace MedLink.LIS.Api.Configuration;

internal enum RedisActiveTokenValidationStatus
{
    Miss,
    Inactive,
    Success
}

internal sealed class RedisActiveTokenValidationResult
{
    public RedisActiveTokenValidationStatus Status { get; set; }

    public string? SubjectId { get; set; }
}

internal static class RedisActiveTokenValidator
{
    public static async Task<RedisActiveTokenValidationResult> ValidateAsync(
        string? accessToken,
        HybridTokenAuthOptions? options,
        IDatabase? redis)
    {
        var result = new RedisActiveTokenValidationResult
        {
            Status = RedisActiveTokenValidationStatus.Miss
        };

        if (options == null || redis == null || string.IsNullOrWhiteSpace(accessToken))
        {
            return result;
        }

        var tokenHash = ActiveTokenHashHelper.Compute(accessToken);
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return result;
        }

        var subjectId = await ReadRedisStringAsync(redis, BuildTokenByHashKey(options, tokenHash)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return result;
        }

        var activeHash = await ReadRedisStringAsync(redis, BuildActiveTokenKey(options, subjectId)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(activeHash) ||
            !string.Equals(activeHash, tokenHash, StringComparison.Ordinal))
        {
            result.Status = RedisActiveTokenValidationStatus.Inactive;
            result.SubjectId = subjectId;
            return result;
        }

        result.Status = RedisActiveTokenValidationStatus.Success;
        result.SubjectId = subjectId;
        return result;
    }

    private static async Task<string?> ReadRedisStringAsync(IDatabase redis, string key)
    {
        var value = await redis.StringGetAsync(key).ConfigureAwait(false);
        if (!value.HasValue)
        {
            return null;
        }

        var raw = value.ToString();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        return raw.StartsWith("\"", StringComparison.Ordinal)
            ? JsonSerializer.Deserialize<string>(raw)
            : raw;
    }

    internal static string BuildActiveTokenKey(HybridTokenAuthOptions options, string subjectId)
    {
        var prefix = options.RedisActiveTokenKeyPrefix;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = SessionControlOptions.DefaultActiveTokenKeyPrefix;
        }

        return $"{prefix}{subjectId}";
    }

    internal static string BuildTokenByHashKey(HybridTokenAuthOptions options, string tokenHash)
    {
        var prefix = options.RedisTokenByHashKeyPrefix;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = SessionControlOptions.DefaultTokenByHashKeyPrefix;
        }

        return $"{prefix}{tokenHash}";
    }
}

/// <summary>Доступ до Redis Identity: підключення створюється ліниво при першому reference-токені.</summary>
public interface IRedisDatabaseProvider
{
    IDatabase? Database { get; }
}

internal sealed class RedisDatabaseProvider : IRedisDatabaseProvider, IDisposable
{
    private readonly Lazy<IConnectionMultiplexer?> _connection;

    public RedisDatabaseProvider(Microsoft.Extensions.Options.IOptions<HybridTokenAuthOptions> options)
    {
        var cs = options.Value.RedisConnectionString;
        _connection = new Lazy<IConnectionMultiplexer?>(() => string.IsNullOrWhiteSpace(cs) ? null : ConnectionMultiplexer.Connect(cs));
    }

    public IDatabase? Database => _connection.Value?.GetDatabase();

    public void Dispose()
    {
        if (_connection.IsValueCreated) _connection.Value?.Dispose();
    }
}
