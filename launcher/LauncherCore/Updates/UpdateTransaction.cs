using System.Text.Json;
namespace ChroniclesLauncher.Updates;
public sealed record UpdateRequest(string InstallationRoot,string StagingRoot,string DataDirectory,string TransactionDirectory,int ParentPid,DateTime ParentStartUtc,string Version,string Nonce);
public sealed class UpdateTransaction {
 public sealed record Operation(string Path,bool Existed,bool HasReplacement);
 public sealed record Journal(UpdateRequest Request,string Work,string OldVersion,string Rid,List<Operation> Operations,string Phase);
 private Journal journal;
 private UpdateTransaction(Journal value){journal=value;}
 public string JournalPath=>Path.Combine(journal.Request.TransactionDirectory,"journal.json");
 private void Save(string phase){journal=journal with{Phase=phase};Directory.CreateDirectory(journal.Request.TransactionDirectory);var tmp=JournalPath+".tmp";using(var file=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(file,journal);file.Flush(true);}File.Move(tmp,JournalPath,true);}
 public static UpdateTransaction Prepare(UpdateRequest request){
  var root=Path.GetFullPath(request.InstallationRoot);var staged=Path.GetFullPath(request.StagingRoot);var data=Path.GetFullPath(request.DataDirectory);
  if(root!=InstallationLease.CanonicalRoot(root)||root==Path.GetPathRoot(root)||Inside(data,root)||Inside(staged,root)||Inside(root,staged)||!Guid.TryParseExact(request.Nonce,"N",out _))throw new InvalidDataException("Unsafe installation layout. Move the application and data into separate folders.");
  var old=InstallManifest.Read(root);var next=InstallManifest.ReadAndValidate(staged,old.Rid,Version.Parse(request.Version));
  if(Version.Parse(old.Version)>=Version.Parse(next.Version))throw new InvalidDataException("The update is not newer.");
  var existing=old.Files.Select(f=>f.Path).Append(InstallManifest.RelativePath(root)).ToHashSet(StringComparer.OrdinalIgnoreCase);
  var replacement=next.Files.Select(f=>f.Path).Append(InstallManifest.RelativePath(root)).ToHashSet(StringComparer.OrdinalIgnoreCase);
  var operations=new List<Operation>();
  foreach(var path in existing.Union(replacement,StringComparer.OrdinalIgnoreCase)){
   var target=InstallManifest.SafePath(root,path);
   if(Directory.Exists(target)||(!existing.Contains(path)&&File.Exists(target)))throw new InvalidDataException("A user file conflicts with the update: "+path);
   operations.Add(new(path,File.Exists(target),replacement.Contains(path)));
  }
  // Complete signed bundles are exchanged on macOS. Extra user files must remain outside them.
  if(old.Rid=="osx-arm64")InstallManifest.ReadAndValidate(root,old.Rid,Version.Parse(old.Version));
  var work=Path.Combine(Path.GetDirectoryName(root)!,".chronicles-update-"+request.Nonce);
  if(Directory.Exists(work))throw new IOException("An update transaction already exists.");
  long needed=next.Files.Sum(f=>f.Size)+old.Files.Sum(f=>f.Size)+32L*1024*1024;
  if(new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace<needed)throw new IOException("Not enough free space for the update and backup.");
  Directory.CreateDirectory(work);
  try{
   CopyTree(staged,Path.Combine(work,"new"));Directory.CreateDirectory(Path.Combine(work,"backup"));
   if(old.Rid!="osx-arm64")foreach(var op in operations.Where(o=>o.Existed)){var src=InstallManifest.SafePath(root,op.Path);var dst=InstallManifest.SafePath(Path.Combine(work,"backup"),op.Path);Directory.CreateDirectory(Path.GetDirectoryName(dst)!);File.Copy(src,dst);}
   var tx=new UpdateTransaction(new(request,work,old.Version,old.Rid,operations,"Prepared"));tx.Save("Prepared");return tx;
  }catch{Directory.Delete(work,true);throw;}
 }
 public void Apply(Action<int>? checkpoint=null){
  Save("Applying");
  if(journal.Rid=="osx-arm64"){MacAppSwap.Exchange(journal.Request.InstallationRoot,Path.Combine(journal.Work,"new"));checkpoint?.Invoke(0);}
  else {int index=0;foreach(var op in journal.Operations.OrderBy(o=>o.Path is "Play" or "Play.exe"?1:0)){
   var target=InstallManifest.SafePath(journal.Request.InstallationRoot,op.Path);
   if(op.HasReplacement)Replace(InstallManifest.SafePath(Path.Combine(journal.Work,"new"),op.Path),target);else if(File.Exists(target))File.Delete(target);
   checkpoint?.Invoke(index++);
  }}Save("Applied");
 }
 public void Rollback(){
  if(journal.Phase is "Committed" or "RolledBack")return;
  if(journal.Rid=="osx-arm64"){
   if(InstallManifest.Read(journal.Request.InstallationRoot).Version==journal.Request.Version)MacAppSwap.Exchange(journal.Request.InstallationRoot,Path.Combine(journal.Work,"new"));
  }else foreach(var op in journal.Operations){var target=InstallManifest.SafePath(journal.Request.InstallationRoot,op.Path);if(op.Existed)Replace(InstallManifest.SafePath(Path.Combine(journal.Work,"backup"),op.Path),target);else if(File.Exists(target))File.Delete(target);}
  Save("RolledBack");Cleanup();
 }
 public void Commit(){Save("Committed");Cleanup();}
 public static UpdateTransaction Load(string path,string expectedRoot){
  var j=JsonSerializer.Deserialize<Journal>(File.ReadAllText(path))??throw new InvalidDataException("Invalid recovery journal.");
  if(Path.GetFullPath(j.Request.InstallationRoot)!=Path.GetFullPath(expectedRoot)||j.Work!=Path.Combine(Path.GetDirectoryName(expectedRoot)!,".chronicles-update-"+j.Request.Nonce)||Path.GetFullPath(path)!=Path.Combine(j.Request.TransactionDirectory,"journal.json")||!Guid.TryParseExact(j.Request.Nonce,"N",out _))throw new InvalidDataException("Recovery journal has unexpected paths.");
  return new(j);
 }
 private void Cleanup(){try{if(Directory.Exists(journal.Work))Directory.Delete(journal.Work,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
 private static void Replace(string source,string destination){Directory.CreateDirectory(Path.GetDirectoryName(destination)!);var tmp=destination+".update-"+Guid.NewGuid().ToString("N");try{File.Copy(source,tmp);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(tmp,File.GetUnixFileMode(source));File.Move(tmp,destination,true);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
 private static bool Inside(string path,string root)=>path.Equals(root,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)||path.StartsWith(root+Path.DirectorySeparatorChar,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
 private static void CopyTree(string from,string to){Directory.CreateDirectory(to);foreach(var file in Directory.EnumerateFiles(from,"*",SearchOption.AllDirectories)){var rel=Path.GetRelativePath(from,file).Replace('\\','/');var src=InstallManifest.SafePath(from,rel);var dst=InstallManifest.SafePath(to,rel);Directory.CreateDirectory(Path.GetDirectoryName(dst)!);File.Copy(src,dst);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(dst,File.GetUnixFileMode(src));}}
}
