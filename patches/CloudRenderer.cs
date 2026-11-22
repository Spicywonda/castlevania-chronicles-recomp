using RecompOne.Runtime.Memory;

namespace Chronicles;

public static class CloudRenderer
{
    public static void DrawCloud(RecompOne.Runtime.Context.CpuContext cpu, IMemory memory)
    {
        if (Widescreen.CanExtend(memory))
        {
            uint sprite = cpu.A0, table = cpu.A1, depth = cpu.A2;
            int x = (short)memory.ReadU16(sprite + 4);
            int width = memory.ReadU16(sprite + 8);
            int left = -43 - WorldViewport.Shift(memory);
            int copy = x < left ? x + 384 : x - 384;
            if (copy < left + 342 && copy + width > left)
            {
                memory.WriteU16(sprite + 4, unchecked((ushort)copy));
                try { Recompiled.CastlevaniaChronicles_main.GsSortFastSprite(cpu, memory); }
                finally
                {
                    memory.WriteU16(sprite + 4, unchecked((ushort)x));
                    cpu.A0 = sprite;
                    cpu.A1 = table;
                    cpu.A2 = depth;
                }
            }
        }
        Recompiled.CastlevaniaChronicles_main.GsSortFastSprite(cpu, memory);
    }
}
