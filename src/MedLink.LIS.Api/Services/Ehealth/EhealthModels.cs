// =============================================================================
// Моделі інтеграції з ЕСОЗ: режими, операції (поопераційні прапорці), класифікація помилок, job, е-направлення.
// Контракт — eHealth-сервіс (проксі) MedLink: patients/service_requests…, jobs/{id}, auth/* (docs/EHEALTH_MEDLINK_PROXY_CONTRACT.md);
// прямий ЕСОЗ (OpenAPI, medical-events.json) — лише застарілий режим Live: /api/service_requests…, /api/jobs/{id},
// /api/patients/{id}/diagnostic_report_package; докладно — docs/ESOZ_LAB_INTERACTIONS.md.
// =============================================================================
using System.Text.Json.Nodes;

namespace MedLink.LIS.Api.Services.Ehealth;

/// <summary>Режим роботи інтеграції: Mock — двійник проксі MedLink; MedLinkProxy — реальний eHealth-сервіс MedLink; Replay — записані фікстури проксі;
/// Live — ЗАСТАРІЛИЙ прямий виклик ЕСОЗ.</summary>
public static class EhealthModes
{
    public const string Mock = "Mock", MedLinkProxy = "MedLinkProxy", Replay = "Replay", Live = "Live";
}

/// <summary>Операції ЕСОЗ, які можна вмикати окремо в реальних режимах (безпечна поетапна обкатка).</summary>
public enum EhealthOperation { SearchRead, Use, Complete, SubmitDr, CancelDr, Specimen, Equipment }

/// <summary>Клас помилки обміну: визначає поведінку воркера (повторити / дія користувача / вхід).</summary>
public enum EhealthErrorKind
{
    /// <summary>Мережа, таймаут, 5xx, 429 — повторити з витримкою.</summary>
    Transient,
    /// <summary>401 — токен недійсний/прострочений: потрібен вхід.</summary>
    Auth,
    /// <summary>403 — немає скоупу/права: повтор безглуздий.</summary>
    Permission,
    /// <summary>404 — об'єкт не знайдено.</summary>
    NotFound,
    /// <summary>409 — конфлікт стану (дубль, неприпустимий статус).</summary>
    Conflict,
    /// <summary>400/422 — помилка валідації тіла.</summary>
    Validation,
    /// <summary>Операцію не дозволено прапорцем Ehealth:Proxy:Operations (для застарілого Live — Ehealth:Live:Operations).</summary>
    Disabled,
    /// <summary>Підключення не налаштовано (токен, ідентифікатори закладу) або немає фікстури Replay.</summary>
    Config
}

public sealed class EhealthException : Exception
{
    public int StatusCode { get; }
    public EhealthErrorKind Kind { get; }
    /// <summary>Перелік порушених правил (JSON-path: опис) з відповіді 422.</summary>
    public IReadOnlyList<string> Invalid { get; }
    public string? RawBody { get; }

    public EhealthException(int statusCode, string message) : this(statusCode, message, KindOf(statusCode), null, null) { }

    public EhealthException(int statusCode, string message, EhealthErrorKind kind, string? rawBody = null, IReadOnlyList<string>? invalid = null) : base(message)
    {
        StatusCode = statusCode; Kind = kind; RawBody = rawBody; Invalid = invalid ?? Array.Empty<string>();
    }

    /// <summary>Повтор має сенс (мережа, 5xx, 429).</summary>
    public bool IsTransient => Kind == EhealthErrorKind.Transient;

    public static EhealthErrorKind KindOf(int status) => status switch
    {
        401 => EhealthErrorKind.Auth,
        403 => EhealthErrorKind.Permission,
        404 => EhealthErrorKind.NotFound,
        409 => EhealthErrorKind.Conflict,
        400 or 422 => EhealthErrorKind.Validation,
        501 => EhealthErrorKind.Config,
        0 or 408 or 429 => EhealthErrorKind.Transient,
        >= 500 => EhealthErrorKind.Transient,
        _ => EhealthErrorKind.Validation
    };

    /// <summary>Машинний код для outbox/UI.</summary>
    public string Code => Kind switch
    {
        EhealthErrorKind.Transient => "TRANSIENT", EhealthErrorKind.Auth => "AUTH", EhealthErrorKind.Permission => "PERMISSION", EhealthErrorKind.NotFound => "NOT_FOUND",
        EhealthErrorKind.Conflict => "CONFLICT", EhealthErrorKind.Validation => "VALIDATION", EhealthErrorKind.Disabled => "OPERATION_DISABLED", _ => "CONFIG"
    };
}

public sealed record EhealthLink(string Entity, string Href);

/// <summary>Нормалізовані стани асинхронного завдання (job) ЕСОЗ.</summary>
public static class EhealthJobStatus
{
    public const string Pending = "pending", Processed = "processed", Failed = "failed";
}

/// <summary>Результат асинхронної операції. <c>Id</c> порожній, якщо ЕСОЗ виконала операцію синхронно (201).</summary>
public sealed record EhealthJob(string Id, string Status, string? EntityId = null, string RawStatus = "", int? StatusCode = null, string? Error = null,
    IReadOnlyList<EhealthLink>? Links = null, IReadOnlyList<string>? Invalid = null)
{
    public bool IsPending => Status == EhealthJobStatus.Pending;
    public bool IsProcessed => Status == EhealthJobStatus.Processed;
    public bool IsFailed => Status == EhealthJobStatus.Failed;
}

/// <summary>Е-направлення з відповіді Search/Details (лише потрібні лабораторії поля; знеособлено: identity — прізвище, ініціали).</summary>
public sealed record EhealthServiceRequest(string Id, string Requisition, string Status, string? PatientId, string? ServiceCode, DateTime? ExpiresAt, string? UsedByLegalEntity,
    string? Category = null, string? ProgramId = null, string? ProgramProcessingStatus = null, decimal? RemainingQuantity = null,
    string? IdentityLastName = null, string? IdentityGender = null, string? ServiceName = null);

/// <summary>Діагностичний звіт з ЕСОЗ (для перевірки ідемпотентності перед повтором).</summary>
public sealed record EhealthDiagnosticReport(string Id, string Status, string? PatientId);

/// <summary>Ідентифікатор eHealth (Reference.identifier) у форматі SDK: system=eHealth/resources.</summary>
public static class EhealthRef
{
    public static JsonObject Identifier(string code, string value) => new()
    {
        ["identifier"] = new JsonObject
        {
            ["type"] = new JsonObject { ["coding"] = new JsonArray(new JsonObject { ["system"] = "eHealth/resources", ["code"] = code }) },
            ["value"] = value
        }
    };

    public static JsonObject Coding(string system, string code, string? text = null)
    {
        var o = new JsonObject { ["coding"] = new JsonArray(new JsonObject { ["system"] = system, ["code"] = code }) };
        if (text != null) o["text"] = text;
        return o;
    }
}
