using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Sdk;
using RecompOne.Runtime.Config;
using GameRuntime = RecompOne.Runtime.Runtime;
ConfigManager.Load();
ConfigManager.Game.Muted = true;
var failures = 0;
GameRuntime.Run(() =>
{
    try
    {
        var m = new PSMemory();
        var c = new CpuContext();
        GameRuntime.SetContext(c, m);
        GameRuntime.Initialize("GPU palette regression");
        foreach (var rect in new[] { (960, 0, 16, 1), (976, 0, 16, 1), (960, 13, 16, 1), (960, 0, 16, 512) })
        {
            var (x, y, w, h) = rect;
            m.WriteU16(0x80010000, (ushort)x); m.WriteU16(0x80010002, (ushort)y);
            m.WriteU16(0x80010004, (ushort)w); m.WriteU16(0x80010006, (ushort)h);
            for (var i = 0; i < w*h; i++) m.WriteU16(0x80020000u + (uint)i*2, (ushort)((i*977+1234)&0xFFFF));
            c.A0 = 0x80010000; c.A1 = 0x80020000; LibGpu.LoadImage(c,m);
            c.A0 = 0x80010000; c.A1 = 0x80030000; LibGpu.StoreImage(c,m);
            var bad = 0;
            for (var i = 0; i < w*h; i++)
                if(m.ReadU16(0x80020000u+(uint)i*2)!=m.ReadU16(0x80030000u+(uint)i*2)) bad++;
            Console.WriteLine($"{(bad==0 ? "PASS" : "FAIL")} palette round trip {rect}: {bad}/{w*h} mismatches");
            if(bad!=0) failures++;
        }
    }
    catch(Exception e) { failures++; Console.Error.WriteLine(e); }
});
return failures == 0 ? 0 : 1;
