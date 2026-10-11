using System.Diagnostics;
using System.Runtime.InteropServices;
namespace ChroniclesLauncher.Updates;
public static class MacAppSwap {
 [DllImport("libc",SetLastError=true)]private static extern int renameatx_np(int fromFd,string from,int toFd,string to,uint flags);
 public static void Exchange(string installedApp,string stagedApp){if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException();if(renameatx_np(-2,installedApp,-2,stagedApp,2)!=0)throw new IOException("Could not exchange application bundles.",new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()));}
 public static async Task VerifyAsync(string app,CancellationToken token){if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException();var info=new ProcessStartInfo("/usr/bin/codesign"){UseShellExecute=false,RedirectStandardError=true};foreach(var a in new[]{"--verify","--deep","--strict",app})info.ArgumentList.Add(a);using var p=Process.Start(info)??throw new IOException("Could not verify app signature.");var error=p.StandardError.ReadToEndAsync(token);await p.WaitForExitAsync(token);if(p.ExitCode!=0)throw new InvalidDataException("App signature verification failed: "+await error);}
}
