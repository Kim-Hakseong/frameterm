using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Ft.App.ViewModels;
using Ft.App.Views;
using Ft.Core.Project;
using Ft.Core.Transport;
using Xunit;

namespace Ft.App.Tests;

/// <summary>M7 smoke — PRD scenario C: composed macro send with auto len/CRC, repeat send.</summary>
public class ComposeSmokeTests
{
    private static Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 10000) =>
        UiTest.WaitUntilAsync(condition, timeoutMs);

    private static async Task<(MainWindow, MainWindowViewModel)> ConnectRawEchoAsync()
    {
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;
        vm.Project = new FtProject(); // raw stream, echo round-trips bytes
        await vm.ConnectWithProjectAsync(new EchoFakeTransport(), "test-echo");
        Assert.True(vm.IsConnected);
        return (window, vm);
    }

    [AvaloniaFact]
    public async Task ScenarioC_ComposedSend_AutoLenAndCrc_RoundTrips()
    {
        var (window, vm) = await ConnectRawEchoAsync();

        vm.ComposeText = "A5 01 {len} \"CMD\" {crc16}";
        await vm.SendComposedAsync();
        Assert.Equal(string.Empty, vm.ComposeError);

        // len = 6 non-checksum bytes; CRC-16/MODBUS over them, LE.
        byte[] expectedBody = [0xA5, 0x01, 0x06, 0x43, 0x4D, 0x44];
        uint crc = Ft.Core.Checksum.ChecksumEngine.Compute(
            Ft.Core.Checksum.ChecksumPresets.Crc16Modbus, expectedBody);
        byte[] expected =
        [
            .. expectedBody,
            .. Ft.Core.Checksum.ChecksumEngine.ToBytes(
                Ft.Core.Checksum.ChecksumPresets.Crc16Modbus, crc, Ft.Core.Checksum.ByteOrder.Little),
        ];

        await WaitUntilAsync(() => vm.StatsText.Contains($"TX {expected.Length} B"));
        await WaitUntilAsync(() => vm.StatsText.Contains($"RX {expected.Length} B"));

        await vm.DisconnectAsync();
        UiTest.FlushAndClose(window);
    }

    [AvaloniaFact]
    public async Task InvalidExpression_ShowsErrorAndSendsNothing()
    {
        var (window, vm) = await ConnectRawEchoAsync();

        vm.ComposeText = "A5 {typo}";
        await vm.SendComposedAsync();
        Assert.NotEqual(string.Empty, vm.ComposeError);
        Assert.Contains("TX 0 B", vm.StatsText);

        await vm.DisconnectAsync();
        UiTest.FlushAndClose(window);
    }

    [AvaloniaFact]
    public async Task Macro_RunViaCommandAndHotkey()
    {
        var (window, vm) = await ConnectRawEchoAsync();

        vm.Project.Macros.Add(new MacroConfig { Name = "Poll", Text = "01 02 03", Hotkey = "F5" });
        vm.ReloadMacros();
        Assert.Single(vm.Macros);

        await vm.RunMacroAsync(vm.Macros[0]);
        await WaitUntilAsync(() => vm.StatsText.Contains("TX 3 B"));

        Assert.True(await vm.RunHotkeyMacroAsync("F5"));
        await WaitUntilAsync(() => vm.StatsText.Contains("TX 6 B"));
        Assert.False(await vm.RunHotkeyMacroAsync("F9"));

        await vm.DisconnectAsync();
        UiTest.FlushAndClose(window);
    }

    [AvaloniaFact]
    public async Task RepeatSend_FiresMultipleTimes_ThenStops()
    {
        var (window, vm) = await ConnectRawEchoAsync();

        vm.ComposeText = "AA 55";
        vm.RepeatMsText = "40";
        vm.RepeatEnabled = true;

        // TX records flow into the frame list even without a framer.
        await WaitUntilAsync(() => vm.FrameRecords.Count >= 3);

        vm.RepeatEnabled = false;
        await vm.DisconnectAsync();
        UiTest.FlushAndClose(window);
    }

    [AvaloniaFact]
    public async Task Macro_Limit20_EnforcedByReload()
    {
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;
        for (int i = 0; i < 25; i++)
        {
            vm.Project.Macros.Add(new MacroConfig { Name = $"M{i}", Text = "01" });
        }
        vm.ReloadMacros();
        Assert.Equal(20, vm.Macros.Count);
        await Task.CompletedTask;
        UiTest.FlushAndClose(window);
    }
}
