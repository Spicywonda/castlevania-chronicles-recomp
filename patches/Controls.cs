using System.Numerics;
using ImGuiNET;
using RecompOne.Runtime.Host.Window;

namespace Chronicles;

public static class Controls
{
    private static bool _initialized;
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        Localization.Merge("""
        {"strings": {
          "chronicles.controls": {"en": "Controls"},
          "chronicles.controls.configure": {"en": "Keyboard and gamepad..."}
        }}
        """);
        PopupManager.Register(new ControlsPopup());
        MenuRegistry.Menu("chronicles.controls").After("menu.system")
            .Popup<ControlsPopup>("chronicles.controls.configure");
    }
}

public sealed class ControlsPopup : Popup
{
    protected override string TitleKey => "chronicles.controls";
    protected override Vector2 Size => new(700f, 500f);
    protected override void DrawContent()
    {
        ImGui.TextWrapped("Choose Keyboard or Gamepad, click a binding, then press the new key or button. Changes are saved automatically.");
        ImGui.Spacing();
        // Reuse the runtime editor and its per-game persistence for both input paths.
        var input = SettingsRegistry.Sections.FirstOrDefault(section => section.Id == "input");
        if (input != null) input.Draw();
        else ImGui.TextDisabled("Controls are not available yet.");
    }
}
