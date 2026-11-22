using System.Reflection;
using Chronicles;
using Recompiled;
using Silk.NET.OpenGL;
using RecompOne.Runtime;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Dispatch;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Host;
using RecompOne.Runtime.Host.Window;
using RecompOne.Runtime.Hle;
using GameRuntime = RecompOne.Runtime.Runtime;
if (args.Length is < 3 or > 4) throw new ArgumentException("Supply a local RAM capture, VRAM capture and output directory. Captures must not be committed.");
byte[] ram = File.ReadAllBytes(args[0]), vram = File.ReadAllBytes(args[1]);
string output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
ConfigManager.Load(); ConfigManager.Game.Muted = true; ConfigManager.View.RenderScale = 1;
int failures = 0;
GameRuntime.Run(() => {
 try {
  Interrupts.Turbo = true;
  var m = new PSMemory(); ram.CopyTo((byte[])typeof(PSMemory).GetField("_ram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(m)!, 0);
  var c = new CpuContext { GP = 0x80059080, SP = 0x801ff000, RA = 0x80100000 };
  GameRuntime.SetContext(c,m); GameRuntime.Initialize("Widescreen scene verification");
  Dispatcher.Register("stage1", new Stage1DispatchTable()); Dispatcher.Load("stage1");
  GpuJobs.Run(() => {
   var core = (GlCore)typeof(HostWindow).GetField("_glBackend", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
   GpuHle.Backend = core; GpuHle.Active = true;
   var gl = (GL)typeof(GlCore).GetField("_gl", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(core)!;
   byte[]? baseline = null;
   foreach (int camera in args.Length == 4 ? args[3].Split(',').Select(int.Parse).ToArray() : new[]{0,400,768})
   foreach (bool wide in new[]{false,true}) {
    ram.CopyTo((byte[])typeof(PSMemory).GetField("_ram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(m)!, 0);
    m.WriteU16(0x80059248, (ushort)camera);
    System.Buffer.BlockCopy(vram,0,GameRuntime.Gpu!.Vram,0,vram.Length);
    core.WriteVram(0,0,1024,512,GameRuntime.Gpu.Vram);
    Widescreen.Enabled = wide; Display.WideAspect = wide ? 16f/9f : 0;
    GpuHle.SourceAspect = 4f/3f; GpuHle.OutputAspect = 4f/3f;
    GpuHle.NotifyDisplay(0,0,256,240);
    const uint table = 0x801ef000, ot = 0x801ef100, arena = 0x801c0000;
    m.WriteU32(table,5); m.WriteU32(table+4,ot);
    for(uint i=0;i<32;i++) m.WriteU32(ot+i*4,i==0?0xffffff:(ot+i*4-4)&0xffffff);
    m.WriteU32(0x80097ca8,arena);
    c.A0=table;c.A1=0xffffffff;CastlevaniaChronicles_main.func_8001E0F0(c,m);
    c.A0=table;CastlevaniaChronicles_main.func_80022118(c,m);
    c.A0=table;CastlevaniaChronicles_stage1.func_80101268_stage1(c,m);
    c.A0=table;c.A1=16;CastlevaniaChronicles_main.func_8001771C(c,m);
    if (!wide) baseline = m.Ram.ToArray();
    else {
     for (int i=0;i<0x1c0000;i++) {
      if (i>=0x97ca8 && i<0x97cac) continue;
      if (m.Ram[i] != baseline![i]) throw new Exception($"Widescreen changed non-packet RAM at {i:X}: camera={camera}");
     }
     Console.WriteLine($"PASS identical game RAM below the scratch packet arena at camera {camera}");
    }
    var gpu=GameRuntime.Gpu;
    gpu.WriteGp0(0xe3000000);gpu.WriteGp0(0xe4000000 | 255 | (239u<<10));gpu.WriteGp0(0xe5000000);
    int nodes=0;
    for(uint p=ot+31*4; p!=0xffffff;){
     if(++nodes>10000)throw new Exception("Invalid ordering chain");
     uint tag=m.ReadU32(p),n=tag>>24;
     if(n>0){var words=new uint[n];for(uint i=0;i<n;i++)words[i]=m.ReadU32(p+4+i*4);gpu.WriteGp0Packet(words,p+4);}
     p=tag&0xffffff;
    }
    core.Flush();var frame=core.PresentDisplay(0,0,256,240,false);
    byte[] pixels=new byte[frame.w*frame.h*4];gl.BindTexture(TextureTarget.Texture2D,frame.tex);
    gl.GetTexImage<byte>(TextureTarget.Texture2D,0,PixelFormat.Rgba,PixelType.UnsignedByte,pixels.AsSpan());
    string path=Path.Combine(output,camera.ToString()+ (wide?"-wide.ppm":"-original.ppm"));
    using(var f=File.Create(path)) {byte[] header=System.Text.Encoding.ASCII.GetBytes($"P6\n{frame.w} {frame.h}\n255\n");f.Write(header);for(int i=0;i<pixels.Length;i+=4)f.Write(pixels,i,3);}
    Console.WriteLine($"Rendered {path}: {frame.w}x{frame.h}, {nodes} ordering nodes");
   }
  });
 } catch(Exception e){failures++;Console.Error.WriteLine(e);}
});
return failures==0?0:1;
