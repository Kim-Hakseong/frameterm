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
}
