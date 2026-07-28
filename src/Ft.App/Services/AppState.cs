namespace Ft.App.Services;

/// <summary>
/// Tiny persisted app state. Today it only records that the quick guide has
/// been shown once, so new users get it automatically and repeat users don't.
/// </summary>
public static class AppState
{
    private static string MarkerPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TestBench", "FrameTerm", "guide-shown");

    /// <summary>
    /// True on the very first run, and marks it so later runs return false.
    /// If the state directory is unwritable we still return true (showing the
    /// guide is the harmless failure mode).
    /// </summary>
    public static bool IsFirstRun()
    {
        try
        {
            string path = MarkerPath;
            if (File.Exists(path)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, DateTimeOffset.Now.ToString("O"));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
