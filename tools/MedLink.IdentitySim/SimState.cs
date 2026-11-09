// Стан імітатора в пам'яті: коди авторизації (одноразові, короткі), refresh-токени (обертаються), підписані квитки форм входу.
// Перезапуск служби скидає коди й refresh-токени (користувач увійде знову); підписані токени доступу лишаються чинними (ключ у файлі).
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace MedLink.IdentitySim;

public sealed record AuthRequest(string ClientId, string RedirectUri, string Scope, string? State, string? Nonce, string Challenge);

public sealed record IssuedCode(AuthRequest Request, string Sub, string? ProfileId, string Sid, long AuthTime, DateTimeOffset Expires);

public sealed record RefreshEntry(string ClientId, string Scope, string Sub, string? ProfileId, string Sid, long AuthTime, DateTimeOffset Expires);

public sealed class SimState
{
    private readonly ConcurrentDictionary<string, IssuedCode> _codes = new();
    private readonly ConcurrentDictionary<string, RefreshEntry> _refresh = new();
    private readonly byte[] _secret = RandomNumberGenerator.GetBytes(32);
    private readonly ConcurrentQueue<DateTimeOffset> _failures = new();
    private readonly TimeProvider _time;

    public SimState(TimeProvider time) => _time = time;

    public static string NewToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public string PutCode(IssuedCode c) { Sweep(); var k = NewToken(); _codes[k] = c; return k; }
    public IssuedCode? TakeCode(string code) => _codes.TryRemove(code, out var c) ? c : null; // одноразовий

    public string PutRefresh(RefreshEntry e) { var k = NewToken(); _refresh[k] = e; return k; }
    public RefreshEntry? TakeRefresh(string token) => _refresh.TryRemove(token, out var e) ? e : null; // обертання: старий недійсний
    public void RevokeSession(string sid) { foreach (var kv in _refresh.Where(k => k.Value.Sid == sid).ToList()) _refresh.TryRemove(kv.Key, out _); }

    private void Sweep()
    {
        var now = _time.GetUtcNow();
        foreach (var kv in _codes.Where(k => k.Value.Expires < now).ToList()) _codes.TryRemove(kv.Key, out _);
        foreach (var kv in _refresh.Where(k => k.Value.Expires < now).ToList()) _refresh.TryRemove(kv.Key, out _);
    }

    /// <summary>Не більше 15 невдалих спроб входу на хвилину в усьому сервісі (прості запобіжники від добору пароля стенда).</summary>
    public bool TooManyFailures()
    {
        var now = _time.GetUtcNow();
        while (_failures.TryPeek(out var t) && now - t > TimeSpan.FromMinutes(1)) _failures.TryDequeue(out _);
        return _failures.Count >= 15;
    }
    public void RegisterFailure() => _failures.Enqueue(_time.GetUtcNow());

    // ----- підписані квитки між кроками форми (без cookie): HMAC-SHA256 з випадковим ключем процесу -----
    public string Seal<T>(T payload)
    {
        var body = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        return body + "." + Base64UrlEncoder.Encode(HMACSHA256.HashData(_secret, Encoding.ASCII.GetBytes(body)));
    }

    public T? Unseal<T>(string? ticket) where T : class
    {
        var parts = (ticket ?? "").Split('.');
        if (parts.Length != 2) return null;
        var expected = HMACSHA256.HashData(_secret, Encoding.ASCII.GetBytes(parts[0]));
        byte[] given;
        try { given = Base64UrlEncoder.DecodeBytes(parts[1]); } catch { return null; }
        if (!CryptographicOperations.FixedTimeEquals(expected, given)) return null;
        try { return JsonSerializer.Deserialize<T>(Base64UrlEncoder.DecodeBytes(parts[0])); } catch { return null; }
    }
}

/// <summary>Квиток кроку «вхід → профіль»: початковий запит, обраний користувач і термін дії.</summary>
public sealed record LoginTicket(AuthRequest Request, string? Sub, long Exp);
