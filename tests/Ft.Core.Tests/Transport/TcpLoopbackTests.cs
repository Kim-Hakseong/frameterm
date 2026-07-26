using Ft.Core.Checksum;
using Ft.Core.Framing;
using Ft.Core.Pipeline;
using Ft.Core.Transport;
using Xunit;

namespace Ft.Core.Tests.Transport;

/// <summary>DESIGN §8.4: TCP echo server ↔ client, 100 frames, order/content intact, 0 drops.</summary>
public class TcpLoopbackTests
{
    [Fact]
    public async Task HundredFrames_EchoRoundTrip_OrderedNoDrops()
    {
        await using var server = new TcpServerTransport(port: 0, echo: true);
        var opened = await server.OpenAsync(CancellationToken.None);
        Assert.True(opened.IsOk);
        // Server reads drive the echo; pump them in the background.
        using var serverCts = new CancellationTokenSource();
        var serverPump = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            while (!serverCts.IsCancellationRequested)
            {
                var read = await server.ReadAsync(buffer, serverCts.Token);
                if (!read.IsOk || read.Value == 0) return;
            }
        });

        await using var client = new TcpClientTransport("127.0.0.1", server.BoundPort);
        var connected = await client.OpenAsync(CancellationToken.None);
        Assert.True(connected.IsOk);

        var config = new PipelineConfig
        {
            // Demo protocol framing: total = len byte + 2.
            Framer = new LengthFieldFramer(2, 1, 1, ByteOrder.Little, 2),
            ChecksumSpec = ChecksumPresets.Crc16Modbus,
            ChecksumPlacement = new ChecksumPlacement(2, ByteOrder.Little, 0, 2),
        };
        await using var pipeline = new RxPipeline(client, config, batchMs: 10);

        const int frameCount = 100;
        var received = new List<FrameRecord>();
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pipeline.FramesReady += batch =>
        {
            lock (received)
            {
                received.AddRange(batch.Where(r => r.Direction == FrameDirection.Rx));
                if (received.Count >= frameCount) done.TrySetResult(true);
            }
        };
        pipeline.Start();

        var sentFrames = new List<byte[]>(frameCount);
        for (int i = 0; i < frameCount; i++)
        {
            byte[] body = [0xA5, 0x06, (byte)i, (byte)(i >> 8), 0x11, 0x22];
            uint crc = ChecksumEngine.Compute(ChecksumPresets.Crc16Modbus, body);
            byte[] frame = [.. body, .. ChecksumEngine.ToBytes(ChecksumPresets.Crc16Modbus, crc, ByteOrder.Little)];
            sentFrames.Add(frame);
            var sent = await pipeline.SendAsync(frame, CancellationToken.None);
            Assert.True(sent.IsOk);
        }

        await done.Task.WaitAsync(TimeSpan.FromSeconds(15));
        lock (received)
        {
            Assert.Equal(frameCount, received.Count);
            for (int i = 0; i < frameCount; i++)
            {
                Assert.Equal(sentFrames[i], received[i].Raw);
                Assert.True(received[i].ChecksumOk);
            }
        }
        Assert.Equal(0, pipeline.DropCount);

        await serverCts.CancelAsync();
        await pipeline.StopAsync();
        await serverPump.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ClientConnectToClosedPort_FailsGracefully()
    {
        await using var listener = new TcpServerTransport(port: 0);
        await listener.OpenAsync(CancellationToken.None);
        int deadPort = listener.BoundPort;
        await listener.CloseAsync();

        await using var client = new TcpClientTransport("127.0.0.1", deadPort);
        var connected = await client.OpenAsync(CancellationToken.None);
        Assert.False(connected.IsOk);
    }
}
