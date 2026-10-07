// Swagger: заголовки X-MedLink-Employee-Id (контекст співробітника) та X-MedLink-ApiKey (коннектор)
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MedLink.LIS.Api.Infrastructure;

public sealed class SwaggerHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();
        var path = context.ApiDescription.RelativePath ?? "";
        if (path.Contains("/connector/", StringComparison.OrdinalIgnoreCase))
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = Services.ConnectorService.ApiKeyHeader, In = ParameterLocation.Header, Required = false,
                Description = "Технічний ключ інсталяції коннектора (видається при /connector/register)", Schema = new OpenApiSchema { Type = "string" }
            });
        }
        else if (path.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = CurrentEmployeeMiddleware.HeaderName, In = ParameterLocation.Header, Required = false,
                Description = "Ідентифікатор поточного співробітника (org_employee.id). Якщо відсутній — Lab:DefaultEmployeeId", Schema = new OpenApiSchema { Type = "string" }
            });
        }
    }
}
