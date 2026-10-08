using Chronicles;
using RecompOne.Runtime.Memory;
int failures = 0;
void Check(string name, Action test) {
    Cheats.DisableAll();
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failures++; Console.Error.WriteLine($"FAIL {name}: {e.Message}"); }
}
void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
PSMemory Playing(byte state = 15, byte stage = 0) {
    var m = new PSMemory();
    m.WriteU8(0x80059286, state); m.WriteU8(0x800592C4, stage);
    m.WriteU8(0x8005A822, 4); m.WriteU8(0x8005A838, 2); m.WriteU8(0x80059292, 1);
    return m;
}
Check("Disabled cheats leave RAM unchanged", () => {
    var m = Playing(); var before = m.Ram.ToArray();
    Cheats.Apply(m, "stage1"); Require(before.SequenceEqual(m.Ram.ToArray()), "RAM changed");
});
Check("Health refills without altering neighboring bytes", () => {
    var m = Playing(); m.WriteU8(0x8005A821, 0xAB); m.WriteU8(0x8005A823, 0xCD);
    Cheats.InfiniteHealth = true; Cheats.Apply(m,"stage1");
    Require(m.ReadU8(0x8005A822)==16 && m.ReadU8(0x8005A821)==0xAB && m.ReadU8(0x8005A823)==0xCD,"Incorrect health write width");
    Require(m.ReadU8(0x8005A838)==2 && m.ReadU8(0x80059292)==1,"Unselected cheat applied");
});
Check("Death is not reversed and invalid health is not overwritten", () => {
    var m = Playing(); Cheats.InfiniteHealth = true;
    foreach (byte hp in new byte[]{0,17,255}) { m.WriteU8(0x8005A822,hp); Cheats.Apply(m,"stage1"); Require(m.ReadU8(0x8005A822)==hp,"Dead/uninitialized player changed"); }
});
Check("All eight stages and boss gameplay support selected cheats", () => {
    Cheats.InfiniteHealth=Cheats.InfiniteHearts=Cheats.InfiniteLives=true;
    for(byte stage=0;stage<8;stage++) foreach(byte state in new byte[]{15,20}) {
        var m=Playing(state,stage); Cheats.Apply(m,$"stage{stage+1}");
        Require(m.ReadU8(0x8005A822)==16 && m.ReadU8(0x8005A838)==99 && m.ReadU8(0x80059292)==5,"Cheat missing in active stage");
    }
});
Check("Menus, death and score tally can finish without replenishment", () => {
    Cheats.InfiniteHealth=Cheats.InfiniteHearts=Cheats.InfiniteLives=true;
    for(byte state=0;state<32;state++) {
        if(state is 15 or 20) continue;
        var m=Playing(state); var before=m.Ram.ToArray(); Cheats.Apply(m,"stage1");
        Require(before.SequenceEqual(m.Ram.ToArray()),$"State {state} was modified");
    }
});
Check("Unloaded or mismatched overlays cannot modify game data", () => {
    Cheats.InfiniteHealth=Cheats.InfiniteHearts=Cheats.InfiniteLives=true;
    foreach(var overlay in new string?[]{null,"main","boot","poststage1","ending","stage2","stage9"}) {
        var m=Playing();var before=m.Ram.ToArray();Cheats.Apply(m,overlay);
        Require(before.SequenceEqual(m.Ram.ToArray()),$"Unexpected write in {overlay}");
    }
});
Check("Lives cheat does not reduce legitimately earned lives", () => {
    var m=Playing();m.WriteU8(0x80059292,12);Cheats.InfiniteLives=true;Cheats.Apply(m,"stage1");Require(m.ReadU8(0x80059292)==12,"Lives reduced");
});
Check("Disable all stops subsequent writes immediately", () => {
    var m=Playing();Cheats.InfiniteHealth=Cheats.InfiniteHearts=Cheats.InfiniteLives=true;Cheats.Apply(m,"stage1");
    Cheats.DisableAll();m.WriteU8(0x8005A822,8);m.WriteU8(0x8005A838,5);m.WriteU8(0x80059292,3);
    var before=m.Ram.ToArray();Cheats.Apply(m,"stage1");Require(before.SequenceEqual(m.Ram.ToArray()),"Disabled cheat still applied");
});
Check("Death diagnostic observes pre-cheat RAM without modifying it", () => {
    var m=Playing(22,1);m.WriteU8(0x800592D0,4);m.WriteU16(0x8005A820,0x100);
    m.WriteU32(0x8005A80C,123456);Cheats.InfiniteHealth=true;
    var before=m.Ram.ToArray();
    var snapshot=RunDiagnostics.Capture(new RecompOne.Runtime.Context.CpuContext { RA=0x801012C4 },m);
    Require(before.SequenceEqual(m.Ram.ToArray()),"Diagnostic changed RAM");
    Require(snapshot.State==22 && snapshot.Stage==1 && snapshot.Room==4 && snapshot.Health==4 && snapshot.PlayerFlags==0x100 && snapshot.Cheats==1,"Death state was lost");
    Require(RunDiagnostics.Describe(snapshot).Contains("stage=2"),"Stage number is misleading");
});
Check("Performance sampler emits bounded observations without game progress", () => {
    var output=new System.Collections.Concurrent.ConcurrentQueue<string>();
    var previous=Console.Out;
    using var writer=new DiagnosticWriter(output);
    try {
        Console.SetOut(writer);
        using var sampler=RunDiagnostics.Start();
        Require(System.Threading.SpinWait.SpinUntil(() => output.Any(s=>s.StartsWith("[Performance]")),8000),"No independent sample during a stall");
        Require(output.Count(s=>s.StartsWith("[Performance]"))==1,"Sampler flooded the log");
    } finally { Console.SetOut(previous); }
});
Check("Runaway guard requires stalled progress and excessive memory growth", () => {
    const long mib=1024*1024;
    Require(RunDiagnostics.ShouldStop(3000,1200*mib,200*mib),"Runaway after respawn was not detected");
    Require(!RunDiagnostics.ShouldStop(10,1200*mib,200*mib),"Active game was rejected");
    Require(!RunDiagnostics.ShouldStop(3000,400*mib,200*mib),"Normal working set was rejected");
    Require(!RunDiagnostics.ShouldStop(3000,1200*mib,0),"Stable loading allocation was rejected");
    Require(RunDiagnostics.ShouldStop(10000,2100*mib,0),"Severe stalled memory footprint was ignored");
});
void SetInvincibility(bool enabled) {
    var property = typeof(Cheats).GetProperty("Invincibility");
    Require(property != null, "Invincibility toggle is missing");
    property!.SetValue(null, enabled);
}
void Contact(PSMemory m) {
    var cpu = new RecompOne.Runtime.Context.CpuContext { SP=0x801FFF00, A0=0x80060000, RA=0x80010000, S0=0x1234 };
    Recompiled.CastlevaniaChronicles_main.func_8001D458(cpu,m);
    Require(cpu.SP==0x801FFF00 && cpu.RA==0x80010000 && cpu.S0==0x1234,"Contact broke saved registers");
}
PSMemory ContactScene(byte stage=0, byte state=15, byte mode=0) {
    var m=Playing(state,stage);
    m.WriteU8(0x80059243,mode);
    for(uint i=0;i<24;i++) { m.WriteU8(0x80059170+i,2); m.WriteU8(0x80010580+i,2); }
    m.WriteU8(0x80060002,2); // Enemy timer must still tick while invincible.
    m.WriteU16(0x8005A820,1);
    return m;
}
Check("Contact invincibility prevents damage and knockback in both modes and all stages", () => {
    SetInvincibility(true);
    for(byte mode=0;mode<2;mode++) for(byte stage=0;stage<8;stage++) foreach(byte state in new byte[]{15,20}) {
        var m=ContactScene(stage,state,mode);
        var player=Enumerable.Range(0,64).Select(i=>m.ReadU8(0x8005A808+(uint)i)).ToArray();
        Contact(m);
        Require(player.SequenceEqual(Enumerable.Range(0,64).Select(i=>m.ReadU8(0x8005A808+(uint)i))),"Hit changed player state");
        Require(m.ReadU8(0x80060002)==1,"Enemy bookkeeping stopped");
    }
});
Check("Turning invincibility off immediately restores contact damage", () => {
    SetInvincibility(true); var m=ContactScene(); Contact(m);
    SetInvincibility(false); Contact(m);
    Require(m.ReadU8(0x8005A822)==2 && m.ReadU16(0x8005A820)==0x80,"Damage or hit reaction did not return");
    Require(m.ReadU16(0x8005A82A)==0,"Cheat left an invulnerability timer behind");
});
Check("Invincibility does not refill dead health or alter transitions", () => {
    SetInvincibility(true);
    foreach(byte state in new byte[]{9,19,22,23}) {
        var m=Playing(state); m.WriteU8(0x8005A822,0);
        var before=m.Ram.ToArray(); Cheats.Apply(m,"stage1");
        Require(before.SequenceEqual(m.Ram.ToArray()),"Death or transition changed");
    }
    Cheats.DisableAll(); Require(Cheats.EnabledMask==0,"Disable all left invincibility active");
});
Check("Disabled contact damage remains active in Original and Arrange", () => {
    for(byte mode=0;mode<2;mode++) {
        var m=ContactScene(mode:mode); Contact(m);
        Require(m.ReadU8(0x8005A822)==2 && m.ReadU16(0x8005A820)==0x80,"Original damage branch changed");
    }
});
Check("Contact immunity excludes dead players, invalid stages and transitions", () => {
    SetInvincibility(true);
    foreach(byte state in new byte[]{0,9,19,22,23}) Require(!Cheats.IgnoreContact(Playing(state)),"Transition protected");
    var m=Playing();m.WriteU8(0x8005A822,0);Require(!Cheats.IgnoreContact(m),"Dead player protected");
    m=Playing(stage:8);Require(!Cheats.IgnoreContact(m),"Invalid stage protected");
});
return failures==0?0:1;

sealed class DiagnosticWriter(System.Collections.Concurrent.ConcurrentQueue<string> output) : System.IO.TextWriter {
    public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    public override void WriteLine(string? value) { if(value is not null) output.Enqueue(value); }
}
