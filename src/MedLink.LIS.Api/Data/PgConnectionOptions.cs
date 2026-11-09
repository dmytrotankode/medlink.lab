// Параметри підключення Npgsql для production: пул, таймаути, keepalive. Те, що явно задано в рядку підключення
// (Database:ConnectionString), не перевизначається; додаються лише відсутні параметри.
using System.Text.RegularExpressions;

namespace MedLink.LIS.Api.Data;

public static class PgConnectionOptions
{
    private static readonly (string Name, string Pattern, string Value)[] Defaults =
    {
        // Максимум одночасних з'єднань одного екземпляра: у кластері max_connections = 100, на хості є інші служби
        ("Maximum Pool Size", @"max(imum)?[ _]?pool[ _]?size", "40"),
        ("Minimum Pool Size", @"min(imum)?[ _]?pool[ _]?size", "2"),
        // Очікування встановлення з'єднання, с
        ("Timeout", @"(^|;)\s*timeout\s*=", "15"),
        // Тайм-аут команди, с (довгі звіти/аналітика — до хвилини)
        ("Command Timeout", @"command[ _]?timeout", "60"),
        // TCP keepalive, с: обрив з'єднань через NAT/балансувальник виявляється, а не зависає
        ("Keepalive", @"keep[ _]?alive", "30"),
        ("Application Name", @"application[ _]?name", "medlink-lab"),
    };

    public static string Normalize(string connectionString)
    {
        var cs = connectionString.TrimEnd().TrimEnd(';');
        foreach (var (name, pattern, value) in Defaults)
            if (!Regex.IsMatch(cs, pattern, RegexOptions.IgnoreCase)) cs += $";{name}={value}";
        return cs;
    }
}
