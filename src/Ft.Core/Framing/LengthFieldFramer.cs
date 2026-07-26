using Ft.Core.Checksum;

namespace Ft.Core.Framing;

/// <summary>
/// Length-field framing: total frame length = length-field value + lenAdjust
/// (lenAdjust absorbs whether the field counts header/CRC bytes). When the
/// implied length is nonsensical (shorter than the bytes needed to read the
/// field, or above maxFrame) the framer resyncs by skipping one byte.
/// </summary>
public sealed class LengthFieldFramer : IFramer
{
    private readonly int _headerLen;
    private readonly int _lenOffset;
    private readonly int _lenSize;
    private readonly ByteOrder _endian;
    private readonly int _lenAdjust;
    private readonly int _maxFrame;
    private readonly List<byte> _buffer = [];

    public int ResyncCount { get; private set; }

    public LengthFieldFramer(
        int headerLen, int lenOffset, int lenSize, ByteOrder endian, int lenAdjust, int maxFrame = 4096)
    {
        if (lenSize is not (1 or 2 or 4)) throw new ArgumentOutOfRangeException(nameof(lenSize), "Length field size must be 1, 2 or 4.");
        if (lenOffset < 0) throw new ArgumentOutOfRangeException(nameof(lenOffset));
        if (headerLen < 0) throw new ArgumentOutOfRangeException(nameof(headerLen));
        if (maxFrame < 1) throw new ArgumentOutOfRangeException(nameof(maxFrame));
        _headerLen = headerLen;
        _lenOffset = lenOffset;
        _lenSize = lenSize;
        _endian = endian;
        _lenAdjust = lenAdjust;
        _maxFrame = maxFrame;
    }

    /// <summary>Bytes required before the length field can be read.</summary>
    private int MinReadable => Math.Max(_headerLen, _lenOffset + _lenSize);

    public IReadOnlyList<RawFrame> Push(ReadOnlySpan<byte> data)
    {
        List<RawFrame>? frames = null;
        foreach (byte b in data)
        {
            _buffer.Add(b);
            while (TryExtract(out var frame))
            {
                if (frame is not null) (frames ??= []).Add(frame);
            }
        }
        return frames ?? (IReadOnlyList<RawFrame>)Array.Empty<RawFrame>();
    }

    /// <summary>
    /// Returns true when progress was made (frame emitted OR a resync skip
    /// happened); the caller loops until neither is possible.
    /// </summary>
    private bool TryExtract(out RawFrame? frame)
    {
        frame = null;
        if (_buffer.Count < MinReadable) return false;

        long lenValue = 0;
        for (int i = 0; i < _lenSize; i++)
        {
            int shift = _endian == ByteOrder.Little ? 8 * i : 8 * (_lenSize - 1 - i);
            lenValue |= (long)_buffer[_lenOffset + i] << shift;
        }

        long total = lenValue + _lenAdjust;
        if (total < MinReadable || total > _maxFrame)
        {
            _buffer.RemoveAt(0);
            ResyncCount++;
            return true;
        }

        if (_buffer.Count < total) return false;

        frame = new RawFrame(_buffer.Take((int)total).ToArray());
        _buffer.RemoveRange(0, (int)total);
        return true;
    }

    public IReadOnlyList<RawFrame> Flush() => Array.Empty<RawFrame>();

    public void Reset() => _buffer.Clear();
}
