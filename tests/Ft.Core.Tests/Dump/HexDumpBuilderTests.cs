using Ft.Core.Dump;
using Ft.Core.Pipeline;
using Ft.Core.Tests.TestUtil;
using Xunit;

namespace Ft.Core.Tests.Dump;

public class HexDumpBuilderTests
{
    private static readonly DateTimeOffset Ts = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FullRow_CompletesWithPaddedColumns()
    {
        var builder = new HexDumpBuilder(4);
        var rows = builder.Append(Hex.Bytes("41 42 00 7F"), FrameDirection.Rx, Ts);
        var row = Assert.Single(rows);
        Assert.Equal(0, row.Offset);
        Assert.Equal("41 42 00 7F", row.Hex);
        Assert.Equal("AB..", row.Ascii);
        Assert.Null(builder.PartialRow);
    }

    [Fact]
    public void PartialRow_ExposedAndPadded()
    {
        var builder = new HexDumpBuilder(4);
        Assert.Empty(builder.Append(Hex.Bytes("41 42"), FrameDirection.Rx, Ts));
        var partial = builder.PartialRow;
        Assert.NotNull(partial);
        Assert.Equal("41 42      ", partial!.Hex);
        Assert.Equal("AB", partial.Ascii);
    }

    [Fact]
    public void DirectionChange_ClosesRowEarly()
    {
        var builder = new HexDumpBuilder(8);
        builder.Append(Hex.Bytes("41 42"), FrameDirection.Rx, Ts);
        var rows = builder.Append(Hex.Bytes("43"), FrameDirection.Tx, Ts);
        var closed = Assert.Single(rows);
        Assert.Equal(FrameDirection.Rx, closed.Direction);
        Assert.Equal("AB", closed.Ascii);
        Assert.Equal(FrameDirection.Tx, builder.PartialRow!.Direction);
    }

    [Fact]
    public void Offsets_AccumulateAcrossRows()
    {
        var builder = new HexDumpBuilder(2);
        var rows = new List<DumpRow>();
        rows.AddRange(builder.Append(Hex.Bytes("01 02 03 04 05"), FrameDirection.Rx, Ts));
        Assert.Equal(2, rows.Count);
        Assert.Equal(0, rows[0].Offset);
        Assert.Equal(2, rows[1].Offset);
        Assert.Equal(4, builder.PartialRow!.Offset);
    }

    [Fact]
    public void Clear_ResetsOffsets()
    {
        var builder = new HexDumpBuilder(2);
        builder.Append(Hex.Bytes("01 02 03"), FrameDirection.Rx, Ts);
        builder.Clear();
        Assert.Null(builder.PartialRow);
        var rows = builder.Append(Hex.Bytes("0A 0B"), FrameDirection.Rx, Ts);
        Assert.Equal(0, Assert.Single(rows).Offset);
    }
}
