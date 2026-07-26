using Ft.Core.Checksum;
using Xunit;

namespace Ft.Core.Tests.Checksum;

public class ChecksumEngineTests
{
    [Fact]
    public void ToBytes_LittleEndian_16Bit()
    {
        byte[] bytes = ChecksumEngine.ToBytes(ChecksumPresets.Crc16Modbus, 0x4B37, ByteOrder.Little);
        Assert.Equal(new byte[] { 0x37, 0x4B }, bytes);
    }

    [Fact]
    public void ToBytes_BigEndian_16Bit()
    {
        byte[] bytes = ChecksumEngine.ToBytes(ChecksumPresets.Crc16Modbus, 0x4B37, ByteOrder.Big);
        Assert.Equal(new byte[] { 0x4B, 0x37 }, bytes);
    }

    [Fact]
    public void ToBytes_32Bit_RoundTripsThroughFromBytes()
    {
        foreach (var order in new[] { ByteOrder.Little, ByteOrder.Big })
        {
            byte[] bytes = ChecksumEngine.ToBytes(ChecksumPresets.Crc32, 0xCBF43926, order);
            Assert.Equal(4, bytes.Length);
            Assert.Equal(0xCBF43926u, ChecksumEngine.FromBytes(bytes, order));
        }
    }

    [Fact]
    public void EmptyInput_CrcEqualsInitTransform()
    {
        // CRC of empty input must be refout(init) ^ xorout; CCITT-FALSE: 0xFFFF.
        Assert.Equal(0xFFFFu, ChecksumEngine.Compute(ChecksumPresets.Crc16CcittFalse, []));
    }

    [Fact]
    public void Sum8_WrapsModulo256()
    {
        byte[] data = [0xFF, 0x02];
        Assert.Equal(0x01u, ChecksumEngine.Compute(ChecksumPresets.Sum8, data));
    }

    [Fact]
    public void CrcSpec_InvalidWidth_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ChecksumSpec.Crc(12, 0x80F, 0, false, false, 0));

    [Fact]
    public void Presets_LookupByName_IsCaseInsensitive()
    {
        Assert.Same(ChecksumPresets.Crc16Modbus, ChecksumPresets.ByName["crc16_modbus"]);
        Assert.Equal("CRC16_MODBUS", ChecksumPresets.NameOf(ChecksumPresets.Crc16Modbus));
    }
}
