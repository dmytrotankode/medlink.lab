// Спільні налаштування System.Text.Json для службових JSON-колонок (camelCase)
using System.Text.Json;

namespace MedLink.LIS.Api.Infrastructure;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
}
