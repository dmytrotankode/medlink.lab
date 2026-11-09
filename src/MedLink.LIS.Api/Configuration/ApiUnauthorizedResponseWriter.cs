using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MedLink.LIS.Api.Configuration;

internal static class ApiUnauthorizedResponseWriter
{
    public const string UnauthorizedTitle = "Не авторизовано";
    public const string UnauthorizedDetail = "Користувача не авторизовано або строк дії авторизації минув";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpResponse response, int statusCode)
    {
        if (response.HasStarted)
        {
            return Task.CompletedTask;
        }

        response.StatusCode = statusCode;
        response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Title = UnauthorizedTitle,
            Detail = UnauthorizedDetail,
            Status = statusCode
        };

        return response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
