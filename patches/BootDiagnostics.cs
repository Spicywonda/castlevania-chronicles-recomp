using System.Text.Json;
using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;

namespace Chronicles;

public static class BootDiagnostics
{
    private static readonly bool Enabled = Environment.GetEnvironmentVariable("CHRONICLES_DIAGNOSTICS") == "1";
    private static long _nextCheck;
    private static uint _bgStart;
    private static object? _bgInput;
    private static readonly List<object> _backgrounds = new();

    public static void BeforeBackground(CpuContext cpu, IMemory memory)
    {
        Widescreen.BeforeBackground(cpu, memory);
        if (!Enabled || _backgrounds.Count >= 64) return;
        _bgStart = memory.ReadU32(0x80097CA8);
        _bgInput = new { cpu.A0, cpu.A1, cpu.A2, cpu.RA,
            Data = Bytes(memory, cpu.A0, 32),
            Map = Bytes(memory, memory.ReadU32(cpu.A0 + 0x14), 32) };
    }

    public static void AfterBackground(CpuContext cpu, IMemory memory)
    {
        Widescreen.AfterBackground(cpu, memory);
        if (!Enabled || _backgrounds.Count >= 64) return;
        var end = memory.ReadU32(0x80097CA8);
        var length = end >= _bgStart ? Math.Min(end - _bgStart, 192u) : 0;
        _backgrounds.Add(new { Input = _bgInput, Start = _bgStart, End = end,
            Packets = Bytes(memory, _bgStart, (int)length) });
        File.WriteAllText("background-state.json", JsonSerializer.Serialize(_backgrounds,
            new JsonSerializerOptions { WriteIndented = true }));
        if (_backgrounds.Count == 64) File.WriteAllText("capture-request", "");
    }

    private static uint _readDestination;
    private static ushort[]? _readExpected;
    private static int _paletteReads;
    public static void BeforeStore(CpuContext cpu, IMemory memory)
    {
        _readExpected = null;
        if (!Enabled || _paletteReads >= 100 || Runtime.Gpu == null) return;
        int x = memory.ReadU16(cpu.A0), y = memory.ReadU16(cpu.A0 + 2),
            w = memory.ReadU16(cpu.A0 + 4), h = memory.ReadU16(cpu.A0 + 6);
        if (x < 960 || w < 1 || h < 1 || x + w > 1024 || y + h > 512) return;
        _readExpected = new ushort[w * h];
        _readDestination = cpu.A1;
        for (var row = 0; row < h; row++)
            Array.Copy(Runtime.Gpu.Vram, (y + row) * 1024 + x, _readExpected, row * w, w);
        Console.WriteLine($"[palette] StoreImage {x},{y} {w}x{h} dst={cpu.A1:X8} caller={cpu.RA:X8}");
    }
    public static void AfterStore(CpuContext cpu, IMemory memory)
    {
        if (_readExpected == null) return;
        var mismatches = 0;
        for (var i = 0; i < _readExpected.Length; i++)
            if (memory.ReadU16(_readDestination + (uint)i * 2) != _readExpected[i]) mismatches++;
        Console.WriteLine($"[palette] readback differs from shadow: {mismatches}/{_readExpected.Length}; shadow nonzero={_readExpected.Count(v => v != 0)}");
        _paletteReads++;
        _readExpected = null;
    }

    private static string Bytes(IMemory memory, uint address, int count)
    {
        var data = new byte[count];
        for (var i = 0; i < count; i++) data[i] = memory.ReadU8(address + (uint)i);
        return Convert.ToHexString(data);
    }

    public static void BeforeVSync(CpuContext cpu, IMemory memory)
    {
        Widescreen.BeforeVSync();
        if (!Enabled || Environment.TickCount64 < _nextCheck) return;
        _nextCheck = Environment.TickCount64 + 1000;
        if (!File.Exists("capture-request")) return;
        File.Delete("capture-request");
        var ram = (PSMemory)memory;
        File.WriteAllBytes("live-ram.bin", ram.Ram.ToArray());
        if (Runtime.Gpu != null)
        {
            var vram = new byte[Runtime.Gpu.Vram.Length * 2];
            Buffer.BlockCopy(Runtime.Gpu.Vram, 0, vram, 0, vram.Length);
            File.WriteAllBytes("live-vram.bin", vram);
        }
        var spu = Runtime.Spu;
        var voices = new Spu.VoiceDebug[24];
        var state = new Spu.SpuDebug();
        spu?.CaptureDebug(voices, out state);
        if (spu != null) File.WriteAllBytes("live-spu.bin", spu.Ram.ToArray());
        var report = new
        {
            cpu.RA, cpu.GP, cpu.SP,
            StreamActive = memory.ReadU32(0x80059320),
            StreamStopRequested = memory.ReadU32(cpu.GP + 0x304),
            StreamEnvironment = memory.ReadU32(cpu.GP + 0x2EC),
            IrqAddress = spu?.ReadReg16(0x1F801DA4),
            SpuControl = spu?.ReadReg16(0x1F801DAA),
            SpuStatus = spu?.ReadReg16(0x1F801DAE),
            IrqStatus = Interrupts.ReadStat(),
            IrqMask = Interrupts.ReadMask(),
            ActiveOverlays = RecompOne.Runtime.Dispatch.Dispatcher.ActiveNames,
            Spu = state, Voices = voices,
        };
        File.WriteAllText("live-state.json", JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        Console.WriteLine("[diagnostics] captured RAM, SPU and stream state at VSync");
    }
}
