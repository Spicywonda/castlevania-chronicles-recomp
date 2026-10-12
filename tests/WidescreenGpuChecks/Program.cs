using System.Reflection;
using Silk.NET.OpenGL;
using RecompOne.Runtime;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Host;
using RecompOne.Runtime.Host.Window;
using RecompOne.Runtime.Hle;
using GameRuntime = RecompOne.Runtime.Runtime;
ConfigManager.Load();ConfigManager.Game.Muted=true;ConfigManager.View.RenderScale=1;
int failures=0;
GameRuntime.Run(()=>{
 try {
  GameRuntime.SetContext(new CpuContext(),new PSMemory());
  GameRuntime.Initialize("Widescreen transition checks");
  GpuJobs.Run(()=>{
   var core=(GlCore)typeof(HostWindow).GetField("_glBackend",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
   GpuHle.SourceAspect=4f/3f;GpuHle.OutputAspect=4f/3f;GpuHle.WideAspect=16f/9f;
   var surface=(GlDisplayRt)typeof(GlCore).GetMethod("GetOrCreateRt",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(core,new object[]{0,0,256,240})!;
   var gl=(GL)typeof(GlCore).GetField("_gl",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(core)!;
   var pixels=new byte[342*240*4];
   for(int y=0;y<240;y++)for(int x=0;x<342;x++){
    int i=(y*342+x)*4;pixels[i+(x<43?0:x<299?1:2)]=255;pixels[i+3]=255;
   }
   gl.BindTexture(TextureTarget.Texture2D,surface.Tex);
   gl.TexSubImage2D<byte>(TextureTarget.Texture2D,0,0,0,342,240,PixelFormat.Rgba,PixelType.UnsignedByte,pixels.AsSpan());
   var wide=core.PresentDisplay(0,0,256,240,false);
   if(wide.w!=342 || Math.Abs(wide.aspect-16f/9f)>.001f)throw new Exception("Wide reference was not created");
   GpuHle.WideAspect=0;
   var normal=core.PresentDisplay(0,0,256,240,false);
   if(normal.w!=256 || Math.Abs(normal.aspect-4f/3f)>.001f)throw new Exception($"Stale wide surface leaked into 4:3: width={normal.w}, aspect={normal.aspect}");
   var cropped=new byte[256*240*4];gl.BindTexture(TextureTarget.Texture2D,normal.tex);
   gl.GetTexImage<byte>(TextureTarget.Texture2D,0,PixelFormat.Rgba,PixelType.UnsignedByte,cropped.AsSpan());
   for(int i=0;i<cropped.Length;i+=4)if(cropped[i]!=0||cropped[i+1]!=255||cropped[i+2]!=0)
    throw new Exception($"Wrong center crop at pixel {i/4}: {cropped[i]},{cropped[i+1]},{cropped[i+2]}");
   Console.WriteLine("PASS Actual GPU pixels preserve the center image and exclude both old side margins");
   Console.WriteLine("PASS Wide-to-normal presentation crops old side surfaces and preserves 4:3 without a redraw");
  });
 } catch(Exception e){failures++;Console.Error.WriteLine(e);}
});
return failures==0?0:1;
