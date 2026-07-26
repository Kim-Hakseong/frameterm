using Ft.Core.Checksum;
using Ft.Core.Compose;
using Ft.Core.Parsing;
using Ft.Core.Tests.TestUtil;
using Xunit;

namespace Ft.Core.Tests.Parsing;

/// <summary>DESIGN §8.3 golden vectors. Do not modify or delete.</summary>
public class ParsingGoldenTests
{
    [Fact]
    public void Composer_AsciiLiteralPlusSum8()
    {
        // DESIGN §8.3 prints SUM8(A5 01 41 42)=0xC9, but §8.1's catalogue-
        // verified SUM8 (plain sum mod 256; "123456789" → 0xDD) gives
        // 0xA5+0x01+0x41+0x42 = 0x129 → 0x29. The §8.3 value is an arithmetic
        // slip in the doc; §8.1 semantics take precedence (RALPH_LOG M3).
        var result = PayloadComposer.Compose("A5 01 \"AB\" {sum8}");
        Assert.True(result.IsOk);
        Assert.Equal(Hex.Bytes("A5 01 41 42 29"), result.Value);
    }

    [Fact]
    public void Composer_LenIsTotalExcludingChecksum()
    {
        var result = PayloadComposer.Compose("A5 {len} 01 02");
        Assert.True(result.IsOk);
        Assert.Equal(Hex.Bytes("A5 04 01 02"), result.Value);
    }

    [Fact]
    public void Field_S16_BigEndian_Negative()
    {
        var spec = new FieldSpec("v", 0, FieldType.S16, ByteOrder.Big);
        var value = FieldParser.ParseOne(spec, Hex.Bytes("FF F6"));
        Assert.True(value.IsAvailable);
        Assert.Equal(-10, value.Numeric);
    }

    [Fact]
    public void Field_F32_BigEndian_Pi()
    {
        var spec = new FieldSpec("v", 0, FieldType.F32, ByteOrder.Big);
        var value = FieldParser.ParseOne(spec, Hex.Bytes("40 49 0F DB"));
        Assert.True(value.IsAvailable);
        Assert.Equal(3.1415927f, value.Numeric, 1e-6);
    }

    [Fact]
    public void Pattern_Wildcard_Matches()
    {
        var pattern = BytePattern.Parse("A5 ?? 01").Value;
        Assert.True(pattern.Matches(Hex.Bytes("A5 77 01 99")));
    }

    [Fact]
    public void Pattern_Wildcard_DoesNotMatch()
    {
        var pattern = BytePattern.Parse("A5 ?? 01").Value;
        Assert.False(pattern.Matches(Hex.Bytes("A5 77 02 99")));
    }
}
