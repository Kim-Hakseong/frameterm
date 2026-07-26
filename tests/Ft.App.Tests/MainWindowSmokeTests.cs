using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Ft.App.Views;
using Xunit;

namespace Ft.App.Tests;

/// <summary>M5 smoke: shell opens, demo mode produces dump rows, disconnect works.</summary>
public class MainWindowSmokeTests
{
    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 10000)
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

    [AvaloniaFact]
    public void Window_Opens_WithDisconnectedState()
    {
        var window = new MainWindow();
        window.Show();
        Assert.False(window.ViewModel.IsConnected);
        Assert.Equal("Disconnected", window.ViewModel.ConnectionStatus);
        window.Close();
    }

    [AvaloniaFact]
    public async Task DemoMode_ProducesDumpRowsAndFrames()
    {
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;

        await vm.ToggleDemoAsync();
        Assert.True(vm.IsConnected);
        Assert.True(vm.IsDemoMode);

        await WaitUntilAsync(() => vm.DumpRows.Count > 0 || vm.PartialRow is not null);
        await WaitUntilAsync(() => vm.StatsText.Contains("frames") && !vm.StatsText.Contains("frames 0"));

        await vm.DisconnectAsync();
        Assert.False(vm.IsConnected);
        Assert.False(vm.IsDemoMode);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ClearDump_EmptiesView()
    {
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;

        await vm.ToggleDemoAsync();
        await WaitUntilAsync(() => vm.DumpRows.Count > 0 || vm.PartialRow is not null);
        await vm.DisconnectAsync();

        vm.ClearDump();
        Assert.Empty(vm.DumpRows);
        Assert.Null(vm.PartialRow);
        window.Close();
    }
}
