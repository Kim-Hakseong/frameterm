using Ft.Core.Checksum;
using Ft.Core.Compose;
using Ft.Core.Parsing;
using Ft.Core.Tests.TestUtil;
using Xunit;

namespace Ft.Core.Tests.Parsing;

public class ParsingBehaviorTests
{
    [Fact]
    public void Composer_Crc16_IsModbusLittleEndian()
    {
        // 01 03 00 00 00 0A + CRC-16/MODBUS LE = C5 CD (M1 golden frame).
        var result = PayloadComposer.Compose("01 03 00 00 00 0A {crc16}");
        Assert.True(result.IsOk);
        Assert.Equal(Hex.Bytes("01 03 00 00 00 0A C5 CD"), result.Value);
    }

    [Fact]
    public void Composer_NamedPreset()
    {
        var result = PayloadComposer.Compose("01 03 00 00 00 0A {crc:CRC16_MODBUS}");
        Assert.True(result.IsOk);
        Assert.Equal(Hex.Bytes("01 03 00 00 00 0A C5 CD"), result.Value);
    }

    [Fact]
    public void Composer_LenPlusAdjust()
    {
        var result = PayloadComposer.Compose("A5 {len+2} 01 02");
        Assert.True(result.IsOk);
        Assert.Equal(Hex.Bytes("A5 06 01 02"), result.Value);
    }

    [Fact]
    public void Composer_LenCountsItselfButNotChecksum()
    {
        // len must include its own byte and exclude the CRC bytes.
        var result = PayloadComposer.Compose("A5 {len} \"AB\" {crc16}");
        Assert.True(result.IsOk);
        byte[] payload = result.Value;
        Assert.Equal(6, payload.Length);
        Assert.Equal(0x04, payload[1]);
    }

    [Fact]
    public void Composer_Xor8()
    {
        var result = PayloadComposer.Compose("A5 0F {xor8}");
        Assert.True(result.IsOk);
        Assert.Equal(Hex.Bytes("A5 0F AA"), result.Value);
    }

    [Theory]
    [InlineData("GG")]
    [InlineData("A5 {nope}")]
    [InlineData("\"unterminated")]
    [InlineData("{crc:UNKNOWN_PRESET}")]
    [InlineData("")]
    public void Composer_InvalidInput_ReturnsError(string expression) =>
        Assert.False(PayloadComposer.Compose(expression).IsOk);

    [Fact]
    public void Field_OutOfRange_OnlyThatFieldIsNA()
    {
        var specs = new List<FieldSpec>
        {
            new("a", 0, FieldType.U8),
            new("b", 10, FieldType.U16),
        };
        var values = FieldParser.Parse(specs, Hex.Bytes("7F"));
        Assert.True(values[0].IsAvailable);
        Assert.Equal(127, values[0].Numeric);
        Assert.False(values[1].IsAvailable);
        Assert.Equal("N/A", values[1].Display);
    }

    [Fact]
    public void Field_U32_LittleEndian()
    {
        var value = FieldParser.ParseOne(new FieldSpec("v", 0, FieldType.U32), Hex.Bytes("78 56 34 12"));
        Assert.Equal(0x12345678, value.Numeric);
    }

    [Fact]
    public void Rules_FirstMatchWins()
    {
        var rules = new List<HighlightRule>
        {
            new("#111111", BytePattern.Parse("A5 ??").Value),
            new("#222222", BytePattern.Parse("A5 01").Value),
        };
        var color = RuleEvaluator.Evaluate(rules, Hex.Bytes("A5 01 02"), []);
        Assert.Equal("#111111", color);
    }

    [Fact]
    public void Rules_FieldCondition_ComparesNumeric()
    {
        var rules = new List<HighlightRule>
        {
            new("#FF0000", new FieldCondition("temp", FieldOp.Gt, 50)),
        };
        var hotField = new List<FieldValue> { new("temp", true, 51, "51") };
        var coldField = new List<FieldValue> { new("temp", true, 49, "49") };
        Assert.Equal("#FF0000", RuleEvaluator.Evaluate(rules, Hex.Bytes("00"), hotField));
        Assert.Null(RuleEvaluator.Evaluate(rules, Hex.Bytes("00"), coldField));
    }

    [Fact]
    public void Rules_UnavailableField_NeverMatches()
    {
        var rules = new List<HighlightRule>
        {
            new("#FF0000", new FieldCondition("temp", FieldOp.Ne, 0)),
        };
        var fields = new List<FieldValue> { FieldValue.NotAvailable("temp") };
        Assert.Null(RuleEvaluator.Evaluate(rules, Hex.Bytes("00"), fields));
    }

    [Fact]
    public void Rules_NoMatch_ReturnsNull() =>
        Assert.Null(RuleEvaluator.Evaluate(
            [new HighlightRule("#000000", BytePattern.Parse("FF").Value)],
            Hex.Bytes("00"), []));
}
