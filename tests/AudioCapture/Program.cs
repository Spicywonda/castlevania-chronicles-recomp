using RecompOne.Runtime;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Hardware;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Sdk;
using GameRuntime=RecompOne.Runtime.Runtime;
if(args.Length!=1 || !File.Exists(args[0])) return 2;
ConfigManager.Load();
GameRuntime.Defaults(view => view.Language = "en");
ConfigManager.Game.CdPath=Path.GetFullPath(args[0]);
Chronicles.Widescreen.Initialize();
Chronicles.Cheats.Initialize();
Chronicles.Controls.Initialize();
Event.AddListener<VSyncEvent>(e=> {
    var state=e.Memory.ReadU8(0x80059286);
    var press=(state is 25 or 1 or 7 or 8) && e.Frame%60<6;
    var attack=state==15 && e.Frame%40<8;
    Controller.State=press ? (ushort)0xBFF7 : attack ? (ushort)0x7FFF : (ushort)0xFFFF;
    var saved=e.Context.Snapshot();
    try { LibPad.PadStartCom(e.Context,e.Memory); }
    finally { e.Context.Restore(saved); }
});
using var diagnostics=Chronicles.RunDiagnostics.Start();
GameRuntime.Run(()=>Recompiled.Entry.Run(new PSMemory(),Path.GetFullPath(args[0]),"Chronicles audio integration capture"));
return 0;
