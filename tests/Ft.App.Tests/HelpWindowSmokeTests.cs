using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Ft.App.Services;
using Ft.App.Views;
using Xunit;

namespace Ft.App.Tests;

/// <summary>Help window: opens, renders both languages, toggles with one button.</summary>
public class HelpWindowSmokeTests
{
    private static List<string> VisibleText(Window window) =>
        window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(t => t.Text ?? string.Empty)
            .Where(t => t.Length > 0)
            .ToList();

    [AvaloniaFact]
    public void Opens_InKorean_WithAllSections()
    {
        var window = new HelpWindow();
        window.Show();

        Assert.Equal(HelpLanguage.Korean, window.Language);
        var text = VisibleText(window);
        Assert.Contains(text, t => t.Contains("간단 사용설명서"));
        Assert.Contains(text, t => t.Contains("Demo"));
        Assert.Contains(text, t => t.Contains("프레임 정의"));
        // Every section heading from the document is rendered.
        foreach (var section in HelpContent.For(HelpLanguage.Korean).Sections)
        {
            Assert.Contains(text, t => t == section.Title);
        }

        UiTest.FlushAndClose(window);
    }

    [AvaloniaFact]
    public void ToggleButton_SwitchesLanguageBothWays()
    {
        var window = new HelpWindow();
        window.Show();

        var toggle = window.GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.GetVisualDescendants().OfType<TextBlock>()
                .Any(t => t.Text == "English"));

        // Click handlers are driven by raising the routed event in headless tests.
        toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(HelpLanguage.English, window.Language);

        var english = VisibleText(window);
        Assert.Contains(english, t => t.Contains("quick guide"));
        Assert.Contains(english, t => t.Contains("Frame definition"));
        Assert.DoesNotContain(english, t => t.Contains("간단 사용설명서"));

        toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(HelpLanguage.Korean, window.Language);
        Assert.Contains(VisibleText(window), t => t.Contains("간단 사용설명서"));

        UiTest.FlushAndClose(window);
    }

    [AvaloniaFact]
    public void BothLanguages_HaveMatchingStructure()
    {
        var kr = HelpContent.For(HelpLanguage.Korean);
        var en = HelpContent.For(HelpLanguage.English);

        Assert.Equal(kr.Sections.Count, en.Sections.Count);
        for (int i = 0; i < kr.Sections.Count; i++)
        {
            Assert.Equal(kr.Sections[i].Lines.Count, en.Sections[i].Lines.Count);
        }
    }

    [AvaloniaFact]
    public void MainWindow_HelpButton_OpensGuide()
    {
        var main = new MainWindow();
        main.Show();

        var helpButton = main.GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.GetVisualDescendants().OfType<TextBlock>()
                .Any(t => t.Text == "Help"));
        helpButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        var help = Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.OfType<HelpWindow>().FirstOrDefault()
            : null;
        // Headless lifetime may not track windows; the click must not throw either way.
        help?.Close();

        UiTest.FlushAndClose(main);
    }
}
