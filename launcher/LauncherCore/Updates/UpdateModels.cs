using System.Runtime.InteropServices;
namespace ChroniclesLauncher.Updates;
public static class BuildVersion {
 public static Version Current => new(typeof(BuildVersion).Assembly.GetName().Version!.ToString(3));
 public static string Rid => (OperatingSystem.IsWindows(),OperatingSystem.IsMacOS(),RuntimeInformation.ProcessArchitecture) switch {
  (true,_,Architecture.X64)=>"win-x64",(_,true,Architecture.Arm64)=>"osx-arm64",(false,false,Architecture.X64) when OperatingSystem.IsLinux()=>"linux-x64",_=>throw new PlatformNotSupportedException("Automatic updates are unavailable for this architecture.")};
}
public sealed record UpdateRelease(Version Version,string Tag,string Rid,Uri PackageUrl,long Size,string Sha256,Uri ReleaseUrl);
public sealed record UpdateProgress(string Phase,long Completed,long? Total);
public enum UpdatePhase {Idle,Checking,Downloading,Ready,Deferred,Installing,Failed,UpToDate}
public sealed record UpdateStatus(UpdatePhase Phase,string Message,UpdateProgress? Progress=null);
