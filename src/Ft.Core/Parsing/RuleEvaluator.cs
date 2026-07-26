namespace Ft.Core.Parsing;

/// <summary>Applies highlight rules in order; the first match wins.</summary>
public static class RuleEvaluator
{
    public static string? Evaluate(
        IReadOnlyList<HighlightRule> rules,
        ReadOnlySpan<byte> frame,
        IReadOnlyList<FieldValue> fields)
    {
        foreach (var rule in rules)
        {
            if (Matches(rule, frame, fields)) return rule.Color;
        }
        return null;
    }

    public static bool Matches(HighlightRule rule, ReadOnlySpan<byte> frame, IReadOnlyList<FieldValue> fields)
    {
        if (rule.Pattern is not null)
        {
            return rule.Pattern.Matches(frame);
        }

        var cond = rule.Condition!;
        FieldValue? field = null;
        foreach (var f in fields)
        {
            if (string.Equals(f.Name, cond.FieldName, StringComparison.OrdinalIgnoreCase))
            {
                field = f;
                break;
            }
        }
        if (field is null || !field.IsAvailable) return false;

        return cond.Op switch
        {
            FieldOp.Eq => field.Numeric == cond.Value,
            FieldOp.Ne => field.Numeric != cond.Value,
            FieldOp.Gt => field.Numeric > cond.Value,
            FieldOp.Lt => field.Numeric < cond.Value,
            _ => false,
        };
    }
}
