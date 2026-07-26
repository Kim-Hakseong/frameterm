using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ft.Core.Project;

/// <summary>.ftproj JSON persistence (System.Text.Json, camelCase).</summary>
public static class FtProjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string ToJson(FtProject project) => JsonSerializer.Serialize(project, Options);

    public static Result<FtProject> FromJson(string json)
    {
        try
        {
            var project = JsonSerializer.Deserialize<FtProject>(json, Options);
            if (project is null) return Result<FtProject>.Fail("Project file is empty.");
            if (project.SchemaVersion > 1)
            {
                return Result<FtProject>.Fail(
                    $"Project schema version {project.SchemaVersion} is newer than this app supports.");
            }
            return Result<FtProject>.Ok(project);
        }
        catch (JsonException ex)
        {
            return Result<FtProject>.Fail($"Invalid project file: {ex.Message}");
        }
    }

    public static async Task<Result<bool>> SaveAsync(FtProject project, string path)
    {
        try
        {
            await File.WriteAllTextAsync(path, ToJson(project)).ConfigureAwait(false);
            return Result<bool>.Ok(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<bool>.Fail($"Save failed: {ex.Message}");
        }
    }

    public static async Task<Result<FtProject>> LoadAsync(string path)
    {
        try
        {
            return FromJson(await File.ReadAllTextAsync(path).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            return Result<FtProject>.Fail($"Load failed: {ex.Message}");
        }
    }
}
