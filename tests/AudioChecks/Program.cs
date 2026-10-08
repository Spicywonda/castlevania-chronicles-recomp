using Recompiled;
using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
var failures=0;
foreach (var length in new[] {16, 80})
{
    var m=new PSMemory();
    var c=new CpuContext {SP=0x801FF000,RA=0x80012340,A0=0x80010000,A1=(uint)length};
    Runtime.SetContext(c,m);
    m.WriteU32(0x800508D4,0x1F801C00);
    m.WriteU16(0x800508EC,0x200);
    for (var i=0;i<length;i++) m.WriteU8(0x80010000u+(uint)i,(byte)(i==1 ? 7 : i*3));
    CastlevaniaChronicles_main.func_8002D630(c,m);
    var exact=Enumerable.Range(0,length).All(i=>Runtime.Spu!.Ram[0x1000+i]==m.ReadU8(0x80010000u+(uint)i));
    if (exact && c.SP==0x801FF000 && c.RA==0x80012340)
        Console.WriteLine($"PASS original SPU manual transfer driver, {length} bytes");
    else { failures++; Console.Error.WriteLine($"FAIL original SPU manual transfer driver, {length} bytes"); }
}
return failures==0 ? 0 : 1;
