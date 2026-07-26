using Ft.Core.Framing;
using Ft.Core.Parsing;
using Ft.Core.Time;

namespace Ft.Core.Pipeline;

/// <summary>
/// Synchronous heart of the pipeline: bytes → frames → checksum → fields →
/// highlight color. No threads or timers here, so it tests deterministically;
/// RxPipeline provides the async plumbing around it.
/// </summary>
public sealed class PipelineCore(PipelineConfig config, ITimeSource time)
{
    private long _seq;

    public PipelineConfig Config { get; } = config;

    public List<FrameRecord> ProcessRx(ReadOnlySpan<byte> data)
    {
        if (Config.Framer is null) return [];
        return Enrich(Config.Framer.Push(data), FrameDirection.Rx);
    }

    /// <summary>Emit any frame closed by elapsed silence (time-based framers).</summary>
    public List<FrameRecord> FlushTimeouts()
    {
        if (Config.Framer is null) return [];
        return Enrich(Config.Framer.Flush(), FrameDirection.Rx);
    }

    public FrameRecord EnrichTx(byte[] payload) => EnrichOne(payload, FrameDirection.Tx);

    private List<FrameRecord> Enrich(IReadOnlyList<RawFrame> frames, FrameDirection dir)
    {
        var records = new List<FrameRecord>(frames.Count);
        foreach (var frame in frames)
        {
            records.Add(EnrichOne(frame.Bytes, dir));
        }
        return records;
    }

    private FrameRecord EnrichOne(byte[] raw, FrameDirection dir)
    {
        bool? checksumOk = null;
        if (Config is { ChecksumSpec: not null, ChecksumPlacement: not null })
        {
            var verdict = Config.ChecksumPlacement.Verify(Config.ChecksumSpec, raw);
            checksumOk = verdict.IsOk ? verdict.Value : null;
        }

        var fields = FieldParser.Parse(Config.Fields, raw);
        string? color = RuleEvaluator.Evaluate(Config.Highlights, raw, fields);

        return new FrameRecord
        {
            Seq = Interlocked.Increment(ref _seq),
            Timestamp = time.Now,
            Direction = dir,
            Raw = raw,
            ChecksumOk = checksumOk,
            Fields = fields,
            Color = color,
        };
    }
}
