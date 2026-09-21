using RecompOne.Runtime.Config;
using RecompOne.Runtime.Memory;
using GameRuntime = RecompOne.Runtime.Runtime;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: CastlevaniaChronicles <path-to-USA-disc.cue>");
    return 2;
}

var cuePath = Path.GetFullPath(args[0]);
ConfigManager.Load();
GameRuntime.Defaults(view => view.Language = "en");
ConfigManager.Game.CdPath = cuePath;
ConfigManager.SaveGame();
var exitCode = 0;
Chronicles.Widescreen.Initialize();
GameRuntime.Run(() =>
{
    var memory = new PSMemory();
    try
    {
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
