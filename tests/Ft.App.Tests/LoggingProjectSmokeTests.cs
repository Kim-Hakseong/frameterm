using Avalonia.Headless.XUnit;
using Ft.App.ViewModels;
using Ft.App.Views;
using Ft.Core.Project;
using Xunit;

namespace Ft.App.Tests;

/// <summary>
/// M8 smoke — PRD scenario D (log to file, filter error frames) and
/// scenario E (.ftproj save → restore full session config).
/// </summary>
public class LoggingProjectSmokeTests
{
    [AvaloniaFact]
    public async Task ScenarioD_LogFile_CapturesTraffic_AndErrorFilterWorks()
    {
        string logPath = Path.Combine(Path.GetTempPath(), $"ft-smoke-{Guid.NewGuid():N}.log");
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;
        try
        {
            await vm.ToggleDemoAsync();
            vm.StartLogging(logPath);
            Assert.True(vm.IsLogging);

            // Wait until at least one corrupted-CRC frame arrived (every 10th).
            await UiTest.WaitUntilAsync(
                () => vm.FrameRecords.Any(f => f.Record.ChecksumOk == false), 20000);

            // Errors-only filter: visible rows shrink to FAIL frames only.
            vm.FilterErrorsOnly = true;
            Assert.NotEmpty(vm.FrameRecords);
            Assert.All(vm.FrameRecords, f => Assert.False(f.Record.ChecksumOk));

            vm.FilterErrorsOnly = false;
            Assert.Contains(vm.FrameRecords, f => f.Record.ChecksumOk == true);

            await vm.StopLoggingAsync();
            Assert.False(vm.IsLogging);

            var lines = await File.ReadAllLinesAsync(logPath);
            Assert.NotEmpty(lines);
            Assert.All(lines, l => Assert.Contains(" RX ", l));

            await vm.DisconnectAsync();
        }
        finally
        {
            UiTest.FlushAndClose(window);
            File.Delete(logPath);
        }
    }

    [AvaloniaFact]
    public async Task PatternFilter_NarrowsToMatchingFrames()
    {
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;

        await vm.ToggleDemoAsync();
        await UiTest.WaitUntilAsync(() => vm.FrameRecords.Count >= 3);

        vm.FilterPattern = "A5 06";      // all demo frames start with A5 06
        Assert.NotEmpty(vm.FrameRecords);
        vm.FilterPattern = "FF FF";      // nothing starts with FF FF
        Assert.Empty(vm.FrameRecords);
        vm.FilterPattern = string.Empty; // filter off restores rows
        Assert.NotEmpty(vm.FrameRecords);

        await vm.DisconnectAsync();
        UiTest.FlushAndClose(window);
    }

    [AvaloniaFact]
    public async Task ScenarioE_ProjectSaveLoad_RestoresSession()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ft-smoke-{Guid.NewGuid():N}.ftproj");
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;
        try
        {
            vm.Project = MainWindowViewModel.DemoProject();
            vm.Project.Transport.Port = "COM7";
            vm.Project.Macros.Add(new MacroConfig { Name = "Poll", Text = "01 02 {crc16}", Hotkey = "F2" });
            await vm.SaveProjectAsync(path);
            Assert.Equal(string.Empty, vm.LastError);

            // Fresh window = next day; open the project and everything is back.
            var window2 = new MainWindow();
            window2.Show();
            var vm2 = window2.ViewModel;
            await vm2.LoadProjectAsync(path);
            Assert.Equal(string.Empty, vm2.LastError);
            Assert.Equal("COM7", vm2.Project.Transport.Port);
            Assert.Equal("LengthField", vm2.Project.Framing.Mode);
            Assert.Equal("CRC16_MODBUS", vm2.Project.Checksum.Preset);
            Assert.Equal(3, vm2.Project.Fields.Count);
            Assert.Single(vm2.Macros);
            Assert.Equal("Poll", vm2.Macros[0].Name);
            UiTest.FlushAndClose(window2);
        }
        finally
        {
            UiTest.FlushAndClose(window);
            File.Delete(path);
        }
    }
}
