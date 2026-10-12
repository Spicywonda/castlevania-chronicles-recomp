using System.Numerics;
using ChroniclesLauncher.Updates;
using ImGuiNET;
internal static class UpdatePanel {
 public static void Draw(UpdateCoordinator updates,CancellationToken token){
  var status=updates.Status;
  if(status.Phase is not (UpdatePhase.Downloading or UpdatePhase.Ready or UpdatePhase.Installing or UpdatePhase.Failed))return;
  ImGui.SetCursorPos(new Vector2(568,408));
  ImGui.PushStyleColor(ImGuiCol.ChildBg,new Vector4(.055f,.06f,.08f,.96f));
  ImGui.BeginChild("Updates",new Vector2(354,138),ImGuiChildFlags.Border);
  ImGui.TextUnformatted(status.Phase==UpdatePhase.Failed?"UPDATE UNAVAILABLE":"UPDATE AVAILABLE");
  ImGui.PushTextWrapPos(320);ImGui.TextWrapped(status.Message);ImGui.PopTextWrapPos();
  if(status.Progress is {Total: >0} p)ImGui.ProgressBar((float)p.Completed/p.Total.Value,new Vector2(280,8),"");
  if(status.Phase is UpdatePhase.Downloading or UpdatePhase.Ready){if(ImGui.SmallButton("Later"))updates.Defer();}
  if(status.Phase==UpdatePhase.Failed && ImGui.SmallButton("Retry"))_ = updates.RetryAsync(token);
  if(updates.ReadySince is {} ready)ImGui.TextWrapped($"Restarting in {Math.Max(0,10-(int)(DateTimeOffset.UtcNow-ready).TotalSeconds)} seconds...");
  ImGui.EndChild();ImGui.PopStyleColor();
 }
}
