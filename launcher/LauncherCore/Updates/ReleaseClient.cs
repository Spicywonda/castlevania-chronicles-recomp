using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChroniclesLauncher.Updates;
public sealed class ReleaseClient(HttpClient client,string cacheDirectory) {
 public const string Repository="Spicywonda/castlevania-chronicles-recomp";
 public const string RepositoryUrl="https://github.com/"+Repository;
 private sealed record Cache(string? ETag,string Json);
 public static HttpClient CreateHttpClient()=>new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
 public async Task<UpdateRelease?> CheckAsync(Version current,string rid,CancellationToken token) {
  if(rid is not ("win-x64" or "osx-arm64" or "linux-x64"))throw new PlatformNotSupportedException();
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(8));token=timeout.Token;
  Cache? cache=null;var path=Path.Combine(cacheDirectory,"release-cache.json");
  try {cache=JsonSerializer.Deserialize<Cache>(await File.ReadAllTextAsync(path,token));if(cache!=null)Parse(cache.Json,rid);}catch(Exception e) when(e is IOException or JsonException or InvalidDataException){cache=null;}
  using var request=new HttpRequestMessage(HttpMethod.Get,$"https://api.github.com/repos/{Repository}/releases/latest");
  request.Headers.UserAgent.ParseAdd("ChroniclesLauncher/"+BuildVersion.Current);request.Headers.Accept.ParseAdd("application/vnd.github+json");
  if(cache?.ETag!=null && EntityTagHeaderValue.TryParse(cache.ETag,out var etag))request.Headers.IfNoneMatch.Add(etag);
  using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
  if(response.StatusCode==HttpStatusCode.NotFound)return null;
  string json;
  if(response.StatusCode==HttpStatusCode.NotModified) json=cache?.Json??throw new InvalidDataException("Update cache is unavailable. Retry the check.");
  else {response.EnsureSuccessStatusCode();json=System.Text.Encoding.UTF8.GetString(await ReadBoundedAsync(response.Content,1024*1024,token));}
  var release=Parse(json,rid);
  if(cache!=null && release!=null){var prior=Parse(cache.Json,rid);if(prior?.Version==release.Version && prior.Sha256!=release.Sha256)throw new InvalidDataException("Published update changed. Download it manually from Releases.");}
  Directory.CreateDirectory(cacheDirectory);var temporary=path+"."+Guid.NewGuid().ToString("N");
  try {await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(new Cache(response.Headers.ETag?.ToString()??cache?.ETag,json)),token);File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
  return release?.Version>current?release:null;
 }
 public static UpdateRelease? Parse(string json,string rid) {
  try {
   using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
   if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean())return null;
   var tag=root.GetProperty("tag_name").GetString()!;
   if(!Regex.IsMatch(tag,@"^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$"))throw new InvalidDataException("Unsupported release version.");
   var version=Version.Parse(tag[1..]);var name=$"CastlevaniaChronicles-{version}-{rid}"+(rid=="linux-x64"?".tar.gz":".zip");
   var matches=root.GetProperty("assets").EnumerateArray().Where(a=>a.GetProperty("name").GetString()==name).ToArray();
   if(matches.Length!=1)throw new InvalidDataException("The update package is not available for this platform.");
   var asset=matches[0];var digest=asset.GetProperty("digest").GetString()??"";var size=asset.GetProperty("size").GetInt64();
   if(asset.GetProperty("state").GetString()!="uploaded"||size<=0||size>1024L*1024*1024||!Regex.IsMatch(digest,@"^sha256:[0-9a-fA-F]{64}$"))throw new InvalidDataException("Incomplete update package.");
   var url=new Uri(asset.GetProperty("browser_download_url").GetString()!);
   if(url.AbsoluteUri!=$"{RepositoryUrl}/releases/download/{tag}/{name}")throw new InvalidDataException("Unexpected update download URL.");
   return new(version,tag,rid,url,size,digest[7..].ToLowerInvariant(),new Uri($"{RepositoryUrl}/releases/tag/{tag}"));
  }catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or OverflowException){throw new InvalidDataException("Invalid release metadata.",e);}
 }
 internal static async Task<byte[]> ReadBoundedAsync(HttpContent content,int limit,CancellationToken token){
  if(content.Headers.ContentLength>limit)throw new InvalidDataException("Update metadata is too large.");
  using var input=await content.ReadAsStreamAsync(token);using var output=new MemoryStream();var buffer=new byte[16384];int n;
  while((n=await input.ReadAsync(buffer,token))!=0){if(output.Length+n>limit)throw new InvalidDataException("Update metadata is too large.");await output.WriteAsync(buffer.AsMemory(0,n),token);}return output.ToArray();
 }
}
