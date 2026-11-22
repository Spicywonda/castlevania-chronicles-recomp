using Chronicles;
using Recompiled;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Memory;

static class RoomEdgeChecks
{
    public static void Run()
    {
        foreach (int extent in new[] { 31, 32 })
        foreach (ushort flags in new ushort[] { 0, 1, 2, 3 })
        foreach (bool wide in new[] { false, true })
        foreach (bool framed in new[] { false, true })
        {
            var m = new PSMemory();
            const uint bg = 0x80010000, table = 0x80011000, map = 0x80012000;
            const uint tiles = 0x80013000, packet = 0x80016000, ot = 0x80015008;
            m.WriteU8(0x80059286, 15);
            if (framed)
            {
                m.WriteU16(0x80059248, 768);
                m.WriteU32(0x8010ac44, 0x8010ac54);
                m.WriteU16(0x8010ac56, 32);
            }
            m.WriteU16(bg + 12, 768); m.WriteU16(bg + 14, 32);
            m.WriteU32(bg + 16, 0x808080); m.WriteU32(bg + 20, map);
            m.WriteU16(map + 2, 32); m.WriteU16(map + 4, 8); m.WriteU32(map + 8, tiles);
            // Include distinctive data after the map: the original 9x9 loop reads it,
            // but the original viewport hides it. Wide output must not expose it.
            for (uint i = 0; i < 32 * 12; i++)
            {
                m.WriteU16(tiles + i * 8, (ushort)(i + 1));
                m.WriteU16(tiles + i * 8 + 4, flags);
                m.WriteU16(tiles + i * 8 + 6, 4);
            }
            m.WriteU32(table + 4, ot - 8); m.WriteU32(ot, 0xffffff);
            m.WriteU32(0x80097ca8, packet);
            Widescreen.Enabled = wide; Display.WideAspect = 16f / 9f;
            var c = new CpuContext { A0 = bg, A1 = table, A2 = 2, A3 = 28, RA = 0x80101670, SP = 0x801ff000 };
            using (WorldViewport.Begin(m))
            {
                if (extent == 31) CastlevaniaChronicles_main.func_8004E480(c, m);
                else CastlevaniaChronicles_main.func_8004E8B8(c, m);
            }
            int count = 0;
            for (uint p = m.ReadU32(ot) & 0xffffff; p != 0xffffff; p = m.ReadU32(p) & 0xffffff)
            {
                if (++count > 200) throw new Exception("Invalid packet chain at room boundary");
                int x = Math.Min((short)m.ReadU16(p + 8), (short)m.ReadU16(p + 16));
                int y = Math.Min((short)m.ReadU16(p + 10), (short)m.ReadU16(p + 26));
                if (wide && (x >= (framed ? 299 : 256) || y >= 224))
                    throw new Exception($"Room boundary exposes wrapped map data: extent={extent}, x={x}, y={y}");
            }
            if (count != (wide ? (framed ? 77 : 70) : 81)) throw new Exception($"Missing valid room tiles: {count}");
        }
        Console.WriteLine("PASS 32 room-boundary cases with and without framing: original mode and all flips; boundaries do not expose wrapped columns or rows in either tile routine");
    }
}
