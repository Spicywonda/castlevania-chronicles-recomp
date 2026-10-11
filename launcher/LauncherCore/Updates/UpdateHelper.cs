using System.Diagnostics;
using System.Text.Json;
namespace ChroniclesLauncher.Updates;
public static class UpdateHelper {
 private static string Pending(string root)=>Path.Combine(InstallationLease.StateDirectory(root),"pending.json");
 public static string FailedVersion(string root)=>Path.Combine(InstallationLease.StateDirectory(root),"failed-version.txt");
 public static string LauncherPath(string root)=>OperatingSystem.IsMacOS()?Path.Combine(root,"Contents","MacOS","Play"):Path.Combine(root,OperatingSystem.IsWindows()?"Play.exe":"Play");
 public static bool Start(StagedUpdate staged,string root,string data){
  root=InstallationLease.CanonicalRoot(root);var state=InstallationLease.StateDirectory(root);
  if(File.Exists(Pending(root)))throw new IOException("An update is already pending. Restart the launcher to recover it.");
  var nonce=Guid.NewGuid().ToString("N");var tx=Path.Combine(state,"transactions",nonce);Directory.CreateDirectory(tx);
  using var current=Process.GetCurrentProcess();
  var request=new UpdateRequest(root,staged.Root,Path.GetFullPath(data),tx,current.Id,current.StartTime.ToUniversalTime(),staged.Release.Version.ToString(),nonce);
  var requestPath=Path.Combine(tx,"request.json");File.WriteAllText(requestPath,JsonSerializer.Serialize(request));
  CopyHelper(tx);File.WriteAllText(Pending(root),requestPath);
  try{LaunchHelper(requestPath,false,tx);return true;}catch{File.Delete(Pending(root));throw;}
 }
 private static void CopyHelper(string tx){
  var source=Environment.ProcessPath??throw new IOException("Launcher executable could not be located.");
  // A framework-dependent dotnet invocation is a development build, not an installable launcher.
  if(Path.GetFileNameWithoutExtension(source).Equals("dotnet",StringComparison.OrdinalIgnoreCase))throw new IOException("Automatic installation requires a packaged launcher.");
  var dest=Path.Combine(tx,OperatingSystem.IsWindows()?"UpdateHelper.exe":"UpdateHelper");File.Copy(source,dest,true);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(dest,File.GetUnixFileMode(source));
 }
 private static void LaunchHelper(string requestPath,bool recovery,string tx){var info=new ProcessStartInfo(Path.Combine(tx,OperatingSystem.IsWindows()?"UpdateHelper.exe":"UpdateHelper")){UseShellExecute=false,WorkingDirectory=tx,CreateNoWindow=true};info.ArgumentList.Add(recovery?"--recover-update":"--apply-update");info.ArgumentList.Add(requestPath);Process.Start(info)?.Dispose();}
 public static int? HandleArguments(string[] args){
  if(args.Length==1 && args[0]=="--updater-self-test")return 0;
  if(args.Length==2 && args[0] is "--apply-update" or "--recover-update"){
   var incoming=ReadRequest(args[1]);return RunAsync(incoming,args[0]=="--recover-update",default).GetAwaiter().GetResult();
  }
  if(args.Length>0&&args[0]=="--update-ack")return null;
  var root=InstallationLease.CanonicalRoot(AppContext.BaseDirectory);var pending=Pending(root);
  if(!File.Exists(pending))return null;
  var requestPath=File.ReadAllText(pending);var old=ReadRequest(requestPath);if(old.InstallationRoot!=root)throw new InvalidDataException("Recovery installation mismatch.");
  // Another helper may still be waiting for startup acknowledgement. Do not race it.
  using(var lease=InstallationLease.TryAcquire(root)){if(lease==null)throw new IOException("An update is in progress. Try again shortly.");}
  using var self=Process.GetCurrentProcess();var request=old with{ParentPid=self.Id,ParentStartUtc=self.StartTime.ToUniversalTime()};File.WriteAllText(requestPath,JsonSerializer.Serialize(request));
  CopyHelper(request.TransactionDirectory);LaunchHelper(requestPath,true,request.TransactionDirectory);return 0;
 }
 private static UpdateRequest ReadRequest(string path){
  if(new FileInfo(path).Length>16384)throw new InvalidDataException("Update request is too large.");
  var r=JsonSerializer.Deserialize<UpdateRequest>(File.ReadAllText(path))??throw new InvalidDataException("Invalid update request.");
  var tx=Path.Combine(InstallationLease.StateDirectory(r.InstallationRoot),"transactions",r.Nonce);
  if(!Guid.TryParseExact(r.Nonce,"N",out _)||r.TransactionDirectory!=tx||Path.GetFullPath(path)!=Path.Combine(tx,"request.json")||r.InstallationRoot!=InstallationLease.CanonicalRoot(r.InstallationRoot))throw new InvalidDataException("Unexpected update transaction location.");return r;
 }
 public static void Acknowledge(string[] args){
  if(args.Length!=2||args[0]!="--update-ack")return;var r=ReadRequest(args[1]);
  if(r.Version!=BuildVersion.Current.ToString()||r.InstallationRoot!=InstallationLease.CanonicalRoot(AppContext.BaseDirectory))throw new InvalidDataException("Update startup identity mismatch.");
  File.WriteAllText(Path.Combine(r.TransactionDirectory,"ack"),r.Nonce+"\n"+r.Version);
 }
 public static async Task<int> RunAsync(UpdateRequest request,bool recovery,CancellationToken token){
  var root=request.InstallationRoot;var logPath=Path.Combine(request.DataDirectory,"update.log");Directory.CreateDirectory(request.DataDirectory);
  void Log(string line)=>File.AppendAllText(logPath,$"{DateTimeOffset.UtcNow:O} {line}\n");
  UpdateTransaction? tx=null;Process? child=null;IDisposable? lease=null;
  try{
   try{using var parent=Process.GetProcessById(request.ParentPid);if(Math.Abs((parent.StartTime.ToUniversalTime()-request.ParentStartUtc).TotalSeconds)<1){using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(30));await parent.WaitForExitAsync(timeout.Token);}}catch(ArgumentException){}
   lease=InstallationLease.TryAcquire(root)??throw new IOException("Game or updater is still using this installation.");
   var journal=Path.Combine(request.TransactionDirectory,"journal.json");
   if(recovery){if(File.Exists(journal)){tx=UpdateTransaction.Load(journal,root);tx.Rollback();}File.WriteAllText(FailedVersion(root),request.Version);File.Delete(Pending(root));Log("Previous installation recovered.");StartLauncher(request,false)?.Dispose();return 0;}
   if(request.Version==FileRead(FailedVersion(root)))throw new IOException("This update previously failed. Use Retry from the launcher.");
   if(OperatingSystem.IsMacOS())await MacAppSwap.VerifyAsync(request.StagingRoot,token);
   tx=UpdateTransaction.Prepare(request);Log("Prepared update "+request.Version);tx.Apply();Log("Installed; waiting for launcher acknowledgement.");
   var ack=Path.Combine(request.TransactionDirectory,"ack");if(File.Exists(ack))File.Delete(ack);
   child=StartLauncher(request,true)??throw new IOException("Updated launcher could not be started.");
   var timer=Stopwatch.StartNew();bool acknowledged=false;
   while(timer.Elapsed<TimeSpan.FromSeconds(30)&&!child.HasExited){token.ThrowIfCancellationRequested();if(FileRead(ack)==request.Nonce+"\n"+request.Version){acknowledged=true;break;}await Task.Delay(100,token);}
   if(!acknowledged)throw new IOException("Updated launcher did not confirm startup.");
   tx.Commit();File.Delete(Pending(root));Log("Update completed.");return 0;
  }catch(Exception e){
   Log("Update failed: "+e);
   if(child is {HasExited:false}){child.Kill(true);await child.WaitForExitAsync(CancellationToken.None);}
   try{if(tx!=null)tx.Rollback();File.WriteAllText(FailedVersion(root),request.Version);File.Delete(Pending(root));Log("Previous version retained/restored.");StartLauncher(request,false)?.Dispose();}catch(Exception rollback){Log("Recovery required: "+rollback);}
   return 1;
  }finally{child?.Dispose();lease?.Dispose();}
 }
 private static Process? StartLauncher(UpdateRequest r,bool acknowledge){var info=new ProcessStartInfo(LauncherPath(r.InstallationRoot)){UseShellExecute=false,WorkingDirectory=r.InstallationRoot};info.Environment["CHRONICLES_DATA_DIR"]=r.DataDirectory;if(acknowledge){info.ArgumentList.Add("--update-ack");info.ArgumentList.Add(Path.Combine(r.TransactionDirectory,"request.json"));}return Process.Start(info);}
 private static string? FileRead(string path){try{return File.Exists(path)?File.ReadAllText(path):null;}catch(IOException){return null;}}
}
