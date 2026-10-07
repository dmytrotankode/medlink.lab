using System.Text;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;
using MedLink.LIS.Core.Protocols.Orders;
using MedLink.LIS.Core.Protocols.Parsers;

namespace MedLink.LIS.Tests.Protocols;

public class OrderBuilderTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 15, 30, 0);

    private static AnalyzerConfigDto Cfg(string typeCode) => AnalyzerProfileCatalog.Default.CreateDefaultConfig(typeCode, "t");

    private static AnalyzerOrderDto Order(string barcode = "10260048", params string[] codes) => new()
    {
        Barcode = barcode,
        OrderNumber = "2610-000048",
        Priority = "R",
        SampleType = "Serum",
        Patient = new AnalyzerOrderPatientDto { Id = "108291", LastName = "Коваленко", FirstName = "Олена", BirthDate = new DateTime(1985, 4, 12), Gender = "F" },
        Tests = (codes.Length == 0 ? new[] { "TSH", "FT4" } : codes).Select(c => new AnalyzerOrderTestDto { TestCode = c, AnalyzerCode = c }).ToList(),
    };

    [Fact]
    public void Mindray_hl7_builder_produces_qck_and_dsr_with_qrd_and_dsp21_barcode()
    {
        var b = OrderBuilderFactory.Create("MINDRAY_HL7");
        Assert.Equal(OrderOutputKind.Hl7Messages, b.Output);
        var msgs = b.BuildRecords(Order("10260048", "GLU", "ALT"), Cfg("MINDRAY240"), Now, new OrderBuildContext { SamplePosition = "7", SequenceNumber = 3 });
        Assert.Equal(2, msgs.Count);
        var qck = Hl7Parser.Parse(msgs[0]);
        Assert.Equal("QCK^Q02", qck.MessageType);
        Assert.Equal("7", qck.MessageControlId);
        var dsr = Hl7Parser.Parse(msgs[1]);
        Assert.Equal("DSR^Q03", dsr.MessageType);
        Assert.Equal("10260048", dsr.First("QRD")!.Field(8));
        Assert.Contains("QRD|", msgs[1]);
        Assert.Contains("DSP|21||10260048|||", msgs[1]);
        Assert.Contains("DSP|22||3|||", msgs[1]);
        Assert.Contains("DSP|26|||serum||", msgs[1]);
        Assert.Contains("DSP|29||^GLU^^|||", msgs[1]);
        Assert.Contains("DSP|30||^ALT^^|||", msgs[1]);
        Assert.Contains("DSP|3||Olena^Kovalenko|||", msgs[1]);
        Assert.EndsWith("DSC||\r", msgs[1]);
    }

    [Fact]
    public void Cobas_e411_builder_produces_tsdwn_reply_with_position_from_query_token()
    {
        var b = OrderBuilderFactory.Create("COBAS_E411");
        var recs = b.BuildRecords(Order(), Cfg("COBAS411"), Now, new OrderBuildContext { RawQueryToken = "1^10260048^S1^SC" });
        Assert.Equal(4, recs.Count);
        Assert.Equal("H|\\^&|||host^1|||||cobas-e411|TSDWN^REPLY|P|1", recs[0]);
        Assert.Equal("P|1", recs[1]);
        Assert.Equal("O|1|10260048|S1^SC|^^^TSH^\\^^^FT4^|R||||||A||||1||||||||||O", recs[2]);
        Assert.Equal("L|1|N", recs[3]);
    }

    [Fact]
    public void Cobas_e411_long_order_list_is_split_at_165_with_ETB_frame()
    {
        var codes = Enumerable.Range(1, 40).Select(i => $"T{i:00}").ToArray();
        var b = OrderBuilderFactory.Create("COBAS_E411");
        var recs = b.BuildRecords(Order("10260048", codes), Cfg("COBAS411"), Now, new OrderBuildContext { SamplePosition = "S1^SC" });
        Assert.Equal("H|\\^&|||||||||TSDWN^REPLY|P|1", recs[0]); // довгий варіант — без назви приладу (як у legacy)
        var o = recs[2];
        int etb = o.IndexOf(AstmControl.EtbChar);
        Assert.True(etb > 0);
        var orderList = string.Join("\\", codes.Select(c => $"^^^{c}^"));
        Assert.Equal("O|1|10260048|S1^SC|" + orderList[..165], o[..etb]);
        Assert.Equal(orderList[165..] + "|R||||||A||||1||||||||||O", o[(etb + 1)..]);

        var frames = AstmFrameCodec.BuildFrames(recs, 240, true);
        Assert.Equal(5, frames.Count);
        Assert.Contains(AstmControl.EtbChar, frames[2]);
        Assert.StartsWith("\u00024" + orderList[165..], frames[3]);
        Assert.All(frames, f => Assert.True(AstmFrameCodec.ParseFrame(f).ChecksumOk));
        Assert.Equal(AstmParser.Join(recs).Replace(AstmControl.EtbChar.ToString(), ""), AstmFrameCodec.AssembleMessage(frames.Select(f => AstmFrameCodec.ParseFrame(f))));
    }

    [Fact]
    public void Cobas_c311_splits_at_170_and_appends_SC_container()
    {
        var codes = Enumerable.Range(1, 40).Select(i => $"T{i:00}").ToArray();
        var recs = OrderBuilderFactory.Create("COBAS_C311").BuildRecords(Order("10260048", codes), Cfg("COBAS311"), Now, new OrderBuildContext { RawQueryToken = "1^10260048^S0" });
        Assert.StartsWith("O|1|10260048|S1^SC|", recs[2]);
        int etb = recs[2].IndexOf(AstmControl.EtbChar);
        Assert.Equal("O|1|10260048|S1^SC|".Length + 170, etb);
    }

    [Fact]
    public void Cobas_c111_builder_matches_legacy_format()
    {
        var recs = OrderBuilderFactory.Create("COBAS_C111").BuildRecords(Order("10260048", "GLU"), Cfg("COBAS111"), Now);
        Assert.Equal("H|\\^&|||HIS01|||||C111||P|1|20261006153300", recs[0]); // legacy: now()+3 хв
        Assert.Equal("P|1||||108291||19850412|F||||||||||||||||||||||||||", recs[1]);
        Assert.Equal("O|1|10260048||^^^GLU|R||||||A||||||||||||||O|||||", recs[2]);
        Assert.Equal("L|1|N", recs[3]);
    }

    [Fact]
    public void Sysmex_xn_builder_echoes_query_token_and_transliterates_patient()
    {
        var recs = OrderBuilderFactory.Create("SYSMEX_XN").BuildRecords(Order("1026004819", "CBC", "DIFF"), Cfg("SYSMEXXN"), Now, new OrderBuildContext { RawQueryToken = "^^   1^1026004819^B" });
        Assert.Equal(4, recs.Count);
        Assert.StartsWith("H|\\^&|||MedLinkLIS^^|||||||P|E1394-97|20261006153000", recs[0]);
        Assert.Equal("P|1||108291||Kovalenko^Olena||19850412|F", recs[1]);
        Assert.Equal("O|1|^^   1^1026004819^B||^^^^CBC\\^^^^DIFF|R|20261006153000|||||N||||||||||||||Q", recs[2]);
    }

    [Fact]
    public void Sysmex_cs2500_and_ca600_builders()
    {
        var cs = OrderBuilderFactory.Create("SYSMEX_CS2500").BuildRecords(Order("10260048", "PT", "APTT"), Cfg("SYSMEX2500"), Now, new OrderBuildContext { SamplePosition = "S1^SC" });
        Assert.Contains("CS-2500|TSDWN^REPLY|P|1", cs[0]);
        Assert.Equal("O|1|10260048|S1^SC|^^^PT^\\^^^APTT^|R||||||A||||1||||||||||O", cs[2]);
        var ca = OrderBuilderFactory.Create("SYSMEX_CA600").BuildRecords(Order("10260048", "040"), Cfg("SYSMEXCA600"), Now, new OrderBuildContext { RawQueryToken = "^^1^10260048^B" });
        Assert.Equal("H|\\^&|||host^1|||||CA-600", ca[0]);
        Assert.StartsWith("O|1|^^1^10260048^B||^^^040^040     |R|20261006153000|||||N", ca[2]);
    }

    [Fact]
    public void Rapid_pat_demog_data_frame_has_valid_crc_and_demographics()
    {
        var recs = OrderBuilderFactory.Create("RAPID").BuildRecords(Order(), Cfg("RAPID"), Now, new OrderBuildContext { SamplePosition = "64204^0500" });
        Assert.Single(recs);
        var f = recs[0];
        Assert.StartsWith("\u0002PAT_DEMOG_DATA\u001c\u001eaMOD\u001d0500\u001d\u001d\u001d\u001ciIID\u001d64204", f);
        Assert.Contains("iPID\u001d10260048", f);
        Assert.Contains("iFNAME\u001dOlena", f);
        Assert.Contains("iLNAME\u001dKovalenko", f);
        Assert.Contains("iSEX\u001dF", f);
        Assert.Contains("iDOB\u001d12Apr1985", f);
        int etx = f.IndexOf('\u0003');
        Assert.Equal(AstmFrameCodec.Checksum(f[..(etx + 1)], Encoding.Latin1), f.Substring(etx + 1, 2));
        Assert.EndsWith("\u0004", f);
    }

    [Fact]
    public void Pentra_tosoh_stago_maglumi_bioksel_builders_match_legacy_layouts()
    {
        var o = Order("10260048", "GLU", "ALT");
        var pentra = OrderBuilderFactory.Create("PENTRA").BuildRecords(o, Cfg("PENTRA"), Now);
        Assert.Equal("H|\\^&|||ABX|||||||P|E1394-97|20261006153300", pentra[0]);
        Assert.Equal("P|1||108291||Kovalenko^Olena||19850412|F|||||Prescriptor||||||||||||Location", pentra[1]);
        Assert.Equal("O|1|10260048||^^^GLU\\^^^ALT|R|20261006153300|||||N||||1", pentra[2]);

        var tosoh = OrderBuilderFactory.Create("TOSOH").BuildRecords(o, Cfg("TOSO"), Now);
        Assert.Equal("P|1|||||||", tosoh[1]);
        Assert.Equal("O|1|10260048||^^^GLU\\^^^ALT|||||||||||Sp.1", tosoh[2]);
        Assert.Equal("L|1", tosoh[3]);

        var stago = OrderBuilderFactory.Create("STAGO").BuildRecords(o, Cfg("STAGO START"), Now);
        Assert.Equal("H|\\^&|||SCE^99^3.00|||||||P|LIS2-A2|20261006153300", stago[0]);
        Assert.Equal("O|1|10260048||^^^GLU\\^^^ALT|R", stago[2]);

        var maglumi = OrderBuilderFactory.Create("MAGLUMI").BuildRecords(o, Cfg("MAGLUMI"), Now);
        Assert.Equal("H|\\^&||PSWD|Maglumi 1000|||||Lis||P|E1394-97|20261006", maglumi[0]);
        Assert.Equal("O|1|10260048||^^^GLU|R", maglumi[2]);
        Assert.Equal("O|2|10260048||^^^ALT|R", maglumi[3]);

        var bioksel = OrderBuilderFactory.Create("BIOKSEL").BuildRecords(Order("10260048", "1-0001", "2-0003"), Cfg("BIOKSEL"), Now);
        Assert.Equal("O|1|10260048||0001|R|20261006153300||||||||||Bio-Ksel|||||||||O|||||", bioksel[2]);
        Assert.Equal("O|2|10260048||0003|R|20261006153300||||||||||Bio-Ksel|||||||||O|||||", bioksel[3]);
    }

    [Fact]
    public void HumaStar_batch_integra_and_acltop_builders()
    {
        var huma = OrderBuilderFactory.Create("HUMASTAR");
        Assert.Equal("\n", huma.RecordTerminator);
        var batch = huma.BuildBatch(new[] { Order("10260048", "GLU"), Order("10260049", "ALT") }, Cfg("HUMASTAR"), Now, OrderBuildContext.Empty);
        var lines = batch.Single();
        Assert.Equal("H|\\^&|||HS100^V1.0|||||Host||P|1|20261006153300", lines[0]);
        Assert.Equal("P|1||10260048|Department1|Kovalenko|Olena|19850412|FEMALE|||||||||||||||||||||||||", lines[1]);
        Assert.Equal("C|1|||", lines[2]);
        Assert.Equal("O|1|||GLU|False||||||||||Serum|||||||||||||||", lines[3]);
        Assert.Equal("P|2||10260049|Department1|Kovalenko|Olena|19850412|FEMALE|||||||||||||||||||||||||", lines[4]);
        Assert.Equal("L||N", lines[^1]);

        var integra = OrderBuilderFactory.Create("INTEGRA").BuildRecords(Order("10260048", "GLU", "ALT"), Cfg("INTEGRA"), Now);
        Assert.Equal("\u0001", integra[0]);
        Assert.Equal("09 CBINTEGRA 400    10", integra[1]);
        Assert.Equal("53 10260048        06/10/2026 SER", integra[3]);
        Assert.Equal("54 000 00 A", integra[4]);
        Assert.Equal("55 GLU", integra[5]);
        Assert.Equal("\u0004", integra[^1]);

        var acl = OrderBuilderFactory.Create("ACLTOP").BuildBatch(new[] { Order("10260048", "PT", "APTT"), Order("10260049", "FIB") }, Cfg("ACLTOP300"), Now, OrderBuildContext.Empty).Single();
        Assert.Equal("H|@^\\|||1|||||1||P|1394-97|20261006153300", acl[0]);
        Assert.Equal("O|1|10260048||^^^PT@^^^APTT|R|20261006153300|||||A||||P||||||||||Q", acl[2]);
        Assert.StartsWith("P|2||108291", acl[3]);
        Assert.Equal("L|1|F", acl[^1]);
    }

    [Fact]
    public void Generic_and_beckman_and_mindray_astm_builders()
    {
        var o = Order("10260048", "GLU");
        var g = OrderBuilderFactory.Create("ASTM_GENERIC").BuildRecords(o, Cfg("SIEMENS"), Now);
        Assert.Equal("O|1|10260048||^^^GLU|R|20261006153000|||||A||||Serum||||||||||O", g[2]);

        var beck = OrderBuilderFactory.Create("BECKMAN_ACCESS").BuildRecords(o, Cfg("BECKMAN"), Now);
        Assert.Equal("H|\\^&|||LIS|||||ACCESS^573061||P|1|20261006153000", beck[0]);
        Assert.Equal("P|1|Olena^Kovalenko||19850412|F||", beck[1]);
        Assert.Equal("O|1|10260048||^^^GLU|R||||||A||||Serum", beck[2]);
        Assert.Equal("L|1|F", beck[3]);

        var mind = OrderBuilderFactory.Create("MINDRAY_ASTM").BuildRecords(Order("10260048", "GLU", "ALT"), Cfg("MINDRAY240"), Now);
        Assert.Equal("O|1|1^1^1|10260048|1^GLU^2^1\\2^ALT^2^1|R|20261006153300|20261006153300||||||||Serum||||||||||Q|||||", mind[2]);

        var prestige = OrderBuilderFactory.Create("PRESTIGE").BuildRecords(Order("10260048", "GOT", "GPT"), Cfg("PRESTIGE"), Now);
        Assert.Equal("O|1|^10260048||^^^1^GOT^0\\^^^2^GPT^0|R||||||||||Serum||||||||||O", prestige[2]);
    }

    [Fact]
    public void No_order_reply_is_empty_by_default_and_Y_when_enabled()
    {
        var b = OrderBuilderFactory.Create("COBAS_E411");
        Assert.Empty(b.BuildNoOrderRecords("10260048", Cfg("COBAS411"), Now, OrderBuildContext.Empty));
        var recs = b.BuildNoOrderRecords("10260048", Cfg("COBAS411"), Now, new OrderBuildContext { SendNoOrderReply = true, SamplePosition = "S1^SC" });
        Assert.Equal("O|1|10260048|S1^SC||R||||||A||||1||||||||||Y", recs[2]);
        Assert.Empty(OrderBuilderFactory.Create("NONE").BuildRecords(Order(), Cfg("URISYS"), Now));
    }

    [Fact]
    public void Factory_resolves_builder_from_profile_and_parser_from_profile()
    {
        var cfg = Cfg("COBAS411");
        cfg.OrderTemplate = "ASTM_GENERIC"; // сервер не задав — беремо з профілю
        Assert.Equal("COBAS_E411", OrderBuilderFactory.ForConfig(cfg).Template);
        cfg.OrderTemplate = "MINDRAY_HL7"; // явне перевизначення сервером
        Assert.Equal("MINDRAY_HL7", OrderBuilderFactory.ForConfig(cfg).Template);
        Assert.Equal("ASTM_COBAS", ParserFactory.ForConfig(Cfg("COBAS411")).Kind);
        Assert.Equal("HL7_ORU", ParserFactory.ForConfig(new AnalyzerConfigDto { TypeCode = "UNKNOWN", Protocol = "HL7" }).Kind);
        Assert.Equal("TEXT_RAPID", ParserFactory.ForConfig(new AnalyzerConfigDto { TypeCode = "X", Protocol = "RAPID" }).Kind);
    }
}
