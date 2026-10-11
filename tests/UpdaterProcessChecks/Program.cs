using System.Diagnostics;
using System.Text.Json;
using ChroniclesLauncher.Updates;
if(args.Length>0 && args[0]=="--idle-parent"){File.WriteAllText(args[1],"ready");await Task.Delay(Timeout.Infinite);return 0;}
if(args.Length>0 && args[0]=="--hold") {using var lease=InstallationLease.TryAcquire(args[1]);if(lease==null)return 9;File.WriteAllText(args[2],"ready");await Task.Delay(Timeout.Infinite);return 0;}
if(args.Length>0 && args[0]=="--update-ack") {
 UpdateHelper.HandleArguments(args);
 using var launcherLease=InstallationLease.TryAcquireLauncher(InstallationLease.CanonicalRoot(AppContext.BaseDirectory));
 if(launcherLease==null)return 8;
 if(File.Exists(Path.Combine(InstallationLease.CanonicalRoot(AppContext.BaseDirectory),OperatingSystem.IsMacOS()?"Contents/Resources/delay-ack":"delay-ack")))await Task.Delay(10000);
 if(File.Exists(Path.Combine(InstallationLease.CanonicalRoot(AppContext.BaseDirectory),OperatingSystem.IsMacOS()?"Contents/Resources/fail-startup":"fail-startup")))return 7;
 UpdateHelper.Acknowledge(args);await Task.Delay(5000);return 0;
}
if(args.Length>0 && args[0] is "--apply-update" or "--recover-update")return UpdateHelper.HandleArguments(args)??1;
if(args.Length==0)return 0; // A rolled-back fixture starts and exits normally.
if(args[0]!="--suite")return 2;
var scratch=Path.Combine(Path.GetTempPath(),"Chronicles update test % ü "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(scratch);scratch=InstallationLease.CanonicalRoot(scratch);
var self=Environment.ProcessPath!;if(Path.GetFileNameWithoutExtension(self)=="dotnet")throw new Exception("Publish the process fixture first");
Process Start(string exe,params string[] argv){var info=new ProcessStartInfo(exe){UseShellExecute=false};foreach(var a in argv)info.ArgumentList.Add(a);return Process.Start(info)!;}
async Task Until(Func<bool> predicate){using var timeout=new CancellationTokenSource(15000);while(!predicate()){await Task.Delay(25,timeout.Token);}}
async Task MakeInstall(string root,string version,bool broken,bool delay=false){
 var play=UpdateHelper.LauncherPath(root);Directory.CreateDirectory(Path.GetDirectoryName(play)!);File.Copy(self,play,true);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(play,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute|UnixFileMode.GroupRead|UnixFileMode.GroupExecute|UnixFileMode.OtherRead|UnixFileMode.OtherExecute);
 if(OperatingSystem.IsMacOS())File.WriteAllText(Path.Combine(root,"Contents","Info.plist"),"<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>CFBundleExecutable</key><string>Play</string><key>CFBundleIdentifier</key><string>com.spicywonda.updater-test</string><key>CFBundlePackageType</key><string>APPL</string></dict></plist>");
 if(OperatingSystem.IsMacOS())Directory.CreateDirectory(Path.Combine(root,"Contents","Resources"));
 if(delay)File.WriteAllText(Path.Combine(root,OperatingSystem.IsMacOS()?"Contents/Resources/delay-ack":"delay-ack"),"1");
 if(broken)File.WriteAllText(Path.Combine(root,OperatingSystem.IsMacOS()?"Contents/Resources/fail-startup":"fail-startup"),"1");InstallManifest.Create(root,BuildVersion.Rid,Version.Parse(version)).Save(root);
 if(OperatingSystem.IsMacOS()){using var signing=Start("/usr/bin/codesign","--force","--deep","--sign","-",root);await signing.WaitForExitAsync();if(signing.ExitCode!=0)throw new Exception("Fixture signing failed");}
}
try {
 var leaseRoot=Path.Combine(scratch,"lease");Directory.CreateDirectory(leaseRoot);var ready=Path.Combine(scratch,"ready");
 using(var holder=Start(self,"--hold",leaseRoot,ready)){await Until(()=>File.Exists(ready));using var denied=InstallationLease.TryAcquire(leaseRoot);if(denied!=null)throw new Exception("Cross-process lease overlap");holder.Kill();await holder.WaitForExitAsync();}
 using(var released=InstallationLease.TryAcquire(leaseRoot)??throw new Exception("Killed process retained lease")){}
 Console.WriteLine("PASS native exclusive lease and killed-owner release");
 foreach(var scenario in new[]{"upgrade","rollback","interrupted"}){
  bool broken=scenario=="rollback", interrupted=scenario=="interrupted";
  var root=Path.Combine(scratch,scenario+".app");var staged=Path.Combine(scratch,scenario+"-new.app");await MakeInstall(root,"0.3.9",false);await MakeInstall(staged,BuildVersion.Current.ToString(),broken,interrupted);
  if(!OperatingSystem.IsMacOS())File.WriteAllText(Path.Combine(root,"my-card.sav"),"preserve");
  var nonce=Guid.NewGuid().ToString("N");var state=InstallationLease.StateDirectory(root);var tx=Path.Combine(state,"transactions",nonce);Directory.CreateDirectory(tx);
  var parentReady=Path.Combine(scratch,"parent-"+scenario);using var parent=Start(UpdateHelper.LauncherPath(root),"--idle-parent",parentReady);await Until(()=>File.Exists(parentReady));
  var request=new UpdateRequest(root,staged,Path.Combine(scratch,"user data"),tx,parent.Id,parent.StartTime.ToUniversalTime(),BuildVersion.Current.ToString(),nonce);var requestPath=Path.Combine(tx,"request.json");File.WriteAllText(requestPath,JsonSerializer.Serialize(request));File.WriteAllText(Path.Combine(state,"pending.json"),requestPath);
  var helper=Path.Combine(scratch,"helper-"+scenario)+(OperatingSystem.IsWindows()?".exe":"");File.Copy(self,helper);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(helper,File.GetUnixFileMode(self));
  using var update=Start(helper,"--apply-update",requestPath);await Task.Delay(200);if(InstallManifest.Read(root).Version!="0.3.9")throw new Exception("Replaced still-running launcher");parent.Kill();await parent.WaitForExitAsync();using var limit=new CancellationTokenSource(45000);int code;
  if(interrupted){await Until(()=>File.Exists(Path.Combine(tx,"child.json")));update.Kill();await update.WaitForExitAsync();using var recover=Start(helper,"--recover-update",requestPath);await recover.WaitForExitAsync(limit.Token);code=recover.ExitCode;}
  else{await update.WaitForExitAsync(limit.Token);code=update.ExitCode;}
  var expected=broken||interrupted?"0.3.9":BuildVersion.Current.ToString();if(InstallManifest.Read(root).Version!=expected||code!=(broken?1:0)||File.Exists(Path.Combine(state,"pending.json"))){Console.WriteLine(File.ReadAllText(Path.Combine(scratch,"user data","update.log")));throw new Exception($"Native update outcome incorrect: version={InstallManifest.Read(root).Version}, exit={update.ExitCode}");}
  if(!OperatingSystem.IsMacOS()&&File.ReadAllText(Path.Combine(root,"my-card.sav"))!="preserve")throw new Exception("User save changed");
  Console.WriteLine(interrupted?"PASS interrupted helper recovery stops the recorded launcher and restores the old version":broken?"PASS native startup failure restores previous installation":"PASS copied native helper installs and receives startup acknowledgement");
 }
 await Task.Delay(5500); // Fixture acknowledged launchers finish; Windows can then clean their binaries.
}finally {try{Directory.Delete(scratch,true);}catch(IOException){}}
return 0;
