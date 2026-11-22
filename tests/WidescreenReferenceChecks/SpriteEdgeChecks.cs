using Chronicles;
using Recompiled;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Memory;
static class SpriteEdgeChecks {
 public static void Run(){
  int count=0;
  foreach(bool wide in new[]{false,true})
  foreach(byte flip in new byte[]{0,1})
  foreach(byte state in new byte[]{8,15,20,25})
  foreach(int x in new[]{-60,-32,0,128,280,315,316}) {
   var m=new PSMemory();const uint actor=0x8005a848,table=0x80011000,packet=0x80016000;
   m.WriteU8(actor+6,flip);m.WriteU8(0x80059286,state);m.WriteU32(actor+0x58,unchecked((uint)(x*256)));m.WriteU32(actor+0x5c,128*256);
   m.WriteU32(table+4,0x80015000);m.WriteU32(0x80015034,0xffffff);m.WriteU32(0x80097ca8,packet);
   var before=m.Ram.Slice((int)(actor&0x1fffff),0xb4).ToArray();
   Widescreen.Enabled=wide;Display.WideAspect=16f/9f; // Previous frame can still be wide during loading.
   var c=new CpuContext{A0=table,A1=actor,SP=0x801ff000,RA=0x80100000};
   CastlevaniaChronicles_stage1.func_80106E3C(c,m);
   bool expected=wide && state is 15 or 20 ? x>=-59&&x<316 : x>=-16&&x<273;
   bool drawn=m.ReadU32(0x80097ca8)>packet;
   if(drawn!=expected)throw new Exception($"Sprite edge visibility mismatch: wide={wide}, state={state}, x={x}, drawn={drawn}");
   if(!before.SequenceEqual(m.Ram.Slice((int)(actor&0x1fffff),0xb4).ToArray()))throw new Exception("Drawing changed actor state");
   if(c.SP!=0x801ff000 || c.RA!=0x80100000)throw new Exception("Drawing changed calling convention");
   count++;
  }
  Console.WriteLine($"PASS {count} real sprite draw cases: wide edges, 4:3, transitions and actor state preservation");
 }
}
