// =============================================================================
// MedLink LIS 4.0 — ASTM E1381 (низький рівень): керуючі символи, контрольна
// сума, розбиття на кадри та збирання повідомлення з кадрів.
// Порт логіки Delphi-коннектора AconnectAstm (CalcCRC, Button6Click/Parse).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text;

namespace MedLink.LIS.Core.Protocols;

/// <summary>Керуючі символи ASTM E1381 / низькорівневих текстових протоколів.</summary>
public static class AstmControl
{
    public const byte SOH = 0x01;
    public const byte STX = 0x02;
    public const byte ETX = 0x03;
    public const byte EOT = 0x04;
    public const byte ENQ = 0x05;
    public const byte ACK = 0x06;
    public const byte LF = 0x0A;
    public const byte VT = 0x0B;
    public const byte CR = 0x0D;
    public const byte NAK = 0x15;
    public const byte ETB = 0x17;
    public const byte FS = 0x1C;
    public const byte GS = 0x1D;
    public const byte RS = 0x1E;

    public const char StxChar = (char)STX;
    public const char EtxChar = (char)ETX;
    public const char EotChar = (char)EOT;
    public const char EnqChar = (char)ENQ;
    public const char AckChar = (char)ACK;
    public const char NakChar = (char)NAK;
    public const char EtbChar = (char)ETB;
    public const char CrChar = (char)CR;
    public const char LfChar = (char)LF;
}

/// <summary>Один кадр ASTM після розбору.</summary>
public sealed class AstmFrame
{
    /// <summary>Номер кадру 0..7 (-1, якщо відсутній/нечисловий).</summary>
    public int Number { get; init; }
    /// <summary>Текст кадру без номера, без ETX/ETB, без контрольної суми.</summary>
    public string Content { get; init; } = "";
    /// <summary>true — кадр завершено ETX (останній кадр запису), false — ETB (проміжний).</summary>
    public bool IsLast { get; init; }
    /// <summary>Контрольна сума, отримана у кадрі (2 hex-символи) або null.</summary>
    public string? ReceivedChecksum { get; init; }
    /// <summary>Обчислена контрольна сума.</summary>
    public string ComputedChecksum { get; init; } = "";
    /// <summary>Чи збігається контрольна сума (true також коли її нема і перевірка не вимагається).</summary>
    public bool ChecksumOk { get; init; }
    /// <summary>Причина відхилення кадру (null — кадр коректний).</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Кодек кадрів ASTM E1381: контрольна сума (сума байтів від номера кадру до ETX/ETB включно mod 256,
/// 2 hex-символи у верхньому регістрі — як CalcCRC у Delphi), побудова та розбір кадрів,
/// збирання повідомлення з кадрів.
/// </summary>
public static class AstmFrameCodec
{
    /// <summary>Максимальна довжина кадру за замовчуванням (ASTM E1381: 240 символів тексту + службові).</summary>
    public const int DefaultMaxFrameLen = 240;

    /// <summary>Кодування тексту кадрів за замовчуванням. ASTM — 8-бітний; використовуємо Latin-1 для байт-прозорості.</summary>
    public static Encoding DefaultEncoding { get; } = Encoding.Latin1;

    /// <summary>Контрольна сума mod 256 (2 hex, верхній регістр) для масиву байтів.</summary>
    public static string Checksum(ReadOnlySpan<byte> data)
    {
        int sum = 0;
        foreach (var b in data) sum += b;
        return (sum % 256).ToString("X2");
    }

    /// <summary>Контрольна сума mod 256 для рядка у заданому кодуванні (за замовчуванням Latin-1 / порядкові номери символів).</summary>
    public static string Checksum(string data, Encoding? encoding = null)
        => Checksum((encoding ?? DefaultEncoding).GetBytes(data));

    /// <summary>
    /// Контрольна сума за правилом ASTM: від першого символу після STX (номер кадру) до ETX/ETB включно.
    /// Приймає текст кадру (номер + текст + ETX/ETB).
    /// </summary>
    public static string ChecksumOfFrameBody(string frameBodyWithTerminator, Encoding? encoding = null)
        => Checksum(frameBodyWithTerminator, encoding);

    /// <summary>
    /// Будує один кадр: STX + номер + текст + (CR)ETX|ETB + контрольна сума + CR LF.
    /// Для останнього (ETX) кадру перед ETX додається CR (кінець запису), для проміжного (ETB) — ні.
    /// </summary>
    public static string BuildFrame(int frameNumber, string text, bool isLast, bool withChecksum = true, bool appendCrBeforeTerminator = true)
    {
        var sb = new StringBuilder(text.Length + 8);
        sb.Append((char)(('0') + (frameNumber % 8)));
        sb.Append(text);
        if (isLast && appendCrBeforeTerminator) sb.Append(AstmControl.CrChar);
        sb.Append(isLast ? AstmControl.EtxChar : AstmControl.EtbChar);
        var body = sb.ToString();
        var result = new StringBuilder(body.Length + 5);
        result.Append(AstmControl.StxChar).Append(body);
        if (withChecksum) result.Append(Checksum(body));
        result.Append(AstmControl.CrChar).Append(AstmControl.LfChar);
        return result.ToString();
    }

    /// <summary>
    /// Розбиває записи на кадри. Нумерація 1..7, потім 0, 1, ... (циклічно mod 8), як вимагає ASTM E1381.
    /// Запис, довший за maxFrameLen, ділиться на кілька кадрів: проміжні завершуються ETB, останній — CR ETX.
    /// Явний символ ETB (0x17) усередині запису трактується як примусова межа кадру (використовується
    /// побудовниками Cobas e411/c311, що ділять список тестів на 165/170 символів, як у legacy gen_lab_order).
    /// </summary>
    public static IReadOnlyList<string> BuildFrames(IEnumerable<string> records, int maxFrameLen = DefaultMaxFrameLen, bool withChecksum = true, int firstFrameNumber = 1)
    {
        if (maxFrameLen < 8) maxFrameLen = 8;
        var frames = new List<string>();
        int fn = firstFrameNumber;
        foreach (var raw in records)
        {
            var record = raw.TrimEnd(AstmControl.CrChar, AstmControl.LfChar, AstmControl.EtxChar, AstmControl.EtbChar);
            // Явні межі кадрів (ETB усередині запису)
            var parts = record.Split(AstmControl.EtbChar);
            for (int p = 0; p < parts.Length; p++)
            {
                var part = parts[p].TrimEnd(AstmControl.CrChar);
                bool lastPart = p == parts.Length - 1;
                // Автоматичне розбиття занадто довгих частин
                var chunks = new List<string>();
                if (part.Length <= maxFrameLen - 1) chunks.Add(part);
                else
                {
                    int pos = 0;
                    while (pos < part.Length)
                    {
                        int len = Math.Min(maxFrameLen - 1, part.Length - pos);
                        chunks.Add(part.Substring(pos, len));
                        pos += len;
                    }
                }
                for (int c = 0; c < chunks.Count; c++)
                {
                    bool isLast = lastPart && c == chunks.Count - 1;
                    frames.Add(BuildFrame(fn, chunks[c], isLast, withChecksum));
                    fn = NextFrameNumber(fn);
                }
            }
        }
        return frames;
    }

    /// <summary>Наступний номер кадру: 1,2,...,7,0,1,...</summary>
    public static int NextFrameNumber(int current) => (current + 1) % 8;

    /// <summary>
    /// Розбирає один кадр (від STX до CR LF включно або без них). Перевіряє контрольну суму, якщо вона є
    /// та requireChecksum=true. Ніколи не кидає виключень — помилка повертається в полі Error.
    /// </summary>
    public static AstmFrame ParseFrame(ReadOnlySpan<byte> frameBytes, bool requireChecksum = true, Encoding? encoding = null)
    {
        encoding ??= DefaultEncoding;
        // Відрізаємо STX
        int start = 0;
        while (start < frameBytes.Length && frameBytes[start] != AstmControl.STX) start++;
        if (start >= frameBytes.Length) return new AstmFrame { Number = -1, Error = "Відсутній STX", ChecksumOk = false };
        var span = frameBytes[(start + 1)..];
        // Шукаємо ETX/ETB
        int term = -1;
        for (int i = span.Length - 1; i >= 0; i--)
        {
            if (span[i] == AstmControl.ETX || span[i] == AstmControl.ETB) { term = i; break; }
        }
        if (term < 0) return new AstmFrame { Number = -1, Content = encoding.GetString(span), Error = "Відсутній ETX/ETB", ChecksumOk = false };
        bool isLast = span[term] == AstmControl.ETX;
        var body = span[..(term + 1)];
        var computed = Checksum(body);
        string? received = null;
        if (span.Length >= term + 3)
        {
            var c1 = (char)span[term + 1];
            var c2 = (char)span[term + 2];
            if (Uri.IsHexDigit(c1) && Uri.IsHexDigit(c2)) received = new string(new[] { char.ToUpperInvariant(c1), char.ToUpperInvariant(c2) });
        }
        int number = -1;
        int contentStart = 0;
        if (body.Length > 0 && body[0] >= (byte)'0' && body[0] <= (byte)'9')
        {
            number = body[0] - '0';
            contentStart = 1;
        }
        var contentSpan = body[contentStart..term];
        var content = encoding.GetString(contentSpan);
        if (isLast && content.EndsWith(AstmControl.CrChar)) content = content[..^1];
        bool ok = received == null ? !requireChecksum : string.Equals(received, computed, StringComparison.OrdinalIgnoreCase);
        return new AstmFrame
        {
            Number = number,
            Content = content,
            IsLast = isLast,
            ReceivedChecksum = received,
            ComputedChecksum = computed,
            ChecksumOk = ok,
            Error = ok ? null : (received == null ? "Відсутня контрольна сума" : $"Невірна контрольна сума: отримано {received}, обчислено {computed}")
        };
    }

    /// <summary>Розбір кадру з рядка (Latin-1 за замовчуванням).</summary>
    public static AstmFrame ParseFrame(string frame, bool requireChecksum = true, Encoding? encoding = null)
        => ParseFrame((encoding ?? DefaultEncoding).GetBytes(frame), requireChecksum, encoding);

    /// <summary>
    /// Збирає повідомлення з кадрів: вміст кадрів зчіплюється, проміжні (ETB) — без роздільника,
    /// останні (ETX) — із CR між записами. Результат: записи, розділені CR.
    /// </summary>
    public static string AssembleMessage(IEnumerable<AstmFrame> frames)
    {
        var sb = new StringBuilder();
        foreach (var f in frames)
        {
            sb.Append(f.Content);
            if (f.IsLast) sb.Append(AstmControl.CrChar);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Зчищає кадрування з «сирого» потоку ASTM (як parse_lab_message у Simplex): прибирає
    /// ETB+контрольна сума+CR LF, STX+номер кадру, ETX+контрольна сума+CR LF, ENQ/EOT/ACK/NAK.
    /// Повертає текст записів, розділених CR.
    /// </summary>
    public static string StripFraming(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new StringBuilder(raw.Length);
        int i = 0;
        while (i < raw.Length)
        {
            char ch = raw[i];
            switch (ch)
            {
                case AstmControl.StxChar:
                    i++;
                    if (i < raw.Length && char.IsDigit(raw[i])) i++; // номер кадру
                    continue;
                case AstmControl.EtbChar:
                case AstmControl.EtxChar:
                    i++;
                    // контрольна сума (2 hex) + CR LF
                    if (i + 1 < raw.Length && Uri.IsHexDigit(raw[i]) && Uri.IsHexDigit(raw[i + 1])) i += 2;
                    if (i < raw.Length && raw[i] == AstmControl.CrChar) i++;
                    if (i < raw.Length && raw[i] == AstmControl.LfChar) i++;
                    continue;
                case AstmControl.EnqChar:
                case AstmControl.EotChar:
                case AstmControl.AckChar:
                case AstmControl.NakChar:
                    i++;
                    continue;
                default:
                    sb.Append(ch);
                    i++;
                    continue;
            }
        }
        return sb.ToString();
    }
}
