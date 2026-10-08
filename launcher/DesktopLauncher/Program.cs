using System.Diagnostics;
using System.Numerics;
using ChroniclesLauncher;
using ImGuiNET;
using NativeFileDialogNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

try { return RunLauncher(args); }
catch (Exception e) {
    var folder = LauncherStorage.DataDirectory;
    Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "launcher.log"), e.ToString());
    Console.Error.WriteLine(e);
    return 1;
}

static int RunLauncher(string[] args) {
var runtime = Path.Combine(AppContext.BaseDirectory, "runtime");
// Silk's loader does not search the single-file extraction directory.
// Reuse the same native libraries shipped with the game.
((Silk.NET.Core.Loader.DefaultPathResolver)Silk.NET.Core.Loader.PathResolver.Default)
    .Resolvers.Add(name => new[] { Path.Combine(runtime, Path.GetFileName(name)) });
var options = WindowOptions.Default with {
    Title = "Castlevania Chronicles", Size = new Vector2D<int>(720, 440),
    WindowBorder = WindowBorder.Fixed, VSync = true, FramesPerSecond = 30, UpdatesPerSecond = 30,
    API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3))
};
using var window = Window.Create(options);
GL? gl = null;
ImGuiController? ui = null;
IInputContext? input = null;
var disc = args.FirstOrDefault() ?? LauncherStorage.LoadSelectedPath(LauncherStorage.DataDirectory) ?? "";
var status = "Select your disc to get started.";
var error = false;
Task<int>? session = null;
bool validating = false;
var data = LauncherStorage.DataDirectory;
var game = Path.Combine(runtime, OperatingSystem.IsWindows() ? "CastlevaniaChronicles.exe" : "CastlevaniaChronicles");
var accent = new Vector4(.78f,.24f,.30f,1);
var muted = new Vector4(.57f,.59f,.65f,1);
ImFontPtr titleFont = default;
void Fail(string message) { status = message; error = true; }
void Browse() {
    try {
        using var dialog = new NativeFileDialog().SelectFile().AddFilter("PlayStation disc", "cue,chd");
        if (dialog.Open(out string? path, File.Exists(disc) ? Path.GetDirectoryName(disc) : null) == DialogResult.Okay && path is not null) {
            disc = path; status = "Ready to play."; error = false;
        }
    } catch (Exception e) { Fail(e.Message); }
}
void Play() {
    var selection = DiscSelection.Validate(disc);
    if (!selection.IsValid) { Fail(selection.Error!); return; }
    if (!File.Exists(game)) { Fail("Game files are missing. Extract the entire download and try again."); return; }
    try {
        disc = selection.Path!;
        LauncherStorage.SaveSelectedPath(data, disc);
        status = "Checking disc..."; error = false; validating = true;
        session = GameSession.RunAsync(GameLaunch.CreateValidationStartInfo(game, disc, data), Path.Combine(data,"validate.log"));
    } catch (Exception e) { Fail(e.Message); session = null; }
}
void OpenLogs() {
    try {
        Directory.CreateDirectory(data);
        var info = OperatingSystem.IsWindows() ? new ProcessStartInfo(data) { UseShellExecute = true }
            : new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false };
        if (!OperatingSystem.IsWindows()) info.ArgumentList.Add(data);
        Process.Start(info);
    } catch (Exception e) { Fail(e.Message); }
}
window.Load += () => {
    gl = GL.GetApi(window);
    input = window.CreateInput();
    ui = new ImGuiController(gl, window, input, null, () => {
        var io = ImGui.GetIO();
        unsafe { io.NativePtr->IniFilename = null; }
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        var font = Path.Combine(runtime,"LauncherFont.ttf");
        if (File.Exists(font)) {
            io.Fonts.AddFontFromFileTTF(font,17);
            titleFont = io.Fonts.AddFontFromFileTTF(font,35);
        } else { io.Fonts.AddFontDefault(); titleFont = io.Fonts.AddFontDefault(); }
        var style = ImGui.GetStyle();
        style.WindowPadding = new Vector2(36,28); style.FramePadding = new Vector2(14,12);
        style.ItemSpacing = new Vector2(12,12); style.FrameRounding = 7; style.WindowBorderSize = 0;
        style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(.055f,.06f,.08f,1);
        style.Colors[(int)ImGuiCol.Text] = new Vector4(.93f,.93f,.95f,1);
        style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(.105f,.115f,.145f,1);
        style.Colors[(int)ImGuiCol.Button] = new Vector4(.14f,.15f,.19f,1);
        style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(.21f,.22f,.27f,1);
        style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(.26f,.27f,.32f,1);
    });
    window.FileDrop += paths => { if (session is null && paths.Length > 0) { disc = paths[0]; status = "Ready to play."; error = false; } };
};
window.Render += delta => {
    if (session is { IsCompleted: true }) {
        try {
            var code = session.GetAwaiter().GetResult(); session = null;
            if (validating && code == 0) {
                validating = false; status = "Game is running.";
                session = GameSession.RunAsync(GameLaunch.CreateStartInfo(game,disc,data),Path.Combine(data,"run.log"));
                window.WindowState = WindowState.Minimized;
            } else {
                window.WindowState = WindowState.Normal;
                if (code != 0) Fail(validating ? "Disc could not be opened. Open logs for details." : $"Game stopped (code {code}). Open logs to share run.log.");
                else { status = "Ready to play again."; error = false; }
            }
        } catch (Exception e) { session = null; window.WindowState = WindowState.Normal; Fail(e.Message); }
    }
    ui!.Update((float)delta);
    gl!.Clear(ClearBufferMask.ColorBufferBit);
    var size = ImGui.GetIO().DisplaySize;
    ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(size);
    ImGui.Begin("Launcher", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
    ImGui.TextColored(accent,"PLAYSTATION RECOMPILATION");
    ImGui.SetCursorPosY(66);
    ImGui.PushFont(titleFont); ImGui.TextUnformatted("Castlevania Chronicles"); ImGui.PopFont();
    ImGui.TextColored(muted,"Your disc. The classic, on your desktop.");
    ImGui.SetCursorPosY(176);
    ImGui.TextUnformatted("GAME DISC"); ImGui.SameLine(); ImGui.TextColored(muted,"USA  /  CUE + BIN or CHD");
    ImGui.BeginDisabled(session is not null);
    ImGui.SetNextItemWidth(size.X - 196);
    ImGui.InputTextWithHint("##disc", "Choose or drop your disc image here", ref disc, 4096);
    ImGui.SameLine(); if (ImGui.Button("Browse",new Vector2(112,46))) Browse();
    ImGui.SetCursorPosY(270);
    ImGui.PushStyleColor(ImGuiCol.Button,accent);
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered,new Vector4(.9f,.30f,.36f,1));
    ImGui.PushStyleColor(ImGuiCol.ButtonActive,new Vector4(.64f,.18f,.24f,1));
    if (ImGui.Button(session is not null ? "Running..." : "Play",new Vector2(size.X-72,52))) Play();
    ImGui.PopStyleColor(3); ImGui.EndDisabled();
    ImGui.SetCursorPosY(338);
    ImGui.PushTextWrapPos(size.X-36); ImGui.TextColored(error ? new Vector4(1,.48f,.48f,1) : muted,status); ImGui.PopTextWrapPos();
    ImGui.SetCursorPosY(size.Y-54);
    if (ImGui.SmallButton("Open logs")) OpenLogs();
    ImGui.SameLine(); ImGui.TextColored(muted,"   Disc selection is remembered.");
    ImGui.End(); ui.Render();
};
try { window.Run(); }
catch (Exception e) {
    Directory.CreateDirectory(data);
    File.WriteAllText(Path.Combine(data,"launcher.log"),e.ToString());
    Console.Error.WriteLine(e);
    return 1;
}
finally { if (session is not null) { try { session.GetAwaiter().GetResult(); } catch { } } ui?.Dispose(); input?.Dispose(); gl?.Dispose(); }
return 0;

}
