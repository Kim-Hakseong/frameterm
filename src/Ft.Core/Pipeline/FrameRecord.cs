using Ft.Core.Parsing;

namespace Ft.Core.Pipeline;

public enum FrameDirection
{
    Rx,
    Tx,
}

/// <summary>One framed, verified, parsed unit ready for display/logging.</summary>
public sealed class FrameRecord
{
    public required long Seq { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required FrameDirection Direction { get; init; }
    public required byte[] Raw { get; init; }

    /// <summary>null = no checksum configured or frame too short to verify.</summary>
    public bool? ChecksumOk { get; init; }

    public IReadOnlyList<FieldValue> Fields { get; init; } = [];

    /// <summary>Hex color from the first matching highlight rule, or null.</summary>
    public string? Color { get; init; }
}
