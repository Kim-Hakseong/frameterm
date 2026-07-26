using Ft.Core.Checksum;
using Ft.Core.Framing;
using Ft.Core.Pipeline;
using Ft.Core.Tests.TestUtil;
using Ft.Core.Transport;
using Xunit;

namespace Ft.Core.Tests.Pipeline;

/// <summary>M4 DoD: fake-stream round trip through the full async pipeline.</summary>
public class RxPipelineRoundTripTests
{
    private static PipelineConfig ModbusConfig() => new()
    {
        Framer = new FixedLengthFramer(8),
        ChecksumSpec = ChecksumPresets.Crc16Modbus,
        ChecksumPlacement = new ChecksumPlacement(2, ByteOrder.Little, 0, 2),
    };

    [Fact]
    public async Task EchoRoundTrip_TxAndRxRecordsArrive()
    {
        await using var transport = new EchoFakeTransport();
        await transport.OpenAsync(CancellationToken.None);
        await using var pipeline = new RxPipeline(transport, ModbusConfig(), batchMs: 10);

        var rxReceived = new TaskCompletionSource<FrameRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        var txReceived = new TaskCompletionSource<FrameRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        pipeline.FramesReady += batch =>
        {
            foreach (var record in batch)
            {
                var target = record.Direction == FrameDirection.Rx ? rxReceived : txReceived;
                target.TrySetResult(record);
            }
        };
        pipeline.Start();

        byte[] frame = Hex.Bytes("01 03 00 00 00 0A C5 CD");
        var sent = await pipeline.SendAsync(frame, CancellationToken.None);
        Assert.True(sent.IsOk);

        var tx = await txReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var rx = await rxReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(frame, tx.Raw);
        Assert.Equal(frame, rx.Raw);
        Assert.True(rx.ChecksumOk);
        Assert.Equal(8, pipeline.RxBytes);
        Assert.Equal(8, pipeline.TxBytes);
        Assert.Equal(0, pipeline.DropCount);
    }

    [Fact]
    public async Task InjectedBurst_AllFramesArriveInOrder()
    {
        await using var transport = new EchoFakeTransport();
        await transport.OpenAsync(CancellationToken.None);
        await using var pipeline = new RxPipeline(transport, ModbusConfig(), batchMs: 10);

        const int frameCount = 50;
        var all = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new List<FrameRecord>();
        pipeline.FramesReady += batch =>
        {
            lock (received)
            {
                received.AddRange(batch);
                if (received.Count >= frameCount) all.TrySetResult(true);
            }
        };
        pipeline.Start();

        for (int i = 0; i < frameCount; i++)
        {
            // Vary one payload byte per frame; CRC recomputed by composer path.
            byte[] payload = [0x01, 0x03, 0x00, (byte)i, 0x00, 0x0A];
            uint crc = ChecksumEngine.Compute(ChecksumPresets.Crc16Modbus, payload);
            byte[] frame = [.. payload, .. ChecksumEngine.ToBytes(ChecksumPresets.Crc16Modbus, crc, ByteOrder.Little)];
            transport.InjectReceive(frame);
        }

        await all.Task.WaitAsync(TimeSpan.FromSeconds(5));
        lock (received)
        {
            Assert.Equal(frameCount, received.Count);
            for (int i = 0; i < frameCount; i++)
            {
                Assert.Equal((byte)i, received[i].Raw[3]);
                Assert.True(received[i].ChecksumOk);
            }
        }
    }

    [Fact]
    public async Task EchoDisabled_NoRxRecords()
    {
        await using var transport = new EchoFakeTransport { EchoEnabled = false };
        await transport.OpenAsync(CancellationToken.None);
        await using var pipeline = new RxPipeline(transport, ModbusConfig(), batchMs: 10);
        pipeline.Start();

        var sent = await pipeline.SendAsync(Hex.Bytes("01 03 00 00 00 0A C5 CD"), CancellationToken.None);
        Assert.True(sent.IsOk);
        Assert.Equal(0, pipeline.RxBytes);
        await pipeline.StopAsync();
    }

    [Fact]
    public async Task PartialReads_SmallBuffer_LeftoverPreserved()
    {
        await using var transport = new EchoFakeTransport();
        await transport.OpenAsync(CancellationToken.None);
        transport.InjectReceive(Hex.Bytes("01 02 03 04 05"));

        var buffer = new byte[2];
        var collected = new List<byte>();
        for (int i = 0; i < 3; i++)
        {
            var read = await transport.ReadAsync(buffer, CancellationToken.None);
            Assert.True(read.IsOk);
            collected.AddRange(buffer.Take(read.Value));
        }
        Assert.Equal(Hex.Bytes("01 02 03 04 05"), collected);
    }
}
