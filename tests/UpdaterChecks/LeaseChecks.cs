using ChroniclesLauncher.Updates;
static class LeaseChecks {
 public static void Run(string scratch){var root=Path.Combine(scratch,"lease-install");Directory.CreateDirectory(root);using(var first=InstallationLease.TryAcquire(root)??throw new Exception("First lease failed")){using var second=InstallationLease.TryAcquire(root);if(second!=null)throw new Exception("Concurrent lease granted");}using var third=InstallationLease.TryAcquire(root)??throw new Exception("Lease not released");Console.WriteLine("PASS Installation lease rejects overlap and releases on disposal");}
}
