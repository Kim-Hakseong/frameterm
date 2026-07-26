using System.Net.Sockets;

namespace Ft.Core.Transport;

/// <summary>TCP client transport — same framing/parsing pipeline over a socket.</summary>
public sealed class TcpClientTransport(string host, int port) : ITransport
{
    private TcpClient? _client;
    private NetworkStream? _stream;

    public string Description => $"tcp://{host}:{port}";
    public bool IsOpen => _client?.Connected ?? false;

    public async Task<Result<bool>> OpenAsync(CancellationToken ct)
    {
        try
        {
            var client = new TcpClient { NoDelay = true };
            await client.ConnectAsync(host, port, ct).ConfigureAwait(false);
            _client = client;
            _stream = client.GetStream();
            return Result<bool>.Ok(true);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return Result<bool>.Fail($"Connect failed: {ex.Message}");
        }
    }

    public Task CloseAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        return Task.CompletedTask;
    }

    public async Task<Result<int>> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_stream is not { } stream) return Result<int>.Fail("Not connected.");
        try
        {
            await stream.WriteAsync(data, ct).ConfigureAwait(false);
            return Result<int>.Ok(data.Length);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            return Result<int>.Fail($"Write failed: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return Result<int>.Fail("Write cancelled.");
        }
    }

    public async Task<Result<int>> ReadAsync(Memory<byte> buffer, CancellationToken ct)
    {
        if (_stream is not { } stream) return Result<int>.Fail("Not connected.");
        try
        {
            return Result<int>.Ok(await stream.ReadAsync(buffer, ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            return Result<int>.Ok(0);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            return Result<int>.Fail($"Read failed: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);
}
