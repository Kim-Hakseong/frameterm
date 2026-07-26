using Ft.Core;

namespace Ft.Core.Parsing;

/// <summary>
/// Hex byte pattern with "??" wildcards, e.g. "A5 ?? 01". Matches when the
/// frame starts with the pattern (anchored at offset 0).
/// </summary>
public sealed class BytePattern
{
    private readonly byte[] _values;
    private readonly bool[] _wildcards;

    public string Text { get; }

    private BytePattern(string text, byte[] values, bool[] wildcards)
    {
        Text = text;
        _values = values;
        _wildcards = wildcards;
    }

    public static Result<BytePattern> Parse(string text)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return Result<BytePattern>.Fail("Pattern is empty.");
        }

        var values = new byte[tokens.Length];
        var wildcards = new bool[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i] == "??")
            {
                wildcards[i] = true;
            }
            else if (tokens[i].Length is 1 or 2 && byte.TryParse(
                tokens[i], System.Globalization.NumberStyles.HexNumber, null, out byte value))
            {
                values[i] = value;
            }
            else
            {
                return Result<BytePattern>.Fail($"Invalid pattern token '{tokens[i]}'.");
            }
        }
        return Result<BytePattern>.Ok(new BytePattern(text, values, wildcards));
    }

    public bool Matches(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < _values.Length) return false;
        for (int i = 0; i < _values.Length; i++)
        {
            if (!_wildcards[i] && frame[i] != _values[i]) return false;
        }
        return true;
    }
}
