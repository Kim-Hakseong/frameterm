using System.Text;
using Ft.Core.Checksum;

namespace Ft.Core.Compose;

/// <summary>
/// Turns a compose expression into wire bytes. Syntax: whitespace-separated
/// hex bytes (<c>A5 01</c>), double-quoted ASCII literals (<c>"CMD"</c>) and
/// placeholders: <c>{len}</c>, <c>{len+n}</c>, <c>{crc16}</c> (CRC-16/MODBUS,
/// little-endian), <c>{crc:PRESET}</c>, <c>{sum8}</c>, <c>{xor8}</c>.
/// Evaluation order: literal expansion → len substitution → checksum
/// computation. <c>{len}</c> is the total payload length excluding checksum
/// bytes; checksums cover every non-checksum byte.
/// </summary>
public static class PayloadComposer
{
    private abstract record Token;
    private sealed record LiteralToken(byte[] Bytes) : Token;
    private sealed record LenToken(int Adjust) : Token;
    private sealed record ChecksumToken(ChecksumSpec Spec, ByteOrder Order) : Token;

    public static Result<byte[]> Compose(string expression)
    {
        var tokensResult = Tokenize(expression);
        if (!tokensResult.IsOk) return Result<byte[]>.Fail(tokensResult.Error);
        var tokens = tokensResult.Value;

        // Pass 1: layout — resolve lengths of every token.
        int nonChecksumLength = 0;
        int totalLength = 0;
        foreach (var token in tokens)
        {
            int size = token switch
            {
                LiteralToken lit => lit.Bytes.Length,
                LenToken => 1,
                ChecksumToken ck => ck.Spec.ByteCount,
                _ => 0,
            };
            totalLength += size;
            if (token is not ChecksumToken) nonChecksumLength += size;
        }

        // Pass 2: materialize literals and len bytes; remember checksum slots.
        var payload = new byte[totalLength];
        var checksumSlots = new List<(int Position, ChecksumToken Token)>();
        var coverage = new List<byte>(nonChecksumLength);
        int pos = 0;
        foreach (var token in tokens)
        {
            switch (token)
            {
                case LiteralToken lit:
                    lit.Bytes.CopyTo(payload, pos);
                    coverage.AddRange(lit.Bytes);
                    pos += lit.Bytes.Length;
                    break;
                case LenToken len:
                    payload[pos] = (byte)(nonChecksumLength + len.Adjust);
                    coverage.Add(payload[pos]);
                    pos += 1;
                    break;
                case ChecksumToken ck:
                    checksumSlots.Add((pos, ck));
                    pos += ck.Spec.ByteCount;
                    break;
            }
        }

        // Pass 3: checksums over all non-checksum bytes.
        foreach (var (position, token) in checksumSlots)
        {
            uint value = ChecksumEngine.Compute(token.Spec, coverage.ToArray());
            ChecksumEngine.ToBytes(token.Spec, value, token.Order).CopyTo(payload, position);
        }

        return Result<byte[]>.Ok(payload);
    }

    private static Result<List<Token>> Tokenize(string expression)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < expression.Length)
        {
            char c = expression[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '"')
            {
                int close = expression.IndexOf('"', i + 1);
                if (close < 0) return Result<List<Token>>.Fail("Unterminated string literal.");
                string text = expression[(i + 1)..close];
                tokens.Add(new LiteralToken(Encoding.ASCII.GetBytes(text)));
                i = close + 1;
                continue;
            }

            if (c == '{')
            {
                int close = expression.IndexOf('}', i + 1);
                if (close < 0) return Result<List<Token>>.Fail("Unterminated placeholder.");
                string body = expression[(i + 1)..close].Trim();
                var tokenResult = ParsePlaceholder(body);
                if (!tokenResult.IsOk) return Result<List<Token>>.Fail(tokenResult.Error);
                tokens.Add(tokenResult.Value);
                i = close + 1;
                continue;
            }

            // Hex byte token up to the next whitespace/quote/brace.
            int end = i;
            while (end < expression.Length && !char.IsWhiteSpace(expression[end])
                   && expression[end] != '"' && expression[end] != '{')
            {
                end++;
            }
            string hex = expression[i..end];
            if (hex.Length is not (1 or 2) || !byte.TryParse(
                hex, System.Globalization.NumberStyles.HexNumber, null, out byte value))
            {
                return Result<List<Token>>.Fail($"Invalid hex byte '{hex}'.");
            }
            tokens.Add(new LiteralToken([value]));
            i = end;
        }

        if (tokens.Count == 0) return Result<List<Token>>.Fail("Expression is empty.");
        return Result<List<Token>>.Ok(MergeLiterals(tokens));
    }

    private static Result<Token> ParsePlaceholder(string body)
    {
        string lower = body.ToLowerInvariant();

        if (lower == "len") return Result<Token>.Ok(new LenToken(0));
        if (lower.StartsWith("len+", StringComparison.Ordinal) &&
            int.TryParse(body[4..], out int plus))
        {
            return Result<Token>.Ok(new LenToken(plus));
        }
        if (lower.StartsWith("len-", StringComparison.Ordinal) &&
            int.TryParse(body[4..], out int minus))
        {
            return Result<Token>.Ok(new LenToken(-minus));
        }

        switch (lower)
        {
            case "crc16":
                return Result<Token>.Ok(new ChecksumToken(ChecksumPresets.Crc16Modbus, ByteOrder.Little));
            case "sum8":
                return Result<Token>.Ok(new ChecksumToken(ChecksumPresets.Sum8, ByteOrder.Little));
            case "xor8":
                return Result<Token>.Ok(new ChecksumToken(ChecksumPresets.Xor8, ByteOrder.Little));
        }

        if (lower.StartsWith("crc:", StringComparison.Ordinal))
        {
            string presetName = body[4..].Trim();
            if (!ChecksumPresets.ByName.TryGetValue(presetName, out var spec))
            {
                return Result<Token>.Fail($"Unknown checksum preset '{presetName}'.");
            }
            return Result<Token>.Ok(new ChecksumToken(spec, ByteOrder.Little));
        }

        return Result<Token>.Fail($"Unknown placeholder '{{{body}}}'.");
    }

    private static List<Token> MergeLiterals(List<Token> tokens)
    {
        var merged = new List<Token>();
        var pending = new List<byte>();
        foreach (var token in tokens)
        {
            if (token is LiteralToken lit)
            {
                pending.AddRange(lit.Bytes);
                continue;
            }
            if (pending.Count > 0)
            {
                merged.Add(new LiteralToken([.. pending]));
                pending.Clear();
            }
            merged.Add(token);
        }
        if (pending.Count > 0) merged.Add(new LiteralToken([.. pending]));
        return merged;
    }
}
