using Ft.Core.Checksum;
using Ft.Core.Framing;
using Ft.Core.Parsing;

namespace Ft.Core.Pipeline;

/// <summary>
/// Everything the RX pipeline needs to turn raw bytes into FrameRecords.
/// Framer is optional — without one the app shows the raw dump only.
/// </summary>
public sealed class PipelineConfig
{
    public IFramer? Framer { get; init; }
    public ChecksumSpec? ChecksumSpec { get; init; }
    public ChecksumPlacement? ChecksumPlacement { get; init; }
    public IReadOnlyList<FieldSpec> Fields { get; init; } = [];
    public IReadOnlyList<HighlightRule> Highlights { get; init; } = [];
}
