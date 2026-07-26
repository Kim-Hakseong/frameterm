using System.Globalization;
using Ft.Core.Checksum;

namespace Ft.Core.Parsing;

/// <summary>Extracts typed field values from a raw frame.</summary>
public static class FieldParser
{
    public static IReadOnlyList<FieldValue> Parse(IReadOnlyList<FieldSpec> specs, ReadOnlySpan<byte> frame)
    {
        var values = new List<FieldValue>(specs.Count);
        foreach (var spec in specs)
        {
            values.Add(ParseOne(spec, frame));
        }
        return values;
    }

    public static FieldValue ParseOne(FieldSpec spec, ReadOnlySpan<byte> frame)
    {
        if (spec.Offset < 0 || spec.Offset + spec.ByteCount > frame.Length)
        {
            return FieldValue.NotAvailable(spec.Name);
        }

        var slice = frame.Slice(spec.Offset, spec.ByteCount);
        Span<byte> le = stackalloc byte[4];
        for (int i = 0; i < slice.Length; i++)
        {
            le[i] = spec.Endian == ByteOrder.Little ? slice[i] : slice[slice.Length - 1 - i];
        }

        double numeric;
        string display;
        switch (spec.Type)
        {
            case FieldType.U8:
                numeric = le[0];
                display = ((byte)numeric).ToString(CultureInfo.InvariantCulture);
                break;
            case FieldType.S8:
                numeric = unchecked((sbyte)le[0]);
                display = ((sbyte)numeric).ToString(CultureInfo.InvariantCulture);
                break;
            case FieldType.U16:
                numeric = (ushort)(le[0] | le[1] << 8);
                display = ((ushort)numeric).ToString(CultureInfo.InvariantCulture);
                break;
            case FieldType.S16:
                numeric = unchecked((short)(le[0] | le[1] << 8));
                display = ((short)numeric).ToString(CultureInfo.InvariantCulture);
                break;
            case FieldType.U32:
                numeric = (uint)(le[0] | le[1] << 8 | le[2] << 16 | le[3] << 24);
                display = ((uint)numeric).ToString(CultureInfo.InvariantCulture);
                break;
            case FieldType.S32:
                numeric = le[0] | le[1] << 8 | le[2] << 16 | le[3] << 24;
                display = ((int)numeric).ToString(CultureInfo.InvariantCulture);
                break;
            case FieldType.F32:
                float f = BitConverter.ToSingle(le[..4]);
                numeric = f;
                display = f.ToString("R", CultureInfo.InvariantCulture);
                break;
            default:
                return FieldValue.NotAvailable(spec.Name);
        }

        return new FieldValue(spec.Name, true, numeric, display);
    }
}
