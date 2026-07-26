namespace Ft.Core.Time;

/// <summary>
/// Injectable clock so time-dependent logic (silence-gap framing, timestamps)
/// is deterministic under test. Never sleep in tests — advance a fake instead.
/// </summary>
public interface ITimeSource
{
    /// <summary>Monotonic milliseconds; only differences are meaningful.</summary>
    long MonotonicMillis { get; }

    /// <summary>Wall-clock time for display timestamps.</summary>
    DateTimeOffset Now { get; }
}

public sealed class SystemTimeSource : ITimeSource
{
    public static readonly SystemTimeSource Instance = new();

    public long MonotonicMillis => Environment.TickCount64;
    public DateTimeOffset Now => DateTimeOffset.Now;
}
