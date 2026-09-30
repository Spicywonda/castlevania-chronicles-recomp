using System.Diagnostics;

namespace ChroniclesLauncher;

public static class GameLaunch
{
    public static ProcessStartInfo CreateValidationStartInfo(string gameExe, string discPath, string dataDirectory)
    {
        var info = CreateStartInfo(gameExe, discPath, dataDirectory);
        info.ArgumentList.Insert(0, "--validate-disc");
        return info;
    }

    public static ProcessStartInfo CreateStartInfo(string gameExe, string discPath, string dataDirectory)
    {
        var info = new ProcessStartInfo(gameExe)
        {
            WorkingDirectory = dataDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(discPath);
        return info;
    }
}
