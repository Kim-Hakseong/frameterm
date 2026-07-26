using Ft.Core.Time;

namespace Ft.Core.Tests.TestUtil;

/// <summary>Deterministic clock for time-dependent tests (no sleeping).</summary>
public sealed class FakeTimeSource : ITimeSource
{
    public long MonotonicMillis { get; private set; }
    public DateTimeOffset Now { get; private set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Advance(long millis)
    {
        MonotonicMillis += millis;
        Now = Now.AddMilliseconds(millis);
    }
}
