namespace Ft.Core.Tests.TestUtil;

public static class Hex
{
    /// <summary>Parse "A5 01 0A" style hex strings into bytes.</summary>
    public static byte[] Bytes(string hex) =>
        hex.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => Convert.ToByte(t, 16))
            .ToArray();
}
