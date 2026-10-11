using ChroniclesLauncher.Updates;
static class HelperChecks {
 public static async Task Run(string scratch){
  var root=Path.Combine(scratch,"helper-install");Directory.CreateDirectory(root);root=InstallationLease.CanonicalRoot(root);var state=InstallationLease.StateDirectory(root);var pending=Path.Combine(state,"pending.json");File.WriteAllText(pending,"another transaction");
  using var lease=InstallationLease.TryAcquire(root)??throw new Exception("Missing lease");
  var request=new UpdateRequest(root,Path.Combine(scratch,"staged"),Path.Combine(scratch,"helper-data"),Path.Combine(scratch,"helper-tx"),int.MaxValue,DateTime.UnixEpoch,"0.3.10",Guid.NewGuid().ToString("N"));
  await UpdateHelper.RunAsync(request,false,default);
  if(!File.Exists(pending)||File.ReadAllText(pending)!="another transaction")throw new Exception("Losing helper erased the owning transaction");
  File.Delete(pending);Console.WriteLine("PASS Helper without lease cannot change another transaction's recovery state");
 }
}
