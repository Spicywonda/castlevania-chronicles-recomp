using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Dispatch;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Hle;
using Silk.NET.Input;
using ImGuiNET;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Host.Window;

namespace Chronicles;

public static class Widescreen
{
    public static int SpriteMarginFor(IMemory memory) => CanExtend(memory) ? 43 + Math.Abs(WorldViewport.Shift(memory)) : 0;
    public static uint ActorMargin(IMemory memory, uint actor)
    {
        if (!CanExtend(memory)) return 0;
        int type = memory.ReadU8(actor + 1) - 12;
        if (type < 0 || type >= 25) return 0;
        var handler = memory.ReadU32(0x800107A8 + (uint)type * 4);
        return handler is 0x800221DC or 0x800221F0 ? (uint)SpriteMarginFor(memory) : 0;
    }
    private static volatile bool _enabled;
    private static bool _initialized;
    private const string PreferenceKey = "Chronicles.Widescreen";
    public static bool Enabled { get => _enabled; set => _enabled = value; }
    private static readonly Stack<(uint Address, ushort X, ushort Width, ushort Scroll)> _backgrounds = new();

    public static void Initialize()
    {
        Enabled = ConfigManager.View.GetBool(PreferenceKey,
            Environment.GetEnvironmentVariable("CHRONICLES_WIDESCREEN") == "1");
        if (_initialized) return;
        _initialized = true;
        Localization.Merge("""
        {"strings": {
          "chronicles.graphics": {"en": "Graphics"},
          "chronicles.widescreen": {"en": "Widescreen (16:9, experimental)"},
          "chronicles.widescreen.hint": {"en": "Shows more scenery in supported first-stage gameplay. Menus, videos and other stages stay 4:3. Side-edge rendering is still experimental."}
        }}
        """);
        MenuRegistry.Menu("chronicles.graphics").After("chronicles.controls")
            .Check("chronicles.widescreen", () => Enabled, SetPreference, "F8")
            .Tooltip("chronicles.widescreen.hint");
        SettingsRegistry.Extend("display", () => {
            ImGui.Separator();
            var enabled = Enabled;
            if (ImGui.Checkbox(Localization.T("chronicles.widescreen"), ref enabled)) SetPreference(enabled);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Localization.T("chronicles.widescreen.hint"));
        });
        Event.AddListener<KeyboardEvent>(e =>
        {
            if (e.Key == (int)Key.F8 && e.Pressed && !e.Repeat) SetPreference(!Enabled);
        });
    }

    public static void SetPreference(bool enabled)
    {
        Enabled = enabled;
        ConfigManager.View.SetBool(PreferenceKey, enabled);
        ConfigManager.SaveView(PanelManager.Panels);
    }

    private static bool IsSupportedGameplay(IMemory memory) =>
        memory.ReadU8(0x800592C4) == 0 && memory.ReadU8(0x80059286) is >= 15 and <= 22;

    public static bool CanExtend(IMemory memory) =>
        Enabled && Display.WideAspect > 0 && IsSupportedGameplay(memory);

    public static void BeforeVSync(IMemory memory)
    {
        Display.WideAspect = Enabled && IsSupportedGameplay(memory) && Dispatcher.ActiveNames.Contains("stage1") ? 16f / 9f : 0;
    }

    public static void BeforeBackground(CpuContext cpu, IMemory memory)
    {
        var p = cpu.A0;
        if (!CanExtend(memory) || cpu.RA < 0x80100000 || cpu.RA >= 0x8011C800 ||
            memory.ReadU16(p + 8) != 256 || (memory.ReadU32(p) & 0x80000000) != 0)
        {
            _backgrounds.Push(default);
            return;
        }
        var x = memory.ReadU16(p + 4);
        var scroll = memory.ReadU16(p + 12);
        var shift = WorldViewport.Shift(memory);
        // GsSortFastBg wraps its map coordinates, including negative scroll.
        // Parallax scroll advances more slowly than the camera: clamping this
        // margin to scroll leaves a widening uncovered strip at the left edge.
        var left = 43 + shift;
        _backgrounds.Push((p, x, 256, scroll));
        memory.WriteU16(p + 4, (ushort)(x - left));
        memory.WriteU16(p + 8, (ushort)(299 - shift + left));
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
