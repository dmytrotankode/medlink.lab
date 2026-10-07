// =============================================================================
// MedLink LIS 4.0 — штрихкод пробірки: точне перенесення процедури Simplex
// gen_lab_tube_barcode (лічильник dct_counter 'lab_tube_barcode_ean8').
// =============================================================================
namespace MedLink.LIS.Core.Barcodes;

public static class TubeBarcodeGenerator
{
    public const string CounterCode = "lab_tube_barcode_ean8";

    /// <summary>
    /// Алгоритм Simplex:
    /// 1) значення лічильника → останні 7 цифр із доповненням нулями + «0» (8 символів);
    /// 2) якщо перший символ «0» → замінюється на «1»;
    /// 3) контрольна цифра у стилі EAN-8: позиції 1..8, непарні ×3, парні ×1
    ///    (8-ма позиція — заглушка «0»), сума mod 10, доповнення до 10;
    /// 4) результат — перші 7 символів + контрольна цифра.
    /// </summary>
    public static string Generate(long counterValue)
    {
        if (counterValue < 0) throw new ArgumentOutOfRangeException(nameof(counterValue));
        var padded = ("00000000" + counterValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var seven = padded.Substring(padded.Length - 7, 7);
        var barcode = seven + "0";
        if (barcode[0] == '0') barcode = "1" + barcode.Substring(1, 7);
        var check = CheckDigit(barcode.Substring(0, 7));
        return barcode.Substring(0, 7) + check.ToString();
    }

    /// <summary>Контрольна цифра для перших 7 цифр (вага 3 на непарних позиціях 1,3,5,7; 1 на парних).</summary>
    public static int CheckDigit(string firstSevenDigits)
    {
        if (firstSevenDigits == null || firstSevenDigits.Length != 7 || !firstSevenDigits.All(char.IsDigit))
            throw new ArgumentException("Очікується 7 цифр", nameof(firstSevenDigits));
        var body = firstSevenDigits + "0";
        var sum = 0;
        for (var i = 8; i > 0; i--)
        {
            var digit = body[i - 1] - '0';
            sum += (i % 2 == 0) ? digit * 1 : digit * 3;
        }
        var rl1 = sum % 10;
        return rl1 != 0 ? 10 - rl1 : 0;
    }

    /// <summary>Перевірка: 8 цифр, контрольна цифра коректна.</summary>
    public static bool IsValid(string? barcode)
    {
        if (string.IsNullOrEmpty(barcode) || barcode.Length != 8 || !barcode.All(char.IsDigit)) return false;
        return CheckDigit(barcode.Substring(0, 7)) == barcode[7] - '0';
    }
}
