using System.Threading.Channels;

namespace Ft.Core.Transport;

/// <summary>
/// In-memory transport for demo mode and tests. Written bytes are echoed
/// back as received data (toggleable), and tests/demos can inject arbitrary
/// inbound bytes with <see cref="InjectReceive"/>.
/// </summary>
public sealed class EchoFakeTransport : ITransport
{
    private readonly Channel<byte[]> _incoming = Channel.CreateUnbounded<byte[]>();
    private byte[]? _leftover;

    public string Description => "Demo (echo)";
    public bool IsOpen { get; private set; }
    public bool EchoEnabled { get; set; } = true;

    public Task<Result<bool>> OpenAsync(CancellationToken ct)
    {
        IsOpen = true;
        return Task.FromResult(Result<bool>.Ok(true));
    }

    public Task CloseAsync()
    {
        IsOpen = false;
        _incoming.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public Task<Result<int>> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (!IsOpen) return Task.FromResult(Result<int>.Fail("Transport closed."));
        if (EchoEnabled) _incoming.Writer.TryWrite(data.ToArray());
        return Task.FromResult(Result<int>.Ok(data.Length));
    }

    /// <summary>Feed inbound bytes as if a device sent them.</summary>
    public void InjectReceive(byte[] data) => _incoming.Writer.TryWrite(data);

    public async Task<Result<int>> ReadAsync(Memory<byte> buffer, CancellationToken ct)
    {
        byte[]? chunk = _leftover;
        _leftover = null;
        if (chunk is null)
        {
            try
            {
                chunk = await _incoming.Reader.ReadAsync(ct).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                return Result<int>.Ok(0);
            }
            catch (OperationCanceledException)
            {
                return Result<int>.Ok(0);
            }
        }

        int n = Math.Min(chunk.Length, buffer.Length);
        chunk.AsSpan(0, n).CopyTo(buffer.Span);
        if (n < chunk.Length) _leftover = chunk[n..];
        return Result<int>.Ok(n);
    }

    public ValueTask DisposeAsync()
    {
        IsOpen = false;
        _incoming.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
