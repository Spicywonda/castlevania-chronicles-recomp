using Recompiled;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Sdk;
var failures = 0;
void Check(string name, Action test) {
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch(Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e.Message}"); }
}
void Require(bool condition, string message) { if(!condition) throw new Exception(message); }
foreach (ushort buffer in new ushort[] { 0, 1 })
Check($"Repeated GsSortClear preserves the existing chain, buffer {buffer}", () => {
    var m = new PSMemory();
    var c = new CpuContext { SP=0x801FF000, RA=0x80012AD4 };
    const uint ot=0x8005E428, head=0x8005A804, tail=0x80010000;
    uint clear=0x80097B28u+buffer*16u;
    m.WriteU32(ot+16,head); m.WriteU32(head,tail&0xFFFFFF); m.WriteU32(tail,0xFFFFFF);
    m.WriteU32(clear,0x03FFFFFF); m.WriteU8(clear+7,2);
    m.WriteU16(0x80097BE4,buffer); m.WriteU16(0x80097BE8,256); m.WriteU16(0x80097BEC,240);
    for(int repeat=0;repeat<3;repeat++) {
        c.A0=(uint)(32+repeat);c.A1=64;c.A2=96;c.A3=ot;
        CastlevaniaChronicles_main.GsSortClear(c,m);
        Require(c.SP==0x801FF000 && c.RA==0x80012AD4,"Call frame changed");
        Require((m.ReadU32(head)&0xFFFFFF)==(clear&0xFFFFFF),"Clear is not at the head");
        Require((m.ReadU32(clear)&0xFFFFFF)==(tail&0xFFFFFF),"Repeated insertion destroyed the tail / created a self-cycle");
        Require(m.ReadU8(clear+4)==32+repeat,"Clear color update was lost");
        c.A0=head;LibGpu.DrawOTag(c,m);
    }
});
Check("Ordinary AddPrim still inserts a new primitive", () => {
    var m=new PSMemory();var c=new CpuContext { A0=0x80010000,A1=0x80010020,RA=0x80012340 };
    m.WriteU32(c.A0,0x00FFFFFF);m.WriteU32(c.A1,0x03000000);
    CastlevaniaChronicles_main.AddPrim(c,m);
    Require(m.ReadU32(0x80010000)==0x00010020 && m.ReadU32(0x80010020)==0x03FFFFFF,"Normal insertion changed");
});
Check("Fix does not suppress unrelated or already corrupt insertions", () => {
    foreach(var scenario in new[]{"caller","packet","opcode","self"}) {
        var m=new PSMemory();
        uint packet=scenario=="packet" ? 0x80010020u : 0x80097B28u;
        var c=new CpuContext { A0=0x8005A804,A1=packet,RA=scenario=="caller"?0x80012340u:0x80041230u };
        m.WriteU32(c.A0,packet&0xFFFFFF);
        m.WriteU32(packet,0x03000000u | (scenario=="self" ? packet&0xFFFFFF : 0xFFFFFFu));
        m.WriteU8(packet+7,scenario=="opcode" ? (byte)0 : (byte)2);
        var before=m.Ram.ToArray();
        Require(Chronicles.OrderingTableFix.BeforeAddPrim(c,m),$"Incorrectly suppressed {scenario}");
        Require(before.SequenceEqual(m.Ram.ToArray()),$"Modified RAM for {scenario}");
    }
});
return failures==0?0:1;
