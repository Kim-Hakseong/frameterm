using System.Text;
using Avalonia.Media;
using Ft.Core.Pipeline;

namespace Ft.App.ViewModels;

/// <summary>Frame-list row: preformatted columns + status/highlight brushes.</summary>
public sealed class FrameRecordViewModel
{
    private static readonly IBrush OkBrush = new SolidColorBrush(Color.Parse("#201F1C"));
    private static readonly IBrush FailBrush = new SolidColorBrush(Color.Parse("#D70027"));
    private static readonly IBrush NoneBrush = new SolidColorBrush(Color.Parse("#6F6E66"));
    private static readonly IBrush RxBrush = new SolidColorBrush(Color.Parse("#1D4ED8"));
    private static readonly IBrush TxBrush = new SolidColorBrush(Color.Parse("#C85A3E"));
    private static readonly IBrush Transparent = new SolidColorBrush(Colors.Transparent);

    public FrameRecord Record { get; }
    public string Time { get; }
    public string Dir { get; }
    public int Len { get; }
    public string ChecksumText { get; }
    public IBrush ChecksumBrush { get; }
    public string FieldsSummary { get; }
    public string RawPreview { get; }
    public IBrush DirBrush { get; }
    public IBrush HighlightBrush { get; }

    public FrameRecordViewModel(FrameRecord record)
    {
        Record = record;
        Time = record.Timestamp.ToString("HH:mm:ss.fff");
        Dir = record.Direction == FrameDirection.Rx ? "RX" : "TX";
        DirBrush = record.Direction == FrameDirection.Rx ? RxBrush : TxBrush;
        Len = record.Raw.Length;
        (ChecksumText, ChecksumBrush) = record.ChecksumOk switch
        {
            true => ("OK", OkBrush),
            false => ("FAIL", FailBrush),
            null => ("—", NoneBrush),
        };

        var summary = new StringBuilder();
        foreach (var field in record.Fields)
        {
            if (summary.Length > 0) summary.Append("  ");
            summary.Append(field.Name).Append('=').Append(field.Display);
        }
        FieldsSummary = summary.ToString();

        const int previewBytes = 20;
        var preview = new StringBuilder(previewBytes * 3 + 1);
        for (int i = 0; i < Math.Min(record.Raw.Length, previewBytes); i++)
        {
            if (i > 0) preview.Append(' ');
            preview.Append(record.Raw[i].ToString("X2"));
        }
        if (record.Raw.Length > previewBytes) preview.Append('…');
        RawPreview = preview.ToString();

        HighlightBrush = TryParseColor(record.Color) ?? Transparent;
    }

    private static IBrush? TryParseColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        try
        {
            return new SolidColorBrush(Color.Parse(hex));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>Name/value pair for the selected-frame field table.</summary>
public sealed record FieldDisplay(string Name, string Value);
