// =============================================================================
// MedLink LIS 4.0 — етикетка пробірки 40×25 мм (ZPL II, 203 dpi → 320×200 dots)
// =============================================================================
using System.Text;

namespace MedLink.LIS.Core.Barcodes;

public static class ZplLabelBuilder
{
    public const int WidthDots = 320;
    public const int HeightDots = 200;

    /// <summary>
    /// Будує ZPL для термопринтера (Zebra/Xprinter): ПІБ пацієнта, Code128 (^BCN,60,Y,N,N),
    /// назва пробірки/біоматеріалу, дата-час. Кодування ^CI28 (UTF-8) для кирилиці.
    /// </summary>
    public static string Build(string barcode, string patientName, string tubeName, DateTime dateTime, string? orderNumber = null)
    {
        var sb = new StringBuilder();
        sb.Append("^XA\n");
        sb.Append("^CI28\n");
        sb.Append($"^PW{WidthDots}\n^LL{HeightDots}\n^LH0,0\n");
        sb.Append($"^FO12,8^A0N,22,22^FB296,1,0,L^FD{Escape(Truncate(patientName, 28))}^FS\n");
        sb.Append($"^FO20,36^BY2,2,60^BCN,60,Y,N,N^FD{Escape(barcode)}^FS\n");
        sb.Append($"^FO12,138^A0N,18,18^FB296,1,0,L^FD{Escape(Truncate(tubeName, 32))}^FS\n");
        var footer = dateTime.ToString("dd.MM.yyyy HH:mm") + (orderNumber != null ? $"  №{orderNumber}" : "");
        sb.Append($"^FO12,162^A0N,18,18^FB296,1,0,L^FD{Escape(footer)}^FS\n");
        sb.Append("^XZ");
        return sb.ToString();
    }

    private static string Truncate(string? s, int max) => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max - 1) + "…");

    // ^ та ~ — службові символи ZPL
    private static string Escape(string s) => s.Replace("^", " ").Replace("~", " ");
}
