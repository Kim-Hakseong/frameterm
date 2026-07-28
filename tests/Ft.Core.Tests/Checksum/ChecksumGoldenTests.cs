using System.Text;
using Ft.Core.Checksum;
using Xunit;

namespace Ft.Core.Tests.Checksum;

/// <summary>
/// DESIGN §8.1 golden vectors — validated against the public CRC catalogue.
/// Do not modify or delete.
/// </summary>
public class ChecksumGoldenTests
{
    private static byte[] Check => Encoding.ASCII.GetBytes("123456789");

    [Fact]
    public void Crc16Modbus_CheckString() =>
        Assert.Equal(0x4B37u, ChecksumEngine.Compute(ChecksumPresets.Crc16Modbus, Check));

    [Fact]
    public void Crc16CcittFalse_CheckString() =>
        Assert.Equal(0x29B1u, ChecksumEngine.Compute(ChecksumPresets.Crc16CcittFalse, Check));

    [Fact]
    public void Crc32_CheckString() =>
        Assert.Equal(0xCBF43926u, ChecksumEngine.Compute(ChecksumPresets.Crc32, Check));

    [Fact]
    public void Crc8_CheckString() =>
        Assert.Equal(0xF4u, ChecksumEngine.Compute(ChecksumPresets.Crc8, Check));

    [Fact]
    public void Crc8_PolyD5_InitFF_CheckString()
    {
        var spec = ChecksumSpec.Crc(8, 0xD5, 0xFF, refIn: false, refOut: false, xorOut: 0x00);
        Assert.Equal(0x7Cu, ChecksumEngine.Compute(spec, Check));
    }

    [Fact]
    public void Xor8_CheckString() =>
        Assert.Equal(0x31u, ChecksumEngine.Compute(ChecksumPresets.Xor8, Check));

    [Fact]
    public void Sum8_CheckString() =>
        Assert.Equal(0xDDu, ChecksumEngine.Compute(ChecksumPresets.Sum8, Check));

    [Fact]
    public void ModbusRtu_RealFrame_CrcBytes()
    {
        byte[] payload = [0x01, 0x03, 0x00, 0x00, 0x00, 0x0A];
        uint crc = ChecksumEngine.Compute(ChecksumPresets.Crc16Modbus, payload);
        byte[] wire = ChecksumEngine.ToBytes(ChecksumPresets.Crc16Modbus, crc, ByteOrder.Little);
        Assert.Equal(new byte[] { 0xC5, 0xCD }, wire);
    }

    [Fact]
    public void ModbusRtu_FullFrame_PlacementVerifies()
    {
        byte[] frame = [0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD];
        var placement = new ChecksumPlacement(
            OffsetFromEnd: 2, ByteOrder.Little, CoverageStart: 0, CoverageEndOffsetFromEnd: 2);
        var result = placement.Verify(ChecksumPresets.Crc16Modbus, frame);
        Assert.True(result.IsOk);
        Assert.True(result.Value);
    }

    [Fact]
    public void ModbusRtu_CorruptedFrame_PlacementFails()
    {
        byte[] frame = [0x01, 0x03, 0x00, 0x01, 0x00, 0x0A, 0xC5, 0xCD];
        var placement = new ChecksumPlacement(2, ByteOrder.Little, 0, 2);
        var result = placement.Verify(ChecksumPresets.Crc16Modbus, frame);
        Assert.True(result.IsOk);
        Assert.False(result.Value);
    }

    [Fact]
    public void Placement_FrameTooShort_ReturnsError()
    {
        byte[] frame = [0x01];
        var placement = new ChecksumPlacement(2, ByteOrder.Little, 0, 2);
        Assert.False(placement.Verify(ChecksumPresets.Crc16Modbus, frame).IsOk);
    }
}
