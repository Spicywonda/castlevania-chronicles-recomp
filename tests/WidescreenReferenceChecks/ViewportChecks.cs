using Chronicles;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Memory;
static class ViewportChecks
{
    public static void Run()
    {
        CheckPacketLayouts();
        var m = new PSMemory();
        Widescreen.Enabled = true; Display.WideAspect = 16f / 9f;
        m.WriteU8(0x80059286, 15);
        m.WriteU32(0x8010ac44, 0x8010ac54);
        m.WriteU16(0x8010ac56, 32);
        foreach (var (camera, expected) in new[] { (0,-43), (20,-23), (43,0), (400,0), (725,0), (750,25), (768,43) })
        {
            m.WriteU16(0x80059248, (ushort)camera);
            if (WorldViewport.Shift(m) != expected) throw new Exception($"Incorrect room framing at {camera}");
        }
        const uint packet = 0x80016000;
        m.WriteU32(0x80097ca8, packet);
        // A texture page command followed by a textured quad in one packet.
        m.WriteU32(packet, 0x0aFFFFFF); m.WriteU32(packet + 4, 0xe1000000); m.WriteU32(packet + 8, 0x2c808080);
        for (uint i = 0; i < 4; i++) { m.WriteU16(packet + 12 + i*8, (ushort)(i*32)); m.WriteU16(packet + 14 + i*8, 100); }
        using (WorldViewport.Begin(m)) { m.WriteU32(0x80097ca8, packet + 44); }
        for (uint i = 0; i < 4; i++)
            if (m.ReadU16(packet + 12 + i*8) != i*32+43 || m.ReadU16(packet + 14 + i*8) != 100)
                throw new Exception("World geometry was not translated without stretching");
        if (m.ReadU16(0x80059248) != 768 || m.ReadU32(packet) != 0x0aFFFFFF || m.ReadU32(packet+4) != 0xe1000000)
            throw new Exception("Framing modified camera state or packet control data");
        m.WriteU8(0x800592D0, 1);
        if (WorldViewport.Shift(m) != 0) throw new Exception("Unverified room was reframed");
        m.WriteU8(0x800592D0, 0); m.WriteU8(0x80059286, 8);
        if (WorldViewport.Shift(m) != 0) throw new Exception("Introduction was reframed");
        m.WriteU8(0x80059286, 15); m.WriteU32(0x8010ac44, 0);
        if (WorldViewport.Shift(m) != 0) throw new Exception("Invalid map was reframed");
        m.WriteU32(0x8010ac44, 0x8010ac54);
        Widescreen.Enabled = false;
        if (WorldViewport.Shift(m) != 0) throw new Exception("4:3 framing changed");
        Console.WriteLine("PASS room framing and geometry translation preserve camera state and packet control data");
    }
    private static void CheckPacketLayouts()
    {
        int cases = 0;
        foreach (int shift in new[] { -43, 43 })
        foreach (uint opcode in Enumerable.Range(0x20, 32).Concat(Enumerable.Range(0x60, 32)).Select(x => (uint)x))
        {
            var m = new PSMemory();
            const uint start = 0x80016000;
            var words = new List<uint> { 0xe1000123, opcode << 24 | 0x123456 };
            var xy = new List<int>();
            int vertices = opcode < 0x40 ? ((opcode & 8) != 0 ? 4 : 3) : 1;
            for (int i = 0; i < vertices; i++)
            {
                if (i > 0 && (opcode & 16) != 0) words.Add(0x00445566);
                xy.Add(words.Count);
                words.Add(100u << 16 | unchecked((ushort)(i * 32 - 20)));
                if ((opcode & 4) != 0) words.Add(0x12345678);
            }
            if (opcode >= 0x60 && (opcode & 0x18) == 0) words.Add(0x00200020);
            uint size = (uint)(words.Count + 1) * 4;
            // Two consecutive DMA packets protect traversal as well as parsing.
            for (uint packet = start; packet < start + size * 2; packet += size)
            {
                m.WriteU32(packet, (uint)words.Count << 24 | 0xffffff);
                for (int i = 0; i < words.Count; i++) m.WriteU32(packet + 4 + (uint)i * 4, words[i]);
            }
            m.WriteU32(0x80097ca8, start);
            using (new WorldViewport.Scope(m, shift)) { m.WriteU32(0x80097ca8, start + size * 2); }
            for (uint packet = start; packet < start + size * 2; packet += size)
            {
                if (m.ReadU32(packet) != ((uint)words.Count << 24 | 0xffffff)) throw new Exception("DMA header changed");
                for (int i = 0; i < words.Count; i++)
                {
                    uint expected = words[i];
                    if (xy.Contains(i)) expected = (expected & 0xffff0000) | unchecked((ushort)((short)expected + shift));
                    if (m.ReadU32(packet + 4 + (uint)i * 4) != expected)
                        throw new Exception($"Primitive {opcode:X} corrupted word {i}, shift {shift}");
                }
            }
            // Missing final word must leave the entire malformed packet intact.
            m.WriteU32(start, (uint)(words.Count - 1) << 24 | 0xffffff);
            for (int i = 0; i < words.Count; i++) m.WriteU32(start + 4 + (uint)i * 4, words[i]);
            m.WriteU32(0x80097ca8, start);
            using (new WorldViewport.Scope(m, shift)) { m.WriteU32(0x80097ca8, start + size - 4); }
            for (int i = 0; i < words.Count; i++)
                if (m.ReadU32(start + 4 + (uint)i * 4) != words[i]) throw new Exception("Malformed packet was partially translated");
            cases++;
        }
        Console.WriteLine($"PASS {cases} primitive layouts with signed shifts, multiple DMA packets and malformed packet preservation");
    }

}
