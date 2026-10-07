using System.Text;
using MedLink.LIS.Core.Protocols;

namespace MedLink.LIS.Tests.Protocols;

public class AstmFrameCodecTests
{
    [Fact]
    public void Checksum_matches_delphi_CalcCRC_for_simple_frame()
    {
        // "3L|1|N<CR><ETX>" → сума байтів mod 256 = 0x06
        var body = "3L|1|N\r\u0003";
        Assert.Equal("06", AstmFrameCodec.Checksum(body));
    }

    [Theory]
    [InlineData("01_astm_query_sysmex.txt")]
    [InlineData("03_astm_results_cobas.txt")]
    [InlineData("04_astm_results_sysmex_xn.txt")]
    [InlineData("02_astm_order_response.txt")]
    public void Sample_frames_have_valid_checksums(string file)
    {
        var raw = SampleFiles.Load(file);
        var frames = SplitFrames(raw);
        Assert.NotEmpty(frames);
        foreach (var f in frames)
        {
            var parsed = AstmFrameCodec.ParseFrame(Encoding.UTF8.GetBytes(f), requireChecksum: true, Encoding.UTF8);
            Assert.True(parsed.ChecksumOk, $"{file}: {parsed.Error} у кадрі {ProtocolText.Escape(f)}");
        }
    }

    [Fact]
    public void ParseFrame_detects_bad_checksum()
    {
        var frame = "\u00021H|\\^&|||X\r\u000300\r\n";
        var parsed = AstmFrameCodec.ParseFrame(frame);
        Assert.False(parsed.ChecksumOk);
        Assert.Contains("контрольна сума", parsed.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFrame_roundtrips_through_ParseFrame()
    {
        var frame = AstmFrameCodec.BuildFrame(2, "P|1||108291||Kovalenko^Olena", isLast: true);
        Assert.StartsWith("\u00022P|1||108291", frame);
        Assert.EndsWith("\r\n", frame);
        var parsed = AstmFrameCodec.ParseFrame(frame);
        Assert.True(parsed.ChecksumOk);
        Assert.Equal(2, parsed.Number);
        Assert.True(parsed.IsLast);
        Assert.Equal("P|1||108291||Kovalenko^Olena", parsed.Content);
    }

    [Fact]
    public void BuildFrames_numbers_cycle_1_to_7_then_0()
    {
        var records = Enumerable.Range(1, 9).Select(i => $"R|{i}|^^^T{i}|1.0").ToList();
        var frames = AstmFrameCodec.BuildFrames(records);
        Assert.Equal(9, frames.Count);
        var numbers = frames.Select(f => f[1] - '0').ToArray();
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 0, 1 }, numbers);
    }

    [Fact]
    public void BuildFrames_splits_long_record_with_ETB_at_max_frame_len()
    {
        var longRecord = "O|1|12345678||" + string.Join("\\", Enumerable.Range(1, 80).Select(i => $"^^^T{i:000}"));
        var frames = AstmFrameCodec.BuildFrames(new[] { longRecord }, maxFrameLen: 240);
        Assert.True(frames.Count >= 2);
        Assert.Contains(AstmControl.EtbChar, frames[0]);
        Assert.DoesNotContain(AstmControl.EtxChar, frames[0]);
        Assert.Contains(AstmControl.EtxChar, frames[^1]);
        foreach (var f in frames) Assert.True(f.Length <= 240 + 7, $"кадр задовгий: {f.Length}");
        // збирання назад дає початковий запис
        var parsed = frames.Select(f => AstmFrameCodec.ParseFrame(f)).ToList();
        Assert.All(parsed, p => Assert.True(p.ChecksumOk));
        Assert.Equal(longRecord + "\r", AstmFrameCodec.AssembleMessage(parsed));
    }

    [Fact]
    public void BuildFrames_honours_explicit_ETB_marker_inside_record()
    {
        var record = "O|1|123|S1^SC|^^^A^\\^^^B^" + AstmControl.EtbChar + "\\^^^C^|R||||||A||||1||||||||||O";
        var frames = AstmFrameCodec.BuildFrames(new[] { record, "L|1|N" }, maxFrameLen: 240);
        Assert.Equal(3, frames.Count);
        Assert.EndsWith("\\^^^B^" + AstmControl.EtbChar + AstmFrameCodec.Checksum("1O|1|123|S1^SC|^^^A^\\^^^B^" + AstmControl.EtbChar) + "\r\n", frames[0]);
        Assert.StartsWith("\u00022\\^^^C^|R", frames[1]);
        Assert.Contains("\r\u0003", frames[1]);
    }

    [Fact]
    public void StripFraming_removes_control_sequences_and_keeps_records()
    {
        var raw = SampleFiles.Load("03_astm_results_cobas.txt");
        var text = AstmFrameCodec.StripFraming(raw);
        Assert.DoesNotContain('\u0002', text);
        Assert.DoesNotContain('\u0003', text);
        Assert.StartsWith("H|\\^&|||Roche^Cobas-e411^01", text);
        var lines = text.Split('\r', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(6, lines.Length);
        Assert.StartsWith("R|2|^^^FT4^|16.2", lines[4]);
    }

    [Fact]
    public void AssembleMessage_concatenates_ETB_frames_without_separator()
    {
        var frames = new[]
        {
            new AstmFrame { Number = 1, Content = "O|1|123||^^^A", IsLast = false, ChecksumOk = true },
            new AstmFrame { Number = 2, Content = "\\^^^B|R", IsLast = true, ChecksumOk = true },
            new AstmFrame { Number = 3, Content = "L|1|N", IsLast = true, ChecksumOk = true },
        };
        Assert.Equal("O|1|123||^^^A\\^^^B|R\rL|1|N\r", AstmFrameCodec.AssembleMessage(frames));
    }

    [Fact]
    public void ProtocolText_escape_and_unescape_roundtrip()
    {
        var raw = "\u0005\u00021H|\\^&\r\u00034D\r\n\u0004\u001c\u001d";
        var escaped = ProtocolText.Escape(raw);
        Assert.Equal("<ENQ><STX>1H|\\^&<CR><ETX>4D<CR><LF><EOT><FS><GS>", escaped);
        Assert.Equal(raw, ProtocolText.Unescape(escaped));
    }

    private static List<string> SplitFrames(string raw)
    {
        var frames = new List<string>();
        int i = 0;
        while ((i = raw.IndexOf('\u0002', i)) >= 0)
        {
            int lf = raw.IndexOf('\n', i);
            if (lf < 0) break;
            frames.Add(raw.Substring(i, lf - i + 1));
            i = lf + 1;
        }
        return frames;
    }
}
