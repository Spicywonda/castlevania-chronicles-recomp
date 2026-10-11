namespace ChroniclesLauncher.Updates;
public sealed class UpdateCoordinator {
 private readonly Func<CancellationToken,Task<UpdateRelease?>> check;
 private readonly Func<UpdateRelease,IProgress<UpdateProgress>,CancellationToken,Task<StagedUpdate>> stage;
 private readonly Func<StagedUpdate,bool> install;
 private readonly Action? retry;
 private readonly object sync=new();private bool deferred,running,busy;private StagedUpdate? staged;
 private volatile UpdateStatus status=new(UpdatePhase.Idle,"Updates");
 public UpdateStatus Status=>status;
 public DateTimeOffset? ReadySince {get;private set;}
 public UpdateCoordinator(Func<CancellationToken,Task<UpdateRelease?>> check,Func<UpdateRelease,IProgress<UpdateProgress>,CancellationToken,Task<StagedUpdate>> stage,Func<StagedUpdate,bool> install,Action? retry=null){this.check=check;this.stage=stage;this.install=install;this.retry=retry;}
 public static UpdateCoordinator Create(HttpClient client,string root,string data){
  var cache=Path.Combine(InstallationLease.StateDirectory(root),"cache");var releases=new ReleaseClient(client,cache);var packages=new PackageStager(client);
  return new(async token=>{var r=await releases.CheckAsync(BuildVersion.Current,BuildVersion.Rid,token);if(r!=null&&File.Exists(UpdateHelper.FailedVersion(root))&&File.ReadAllText(UpdateHelper.FailedVersion(root))==r.Version.ToString())throw new IOException("The last update failed. Use Retry to try again.");return r;},(r,p,t)=>packages.StageAsync(r,cache,p,t),s=>UpdateHelper.Start(s,root,data),()=>File.Delete(UpdateHelper.FailedVersion(root)));
 }
 public async Task StartAsync(CancellationToken token){
  lock(sync){if(busy||status.Phase==UpdatePhase.Installing)return;busy=true;status=new(UpdatePhase.Checking,"Checking for updates...");}
  try{
   var release=await check(token);if(release==null){status=new(UpdatePhase.UpToDate,"You're up to date.");return;}
   status=new(UpdatePhase.Downloading,$"Downloading {release.Version}...");
   var progress=new InlineProgress(p=>{lock(sync){if(status.Phase==UpdatePhase.Downloading)status=new(UpdatePhase.Downloading,p.Phase,p);}});
   var result=await stage(release,progress,token);
   lock(sync){staged=result;status=new(deferred?UpdatePhase.Deferred:UpdatePhase.Ready,deferred?"Update postponed for this session.":$"Version {release.Version} is ready.");ReadySince=!deferred&&!running?DateTimeOffset.UtcNow:null;}
  }catch(OperationCanceledException) when(token.IsCancellationRequested){status=new(UpdatePhase.Idle,"Update check cancelled.");}
   catch(Exception e){status=new(UpdatePhase.Failed,e is OperationCanceledException?"Update check timed out. You can still play.":e.Message);}
  finally{lock(sync)busy=false;}
 }
 public void Defer(){lock(sync){if(status.Phase==UpdatePhase.Installing)return;deferred=true;ReadySince=null;if(staged!=null)status=new(UpdatePhase.Deferred,"Update postponed for this session.");}}
 public async Task RetryAsync(CancellationToken token){lock(sync){if(busy||status.Phase==UpdatePhase.Installing)return;deferred=false;staged=null;retry?.Invoke();}await StartAsync(token);}
 public void SetGameRunning(bool value){lock(sync){if(running==value)return;running=value;ReadySince=!running&&!deferred&&status.Phase==UpdatePhase.Ready?DateTimeOffset.UtcNow:null;}}
 public async Task<bool> InstallIfReadyAsync(CancellationToken token){StagedUpdate? next;lock(sync){if(busy||running||deferred||status.Phase!=UpdatePhase.Ready||staged==null)return false;next=staged;status=new(UpdatePhase.Installing,"Installing update. The launcher will restart...");ReadySince=null;}
  try{token.ThrowIfCancellationRequested();return await Task.Run(()=>install(next),token);}catch(Exception e){status=new(UpdatePhase.Failed,e.Message);return false;}
 }
 private sealed class InlineProgress(Action<UpdateProgress> report):IProgress<UpdateProgress>{public void Report(UpdateProgress value)=>report(value);}
}
