using System.Threading;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Dispatch;
using RecompOne.Runtime.Host.Window;
using RecompOne.Runtime.Memory;

namespace Chronicles;

// SLUS-01384 only. Data addresses are verified against the player initialization
// and stage dispatch code. No MIPS instructions or disc bytes are modified.
public static class Cheats
{
    private const int Health = 1, Hearts = 2, Lives = 4, Invincible = 8;
    private static int _enabled;
    private static bool _initialized;
    private static readonly string[] Stages = ["stage1", "stage2", "stage3", "stage4", "stage5", "stage6", "stage7", "stage8"];
    public static bool InfiniteHealth { get => IsEnabled(Health); set => Set(Health, value); }
    public static bool InfiniteHearts { get => IsEnabled(Hearts); set => Set(Hearts, value); }
    public static bool InfiniteLives { get => IsEnabled(Lives); set => Set(Lives, value); }

    public static bool Invincibility { get => IsEnabled(Invincible); set => Set(Invincible, value); }

    // Called only at the original contact immunity branch; never changes RAM or timers.
    public static bool IgnoreContact(IMemory memory) => Invincibility
        && memory.ReadU8(0x800592C4) < 8
        && memory.ReadU8(0x80059286) is 15 or 20
        && memory.ReadU8(0x8005A822) is > 0 and <= 16;

    public static int EnabledMask => Volatile.Read(ref _enabled);

    private static bool IsEnabled(int flag) => (Volatile.Read(ref _enabled) & flag) != 0;
    private static void Set(int flag, bool value)
    {
        if (value) Interlocked.Or(ref _enabled, flag);
        else Interlocked.And(ref _enabled, ~flag);
    }

    public static void DisableAll() => Interlocked.Exchange(ref _enabled, 0);

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        Localization.Merge("""
        {"strings": {
          "chronicles.cheats": {"en": "Cheats"},
          "chronicles.cheats.invincible": {"en": "Invincibility"},
          "chronicles.cheats.invincible.help": {"en": "Prevents contact damage and knockback. Falls and instant death still apply."},
          "chronicles.cheats.health": {"en": "Infinite health"},
          "chronicles.cheats.hearts": {"en": "Infinite hearts"},
          "chronicles.cheats.lives": {"en": "Infinite lives"},
          "chronicles.cheats.off": {"en": "Disable all"},
          "chronicles.cheats.health.help": {"en": "Keeps health full during play. Falls and instant death still apply."},
          "chronicles.cheats.hearts.help": {"en": "Keeps 99 hearts during play. Pauses for the stage-clear score tally."},
          "chronicles.cheats.lives.help": {"en": "Keeps at least 5 lives. Extra lives you earn are preserved."}
        }}
        """);
        MenuRegistry.Menu("chronicles.cheats").After("menu.mods")
            .Check("chronicles.cheats.invincible", () => Invincibility, on => Invincibility = on)
            .Tooltip("chronicles.cheats.invincible.help")
            .Check("chronicles.cheats.health", () => InfiniteHealth, on => InfiniteHealth = on)
            .Tooltip("chronicles.cheats.health.help")
            .Check("chronicles.cheats.hearts", () => InfiniteHearts, on => InfiniteHearts = on)
            .Tooltip("chronicles.cheats.hearts.help")
            .Check("chronicles.cheats.lives", () => InfiniteLives, on => InfiniteLives = on)
            .Tooltip("chronicles.cheats.lives.help")
            .Separator()
            .Item("chronicles.cheats.off", DisableAll)
            .Text(() => "Changes apply during play. Session only.");
    }

    // Run on the game thread, never on the UI thread. UI toggles only update a mask.
    public static void BeforeVSync(CpuContext cpu, IMemory memory)
    {
        if (Volatile.Read(ref _enabled) == 0) return;
        var active = Dispatcher.ActiveNames;
        Apply(memory, active.Length > 0 ? active[^1] : null);
    }

    public static void Apply(IMemory memory, string? overlay)
    {
        var enabled = Volatile.Read(ref _enabled);
        if (enabled == 0) return;
        var stage = memory.ReadU8(0x800592C4);
        if (stage >= Stages.Length || overlay != Stages[stage]) return;
        // The stage dispatch tables use 15 for normal play and 20 for the boss.
        // All other states (including the heart/time score tally) must progress normally.
        if (memory.ReadU8(0x80059286) is not (15 or 20)) return;
        if ((enabled & Health) != 0 && memory.ReadU8(0x8005A822) is > 0 and <= 16)
            memory.WriteU8(0x8005A822, 16);
        if ((enabled & Hearts) != 0)
            memory.WriteU8(0x8005A838, 99);
        if ((enabled & Lives) != 0 && memory.ReadU8(0x80059292) < 5)
            memory.WriteU8(0x80059292, 5);
    }
}
