using Ft.Core.Time;

namespace Ft.Core.Framing;

/// <summary>
/// Emits the buffered bytes as one frame after <c>gapMs</c> of silence.
/// Purely Flush()-driven for determinism: the pipeline calls Flush on a
/// timer, and Push also closes an elapsed gap before appending new bytes.
/// </summary>
public sealed class SilenceGapFramer : IFramer
{
    private readonly int _gapMs;
    private readonly ITimeSource _time;
    private readonly List<byte> _buffer = [];
    private long _lastReceiveMs;

    public int ResyncCount => 0;

    public SilenceGapFramer(int gapMs, ITimeSource time)
    {
        if (gapMs <= 0) throw new ArgumentOutOfRangeException(nameof(gapMs), "Gap must be positive.");
        _gapMs = gapMs;
        _time = time;
    }

    public IReadOnlyList<RawFrame> Push(ReadOnlySpan<byte> data)
    {
        long now = _time.MonotonicMillis;
        List<RawFrame>? frames = null;
        if (_buffer.Count > 0 && now - _lastReceiveMs >= _gapMs)
        {
            (frames ??= []).Add(Emit());
        }
        foreach (byte b in data) _buffer.Add(b);
        if (!data.IsEmpty) _lastReceiveMs = now;
        return frames ?? (IReadOnlyList<RawFrame>)Array.Empty<RawFrame>();
    }

    public IReadOnlyList<RawFrame> Flush()
    {
        if (_buffer.Count == 0 || _time.MonotonicMillis - _lastReceiveMs < _gapMs)
        {
            return Array.Empty<RawFrame>();
        }
        return [Emit()];
    }

    public void Reset() => _buffer.Clear();

    private RawFrame Emit()
    {
        var frame = new RawFrame([.. _buffer]);
        _buffer.Clear();
        return frame;
    }
}
