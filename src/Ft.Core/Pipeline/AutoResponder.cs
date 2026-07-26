using Ft.Core.Compose;
using Ft.Core.Parsing;

namespace Ft.Core.Pipeline;

/// <summary>One auto-respond rule: RX frame pattern → composed reply after a delay.</summary>
public sealed class AutoRespondRule
{
    public required BytePattern Pattern { get; init; }
    public required string ResponseExpression { get; init; }
    public int DelayMs { get; init; }
}

/// <summary>
/// Watches RX frames and fires configured responses — device emulation and
/// handshake automation. Hook <see cref="HandleFrames"/> to the pipeline's
/// FramesReady event; responses go out through the provided send delegate.
/// </summary>
public sealed class AutoResponder(
    IReadOnlyList<AutoRespondRule> rules,
    Func<byte[], Task> sendAsync)
{
    /// <summary>Errors surfaced by response composition (bad expressions).</summary>
    public event Action<string>? ComposeFailed;

    public void HandleFrames(IReadOnlyList<FrameRecord> batch)
    {
        foreach (var record in batch)
        {
            if (record.Direction != FrameDirection.Rx) continue;
            foreach (var rule in rules)
            {
                if (!rule.Pattern.Matches(record.Raw)) continue;
                _ = RespondAsync(rule);
                break; // first matching rule wins
            }
        }
    }

    private async Task RespondAsync(AutoRespondRule rule)
    {
        var payload = PayloadComposer.Compose(rule.ResponseExpression);
        if (!payload.IsOk)
        {
            ComposeFailed?.Invoke(payload.Error);
            return;
        }
        if (rule.DelayMs > 0)
        {
            await Task.Delay(rule.DelayMs).ConfigureAwait(false);
        }
        await sendAsync(payload.Value).ConfigureAwait(false);
    }
}
