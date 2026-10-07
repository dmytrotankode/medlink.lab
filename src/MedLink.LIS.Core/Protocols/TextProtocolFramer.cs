// =============================================================================
// MedLink LIS 4.0 — універсальний рамкувальник текстових протоколів за байтами
// початку/кінця пакета (bop/eop) із профілю Simplex ac_analyzer_type
// (TEXT, HUMA5L, UC1000, CYAN, FUJI, RAPID, IRIS, TXT, JUNIOR).
// Порт логіки nrComm1AfterReceive із Delphi-коннектора.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text;

namespace MedLink.LIS.Core.Protocols;

/// <summary>
/// Накопичує байти та виділяє пакети між послідовностями bop (початок) та eop (кінець).
/// Якщо bop відсутній (null/порожній) — пакет починається з першого байта після попереднього eop.
/// Якщо eop відсутній — пакет завершується таймером тиші (викликом <see cref="FlushByTimeout"/>).
/// Потокобезпечність не гарантується — використовувати з одного потоку/черги.
/// </summary>
public sealed class TextProtocolFramer
{
    private readonly byte[] _bop;
    private readonly byte[] _eop;
    private readonly List<byte> _buffer = new();
    private bool _inPacket;
    private readonly int _maxPacketSize;

    public TextProtocolFramer(byte[]? bop, byte[]? eop, int maxPacketSize = 4 * 1024 * 1024)
    {
        _bop = bop ?? Array.Empty<byte>();
        _eop = eop ?? Array.Empty<byte>();
        _maxPacketSize = maxPacketSize;
        _inPacket = _bop.Length == 0;
    }

    /// <summary>Створює рамкувальник із base64-полів профілю (null → відсутній маркер).</summary>
    public static TextProtocolFramer FromBase64(string? bopBase64, string? eopBase64)
        => new(DecodeBase64(bopBase64), DecodeBase64(eopBase64));

    public static byte[]? DecodeBase64(string? b64)
    {
        if (string.IsNullOrWhiteSpace(b64)) return null;
        try { return Convert.FromBase64String(b64.Trim()); }
        catch { return Encoding.Latin1.GetBytes(b64); }
    }

    public byte[] Bop => _bop;
    public byte[] Eop => _eop;
    public bool HasBop => _bop.Length > 0;
    public bool HasEop => _eop.Length > 0;
    /// <summary>Чи є незавершені дані у буфері.</summary>
    public bool HasPending => _buffer.Count > 0;
    public int PendingLength => _buffer.Count;

    /// <summary>
    /// Додає отримані байти. Повертає список завершених пакетів (без bop/eop).
    /// </summary>
    public List<byte[]> Push(ReadOnlySpan<byte> data)
    {
        var packets = new List<byte[]>();
        foreach (var b in data)
        {
            _buffer.Add(b);
            if (!_inPacket)
            {
                if (HasBop && EndsWith(_buffer, _bop))
                {
                    _buffer.Clear();
                    _inPacket = true;
                }
                else if (_buffer.Count > _bop.Length + 64)
                {
                    // Сміття поза пакетом — тримаємо лише хвіст для пошуку bop
                    _buffer.RemoveRange(0, _buffer.Count - _bop.Length);
                }
                continue;
            }
            // Усередині пакета
            if (HasBop && EndsWith(_buffer, _bop))
            {
                // Новий bop без eop — скидаємо попередній незавершений пакет (як у Delphi: astmPacket := '')
                _buffer.Clear();
                continue;
            }
            if (HasEop && EndsWith(_buffer, _eop))
            {
                var len = _buffer.Count - _eop.Length;
                var packet = _buffer.GetRange(0, len).ToArray();
                _buffer.Clear();
                packets.Add(packet);
                _inPacket = !HasBop;
                continue;
            }
            if (_buffer.Count > _maxPacketSize)
            {
                packets.Add(_buffer.ToArray());
                _buffer.Clear();
                _inPacket = !HasBop;
            }
        }
        return packets;
    }

    /// <summary>Завершує пакет за таймаутом тиші (для протоколів без eop). Повертає null, якщо буфер порожній.</summary>
    public byte[]? FlushByTimeout()
    {
        if (_buffer.Count == 0) return null;
        if (!_inPacket && HasBop) { _buffer.Clear(); return null; }
        var packet = _buffer.ToArray();
        _buffer.Clear();
        _inPacket = !HasBop;
        return packet;
    }

    public void Reset()
    {
        _buffer.Clear();
        _inPacket = !HasBop;
    }

    private static bool EndsWith(List<byte> buffer, byte[] marker)
    {
        if (marker.Length == 0 || buffer.Count < marker.Length) return false;
        int off = buffer.Count - marker.Length;
        for (int i = 0; i < marker.Length; i++)
            if (buffer[off + i] != marker[i]) return false;
        return true;
    }
}

/// <summary>Утиліти для показу керуючих символів у журналах/тестах: &lt;STX&gt;, &lt;CR&gt; тощо.</summary>
public static class ProtocolText
{
    private static readonly Dictionary<char, string> Names = new()
    {
        [(char)0x00] = "<NUL>", [(char)0x01] = "<SOH>", [(char)0x02] = "<STX>", [(char)0x03] = "<ETX>", [(char)0x04] = "<EOT>",
        [(char)0x05] = "<ENQ>", [(char)0x06] = "<ACK>", [(char)0x07] = "<BEL>", [(char)0x08] = "<BS>", [(char)0x09] = "<TAB>",
        [(char)0x0A] = "<LF>", [(char)0x0B] = "<VT>", [(char)0x0C] = "<FF>", [(char)0x0D] = "<CR>", [(char)0x15] = "<NAK>",
        [(char)0x17] = "<ETB>", [(char)0x1C] = "<FS>", [(char)0x1D] = "<GS>", [(char)0x1E] = "<RS>", [(char)0x1F] = "<US>",
    };

    private static readonly Dictionary<string, char> Codes = Names.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Замінює керуючі символи текстовими мітками (&lt;STX&gt; …). TAB за замовчуванням лишається.</summary>
    public static string Escape(string raw, bool keepTab = true, bool keepNewLines = false)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new StringBuilder(raw.Length + 16);
        foreach (var c in raw)
        {
            if (keepTab && c == '\t') { sb.Append(c); continue; }
            if (keepNewLines && (c == '\r' || c == '\n')) { sb.Append(c); continue; }
            if (c < 0x20 && Names.TryGetValue(c, out var n)) sb.Append(n);
            else if (c < 0x20) sb.Append("<0x").Append(((int)c).ToString("X2")).Append('>');
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Перетворює текстові мітки (&lt;STX&gt;, &lt;CR&gt;, &lt;0x1D&gt; …) назад у символи. Використовується для файлів-зразків.</summary>
    public static string Unescape(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '<')
            {
                int end = text.IndexOf('>', i + 1);
                if (end > i && end - i <= 7)
                {
                    var token = text.Substring(i, end - i + 1);
                    if (Codes.TryGetValue(token, out var ch)) { sb.Append(ch); i = end + 1; continue; }
                    if (token.StartsWith("<0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token.AsSpan(3, token.Length - 4), System.Globalization.NumberStyles.HexNumber, null, out var code))
                    { sb.Append((char)code); i = end + 1; continue; }
                }
            }
            sb.Append(text[i]);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Завантажує файл-зразок: якщо він містить текстові мітки (&lt;STX&gt; тощо), розгортає їх; переводи рядків файлу
    /// між кадрами ігноруються (кадри самі містять &lt;CR&gt;&lt;LF&gt;).
    /// </summary>
    public static string LoadSample(string fileText)
    {
        // Якщо переводи рядків закодовані мітками <CR>/<LF> — фізичні переводи рядків файлу є лише форматуванням.
        if (fileText.Contains("<CR>") || fileText.Contains("<LF>"))
        {
            var noNewLines = fileText.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
            return Unescape(noNewLines);
        }
        return Unescape(fileText);
    }
}
