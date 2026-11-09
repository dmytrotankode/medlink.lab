// =============================================================================
// Налаштування інтеграції з ЕСОЗ (секція Ehealth у appsettings). Основна схема: ЛІС викликає eHealth-сервіс (проксі) MedLink, який
// тримає токени й секрети та сам звертається до ЕСОЗ (Ehealth:Mode = MedLinkProxy, секція Ehealth:Proxy). Секрети (Proxy:ApiKey,
// застарілі AccessToken/ApiKey/CustomPsk) зберігаються лише в конфігурації сервера/сховищі секретів і не потрапляють у відповіді API та журнали.
// =============================================================================
namespace MedLink.LIS.Api.Services.Ehealth;

public sealed class EhealthLiveOptions
{
    /// <summary>Поопераційні прапорці режиму Live (типово все вимкнено): вмикайте по одній, починаючи з SearchRead.</summary>
    public EhealthOperationFlags Operations { get; set; } = new();
}

public sealed class EhealthOperationFlags
{
    public bool SearchRead { get; set; }
    public bool Use { get; set; }
    public bool Complete { get; set; }
    public bool SubmitDr { get; set; }
    public bool CancelDr { get; set; }
    public bool Specimen { get; set; }
    public bool Equipment { get; set; }

    public bool IsEnabled(EhealthOperation op) => op switch
    {
        EhealthOperation.SearchRead => SearchRead, EhealthOperation.Use => Use, EhealthOperation.Complete => Complete, EhealthOperation.SubmitDr => SubmitDr,
        EhealthOperation.CancelDr => CancelDr, EhealthOperation.Specimen => Specimen, EhealthOperation.Equipment => Equipment, _ => false
    };
}

/// <summary>Підключення до eHealth-сервісу (проксі) MedLink. Контракт — docs/EHEALTH_MEDLINK_PROXY_CONTRACT.md.</summary>
public sealed class EhealthProxyOptions
{
    /// <summary>Базова адреса проксі (без префікса /api). Порожньо в appsettings: значення дає команда проксі через змінну середовища Ehealth__Proxy__BaseUrl.</summary>
    public string? BaseUrl { get; set; }
    /// <summary>Заголовок ClientId: id організації (legal_entity) в ЕСОЗ. Порожньо — береться Ehealth:LegalEntityId.</summary>
    public string? ClientId { get; set; }
    /// <summary>Роль користувача в ЕСОЗ для авторизації (auth/EhealthUILink/{role}); типово DOCTOR (як у МІС).</summary>
    public string Role { get; set; } = "DOCTOR";
    /// <summary>Адреса сторінки авторизації ЕСОЗ (UIUri в МІС), на яку перенаправляється користувач для входу. Порожньо — вхід недоступний.</summary>
    public string? AuthPortalUrl { get; set; }
    /// <summary>Зареєстрований redirect_uri клієнта (сторінка ЛІС, що приймає ?code=…). Порожньо — береться redirect_uri з відповіді проксі.</summary>
    public string? RedirectUri { get; set; }
    /// <summary>Чи заклад пройшов реорганізацію (параметр isReorganized в auth/EhealthUILink).</summary>
    public bool IsReorganized { get; set; }
    /// <summary>Службовий UserId для викликів без користувача (довідники, перевірка підключення). Порожньо — такі виклики вимагають користувача.</summary>
    public string? ServiceUserId { get; set; }
    /// <summary>Поопераційні прапорці (типово все вимкнено): вмикайте по одній, починаючи з SearchRead.</summary>
    public EhealthOperationFlags Operations { get; set; } = new();
}

public sealed class EhealthHttpOptions
{
    /// <summary>Таймаут одного запиту, с (ТВ 1.1.3: не менше 15 с для синхронних запитів).</summary>
    public int TimeoutSeconds { get; set; } = 30;
    /// <summary>Повтори лише для ідемпотентних викликів (GET): кількість додаткових спроб.</summary>
    public int MaxRetries { get; set; } = 2;
    /// <summary>Початкова пауза між повторами, мс (подвоюється).</summary>
    public int RetryBaseDelayMs { get; set; } = 500;
    /// <summary>Скільки секунд чекати завершення job у синхронному сценарії (use/release при реєстрації замовлення).</summary>
    public int SyncJobWaitSeconds { get; set; } = 10;
    /// <summary>Паузи опитування job у синхронному сценарії, мс (порожньо — 300, 700, 1500, 3000, 5000).</summary>
    public int[] SyncJobPollDelaysMs { get; set; } = Array.Empty<int>();
}

public sealed class EhealthOutboxOptions
{
    /// <summary>Фоновий воркер. Типово увімкнений; у середовищі Testing вимикається, якщо не задано явно.</summary>
    public bool? WorkerEnabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 5;
    /// <summary>Скільки задач виконується паралельно в одному екземплярі воркера.</summary>
    public int MaxParallel { get; set; } = 4;
    /// <summary>Спроб відправки (мережа/5xx) до переходу в DEAD.</summary>
    public int MaxAttempts { get; set; } = 8;
    public int BackoffBaseSeconds { get; set; } = 30;
    public int BackoffMaxSeconds { get; set; } = 1800;
    /// <summary>Розкид витримки, частка (0 — без розкиду; для тестів).</summary>
    public double BackoffJitter { get; set; } = 0.1;
    /// <summary>Запис у стані SENDING довше цього часу вважається покинутим (збій воркера) і повертається в чергу.</summary>
    public int SendingTimeoutMinutes { get; set; } = 5;
    /// <summary>Скільки годин очікувати завершення job до переходу в FAILED.</summary>
    public int JobDeadlineHours { get; set; } = 24;
    /// <summary>Паузи опитування job, с: 2/5/10/30, далі кожні PollMaxSeconds (ТВ 1.1.4).</summary>
    public int[] PollDelaysSeconds { get; set; } = { 2, 5, 10, 30 };
    public int PollMaxSeconds { get; set; } = 120;
}

public sealed class EhealthReplayOptions
{
    /// <summary>Каталог із фікстурами *.json (типово Fixtures/ehealth поруч зі збіркою).</summary>
    public string? FixturesPath { get; set; }
}

public sealed class EhealthMockOptions
{
    /// <summary>Скільки разів job повертає pending, перш ніж стати processed.</summary>
    public int JobPendingPolls { get; set; } = 1;
    /// <summary>Одноразово наповнити реєстр мока е-направленнями з демо-даних дзеркала (лише для демонстрації).</summary>
    public bool SeedFromMirror { get; set; } = true;
    /// <summary>true — двійник вимагає збереженого токена користувача (як реальний проксі: без входу 401 token_not_found); false (типово) — поблажливий режим демо.
    /// Перевірка наявності токена перед відправкою звіту (auth/hasAccessToken) від цього прапорця не залежить.</summary>
    public bool RequireSession { get; set; }
}

public sealed class EhealthOptions
{
    /// <summary>Mock (двійник проксі, типово) | MedLinkProxy (реальний eHealth-сервіс MedLink) | Replay (фікстури проксі) | Live (ЗАСТАРІЛО: прямий ЕСОЗ).</summary>
    public string Mode { get; set; } = EhealthModes.Mock;
    /// <summary>ЗАСТАРІЛО (лише Mode = Live): адреса прямого API ЕСОЗ. Замовник вимагає працювати через проксі MedLink — використовуйте Proxy:BaseUrl.</summary>
    public string BaseUrl { get; set; } = "https://api.ehealth.gov.ua/api/";
    /// <summary>ЗАСТАРІЛО (лише Live): статичний access token. Не журналюється.</summary>
    public string? AccessToken { get; set; }
    /// <summary>ЗАСТАРІЛО (лише Live).</summary>
    public string? ApiKey { get; set; }
    /// <summary>ЗАСТАРІЛО (лише Live): заголовок X-Custom-PSK шлюзу ЕСОЗ. У схемі через проксі ці секрети тримає проксі.</summary>
    public string? CustomPsk { get; set; }
    public EhealthProxyOptions Proxy { get; set; } = new();
    public int PageSize { get; set; } = 50;

    /// <summary>Ідентифікатори закладу/підрозділу/працівника в ЕСОЗ (для Live; у Mock-режимі генеруються детерміновано).</summary>
    public string? LegalEntityId { get; set; }
    public string? DivisionId { get; set; }
    public string? DefaultEmployeeId { get; set; }
    /// <summary>Код категорії діагностичного звіту з довідника eHealth/diagnostic_report_categories (звірити з GET /api/dictionaries).</summary>
    public string DiagnosticReportCategoryCode { get; set; } = "LAB";
    /// <summary>Чи викликати use для е-направлень без медичної програми (за схемою program обов'язкова — на preprod не підтверджено).</summary>
    public bool UseWithoutProgram { get; set; }

    public EhealthLiveOptions Live { get; set; } = new();
    public EhealthHttpOptions Http { get; set; } = new();
    public EhealthOutboxOptions Outbox { get; set; } = new();
    public EhealthReplayOptions Replay { get; set; } = new();
    public EhealthMockOptions Mock { get; set; } = new();

    /// <summary>ЗАСТАРІЛИЙ режим прямого виклику ЕСОЗ (Bearer-токен, шляхи OpenAPI). Збережено для довідки; для роботи не використовувати.</summary>
    public bool IsLive => string.Equals(Mode, EhealthModes.Live, StringComparison.OrdinalIgnoreCase);
    /// <summary>Реальний eHealth-сервіс (проксі) MedLink.</summary>
    public bool IsProxy => string.Equals(Mode, EhealthModes.MedLinkProxy, StringComparison.OrdinalIgnoreCase);
    public bool IsReplay => string.Equals(Mode, EhealthModes.Replay, StringComparison.OrdinalIgnoreCase);
    public bool IsMock => !IsLive && !IsProxy && !IsReplay;
    /// <summary>Mock або Replay: проксі й ЕСОЗ імітуються (тестовий вхід, тестовий підпис, мок-реєстр/фікстури).</summary>
    public bool Simulated => IsMock || IsReplay;
    /// <summary>Реальне зовнішнє з'єднання (проксі або застарілий прямий ЕСОЗ): операції вмикаються прапорцями.</summary>
    public bool IsReal => IsLive || IsProxy;
    /// <summary>Виклики йдуть за контрактом проксі MedLink (усі режими, крім застарілого Live).</summary>
    public bool UsesProxyContract => !IsLive;
    public string ModeName => IsLive ? "LIVE" : IsProxy ? "PROXY" : IsReplay ? "REPLAY" : "MOCK";

    /// <summary>Прапорці операцій поточного реального режиму.</summary>
    public EhealthOperationFlags Operations => IsProxy ? Proxy.Operations : Live.Operations;

    /// <summary>Значення заголовка ClientId (id організації в ЕСОЗ): Proxy:ClientId або Ehealth:LegalEntityId.</summary>
    public string? ClientIdValue => !string.IsNullOrWhiteSpace(Proxy.ClientId) ? Proxy.ClientId : !string.IsNullOrWhiteSpace(LegalEntityId) ? LegalEntityId : null;

    /// <summary>Чи дозволена операція: у Mock/Replay — завжди, у реальних режимах — за прапорцем.</summary>
    public bool IsAllowed(EhealthOperation op) => !IsReal || Operations.IsEnabled(op);
}
