using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;

namespace Chronicles;

public static class OrderingTableFix
{
    private static int _reported;

    // GsSortClear owns two reusable FILL packets. A respawn can submit an OT
    // whose head already is that buffer's clear packet. AddPrim would replace
    // the packet's next pointer with itself, losing the rest of the chain.
    // Keep the existing link; GsSortClear has already updated color/geometry.
    public static bool BeforeAddPrim(CpuContext cpu, IMemory memory)
    {
        if (cpu.RA != 0x80041230u || cpu.A1 is not (0x80097B28u or 0x80097B38u)) return true;
        var tag = memory.ReadU32(cpu.A0);
        var packet = memory.ReadU32(cpu.A1);
        var physical = cpu.A1 & 0xFFFFFFu;
        if ((tag & 0xFFFFFFu) != physical || (packet & 0xFFFFFFu) == physical) return true;
        if ((packet >> 24) != 3 || memory.ReadU8(cpu.A1 + 7) != 2) return true;

        if (_reported++ < 8)
            Console.WriteLine($"[OrderingTable] kept existing clear packet: ot={cpu.A0:X8} packet={cpu.A1:X8} next={packet & 0xFFFFFFu:X6}");
        return false;
    }
}
