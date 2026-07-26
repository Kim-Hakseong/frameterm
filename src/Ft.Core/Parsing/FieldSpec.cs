using Ft.Core.Checksum;

namespace Ft.Core.Parsing;

public enum FieldType
{
    U8,
    S8,
    U16,
    S16,
    U32,
    S32,
    F32,
}

/// <summary>A named typed field at a fixed offset inside a frame.</summary>
public sealed record FieldSpec(
    string Name,
    int Offset,
    FieldType Type,
    ByteOrder Endian = ByteOrder.Little)
{
    public int ByteCount => Type switch
    {
        FieldType.U8 or FieldType.S8 => 1,
        FieldType.U16 or FieldType.S16 => 2,
        _ => 4,
    };
}
