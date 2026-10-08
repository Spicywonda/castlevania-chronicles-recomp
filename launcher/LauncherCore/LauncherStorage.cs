using System.Text.Json;

namespace ChroniclesLauncher;

public static class LauncherStorage
{
    private sealed record Settings(string? DiscPath);

    public static string DataDirectory => Environment.GetEnvironmentVariable("CHRONICLES_DATA_DIR") is { Length: > 0 } custom
        ? Path.GetFullPath(custom)
        : OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Castlevania Chronicles Recomp")
            : OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CastlevaniaChroniclesRecomp")
                : Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"), "castlevania-chronicles-recomp");

    public static string? LoadSelectedPath(string dataDirectory)
    {
        try
        {
            var path = Path.Combine(dataDirectory, "launcher.json");
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path))?.DiscPath;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void SaveSelectedPath(string dataDirectory, string discPath)
    {
        Directory.CreateDirectory(dataDirectory);
        var path = Path.Combine(dataDirectory, "launcher.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new Settings(discPath)));
        File.Move(temp, path, overwrite: true);
    }
}
