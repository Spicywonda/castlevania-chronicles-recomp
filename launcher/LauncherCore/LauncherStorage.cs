using System.Text.Json;

namespace ChroniclesLauncher;

public static class LauncherStorage
{
    private sealed record Settings(string? DiscPath);

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CastlevaniaChroniclesRecomp");

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
