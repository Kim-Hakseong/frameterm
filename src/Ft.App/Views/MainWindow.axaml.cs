using Avalonia.Controls;
using Avalonia.Interactivity;
using Ft.App.ViewModels;
using Ft.Core.Pipeline;
using Ft.Core.Transport;

namespace Ft.App.Views;

public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel();
        DataContext = ViewModel;
    }

    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new ConnectionDialog();
        await dialog.ShowDialog(this);
        if (dialog.Result is not { } settings) return;

        var transport = new SerialTransport(settings);
        await ViewModel.ConnectWithProjectAsync(
            transport,
            $"{settings.PortName} @ {settings.BaudRate}");
    }

    private async void OnFrameDefClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new FrameDefinitionDialog(ViewModel.Project);
        await dialog.ShowDialog(this);
        if (dialog.Applied)
        {
            await ViewModel.ApplyProjectAsync();
        }
    }

    private async void OnMacrosClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new MacroDialog(ViewModel.Project);
        await dialog.ShowDialog(this);
        if (dialog.Applied)
        {
            ViewModel.ReloadMacros();
        }
    }

    protected override async void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        if (e.Key is >= Avalonia.Input.Key.F1 and <= Avalonia.Input.Key.F12)
        {
            e.Handled = await ViewModel.RunHotkeyMacroAsync(e.Key.ToString());
        }
    }
}
