namespace Ft.Core.Checksum;

/// <summary>
/// Named checksum presets. Parameters follow the public CRC catalogue
/// (reveng catalogue names); every preset is pinned by golden vector tests.
/// </summary>
public static class ChecksumPresets
{
    public static readonly ChecksumSpec Crc16Modbus =
        ChecksumSpec.Crc(16, 0x8005, 0xFFFF, refIn: true, refOut: true, xorOut: 0x0000);

    public static readonly ChecksumSpec Crc16CcittFalse =
        ChecksumSpec.Crc(16, 0x1021, 0xFFFF, refIn: false, refOut: false, xorOut: 0x0000);

    public static readonly ChecksumSpec Crc32 =
        ChecksumSpec.Crc(32, 0x04C11DB7, 0xFFFFFFFF, refIn: true, refOut: true, xorOut: 0xFFFFFFFF);

    public static readonly ChecksumSpec Crc8 =
        ChecksumSpec.Crc(8, 0x07, 0x00, refIn: false, refOut: false, xorOut: 0x00);

    public static readonly ChecksumSpec Xor8 = new(ChecksumKind.Xor8);

    public static readonly ChecksumSpec Sum8 = new(ChecksumKind.Sum8);

    /// <summary>Stable preset ids used by .ftproj files and the UI.</summary>
    public static readonly IReadOnlyDictionary<string, ChecksumSpec> ByName =
        new Dictionary<string, ChecksumSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["CRC16_MODBUS"] = Crc16Modbus,
            ["CRC16_CCITT_FALSE"] = Crc16CcittFalse,
            ["CRC32"] = Crc32,
            ["CRC8"] = Crc8,
            ["XOR8"] = Xor8,
            ["SUM8"] = Sum8,
        };

    public static string? NameOf(ChecksumSpec spec) =>
        ByName.FirstOrDefault(kv => kv.Value == spec).Key;
}
