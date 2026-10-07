using System.Text;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;
using MedLink.LIS.Core.Protocols.Parsers;

namespace MedLink.LIS.Tests.Protocols;

public class TextParserTests
{
    private static AnalyzerConfigDto Cfg(string typeCode) => AnalyzerProfileCatalog.Default.CreateDefaultConfig(typeCode, "t");

    [Fact]
    public void Rapid_smp_new_data_gives_results_with_m_and_c_prefixed_codes_and_ack_reply()
    {
        var raw = SampleFiles.Load("11_rapid_smp_new_data.txt");
        var parsed = new RapidParser().Parse(Cfg("RAPID"), raw);
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal("10260048", parsed.Barcode);
        Assert.Equal(new[] { "mpH", "mpCO2", "mpO2", "cHCO3", "mNa" }, parsed.Results.Select(r => r.AnalyzerCode).ToArray());
        Assert.Equal("7.38", parsed.Results[0].Value);
        Assert.Equal(RapidParser.AckFrame(), parsed.ImmediateReply);
    }

    [Fact]
    public void Rapid_ack_frame_has_legacy_crc_0B()
    {
        var ack = RapidParser.AckFrame();
        Assert.Equal("\u0002\u0006\u00030B\u0004", ack);
    }

    [Fact]
    public void Rapid_smp_new_av_produces_smp_req_with_valid_crc()
    {
        var raw = "\u0002SMP_NEW_AV\u001c\u001eaMOD\u001d0500\u001d\u001d\u001d\u001ciIID\u001d64204\u001d\u001d\u001d\u001crSEQ\u001d13223\u001d\u001d\u001d\u001c\u001e\u0003D7\u0004";
        var parsed = new RapidParser().Parse(Cfg("RAPID"), raw);
        Assert.Equal(InboundMessageKind.ProtocolReply, parsed.Kind);
        var reply = parsed.ImmediateReply!;
        Assert.StartsWith("\u0002SMP_REQ\u001c\u001eaMOD\u001d0500", reply);
        Assert.Contains("iIID\u001d64204", reply);
        Assert.Contains("rSEQ\u001d13223", reply);
        Assert.EndsWith("\u0004", reply);
        // CRC = сума байтів від STX до ETX включно
        int etx = reply.IndexOf('\u0003');
        var crc = reply.Substring(etx + 1, 2);
        Assert.Equal(AstmFrameCodec.Checksum(reply[..(etx + 1)], Encoding.Latin1), crc);
    }

    [Fact]
    public void Rapid_pat_demog_req_is_query_with_iid_in_position()
    {
        var raw = "\u0002PAT_DEMOG_REQ\u001c\u001eaMOD\u001d0500\u001d\u001d\u001d\u001ciIID\u001d64204\u001d\u001d\u001d\u001ciPID\u001d10260048\u001d\u001d\u001d\u001c\u001e\u000300\u0004";
        var parsed = new RapidParser().Parse(Cfg("RAPID"), raw);
        Assert.Equal(InboundMessageKind.Query, parsed.Kind);
        Assert.Equal("10260048", parsed.Barcode);
        Assert.Equal("64204^0500", parsed.SamplePosition);
        var ready = new RapidParser().Parse(Cfg("RAPID"), "\u0002SYS_READY\u001c\u001e\u000300\u0004");
        Assert.Equal(InboundMessageKind.ProtocolReply, ready.Kind);
        Assert.Equal(RapidParser.AckFrame(), ready.ImmediateReply);
    }

    [Fact]
    public void Fuji_csv_gives_three_results_with_units()
    {
        var parsed = new FujiParser().Parse(Cfg("FUJI"), SampleFiles.Load("12_fuji_results.txt"));
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal("10260052", parsed.Barcode);
        Assert.Equal(new[] { "TCHO-PS", "Mg-PS", "Ca-PS" }, parsed.Results.Select(r => r.AnalyzerCode).ToArray());
        Assert.Equal("4.53", parsed.Results[0].Value);
        Assert.Equal("mmol/l", parsed.Results[0].Unit);
        Assert.Equal("0.84", parsed.Results[1].Value);
    }

    [Fact]
    public void Integra_text_results_and_query_are_parsed()
    {
        var parsed = new IntegraParser().Parse(Cfg("INTEGRA"), SampleFiles.Load("13_integra_results.txt"));
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal("10260053", parsed.Barcode);
        Assert.Equal(2, parsed.Results.Count);
        Assert.Equal("GLU", parsed.Results[0].AnalyzerCode);
        Assert.Equal("5.21", parsed.Results[0].Value);
        Assert.Equal("ALT", parsed.Results[1].AnalyzerCode);
        Assert.Equal("31", parsed.Results[1].Value);

        var q = new IntegraParser().Parse(Cfg("INTEGRA"), "\u0001\n09 CBINTEGRA 400    10\n\u0002\n42 000000000 10260053      \n\u0003\n\u0004\n");
        Assert.Equal(InboundMessageKind.Query, q.Kind);
        Assert.Equal("10260053", q.Barcode);
    }

    [Fact]
    public void Cyan_tab_report_uses_value_column_and_stops_at_warnings()
    {
        var parsed = new CyanParser().Parse(Cfg("HUMACOUNT5L"), SampleFiles.Load("16_cyan_humacount5l_results.txt"));
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal("10260054", parsed.Barcode);
        Assert.Equal(5, parsed.Results.Count);
        Assert.Equal("7.45", parsed.Results.First(r => r.AnalyzerCode == "WBC").Value);
        var plt = parsed.Results.First(r => r.AnalyzerCode == "PLT");
        Assert.Equal("135", plt.Value);
        Assert.Equal("L", plt.Flags);
        Assert.Equal("10^9/l", plt.Unit);
        Assert.Equal("150-400", plt.ReferenceText);
    }

    [Fact]
    public void Dirui_h100_key_value_lines_are_parsed()
    {
        var parsed = new GenericKeyValueParser().Parse(Cfg("DIRUI_H100"), SampleFiles.Load("18_text_dirui_h100.txt"));
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal("10021415", parsed.Barcode);
        Assert.Equal("Normal 3.4umol/L", parsed.Results.First(r => r.AnalyzerCode == "UBG").Value);
        Assert.Equal("Neg", parsed.Results.First(r => r.AnalyzerCode == "BIL").Value);
        Assert.Equal("<=1.005", parsed.Results.First(r => r.AnalyzerCode == "SG").Value);
        Assert.Equal("7.5", parsed.Results.First(r => r.AnalyzerCode == "pH").Value);
        Assert.Equal(10, parsed.Results.Count);
    }

    [Fact]
    public void ElytePlus_token_pairs_are_parsed_until_End()
    {
        var parsed = new GenericKeyValueParser().Parse(Cfg("ELYTEPLUS"), SampleFiles.Load("19_text_elyteplus.txt"));
        Assert.Equal("10042014", parsed.Barcode);
        Assert.Equal("138.6", parsed.Results.First(r => r.AnalyzerCode == "Na").Value);
        Assert.Equal("4.10", parsed.Results.First(r => r.AnalyzerCode == "K").Value);
        Assert.DoesNotContain(parsed.Results, r => r.AnalyzerCode == "End");
        Assert.Equal(11, parsed.Results.Count);
    }

    [Fact]
    public void Uc1000_fixed_columns_are_parsed()
    {
        var raw = "12845873      ,    ,  ,N00000001,0000, Strip 12S,2024/10/24,10:45:40,\n" +
                  "0 normal      88.5             ,URO\n0  -          97.0             ,BLD (RBC)\n0 +-    50    43.4             ,GLU\n" +
                  "0 5.5         185.4            ,PH\n0  +          114.2            ,NIT\n0 1.025       28.4  142.2      ,S.G\n0 normal      ,                 P/C\n0     YELLOW    03 -            COLOR \n";
        var parsed = new Uc1000Parser().Parse(Cfg("UC1000"), raw);
        Assert.Equal("12845873", parsed.Barcode);
        Assert.Equal("normal", parsed.Results.First(r => r.AnalyzerCode == "URO").Value);
        Assert.Equal("negative", parsed.Results.First(r => r.AnalyzerCode == "BLD (RBC)").Value);
        Assert.Equal("+- 50", parsed.Results.First(r => r.AnalyzerCode == "GLU").Value);
        Assert.Equal("positive", parsed.Results.First(r => r.AnalyzerCode == "NIT").Value);
        Assert.Equal("1.025", parsed.Results.First(r => r.AnalyzerCode == "S.G").Value);
        Assert.Equal("YELLOW", parsed.Results.First(r => r.AnalyzerCode == "COLOR").Value);
    }

    [Fact]
    public void Junior_and_apoti_formats_are_parsed()
    {
        var junior = new JuniorParser().Parse(Cfg("JUNIOR"), "{r;FLEXOR;0;10001516    ;N;test test           ;01.Jan.00  ;M;                    ;13.Jul.24  ;17:13; 1;ALT ;13     ;                       ;   ;U/I   ;}");
        Assert.Equal("10001516", junior.Barcode);
        Assert.Single(junior.Results);
        Assert.Equal("ALT", junior.Results[0].AnalyzerCode);
        Assert.Equal("13", junior.Results[0].Value);
        Assert.Equal("U/I", junior.Results[0].Unit);

        var apoti = new ApotiParser().Parse(Cfg("APOTI"), "|11284932|48|Vitamin D|5.000|100.000|20.000|30.000|45.8|ng/mL|Suf.|20250207205236|0|0|033cff8ff4665d23|1234");
        Assert.Equal("11284932", apoti.Barcode);
        Assert.Equal("Vitamin D", apoti.Results[0].AnalyzerCode);
        Assert.Equal("45.8", apoti.Results[0].Value);
        Assert.Equal("ng/mL", apoti.Results[0].Unit);
    }

    [Fact]
    public void Iris_ping_is_echoed_and_hex_xml_is_decoded()
    {
        var ping = "\u00021" + IrisParser.PingHex + "\u0003XX\r\n";
        var parsed = new IrisParser().Parse(Cfg("IRISIQ200"), ping);
        Assert.Equal(InboundMessageKind.ProtocolReply, parsed.Kind);
        Assert.Equal("1" + IrisParser.PingHex, parsed.ImmediateReply);

        var xml = "<?xml version=\"1.0\"?><Root><SA ID=\"10260055\"><AC><AR Key=\"RBC\">5</AR><AR Key=\"WBC\">2</AR></AC></SA></Root>";
        var hex = string.Concat(xml.Select(c => ((int)c).ToString("X2") + "00"));
        var frame = "\u00021" + hex + "\u0003XX\r\n";
        var res = new IrisParser().Parse(Cfg("IRISIQ200"), frame);
        Assert.Equal(InboundMessageKind.Results, res.Kind);
        Assert.Equal("10260055", res.Barcode);
        Assert.Equal(2, res.Results.Count);
        Assert.Equal("RBC", res.Results[0].AnalyzerCode);
    }

    [Fact]
    public void TextProtocolFramer_splits_packets_by_bop_eop_across_chunks()
    {
        var framer = TextProtocolFramer.FromBase64("Ag==", "Aw=="); // STX / ETX
        var p1 = framer.Push(Encoding.ASCII.GetBytes("junk\u0002R,NORM"));
        Assert.Empty(p1);
        var p2 = framer.Push(Encoding.ASCII.GetBytes("AL,1\u0003tail\u0002second\u0003"));
        Assert.Equal(2, p2.Count);
        Assert.Equal("R,NORMAL,1", Encoding.ASCII.GetString(p2[0]));
        Assert.Equal("second", Encoding.ASCII.GetString(p2[1]));

        var noEop = new TextProtocolFramer(null, null);
        Assert.Empty(noEop.Push(Encoding.ASCII.GetBytes("Sample ID:\t123")));
        Assert.Equal("Sample ID:\t123", Encoding.ASCII.GetString(noEop.FlushByTimeout()!));

        var multi = new TextProtocolFramer(Encoding.ASCII.GetBytes("Sample Report"), Encoding.ASCII.GetBytes("End"));
        var packets = multi.Push(Encoding.ASCII.GetBytes("Sample Report Barcode 1 K 2 End"));
        Assert.Single(packets);
        Assert.Equal(" Barcode 1 K 2 ", Encoding.ASCII.GetString(packets[0]));
    }
}
