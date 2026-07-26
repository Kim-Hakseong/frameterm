namespace Ft.Core.Framing;

/// <summary>A complete frame cut from the byte stream by a framer.</summary>
public sealed class RawFrame(byte[] bytes)
{
    public byte[] Bytes { get; } = bytes;
    public int Length => Bytes.Length;
}
