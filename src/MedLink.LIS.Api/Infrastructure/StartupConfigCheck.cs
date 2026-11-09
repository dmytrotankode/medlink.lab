// Перевірка конфігурації при старті: у виробничому середовищі значення-заглушки (Lab:VerifySecret, Lab:PublicBaseUrl) і відсутність автентифікації
// небезпечні. Система НЕ падає, але гучно попереджає в журналі та в /health (поле configWarnings).
namespace MedLink.LIS.Api.Infrastructure;

public sealed record ConfigWarning(string Code, string Message);

public static class AuthModes
{
    /// <summary>Без автентифікації: заголовок співробітника ігнорується, використовується Lab:DefaultEmployeeId.</summary>
    public const string None = "None";
    /// <summary>Співробітник береться із заголовка X-MedLink-Employee-Id (довіра до заголовка; лише за зворотним проксі МІС).</summary>
    public const string Header = "Header";
    /// <summary>Вхід через сервер Identity MedLink: JWT/reference-токен у заголовку Authorization, співробітник і роль — з МІС (Medlink:*).</summary>
    public const string Oidc = "Oidc";

    public static string Resolve(IConfiguration config)
    {
        var raw = config["Auth:Mode"];
        if (string.Equals(raw, Oidc, StringComparison.OrdinalIgnoreCase)) return Oidc;
        return string.Equals(raw, Header, StringComparison.OrdinalIgnoreCase) ? Header : None; // невідоме значення = безпечніший None
    }
}

public static class StartupConfigCheck
{
    private static readonly string[] Placeholders = { "change-me", "changeme", "secret", "password", "test", "default", "example" };

    public static List<ConfigWarning> Evaluate(IConfiguration config, IHostEnvironment env)
    {
        var list = new List<ConfigWarning>();
        if (env.IsDevelopment()) return list;

        var secret = config["Lab:VerifySecret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 16 || Placeholders.Any(p => secret.Contains(p, StringComparison.OrdinalIgnoreCase)))
            list.Add(new("VERIFY_SECRET", "Lab:VerifySecret порожній, короткий або є заглушкою: підписи перевірки бланків (QR) можна підробити. Задайте випадковий секрет довжиною від 32 символів у змінній середовища Lab__VerifySecret."));

        var url = config["Lab:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var u) || u.IsLoopback || u.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            list.Add(new("PUBLIC_BASE_URL", "Lab:PublicBaseUrl не задано або вказує на localhost: QR-коди бланків і посилання в сповіщеннях пацієнтам не відкриються. Вкажіть публічну адресу системи."));
        else if (u.Scheme != Uri.UriSchemeHttps)
            list.Add(new("PUBLIC_BASE_URL_HTTP", "Lab:PublicBaseUrl використовує HTTP без шифрування: посилання на результати пацієнтів передаватимуться відкрито."));

        var mode = AuthModes.Resolve(config);
        if (mode == AuthModes.None)
            list.Add(new("AUTH_NONE", "Auth:Mode = None: автентифікації користувачів немає, усі запити виконуються від імені співробітника за замовчуванням. Увімкніть автентифікацію МІС перед промисловою експлуатацією (docs/AUTH_PLAN.md)."));
        else if (mode == AuthModes.Oidc)
            list.AddRange(EvaluateOidc(config));
        else
            list.Add(new("AUTH_HEADER", "Auth:Mode = Header: співробітник береться із заголовка запиту без перевірки — будь-хто може підставити чужий ідентифікатор. Допустимо лише за довіреним зворотним проксі МІС (docs/AUTH_PLAN.md)."));
        return list;
    }

    /// <summary>Режим Oidc: усе, що робить вхід недіючим або слабшим, ніж очікується.</summary>
    public static List<ConfigWarning> EvaluateOidc(IConfiguration config)
    {
        var list = new List<ConfigWarning>();
        var authority = config["Identity:Authority"];
        if (string.IsNullOrWhiteSpace(authority))
            list.Add(new("OIDC_AUTHORITY", "Auth:Mode = Oidc, але Identity:Authority не задано: токени неможливо перевірити, усі запити до API отримуватимуть 401."));
        else if (!authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            list.Add(new("OIDC_AUTHORITY_HTTP", "Identity:Authority вказує не на HTTPS: ключі підпису токенів завантажуються без шифрування."));
        if (string.IsNullOrWhiteSpace(config["Identity:JwtBearerAudience"]))
            list.Add(new("OIDC_AUDIENCE", "Identity:JwtBearerAudience не задано: очікується IdentityApiScope;MisApiScope, інакше токени з audience відхиляються."));
        if (string.IsNullOrWhiteSpace(config["Medlink:ConnectionString"]))
            list.Add(new("MEDLINK_BRIDGE_OFF", "Medlink:ConnectionString не задано: міст до МІС вимкнено, співробітник береться з власної бази за claim (Auth:Oidc:EmployeeIdClaim або sub). Для роботи в складі МІС задайте підключення лише для читання."));
        else if (string.IsNullOrWhiteSpace(config["Medlink:DefaultLabRole"]) && !config.GetSection("Medlink:ProfileRoles").GetChildren().Any())
            list.Add(new("MEDLINK_NO_ROLES", "Medlink:ProfileRoles порожній і Medlink:DefaultLabRole не задано: жоден працівник МІС не матиме доступу до Лабораторії (403)."));
        list.Add(new("OIDC_PORTAL_OPEN", "Кабінет пацієнта (/api/v1/lab/portal/*) лишається без входу співробітника: потрібна окрема схема доступу пацієнта (docs/AUTH_PLAN.md п.5), до цього не виставляйте його назовні."));
        return list;
    }

    public static void LogWarnings(ILogger logger, IReadOnlyCollection<ConfigWarning> warnings)
    {
        if (warnings.Count == 0) return;
        logger.LogWarning("!!! НЕБЕЗПЕЧНА КОНФІГУРАЦІЯ ВИРОБНИЧОГО СЕРЕДОВИЩА: {Count} попереджень. Система працює, але це потрібно виправити.", warnings.Count);
        foreach (var w in warnings) logger.LogWarning("!!! [{Code}] {Message}", w.Code, w.Message);
    }
}
