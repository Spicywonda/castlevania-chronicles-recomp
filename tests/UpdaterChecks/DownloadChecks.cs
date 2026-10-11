using System.IO.Compression;
using System.Net;
using System.Text;
using ChroniclesLauncher.Updates;
static class DownloadChecks {
 public static async Task Run(string scratch){
  var root=Path.Combine(scratch,"download-source");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"Play.exe"),"fixture");InstallManifest.Create(root,"win-x64",new Version(0,3,10)).Save(root);
  var archive=Path.Combine(scratch,"valid.zip");using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create))foreach(var file in Directory.GetFiles(root))zip.CreateEntryFromFile(file,"CastlevaniaChronicles-win-x64/"+Path.GetFileName(file));
  var bytes=File.ReadAllBytes(archive);var release=new UpdateRelease(new Version(0,3,10),"v0.3.10","win-x64",new Uri(ReleaseClient.RepositoryUrl+"/releases/download/v0.3.10/CastlevaniaChronicles-0.3.10-win-x64.zip"),bytes.Length,InstallManifest.Hash(archive),new Uri(ReleaseClient.RepositoryUrl));
  using var http=new HttpClient(new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)})));
  var result=await new PackageStager(http).StageAsync(release,Path.Combine(scratch,"cache-good"),new Progress<UpdateProgress>(),default);if(File.ReadAllText(Path.Combine(result.Root,"Play.exe"))!="fixture")throw new Exception("Valid package did not install into staging");
  try{await new PackageStager(http).StageAsync(release with{Sha256=new string('0',64)},Path.Combine(scratch,"cache-bad"),new Progress<UpdateProgress>(),default);throw new Exception("Accepted bad hash");}catch(InvalidDataException){}
  if(Directory.EnumerateFileSystemEntries(Path.Combine(scratch,"cache-bad")).Any())throw new Exception("Failed download was not cleaned");
  using var redirect=new HttpClient(new Handler((_,_)=>{var r=new HttpResponseMessage(HttpStatusCode.Redirect);r.Headers.Location=new Uri("https://example.invalid/evil.zip");return Task.FromResult(r);}));
  try{await new PackageStager(redirect).StageAsync(release,Path.Combine(scratch,"cache-redirect"),new Progress<UpdateProgress>(),default);throw new Exception("Accepted foreign redirect");}catch(InvalidDataException){}
  using var cancel=new CancellationTokenSource();cancel.Cancel();
  try{await new PackageStager(http).StageAsync(release,Path.Combine(scratch,"cache-cancel"),new Progress<UpdateProgress>(),cancel.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}
  Console.WriteLine("PASS Real ZIP download/staging, checksum failure, redirect rejection and cancellation");
 }
 private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> reply):HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>reply(request,token);}
}
