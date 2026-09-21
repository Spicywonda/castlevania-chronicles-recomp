using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Dispatch;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Hle;
using Silk.NET.Input;

namespace Chronicles;

public static class Widescreen
{
    public static int SpriteMargin => Enabled && Display.WideAspect > 0 ? 43 : 0;
    public static uint ActorMargin(IMemory memory, uint actor)
    {
        if (SpriteMargin == 0) return 0;
        int type = memory.ReadU8(actor + 1) - 12;
        if (type < 0 || type >= 25) return 0;
        var handler = memory.ReadU32(0x800107A8 + (uint)type * 4);
        return handler is 0x800221DC or 0x800221F0 ? 43u : 0;
    }
    public static bool Enabled { get; set; }
    private static readonly Stack<(uint Address, ushort X, ushort Width, ushort Scroll)> _backgrounds = new();

    public static void Initialize()
    {
        var experimental = Environment.GetEnvironmentVariable("CHRONICLES_WIDESCREEN") == "1";
        Enabled = experimental;
        Event.AddListener<KeyboardEvent>(e =>
        {
            if (experimental && e.Key == (int)Key.F8 && e.Pressed && !e.Repeat) Enabled = !Enabled;
        });
    }

    public static void BeforeVSync()
    {
        Display.WideAspect = Enabled && Dispatcher.ActiveNames.Contains("stage1") ? 16f / 9f : 0;
    }

    public static void BeforeBackground(CpuContext cpu, IMemory memory)
    {
        var p = cpu.A0;
        if (!Enabled || cpu.RA < 0x80100000 || cpu.RA >= 0x8011C800 ||
            memory.ReadU16(p + 8) != 256 || (memory.ReadU32(p) & 0x80000000) != 0)
        {
            _backgrounds.Push(default);
            return;
        }
        var x = memory.ReadU16(p + 4);
        var scroll = memory.ReadU16(p + 12);
        var left = Math.Clamp((int)(short)scroll, 0, 43);
        _backgrounds.Push((p, x, 256, scroll));
        memory.WriteU16(p + 4, (ushort)(x - left));
        memory.WriteU16(p + 8, (ushort)(299 + left));
        memory.WriteU16(p + 12, (ushort)(scroll - left));
    }

    public static void AfterBackground(CpuContext cpu, IMemory memory)
    {
        if (!_backgrounds.TryPop(out var saved) || saved.Address == 0) return;
        memory.WriteU16(saved.Address + 4, saved.X);
        memory.WriteU16(saved.Address + 8, saved.Width);
        memory.WriteU16(saved.Address + 12, saved.Scroll);
    }
}
