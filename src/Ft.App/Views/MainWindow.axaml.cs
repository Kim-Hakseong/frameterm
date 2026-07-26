using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
        if (dialog.Transport is not { } transport) return;
        await ViewModel.ConnectWithProjectAsync(transport, dialog.Summary);
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

    private async void OnLogToggleClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLogging)
        {
            await ViewModel.StopLoggingAsync();
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save raw log",
            SuggestedFileName = $"frameterm-{DateTime.Now:yyyyMMdd-HHmmss}.log",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("Log file") { Patterns = ["*.log"] }],
        });
        if (file?.TryGetLocalPath() is { } path)
        {
            ViewModel.StartLogging(path);
        }
    }

    private async void OnOpenProjectClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open project",
            AllowMultiple = false,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("FrameTerm project") { Patterns = ["*.ftproj"] }],
        });
        if (files.Count == 1 && files[0].TryGetLocalPath() is { } path)
        {
            await ViewModel.LoadProjectAsync(path);
        }
    }

    private async void OnSaveProjectClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save project",
            SuggestedFileName = "session.ftproj",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("FrameTerm project") { Patterns = ["*.ftproj"] }],
        });
        if (file?.TryGetLocalPath() is { } path)
        {
            await ViewModel.SaveProjectAsync(path);
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
