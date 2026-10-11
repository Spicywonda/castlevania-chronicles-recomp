using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
namespace ChroniclesLauncher.Updates;
public sealed class PackageStager(HttpClient client) {
 public async Task<StagedUpdate> StageAsync(UpdateRelease release,string cacheDirectory,IProgress<UpdateProgress> progress,CancellationToken token){
  Directory.CreateDirectory(cacheDirectory);var folder=Path.Combine(cacheDirectory,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  var partial=Path.Combine(folder,"download.partial");var archive=Path.Combine(folder,release.Rid=="linux-x64"?"package.tar.gz":"package.zip");
  try{
   var url=release.PackageUrl;
   if(url.Scheme!="https"||url.Host!="github.com"||!url.AbsolutePath.StartsWith("/"+ReleaseClient.Repository+"/releases/download/",StringComparison.Ordinal))throw new InvalidDataException("Invalid update origin.");
   HttpResponseMessage? response=null;
   for(var redirects=0;redirects<=3;redirects++){
    using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.UserAgent.ParseAdd("ChroniclesLauncher/"+BuildVersion.Current);
    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(30));
    response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
    if((int)response.StatusCode is >=300 and <=399){var next=response.Headers.Location;response.Dispose();response=null;if(next==null)throw new InvalidDataException("Invalid download redirect.");url=new Uri(url,next);if(url.Scheme!="https"||url.UserInfo!=""||!url.IsDefaultPort||url.Host is not("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))throw new InvalidDataException("Untrusted download redirect.");continue;}break;
   }
   using(response??throw new InvalidDataException("Too many download redirects.")){
    response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength is long length && length!=release.Size)throw new InvalidDataException("Update size changed.");
    using var input=await response.Content.ReadAsStreamAsync(token);using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)){
     long total=0;var buffer=new byte[65536];while(true){using var stall=CancellationTokenSource.CreateLinkedTokenSource(token);stall.CancelAfter(TimeSpan.FromSeconds(30));var n=await input.ReadAsync(buffer,stall.Token);if(n==0)break;total+=n;if(total>release.Size||total>1024L*1024*1024)throw new InvalidDataException("Update exceeds expected size.");await output.WriteAsync(buffer.AsMemory(0,n),token);progress.Report(new("Downloading update",total,release.Size));}if(total!=release.Size)throw new InvalidDataException("Incomplete update download.");
    }
   }
   if(!InstallManifest.Hash(partial).Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update checksum failed.");
   File.Move(partial,archive);progress.Report(new("Verifying update",0,null));
   var root=await ExtractAsync(archive,Path.Combine(folder,"unpacked"),release.Rid,token);
   var manifest=InstallManifest.ReadAndValidate(root,release.Rid,release.Version);
   if(release.Rid=="osx-arm64")await MacAppSwap.VerifyAsync(root,token);
   return new(release,root,manifest);
  }catch{Directory.Delete(folder,true);throw;}
 }
 public static async Task<string> ExtractAsync(string archive,string output,string rid,CancellationToken token){
  Directory.CreateDirectory(output);var prefix="CastlevaniaChronicles-"+rid+"/";var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;int count=0;
  async Task Entry(string name,long size,int mode,bool directory,Stream? input){
   token.ThrowIfCancellationRequested();if(++count>20000)throw new InvalidDataException("Too many archive entries.");
   if(rid=="osx-arm64"&&name.StartsWith("__MACOSX/",StringComparison.Ordinal))return;
   if(directory&&name.TrimEnd('/')==prefix.TrimEnd('/'))return;
   if(!name.StartsWith(prefix,StringComparison.Ordinal))throw new InvalidDataException("Unexpected package root.");name=name[prefix.Length..].TrimEnd('/');
   var target=InstallManifest.SafePath(output,name);if(!names.Add(name))throw new InvalidDataException("Duplicate archive path.");
   if((mode&0xF000) is not(0 or 0x8000 or 0x4000))throw new InvalidDataException("Archive links are not supported.");
   if(directory){Directory.CreateDirectory(target);return;}
   if(size<0||size>512L*1024*1024||(total+=size)>4L*1024*1024*1024)throw new InvalidDataException("Archive exceeds extraction limits.");
   Directory.CreateDirectory(Path.GetDirectoryName(target)!);using var file=new FileStream(target,FileMode.CreateNew);long written=0;var buffer=new byte[65536];int n;
   if(input!=null)while((n=await input.ReadAsync(buffer,token))!=0){written+=n;if(written>size)throw new InvalidDataException("Invalid archive entry length.");await file.WriteAsync(buffer.AsMemory(0,n),token);}
   if(written!=size)throw new InvalidDataException("Truncated archive entry.");
   if(!OperatingSystem.IsWindows())File.SetUnixFileMode(target,(UnixFileMode)(mode==0?0x1a4:mode&0x1ff));
  }
  if(rid=="linux-x64"){
   using var file=File.OpenRead(archive);using var gzip=new GZipStream(file,CompressionMode.Decompress);using var tar=new TarReader(gzip);
   while(await tar.GetNextEntryAsync(false,token) is {} e){if(e.EntryType is not(TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.Directory))throw new InvalidDataException("Unsupported tar entry.");await Entry(e.Name,e.Length,(int)e.Mode,e.EntryType==TarEntryType.Directory,e.DataStream);}
  }else{
   using var zip=ZipFile.OpenRead(archive);foreach(var e in zip.Entries){using var input=e.Open();await Entry(e.FullName,e.Length,(e.ExternalAttributes>>16)&0xffff,e.FullName.EndsWith('/'),input);}
  }
  return rid=="osx-arm64"?Path.Combine(output,"Castlevania Chronicles.app"):output;
 }
}
