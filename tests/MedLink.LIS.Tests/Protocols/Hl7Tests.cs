using System.Text;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;
using MedLink.LIS.Core.Protocols.Parsers;

namespace MedLink.LIS.Tests.Protocols;

public class Hl7Tests
{
    private static AnalyzerConfigDto Cfg(string typeCode) => AnalyzerProfileCatalog.Default.CreateDefaultConfig(typeCode, "t");

    [Fact]
    public void Hl7Parser_parses_mindray_oru_segments_and_fields()
    {
        var msg = Hl7Parser.Parse(SampleFiles.Load("05_hl7_oru_r01_mindray.hl7"));
        Assert.Equal("ORU^R01", msg.MessageType);
        Assert.Equal("MSG009841", msg.MessageControlId);
        Assert.Equal("2.3.1", msg.Version);
        Assert.Equal("Mindray", msg.SendingApplication);
        Assert.Equal(5, msg.All("OBX").Count());
        var pid = msg.First("PID")!;
        Assert.Equal("108291", pid.Component(3, 1));
        Assert.Equal("Коваленко", pid.Component(5, 1));
        var obr = msg.First("OBR")!;
        Assert.Equal("1026004819", obr.Field(3));
    }

    [Fact]
    public void Mindray_oru_sample_yields_five_results()
    {
        var cfg = Cfg("MINDRAY240");
        var parsed = ParserFactory.ForConfig(cfg).Parse(cfg, SampleFiles.Load("05_hl7_oru_r01_mindray.hl7"));
        Assert.Equal("HL7_MINDRAY", ParserFactory.ForConfig(cfg).Kind);
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal("1026004819", parsed.Barcode);
        Assert.Equal(new[] { "GLU", "ALT", "AST", "CREAT", "UREA" }, parsed.Results.Select(r => r.AnalyzerCode).ToArray());
        Assert.Equal("5.2", parsed.Results[0].Value);
        Assert.Equal("mmol/L", parsed.Results[0].Unit);
        Assert.Equal("4.1-5.9", parsed.Results[0].ReferenceText);
        Assert.Equal("N", parsed.Results[0].Flags);
        Assert.Equal(new DateTime(2026, 10, 6, 15, 52, 0), parsed.Results[0].MeasuredAt);
        Assert.Equal("Коваленко", parsed.Patient!.LastName);
        Assert.Equal("MSG009841", parsed.MessageControlId);
    }

    [Fact]
    public void Ack_sample_is_recognised_as_ack()
    {
        var cfg = Cfg("MINDRAY240");
        var parsed = new Hl7OruParser().Parse(cfg, SampleFiles.Load("06_hl7_ack_response.hl7"));
        Assert.Equal(InboundMessageKind.Ack, parsed.Kind);
    }

    [Fact]
    public void Mllp_wrap_and_unwrap_roundtrip_and_block_extraction()
    {
        var text = "MSH|^~\\&|A|B|C|D|20261006||ORU^R01|1|P|2.3.1\rOBX|1|NM|GLU||5.2|mmol/L\r";
        var bytes = MllpCodec.Wrap(text);
        Assert.Equal(0x0B, bytes[0]);
        Assert.Equal(0x1C, bytes[^2]);
        Assert.Equal(0x0D, bytes[^1]);
        Assert.Equal(text.TrimEnd('\r'), MllpCodec.Unwrap(bytes));
        Assert.Equal(text.TrimEnd('\r'), MllpCodec.Unwrap(MllpCodec.WrapString(text)));

        var two = bytes.Concat(MllpCodec.Wrap("MSH|^~\\&|X\rMSA|AA|1\r")).Concat(new byte[] { 0x0B, (byte)'M', (byte)'S' }).ToArray();
        var (messages, remainder) = MllpCodec.ExtractBlocks(two);
        Assert.Equal(2, messages.Count);
        Assert.StartsWith("MSH|^~\\&|X", Encoding.UTF8.GetString(messages[1]));
        Assert.Equal(3, remainder.Length);
    }

    [Fact]
    public void AckBuilder_produces_MSA_AA_with_original_control_id_and_swapped_endpoints()
    {
        var inbound = Hl7Parser.Parse(SampleFiles.Load("05_hl7_oru_r01_mindray.hl7"));
        var ack = Hl7AckBuilder.BuildAck(inbound, new DateTime(2026, 10, 6, 15, 55, 2), controlId: "ACK009841");
        var parsed = Hl7Parser.Parse(ack);
        Assert.Equal("ACK^R01^ACK", parsed.MessageType);
        Assert.Equal("MedLinkLIS", parsed.SendingApplication);
        Assert.Equal("Mindray", parsed.ReceivingApplication);
        var msa = parsed.First("MSA")!;
        Assert.Equal("AA", msa.Field(1));
        Assert.Equal("MSG009841", msa.Field(2));
        Assert.Equal("ACK009841", parsed.MessageControlId);
        Assert.Equal("20261006155502", parsed.Msh!.Field(7));
    }

    [Fact]
    public void Mindray_qry_q02_is_parsed_as_query_with_position_from_msh10()
    {
        var raw = SampleFiles.Load("14_hl7_mindray_qry_q02.hl7");
        var cfg = Cfg("MINDRAY240");
        var parsed = new Hl7MindrayParser().Parse(cfg, raw);
        Assert.Equal(InboundMessageKind.Query, parsed.Kind);
        Assert.Equal("10260048", parsed.Barcode);
        Assert.Equal("7", parsed.SamplePosition);
    }

    [Fact]
    public void Ichroma_oul_r24_parses_value_and_unit_from_text_observation()
    {
        var raw = SampleFiles.Load("17_hl7_ichroma_oul_r24.hl7");
        var cfg = Cfg("ICHROMA3");
        var parser = ParserFactory.ForConfig(cfg);
        Assert.Equal("HL7_ICHROMA", parser.Kind);
        var parsed = parser.Parse(cfg, raw);
        Assert.Equal(InboundMessageKind.Results, parsed.Kind);
        Assert.Equal("24318785", parsed.Barcode);
        Assert.Equal(2, parsed.Results.Count);
        Assert.Equal("Vitamin D Neo", parsed.Results[0].AnalyzerCode);
        Assert.Equal("14.24", parsed.Results[0].Value);
        Assert.Equal("ng/mL", parsed.Results[0].Unit);
    }

    [Fact]
    public void Mindray_coagulation_PT_code_is_suffixed_with_unit_and_loinc_code_is_replaced_by_name()
    {
        var raw = "MSH|^~\\&|Mindray|C3100|||20261006||ORU^R01|1|P|2.3.1\rPID|1||10260048\rOBR|1||10260048|\r" +
                  "OBX|1|NM|PT^PT^|1|12.5|s|10-14|N\rOBX|2|NM|PT^PT^|1|98|%|70-130|N\rOBX|3|NM|6690-2^WBC^LN|1|7.1|10*9/L|4-9|N\r";
        var parsed = new Hl7MindrayParser().Parse(Cfg("MINDRAY3100"), raw);
        Assert.Equal(new[] { "PT_s", "PT_%", "WBC" }, parsed.Results.Select(r => r.AnalyzerCode).ToArray());
    }

    [Fact]
    public void Hl7_escape_sequences_are_decoded_in_values()
    {
        var d = Hl7Delimiters.Default;
        Assert.Equal("a|b^c~d\\e&f", d.Unescape("a\\F\\b\\S\\c\\R\\d\\E\\e\\T\\f"));
        Assert.Equal("a\\F\\b", d.EscapeValue("a|b"));
        Assert.Equal(new DateTime(2026, 10, 6, 15, 55, 0), Hl7Parser.ParseTimestamp("20261006155500+0300"));
        Assert.Equal(new DateTime(1985, 4, 12), Hl7Parser.ParseTimestamp("19850412"));
        Assert.Null(Hl7Parser.ParseTimestamp(""));
    }
}
