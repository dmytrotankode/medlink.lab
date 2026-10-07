// =============================================================================
// MedLink LIS 4.0 — кодер Code 128 (набори B/C) із рендером у SVG
// =============================================================================
using System.Globalization;
using System.Text;

namespace MedLink.LIS.Core.Barcodes;

public static class Code128Svg
{
    // 107 шаблонів (ширини штрихів/пробілів), індекси 0..106; 106 — STOP (7 елементів)
    private static readonly string[] Patterns =
    {
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112"
    };

    private const int StartB = 104;
    private const int StartC = 105;
    private const int CodeC = 99;
    private const int CodeB = 100;
    private const int Stop = 106;

    /// <summary>Повертає послідовність кодових значень (включно зі старт/контрольна сума/стоп).</summary>
    public static List<int> Encode(string text)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("Порожній текст", nameof(text));
        if (text.Any(c => c < 32 || c > 126)) throw new ArgumentException("Code128 B підтримує лише ASCII 32..126", nameof(text));

        var codes = new List<int>();
        var allDigits = text.All(char.IsDigit);
        if (allDigits && text.Length % 2 == 0 && text.Length >= 2)
        {
            codes.Add(StartC);
            for (var i = 0; i < text.Length; i += 2)
                codes.Add(int.Parse(text.Substring(i, 2), CultureInfo.InvariantCulture));
        }
        else
        {
            codes.Add(StartB);
            var i = 0;
            while (i < text.Length)
            {
                // перемикання на C для ≥4 цифр підряд (парна кількість)
                var run = 0;
                while (i + run < text.Length && char.IsDigit(text[i + run])) run++;
                if (run >= 4)
                {
                    if (run % 2 == 1) { codes.Add(text[i] - 32); i++; run--; }
                    codes.Add(CodeC);
                    for (var k = 0; k < run; k += 2) codes.Add(int.Parse(text.Substring(i + k, 2), CultureInfo.InvariantCulture));
                    i += run;
                    if (i < text.Length) codes.Add(CodeB);
                }
                else
                {
                    codes.Add(text[i] - 32);
                    i++;
                }
            }
        }

        // контрольна сума: start + Σ(i × value) mod 103
        var sum = codes[0];
        for (var k = 1; k < codes.Count; k++) sum += k * codes[k];
        codes.Add(sum % 103);
        codes.Add(Stop);
        return codes;
    }

    /// <summary>Рендер у SVG. moduleWidth — ширина модуля у px, height — висота штрихів, тиха зона 10 модулів.</summary>
    public static string Render(string text, int height = 60, int moduleWidth = 2, bool showText = true)
    {
        var codes = Encode(text);
        var quiet = 10;
        var totalModules = quiet * 2 + codes.Sum(c => Patterns[c].Sum(ch => ch - '0'));
        var width = totalModules * moduleWidth;
        var textHeight = showText ? 14 : 0;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height + textHeight + 2}\" viewBox=\"0 0 {width} {height + textHeight + 2}\" shape-rendering=\"crispEdges\">");
        sb.Append(CultureInfo.InvariantCulture, $"<rect width=\"{width}\" height=\"{height + textHeight + 2}\" fill=\"#fff\"/>");
        var x = quiet * moduleWidth;
        foreach (var code in codes)
        {
            var pattern = Patterns[code];
            for (var i = 0; i < pattern.Length; i++)
            {
                var w = (pattern[i] - '0') * moduleWidth;
                if (i % 2 == 0)
                    sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x}\" y=\"0\" width=\"{w}\" height=\"{height}\" fill=\"#000\"/>");
                x += w;
            }
        }
        if (showText)
            sb.Append(CultureInfo.InvariantCulture,
                $"<text x=\"{width / 2}\" y=\"{height + textHeight}\" font-family=\"monospace\" font-size=\"12\" text-anchor=\"middle\" fill=\"#000\">{System.Security.SecurityElement.Escape(text)}</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }
}
