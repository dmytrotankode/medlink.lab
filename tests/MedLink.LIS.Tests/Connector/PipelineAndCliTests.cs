using MedLink.LabConnector.Cli;
using MedLink.LabConnector.Configuration;
using MedLink.LabConnector.Pipeline;
using MedLink.LabConnector.Transport;
using MedLink.LIS.Core.Contracts;
using MedLink.LIS.Core.Protocols;

namespace MedLink.LIS.Tests.Connector;

public class PipelineAndCliTests
{
    [Fact]
    public void BuildBatch_applies_factor_offset_and_unit_override_from_parameter_map()
    {
        var cfg = new AnalyzerConfigDto
        {
            AnalyzerId = "a1",
            ParameterMap = new()
            {
                new AnalyzerParameterMapDto { AnalyzerCode = "GLU", TestCode = "GLU", Factor = 0.0555, Offset = 0 , UnitOverride = "mmol/L" },
                new AnalyzerParameterMapDto { AnalyzerCode = "TEMP", TestCode = "TEMP", Factor = 1, Offset = -273.15 },
            },
        };
        var parsed = new AnalyzerInboundMessage { Kind = InboundMessageKind.Results, IsQc = true };
        parsed.AddResult("10260048", "GLU", "100", "mg/dL", "N");
        parsed.AddResult("10260048", "TEMP", "310,15", "K");
        parsed.AddResult("10260048", "WBC", "7,45", "10*9/L");
        parsed.AddResult("10260048", "COLOR", "yellow");
        var batch = ResultPipeline.BuildBatch(cfg, parsed, DateTime.UtcNow);
        Assert.Equal("a1", batch.AnalyzerId);
        Assert.True(batch.IsQc);
        Assert.Equal("5.55", batch.Results[0].Value);
        Assert.Equal("mmol/L", batch.Results[0].Unit);
        Assert.Equal("37", batch.Results[1].Value);
        Assert.Equal("7.45", batch.Results[2].Value); // кома → крапка без мапінгу
        Assert.Equal("yellow", batch.Results[3].Value);
    }

    [Fact]
    public void CommandLine_parses_commands_and_options()
    {
        var cli = CommandLine.Parse(new[] { "setup", "--server", "https://lis.example.ua", "--install-key=ABC123", "--name", "Лаб ПК 1", "--insecure" });
        Assert.Equal("setup", cli.Command);
        Assert.Equal("https://lis.example.ua", cli.Get("server"));
        Assert.Equal("ABC123", cli.Get("install-key"));
        Assert.Equal("Лаб ПК 1", cli.Get("name"));
        Assert.True(cli.Has("insecure"));
        Assert.Equal("run", CommandLine.Parse(Array.Empty<string>()).Command);
        Assert.Equal("run", CommandLine.Parse(new[] { "--urls", "http://localhost:6000" }).Command);
        Assert.Equal(new[] { "--urls", "http://localhost:6000" }, CommandLine.Parse(new[] { "--urls", "http://localhost:6000" }).Passthrough);
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(new[] { "test" }).Require("file"));
    }

    [Fact]
    public void Server_base_url_is_normalised_to_api_v1_lab()
    {
        Assert.Equal("https://lis.example.ua/api/v1/lab/", ServerOptions.NormalizeBaseUrl("https://lis.example.ua"));
        Assert.Equal("https://lis.example.ua/api/v1/lab/", ServerOptions.NormalizeBaseUrl("lis.example.ua/"));
        Assert.Equal("http://10.0.0.5:5000/api/v1/lab/", ServerOptions.NormalizeBaseUrl("http://10.0.0.5:5000/api/v1/lab"));
    }

    [Fact]
    public void Serial_settings_map_from_lis_dictionary_values()
    {
        Assert.Equal(System.IO.Ports.Parity.Even, SerialTransport.MapParity("Even"));
        Assert.Equal(System.IO.Ports.Parity.Space, SerialTransport.MapParity("pSpace"));
        Assert.Equal(System.IO.Ports.StopBits.Two, SerialTransport.MapStopBits("Two"));
        Assert.Equal(System.IO.Ports.StopBits.OnePointFive, SerialTransport.MapStopBits("OneAndHalf"));
        Assert.Equal(System.IO.Ports.Handshake.RequestToSend, SerialTransport.MapFlowControl("Hardware"));
        Assert.Equal(System.IO.Ports.Handshake.XOnXOff, SerialTransport.MapFlowControl("XonXoff"));
        Assert.Equal(OperatingSystem.IsWindows() ? "COM3" : "/dev/ttyS2", SerialTransport.NormalizePortName("3"));
        Assert.Equal("/dev/ttyUSB0", SerialTransport.NormalizePortName("/dev/ttyUSB0"));
    }

    [Fact]
    public void Config_merge_applies_profile_defaults()
    {
        var a = new AnalyzerConfigDto { AnalyzerId = "x", Code = "COBAS_E411", TypeCode = "COBAS411", Protocol = "", OrderTemplate = "ASTM_GENERIC" };
        ConfigStore.ApplyProfileDefaults(a);
        Assert.Equal("ASTM", a.Protocol);
        Assert.Equal("COBAS_E411", a.OrderTemplate);
        Assert.Equal(240, a.Framing.MaxFrameLen);
        var r = new AnalyzerConfigDto { AnalyzerId = "y", Code = "RAPID1", TypeCode = "RAPID", Protocol = "" };
        ConfigStore.ApplyProfileDefaults(r);
        Assert.Equal("RAPID", r.Protocol);
        Assert.Equal("Ag==", r.Framing.BopBase64);
        Assert.Equal("BA==", r.Framing.EopBase64);
    }
}
