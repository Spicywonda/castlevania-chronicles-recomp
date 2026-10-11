using ChroniclesLauncher.Updates;
static class TransactionChecks {
 public static void Run(string scratch){
  foreach(var fail in new[]{-1,0,1,2}){
   var root=Path.Combine(scratch,"installed"+fail);var staged=Path.Combine(scratch,"staged"+fail);Directory.CreateDirectory(root);Directory.CreateDirectory(staged);root=InstallationLease.CanonicalRoot(root);
   File.WriteAllText(Path.Combine(root,"Play.exe"),"old");File.WriteAllText(Path.Combine(root,"obsolete.dll"),"old extra");InstallManifest.Create(root,"win-x64",new Version(0,3,9)).Save(root);
   File.WriteAllText(Path.Combine(root,"my-save.sav"),"keep");
   File.WriteAllText(Path.Combine(staged,"Play.exe"),"new");InstallManifest.Create(staged,"win-x64",new Version(0,3,10)).Save(staged);
   var request=new UpdateRequest(root,staged,Path.Combine(scratch,"data"),Path.Combine(scratch,"tx"+fail),0,DateTime.UtcNow,"0.3.10",Guid.NewGuid().ToString("N"));
   var tx=UpdateTransaction.Prepare(request);
   try{tx.Apply(n=>{if(n==fail)throw new IOException("injected");});if(fail>=0)throw new Exception("Fault not injected");tx.Commit();if(File.ReadAllText(Path.Combine(root,"Play.exe"))!="new"||File.Exists(Path.Combine(root,"obsolete.dll")))throw new Exception("Update failed");}
   catch(IOException){tx.Rollback();if(File.ReadAllText(Path.Combine(root,"Play.exe"))!="old"||!File.Exists(Path.Combine(root,"obsolete.dll")))throw new Exception("Rollback lost original");}
   if(File.ReadAllText(Path.Combine(root,"my-save.sav"))!="keep")throw new Exception("Lost user data");
  }
  Console.WriteLine("PASS Transaction commits, removes owned obsolete files and rolls back injected failures without touching saves");
 }
}
