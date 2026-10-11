using System.Numerics;
using ChroniclesLauncher.Updates;
using ImGuiNET;
internal static class UpdatePanel {
 public static void Draw(UpdateCoordinator updates,CancellationToken token){
  ImGui.SetCursorPos(new Vector2(568,324));ImGui.BeginChild("Updates",new Vector2(354,222),ImGuiChildFlags.Border);
  ImGui.TextUnformatted("UPDATES");ImGui.SameLine();ImGui.TextDisabled("v"+BuildVersion.Current);
  var status=updates.Status;ImGui.PushTextWrapPos(320);ImGui.TextWrapped(status.Message);ImGui.PopTextWrapPos();
  if(status.Progress is {Total: >0} p)ImGui.ProgressBar((float)p.Completed/p.Total.Value,new Vector2(310,12),"");
  if(status.Phase is UpdatePhase.Checking or UpdatePhase.Downloading or UpdatePhase.Ready){if(ImGui.SmallButton("Later"))updates.Defer();}
  if(status.Phase is UpdatePhase.Failed or UpdatePhase.Deferred or UpdatePhase.UpToDate or UpdatePhase.Idle){if(ImGui.SmallButton(status.Phase==UpdatePhase.Failed?"Retry":"Check for updates"))_ = updates.RetryAsync(token);}
  if(updates.ReadySince is {} ready)ImGui.TextWrapped($"Restarting in {Math.Max(0,10-(int)(DateTimeOffset.UtcNow-ready).TotalSeconds)} seconds...");
  ImGui.EndChild();
 }
}
