using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Ft.App.Views;
using Xunit;

namespace Ft.App.Tests;

/// <summary>
/// Not a test of behavior — renders the demo-mode main window to a PNG for
/// the README. Kept runnable so the marketing screenshot stays reproducible.
/// </summary>
public class ScreenshotCapture
{
    [AvaloniaFact]
    public async Task CaptureDemoModeScreenshot()
    {
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;

        await vm.ToggleDemoAsync();
        // Enough frames to fill the list, including at least one CRC FAIL row.
        await UiTest.WaitUntilAsync(
            () => vm.FrameRecords.Count >= 14 &&
                  vm.FrameRecords.Any(f => f.Record.ChecksumOk == false), 30000);

        // Select the CRC-FAIL frame so the red status row and its detail
        // (hex dump + field table) are both in the shot, and scroll to it.
        var failFrame = vm.FrameRecords.First(f => f.Record.ChecksumOk == false);
        vm.SelectedFrame = failFrame;
        vm.ComposeText = "A5 01 {len} \"CMD\" {crc16}";
        var grid = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window)
            .OfType<Avalonia.Controls.DataGrid>()
            .First();
        grid.ScrollIntoView(failFrame, null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        using (var stream = File.Create("/tmp/frameterm-screenshot.png"))
        {
            bitmap!.Save(stream);
        }

        await vm.DisconnectAsync();
        UiTest.FlushAndClose(window);
    }
}
