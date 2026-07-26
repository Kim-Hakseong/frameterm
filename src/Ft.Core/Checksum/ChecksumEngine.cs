namespace Ft.Core.Checksum;

/// <summary>
/// Single parameterized checksum implementation. CRC is computed bit-by-bit
/// (MSB-first with per-byte reflection for RefIn) so any width/poly/init
/// combination works; presets are validated against golden vectors.
/// </summary>
public static class ChecksumEngine
{
    public static uint Compute(ChecksumSpec spec, ReadOnlySpan<byte> data) => spec.Kind switch
    {
        ChecksumKind.Crc => ComputeCrc(spec, data),
        ChecksumKind.Xor8 => ComputeXor8(data),
        ChecksumKind.Sum8 => ComputeSum8(data),
        _ => throw new ArgumentOutOfRangeException(nameof(spec), $"Unknown checksum kind {spec.Kind}."),
    };

    public static uint ComputeXor8(ReadOnlySpan<byte> data)
    {
        byte acc = 0;
        foreach (byte b in data) acc ^= b;
        return acc;
    }

    public static uint ComputeSum8(ReadOnlySpan<byte> data)
    {
        byte acc = 0;
        foreach (byte b in data) acc = unchecked((byte)(acc + b));
        return acc;
    }

    public static uint ComputeCrc(ChecksumSpec spec, ReadOnlySpan<byte> data)
    {
        int width = spec.Width;
        uint mask = width == 32 ? 0xFFFFFFFFu : (1u << width) - 1;
        uint topBit = 1u << (width - 1);
        uint crc = spec.Init & mask;

        foreach (byte raw in data)
        {
            byte b = spec.RefIn ? Reflect8(raw) : raw;
            crc ^= (uint)b << (width - 8);
            for (int i = 0; i < 8; i++)
            {
                crc = (crc & topBit) != 0 ? (crc << 1) ^ spec.Poly : crc << 1;
                crc &= mask;
            }
        }

        if (spec.RefOut) crc = Reflect(crc, width);
        return (crc ^ spec.XorOut) & mask;
    }

    /// <summary>Serialize a checksum value to its on-wire bytes.</summary>
    public static byte[] ToBytes(ChecksumSpec spec, uint value, ByteOrder order)
    {
        int n = spec.ByteCount;
        var bytes = new byte[n];
        for (int i = 0; i < n; i++)
        {
            int shift = order == ByteOrder.Little ? 8 * i : 8 * (n - 1 - i);
            bytes[i] = (byte)(value >> shift);
        }
        return bytes;
    }

    /// <summary>Read a checksum value from its on-wire bytes.</summary>
    public static uint FromBytes(ReadOnlySpan<byte> bytes, ByteOrder order)
    {
        uint value = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            int shift = order == ByteOrder.Little ? 8 * i : 8 * (bytes.Length - 1 - i);
            value |= (uint)bytes[i] << shift;
        }
        return value;
    }

    internal static byte Reflect8(byte b)
    {
        byte r = 0;
        for (int i = 0; i < 8; i++)
        {
            if ((b & (1 << i)) != 0) r |= (byte)(1 << (7 - i));
        }
        return r;
    }

    internal static uint Reflect(uint value, int width)
    {
        uint r = 0;
        for (int i = 0; i < width; i++)
        {
            if ((value & (1u << i)) != 0) r |= 1u << (width - 1 - i);
        }
        return r;
    }
}
