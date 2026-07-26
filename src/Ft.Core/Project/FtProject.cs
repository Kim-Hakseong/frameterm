using Ft.Core.Checksum;
using Ft.Core.Framing;
using Ft.Core.Parsing;
using Ft.Core.Pipeline;
using Ft.Core.Time;

namespace Ft.Core.Project;

/// <summary>
/// Root project model: everything a session needs (transport, framing,
/// checksum, fields, highlights, macros). Serialized as .ftproj JSON (M8);
/// BuildPipelineConfig turns it into runtime pipeline pieces.
/// </summary>
public sealed class FtProject
{
    public int SchemaVersion { get; set; } = 1;
    public TransportConfig Transport { get; set; } = new();
    public FramingConfig Framing { get; set; } = new();
    public ChecksumConfig Checksum { get; set; } = new();
    public List<FieldConfig> Fields { get; set; } = [];
    public List<HighlightConfig> Highlights { get; set; } = [];
    public List<MacroConfig> Macros { get; set; } = [];
    public List<AutoRespondConfig> AutoResponds { get; set; } = [];

    /// <summary>Build runtime auto-respond rules; Fail on the first invalid rule.</summary>
    public Result<List<AutoRespondRule>> BuildAutoRespondRules()
    {
        var rules = new List<AutoRespondRule>();
        foreach (var config in AutoResponds)
        {
            var built = config.Build();
            if (!built.IsOk) return Result<List<AutoRespondRule>>.Fail(built.Error);
            rules.Add(built.Value);
        }
        return Result<List<AutoRespondRule>>.Ok(rules);
    }

    /// <summary>Build runtime pipeline config; Fail lists the first invalid setting.</summary>
    public Result<PipelineConfig> BuildPipelineConfig(ITimeSource time)
    {
        var framer = Framing.BuildFramer(time);
        if (!framer.IsOk) return Result<PipelineConfig>.Fail(framer.Error);

        ChecksumSpec? spec = null;
        ChecksumPlacement? placement = null;
        if (!string.IsNullOrEmpty(Checksum.Preset) &&
            !Checksum.Preset.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            if (!ChecksumPresets.ByName.TryGetValue(Checksum.Preset, out spec))
            {
                return Result<PipelineConfig>.Fail($"Unknown checksum preset '{Checksum.Preset}'.");
            }
            placement = new ChecksumPlacement(
                Checksum.OffsetFromEnd,
                Checksum.ByteOrder.Equals("BE", StringComparison.OrdinalIgnoreCase) ? ByteOrder.Big : ByteOrder.Little,
                Checksum.CoverageStart,
                Checksum.CoverageEndOffsetFromEnd);
        }

        var fields = new List<FieldSpec>();
        foreach (var field in Fields)
        {
            var built = field.Build();
            if (!built.IsOk) return Result<PipelineConfig>.Fail(built.Error);
            fields.Add(built.Value);
        }

        var highlights = new List<HighlightRule>();
        foreach (var highlight in Highlights)
        {
            var built = highlight.Build();
            if (!built.IsOk) return Result<PipelineConfig>.Fail(built.Error);
            highlights.Add(built.Value);
        }

        return Result<PipelineConfig>.Ok(new PipelineConfig
        {
            Framer = framer.Value,
            ChecksumSpec = spec,
            ChecksumPlacement = placement,
            Fields = fields,
            Highlights = highlights,
        });
    }
}

public sealed class TransportConfig
{
    public string Type { get; set; } = "Serial";
    public string Port { get; set; } = string.Empty;
    public int Baud { get; set; } = 115200;
    public string Parity { get; set; } = "None";
    public int DataBits { get; set; } = 8;
    public string StopBits { get; set; } = "1";
    public string FlowControl { get; set; } = "None";
    public string Host { get; set; } = "127.0.0.1";
    public int TcpPort { get; set; } = 5000;
}

public sealed class FramingConfig
{
    /// <summary>None | Delimiter | FixedLength | LengthField | SilenceGap.</summary>
    public string Mode { get; set; } = "None";

    // Delimiter
    public string StartHex { get; set; } = string.Empty;
    public string EndHex { get; set; } = string.Empty;
    public string EscapeHex { get; set; } = string.Empty;

    // FixedLength
    public int Length { get; set; } = 8;

    // LengthField
    public int HeaderLen { get; set; } = 2;
    public int LenOffset { get; set; } = 1;
    public int LenSize { get; set; } = 1;
    public string Endian { get; set; } = "LE";
    public int LenAdjust { get; set; }

    // SilenceGap
    public int GapMs { get; set; } = 20;

    public int MaxFrame { get; set; } = 4096;

    public Result<IFramer?> BuildFramer(ITimeSource time)
    {
        try
        {
            switch (Mode.ToLowerInvariant())
            {
                case "none" or "":
                    return Result<IFramer?>.Ok(null);
                case "delimiter":
                {
                    var end = ParseHex(EndHex);
                    if (!end.IsOk) return Result<IFramer?>.Fail($"End sequence: {end.Error}");
                    if (end.Value.Length == 0) return Result<IFramer?>.Fail("End sequence is required.");
                    byte[]? start = null;
                    if (!string.IsNullOrWhiteSpace(StartHex))
                    {
                        var parsed = ParseHex(StartHex);
                        if (!parsed.IsOk) return Result<IFramer?>.Fail($"Start sequence: {parsed.Error}");
                        start = parsed.Value;
                    }
                    byte? escape = null;
                    if (!string.IsNullOrWhiteSpace(EscapeHex))
                    {
                        var parsed = ParseHex(EscapeHex);
                        if (!parsed.IsOk || parsed.Value.Length != 1)
                        {
                            return Result<IFramer?>.Fail("Escape must be a single hex byte.");
                        }
                        escape = parsed.Value[0];
                    }
                    return Result<IFramer?>.Ok(new DelimiterFramer(start, end.Value, escape, MaxFrame));
                }
                case "fixedlength":
                    return Result<IFramer?>.Ok(new FixedLengthFramer(Length));
                case "lengthfield":
                    return Result<IFramer?>.Ok(new LengthFieldFramer(
                        HeaderLen, LenOffset, LenSize,
                        Endian.Equals("BE", StringComparison.OrdinalIgnoreCase) ? ByteOrder.Big : ByteOrder.Little,
                        LenAdjust, MaxFrame));
                case "silencegap":
                    return Result<IFramer?>.Ok(new SilenceGapFramer(GapMs, time));
                default:
                    return Result<IFramer?>.Fail($"Unknown framing mode '{Mode}'.");
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Result<IFramer?>.Fail($"Invalid framing parameter: {ex.ParamName}");
        }
        catch (ArgumentException ex)
        {
            return Result<IFramer?>.Fail($"Invalid framing parameter: {ex.Message}");
        }
    }

    internal static Result<byte[]> ParseHex(string text)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].Length is not (1 or 2) || !byte.TryParse(
                tokens[i], System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
            {
                return Result<byte[]>.Fail($"'{tokens[i]}' is not a hex byte.");
            }
        }
        return Result<byte[]>.Ok(bytes);
    }
}

public sealed class ChecksumConfig
{
    /// <summary>Preset id from ChecksumPresets.ByName, or "None".</summary>
    public string Preset { get; set; } = "None";
    public int OffsetFromEnd { get; set; } = 2;
    public string ByteOrder { get; set; } = "LE";
    public int CoverageStart { get; set; }
    public int CoverageEndOffsetFromEnd { get; set; } = 2;
}

public sealed class FieldConfig
{
    public string Name { get; set; } = string.Empty;
    public int Offset { get; set; }
    /// <summary>u8 s8 u16 s16 u32 s32 f32.</summary>
    public string Type { get; set; } = "u8";
    public string Endian { get; set; } = "LE";

    public Result<FieldSpec> Build()
    {
        if (string.IsNullOrWhiteSpace(Name)) return Result<FieldSpec>.Fail("Field name is required.");
        FieldType? type = Type.ToLowerInvariant() switch
        {
            "u8" => FieldType.U8,
            "s8" => FieldType.S8,
            "u16" => FieldType.U16,
            "s16" => FieldType.S16,
            "u32" => FieldType.U32,
            "s32" => FieldType.S32,
            "f32" => FieldType.F32,
            _ => null,
        };
        if (type is null) return Result<FieldSpec>.Fail($"Field '{Name}': unknown type '{Type}'.");
        return Result<FieldSpec>.Ok(new FieldSpec(
            Name, Offset, type.Value,
            Endian.Equals("BE", StringComparison.OrdinalIgnoreCase)
                ? Checksum.ByteOrder.Big
                : Checksum.ByteOrder.Little));
    }
}

public sealed class HighlightConfig
{
    /// <summary>Byte pattern like "A5 ?? 01" — used when non-empty.</summary>
    public string Pattern { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    /// <summary>= != &gt; &lt;.</summary>
    public string Op { get; set; } = "=";
    public double Value { get; set; }
    public string Color { get; set; } = "#7A1020";

    public Result<HighlightRule> Build()
    {
        if (!string.IsNullOrWhiteSpace(Pattern))
        {
            var pattern = BytePattern.Parse(Pattern);
            if (!pattern.IsOk) return Result<HighlightRule>.Fail(pattern.Error);
            return Result<HighlightRule>.Ok(new HighlightRule(Color, pattern.Value));
        }
        if (string.IsNullOrWhiteSpace(Field))
        {
            return Result<HighlightRule>.Fail("Highlight needs a byte pattern or a field condition.");
        }
        FieldOp? op = Op switch
        {
            "=" or "==" => FieldOp.Eq,
            "!=" or "≠" => FieldOp.Ne,
            ">" => FieldOp.Gt,
            "<" => FieldOp.Lt,
            _ => null,
        };
        if (op is null) return Result<HighlightRule>.Fail($"Unknown operator '{Op}'.");
        return Result<HighlightRule>.Ok(new HighlightRule(Color, new FieldCondition(Field, op.Value, Value)));
    }
}

public sealed class MacroConfig
{
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Hotkey { get; set; } = string.Empty;
}

public sealed class AutoRespondConfig
{
    /// <summary>RX frame byte pattern, e.g. "A5 01 ??".</summary>
    public string Pattern { get; set; } = string.Empty;
    /// <summary>Composer expression to send back.</summary>
    public string Response { get; set; } = string.Empty;
    public int DelayMs { get; set; }

    public Result<AutoRespondRule> Build()
    {
        var pattern = BytePattern.Parse(Pattern);
        if (!pattern.IsOk) return Result<AutoRespondRule>.Fail($"Auto-respond pattern: {pattern.Error}");
        if (string.IsNullOrWhiteSpace(Response))
        {
            return Result<AutoRespondRule>.Fail("Auto-respond response expression is required.");
        }
        return Result<AutoRespondRule>.Ok(new AutoRespondRule
        {
            Pattern = pattern.Value,
            ResponseExpression = Response,
            DelayMs = DelayMs,
        });
    }
}
