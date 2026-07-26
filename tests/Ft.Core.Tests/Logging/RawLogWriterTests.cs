using Ft.Core.Logging;
using Ft.Core.Pipeline;
using Ft.Core.Tests.TestUtil;
using Xunit;

namespace Ft.Core.Tests.Logging;

public class RawLogWriterTests
{
    [Fact]
    public async Task WritesTimestampedHexLines_InOrder()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"ft-log-{Guid.NewGuid():N}.log");
        try
        {
            var ts = new DateTimeOffset(2026, 7, 26, 12, 0, 0, 500, TimeSpan.Zero);
            await using (var writer = new RawLogWriter(path))
            {
                writer.WriteChunk(Hex.Bytes("A5 01"), FrameDirection.Rx, ts);
                writer.WriteChunk(Hex.Bytes("0D"), FrameDirection.Tx, ts.AddMilliseconds(1));
                await writer.StopAsync();
            }

            var lines = await File.ReadAllLinesAsync(path);
            Assert.Equal(2, lines.Length);
            Assert.Equal("2026-07-26T12:00:00.500 RX A5 01", lines[0]);
            Assert.Equal("2026-07-26T12:00:00.501 TX 0D", lines[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Append_KeepsExistingContent()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"ft-log-{Guid.NewGuid():N}.log");
        try
        {
            var ts = DateTimeOffset.FromUnixTimeMilliseconds(0);
            await using (var first = new RawLogWriter(path))
            {
                first.WriteChunk(Hex.Bytes("01"), FrameDirection.Rx, ts);
            }
            await using (var second = new RawLogWriter(path))
            {
                second.WriteChunk(Hex.Bytes("02"), FrameDirection.Rx, ts);
            }
            Assert.Equal(2, (await File.ReadAllLinesAsync(path)).Length);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
