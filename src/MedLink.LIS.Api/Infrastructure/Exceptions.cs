// Доменні винятки → RFC 7807 ProblemDetails (повідомлення українською)
namespace MedLink.LIS.Api.Infrastructure;

public class LisException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
    public IDictionary<string, string[]>? Errors { get; }
    /// <summary>Машинний код проблеми (розд. 8.3, 21.10): intervention-unmapped, required-indicator-missing, ehealth-auth-required…</summary>
    public string? Code { get; init; }

    public LisException(int statusCode, string title, string detail, IDictionary<string, string[]>? errors = null) : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
        Errors = errors;
    }
}

public sealed class NotFoundException : LisException
{
    public NotFoundException(string detail) : base(404, "Не знайдено", detail) { }
    public static NotFoundException For(string entity, string? id) => new($"{entity} з ідентифікатором '{id}' не знайдено");
}

public sealed class ConflictException : LisException
{
    public ConflictException(string detail) : base(409, "Конфлікт стану", detail) { }
}

public sealed class ValidationException : LisException
{
    public ValidationException(string detail, IDictionary<string, string[]>? errors = null) : base(400, "Помилка валідації", detail, errors) { }
    public static ValidationException Field(string field, string message) =>
        new(message, new Dictionary<string, string[]> { [field] = new[] { message } });
}

public sealed class ForbiddenException : LisException
{
    public ForbiddenException(string detail) : base(403, "Дія заборонена для ролі", detail) { }
}

public sealed class ForbiddenConnectorException : LisException
{
    public ForbiddenConnectorException(string detail) : base(401, "Коннектор не авторизований", detail) { }
}

/// <summary>Доменна проблема з машинним кодом (ProblemDetails.type = …/problems/{code}).</summary>
public sealed class DomainProblemException : LisException
{
    public DomainProblemException(int statusCode, string code, string title, string detail, IDictionary<string, string[]>? errors = null) : base(statusCode, title, detail, errors) => Code = code;
}

/// <summary>Перевищено ліміт пакетної операції (413): замовлення в batch, рядки імпорту.</summary>
public sealed class PayloadTooLargeException : LisException
{
    public PayloadTooLargeException(string detail) : base(413, "Забагато записів в одному запиті", detail) { Code = "batch-too-large"; }
}

/// <summary>Ліміти пакетних операцій (FR-GAP / розд. 19 ТЗ).</summary>
public static class BatchLimits
{
    public const int OrdersPerBatch = 200;
    public const int ImportRows = 5000;
    public static void CheckOrders(int count)
    {
        if (count > OrdersPerBatch) throw new PayloadTooLargeException($"У пакеті {count} замовлень, дозволено не більше {OrdersPerBatch}. Розділіть пакет на кілька запитів.");
    }
    public static void CheckImportRows(int count)
    {
        if (count > ImportRows) throw new PayloadTooLargeException($"У файлі {count} рядків, дозволено не більше {ImportRows}. Розділіть файл на кілька частин.");
    }
}
