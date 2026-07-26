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
        // Raw mode until a frame definition is configured (M6).
        await ViewModel.ConnectAsync(
            transport,
            new PipelineConfig(),
            $"{settings.PortName} @ {settings.BaudRate}");
    }
}
