// =============================================================================
// Підпис КЕП пакета DiagnosticReport. Приватний ключ лаборанта на сервер не потрапляє: підписаний рядок (base64 від CAdES-X Long,
// без BOM) приходить з браузерного віджета готовим. Сервер виконує лише СТРУКТУРНУ перевірку (ТВ МІС 1.3.1–1.3.4):
//   • коректний base64 без BOM/пробілів; • контейнер CMS SignedData (DER); • в підписі є атрибути certificate-values і revocation-values
//   (профіль CAdES-X Long); • підписаний вміст (eContent) збігається з пакетом, який ми підготували.
// Криптографічної перевірки підпису (ланцюжок сертифікатів, відкликання, хеш) тут НЕМАЄ: її виконує ЕСОЗ при реєстрації; сервер не
// «схвалює» довільний рядок — лише відсікає очевидно непридатний формат до відправки.
// =============================================================================
using System.Security.Cryptography;
using System.Text;

namespace MedLink.LIS.Api.Services.Ehealth;

/// <summary>UUID версії 1 (ТВ 1.1.7: ідентифікатори записів створює МІС). Монотонний у межах процесу.</summary>
public static class Uuid1
{
    private static readonly object Gate = new();
    private static long _last;
    private static readonly byte[] Node = RandomNumberGenerator.GetBytes(6);
    private static ushort _clockSeq = (ushort)(RandomNumberGenerator.GetInt32(0, 0x3FFF));

    public static string NewString() => New().ToString();

    public static Guid New()
    {
        long ts;
        lock (Gate)
        {
            // 100-нс інтервали від 1582-10-15
            ts = (DateTime.UtcNow - new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc)).Ticks;
            if (ts <= _last) ts = _last + 1;
            _last = ts;
        }
        var b = new byte[16];
        var timeLow = (uint)(ts & 0xFFFFFFFF);
        var timeMid = (ushort)((ts >> 32) & 0xFFFF);
        var timeHi = (ushort)(((ts >> 48) & 0x0FFF) | 0x1000);
        b[0] = (byte)(timeLow >> 24); b[1] = (byte)(timeLow >> 16); b[2] = (byte)(timeLow >> 8); b[3] = (byte)timeLow;
        b[4] = (byte)(timeMid >> 8); b[5] = (byte)timeMid;
        b[6] = (byte)(timeHi >> 8); b[7] = (byte)timeHi;
        b[8] = (byte)(0x80 | ((_clockSeq >> 8) & 0x3F)); b[9] = (byte)_clockSeq;
        Array.Copy(Node, 0, b, 10, 6);
        return new Guid(b, bigEndian: true);
    }

    public static bool IsV1(string? s) => Guid.TryParse(s, out _) && s!.Length == 36 && s[14] == '1' && "89abAB".Contains(s[19]);
}

public sealed record CadesInspection(bool Valid, string Profile, bool HasCertificateValues, bool HasRevocationValues, bool ContentMatches, string[] Errors)
{
    public string? FirstError => Errors.Length > 0 ? Errors[0] : null;
}

public static class CadesInspector
{
    // OID у DER (tag 06 + довжина + тіло)
    private static readonly byte[] OidSignedData = { 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x02 };
    public static readonly byte[] OidData = { 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x01 };
    public static readonly byte[] OidCertificateValues = { 0x06, 0x0B, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x10, 0x02, 0x17 };
    public static readonly byte[] OidRevocationValues = { 0x06, 0x0B, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x10, 0x02, 0x18 };
    public static readonly byte[] OidSignedDataPublic = OidSignedData;

    public const string ProfileXLong = "CAdES-X-Long";

    /// <summary>Перевіряє рядок підпису. <paramref name="expectedContent"/> — канонічні байти пакета (UTF-8 без BOM), якщо відомі.</summary>
    public static CadesInspection Inspect(string? signedData, byte[]? expectedContent = null)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(signedData))
            return new CadesInspection(false, "NONE", false, false, false, new[] { "Підпис відсутній" });
        if (signedData.Contains('﻿')) errors.Add("Підпис містить BOM (U+FEFF): перед підписом і кодуванням у base64 BOM треба прибрати");
        var s = signedData.Trim();
        if (s.Any(char.IsWhiteSpace)) errors.Add("Підпис має містити суцільний рядок base64 без пробілів і переносів");
        byte[] raw;
        try { raw = Convert.FromBase64String(s.Replace("﻿", "")); }
        catch (FormatException) { return new CadesInspection(false, "NONE", false, false, false, errors.Append("Підпис не є коректним base64").ToArray()); }
        if (raw.Length < 16 || raw[0] != 0x30) errors.Add("Підпис не є контейнером CMS/CAdES (очікується ASN.1 SEQUENCE)");
        else if (!ContainsSeq(raw, OidSignedData)) errors.Add("У контейнері немає ідентифікатора CMS SignedData");

        var hasCert = ContainsSeq(raw, OidCertificateValues);
        var hasRev = ContainsSeq(raw, OidRevocationValues);
        if (errors.Count == 0 && !(hasCert && hasRev))
            errors.Add("Підпис не відповідає профілю CAdES-X Long: немає атрибутів " + string.Join(" і ", new[] { hasCert ? null : "certificate-values", hasRev ? null : "revocation-values" }.Where(x => x != null)));

        var contentOk = true;
        if (expectedContent != null)
        {
            contentOk = ContainsSeq(raw, expectedContent);
            if (!contentOk) errors.Add("Підписаний вміст не збігається з підготовленим пакетом (пакет змінено після підготовки або підписано інші дані)");
        }
        var profile = hasCert && hasRev ? ProfileXLong : raw.Length > 0 && raw[0] == 0x30 && ContainsSeq(raw, OidSignedData) ? "CAdES-BES/other" : "NONE";
        return new CadesInspection(errors.Count == 0, profile, hasCert, hasRev, contentOk, errors.ToArray());
    }

    /// <summary>Витягує вкладений вміст із CMS (для мока ЕСОЗ): пошук JSON-об'єкта в eContent OCTET STRING.</summary>
    public static byte[]? TryExtractContent(string? signedData)
    {
        try
        {
            var raw = Convert.FromBase64String((signedData ?? "").Trim());
            // eContent: після OID data йде [0] { OCTET STRING { ... } }
            var i = IndexOf(raw, OidData);
            if (i < 0) return null;
            var pos = i + OidData.Length;
            if (pos >= raw.Length || raw[pos] != 0xA0) return null;
            pos++; Der.ReadLength(raw, ref pos);
            if (pos >= raw.Length || raw[pos] != 0x04) return null;
            pos++; var len = Der.ReadLength(raw, ref pos);
            if (len < 0 || pos + len > raw.Length) return null;
            return raw.AsSpan(pos, len).ToArray();
        }
        catch { return null; }
    }

    private static bool ContainsSeq(byte[] hay, byte[] needle) => IndexOf(hay, needle) >= 0;
    private static int IndexOf(byte[] hay, byte[] needle) => hay.AsSpan().IndexOf(needle);
}

/// <summary>Мінімальний DER-кодувальник/читач (лише для тестового підписувача та мока).</summary>
public static class Der
{
    public static byte[] Tlv(byte tag, params byte[][] content)
    {
        var body = content.SelectMany(x => x).ToArray();
        var len = body.Length;
        var head = new List<byte> { tag };
        if (len < 0x80) head.Add((byte)len);
        else if (len <= 0xFF) { head.Add(0x81); head.Add((byte)len); }
        else if (len <= 0xFFFF) { head.Add(0x82); head.Add((byte)(len >> 8)); head.Add((byte)len); }
        else { head.Add(0x83); head.Add((byte)(len >> 16)); head.Add((byte)(len >> 8)); head.Add((byte)len); }
        return head.Concat(body).ToArray();
    }

    public static int ReadLength(byte[] b, ref int pos)
    {
        var first = b[pos++];
        if (first < 0x80) return first;
        var n = first & 0x7F;
        if (n == 0 || n > 4) return -1;
        var len = 0;
        for (var i = 0; i < n; i++) len = (len << 8) | b[pos++];
        return len;
    }
}

/// <summary>
/// ТЕСТОВИЙ підписувач (лише Mock/Replay): будує структурно коректний контейнер CMS SignedData з атрибутами certificate-values і
/// revocation-values й вкладеним вмістом. Це НЕ криптографічний підпис — ЕСОЗ (реальна) такий підпис відхилить.
/// </summary>
public static class EhealthMockSigner
{
    public static string Sign(byte[] content, bool xLong = true)
    {
        var version = Der.Tlv(0x02, new byte[] { 0x01 });
        var digestAlgs = Der.Tlv(0x31, Der.Tlv(0x30, new byte[] { 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01 }, new byte[] { 0x05, 0x00 }));
        var encap = Der.Tlv(0x30, CadesInspector.OidData, Der.Tlv(0xA0, Der.Tlv(0x04, content)));
        var hash = SHA256.HashData(content);
        var attrs = new List<byte[]>();
        if (xLong)
        {
            attrs.Add(Der.Tlv(0x30, CadesInspector.OidCertificateValues, Der.Tlv(0x31, Der.Tlv(0x30, Encoding.ASCII.GetBytes("MOCK-CERT")))));
            attrs.Add(Der.Tlv(0x30, CadesInspector.OidRevocationValues, Der.Tlv(0x31, Der.Tlv(0x30, Encoding.ASCII.GetBytes("MOCK-CRL")))));
        }
        var signerInfo = Der.Tlv(0x30, version, Der.Tlv(0x04, hash), Der.Tlv(0xA1, attrs.SelectMany(x => x).ToArray()));
        var signedData = Der.Tlv(0x30, version, digestAlgs, encap, Der.Tlv(0x31, signerInfo));
        var contentInfo = Der.Tlv(0x30, CadesInspector.OidSignedDataPublic, Der.Tlv(0xA0, signedData));
        return Convert.ToBase64String(contentInfo);
    }
}
