using Ft.Core.Time;
using Ft.Core.Transport;

namespace Ft.Core.Pipeline;

/// <summary>
/// Async host around PipelineCore: a reader task pulls transport bytes into a
/// bounded queue (oldest-drop backpressure) and a processor task drains it on
/// a fixed cadence, batching UI events so a 921600bps flood cannot swamp the
/// frontend. Raw bytes are also published pre-framing for dump view/logging.
/// </summary>
public sealed class RxPipeline : IAsyncDisposable
{
    private readonly ITransport _transport;
    private readonly PipelineCore _core;
    private readonly BoundedByteQueue _queue;
    private readonly int _batchMs;
    private CancellationTokenSource? _cts;
    private Task? _readerTask;
    private Task? _processorTask;
    private long _rxBytes;
    private long _txBytes;

    /// <summary>Batched frame records (RX from the processor cadence; TX immediate).</summary>
    public event Action<IReadOnlyList<FrameRecord>>? FramesReady;

    /// <summary>Raw byte chunks with direction/timestamp, pre-framing (dump view + logger).</summary>
    public event Action<byte[], FrameDirection, DateTimeOffset>? BytesFlowed;

    /// <summary>Transport-level errors (read loop keeps running policy decisions upstream).</summary>
    public event Action<string>? TransportError;

    public long DropCount => _queue.DropCount;
    public long RxBytes => Interlocked.Read(ref _rxBytes);
    public long TxBytes => Interlocked.Read(ref _txBytes);
    public ITimeSource Time { get; }

    public RxPipeline(
        ITransport transport,
        PipelineConfig config,
        ITimeSource? time = null,
        int queueCapacity = 1024,
        int batchMs = 50)
    {
        _transport = transport;
        Time = time ?? SystemTimeSource.Instance;
        _core = new PipelineCore(config, Time);
        _queue = new BoundedByteQueue(queueCapacity);
        _batchMs = batchMs;
    }

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _readerTask = Task.Run(() => ReadLoopAsync(_cts.Token));
        _processorTask = Task.Run(() => ProcessLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        await _cts.CancelAsync().ConfigureAwait(false);
        foreach (var task in new[] { _readerTask, _processorTask })
        {
            if (task is null) continue;
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
        _cts.Dispose();
        _cts = null;
        _readerTask = _processorTask = null;
    }

    public async Task<Result<int>> SendAsync(byte[] payload, CancellationToken ct)
    {
        var written = await _transport.WriteAsync(payload, ct).ConfigureAwait(false);
        if (!written.IsOk) return written;

        Interlocked.Add(ref _txBytes, payload.Length);
        BytesFlowed?.Invoke(payload, FrameDirection.Tx, Time.Now);
        FramesReady?.Invoke([_core.EnrichTx(payload)]);
        return written;
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (!ct.IsCancellationRequested)
        {
            var read = await _transport.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (!read.IsOk)
            {
                TransportError?.Invoke(read.Error);
                return;
            }
            if (read.Value == 0)
            {
                if (ct.IsCancellationRequested) return;
                // Transport closed from the far side.
                TransportError?.Invoke("Connection closed.");
                return;
            }

            byte[] chunk = buffer.AsSpan(0, read.Value).ToArray();
            Interlocked.Add(ref _rxBytes, chunk.Length);
            BytesFlowed?.Invoke(chunk, FrameDirection.Rx, Time.Now);
            _queue.Enqueue(chunk);
        }
    }

    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_batchMs));
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            var batch = DrainOnce();
            if (batch.Count > 0) FramesReady?.Invoke(batch);
        }
    }

    /// <summary>One processor cadence: drain the queue, then flush time-based framers.</summary>
    public List<FrameRecord> DrainOnce()
    {
        var batch = new List<FrameRecord>();
        while (_queue.TryDequeue(out var chunk))
        {
            batch.AddRange(_core.ProcessRx(chunk));
        }
        batch.AddRange(_core.FlushTimeouts());
        return batch;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
