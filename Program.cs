using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Memory;
using GameRuntime = RecompOne.Runtime.Runtime;

if (args.Length == 2 && args[0] == "--validate-disc")
{
    try
    {
        using var disc = DiscFs.Open(args[1]);
        if (!disc.Locate("SLUS_013.84", out _, out _))
        {
            Console.Error.WriteLine("The image does not contain Castlevania Chronicles USA (SLUS-01384).");
            return 2;
        }
        Console.WriteLine("USA SLUS-01384 disc verified.");
        return 0;
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"Could not validate the disc: {error.Message}");
        return 2;
    }
}

var chooseDisc = (OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()) && args.Length == 0;
if (!chooseDisc && (args.Length != 1 || !File.Exists(args[0])))
{
    Console.Error.WriteLine("Usage: CastlevaniaChronicles <path-to-USA-disc.cue>");
    return 2;
}

var cuePath = chooseDisc ? null : Path.GetFullPath(args[0]);
ConfigManager.Load();
GameRuntime.Defaults(view => view.Language = "en");
if (cuePath != null) ConfigManager.Game.CdPath = cuePath;
ConfigManager.SaveGame();
Console.WriteLine("[Build] Chronicles desktop preview 0.3.8 sound-bank transition — stages 1–8");
var exitCode = 0;
Chronicles.Widescreen.Initialize();
Chronicles.Cheats.Initialize();
Chronicles.Controls.Initialize();
using var performanceDiagnostics = Chronicles.RunDiagnostics.Start();
GameRuntime.Run(() =>
{
    var memory = new PSMemory();
    try
    {
        Chronicles.CodeWarmup.Prepare();
        Recompiled.Entry.Run(memory, cuePath, "Castlevania Chronicles — Development");
    }
    catch (RecompOne.Runtime.HardResetSignal)
    {
        throw;
    }
    catch (Exception error)
    {
        exitCode = 1;
        Console.Error.WriteLine(error);
        Console.Error.WriteLine($"[Crash] overlays={string.Join(",", RecompOne.Runtime.Dispatch.Dispatcher.ActiveNames)}");
        if (GameRuntime.Cpu is { } cpu)
            Console.Error.WriteLine($"[Crash] RA={cpu.RA:X8} SP={cpu.SP:X8} GP={cpu.GP:X8} V0={cpu.V0:X8} A0={cpu.A0:X8} A1={cpu.A1:X8}");
        if (Environment.GetEnvironmentVariable("CHRONICLES_DUMP_RAM") == "1")
        {
            var ram = new byte[2 * 1024 * 1024];
            for (var i = 0; i < ram.Length; i++) ram[i] = memory.ReadU8(0x80000000u + (uint)i);
            File.WriteAllBytes("crash-ram.bin", ram);
            Console.Error.WriteLine("Saved crash-ram.bin in the runtime directory.");
        }
    }
});
return exitCode;
