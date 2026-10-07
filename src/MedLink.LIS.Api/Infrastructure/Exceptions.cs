// Доменні винятки → RFC 7807 ProblemDetails (повідомлення українською)
namespace MedLink.LIS.Api.Infrastructure;

public class LisException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
    public IDictionary<string, string[]>? Errors { get; }

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
