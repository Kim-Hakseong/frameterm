using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;

namespace Ft.App.Tests;

/// <summary>Shared helpers for headless UI tests.</summary>
public static class UiTest
{
    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 10000)
    {
        int elapsed = 0;
        while (!condition() && elapsed < timeoutMs)
        {
            await Task.Delay(50);
            Dispatcher.UIThread.RunJobs();
            elapsed += 50;
        }
        Assert.True(condition(), "Condition not met within timeout.");
    }

    /// <summary>
    /// Close a window and flush every queued dispatcher job so no layout or
    /// render work is left to run during the headless session reset (it would
    /// throw once app services are gone).
    /// </summary>
    public static void FlushAndClose(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
