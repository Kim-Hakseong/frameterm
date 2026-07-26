using Avalonia;
using Avalonia.Headless;
using Ft.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Ft.App.Tests;

public class TestAppBuilder
{
    // Skia renderer (not headless drawing) so the app-wide Inter FontFamily
    // can create real glyph typefaces during layout.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<Ft.App.App>()
        .WithInterFont()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
