// =============================================================================
// Сповіщення пацієнтів (ТЗ: «Сповіщення»): параметри каналів, статуси, шаблони, маскування одержувача.
// Секрети (пароль SMTP, ключі API каналів) читаються лише з конфігурації/змінних середовища (Notify__Email__Password тощо),
// у appsettings мають бути порожні; у відповіді API й журнали не потрапляють.
// =============================================================================
namespace MedLink.LIS.Api.Services.Notifications;

public static class NotifyChannels
{
    public const string Email = "EMAIL", Sms = "SMS", Viber = "VIBER", Telegram = "TELEGRAM";
    public static readonly string[] All = { Email, Sms, Viber, Telegram };
    public static string Label(string c) => c switch { Email => "Електронна пошта", Sms => "SMS", Viber => "Viber", Telegram => "Telegram", "PUSH" => "Push", _ => c };
    /// <summary>Ключі конфігурації, які треба задати для каналу (лише назви, без значень).</summary>
    public static string[] ConfigKeys(string c)
    {
        if (c == Email) return new[] { "Notify:Email:Host", "Notify:Email:From", "Notify:Email:User", "Notify:Email:Password" };
        var name = char.ToUpperInvariant(c[0]) + c[1..].ToLowerInvariant();
        return new[] { $"Notify:{name}:Url", $"Notify:{name}:ApiKey" };
    }
}

public static class NotifyStatuses
{
    public const string Queued = "QUEUED", Sending = "SENDING", Sent = "SENT", Failed = "FAILED", Skipped = "SKIPPED", NotConfigured = "NOT_CONFIGURED";
    public static readonly string[] All = { Queued, Sending, Sent, Failed, Skipped, NotConfigured };
    public static string Label(string s) => s switch
    {
        Queued => "У черзі", Sending => "Відправляється", Sent => "Надіслано", Failed => "Помилка", Skipped => "Пропущено", NotConfigured => "Не налаштовано", _ => s
    };
    /// <summary>Повтор вручну доступний для підсумкових станів, крім «Надіслано».</summary>
    public static bool CanRetry(string s) => s is Failed or Skipped or NotConfigured;
}

public static class NotifyReasons
{
    public const string NotConfigured = "NOT_CONFIGURED", ChannelOff = "CHANNEL_OFF", NoConsent = "NO_CONSENT", NoContact = "NO_CONTACT", PatientMissing = "PATIENT_MISSING", Unsupported = "UNSUPPORTED_CHANNEL", Superseded = "SUPERSEDED";
    public static string Label(string? r) => r switch
    {
        NotConfigured => "Канал не налаштовано", ChannelOff => "Канал вимкнено адміністратором", NoConsent => "Немає згоди пацієнта на сповіщення",
        NoContact => "У пацієнта немає контакту для цього каналу", PatientMissing => "Пацієнта не знайдено", Unsupported => "Канал не підтримується", Superseded => "Замінено підсумковим сповіщенням", _ => ""
    };
}

public static class NotifyTemplates
{
    public const string ResultReady = "RESULT_READY", ResultPartial = "RESULT_PARTIAL";

    /// <summary>Текст повідомлення: лише факт готовності та посилання на портал; жодних результатів, назв тестів чи діагнозів.</summary>
    public static (string Subject, string Body) Render(string? code, string channel, string link)
    {
        var partial = code == ResultPartial;
        if (channel == NotifyChannels.Email)
            return (partial ? "Нові результати лабораторних досліджень" : "Результати лабораторних досліджень готові",
                (partial ? "Добрий день! Готові нові результати ваших лабораторних досліджень." : "Добрий день! Результати ваших лабораторних досліджень готові.")
                + $"\n\nПереглянути їх можна за посиланням: {link}\n\nЦе автоматичне повідомлення, відповідати на нього не потрібно.");
        return ("", partial ? $"Лабораторія: готові нові результати. Перегляд: {link}" : $"Лабораторія: результати готові. Перегляд: {link}");
    }
}

public static class NotifyMask
{
    public static string? Recipient(string channel, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (channel == NotifyChannels.Email)
        {
            var at = v.IndexOf('@');
            return at <= 1 ? "***" + (at >= 0 ? v[at..] : "") : v[0] + "***" + v[at..];
        }
        return v.Length <= 4 ? "***" : new string('*', Math.Min(6, v.Length - 4)) + v[^4..];
    }

    /// <summary>Номер для SMS/месенджерів: лише цифри та провідний «+».</summary>
    public static string NormalizePhone(string v)
    {
        var digits = new string(v.Where(char.IsDigit).ToArray());
        return string.IsNullOrEmpty(digits) ? "" : "+" + digits;
    }
}

public sealed class NotifyEmailOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? User { get; set; }
    /// <summary>Секрет: лише змінна середовища Notify__Email__Password / сховище секретів.</summary>
    public string? Password { get; set; }
    public string? From { get; set; }
    public string? FromName { get; set; }
    public int TimeoutSeconds { get; set; } = 20;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

public sealed class NotifyHttpChannelOptions
{
    /// <summary>Адреса HTTP-шлюзу каналу (POST JSON). Порожньо — канал вимкнено.</summary>
    public string? Url { get; set; }
    /// <summary>Секрет: передається як Authorization: Bearer; лише змінна середовища.</summary>
    public string? ApiKey { get; set; }
    /// <summary>Підпис відправника (alpha-name) для шлюзу.</summary>
    public string? Sender { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public bool IsConfigured => Uri.TryCreate(Url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}

public sealed class NotifyOptions
{
    /// <summary>Фонова служба. Типово увімкнена; у середовищі Testing вимикається, якщо не задано явно.</summary>
    public bool? WorkerEnabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
    public int BackoffBaseSeconds { get; set; } = 30;
    public int BackoffMaxSeconds { get; set; } = 3600;
    public int SendingTimeoutMinutes { get; set; } = 5;
    public NotifyEmailOptions Email { get; set; } = new();
    public NotifyHttpChannelOptions Sms { get; set; } = new();
    public NotifyHttpChannelOptions Viber { get; set; } = new();
    public NotifyHttpChannelOptions Telegram { get; set; } = new();

    public NotifyHttpChannelOptions? Http(string channel) => channel switch { NotifyChannels.Sms => Sms, NotifyChannels.Viber => Viber, NotifyChannels.Telegram => Telegram, _ => null };

    /// <summary>Витримка перед повтором: base·2^(attempt-1), не більше max.</summary>
    public TimeSpan RetryDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Min((double)BackoffMaxSeconds, BackoffBaseSeconds * Math.Pow(2, Math.Max(0, attempt - 1))));
}
