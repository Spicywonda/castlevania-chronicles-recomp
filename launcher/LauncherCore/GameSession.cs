using System.Diagnostics;
using System.Text;

namespace ChroniclesLauncher;

public static class GameSession
{
    public static async Task<int> RunAsync(ProcessStartInfo startInfo, string logPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        using var log = new StreamWriter(logPath, append: false, new UTF8Encoding(false)) { AutoFlush = true };
        log.WriteLine($"started={DateTimeOffset.UtcNow:O}");
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start()) throw new InvalidOperationException("Could not start the game.");

        async Task CopyAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
                lock (log) log.WriteLine(line);
        }

        var stdout = CopyAsync(process.StandardOutput);
        var stderr = CopyAsync(process.StandardError);
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        log.WriteLine($"exit={process.ExitCode}");
        return process.ExitCode;
    }
}
