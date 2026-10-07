// =============================================================================
// MedLink LIS 4.0 — транслітерація українського алфавіту латиницею за
// Постановою КМУ № 55 від 27.01.2010 «Про впорядкування транслітерації
// українського алфавіту латиницею». Спільна для API та коннектора.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text;

namespace MedLink.LIS.Core.Common;

public static class TransliterationKmu2010
{
    private static readonly Dictionary<char, string> Map = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "h", ['ґ'] = "g", ['д'] = "d", ['е'] = "e", ['є'] = "ie",
        ['ж'] = "zh", ['з'] = "z", ['и'] = "y", ['і'] = "i", ['ї'] = "i", ['й'] = "i", ['к'] = "k", ['л'] = "l",
        ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
        ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "shch", ['ь'] = "", ['ю'] = "iu",
        ['я'] = "ia", ['\''] = "", ['’'] = "", ['ʼ'] = "", ['`'] = "",
        // літери російського алфавіту (трапляються у старих картках пацієнтів)
        ['ы'] = "y", ['э'] = "e", ['ё'] = "io", ['ъ'] = "",
    };

    /// <summary>На початку слова: Є→Ye, Ї→Yi, Й→Y, Ю→Yu, Я→Ya.</summary>
    private static readonly Dictionary<char, string> WordStart = new()
    {
        ['є'] = "ye", ['ї'] = "yi", ['й'] = "y", ['ю'] = "yu", ['я'] = "ya",
    };

    /// <summary>
    /// Транслітерує текст латиницею за КМУ №55 (2010): «зг» → «zgh», апостроф і м'який знак не відтворюються,
    /// слово ВЕЛИКИМИ літерами → латиниця великими. Латиниця, цифри та розділові знаки — без змін.
    /// </summary>
    public static string ToLatin(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length * 2);
        bool wordStart = true;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char lower = char.ToLowerInvariant(c);
            bool allUpper = char.IsUpper(c) && ((i + 1 < text.Length && char.IsUpper(text[i + 1])) || (i > 0 && char.IsUpper(text[i - 1])));
            if (lower == 'з' && i + 1 < text.Length && char.ToLowerInvariant(text[i + 1]) == 'г')
            {
                Append(sb, "zgh", char.IsUpper(c), allUpper);
                i++;
                wordStart = false;
                continue;
            }
            if (Map.TryGetValue(lower, out var latin))
            {
                if (wordStart && WordStart.TryGetValue(lower, out var ws)) latin = ws;
                Append(sb, latin, char.IsUpper(c), allUpper);
                wordStart = false;
            }
            else
            {
                sb.Append(c);
                wordStart = !char.IsLetterOrDigit(c);
            }
        }
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, string latin, bool capitalize, bool allUpper)
    {
        if (latin.Length == 0) return;
        if (allUpper) sb.Append(latin.ToUpperInvariant());
        else if (capitalize) sb.Append(char.ToUpperInvariant(latin[0])).Append(latin, 1, latin.Length - 1);
        else sb.Append(latin);
    }
}
