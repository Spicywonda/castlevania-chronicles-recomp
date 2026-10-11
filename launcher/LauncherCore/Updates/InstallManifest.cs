using System.Security.Cryptography;
using System.Text.Json;
namespace ChroniclesLauncher.Updates;
public sealed record OwnedFile(string Path,long Size,string Sha256,int UnixMode);
public sealed record InstallManifest(int Schema,string Version,string Rid,IReadOnlyList<OwnedFile> Files) {
 public const string FileName="install-manifest.json";
 public void Save(string root)=>File.WriteAllText(System.IO.Path.Combine(root,FileName),JsonSerializer.Serialize(this));
 public static InstallManifest Create(string root,string rid,Version version)=>new(1,version.ToString(),rid,
  Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Select(p=>new OwnedFile(System.IO.Path.GetRelativePath(root,p).Replace('\\','/'),new FileInfo(p).Length,Hash(p),OperatingSystem.IsWindows()?0:(int)File.GetUnixFileMode(p))).Where(f=>f.Path!=FileName && !f.Path.StartsWith("Contents/_CodeSignature/",StringComparison.Ordinal)).ToArray());
 public static string Hash(string path){using var input=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();}
 public static string SafePath(string root,string relative){
  if(string.IsNullOrEmpty(relative)||relative.Length>1024||relative.Contains('\\')||relative.Contains(':')||relative.Contains('\0')||System.IO.Path.IsPathRooted(relative)||relative.Split('/').Any(p=>p is "" or "." or ".."||p.EndsWith(' ')||p.EndsWith('.')||p.Any(char.IsControl)))throw new InvalidDataException("Unsafe update path.");
  var destination=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative));
  if(!destination.StartsWith(System.IO.Path.GetFullPath(root)+System.IO.Path.DirectorySeparatorChar,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new InvalidDataException("Update path escaped its root.");
  for(var p=destination;p!=System.IO.Path.GetFullPath(root);p=System.IO.Path.GetDirectoryName(p)!){if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Update path contains a link.");}
  return destination;
 }
 public static InstallManifest Read(string root){
  var path=System.IO.Path.Combine(root,FileName);if(new FileInfo(path).Length>8*1024*1024)throw new InvalidDataException("Manifest is too large.");
  var m=JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(path))??throw new InvalidDataException("Missing manifest.");
  if(m.Schema!=1||m.Files is null||m.Files.Count is <1 or >20000||!System.Version.TryParse(m.Version,out _)||m.Rid is not("win-x64" or "linux-x64" or "osx-arm64"))throw new InvalidDataException("Unsupported install manifest.");
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var f in m.Files){SafePath(root,f.Path);if(!names.Add(f.Path)||f.Path==FileName||f.Size<0||f.Size>512L*1024*1024||f.Sha256.Length!=64||f.Sha256.Any(c=>!Uri.IsHexDigit(c))||(f.UnixMode&~0x1ff)!=0||IsUserData(f.Path))throw new InvalidDataException("Invalid managed file.");}
  return m;
 }
 public static bool IsUserData(string path)=>path.Split('/').Any(p=>p.Equals("packs",StringComparison.OrdinalIgnoreCase))||new[]{".cue",".bin",".iso",".chd",".sav",".mcr",".mcd"}.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant())||new[]{"settings.json","launcher.json"}.Contains(System.IO.Path.GetFileName(path).ToLowerInvariant());
 public static InstallManifest ReadAndValidate(string root,string rid,Version version){
  var m=Read(root);if(m.Rid!=rid||System.Version.Parse(m.Version)!=version)throw new InvalidDataException("Package platform/version mismatch.");
  foreach(var f in m.Files){var p=SafePath(root,f.Path);if(!File.Exists(p)||new FileInfo(p).Length!=f.Size||!Hash(p).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Package file integrity check failed: "+f.Path);}
  var owned=m.Files.Select(f=>f.Path).ToHashSet(StringComparer.Ordinal);
  foreach(var p in Directory.EnumerateFileSystemEntries(root,"*",SearchOption.AllDirectories)){
   if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Package contains a link.");
   if(Directory.Exists(p))continue;var rel=System.IO.Path.GetRelativePath(root,p).Replace('\\','/');
   if(rel!=FileName && !(rid=="osx-arm64"&&rel.StartsWith("Contents/_CodeSignature/",StringComparison.Ordinal))&&!owned.Contains(rel))throw new InvalidDataException("Package contains an unmanaged file: "+rel);
  }return m;
 }
}
public sealed record StagedUpdate(UpdateRelease Release,string Root,InstallManifest Manifest);
