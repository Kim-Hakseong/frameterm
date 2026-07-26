using Ft.Core.Checksum;
using Ft.Core.Transport;

namespace Ft.App.Services;

/// <summary>
/// Built-in sample protocol generator for demo mode: lets users experience
/// framing/checksum/fields without hardware. Frame layout (8 bytes):
/// A5 | len=06 | seq | temp s16 BE (0.1 °C) | status | CRC-16/MODBUS LE.
/// Every 10th frame is emitted with a corrupted CRC to demonstrate FAIL
/// highlighting. Values are computed, not canned.
/// </summary>
public sealed class DemoTraffic(EchoFakeTransport transport) : IDisposable
{
    private CancellationTokenSource? _cts;
    private int _seq;

    public int PeriodMs { get; init; } = 300;

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(PeriodMs));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                transport.InjectReceive(BuildFrame(_seq++));
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    /// <summary>Deterministic synthetic frame for a sequence number.</summary>
    public static byte[] BuildFrame(int seq)
    {
        // Temperature swings ±8.0 °C around 23.5 °C on a triangle wave.
        int phase = seq % 32;
        int delta = phase < 16 ? phase * 10 : (32 - phase) * 10;
        short temp = (short)(235 - 80 + delta);
        byte status = (byte)(seq % 7 == 0 ? 0x01 : 0x00);

        byte[] body =
        [
            0xA5, 0x06, (byte)seq,
            (byte)(temp >> 8), (byte)temp,
            status,
        ];
        uint crc = ChecksumEngine.Compute(ChecksumPresets.Crc16Modbus, body);
        if (seq % 10 == 9) crc ^= 0xFFFF; // deliberate corruption to show FAIL
        return [.. body, .. ChecksumEngine.ToBytes(ChecksumPresets.Crc16Modbus, crc, ByteOrder.Little)];
    }

    public void Dispose() => Stop();
}
