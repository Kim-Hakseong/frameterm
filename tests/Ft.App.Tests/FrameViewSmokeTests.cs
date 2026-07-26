using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Ft.App.Views;
using Ft.Core.Checksum;
using Ft.Core.Project;
using Ft.Core.Transport;
using Xunit;

namespace Ft.App.Tests;

/// <summary>
/// M6 smoke — PRD scenario A (custom STX..ETX + CRC16 protocol shows OK/FAIL
/// framed rows) and scenario B (field definitions appear as parsed values).
/// </summary>
public class FrameViewSmokeTests
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

    /// <summary>Build a scenario-A frame: 02 | payload | CRC16(coverage) | 03.</summary>
    private static byte[] StxEtxFrame(byte[] payload, bool corrupt = false)
    {
        // CRC-16/CCITT-FALSE over payload, BE, placed right before ETX.
        uint crc = ChecksumEngine.Compute(ChecksumPresets.Crc16CcittFalse, payload);
        if (corrupt) crc ^= 0xFFFF;
        byte[] crcBytes = ChecksumEngine.ToBytes(ChecksumPresets.Crc16CcittFalse, crc, ByteOrder.Big);
        return [0x02, .. payload, .. crcBytes, 0x03];
    }

    [AvaloniaFact]
    public async Task ScenarioA_CustomStxEtxCrcProtocol_FramesShowOkAndFail()
    {
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;

        // User defines the frame in the Frame Definition dialog; here we set
        // the same project model the dialog writes.
        vm.Project = new FtProject
        {
            Framing = new FramingConfig { Mode = "Delimiter", StartHex = "02", EndHex = "03" },
            Checksum = new ChecksumConfig
            {
                Preset = "CRC16_CCITT_FALSE",
                OffsetFromEnd = 3,        // CRC sits before the 1-byte ETX
                ByteOrder = "BE",
                CoverageStart = 1,        // exclude STX
                CoverageEndOffsetFromEnd = 3,
            },
        };

        var transport = new EchoFakeTransport { EchoEnabled = false };
        await vm.ConnectWithProjectAsync(transport, "test");
        Assert.True(vm.IsConnected);

        transport.InjectReceive(StxEtxFrame([0x10, 0x20]));
        transport.InjectReceive(StxEtxFrame([0x11, 0x21], corrupt: true));

        await WaitUntilAsync(() => vm.FrameRecords.Count >= 2);
        Assert.True(vm.FrameRecords[0].Record.ChecksumOk);
        Assert.False(vm.FrameRecords[1].Record.ChecksumOk);
        Assert.Equal("OK", vm.FrameRecords[0].ChecksumText);
        Assert.Equal("FAIL", vm.FrameRecords[1].ChecksumText);

        await vm.DisconnectAsync();
        window.Close();
    }

    [AvaloniaFact]
    public async Task ScenarioB_DemoFields_ParsedIntoTableAndSummary()
    {
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;

        await vm.ToggleDemoAsync();
        await WaitUntilAsync(() => vm.FrameRecords.Count >= 2);

        var row = vm.FrameRecords[0];
        Assert.Contains("seq=", row.FieldsSummary);
        Assert.Contains("temp=", row.FieldsSummary);
        Assert.Contains("status=", row.FieldsSummary);

        // Selecting a frame fills the detail hex view and field table.
        vm.SelectedFrame = row;
        Dispatcher.UIThread.RunJobs();
        Assert.NotEmpty(vm.DetailRows);
        Assert.Equal(3, vm.DetailFields.Count);
        Assert.Equal("seq", vm.DetailFields[0].Name);

        await vm.DisconnectAsync();
        window.Close();
    }

    [AvaloniaFact]
    public async Task DemoHighlight_StatusFrames_GetRuleColor()
    {
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;

        await vm.ToggleDemoAsync();
        // seq % 7 == 0 frames carry status=1 → highlight rule #9C2030.
        await WaitUntilAsync(() => vm.FrameRecords.Any(f => f.Record.Color == "#9C2030"));

        await vm.DisconnectAsync();
        window.Close();
    }
}
