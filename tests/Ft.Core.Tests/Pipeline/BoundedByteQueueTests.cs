using Ft.Core.Pipeline;
using Xunit;

namespace Ft.Core.Tests.Pipeline;

public class BoundedByteQueueTests
{
    [Fact]
    public void Fifo_Order()
    {
        var queue = new BoundedByteQueue(4);
        queue.Enqueue([1]);
        queue.Enqueue([2]);
        Assert.True(queue.TryDequeue(out var first));
        Assert.Equal(new byte[] { 1 }, first);
        Assert.True(queue.TryDequeue(out var second));
        Assert.Equal(new byte[] { 2 }, second);
        Assert.False(queue.TryDequeue(out _));
    }

    [Fact]
    public void Overflow_DropsOldestAndCounts()
    {
        var queue = new BoundedByteQueue(2);
        queue.Enqueue([1]);
        queue.Enqueue([2]);
        queue.Enqueue([3]);
        Assert.Equal(1, queue.DropCount);
        Assert.Equal(2, queue.Count);
        Assert.True(queue.TryDequeue(out var first));
        Assert.Equal(new byte[] { 2 }, first);
    }
}
