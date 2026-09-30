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
            Console.Error.WriteLine("La imagen no contiene Castlevania Chronicles USA (SLUS-01384).");
            return 2;
        }
        Console.WriteLine("Disco USA SLUS-01384 verificado.");
        return 0;
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"No se pudo validar el disco: {error.Message}");
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
