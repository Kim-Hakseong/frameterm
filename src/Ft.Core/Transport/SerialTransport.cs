using System.IO.Ports;

namespace Ft.Core.Transport;

/// <summary>
/// System.IO.Ports-backed transport. Reads run on the port's BaseStream so
/// they are truly async; hot-unplug surfaces as an error Result, never an
/// unhandled exception.
/// </summary>
public sealed class SerialTransport(SerialSettings settings) : ITransport
{
    private SerialPort? _port;

    public SerialSettings Settings { get; } = settings;
    public string Description => $"{Settings.PortName} @ {Settings.BaudRate}";
    public bool IsOpen => _port?.IsOpen ?? false;

    public static string[] GetPortNames()
    {
        try
        {
            return SerialPort.GetPortNames();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public Task<Result<bool>> OpenAsync(CancellationToken ct)
    {
        try
        {
            var port = new SerialPort(Settings.PortName, Settings.BaudRate, Settings.Parity,
                Settings.DataBits, Settings.StopBits)
            {
                Handshake = Settings.Handshake,
                DtrEnable = Settings.DtrEnable,
                RtsEnable = Settings.RtsEnable,
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = 2000,
            };
            port.Open();
            _port = port;
            return Task.FromResult(Result<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<bool>.Fail($"Open failed: {ex.Message}"));
        }
    }

    public Task CloseAsync()
    {
        try
        {
            _port?.Close();
        }
        catch (Exception)
        {
            // Port may already be gone (hot unplug); closing is best-effort.
        }
        _port = null;
        return Task.CompletedTask;
    }

    public void SetDtr(bool enabled)
    {
        if (_port is { IsOpen: true } p) p.DtrEnable = enabled;
    }

    public void SetRts(bool enabled)
    {
        if (_port is { IsOpen: true } p) p.RtsEnable = enabled;
    }

    public async Task<Result<int>> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_port is not { IsOpen: true } port) return Result<int>.Fail("Port is not open.");
        try
        {
            await port.BaseStream.WriteAsync(data, ct).ConfigureAwait(false);
            return Result<int>.Ok(data.Length);
        }
        catch (OperationCanceledException)
        {
            return Result<int>.Fail("Write cancelled.");
        }
        catch (Exception ex)
        {
            return Result<int>.Fail($"Write failed: {ex.Message}");
        }
    }

    public async Task<Result<int>> ReadAsync(Memory<byte> buffer, CancellationToken ct)
    {
        if (_port is not { IsOpen: true } port) return Result<int>.Fail("Port is not open.");
        try
        {
            int n = await port.BaseStream.ReadAsync(buffer, ct).ConfigureAwait(false);
            return Result<int>.Ok(n);
        }
        catch (OperationCanceledException)
        {
            return Result<int>.Ok(0);
        }
        catch (Exception ex)
        {
            return Result<int>.Fail($"Read failed: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);
}
