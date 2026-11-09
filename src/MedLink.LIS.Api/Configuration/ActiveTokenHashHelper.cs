using System.Security.Cryptography;
using System.Text;

namespace MedLink.LIS.Api.Configuration;

internal static class ActiveTokenHashHelper
{
    public static string? Compute(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(accessToken));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }
}
