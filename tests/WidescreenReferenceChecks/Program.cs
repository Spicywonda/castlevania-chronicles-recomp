using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Hle;
using Recompiled;
using Chronicles;
Interrupts.Turbo=true;
int cases=0;
foreach(int extent in new[]{31,32})
foreach(int scroll in new[]{96,97,127,128})
foreach(ushort flags in new ushort[]{0,1,2,3}) {
 const uint bg=0x80010000, table=0x80011000, map=0x80012000, tiles=0x80013000, packet=0x80016000;
 PSMemory Make(int sx){
  var m=new PSMemory();m.WriteU8(0x80059286,15);
  m.WriteU16(bg+6,16);m.WriteU16(bg+12,(ushort)sx);m.WriteU16(bg+14,5);m.WriteU32(bg+16,0x00808080);m.WriteU32(bg+20,map);
  m.WriteU16(map+2,24);m.WriteU16(map+4,12);m.WriteU32(map+8,tiles);
  for(uint i=0;i<24*12;i++){uint t=tiles+i*8;m.WriteU8(t,(byte)(i*3));m.WriteU8(t+1,(byte)(i*7));m.WriteU16(t+2,0x3c);m.WriteU16(t+4,flags);m.WriteU16(t+6,4);}
  m.WriteU32(table+4,0x80015000);m.WriteU32(0x80015008,0xffffff);m.WriteU32(0x80097ca8,packet);return m;
 }
 CpuContext Cpu()=>new(){A0=bg,A1=table,A2=2,A3=28,RA=0x80101670,SP=0x801ff000};
 int first=(scroll-43)>>5, referenceScroll=first*32;
 var actual=Make(scroll);Display.WideAspect=16f/9f;Widescreen.Enabled=true;
 if(extent==31)TileMargins.Before31(Cpu(),actual);else TileMargins.Before32(Cpu(),actual);
 TileMargins.After(Cpu(),actual);
 var reference=Make(referenceScroll);Widescreen.Enabled=false;
 if(extent==31)CastlevaniaChronicles_main.func_8004E480(Cpu(),reference);else CastlevaniaChronicles_main.func_8004E8B8(Cpu(),reference);
 foreach(uint offset in new uint[]{8,16,24,32}) {
  int expected=(short)reference.ReadU16(packet+offset)-(scroll-referenceScroll);
  if((short)actual.ReadU16(packet+offset)!=expected)throw new Exception($"X mismatch {extent}/{scroll}/{flags}");
 }
 for(uint offset=4;offset<40;offset++){
  if(new uint[]{8,9,16,17,24,25,32,33}.Contains(offset))continue;
  if(actual.ReadU8(packet+offset)!=reference.ReadU8(packet+offset))throw new Exception($"Packet mismatch {extent}/{scroll}/{flags} byte {offset}: {actual.ReadU8(packet+offset)} vs {reference.ReadU8(packet+offset)}");
 }
 cases++;
}
Console.WriteLine($"PASS {cases} margin packets match original game routines, including scrolling and all tile flips");
SpriteEdgeChecks.Run();

RoomEdgeChecks.Run();

ViewportChecks.Run();

ParallaxChecks.Run();
