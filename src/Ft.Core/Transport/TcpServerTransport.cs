using System.Net;
using System.Net.Sockets;

namespace Ft.Core.Transport;

/// <summary>
/// Single-client TCP server transport. OpenAsync starts listening; reads
/// wait for the first client to connect. Optional echo mode reflects every
/// received byte back to the sender (used by the loopback tests and as a
/// quick device stand-in).
/// </summary>
public sealed class TcpServerTransport(int port, bool echo = false) : ITransport
{
    private readonly int _requestedPort = port;
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _clientReady = new(0, 1);

    public string Description => $"tcp-listen://{BoundPort}";
    public bool IsOpen => _listener is not null;

    /// <summary>Actual bound port (use with port 0 for ephemeral).</summary>
    public int BoundPort { get; private set; }

    public Task<Result<bool>> OpenAsync(CancellationToken ct)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, _requestedPort);
            _listener.Start();
            BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = AcceptLoopAsync(ct);
            return Task.FromResult(Result<bool>.Ok(true));
        }
        catch (SocketException ex)
        {
            return Task.FromResult(Result<bool>.Fail($"Listen failed: {ex.Message}"));
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            var client = await _listener!.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            client.NoDelay = true;
            _client = client;
            _stream = client.GetStream();
            _clientReady.Release();
        }
        catch (Exception)
        {
            // Listener disposed or cancelled — nothing to accept.
        }
    }

    public Task CloseAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _listener?.Stop();
        _stream = null;
        _client = null;
        _listener = null;
        return Task.CompletedTask;
    }

    public async Task<Result<int>> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_stream is not { } stream) return Result<int>.Fail("No client connected.");
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
        try
        {
            await _clientReady.WaitAsync(ct).ConfigureAwait(false);
            _clientReady.Release(); // stay signaled for subsequent reads
        }
        catch (OperationCanceledException)
        {
            return Result<int>.Ok(0);
        }

        if (_stream is not { } stream) return Result<int>.Fail("No client connected.");
        try
        {
            int n = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (n > 0 && echo)
            {
                await stream.WriteAsync(buffer[..n], ct).ConfigureAwait(false);
            }
            return Result<int>.Ok(n);
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
