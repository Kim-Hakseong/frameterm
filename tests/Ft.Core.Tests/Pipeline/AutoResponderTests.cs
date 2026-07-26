using Ft.Core.Parsing;
using Ft.Core.Pipeline;
using Ft.Core.Tests.TestUtil;
using Xunit;

namespace Ft.Core.Tests.Pipeline;

public class AutoResponderTests
{
    private static FrameRecord RxFrame(byte[] raw) => new()
    {
        Seq = 1,
        Timestamp = DateTimeOffset.UnixEpoch,
        Direction = FrameDirection.Rx,
        Raw = raw,
    };

    [Fact]
    public async Task MatchingRxFrame_TriggersComposedResponse()
    {
        var sent = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var responder = new AutoResponder(
            [new AutoRespondRule
            {
                Pattern = BytePattern.Parse("A5 01").Value,
                ResponseExpression = "06 \"ACK\"",
            }],
            payload =>
            {
                sent.TrySetResult(payload);
                return Task.CompletedTask;
            });

        responder.HandleFrames([RxFrame(Hex.Bytes("A5 01 99"))]);
        var payload = await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Hex.Bytes("06 41 43 4B"), payload);
    }

    [Fact]
    public void NonMatching_And_TxFrames_Ignored()
    {
        int calls = 0;
        var responder = new AutoResponder(
            [new AutoRespondRule
            {
                Pattern = BytePattern.Parse("A5 01").Value,
                ResponseExpression = "06",
            }],
            _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            });

        responder.HandleFrames([RxFrame(Hex.Bytes("B0 01"))]);
        responder.HandleFrames([new FrameRecord
        {
            Seq = 2,
            Timestamp = DateTimeOffset.UnixEpoch,
            Direction = FrameDirection.Tx,
            Raw = Hex.Bytes("A5 01"),
        }]);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task FirstMatchingRuleWins()
    {
        var sent = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var responder = new AutoResponder(
            [
                new AutoRespondRule { Pattern = BytePattern.Parse("A5 ??").Value, ResponseExpression = "01" },
                new AutoRespondRule { Pattern = BytePattern.Parse("A5 01").Value, ResponseExpression = "02" },
            ],
            payload =>
            {
                sent.TrySetResult(payload);
                return Task.CompletedTask;
            });

        responder.HandleFrames([RxFrame(Hex.Bytes("A5 01"))]);
        Assert.Equal(new byte[] { 0x01 }, await sent.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task BadResponseExpression_RaisesComposeFailed()
    {
        var failed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var responder = new AutoResponder(
            [new AutoRespondRule
            {
                Pattern = BytePattern.Parse("A5").Value,
                ResponseExpression = "{bogus}",
            }],
            _ => Task.CompletedTask);
        responder.ComposeFailed += message => failed.TrySetResult(message);

        responder.HandleFrames([RxFrame(Hex.Bytes("A5"))]);
        Assert.NotEmpty(await failed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
