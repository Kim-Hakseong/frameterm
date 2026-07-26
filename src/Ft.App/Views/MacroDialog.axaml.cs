using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ft.Core.Compose;
using Ft.Core.Project;

namespace Ft.App.Views;

public partial class MacroDialog : Window
{
    private const int MaxMacros = 20;

    private readonly FtProject _project;
    private readonly ObservableCollection<MacroConfig> _macros;

    public bool Applied { get; private set; }

    // Designer constructor.
    public MacroDialog() : this(new FtProject())
    {
    }

    public MacroDialog(FtProject project)
    {
        InitializeComponent();
        _project = project;
        _macros = new ObservableCollection<MacroConfig>(project.Macros);
        MacrosGrid.ItemsSource = _macros;
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (_macros.Count >= MaxMacros)
        {
            ShowError($"Up to {MaxMacros} macros are supported.");
            return;
        }
        _macros.Add(new MacroConfig { Name = $"Macro {_macros.Count + 1}" });
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (MacrosGrid.SelectedItem is MacroConfig macro) _macros.Remove(macro);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        foreach (var macro in _macros)
        {
            if (string.IsNullOrWhiteSpace(macro.Text)) continue;
            var composed = PayloadComposer.Compose(macro.Text);
            if (!composed.IsOk)
            {
                ShowError($"Macro '{macro.Name}': {composed.Error}");
                return;
            }
        }

        _project.Macros = [.. _macros];
        Applied = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }
}
