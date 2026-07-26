using System.Windows.Input;
using Ft.Core.Project;

namespace Ft.App.ViewModels;

/// <summary>A send macro button: name, compose expression, optional hotkey.</summary>
public sealed class MacroViewModel(MacroConfig config, ICommand runCommand)
{
    public MacroConfig Config { get; } = config;

    /// <summary>The owning view-model's run command; parameter is this macro.</summary>
    public ICommand RunCommand { get; } = runCommand;

    public string Name => string.IsNullOrWhiteSpace(Config.Name) ? "(macro)" : Config.Name;
    public string Text => Config.Text;
    public string Hotkey => Config.Hotkey;
    public string ToolTip => string.IsNullOrWhiteSpace(Hotkey) ? Text : $"{Text}  [{Hotkey}]";
}
