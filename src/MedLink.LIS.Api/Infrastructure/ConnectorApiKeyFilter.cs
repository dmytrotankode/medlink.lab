// Автентифікація коннектора заголовком X-MedLink-ApiKey (SHA-256 hash у lab_connector_installation).
// Це ідентифікація пристрою, а не користувацька авторизація.
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MedLink.LIS.Api.Infrastructure;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowWithoutApiKeyAttribute : Attribute { }

public sealed class ConnectorApiKeyFilter : IAsyncActionFilter
{
    public const string ItemKey = "MedLink.Connector";
    private readonly ConnectorService _connectors;
    public ConnectorApiKeyFilter(ConnectorService connectors) => _connectors = connectors;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var allowAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<AllowWithoutApiKeyAttribute>().Any();
        if (!allowAnonymous)
        {
            var key = context.HttpContext.Request.Headers[ConnectorService.ApiKeyHeader].FirstOrDefault();
            var connector = await _connectors.ResolveByApiKeyAsync(key)
                            ?? throw new ForbiddenConnectorException($"Відсутній або недійсний заголовок {ConnectorService.ApiKeyHeader}");
            context.HttpContext.Items[ItemKey] = connector;
        }
        await next();
    }
}

public static class ConnectorContextExtensions
{
    public static LabConnectorInstallation CurrentConnector(this HttpContext ctx) =>
        ctx.Items[ConnectorApiKeyFilter.ItemKey] as LabConnectorInstallation ?? throw new ForbiddenConnectorException("Коннектор не автентифіковано");
}
