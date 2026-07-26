namespace Ft.Core.Framing;

/// <summary>
/// Incremental stream framer. Contract: the emitted frame sequence must be
/// identical no matter how the input stream is chunked (partial-delivery
/// invariance, CLAUDE.md §4-3). Implementations keep internal buffer state.
/// </summary>
public interface IFramer
{
    /// <summary>Feed bytes; returns zero or more completed frames.</summary>
    IReadOnlyList<RawFrame> Push(ReadOnlySpan<byte> data);

    /// <summary>
    /// Time-driven flush hook (silence-gap). Non-time-based framers return
    /// an empty list. The pipeline calls this periodically.
    /// </summary>
    IReadOnlyList<RawFrame> Flush();

    /// <summary>Bytes skipped while re-synchronizing on garbage input.</summary>
    int ResyncCount { get; }

    /// <summary>Drop all buffered state.</summary>
    void Reset();
}
