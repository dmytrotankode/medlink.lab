using MedLink.LIS.Core.Barcodes;
using MedLink.LIS.Core.Common;

namespace MedLink.LIS.Tests.Barcodes;

public class TubeBarcodeGeneratorTests
{
    [Fact]
    public void Counter_1_gives_10000014()
    {
        // '0000001'+'0' → '00000010' → перша '0'→'1' → '10000010'; сума: 1×3 + 1×3 = 6 → контрольна 4
        Assert.Equal("10000014", TubeBarcodeGenerator.Generate(1));
    }

    [Fact]
    public void Counter_4812_gives_8_digits_with_valid_check()
    {
        var bc = TubeBarcodeGenerator.Generate(4812);
        Assert.Equal(8, bc.Length);
        Assert.StartsWith("1004812", bc);
        Assert.True(TubeBarcodeGenerator.IsValid(bc));
    }

    [Fact]
    public void Leading_digit_is_kept_when_not_zero()
    {
        Assert.StartsWith("1234567", TubeBarcodeGenerator.Generate(1234567));
        Assert.StartsWith("9876543", TubeBarcodeGenerator.Generate(9876543));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(4819)]
    [InlineData(999999)]
    [InlineData(1234567)]
    public void Check_digit_follows_ean8_weights(long counter)
    {
        var bc = TubeBarcodeGenerator.Generate(counter);
        var sum = 0;
        for (var i = 1; i <= 7; i++) sum += (bc[i - 1] - '0') * (i % 2 == 1 ? 3 : 1);
        var expected = (10 - sum % 10) % 10;
        Assert.Equal(expected, bc[7] - '0');
        Assert.True(TubeBarcodeGenerator.IsValid(bc));
    }

    [Fact]
    public void Invalid_barcodes_are_rejected()
    {
        Assert.False(TubeBarcodeGenerator.IsValid("10000015"));
        Assert.False(TubeBarcodeGenerator.IsValid("1234"));
        Assert.False(TubeBarcodeGenerator.IsValid("ABCDEFGH"));
    }
}

public class Code128SvgTests
{
    [Fact]
    public void Digits_use_code_c_and_checksum()
    {
        var codes = Code128Svg.Encode("10000014");
        Assert.Equal(105, codes[0]); // Start C
        Assert.Equal(new[] { 10, 0, 0, 14 }, codes.Skip(1).Take(4));
        Assert.Equal(106, codes[^1]); // Stop
        var sum = codes[0];
        for (var i = 1; i < codes.Count - 2; i++) sum += i * codes[i];
        Assert.Equal(sum % 103, codes[^2]);
    }

    [Fact]
    public void Text_uses_code_b()
    {
        var codes = Code128Svg.Encode("S26-00123-1-HE");
        Assert.Equal(104, codes[0]);
        Assert.Equal(106, codes[^1]);
    }

    [Fact]
    public void Svg_is_non_empty_and_well_formed()
    {
        var svg = Code128Svg.Render("10048016");
        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>", svg);
        Assert.Contains("<rect", svg);
        Assert.Contains("10048016", svg);
    }

    [Fact]
    public void Non_ascii_throws() => Assert.Throws<ArgumentException>(() => Code128Svg.Encode("Пробірка"));
}

public class ZplAndQrTests
{
    [Fact]
    public void Zpl_label_contains_code128_and_fields()
    {
        var zpl = ZplLabelBuilder.Build("10048016", "Коваленко Олена Сергіївна", "Сироватка — гель", new DateTime(2026, 10, 7, 8, 15, 0), "2610-000003");
        Assert.StartsWith("^XA", zpl);
        Assert.Contains("^BCN,60,Y,N,N", zpl);
        Assert.Contains("^FD10048016^FS", zpl);
        Assert.Contains("Коваленко", zpl);
        Assert.Contains("^CI28", zpl);
        Assert.Contains("^PW320", zpl);
        Assert.EndsWith("^XZ", zpl);
    }

    [Fact]
    public void Qr_matrix_has_finder_patterns_and_valid_size()
    {
        var m = QrEncoder.Encode("http://localhost:5055/verify/abc123");
        var size = m.GetLength(0);
        Assert.Equal(0, (size - 17) % 4);
        Assert.True(size >= 21);
        // кутові пошукові шаблони: темні кути та темний центр
        Assert.True(m[0, 0] && m[0, size - 1] && m[size - 1, 0]);
        Assert.True(m[3, 3] && m[3, size - 4] && m[size - 4, 3]);
        // темний модуль (8, size-8) завжди увімкнений
        Assert.True(m[size - 8, 8]);
        // світла межа-роздільник навколо пошукового шаблону
        Assert.False(m[7, 7]);
    }

    [Fact]
    public void Qr_svg_renders_for_long_verify_url()
    {
        var url = "https://lis.medlink.ua/verify/" + new string('A', 43);
        var svg = QrSvg.Render(url);
        Assert.StartsWith("<svg", svg);
        Assert.Contains("<path", svg);
    }

    [Fact]
    public void Qr_too_long_throws() => Assert.Throws<ArgumentException>(() => QrEncoder.Encode(new string('x', 400)));
}

public class TransliterationTests
{
    [Theory]
    [InlineData("Коваленко", "Kovalenko")]
    [InlineData("Олена", "Olena")]
    [InlineData("Їжакевич", "Yizhakevych")]
    [InlineData("Юрій", "Yurii")]
    [InlineData("Згуровський", "Zghurovskyi")]
    [InlineData("Ярошенко", "Yaroshenko")]
    [InlineData("Мар'яна", "Mariana")]
    [InlineData("Щербань", "Shcherban")]
    [InlineData("Євген", "Yevhen")]
    [InlineData("Гаврилюк", "Havryliuk")]
    [InlineData("Ґалаґан", "Galagan")]
    [InlineData("ШЕВЧЕНКО", "SHEVCHENKO")]
    public void Kmu_2010_rules(string ua, string expected) => Assert.Equal(expected, TransliterationKmu2010.ToLatin(ua));
}
