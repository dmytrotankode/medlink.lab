// =============================================================================
// MedLink LIS 4.0 — компактний кодер QR Code (байтовий режим, рівень корекції M,
// версії 1–10) з рендером у SVG. Використовується для QR верифікації бланка.
// Реалізація за ISO/IEC 18004 (структура — як у довідковій реалізації Nayuki).
// =============================================================================
using System.Globalization;
using System.Text;

namespace MedLink.LIS.Core.Barcodes;

public static class QrSvg
{
    /// <summary>SVG з QR-кодом. moduleSize — px на модуль, quietZone — модулів тихої зони.</summary>
    public static string Render(string text, int moduleSize = 4, int quietZone = 4)
    {
        var matrix = QrEncoder.Encode(text);
        var size = matrix.GetLength(0);
        var total = (size + quietZone * 2) * moduleSize;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{total}\" height=\"{total}\" viewBox=\"0 0 {total} {total}\" shape-rendering=\"crispEdges\">");
        sb.Append(CultureInfo.InvariantCulture, $"<rect width=\"{total}\" height=\"{total}\" fill=\"#fff\"/>");
        sb.Append("<path fill=\"#000\" d=\"");
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                if (matrix[y, x])
                    sb.Append(CultureInfo.InvariantCulture, $"M{(x + quietZone) * moduleSize} {(y + quietZone) * moduleSize}h{moduleSize}v{moduleSize}h-{moduleSize}z");
        sb.Append("\"/></svg>");
        return sb.ToString();
    }
}

/// <summary>Кодер QR (byte mode, EC level M, версії 1..10). Повертає матрицю модулів [row, col].</summary>
public static class QrEncoder
{
    // Кількість EC-кодових слів на блок та кількість блоків для рівня M, версії 1..10
    private static readonly int[] EccPerBlockM = { 0, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26 };
    private static readonly int[] NumBlocksM = { 0, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5 };
    private const int MaxVersion = 10;

    public static bool[,] Encode(string text)
    {
        var data = Encoding.UTF8.GetBytes(text ?? "");
        var version = ChooseVersion(data.Length);
        var size = version * 4 + 17;
        var dataCapacityBytes = NumDataCodewords(version);

        // --- бітовий потік: 0100 + довжина + дані + термінатор + паддінг ---
        var bits = new List<bool>();
        AppendBits(bits, 0b0100, 4);
        AppendBits(bits, data.Length, version <= 9 ? 8 : 16);
        foreach (var b in data) AppendBits(bits, b, 8);
        var capacityBits = dataCapacityBytes * 8;
        AppendBits(bits, 0, Math.Min(4, capacityBits - bits.Count));
        while (bits.Count % 8 != 0) bits.Add(false);
        for (var pad = 0xEC; bits.Count < capacityBits; pad ^= 0xEC ^ 0x11) AppendBits(bits, pad, 8);

        var dataCodewords = new byte[bits.Count / 8];
        for (var i = 0; i < bits.Count; i++) if (bits[i]) dataCodewords[i >> 3] |= (byte)(0x80 >> (i & 7));

        var allCodewords = AddEccAndInterleave(dataCodewords, version);

        // --- матриця ---
        var modules = new bool[size, size];
        var isFunction = new bool[size, size];
        DrawFunctionPatterns(modules, isFunction, version, size);
        DrawCodewords(modules, isFunction, allCodewords, size);

        // --- вибір маски за мінімальним штрафом ---
        var bestMask = 0; var bestPenalty = long.MaxValue;
        for (var m = 0; m < 8; m++)
        {
            ApplyMask(modules, isFunction, m, size);
            DrawFormatBits(modules, isFunction, m, size);
            var penalty = Penalty(modules, size);
            if (penalty < bestPenalty) { bestPenalty = penalty; bestMask = m; }
            ApplyMask(modules, isFunction, m, size); // XOR — знімаємо маску
        }
        ApplyMask(modules, isFunction, bestMask, size);
        DrawFormatBits(modules, isFunction, bestMask, size);
        return modules;
    }

    private static int ChooseVersion(int byteLen)
    {
        for (var v = 1; v <= MaxVersion; v++)
        {
            var need = 4 + (v <= 9 ? 8 : 16) + byteLen * 8;
            if (need <= NumDataCodewords(v) * 8) return v;
        }
        throw new ArgumentException($"Текст задовгий для QR версій 1..{MaxVersion} (рівень M)");
    }

    private static int NumRawDataModules(int ver)
    {
        var result = (16 * ver + 128) * ver + 64;
        if (ver >= 2)
        {
            var numAlign = ver / 7 + 2;
            result -= (25 * numAlign - 10) * numAlign - 55;
            if (ver >= 7) result -= 36;
        }
        return result;
    }

    private static int NumDataCodewords(int ver) => NumRawDataModules(ver) / 8 - EccPerBlockM[ver] * NumBlocksM[ver];

    private static void AppendBits(List<bool> bits, int value, int len)
    {
        for (var i = len - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
    }

    // ---------------- Reed–Solomon ----------------
    private static byte[] AddEccAndInterleave(byte[] data, int ver)
    {
        var numBlocks = NumBlocksM[ver];
        var blockEccLen = EccPerBlockM[ver];
        var rawCodewords = NumRawDataModules(ver) / 8;
        var numShortBlocks = numBlocks - rawCodewords % numBlocks;
        var shortBlockLen = rawCodewords / numBlocks;

        var blocks = new List<byte[]>();
        var rsDiv = ReedSolomonDivisor(blockEccLen);
        for (int i = 0, k = 0; i < numBlocks; i++)
        {
            var datLen = shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1);
            var dat = new byte[datLen];
            Array.Copy(data, k, dat, 0, datLen);
            k += datLen;
            var block = new byte[shortBlockLen + 1];
            Array.Copy(dat, block, datLen);
            var ecc = ReedSolomonRemainder(dat, rsDiv);
            Array.Copy(ecc, 0, block, block.Length - blockEccLen, blockEccLen);
            blocks.Add(block);
        }

        var result = new byte[rawCodewords];
        var idx = 0;
        for (var i = 0; i < blocks[0].Length; i++)
            for (var j = 0; j < blocks.Count; j++)
                if (i != shortBlockLen - blockEccLen || j >= numShortBlocks)
                    result[idx++] = blocks[j][i];
        return result;
    }

    private static byte[] ReedSolomonDivisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;
        var root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < result.Length; j++)
            {
                result[j] = (byte)GfMultiply(result[j], root);
                if (j + 1 < result.Length) result[j] ^= result[j + 1];
            }
            root = GfMultiply(root, 0x02);
        }
        return result;
    }

    private static byte[] ReedSolomonRemainder(byte[] data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (var b in data)
        {
            var factor = (b ^ result[0]) & 0xFF;
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++) result[i] ^= (byte)GfMultiply(divisor[i], factor);
        }
        return result;
    }

    private static int GfMultiply(int x, int y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return z & 0xFF;
    }

    // ---------------- функціональні шаблони ----------------
    private static void DrawFunctionPatterns(bool[,] modules, bool[,] isFunction, int ver, int size)
    {
        for (var i = 0; i < size; i++)
        {
            SetFunction(modules, isFunction, 6, i, i % 2 == 0);
            SetFunction(modules, isFunction, i, 6, i % 2 == 0);
        }
        DrawFinder(modules, isFunction, 3, 3, size);
        DrawFinder(modules, isFunction, size - 4, 3, size);
        DrawFinder(modules, isFunction, 3, size - 4, size);

        var align = AlignmentPositions(ver, size);
        for (var i = 0; i < align.Length; i++)
            for (var j = 0; j < align.Length; j++)
            {
                if ((i == 0 && j == 0) || (i == 0 && j == align.Length - 1) || (i == align.Length - 1 && j == 0)) continue;
                DrawAlignment(modules, isFunction, align[i], align[j]);
            }

        DrawFormatBits(modules, isFunction, 0, size); // резервування
        DrawVersion(modules, isFunction, ver, size);
    }

    private static int[] AlignmentPositions(int ver, int size)
    {
        if (ver == 1) return Array.Empty<int>();
        var numAlign = ver / 7 + 2;
        var step = (ver == 32) ? 26 : (ver * 4 + numAlign * 2 + 1) / (numAlign * 2 - 2) * 2;
        var result = new int[numAlign];
        result[0] = 6;
        for (int i = result.Length - 1, pos = size - 7; i >= 1; i--, pos -= step) result[i] = pos;
        return result;
    }

    private static void DrawFinder(bool[,] modules, bool[,] isFunction, int x, int y, int size)
    {
        for (var dy = -4; dy <= 4; dy++)
            for (var dx = -4; dx <= 4; dx++)
            {
                var dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var xx = x + dx; var yy = y + dy;
                if (xx >= 0 && xx < size && yy >= 0 && yy < size)
                    SetFunction(modules, isFunction, xx, yy, dist != 2 && dist != 4);
            }
    }

    private static void DrawAlignment(bool[,] modules, bool[,] isFunction, int x, int y)
    {
        for (var dy = -2; dy <= 2; dy++)
            for (var dx = -2; dx <= 2; dx++)
                SetFunction(modules, isFunction, x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
    }

    private static void DrawFormatBits(bool[,] modules, bool[,] isFunction, int mask, int size)
    {
        var data = (0 << 3) | mask; // рівень M = 00
        var rem = data;
        for (var i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
        var bits = ((data << 10) | rem) ^ 0x5412;

        for (var i = 0; i <= 5; i++) SetFunction(modules, isFunction, 8, i, Bit(bits, i));
        SetFunction(modules, isFunction, 8, 7, Bit(bits, 6));
        SetFunction(modules, isFunction, 8, 8, Bit(bits, 7));
        SetFunction(modules, isFunction, 7, 8, Bit(bits, 8));
        for (var i = 9; i < 15; i++) SetFunction(modules, isFunction, 14 - i, 8, Bit(bits, i));

        for (var i = 0; i < 8; i++) SetFunction(modules, isFunction, size - 1 - i, 8, Bit(bits, i));
        for (var i = 8; i < 15; i++) SetFunction(modules, isFunction, 8, size - 15 + i, Bit(bits, i));
        SetFunction(modules, isFunction, 8, size - 8, true); // темний модуль
    }

    private static void DrawVersion(bool[,] modules, bool[,] isFunction, int ver, int size)
    {
        if (ver < 7) return;
        var rem = ver;
        for (var i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
        var bits = (ver << 12) | rem;
        for (var i = 0; i < 18; i++)
        {
            var bit = Bit(bits, i);
            var a = size - 11 + i % 3;
            var b = i / 3;
            SetFunction(modules, isFunction, a, b, bit);
            SetFunction(modules, isFunction, b, a, bit);
        }
    }

    private static void DrawCodewords(bool[,] modules, bool[,] isFunction, byte[] data, int size)
    {
        var i = 0;
        for (var right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (var vert = 0; vert < size; vert++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? size - 1 - vert : vert;
                    if (!isFunction[y, x] && i < data.Length * 8)
                    {
                        modules[y, x] = Bit(data[i >> 3], 7 - (i & 7));
                        i++;
                    }
                }
            }
        }
    }

    private static void ApplyMask(bool[,] modules, bool[,] isFunction, int mask, int size)
    {
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                if (isFunction[y, x]) continue;
                var invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (x / 3 + y / 2) % 2 == 0,
                    5 => x * y % 2 + x * y % 3 == 0,
                    6 => (x * y % 2 + x * y % 3) % 2 == 0,
                    _ => ((x + y) % 2 + x * y % 3) % 2 == 0
                };
                if (invert) modules[y, x] = !modules[y, x];
            }
    }

    private static long Penalty(bool[,] m, int size)
    {
        long result = 0;
        // правило 1: серії ≥5 однакових у рядках/стовпцях
        for (var y = 0; y < size; y++)
        {
            var runColor = m[y, 0]; var run = 1;
            for (var x = 1; x < size; x++)
            {
                if (m[y, x] == runColor) { run++; if (run == 5) result += 3; else if (run > 5) result++; }
                else { runColor = m[y, x]; run = 1; }
            }
        }
        for (var x = 0; x < size; x++)
        {
            var runColor = m[0, x]; var run = 1;
            for (var y = 1; y < size; y++)
            {
                if (m[y, x] == runColor) { run++; if (run == 5) result += 3; else if (run > 5) result++; }
                else { runColor = m[y, x]; run = 1; }
            }
        }
        // правило 2: блоки 2×2
        for (var y = 0; y < size - 1; y++)
            for (var x = 0; x < size - 1; x++)
            {
                var c = m[y, x];
                if (c == m[y, x + 1] && c == m[y + 1, x] && c == m[y + 1, x + 1]) result += 3;
            }
        // правило 4: баланс темних модулів
        var dark = 0;
        for (var y = 0; y < size; y++) for (var x = 0; x < size; x++) if (m[y, x]) dark++;
        var total = size * size;
        var k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
        result += Math.Max(0, k) * 10;
        return result;
    }

    private static void SetFunction(bool[,] modules, bool[,] isFunction, int x, int y, bool dark)
    {
        modules[y, x] = dark;
        isFunction[y, x] = true;
    }

    private static bool Bit(int value, int i) => ((value >> i) & 1) != 0;
}
