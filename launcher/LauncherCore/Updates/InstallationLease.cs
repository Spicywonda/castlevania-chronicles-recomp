using System.Security.Cryptography;
using System.Text;
namespace ChroniclesLauncher.Updates;
public static class InstallationLease {
 public static string CanonicalRoot(string path){
  path=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));if(File.Exists(path))path=Path.GetDirectoryName(path)!;
  if(Path.GetFileName(path)=="runtime")path=Path.GetDirectoryName(path)!;
  if(Path.GetFileName(path)=="MacOS"&&Path.GetFileName(Path.GetDirectoryName(path))=="Contents")path=Path.GetDirectoryName(Path.GetDirectoryName(path))!;
  var parts=path[Path.GetPathRoot(path)!.Length..].Split(Path.DirectorySeparatorChar,StringSplitOptions.RemoveEmptyEntries);var resolved=Path.GetPathRoot(path)!;
  foreach(var part in parts){resolved=Path.Combine(resolved,part);var info=new DirectoryInfo(resolved);if(info.Exists&&info.LinkTarget!=null)resolved=info.ResolveLinkTarget(true)!.FullName;}
  return Path.TrimEndingDirectorySeparator(resolved);
 }
 public static string StateDirectory(string installationRoot){
  var canonical=CanonicalRoot(installationRoot);if(OperatingSystem.IsWindows())canonical=canonical.ToUpperInvariant();
  var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
  var basePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChroniclesUpdater",key);Directory.CreateDirectory(basePath);return basePath;
 }
 public static IDisposable? TryAcquireLauncher(string root)=>Acquire(root,"launcher.lock");
 public static IDisposable? TryAcquire(string installationRoot)=>Acquire(installationRoot,"installation.lock");
 private static IDisposable? Acquire(string installationRoot,string name){
  var file=Path.Combine(StateDirectory(installationRoot),name);
  try{return new FileStream(file,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){return null;}
 }
}
