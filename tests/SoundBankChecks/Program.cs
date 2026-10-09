using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using Recompiled;
Chronicles.CodeWarmup.Prepare();
Interrupts.Turbo=true;
var checks=0;
foreach(byte stage in new byte[]{0,1,2,3,4,5,6,7,8})
foreach(byte state in new byte[]{0,1,9,15,18,20,21,22,25}) {
 var m=new PSMemory();var c=new CpuContext{SP=0x801ff000,GP=0x80060000,RA=0x801027EC};
 m.WriteU8(0x8006E45D,1);m.WriteU8(c.GP+0x29,1);m.WriteU16(c.GP+0x314,0xffff);
 m.WriteU8(0x80059286,state);m.WriteU8(0x800592C4,stage);
 c.A0=0;CastlevaniaChronicles_main.VSync(c,m);c.RA=0x801027EC;
 var before=Interrupts.VBlankCount;
 CastlevaniaChronicles_main.func_8001624C(c,m);
 var expected=stage<8 && (state==15||state==20)?1:3;
 if(Interrupts.VBlankCount-before!=expected)throw new Exception($"stage={stage} state={state}: wrong wait");
 if(c.SP!=0x801ff000 || c.RA!=0x801027EC || m.ReadU8(c.GP+0x29)!=0)throw new Exception("Cleanup/ABI changed");
 if(m.ReadU8(0x80059286)!=state || m.ReadU8(0x800592C4)!=stage)throw new Exception("Changed game state");
 checks++;
}
Console.WriteLine($"PASS {checks} real sound-bank-close cases: gameplay wait, other waits, cleanup and ABI");
