using System.IO.Compression;
using System.Security.Cryptography;
using ChroniclesLauncher.Updates;
static class PackageChecks {
 public static async Task Run(string scratch){
  var root=Path.Combine(scratch,"package");Directory.CreateDirectory(root);await File.WriteAllTextAsync(Path.Combine(root,"Play"),"old executable");
  var manifest=InstallManifest.Create(root,"linux-x64",new Version(0,3,10));manifest.Save(root);
  if(InstallManifest.ReadAndValidate(root,"linux-x64",new Version(0,3,10)).Files.Count!=1)throw new Exception("Manifest mismatch");
  await File.WriteAllTextAsync(Path.Combine(root,"Play"),"tampered");
  try{InstallManifest.ReadAndValidate(root,"linux-x64",new Version(0,3,10));throw new Exception("Accepted modified file");}catch(InvalidDataException){}
  foreach(var name in new[]{"../escape","/absolute","a/../../escape","C:/escape","folder\\escape","a/./b"}){
   try{InstallManifest.SafePath(root,name);throw new Exception("Accepted unsafe path "+name);}catch(InvalidDataException){}
  }
  var zip=Path.Combine(scratch,"bad.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){using var writer=new StreamWriter(archive.CreateEntry("CastlevaniaChronicles-win-x64/../escape").Open());writer.Write("bad");}
  try{await PackageStager.ExtractAsync(zip,Path.Combine(scratch,"extract"),"win-x64",default);throw new Exception("Accepted traversal archive");}catch(InvalidDataException){}
  Console.WriteLine("PASS Manifest integrity and archive/path traversal rejection");
 }
}
