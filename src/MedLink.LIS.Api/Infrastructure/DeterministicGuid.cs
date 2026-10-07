// Стабільні GUID для сід-даних (однакові між запусками) — на основі MD5 від ключа
using System.Security.Cryptography;
using System.Text;

namespace MedLink.LIS.Api.Infrastructure;

public static class DeterministicGuid
{
    public static string For(string key)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("medlink-lis:" + key));
        return new Guid(hash).ToString();
    }
}
