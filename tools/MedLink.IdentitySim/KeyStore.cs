// Ключ підпису RSA: створюється один раз і зберігається у файлі (PEM) поза репозиторієм; kid — відбиток відкритого ключа.
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace MedLink.IdentitySim;

public sealed class KeyStore
{
    public RsaSecurityKey Key { get; }
    public SigningCredentials Credentials { get; }
    public JsonWebKey PublicJwk { get; }

    public KeyStore(string path)
    {
        var rsa = RSA.Create(2048);
        if (File.Exists(path)) rsa.ImportFromPem(File.ReadAllText(path));
        else
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, rsa.ExportPkcs8PrivateKeyPem());
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        var kid = Base64UrlEncoder.Encode(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))[..24];
        Key = new RsaSecurityKey(rsa) { KeyId = kid };
        Credentials = new SigningCredentials(Key, SecurityAlgorithms.RsaSha256);
        PublicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = kid });
        PublicJwk.Use = "sig";
        PublicJwk.Alg = "RS256";
    }
}
