using System.Text;
using Ft.Core.Pipeline;

namespace Ft.Core.Dump;

/// <summary>A rendered dump row: offset + hex column + ASCII column.</summary>
public sealed record DumpRow(
    long Offset,
    string Time,
    FrameDirection Direction,
    string Hex,
    string Ascii);

/// <summary>
/// Builds fixed-width hex+ASCII dump rows from a byte stream. A row closes
/// when it reaches BytesPerRow or the direction flips; the still-filling row
/// is exposed as <see cref="PartialRow"/>. Pure formatting — no UI types.
/// </summary>
public sealed class HexDumpBuilder(int bytesPerRow = 16)
{
    private readonly List<byte> _pending = [];
    private long _offset;
    private long _rowStartOffset;
    private FrameDirection _pendingDir;
    private string _pendingTime = string.Empty;

    public int BytesPerRow { get; } = bytesPerRow > 0
        ? bytesPerRow
        : throw new ArgumentOutOfRangeException(nameof(bytesPerRow));

    /// <summary>Append a chunk; returns rows completed by this chunk.</summary>
    public IReadOnlyList<DumpRow> Append(ReadOnlySpan<byte> data, FrameDirection dir, DateTimeOffset ts)
    {
        var completed = new List<DumpRow>();
        string time = ts.ToString("HH:mm:ss.fff");

        if (_pending.Count > 0 && _pendingDir != dir)
        {
            completed.Add(CloseRow());
        }
        if (_pending.Count == 0)
        {
            _pendingDir = dir;
            _pendingTime = time;
            _rowStartOffset = _offset;
        }

        foreach (byte b in data)
        {
            _pending.Add(b);
            _offset++;
            if (_pending.Count == BytesPerRow)
            {
                completed.Add(CloseRow());
                _pendingDir = dir;
                _pendingTime = time;
                _rowStartOffset = _offset;
            }
        }
        return completed;
    }

    /// <summary>The currently filling row, or null when aligned.</summary>
    public DumpRow? PartialRow =>
        _pending.Count == 0 ? null : Render(_rowStartOffset, _pendingTime, _pendingDir, _pending);

    public void Clear()
    {
        _pending.Clear();
        _offset = 0;
        _rowStartOffset = 0;
    }

    private DumpRow CloseRow()
    {
        var row = Render(_rowStartOffset, _pendingTime, _pendingDir, _pending);
        _pending.Clear();
        return row;
    }

    private DumpRow Render(long offset, string time, FrameDirection dir, List<byte> bytes)
    {
        var hex = new StringBuilder(BytesPerRow * 3);
        var ascii = new StringBuilder(BytesPerRow);
        for (int i = 0; i < bytes.Count; i++)
        {
            if (i > 0) hex.Append(' ');
            hex.Append(bytes[i].ToString("X2"));
            ascii.Append(bytes[i] is >= 0x20 and < 0x7F ? (char)bytes[i] : '.');
        }
        // Pad hex so the ASCII column lines up in a monospace font.
        int fullWidth = BytesPerRow * 3 - 1;
        if (hex.Length < fullWidth) hex.Append(' ', fullWidth - hex.Length);
        return new DumpRow(offset, time, dir, hex.ToString(), ascii.ToString());
    }
}
