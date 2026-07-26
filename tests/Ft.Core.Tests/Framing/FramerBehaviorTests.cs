using Ft.Core.Checksum;
using Ft.Core.Framing;
using Ft.Core.Tests.TestUtil;
using Xunit;

namespace Ft.Core.Tests.Framing;

/// <summary>Behavior beyond the golden vectors: resync, chunking sweeps, edge cases.</summary>
public class FramerBehaviorTests
{
    /// <summary>Push the same stream in every chunk size and compare against whole-push.</summary>
    private static void AssertChunkingInvariant(Func<IFramer> makeFramer, byte[] input)
    {
        var whole = makeFramer();
        var expected = whole.Push(input).Select(f => f.Bytes).ToList();

        for (int chunkSize = 1; chunkSize <= Math.Min(input.Length, 7); chunkSize++)
        {
            var framer = makeFramer();
            var frames = new List<RawFrame>();
            for (int i = 0; i < input.Length; i += chunkSize)
            {
                frames.AddRange(framer.Push(input.AsSpan(i, Math.Min(chunkSize, input.Length - i))));
            }
            Assert.Equal(expected.Count, frames.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], frames[i].Bytes);
            }
        }
    }

    [Fact]
    public void Delimiter_MultiByteEndSequence_AllChunkSizes() =>
        AssertChunkingInvariant(
            () => new DelimiterFramer(null, [0x0D, 0x0A]),
            Hex.Bytes("41 0D 42 0D 0A 43 0D 0A 44"));

    [Fact]
    public void Delimiter_MultiByteStartSequence_AllChunkSizes() =>
        AssertChunkingInvariant(
            () => new DelimiterFramer([0xAA, 0x55], [0x0D, 0x0A]),
            Hex.Bytes("00 AA AA 55 01 02 0D 0A AA 55 03 0D 0A"));

    [Fact]
    public void Delimiter_MaxFrameOverflow_ResyncsByDroppingOldest()
    {
        var framer = new DelimiterFramer(null, [0x0A], maxFrame: 4);
        Assert.Empty(framer.Push(Hex.Bytes("01 02 03 04 05 06")));
        Assert.Equal(2, framer.ResyncCount);
        // A terminator still closes what remains in the window.
        var frames = framer.Push(Hex.Bytes("0A"));
        Assert.Single(frames);
        Assert.Equal(Hex.Bytes("03 04 05 06 0A"), frames[0].Bytes);
    }

    [Fact]
    public void LengthField_TwoByteBigEndianField_AllChunkSizes() =>
        // total = lenValue + 4 (2 header + 2 crc absorbed by adjust)
        AssertChunkingInvariant(
            () => new LengthFieldFramer(headerLen: 4, lenOffset: 2, lenSize: 2, ByteOrder.Big, lenAdjust: 4),
            Hex.Bytes("A5 5A 00 02 11 22 A5 5A 00 01 33"));

    [Fact]
    public void LengthField_NonsenseLength_SkipsOneByteAndCountsResync()
    {
        // lenAdjust 0 with lenValue 0 → total 0 < readable minimum → resync.
        var framer = new LengthFieldFramer(headerLen: 1, lenOffset: 0, lenSize: 1, ByteOrder.Little, lenAdjust: 0);
        var frames = framer.Push(Hex.Bytes("00 00 03 AA BB"));
        Assert.True(framer.ResyncCount >= 2);
        Assert.Single(frames);
        Assert.Equal(Hex.Bytes("03 AA BB"), frames[0].Bytes);
    }

    [Fact]
    public void LengthField_OversizedLength_Resyncs()
    {
        var framer = new LengthFieldFramer(headerLen: 1, lenOffset: 0, lenSize: 1, ByteOrder.Little, lenAdjust: 0, maxFrame: 16);
        Assert.Empty(framer.Push(Hex.Bytes("FF")));
        Assert.Equal(1, framer.ResyncCount);
    }

    [Fact]
    public void FixedLength_ExactMultiple_NoResidue() =>
        AssertChunkingInvariant(() => new FixedLengthFramer(3), Hex.Bytes("01 02 03 04 05 06"));

    [Fact]
    public void SilenceGap_PushAfterElapsedGap_ClosesPreviousFrameFirst()
    {
        var time = new FakeTimeSource();
        var framer = new SilenceGapFramer(gapMs: 10, time);

        framer.Push(Hex.Bytes("01 02"));
        time.Advance(20);
        // No Flush happened; the next Push must still cut at the gap.
        var frames = framer.Push(Hex.Bytes("03"));
        Assert.Single(frames);
        Assert.Equal(Hex.Bytes("01 02"), frames[0].Bytes);

        time.Advance(10);
        var flushed = framer.Flush();
        Assert.Single(flushed);
        Assert.Equal(Hex.Bytes("03"), flushed[0].Bytes);
    }

    [Fact]
    public void Reset_DropsBufferedBytes()
    {
        var framer = new FixedLengthFramer(4);
        framer.Push(Hex.Bytes("01 02 03"));
        framer.Reset();
        Assert.Empty(framer.Push(Hex.Bytes("04 05 06")));
    }
}
