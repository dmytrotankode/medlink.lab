// =============================================================================
// Додаткові довідники ЛІС: синоніми показників, виробничий календар, номенклатура реагентів,
// цитологічні довідники й тарифи патоморфології (винесені з коду), версії шаблонів бланків результатів.
// Усі таблиці — [ЛІС]; таблиці MedLink не змінюються.
// =============================================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

/// <summary>[ЛІС] Синонім або альтернативна назва показника для пошуку (FR-GAP-052): «глікований гемоглобін» → HBA1C.</summary>
[Table("lab_test_synonym")]
public class LabTestSynonym : GuidEntity
{
    [MaxLength(64)] public string TestCode { get; set; } = "";
    [MaxLength(256)] public string Synonym { get; set; } = "";
    /// <summary>Мова синоніма: uk, en, ru… (довільно, для довідки).</summary>
    [MaxLength(8)] public string Language { get; set; } = "uk";
    public bool IsActive { get; set; } = true;
}

/// <summary>Види днів виробничого календаря.</summary>
public static class CalendarDayKinds
{
    /// <summary>Вихідний/святковий день у звичайний робочий тиждень.</summary>
    public const string Holiday = "HOLIDAY";
    /// <summary>Робочий день на вихідний (перенесення).</summary>
    public const string Workday = "WORKDAY";
    public static readonly string[] All = { Holiday, Workday };
    public static string Label(string? k) => k switch { Holiday => "Вихідний", Workday => "Робочий (перенесення)", _ => k ?? "—" };
}

/// <summary>[ЛІС] Виняток виробничого календаря (FR-GAP-053): вихідний/святковий або робочий день на вихідний. Решта днів — за робочим тижнем пн–пт.</summary>
[Table("lab_work_calendar")]
public class LabWorkCalendarDay : GuidEntity
{
    /// <summary>Дата (без часу, UTC-північ).</summary>
    public DateTime Day { get; set; }
    /// <summary>HOLIDAY | WORKDAY</summary>
    [MaxLength(16)] public string Kind { get; set; } = CalendarDayKinds.Holiday;
    [MaxLength(256)] public string? Name { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>[ЛІС] Номенклатура реагентів: окремо від лотів (lab_reagent_lot) — один реагент, багато лотів.</summary>
[Table("lab_reagent_item")]
public class LabReagentItem : GuidEntity
{
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    [MaxLength(128)] public string? Manufacturer { get; set; }
    [MaxLength(64)] public string? CatalogNumber { get; set; }
    /// <summary>Одиниця обліку: тест, мл, набір…</summary>
    [MaxLength(32)] public string? Unit { get; set; }
    [MaxLength(64)] public string? TestCode { get; set; }
    /// <summary>Умови зберігання (наприклад, «+2…+8 °C»).</summary>
    [MaxLength(64)] public string? Storage { get; set; }
    public int MinimumTests { get; set; } = 50;
    public bool IsActive { get; set; } = true;
}

/// <summary>Види позицій цитологічного довідника.</summary>
public static class CytoItemKinds
{
    public const string Material = "MATERIAL", Adequacy = "ADEQUACY", Category = "CATEGORY";
    public static readonly string[] All = { Material, Adequacy, Category };
}

/// <summary>[ЛІС] Позиція цитологічного довідника: вид матеріалу, адекватність або категорія висновку (раніше — константи PathCytology).</summary>
[Table("lab_path_cyto_item")]
public class LabPathCytoItem : GuidEntity
{
    /// <summary>MATERIAL | ADEQUACY | CATEGORY</summary>
    [MaxLength(16)] public string Kind { get; set; } = CytoItemKinds.Category;
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(256)] public string Name { get; set; } = "";
    /// <summary>Для категорій: потребує повідомлення направнику (FR-CYT-008).</summary>
    public bool IsHigh { get; set; }
    /// <summary>Для категорій: «нормальна» (скринінг NORMAL збігається з нею).</summary>
    public bool IsNormal { get; set; }
    /// <summary>Для категорій: коди видів матеріалу через кому, до яких категорія застосовується; порожньо — до всіх.</summary>
    [MaxLength(256)] public string? Materials { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>[ЛІС] Тариф патоморфології (раніше — константи PathPrices): вирізка, ІГХ, консультація, CITO, цитологія; код → значення.</summary>
[Table("lab_path_price")]
public class LabPathPrice : GuidEntity
{
    /// <summary>FIRST_BLOCK | NEXT_BLOCK | IHC | CONSULT_SLIDE | CITO | CYTO_SLIDE | IHC_EXTRA_DAYS</summary>
    [MaxLength(32)] public string Code { get; set; } = "";
    [MaxLength(128)] public string Name { get; set; } = "";
    /// <summary>Ціна, грн (для IHC_EXTRA_DAYS — кількість робочих днів).</summary>
    public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>[ЛІС] Версія шаблону бланка результатів (варіант final | preliminary | cito): реквізити шапки/підвалу й мови; активна лише одна версія варіанта.</summary>
[Table("lab_report_template")]
public class LabReportTemplate : GuidEntity
{
    /// <summary>final | preliminary | cito</summary>
    [MaxLength(16)] public string Variant { get; set; } = "final";
    public int Version { get; set; } = 1;
    [MaxLength(128)] public string Name { get; set; } = "";
    /// <summary>Заголовок бланка; порожньо — стандартний «Результати лабораторних досліджень».</summary>
    [MaxLength(256)] public string? Title { get; set; }
    /// <summary>Додатковий рядок під шапкою (наприклад, акредитація).</summary>
    [MaxLength(512)] public string? HeaderNote { get; set; }
    /// <summary>Текст підвалу; порожньо — з налаштувань лабораторії.</summary>
    public string? FooterText { get; set; }
    /// <summary>Мови бланка через кому: uk, en. Для en заголовок виводиться двома мовами.</summary>
    [MaxLength(32)] public string Languages { get; set; } = "uk";
    [MaxLength(512)] public string? Note { get; set; }
    public bool IsActive { get; set; }
}
