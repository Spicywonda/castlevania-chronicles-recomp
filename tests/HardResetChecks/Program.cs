using System.Reflection;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Host;
using RecompOne.Runtime.Interp;
using GameRuntime = RecompOne.Runtime.Runtime;

var failures=0;
void Check(string name, Action test) {
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch(Exception e) { failures++; Console.Error.WriteLine($"FAIL {name}: {e.Message}"); }
}
void Require(bool ok,string message) { if(!ok) throw new Exception(message); }
GpuJobs.Claim();
void ResetFromGameThread() {
    Exception? error=null;
    var thread=new Thread(()=> {
        try { typeof(GameRuntime).GetMethod("ResetForBoot",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null); }
        catch(Exception e) { error=e; }
    }) { IsBackground=true };
    thread.Start();
    var watch=System.Diagnostics.Stopwatch.StartNew();
    while(thread.IsAlive && watch.ElapsedMilliseconds<5000) { GpuJobs.Drain(); Thread.Sleep(1); }
    Require(!thread.IsAlive,"Reset did not complete while the graphics owner drained jobs");
    if(error!=null) throw error;
}
foreach(var interpolation in new[]{false,true})
Check($"Reset runs graphics on the owner thread (interpolation={interpolation})",()=> {
    var inner=new RecordingBackend();
    GpuHle.Backend=interpolation ? new InterpBackend(inner) : inner;
    if(GpuHle.Backend is InterpBackend interp) { interp.FillRect(0,0,1,1,99); interp.Publish(); interp.Acquire(); }
    ResetFromGameThread();
    Require(inner.WrongThread==0,"Graphics calls occurred on the game thread");
});
Check("Repeated resets discard previous/current/queued/unfinished frames and accept new frames",()=> {
    var inner=new RecordingBackend(); var interp=new InterpBackend(inner); GpuHle.Backend=interp;
    for(int reset=0;reset<3;reset++) {
        interp.FillRect(0,0,1,1,1);interp.Publish();interp.Acquire();
        interp.FillRect(0,0,1,1,2);interp.Publish();interp.Acquire();
        interp.FillRect(0,0,1,1,3);interp.Publish();
        interp.FillRect(0,0,1,1,4);
        inner.Colors.Clear(); ResetFromGameThread();
        Require(!interp.Acquire(),"Old queued frame survived reset");
        interp.Compose(0);interp.EndPresent();interp.Publish();interp.Acquire();interp.Compose(0);interp.EndPresent();
        Require(inner.Colors.SequenceEqual(new ushort[]{0}),"Old draws replayed or full VRAM clear was not issued");
        Require(inner.LastClear==(0,0,1024,512),"Reset did not clear the complete VRAM");
        interp.FillRect(0,0,1,1,7);interp.Publish();Require(interp.Acquire(),"New boot could not publish a frame");
        interp.BeginPresent();interp.Compose(0);interp.EndPresent();
        Require(inner.Colors.Last()==7,"New frame was not rendered");
    }
});
return failures==0?0:1;

sealed class RecordingBackend : IGpuBackend {
    public bool Ready=>true;
    public int WrongThread;
    public List<ushort> Colors=new();
    public (int,int,int,int) LastClear;
    void Touch() { if(!GpuJobs.IsOwner) Interlocked.Increment(ref WrongThread); }
    public void SetDrawEnv(in HleDrawEnv e)=>Touch();
    public void DrawTri(in HleVertex a,in HleVertex b,in HleVertex c,in PrimFlags f)=>Touch();
    public void DrawRect(in HleRect r,in PrimFlags f)=>Touch();
    public void DrawLine(in HleVertex a,in HleVertex b,in PrimFlags f)=>Touch();
    public void FillRect(int x,int y,int w,int h,ushort color) { Touch(); Colors.Add(color); LastClear=(x,y,w,h); }
    public void CopyVram(int sx,int sy,int dx,int dy,int w,int h)=>Touch();
    public void WriteVram(int x,int y,int w,int h,ReadOnlySpan<ushort> p)=>Touch();
    public void ReadVram(int x,int y,int w,int h,Span<ushort> p)=>Touch();
    public int RegisterImage(ReadOnlySpan<byte> p,int w,int h) { Touch(); return 0; }
    public void Flush()=>Touch();
    public void Present(in HleDispEnv d)=>Touch();
}
