using System.Diagnostics;
using System.Threading;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;

namespace Chronicles;

// Diagnostic-only observation: RAM is read on the game thread. The sampler
// reads an immutable snapshot, so it cannot interfere with game memory.
public static class RunDiagnostics
{
    public sealed record Snapshot(long At, byte State, byte Stage, byte Room, byte Health,
        ushort PlayerFlags, byte Lives, int PlayerY, uint ReturnAddress, int Cheats);
    private static Snapshot? _latest;
    private static Snapshot? _logged;
    private static long _calls;
    private static long _lastStateLog;
    private static int _sampling;
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    public static Snapshot Capture(CpuContext cpu, IMemory m) => new(
        Clock.ElapsedMilliseconds, m.ReadU8(0x80059286), m.ReadU8(0x800592C4),
        m.ReadU8(0x800592D0), m.ReadU8(0x8005A822), m.ReadU16(0x8005A820),
        m.ReadU8(0x80059292), unchecked((int)m.ReadU32(0x8005A80C)), cpu.RA,
        Cheats.EnabledMask);

    public static string Describe(Snapshot s) =>
        $"state={s.State} stage={s.Stage + 1} room={s.Room} hp={s.Health} playerFlags={s.PlayerFlags:X4} lives={s.Lives} yFixed={s.PlayerY} RA={s.ReturnAddress:X8} cheats={s.Cheats}";

    public static void BeforeVSync(CpuContext cpu, IMemory memory)
    {
        Interlocked.Increment(ref _calls);
        var latest = Volatile.Read(ref _latest);
        if (latest is not null && Clock.ElapsedMilliseconds - latest.At < 16) return;
        var old = _logged;
        var current = Capture(cpu, memory);
        Volatile.Write(ref _latest, current);
        // At most four state messages per second, even for an erroneous state loop.
        if (current.At - _lastStateLog < 250) return;
        if (old is null || old.State != current.State || old.Stage != current.Stage ||
            old.Room != current.Room || old.Cheats != current.Cheats || (old.Health == 0) != (current.Health == 0))
        {
            _lastStateLog = current.At;
            _logged = current;
            Console.WriteLine($"[State] t={current.At}ms {Describe(current)}");
        }
    }

    public static bool ShouldStop(long stalledMs, long privateBytes, long growthBytes) =>
        (stalledMs >= 2000 && privateBytes >= 1024L*1024*1024 && growthBytes >= 128L*1024*1024) ||
        (stalledMs >= 10000 && privateBytes >= 2048L*1024*1024);

    public static IDisposable Start()
    {
        long lastCalls = 0, lastTime = Clock.ElapsedMilliseconds;
        double lastCpu = 0;
        long lastPrivate = 0, lastReport = lastTime;
        using (var p = Process.GetCurrentProcess()) lastCpu = p.TotalProcessorTime.TotalMilliseconds;
        Console.WriteLine("[Diagnostics] CPU core%=100 means one full core; RAM in MiB; cheats mask: health=1 hearts=2 lives=4. VSync calls are not rendered FPS.");
        return new Timer(_ =>
        {
            if (Interlocked.Exchange(ref _sampling, 1) != 0) return;
            try
            {
                using var p = Process.GetCurrentProcess();
                var now = Clock.ElapsedMilliseconds;
                var cpu = p.TotalProcessorTime.TotalMilliseconds;
                var count = Interlocked.Read(ref _calls);
                var elapsed = Math.Max(1, now - lastTime);
                var state = Volatile.Read(ref _latest);
                var privateBytes = p.PrivateMemorySize64;
                var age = state is null ? -1 : now - state.At;
                var stop = state is not null && ShouldStop(age, privateBytes, privateBytes-lastPrivate);
                lastPrivate = privateBytes;
                if (!stop && now-lastReport < 5000) return;
                lastReport = now;
                Console.WriteLine(FormattableString.Invariant(
                    $"[Performance] t={now}ms cpuCore%={(cpu-lastCpu)*100/elapsed:F1} ramMiB={p.WorkingSet64/1048576.0:F1} privateMiB={p.PrivateMemorySize64/1048576.0:F1} managedMiB={GC.GetTotalMemory(false)/1048576.0:F1} vsyncCalls/s={(count-lastCalls)*1000.0/elapsed:F1} lastVSyncAgeMs={(state is null ? -1 : now-state.At)}"));
                if (state is not null) Console.WriteLine($"[Snapshot] {Describe(state)}");
                Console.WriteLine($"[GpuWork] {RecompOne.Runtime.GpuWorkDiagnostics.Describe()}");
                Console.WriteLine($"[Audio] {RecompOne.Runtime.Runtime.AudioStatus}");
                lastTime = now; lastCpu = cpu; lastCalls = count;
                if (stop)
                {
                    Console.Error.WriteLine("[RunawayGuard] Game stopped: stalled progress with excessive memory use. Exit code 3. Please share run.log.");
                    Console.Out.Flush(); Console.Error.Flush();
                    Environment.Exit(3);
                }
            }
            catch (Exception e) { Console.Error.WriteLine($"[Diagnostics] sampler: {e.GetType().Name}"); }
            finally { Volatile.Write(ref _sampling, 0); }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }
}
