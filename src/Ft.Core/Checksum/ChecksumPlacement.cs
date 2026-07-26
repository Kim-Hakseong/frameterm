namespace Ft.Core.Checksum;

/// <summary>
/// Where a checksum lives inside a frame and which bytes it covers.
/// Example — Modbus RTU: OffsetFromEnd=2, ByteOrder=Little, CoverageStart=0,
/// CoverageEndOffsetFromEnd=2 (coverage runs from frame start up to the CRC).
/// </summary>
public sealed record ChecksumPlacement(
    int OffsetFromEnd,
    ByteOrder ByteOrder,
    int CoverageStart = 0,
    int CoverageEndOffsetFromEnd = 0)
{
    /// <summary>
    /// Verify a frame. Returns Ok(true/false) for match/mismatch and Fail when
    /// the frame is too short to contain the checksum or the coverage range.
    /// </summary>
    public Result<bool> Verify(ChecksumSpec spec, ReadOnlySpan<byte> frame)
    {
        int n = spec.ByteCount;
        int checksumStart = frame.Length - OffsetFromEnd;
        if (checksumStart < 0 || checksumStart + n > frame.Length)
        {
            return Result<bool>.Fail("Frame too short for checksum placement.");
        }

        int coverageEnd = frame.Length - CoverageEndOffsetFromEnd;
        if (CoverageStart < 0 || coverageEnd < CoverageStart || coverageEnd > frame.Length)
        {
            return Result<bool>.Fail("Frame too short for checksum coverage.");
        }

        uint expected = ChecksumEngine.Compute(spec, frame[CoverageStart..coverageEnd]);
        uint actual = ChecksumEngine.FromBytes(frame.Slice(checksumStart, n), ByteOrder);
        return Result<bool>.Ok(expected == actual);
    }
}
