using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Chronicles;

// Prepare generated code without executing it or accessing game memory. Called
// before boot so first-use compilation does not interrupt gameplay. SDK methods
// and later tiered optimizations may still compile during execution.
public static class CodeWarmup
{
    private static readonly object Gate = new();
    private static bool _prepared;

    public static int Prepare()
    {
        lock (Gate)
        {
            if (_prepared) return 0;
            var watch = Stopwatch.StartNew();
            Console.WriteLine("[Warmup] Preparing game code...");
            var count = 0;
            foreach (var type in typeof(Recompiled.Entry).Assembly.GetTypes())
            {
                if (type.Namespace != "Recompiled" ||
                    !type.Name.StartsWith("CastlevaniaChronicles_", StringComparison.Ordinal)) continue;
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (method.ContainsGenericParameters || method.GetMethodBody() is null) continue;
                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    count++;
                }
            }
            _prepared = true;
            Console.WriteLine($"[Warmup] Prepared {count} game methods in {watch.ElapsedMilliseconds}ms.");
            return count;
        }
    }
}
