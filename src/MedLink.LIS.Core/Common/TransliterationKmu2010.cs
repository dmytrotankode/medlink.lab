// =============================================================================
// MedLink LIS 4.0 — транслітерація української латиницею за Постановою КМУ №55
// від 27.01.2010 «Про впорядкування транслітерації українського алфавіту латиницею».
// Спільний клас для API (mis_patient_card.last_name_latin) та коннектора (ПІБ для приладів).
// =============================================================================
using System.Text;

namespace MedLink.LIS.Core.Common;

public static class TransliterationKmu2010
{
    // Літери з однаковою передачею незалежно від позиції
    private static readonly Dictionary<char, string> Simple = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "h", ['ґ'] = "g", ['д'] = "d", ['е'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "y",
        ['і'] = "i", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
        ['у'] = "u", ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "shch",
        // російські літери (на випадок старих карток) — найближчі відповідники
        ['ы'] = "y", ['э'] = "e", ['ё'] = "io", ['ъ'] = ""
    };

    // Літери, що передаються по-різному на початку слова та в інших позиціях
    private static readonly Dictionary<char, (string Start, string Other)> Positional = new()
    {
        ['є'] = ("ye", "ie"), ['ї'] = ("yi", "i"), ['й'] = ("y", "i"), ['ю'] = ("yu", "iu"), ['я'] = ("ya", "ia")
    };

    /// <summary>
    /// Транслітерує рядок (ПІБ, назву). М'який знак та апостроф не відтворюються; буквосполучення «зг» → «zgh».
    /// Регістр зберігається: велика літера на початку → велика перша латинська літера; слово повністю великими → великими.
    /// </summary>
    public static string ToLatin(string ukrainian)
    {
        if (string.IsNullOrEmpty(ukrainian)) return ukrainian ?? "";
        var sb = new StringBuilder(ukrainian.Length * 2);
        var wordStart = true;
        for (var i = 0; i < ukrainian.Length; i++)
        {
            var ch = ukrainian[i];
            var lower = char.ToLowerInvariant(ch);

            if (lower is 'ь' or '\'' or '’' or 'ʼ') { continue; } // не відтворюються, позиція у слові не змінюється

            if (!char.IsLetter(ch)) { sb.Append(ch); wordStart = true; continue; }

            string lat;
            if (lower == 'з' && i + 1 < ukrainian.Length && char.ToLowerInvariant(ukrainian[i + 1]) == 'г')
            {
                lat = "zgh"; i++; // «зг» → zgh
                var bothUpper = char.IsUpper(ch) && char.IsUpper(ukrainian[i]);
                sb.Append(ApplyCase(lat, ch, bothUpper || IsAllUpperWord(ukrainian, i - 1)));
                wordStart = false;
                continue;
            }
            if (Positional.TryGetValue(lower, out var pos)) lat = wordStart ? pos.Start : pos.Other;
            else if (Simple.TryGetValue(lower, out var s)) lat = s;
            else { sb.Append(ch); wordStart = false; continue; } // латиниця/інші літери — як є

            sb.Append(ApplyCase(lat, ch, IsAllUpperWord(ukrainian, i)));
            wordStart = false;
        }
        return sb.ToString();
    }

    private static string ApplyCase(string lat, char source, bool allUpper)
    {
        if (lat.Length == 0) return lat;
        if (!char.IsUpper(source)) return lat;
        if (allUpper) return lat.ToUpperInvariant();
        return char.ToUpperInvariant(lat[0]) + lat.Substring(1);
    }

    /// <summary>Чи слово, що містить позицію index, записане повністю великими літерами (≥2 літер).</summary>
    private static bool IsAllUpperWord(string s, int index)
    {
        var start = index; while (start > 0 && char.IsLetter(s[start - 1])) start--;
        var end = index; while (end + 1 < s.Length && char.IsLetter(s[end + 1])) end++;
        var letters = 0;
        for (var i = start; i <= end; i++)
        {
            if (!char.IsLetter(s[i])) continue;
            letters++;
            if (char.IsLower(s[i])) return false;
        }
        return letters >= 2;
    }
}
