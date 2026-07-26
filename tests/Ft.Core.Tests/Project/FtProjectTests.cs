using Ft.Core.Framing;
using Ft.Core.Project;
using Ft.Core.Tests.TestUtil;
using Xunit;

namespace Ft.Core.Tests.Project;

public class FtProjectTests
{
    [Fact]
    public void BuildPipelineConfig_FullDemoStyleProject()
    {
        var project = new FtProject
        {
            Framing = new FramingConfig
            {
                Mode = "LengthField",
                HeaderLen = 2,
                LenOffset = 1,
                LenSize = 1,
                Endian = "LE",
                LenAdjust = 2,
            },
            Checksum = new ChecksumConfig
            {
                Preset = "CRC16_MODBUS",
                OffsetFromEnd = 2,
                ByteOrder = "LE",
                CoverageEndOffsetFromEnd = 2,
            },
            Fields = [new FieldConfig { Name = "temp", Offset = 3, Type = "s16", Endian = "BE" }],
            Highlights = [new HighlightConfig { Pattern = "A5 ?? 01", Color = "#123456" }],
        };

        var config = project.BuildPipelineConfig(new FakeTimeSource());
        Assert.True(config.IsOk);
        Assert.IsType<LengthFieldFramer>(config.Value.Framer);
        Assert.NotNull(config.Value.ChecksumSpec);
        Assert.Single(config.Value.Fields);
        Assert.Single(config.Value.Highlights);
    }

    [Fact]
    public void BuildFramer_Delimiter_WithStartEndEscape()
    {
        var framing = new FramingConfig
        {
            Mode = "Delimiter",
            StartHex = "02",
            EndHex = "03",
            EscapeHex = "1B",
        };
        var framer = framing.BuildFramer(new FakeTimeSource());
        Assert.True(framer.IsOk);
        Assert.IsType<DelimiterFramer>(framer.Value);
    }

    [Fact]
    public void BuildFramer_ModeNone_ReturnsNull()
    {
        var framer = new FramingConfig { Mode = "None" }.BuildFramer(new FakeTimeSource());
        Assert.True(framer.IsOk);
        Assert.Null(framer.Value);
    }

    [Theory]
    [InlineData("Delimiter", "", "ZZ", "")]      // invalid end hex
    [InlineData("Delimiter", "", "", "")]        // missing end
    [InlineData("Delimiter", "", "03", "1B 1B")] // escape must be one byte
    [InlineData("Warp", "", "", "")]             // unknown mode
    public void BuildFramer_InvalidConfigs_Fail(string mode, string start, string end, string escape)
    {
        var framing = new FramingConfig { Mode = mode, StartHex = start, EndHex = end, EscapeHex = escape };
        Assert.False(framing.BuildFramer(new FakeTimeSource()).IsOk);
    }

    [Fact]
    public void BuildPipelineConfig_UnknownPreset_Fails()
    {
        var project = new FtProject { Checksum = new ChecksumConfig { Preset = "CRC99" } };
        Assert.False(project.BuildPipelineConfig(new FakeTimeSource()).IsOk);
    }

    [Fact]
    public void FieldConfig_UnknownType_Fails() =>
        Assert.False(new FieldConfig { Name = "x", Type = "u64" }.Build().IsOk);

    [Fact]
    public void HighlightConfig_FieldCondition_Builds()
    {
        var built = new HighlightConfig { Field = "temp", Op = ">", Value = 50, Color = "#FF0000" }.Build();
        Assert.True(built.IsOk);
        Assert.NotNull(built.Value.Condition);
    }

    [Fact]
    public void HighlightConfig_Empty_Fails() =>
        Assert.False(new HighlightConfig().Build().IsOk);

    [Fact]
    public void SilenceGap_BuildsWithInjectedClock()
    {
        var framer = new FramingConfig { Mode = "SilenceGap", GapMs = 10 }.BuildFramer(new FakeTimeSource());
        Assert.True(framer.IsOk);
        Assert.IsType<SilenceGapFramer>(framer.Value);
    }
}
