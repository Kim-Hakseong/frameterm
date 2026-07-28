using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Ft.App.Services;

namespace Ft.App.Views;

/// <summary>
/// Quick manual. Content lives in <see cref="HelpContent"/> as data for both
/// languages, so the toggle button just rebuilds the panel — no duplicated
/// XAML to keep in sync.
/// </summary>
public partial class HelpWindow : Window
{
    public HelpLanguage Language { get; private set; } = HelpLanguage.Korean;

    public HelpWindow()
    {
        InitializeComponent();
        Render();
    }

    private void OnToggleLanguageClick(object? sender, RoutedEventArgs e)
    {
        Language = HelpContent.Other(Language);
        Render();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>Rebuild every text element for the current language.</summary>
    public void Render()
    {
        var doc = HelpContent.For(Language);
        Title = doc.WindowTitle;
        HeadingText.Text = doc.Heading;
        ToggleText.Text = doc.ToggleLabel;
        CloseText.Text = doc.CloseLabel;
        FootnoteText.Text = doc.Footnote;

        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(new TextBlock
        {
            Text = doc.Intro,
            TextWrapping = TextWrapping.Wrap,
            Classes = { "muted" },
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
        });

        foreach (var section in doc.Sections)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = section.Title,
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 16, 0, 6),
            });

            foreach (var line in section.Lines)
            {
                ContentPanel.Children.Add(BuildLine(line));
            }
        }
    }

    private static Control BuildLine(HelpLine line)
    {
        if (line.Kind == HelpLineKind.Code)
        {
            return new Border
            {
                Classes = { "softCard" },
                Padding = new Avalonia.Thickness(12, 8),
                Margin = new Avalonia.Thickness(0, 4, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = line.Text,
                    Classes = { "mono" },
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                },
            };
        }

        return new TextBlock
        {
            Text = line.Text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 20,
            Margin = line.Kind == HelpLineKind.Bullet
                ? new Avalonia.Thickness(2, 2, 0, 2)
                : new Avalonia.Thickness(0, 6, 0, 2),
        };
    }
}
