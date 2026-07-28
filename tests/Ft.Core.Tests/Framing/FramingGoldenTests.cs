using Ft.Core.Checksum;
using Ft.Core.Framing;
using Ft.Core.Tests.TestUtil;
using Xunit;

namespace Ft.Core.Tests.Framing;

/// <summary>
/// DESIGN §8.2 golden vectors. Every case runs twice: whole-stream push and
/// 1-byte-at-a-time push — results must be identical.
/// Do not modify or delete.
/// </summary>
public class FramingGoldenTests
{
    /// <summary>Push input both ways and assert identical framing output.</summary>
    private static void AssertFrames(Func<IFramer> makeFramer, byte[] input, params byte[][] expected)
    {
        // Whole-stream push.
        var whole = makeFramer();
        var wholeFrames = new List<RawFrame>(whole.Push(input));
        AssertFrameBytes(expected, wholeFrames);

        // 1-byte injection (partial-delivery invariance).
        var single = makeFramer();
        var singleFrames = new List<RawFrame>();
        foreach (byte b in input)
        {
            singleFrames.AddRange(single.Push(new[] { b }));
        }
        AssertFrameBytes(expected, singleFrames);
    }

    private static void AssertFrameBytes(byte[][] expected, List<RawFrame> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i].Bytes);
        }
    }

    [Fact]
    public void Delimiter_EndOnly() =>
        AssertFrames(
            () => new DelimiterFramer(null, [0x0A]),
            Hex.Bytes("41 42 0A 43 0A"),
            Hex.Bytes("41 42 0A"),
            Hex.Bytes("43 0A"));

    [Fact]
    public void Delimiter_StartAndEnd_DiscardsPreamble() =>
        AssertFrames(
            () => new DelimiterFramer([0x02], [0x03]),
            Hex.Bytes("FF 02 41 03 02 42 03"),
            Hex.Bytes("02 41 03"),
            Hex.Bytes("02 42 03"));

    [Fact]
    public void Delimiter_EscapedEndByte_IsNotTerminator() =>
        AssertFrames(
            () => new DelimiterFramer(null, [0x03], escapeByte: 0x1B),
            Hex.Bytes("41 1B 03 42 03"),
            Hex.Bytes("41 1B 03 42 03"));

    [Fact]
    public void FixedLength_CutsEveryN_RemainderStaysBuffered() =>
        AssertFrames(
            () => new FixedLengthFramer(4),
            Hex.Bytes("01 02 03 04 05 06 07 08 09"),
            Hex.Bytes("01 02 03 04"),
            Hex.Bytes("05 06 07 08"));

    [Fact]
    public void LengthField_TotalIsFieldPlusAdjust() =>
        AssertFrames(
            () => new LengthFieldFramer(headerLen: 2, lenOffset: 1, lenSize: 1, ByteOrder.Little, lenAdjust: 2),
            Hex.Bytes("A5 03 11 22 33"),
            Hex.Bytes("A5 03 11 22 33"));

    [Fact]
    public void SilenceGap_FlushAfterGap_EmitsBufferAndRetainsNewBytes()
    {
        var time = new FakeTimeSource();
        var framer = new SilenceGapFramer(gapMs: 10, time);

        Assert.Empty(framer.Push(Hex.Bytes("01 02")));
        time.Advance(15);
        var flushed = framer.Flush();
        Assert.Single(flushed);
        Assert.Equal(Hex.Bytes("01 02"), flushed[0].Bytes);

        Assert.Empty(framer.Push(Hex.Bytes("03")));
        Assert.Empty(framer.Flush()); // gap not yet elapsed for the new byte
    }

    [Fact]
    public void SilenceGap_OneByteInjection_SameResult()
    {
        var time = new FakeTimeSource();
        var framer = new SilenceGapFramer(gapMs: 10, time);

        foreach (byte b in Hex.Bytes("01 02"))
        {
            Assert.Empty(framer.Push(new[] { b }));
        }
        time.Advance(15);
        var flushed = framer.Flush();
        Assert.Single(flushed);
        Assert.Equal(Hex.Bytes("01 02"), flushed[0].Bytes);

        Assert.Empty(framer.Push(Hex.Bytes("03")));
        Assert.Empty(framer.Flush());
    }
}
