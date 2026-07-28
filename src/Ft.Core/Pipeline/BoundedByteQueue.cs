namespace Ft.Core.Pipeline;

/// <summary>
/// Bounded FIFO of byte chunks between the reader task and the processor.
/// When full, the oldest chunk is dropped and counted — backpressure never
/// blocks the serial reader.
/// </summary>
public sealed class BoundedByteQueue(int capacity = 1024)
{
    private readonly Queue<byte[]> _queue = new();
    private readonly object _gate = new();
    private long _dropCount;

    public long DropCount => Interlocked.Read(ref _dropCount);
    public int Count { get { lock (_gate) return _queue.Count; } }

    public void Enqueue(byte[] chunk)
    {
        lock (_gate)
        {
            while (_queue.Count >= capacity)
            {
                _queue.Dequeue();
                Interlocked.Increment(ref _dropCount);
            }
            _queue.Enqueue(chunk);
        }
    }

    public bool TryDequeue(out byte[] chunk)
    {
        lock (_gate)
        {
            if (_queue.Count > 0)
            {
                chunk = _queue.Dequeue();
                return true;
            }
        }
        chunk = [];
        return false;
    }
}
