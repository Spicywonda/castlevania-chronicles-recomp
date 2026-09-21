using Chronicles;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;

var failures = 0;
void Check(string name, Action test)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failures++; Console.Error.WriteLine($"FAIL {name}: {e.Message}"); }
}
void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
foreach (var scroll in new[] { 0, 20, 100 })
Check($"Background extends visible map and restores game state at scroll {scroll}", () =>
{
    var m = new PSMemory();
    var c = new CpuContext { A0 = 0x800593C8, RA = 0x80101520 };
    var p = c.A0;
    m.WriteU16(p + 4, 128); m.WriteU16(p + 8, 256);
    m.WriteU16(p + 10, 32); m.WriteU16(p + 12, (ushort)scroll);
    Widescreen.Enabled = true;
    Widescreen.BeforeBackground(c, m);
    int left = scroll == 0 ? 0 : scroll == 20 ? 20 : 43;
    Require(m.ReadU16(p + 4) == 128 - left, "Screen origin was not extended");
    Require(m.ReadU16(p + 8) == 299 + left, "New visible columns were not requested");
    Require(m.ReadU16(p + 12) == scroll - left, "World alignment changed");
    c.A0 = 0;
    Widescreen.AfterBackground(c, m);
    Require(m.ReadU16(p + 4) == 128 && m.ReadU16(p + 8) == 256 && m.ReadU16(p + 12) == scroll,
        "Temporary render change leaked into game state");
});
Check("4:3 and menu backgrounds remain unchanged", () =>
{
    var m = new PSMemory();
    var c = new CpuContext { A0 = 0x800593C8, RA = 0x80101520 };
    m.WriteU16(c.A0 + 4, 128); m.WriteU16(c.A0 + 8, 256); m.WriteU16(c.A0 + 10, 32);
    foreach (var enabled in new[] { false, true })
    {
        Widescreen.Enabled = enabled;
        if (enabled) c.RA = 0x80020000;
        Widescreen.BeforeBackground(c, m);
        Require(m.ReadU16(c.A0 + 4) == 128 && m.ReadU16(c.A0 + 8) == 256, "Unrelated background was changed");
        Widescreen.AfterBackground(c, m);
    }
});
foreach (var extent in new[] { 31, 32 })
Check($"Terrain margins draw real neighboring columns with UV extent {extent}", () =>
{
    var m = new PSMemory();
    var c = new CpuContext { A0 = 0x80010000, A1 = 0x80011000, A2 = 2, A3 = 28, RA = 0x80101670 };
    m.WriteU16(c.A0 + 6, 16); m.WriteU16(c.A0 + 12, 96);
    m.WriteU8(c.A0 + 16, 128); m.WriteU8(c.A0 + 17, 128); m.WriteU8(c.A0 + 18, 128);
    m.WriteU32(c.A0 + 20, 0x80012000);
    m.WriteU16(0x80012002, 20); m.WriteU16(0x80012004, 1);
    m.WriteU32(0x80012008, 0x80013000);
    for (uint i = 0; i < 20; i++)
    {
        var t = 0x80013000 + i * 8;
        m.WriteU8(t, (byte)(i * 8)); m.WriteU16(t + 2, 0x3C); m.WriteU16(t + 6, 4);
    }
    m.WriteU32(c.A1 + 4, 0x80014000); m.WriteU32(0x80014008, 0x00FFFFFF);
    m.WriteU32(0x80097CA8, 0x80015000);
    Widescreen.Enabled = true;
    if (extent == 31) TileMargins.Before31(c, m); else TileMargins.Before32(c, m);
    c.A0 = c.A1 = c.A2 = c.A3 = 0;
    TileMargins.After(c, m);
    Require(m.ReadU32(0x80097CA8) == 0x80015078, "Expected three additional 40-byte tile packets");
    var xs = new[] { -64, -32, 288 };
    var us = new[] { 8, 16, 96 };
    for (uint i = 0; i < 3; i++)
    {
        var p = 0x80015000 + i * 40;
        Require((short)m.ReadU16(p + 8) == xs[i], "Tile uses the wrong world position");
        Require(m.ReadU8(p + 12) == us[i] && m.ReadU8(p + 20) == us[i] + extent,
            "Margin repeats an edge texture instead of using its map column");
        Require(m.ReadU8(p + 7) == 0x2C && m.ReadU16(p + 14) == 0x3C, "Invalid textured packet");
    }
    Require(m.ReadU32(0x80014008) == 0x15050, "Additional tiles are not linked into the ordering table");
});
Check("Sprite margins preserve original mode and stateful actor handlers", () =>
{
    var m = new PSMemory();
    const uint actor = 0x8005A848;
    m.WriteU8(actor + 1, 12);
    m.WriteU32(0x800107A8, 0x800221DC);
    Widescreen.Enabled = true;
    RecompOne.Runtime.Hle.Display.WideAspect = 16f / 9f;
    Require(Widescreen.SpriteMargin == 43, "Stage sprites still use the old viewport");
    Require(Widescreen.ActorMargin(m, actor) == 43, "Pure sprite handler still culled");
    foreach (uint handler in new uint[] { 0x800221C8, 0x80022204, 0x80022218, 0 })
    {
        m.WriteU32(0x800107A8, handler);
        Require(Widescreen.ActorMargin(m, actor) == 0, "Stateful or unknown handler was widened");
    }
    m.WriteU32(0x800107A8, 0x800221F0);
    Require(Widescreen.ActorMargin(m, actor) == 43, "Pickup sprite handler still culled");
    Widescreen.Enabled = false;
    Require(Widescreen.SpriteMargin == 0 && Widescreen.ActorMargin(m, actor) == 0, "4:3 bounds changed");
    Widescreen.Enabled = true;
    RecompOne.Runtime.Hle.Display.WideAspect = 0;
    Require(Widescreen.ActorMargin(m, actor) == 0, "Menu bounds changed");
});
return failures == 0 ? 0 : 1;
