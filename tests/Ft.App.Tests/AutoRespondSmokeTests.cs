using Avalonia.Headless.XUnit;
using Ft.App.Views;
using Ft.Core.Pipeline;
using Ft.Core.Project;
using Ft.Core.Transport;
using Xunit;

namespace Ft.App.Tests;

/// <summary>M9 smoke — auto-respond rule fires a reply when a matching RX frame arrives.</summary>
public class AutoRespondSmokeTests
{
    [AvaloniaFact]
    public async Task RxPollFrame_TriggersConfiguredAck()
    {
        var window = new MainWindow();
        window.Show();
        var vm = window.ViewModel;

        vm.Project = new FtProject
        {
            Framing = new FramingConfig { Mode = "FixedLength", Length = 3 },
            AutoResponds =
            [
                new AutoRespondConfig { Pattern = "A5 01", Response = "06 \"OK\"", DelayMs = 0 },
            ],
        };

        var transport = new EchoFakeTransport { EchoEnabled = false };
        await vm.ConnectWithProjectAsync(transport, "test");
        Assert.True(vm.IsConnected);

        transport.InjectReceive([0xA5, 0x01, 0x33]);

        // The responder's reply appears as a TX frame record.
        await UiTest.WaitUntilAsync(() =>
            vm.FrameRecords.Any(f => f.Record.Direction == FrameDirection.Tx));
        var tx = vm.FrameRecords.First(f => f.Record.Direction == FrameDirection.Tx);
        Assert.Equal(new byte[] { 0x06, 0x4F, 0x4B }, tx.Record.Raw);

        await vm.DisconnectAsync();
        UiTest.FlushAndClose(window);
    }

    [AvaloniaFact]
    public async Task Connecting_DoesNotShowTrialMessaging()
    {
        // The product is not trialware — nothing in the shell may advertise one.
        var window = new MainWindow();
        window.Show();
        Assert.DoesNotContain("Trial", window.ViewModel.ConnectionStatus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Trial", window.ViewModel.StatsText, StringComparison.OrdinalIgnoreCase);
        await Task.CompletedTask;
        UiTest.FlushAndClose(window);
    }
}
