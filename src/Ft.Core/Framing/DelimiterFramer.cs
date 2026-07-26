namespace Ft.Core.Framing;

/// <summary>
/// Delimiter-based framing. With a start sequence, a frame spans start..end
/// inclusive and preamble bytes before the start are discarded. Without one,
/// the stream is split after each end sequence. An optional escape byte makes
/// the following byte literal (an escaped end sequence byte does not
/// terminate the frame). Extraction decisions depend only on the accumulated
/// buffer, so results are chunking-invariant by construction.
/// </summary>
public sealed class DelimiterFramer : IFramer
{
    private readonly byte[]? _start;
    private readonly byte[] _end;
    private readonly byte? _escape;
    private readonly int _maxFrame;
    private readonly List<byte> _buffer = [];

    public int ResyncCount { get; private set; }

    public DelimiterFramer(byte[]? startSeq, byte[] endSeq, byte? escapeByte = null, int maxFrame = 4096)
    {
        if (endSeq.Length == 0) throw new ArgumentException("End sequence must not be empty.", nameof(endSeq));
        if (startSeq is { Length: 0 }) throw new ArgumentException("Start sequence must not be empty when provided.", nameof(startSeq));
        if (maxFrame < endSeq.Length) throw new ArgumentOutOfRangeException(nameof(maxFrame));
        _start = startSeq;
        _end = endSeq;
        _escape = escapeByte;
        _maxFrame = maxFrame;
    }

    public IReadOnlyList<RawFrame> Push(ReadOnlySpan<byte> data)
    {
        List<RawFrame>? frames = null;
        foreach (byte b in data)
        {
            _buffer.Add(b);
            while (TryExtract(out var frame))
            {
                (frames ??= []).Add(frame!);
            }
            EnforceMaxFrame();
        }
        return frames ?? (IReadOnlyList<RawFrame>)Array.Empty<RawFrame>();
    }

    public IReadOnlyList<RawFrame> Flush() => Array.Empty<RawFrame>();

    public void Reset() => _buffer.Clear();

    private bool TryExtract(out RawFrame? frame)
    {
        frame = null;
        int contentStart = 0;

        if (_start is not null)
        {
            int startIdx = IndexOfSequence(_buffer, 0, _start);
            if (startIdx < 0)
            {
                // Keep only a possible partial start match at the tail.
                int keep = Math.Min(_buffer.Count, _start.Length - 1);
                int discard = _buffer.Count - keep;
                if (discard > 0) _buffer.RemoveRange(0, discard);
                return false;
            }
            if (startIdx > 0) _buffer.RemoveRange(0, startIdx);
            contentStart = _start.Length;
        }

        int endIdx = FindUnescapedEnd(contentStart);
        if (endIdx < 0) return false;

        int frameLen = endIdx + _end.Length;
        frame = new RawFrame(_buffer.Take(frameLen).ToArray());
        _buffer.RemoveRange(0, frameLen);
        return true;
    }

    /// <summary>Index where an unescaped end sequence begins, or -1.</summary>
    private int FindUnescapedEnd(int from)
    {
        bool escaped = false;
        for (int i = from; i < _buffer.Count; i++)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (_escape.HasValue && _buffer[i] == _escape.Value)
            {
                escaped = true;
                continue;
            }
            if (MatchesAt(i, _end)) return i;
        }
        return -1;
    }

    private bool MatchesAt(int index, byte[] seq)
    {
        if (index + seq.Length > _buffer.Count) return false;
        for (int i = 0; i < seq.Length; i++)
        {
            if (_buffer[index + i] != seq[i]) return false;
        }
        return true;
    }

    private static int IndexOfSequence(List<byte> buffer, int from, byte[] seq)
    {
        for (int i = from; i + seq.Length <= buffer.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < seq.Length; j++)
            {
                if (buffer[i + j] != seq[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    private void EnforceMaxFrame()
    {
        while (_buffer.Count > _maxFrame)
        {
            _buffer.RemoveAt(0);
            ResyncCount++;
        }
    }
}
