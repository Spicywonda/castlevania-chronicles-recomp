using System.Net;
using System.Text;
using System.Text.Json;
using ChroniclesLauncher.Updates;
var scratch=Path.Combine(Path.GetTempPath(),"chronicles-updater-check-"+Guid.NewGuid());Directory.CreateDirectory(scratch);
try {
 string Payload(string tag="v0.3.10",bool pre=false,bool draft=false,string rid="win-x64",string? digest=null,string state="uploaded") => JsonSerializer.Serialize(new {tag_name=tag,prerelease=pre,draft,assets=new[]{new {name=$"CastlevaniaChronicles-{tag[1..]}-{rid}.zip",browser_download_url=$"https://github.com/Spicywonda/castlevania-chronicles-recomp/releases/download/{tag}/CastlevaniaChronicles-{tag[1..]}-{rid}.zip",size=10,state,digest=digest??"sha256:"+new string('a',64)}}});
 async Task<UpdateRelease?> Check(string body,Version? current=null){using var http=new HttpClient(new Reply(body));return await new ReleaseClient(http,Path.Combine(scratch,Guid.NewGuid().ToString())).CheckAsync(current??new Version(0,3,9),"win-x64",default);}
 if((await Check(Payload()))?.Version!=new Version(0,3,10))throw new Exception("Numeric version comparison failed");
 foreach(var p in new[]{Payload(tag:"v0.3.9"),Payload(tag:"v0.3.8"),Payload(pre:true),Payload(draft:true),Payload(rid:"osx-arm64"),Payload(digest:"sha256:bad"),Payload(state:"new")}) {
  try {if(await Check(p)!=null)throw new Exception("Invalid release selected");}catch(InvalidDataException){}
 }
 Console.WriteLine("PASS Stable release selection, numeric versions, platform and digest validation");
 using(var http=new HttpClient(new Reply("",HttpStatusCode.NotFound))) if(await new ReleaseClient(http,scratch).CheckAsync(new Version(0,3,9),"win-x64",default)!=null)throw new Exception("404 should mean no release");
 using(var http=new HttpClient(new Reply("",HttpStatusCode.NotModified))) {
  try {await new ReleaseClient(http,scratch).CheckAsync(new Version(0,3,9),"win-x64",default);throw new Exception("Accepted 304 without cache");}catch(InvalidDataException){}
 }
 Console.WriteLine("PASS Missing stable release and invalid conditional response");
 await PackageChecks.Run(scratch);
 LeaseChecks.Run(scratch);
 TransactionChecks.Run(scratch);
 await CoordinatorChecks.Run(scratch);
 await DownloadChecks.Run(scratch);
 await HelperChecks.Run(scratch);
} finally {Directory.Delete(scratch,true);}
sealed class Reply(string body,HttpStatusCode status=HttpStatusCode.OK):HttpMessageHandler {
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(body,Encoding.UTF8,"application/json")});
}
