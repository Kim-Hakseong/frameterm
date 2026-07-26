namespace Ft.Core.Checksum;

public enum ChecksumKind
{
    /// <summary>Parameterized CRC (width 8/16/32).</summary>
    Crc,
    /// <summary>XOR of all covered bytes.</summary>
    Xor8,
    /// <summary>Sum of all covered bytes modulo 256.</summary>
    Sum8,
}

/// <summary>
/// Full parameterization of a checksum algorithm. CRC parameters follow the
/// Rocksoft/catalogue model: width, poly, init, refin, refout, xorout.
/// Xor8/Sum8 ignore the CRC parameters.
/// </summary>
public sealed record ChecksumSpec(
    ChecksumKind Kind,
    int Width = 8,
    uint Poly = 0,
    uint Init = 0,
    bool RefIn = false,
    bool RefOut = false,
    uint XorOut = 0)
{
    /// <summary>Number of bytes the checksum occupies inside a frame.</summary>
    public int ByteCount => Kind == ChecksumKind.Crc ? Width / 8 : 1;

    public static ChecksumSpec Crc(int width, uint poly, uint init, bool refIn, bool refOut, uint xorOut) =>
        width is 8 or 16 or 32
            ? new ChecksumSpec(ChecksumKind.Crc, width, poly, init, refIn, refOut, xorOut)
            : throw new ArgumentOutOfRangeException(nameof(width), "CRC width must be 8, 16 or 32.");
}
