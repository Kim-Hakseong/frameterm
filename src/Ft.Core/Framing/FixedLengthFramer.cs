namespace Ft.Core.Framing;

/// <summary>Cuts the stream into frames of exactly <paramref name="length"/> bytes.</summary>
public sealed class FixedLengthFramer(int length) : IFramer
{
    private readonly int _length = length > 0
        ? length
        : throw new ArgumentOutOfRangeException(nameof(length), "Frame length must be positive.");
    private readonly List<byte> _buffer = [];

    public int ResyncCount => 0;

    public IReadOnlyList<RawFrame> Push(ReadOnlySpan<byte> data)
    {
        List<RawFrame>? frames = null;
        foreach (byte b in data)
        {
            _buffer.Add(b);
            if (_buffer.Count == _length)
            {
                (frames ??= []).Add(new RawFrame([.. _buffer]));
                _buffer.Clear();
            }
        }
        return frames ?? (IReadOnlyList<RawFrame>)Array.Empty<RawFrame>();
    }

    public IReadOnlyList<RawFrame> Flush() => Array.Empty<RawFrame>();

    public void Reset() => _buffer.Clear();
}
