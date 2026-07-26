namespace Ft.Core.Parsing;

public enum FieldOp
{
    Eq,
    Ne,
    Gt,
    Lt,
}

public sealed record FieldCondition(string FieldName, FieldOp Op, double Value);

/// <summary>
/// A highlight rule: either a byte pattern or a field condition, mapped to a
/// color (hex string like "#7A1020"). Exactly one of Pattern/Condition is set.
/// </summary>
public sealed class HighlightRule
{
    public string Color { get; }
    public BytePattern? Pattern { get; }
    public FieldCondition? Condition { get; }

    public HighlightRule(string color, BytePattern pattern)
    {
        Color = color;
        Pattern = pattern;
    }

    public HighlightRule(string color, FieldCondition condition)
    {
        Color = color;
        Condition = condition;
    }
}
