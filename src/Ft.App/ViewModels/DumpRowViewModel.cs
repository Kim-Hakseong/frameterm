using Avalonia.Media;
using Ft.Core.Dump;
using Ft.Core.Pipeline;

namespace Ft.App.ViewModels;

/// <summary>Display wrapper for a dump row: preformatted columns + direction brush.</summary>
public sealed class DumpRowViewModel(DumpRow row)
{
    private static readonly IBrush RxBrush = new SolidColorBrush(Color.Parse("#2C4A6E"));
    private static readonly IBrush TxBrush = new SolidColorBrush(Color.Parse("#7A1020"));

    public string Time { get; } = row.Time;
    public string Dir { get; } = row.Direction == FrameDirection.Rx ? "RX" : "TX";
    public string Offset { get; } = row.Offset.ToString("X8");
    public string Hex { get; } = row.Hex;
    public string Ascii { get; } = row.Ascii;
    public IBrush Brush { get; } = row.Direction == FrameDirection.Rx ? RxBrush : TxBrush;
}
