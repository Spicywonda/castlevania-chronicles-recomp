using ChroniclesLauncher.Updates;
static class CoordinatorChecks {
 public static async Task Run(string scratch){
  var release=new UpdateRelease(new Version(0,3,10),"v0.3.10","win-x64",new Uri("https://example.invalid"),1,new string('a',64),new Uri("https://example.invalid"));
  int installs=0;var coordinator=new UpdateCoordinator(_=>Task.FromResult<UpdateRelease?>(release),(r,_,_)=>Task.FromResult(new StagedUpdate(r,scratch,new InstallManifest(1,"0.3.10","win-x64",[]))),_=>{installs++;return true;});
  await coordinator.StartAsync(default);coordinator.SetGameRunning(true);if(await coordinator.InstallIfReadyAsync(default))throw new Exception("Installed during game");
  coordinator.SetGameRunning(false);coordinator.Defer();if(await coordinator.InstallIfReadyAsync(default))throw new Exception("Ignored Later");
  await coordinator.RetryAsync(default);if(!await coordinator.InstallIfReadyAsync(default)||installs!=1)throw new Exception("Retry did not install");
  if(await coordinator.InstallIfReadyAsync(default)||installs!=1)throw new Exception("Installed twice");
  Console.WriteLine("PASS Update coordinator respects running game, Later, explicit Retry and single installation");
 }
}
