using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;
using MedLink.LIS.Core.Protocols.Parsers;

namespace MedLink.LIS.Tests.Protocols;

public class AstmParserTests
{
    private static AnalyzerConfigDto Cfg(string typeCode) => AnalyzerProfileCatalog.Default.CreateDefaultConfig(typeCode, "t");

    [Fact]
    public void AstmParser_builds_records_with_fields_and_components()
    {
        var msg = AstmParser.Parse(SampleFiles.Load("04_astm_results_sysmex_xn.txt"));
        Assert.Equal(9, msg.Records.Count);
        Assert.Equal("H", msg.Records[0].Type);
        Assert.Equal("Sysmex", msg.Header!.Component(5, 1));
        var r1 = msg.OfType("R").First();
        Assert.Equal("WBC", r1.Component(3, 4));
        Assert.Equal("7.45", r1.Field(4));
        Assert.Equal("10*9/L", r1.Field(5));
        Assert.Equal(5, msg.OfType("R").Count());
    }

    [Fact]
    public void Sysmex_query_sample_is_parsed_as_query_with_barcode()
    {
        var parsed = ParserFactory.ForConfig(Cfg("SYSMEXXN")).Parse(Cfg("SYSMEXXN"), SampleFiles.Load("01_astm_query_sysmex.txt"));
        Assert.Equal(InboundMessageKind.Query, parsed.Kind);
        Assert.Equal("1026004819", parsed.Barcode);
        Assert.Equal("^1026004819", parsed.RawQueryToken);
    }

    [Fact]
    public void Sysmex_query_with_rack_position_token_takes_barcode_component()
    {
        var raw = "H|\\^&|||Sysmex^XN-1000|||||||P|E1394-97|20261006153000\rQ|1|^^         1^1026004819^B||ALL||||||||O\rL|1|N\r";
        var parsed = new AstmSysmexParser().Parse(Cfg("SYSMEXXN"), raw);
        Assert.Equal(InboundMessageKind.Query, parsed.Kind);
        Assert.Equal("1026004819", parsed.Barcode);
        Assert.Equal("B", parsed.SamplePosition);
    }

    [Fact]
    public void Cobas_results_sample_gives_two_results_with_units_flags_and_patient()
    {
        var cfg = Cfg("COBAS411");
        var parsed = ParserFactory.ForConfig(cfg).Parse(cfg, SampleFiles.Load("03_astm_results_cobas.txt"));
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal("1026004819", parsed.Barcode);
        Assert.Equal(2, parsed.Results.Count);
        Assert.Equal("TSH", parsed.Results[0].AnalyzerCode);
        Assert.Equal("1.45", parsed.Results[0].Value);
        Assert.Equal("uIU/mL", parsed.Results[0].Unit);
        Assert.Equal("N", parsed.Results[0].Flags);
        Assert.Equal("0.4^4.0", parsed.Results[0].ReferenceText);
        Assert.Equal(new DateTime(2026, 10, 6, 15, 40, 12), parsed.Results[0].MeasuredAt);
        Assert.Equal("FT4", parsed.Results[1].AnalyzerCode);
        Assert.NotNull(parsed.Patient);
        Assert.Equal("Коваленко", parsed.Patient!.LastName);
        Assert.Equal("Олександр", parsed.Patient.FirstName);
        Assert.Equal(new DateTime(1985, 4, 12), parsed.Patient.BirthDate);
        Assert.Equal("M", parsed.Patient.Gender);
    }

    [Fact]
    public void Sysmex_xn_results_sample_gives_five_results()
    {
        var cfg = Cfg("SYSMEXXN");
        var parsed = ParserFactory.ForConfig(cfg).Parse(cfg, SampleFiles.Load("04_astm_results_sysmex_xn.txt"));
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal(new[] { "WBC", "RBC", "HGB", "HCT", "PLT" }, parsed.Results.Select(r => r.AnalyzerCode).ToArray());
        Assert.All(parsed.Results, r => Assert.Equal("1026004819", r.Barcode));
        Assert.Equal("148", parsed.Results[2].Value);
    }

    [Fact]
    public void Cobas_parser_handles_seq_barcode_type_token_bang_and_slash_quirks()
    {
        var raw = "H|\\^&|||||||||||P|1\rP|1\rO|1|  3^10260048^S1^SC|0^50002^1^^S1^SC|^^^GLU^\\^^^ALT^|R||||||A||||1|||||||||||F\r" +
                  "R|1|^^^GLU/2^|5.21!|mmol/L||N||F\rR|2|^^^ALT^|32|U/L||H||F\rL|1|N\r";
        var parsed = new AstmCobasParser().Parse(Cfg("COBAS411"), raw);
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal("10260048", parsed.Barcode);
        Assert.Equal("GLU", parsed.Results[0].AnalyzerCode);
        Assert.Equal("5.21", parsed.Results[0].Value);
        Assert.Equal("ALT", parsed.Results[1].AnalyzerCode);
        Assert.Equal("H", parsed.Results[1].Flags);
    }

    [Fact]
    public void Cobas_query_token_keeps_position_tail_for_order_builder()
    {
        var raw = "H|\\^&|||||||||||P|1\rQ|1|1^10260048^S1^SC||ALL||||||||O\rL|1|N\r";
        var parsed = new AstmCobasParser().Parse(Cfg("COBAS411"), raw);
        Assert.Equal(InboundMessageKind.Query, parsed.Kind);
        Assert.Equal("10260048", parsed.Barcode);
        Assert.Equal("S1^SC", parsed.SamplePosition);
    }

    [Fact]
    public void Abl_parser_takes_barcode_from_patient_record()
    {
        var raw = "H|\\^&|||ABL80|||||||P|1\rP|1||||10260048\rO|1|||^^^pH\rR|1|^^^pH^|7.41|||N\rR|2|^^^pCO2^|38.2|mmHg||N\rL|1|N\r";
        var parsed = new AstmAblParser().Parse(Cfg("ABL80"), raw);
        Assert.Equal("10260048", parsed.Barcode);
        Assert.Equal(2, parsed.Results.Count);
        Assert.Equal("pH", parsed.Results[0].AnalyzerCode);
    }

    [Fact]
    public void Urisys_parser_reads_code_from_second_component_and_value_before_caret()
    {
        var raw = SampleFiles.Load("10_astm_urisys_1100_results.txt");
        var parsed = new AstmUrisysParser().Parse(Cfg("URISYS"), raw);
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal("10260051", parsed.Barcode);
        var sg = parsed.Results.First(r => r.AnalyzerCode == "SG");
        Assert.Equal("1.020", sg.Value);
        var leu = parsed.Results.First(r => r.AnalyzerCode == "LEU");
        Assert.Equal("neg", leu.Value);
        Assert.Contains(parsed.Results, r => r.AnalyzerCode == "pH" && r.Value == "6.0");
        Assert.Equal(10, parsed.Results.Count);
    }

    [Fact]
    public void BioKsel_parser_combines_record_number_and_method_code()
    {
        var raw = "H|\\^&|||HOST\rP|1|||||||||||||||||||||||||||||||||\rO|1|10260048||0001|R\rR|1|0001|12.5|s\rR|2|0001|98|%\rR|1|0002|31.2|s\rL|1|N\r";
        var parsed = new AstmBioKselParser().Parse(Cfg("BIOKSEL"), raw);
        Assert.Equal(new[] { "1-0001", "2-0001", "1-0002" }, parsed.Results.Select(r => r.AnalyzerCode).ToArray());
        Assert.All(parsed.Results, r => Assert.Equal("10260048", r.Barcode));
    }

    [Fact]
    public void Mindray_astm_parser_reads_code_between_carets_and_strips_value_carets()
    {
        var raw = "H|\\^&|||Mindray^^|||||||SA|1394-97|20240517\rP|1\rO|1|10004883|^Glucose^^|R|20240517113038|20240517113014|||||||20240517113014|serum||||||||||F|||||\r" +
                  "R|1|^Glucose (GOD-POD Method)^^F|5.43^^^^|mmol/L|^|N||F\rL|1|N\r";
        var parsed = new AstmMindrayParser().Parse(Cfg("MINDRAY"), raw);
        Assert.Equal("10004883", parsed.Barcode);
        Assert.Single(parsed.Results);
        Assert.Equal("Glucose (GOD-POD Method)", parsed.Results[0].AnalyzerCode);
        Assert.Equal("5.43", parsed.Results[0].Value);
    }

    [Fact]
    public void Vitros_parser_extracts_assay_number_from_plus_notation()
    {
        var raw = "H|\\^&|||VITROS\rP|1\rO|1|70648003^3^1||^^^1.0000+314+1.0|S||||||N||||5||||||||||F\rR|1|^^^1.0000+314+1.0|65.0|umol/L||^0^||V\rL|1|N\r";
        var parsed = new AstmVitrosParser().Parse(Cfg("VITROS"), raw);
        Assert.Equal("70648003", parsed.Barcode);
        Assert.Equal("314", parsed.Results[0].AnalyzerCode);
        Assert.Equal("65.0", parsed.Results[0].Value);
    }

    [Fact]
    public void Mek7300_parser_uses_fourth_component_as_code_and_P4_barcode()
    {
        var raw = "H|\\^&|||MEK\rP|1|||10116418 0001|&H&&N&\rO|1\rR|1|^^2A0100000019301^WBC^JC10|4.8|10e3/uL^9^1|4.0-9.0||||||||MEK-7300\rL|1|N\r";
        var parsed = new AstmMek7300Parser().Parse(Cfg("MEK7300"), raw);
        Assert.Equal("10116418", parsed.Barcode);
        Assert.Equal("WBC", parsed.Results[0].AnalyzerCode);
        Assert.Equal("4.8", parsed.Results[0].Value);
    }

    [Fact]
    public void Iflash_parser_builds_two_component_code_and_O4_barcode()
    {
        var raw = "H|\\^&|||iFlash\rP|1\rO|1|^|10159767|368^TSH_1^1.000000^^0|R|20250605172110\rR|1|368^TSH_1^0^F|1.853^|µIU/mL||N||F\rL|1|N\r";
        var parsed = new AstmIflashParser().Parse(Cfg("IFLASH"), raw);
        Assert.Equal("10159767", parsed.Barcode);
        Assert.Equal("368^TSH_1", parsed.Results[0].AnalyzerCode);
        Assert.Equal("1.853", parsed.Results[0].Value);
    }

    [Fact]
    public void HumaStar_parser_reads_batch_results_with_value_in_field_8()
    {
        var raw = "H|\\^&|||HS100\rP|1||10260048|Department1|Kovalenko|Olena|19850412|FEMALE\rR|1|GLU|||||5.4|mmol/L\rR|1|ALT|||||31|U/L\rP|2||10260049|Department1|X|Y\rR|1|GLU|||||6.1|mmol/L\rL||N\r";
        var parsed = new AstmHumaStarParser().Parse(Cfg("HUMASTAR"), raw);
        Assert.Equal(3, parsed.Results.Count);
        Assert.Equal("10260048", parsed.Results[0].Barcode);
        Assert.Equal("5.4", parsed.Results[0].Value);
        Assert.Equal("10260049", parsed.Results[2].Barcode);
        Assert.Equal(2, parsed.Barcodes.Count);
    }

    [Fact]
    public void Generic_parser_collects_multiple_query_barcodes_and_marks_qc_orders()
    {
        var raw = "H|\\^&\rQ|1|^10260048||ALL\rQ|2|^10260049||ALL\rL|1|N\r";
        var parsed = new AstmGenericParser().Parse(Cfg("SIEMENS"), raw);
        Assert.Equal(InboundMessageKind.Query, parsed.Kind);
        Assert.Equal(new[] { "10260048", "10260049" }, parsed.Barcodes.ToArray());

        var qc = "H|\\^&\rP|1\rO|1|QC1234567||^^^GLU|R||||||Q||||QC Level 1\rR|1|^^^GLU|5.0|mmol/L\rL|1|N\r";
        var parsedQc = new AstmGenericParser().Parse(Cfg("SIEMENS"), qc);
        Assert.True(parsedQc.IsQc);
    }

    [Fact]
    public void Generic_parser_never_throws_on_garbage_and_marks_other()
    {
        var parsed = new AstmGenericParser().Parse(Cfg("SIEMENS"), "\u0002garbage\u0003ZZ\r\n\u0004");
        Assert.Equal(InboundMessageKind.Other, parsed.Kind);
        var empty = new AstmGenericParser().Parse(Cfg("SIEMENS"), "");
        Assert.Equal(InboundMessageKind.Other, empty.Kind);
        Assert.NotEmpty(empty.Warnings);
        var ack = new AstmGenericParser().Parse(Cfg("SIEMENS"), "\u0006");
        Assert.Equal(InboundMessageKind.Ack, ack.Kind);
    }

    [Fact]
    public void Astm_parser_delegates_to_hl7_when_message_is_hl7()
    {
        var cfg = Cfg("ACCESS");
        var parsed = new AstmGenericParser().Parse(cfg, SampleFiles.Load("05_hl7_oru_r01_mindray.hl7"));
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal(5, parsed.Results.Count);
    }

    [Fact]
    public void SplitBarcodeToken_picks_barcode_like_component()
    {
        Assert.Equal(("10260048", "S1^SC", "1"), AstmParseHelpers.SplitBarcodeToken("1^10260048^S1^SC"));
        Assert.Equal(("1026004819", "B", "^^1"), AstmParseHelpers.SplitBarcodeToken("^^   1^1026004819^B"));
        Assert.Equal(("1026004819", "", ""), AstmParseHelpers.SplitBarcodeToken("^1026004819"));
        Assert.Equal(("1026004819", "", ""), AstmParseHelpers.SplitBarcodeToken("1026004819"));
        Assert.Equal("", AstmParseHelpers.SplitBarcodeToken("").Barcode);
    }
}
