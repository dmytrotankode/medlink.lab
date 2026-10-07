using MedLink.LIS.Core.Common;
using MedLink.LIS.Core.Protocols;
using MedLink.LIS.Core.Protocols.Orders;
using MedLink.LIS.Core.Protocols.Parsers;

namespace MedLink.LIS.Tests.Protocols;

public class CatalogAndTransliterationTests
{
    [Fact]
    public void Catalog_loads_64_profiles_with_parser_and_template_assigned()
    {
        var catalog = AnalyzerProfileCatalog.Default;
        Assert.Equal(64, catalog.Count);
        Assert.All(catalog.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Code));
            Assert.False(string.IsNullOrWhiteSpace(p.ParserKind), $"{p.Code}: parserKind порожній");
            Assert.False(string.IsNullOrWhiteSpace(p.OrderTemplate), $"{p.Code}: orderTemplate порожній");
            Assert.True(ParserFactory.IsKnown(p.ParserKind), $"{p.Code}: невідомий parserKind {p.ParserKind}");
            Assert.True(OrderBuilderFactory.IsKnown(p.OrderTemplate), $"{p.Code}: невідомий orderTemplate {p.OrderTemplate}");
            Assert.Contains(p.Category, new[] { "HEMATOLOGY", "BIOCHEM", "IMMUNO", "COAG", "URINE", "BLOODGAS", "OTHER" });
        });
        Assert.Equal(64, catalog.All.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Catalog_lookup_is_tolerant_to_case_spaces_and_cyrillic_C()
    {
        var c = AnalyzerProfileCatalog.Default;
        Assert.Equal("SYSMEXXN", c.GetByCode("sysmexxn")!.Code);
        Assert.Equal("SYSMEX XS-1000I", c.GetByCode("SYSMEX XS-1000I")!.Code);
        Assert.Equal("SYSMEX XS-1000I", c.GetByCode("SYSMEXXS1000I")!.Code);
        Assert.Equal("СOBAS 411", c.GetByCode("СOBAS 411")!.Code); // кирилична «С» у legacy-коді — точний збіг
        Assert.Equal("COBAS411", c.GetByCode("COBAS 411")!.Code);   // латиниця з пробілом → нормалізація до COBAS411 (id 12)
        Assert.Equal("Phadia 250", c.GetByCode("PHADIA250")!.Code);
        Assert.Null(c.GetByCode("NOPE"));
        Assert.Equal("RAPID", c.GetByCode("RAPID")!.ParserKind.Replace("TEXT_", ""));
        Assert.Equal(new byte[] { 0x02 }, c.GetByCode("RAPID")!.Bop);
        Assert.Equal(new byte[] { 0x04 }, c.GetByCode("RAPID")!.Eop);
        Assert.Equal("Sample Report", System.Text.Encoding.ASCII.GetString(c.GetByCode("ELYTEPLUS")!.Bop!));
    }

    [Fact]
    public void Catalog_assignments_follow_legacy_parse_and_order_procedures()
    {
        var c = AnalyzerProfileCatalog.Default;
        Assert.Equal(("ASTM_COBAS", "COBAS_E411"), (c.GetByCode("COBAS411")!.ParserKind, c.GetByCode("COBAS411")!.OrderTemplate));
        Assert.Equal(("ASTM_COBAS", "COBAS_C311"), (c.GetByCode("COBAS311")!.ParserKind, c.GetByCode("COBAS311")!.OrderTemplate));
        Assert.Equal(("HL7_MINDRAY", "MINDRAY_HL7"), (c.GetByCode("MINDRAY240")!.ParserKind, c.GetByCode("MINDRAY240")!.OrderTemplate));
        Assert.Equal(("TEXT_RAPID", "RAPID"), (c.GetByCode("RAPID")!.ParserKind, c.GetByCode("RAPID")!.OrderTemplate));
        Assert.Equal(("TEXT_INTEGRA", "INTEGRA"), (c.GetByCode("INTEGRA")!.ParserKind, c.GetByCode("INTEGRA")!.OrderTemplate));
        Assert.Equal(("ASTM_URISYS", "NONE"), (c.GetByCode("URISYS")!.ParserKind, c.GetByCode("URISYS")!.OrderTemplate));
        Assert.Equal("TEXT_FUJI", c.GetByCode("FUJI")!.ParserKind);
        Assert.Equal("TEXT_CYAN", c.GetByCode("CYAN")!.ParserKind);
        Assert.Equal("HL7_ICHROMA", c.GetByCode("ICHROMA3")!.ParserKind);
        Assert.Equal("COAG", c.GetByCode("BIOKSEL")!.Category);
    }

    [Fact]
    public void Default_config_from_profile_carries_framing()
    {
        var cfg = AnalyzerProfileCatalog.Default.CreateDefaultConfig("ACLTOP300");
        Assert.Equal("ASTM", cfg.Protocol);
        Assert.Equal(500, cfg.Framing.SleepMs);
        Assert.Equal("ACLTOP", cfg.OrderTemplate);
        Assert.Throws<KeyNotFoundException>(() => AnalyzerProfileCatalog.Default.CreateDefaultConfig("UNKNOWN_TYPE"));
    }

    [Theory]
    [InlineData("Коваленко", "Kovalenko")]
    [InlineData("Олена", "Olena")]
    [InlineData("Юрій", "Yurii")]
    [InlineData("Єгор", "Yehor")]
    [InlineData("Їжакевич", "Yizhakevych")]
    [InlineData("Згурський", "Zghurskyi")]
    [InlineData("Щербина", "Shcherbyna")]
    [InlineData("Мар'яна", "Mariana")]
    [InlineData("Ґудзь", "Gudz")]
    [InlineData("Хмельницький", "Khmelnytskyi")]
    [InlineData("Сергійович", "Serhiiovych")]
    [InlineData("ПЕТРЕНКО", "PETRENKO")]
    [InlineData("Smith", "Smith")]
    [InlineData("", "")]
    public void Transliteration_follows_kmu_2010(string ua, string expected)
    {
        Assert.Equal(expected, TransliterationKmu2010.ToLatin(ua));
        Assert.Equal(expected, Transliteration.UaToEn(ua));
    }

    [Fact]
    public void Latin_name_from_server_wins_over_transliteration()
    {
        Assert.Equal("Kovalenko-Smith", Transliteration.LatinOrTransliterate("Kovalenko-Smith", "Коваленко"));
        Assert.Equal("Kovalenko", Transliteration.LatinOrTransliterate("  ", "Коваленко"));
        Assert.Equal("Ko??", Transliteration.AsciiOnly("Koва"));
    }
}
