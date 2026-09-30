using ChroniclesLauncher;
using System.Diagnostics;
using System.Reflection;

if (args.Length == 1 && args[0] == "--child")
{
    Console.WriteLine("child-stdout");
    Console.Error.WriteLine("child-stderr");
    return 7;
}

var failures = 0;
void Check(string name, Action action)
{
    try { action(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failures++; Console.Error.WriteLine($"FAIL {name}: {error}"); }
}
void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var root = Path.Combine(Path.GetTempPath(), "chronicles-launcher-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    Check("Missing image is rejected", () =>
    {
        var result = DiscSelection.Validate(Path.Combine(root, "missing.cue"));
        Require(!result.IsValid && result.Error is not null, "Missing image was accepted");
    });
    Check("Unsupported file is rejected", () =>
    {
        var file = Path.Combine(root, "readme.txt");
        File.WriteAllText(file, "text");
        var result = DiscSelection.Validate(file);
        Require(!result.IsValid && result.Error!.Contains("CUE", StringComparison.OrdinalIgnoreCase), "Unsupported file was accepted");
    });
    Check("Malformed CUE stays in the launcher", () =>
    {
        var file = Path.Combine(root, "broken.cue");
        File.WriteAllText(file, "FILE missing.bin BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        var result = DiscSelection.Validate(file);
        Require(!result.IsValid && result.Error is not null, "Malformed CUE was accepted");
    });
    Check("Selected path survives reload", () =>
    {
        var path = Path.Combine(root, "Juegos con espacios", "Crónicas.cue");
        LauncherStorage.SaveSelectedPath(root, path);
        Require(LauncherStorage.LoadSelectedPath(root) == path, "Selected path was not retained");
    });
    Check("Corrupt saved settings request a new image", () =>
    {
        File.WriteAllText(Path.Combine(root, "launcher.json"), "not json");
        Require(LauncherStorage.LoadSelectedPath(root) is null, "Corrupt settings were trusted");
    });
    Check("Game log captures both streams and exit code", () =>
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!)
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        info.ArgumentList.Add("--child");
        var log = Path.Combine(root, "run.log");
        var exitCode = GameSession.RunAsync(info, log).GetAwaiter().GetResult();
        var content = File.ReadAllText(log);
        Require(exitCode == 7, "Exit code was lost");
        Require(content.Contains("child-stdout") && content.Contains("child-stderr"), "A stream was lost");
        Require(content.Contains("exit=7"), "Exit code was not logged");
    });
    Check("Disc edition check runs in the game executable", () =>
    {
        var game = Path.Combine(root, "CastlevaniaChronicles.exe");
        var disc = Path.Combine(root, "Juegos con espacios", "Crónicas.cue");
        var info = GameLaunch.CreateValidationStartInfo(game, disc, root);
        Require(info.ArgumentList.Count == 2, "Wrong validation argument count");
        Require(info.ArgumentList[0] == "--validate-disc" && info.ArgumentList[1] == disc,
            "Validation command was not built correctly");
    });
    Check("Game path with spaces stays one argument", () =>
    {
        var game = Path.Combine(root, "Castlevania Chronicles.exe");
        var disc = Path.Combine(root, "Juegos con espacios", "Crónicas.cue");
        var data = Path.Combine(root, "user data");
        var info = GameLaunch.CreateStartInfo(game, disc, data);
        Require(info.FileName == game, "Wrong executable");
        Require(info.WorkingDirectory == data, "Wrong data directory");
        Require(info.ArgumentList.Count == 1 && info.ArgumentList[0] == disc, "Disc path was split");
        Require(info.RedirectStandardOutput && info.RedirectStandardError, "Output was not captured");
    });
}
finally { Directory.Delete(root, recursive: true); }
return failures == 0 ? 0 : 1;
