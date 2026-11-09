// =============================================================================
// Налаштування інтеграції з ЕСОЗ (секція Ehealth у appsettings). Секрети (AccessToken, ApiKey, CustomPsk) зберігаються
// лише в конфігурації сервера/сховищі секретів і не потрапляють у відповіді API та журнали.
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
}

public sealed class EhealthOptions
{
    /// <summary>Mock | Replay | Live</summary>
    public string Mode { get; set; } = EhealthModes.Mock;
    public string BaseUrl { get; set; } = "https://api.ehealth.gov.ua/api/";
    /// <summary>Access token (у Live — з авторизації МІС; тимчасово статичний). Не журналюється.</summary>
    public string? AccessToken { get; set; }
    public string? ApiKey { get; set; }
    /// <summary>Заголовок X-Custom-PSK (потрібен шлюзу ЕСОЗ).</summary>
    public string? CustomPsk { get; set; }
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

    public bool IsLive => string.Equals(Mode, EhealthModes.Live, StringComparison.OrdinalIgnoreCase);
    public bool IsReplay => string.Equals(Mode, EhealthModes.Replay, StringComparison.OrdinalIgnoreCase);
    public bool IsMock => !IsLive && !IsReplay;
    /// <summary>Mock або Replay: ЕСОЗ імітується (вхід паролем/2FA, тестовий підпис).</summary>
    public bool Simulated => !IsLive;
    public string ModeName => IsLive ? "LIVE" : IsReplay ? "REPLAY" : "MOCK";

    /// <summary>Чи дозволена операція: у Mock/Replay — завжди, у Live — за прапорцем.</summary>
    public bool IsAllowed(EhealthOperation op) => !IsLive || Live.Operations.IsEnabled(op);
}
