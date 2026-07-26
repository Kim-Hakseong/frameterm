using Ft.Core.Project;
using Xunit;

namespace Ft.Core.Tests.Project;

/// <summary>M8 DoD: .ftproj round-trip.</summary>
public class FtProjectSerializerTests
{
    private static FtProject SampleProject() => new()
    {
        Transport = new TransportConfig { Type = "Serial", Port = "COM3", Baud = 921600, Parity = "Even" },
        Framing = new FramingConfig
        {
            Mode = "LengthField",
            HeaderLen = 2,
            LenOffset = 1,
            LenSize = 2,
            Endian = "BE",
            LenAdjust = 5,
            MaxFrame = 2048,
        },
        Checksum = new ChecksumConfig
        {
            Preset = "CRC16_MODBUS",
            OffsetFromEnd = 2,
            ByteOrder = "LE",
            CoverageStart = 1,
            CoverageEndOffsetFromEnd = 2,
        },
        Fields = [new FieldConfig { Name = "temp", Offset = 4, Type = "s16", Endian = "BE" }],
        Highlights =
        [
            new HighlightConfig { Pattern = "A5 ?? 01", Color = "#7A9E4F" },
            new HighlightConfig { Field = "temp", Op = ">", Value = 500, Color = "#9C2030" },
        ],
        Macros = [new MacroConfig { Name = "Poll", Text = "A5 01 {len} \"CMD\" {crc16}", Hotkey = "F5" }],
    };

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var original = SampleProject();
        var restored = FtProjectSerializer.FromJson(FtProjectSerializer.ToJson(original));

        Assert.True(restored.IsOk);
        var project = restored.Value;
        Assert.Equal(original.Transport.Port, project.Transport.Port);
        Assert.Equal(original.Transport.Baud, project.Transport.Baud);
        Assert.Equal(original.Framing.Mode, project.Framing.Mode);
        Assert.Equal(original.Framing.LenSize, project.Framing.LenSize);
        Assert.Equal(original.Framing.LenAdjust, project.Framing.LenAdjust);
        Assert.Equal(original.Checksum.Preset, project.Checksum.Preset);
        Assert.Equal(original.Checksum.CoverageStart, project.Checksum.CoverageStart);
        Assert.Single(project.Fields);
        Assert.Equal("temp", project.Fields[0].Name);
        Assert.Equal(2, project.Highlights.Count);
        Assert.Equal("A5 ?? 01", project.Highlights[0].Pattern);
        Assert.Single(project.Macros);
        Assert.Equal("F5", project.Macros[0].Hotkey);
    }

    [Fact]
    public async Task SaveLoad_File_RoundTrips()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"ft-proj-{Guid.NewGuid():N}.ftproj");
        try
        {
            var saved = await FtProjectSerializer.SaveAsync(SampleProject(), path);
            Assert.True(saved.IsOk);
            var loaded = await FtProjectSerializer.LoadAsync(path);
            Assert.True(loaded.IsOk);
            Assert.Equal("COM3", loaded.Value.Transport.Port);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvalidJson_Fails() =>
        Assert.False(FtProjectSerializer.FromJson("{ not json").IsOk);

    [Fact]
    public void NewerSchema_Rejected() =>
        Assert.False(FtProjectSerializer.FromJson("{\"schemaVersion\": 99}").IsOk);

    [Fact]
    public async Task MissingFile_FailsGracefully() =>
        Assert.False((await FtProjectSerializer.LoadAsync("/nonexistent/x.ftproj")).IsOk);

    [Fact]
    public void RestoredProject_BuildsValidPipeline()
    {
        var restored = FtProjectSerializer.FromJson(FtProjectSerializer.ToJson(SampleProject()));
        var config = restored.Value.BuildPipelineConfig(new Tests.TestUtil.FakeTimeSource());
        Assert.True(config.IsOk);
        Assert.NotNull(config.Value.Framer);
        Assert.Single(config.Value.Fields);
        Assert.Equal(2, config.Value.Highlights.Count);
    }
}
