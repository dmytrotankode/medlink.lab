// =============================================================================
// MedLink LIS 4.0 — транслітерація UA→EN для P-записів ASTM / PID HL7
// (прилади не підтримують кирилицю). Делегує до спільного
// MedLink.LIS.Core.Common.TransliterationKmu2010 (Постанова КМУ №55 від 27.01.2010).
// Замінює функцію transliteration_word(..., 'UA-EN') із Simplex.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text;
using MedLink.LIS.Core.Common;

namespace MedLink.LIS.Core.Protocols;

public static class Transliteration
{
    /// <summary>Транслітерує український (і російський) текст латиницею за КМУ №55 (2010).</summary>
    public static string UaToEn(string? text) => TransliterationKmu2010.ToLatin(text);

    /// <summary>Повертає латинське ім'я: якщо сервер уже передав латинський варіант (lastNameLatin), використовує його; інакше транслітерує.</summary>
    public static string LatinOrTransliterate(string? latin, string? original)
        => !string.IsNullOrWhiteSpace(latin) ? latin.Trim() : UaToEn(original);

    /// <summary>Залишає лише ASCII (для приладів, що падають на нестандартних символах).</summary>
    public static string AsciiOnly(string? text, char replacement = '?')
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length);
        foreach (var c in text) sb.Append(c < 128 ? c : replacement);
        return sb.ToString();
    }
}
