using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;

namespace Chronicles;

public static class TileMargins
{
    private readonly record struct Call(uint Background, uint OrderingTable, uint Depth, uint EmptyPage, int UvExtent);
    private static readonly Stack<Call> _calls = new();

    public static void Before31(CpuContext cpu, IMemory memory) => Before(cpu, 31);
    public static void Before32(CpuContext cpu, IMemory memory) => Before(cpu, 32);
    private static void Before(CpuContext cpu, int uvExtent)
    {
        _calls.Push(Widescreen.Enabled && cpu.RA >= 0x80100000 && cpu.RA < 0x8011C800
            ? new(cpu.A0, cpu.A1, cpu.A2 & 0xFFFF, cpu.A3 & 0xFF, uvExtent) : default);
    }

    public static void After(CpuContext cpu, IMemory memory)
    {
        if (!_calls.TryPop(out var call) || call.Background == 0) return;
        var bg = call.Background;
        int scrollX = (short)memory.ReadU16(bg + 12), scrollY = (short)memory.ReadU16(bg + 14);
        var map = memory.ReadU32(bg + 20);
        int columns = memory.ReadU16(map + 2), rows = memory.ReadU16(map + 4);
        if (columns < 1 || columns > 1024 || rows < 1 || rows > 1024) return;
        var tiles = memory.ReadU32(map + 8);
        var firstOriginal = scrollX >> 5;
        var first = Math.Max(0, (scrollX - 43) >> 5);
        var last = Math.Min(columns - 1, (scrollX + 298) >> 5);
        var firstRow = scrollY >> 5;
        var packet = memory.ReadU32(0x80097CA8);
        var ot = memory.ReadU32(call.OrderingTable + 4) + call.Depth * 4;
        for (var row = Math.Max(0, firstRow); row < Math.Min(rows, firstRow + 9); row++)
        for (var column = first; column <= last; column++)
        {
            if (column >= firstOriginal && column < firstOriginal + 9) continue;
            var tile = tiles + (uint)((row * columns + column) * 8);
            var page = memory.ReadU16(tile + 6);
            if (page == call.EmptyPage && memory.ReadU16(tile) == 0) continue;
            int x = column * 32 - scrollX;
            int y = (short)memory.ReadU16(bg + 6) + row * 32 - scrollY;
            var flags = memory.ReadU16(tile + 4);
            int x0 = x, x1 = x + 32, y0 = y, y1 = y + 32;
            if ((flags & 2) != 0) (x0, x1) = (x1, x0);
            if ((flags & 1) != 0) (y0, y1) = (y1, y0);
            var u = memory.ReadU8(tile); var v = memory.ReadU8(tile + 1);
            memory.WriteU32(packet, 0x09000000 | (memory.ReadU32(ot) & 0xFFFFFF));
            memory.WriteU32(packet + 4, 0x2C000000 | (memory.ReadU32(bg + 16) & 0xFFFFFF));
            WriteVertex(memory, packet + 8, x0, y0, u, v, memory.ReadU16(tile + 2));
            WriteVertex(memory, packet + 16, x1, y0, u + call.UvExtent, v, page);
            WriteVertex(memory, packet + 24, x0, y1, u, v + call.UvExtent, 0);
            WriteVertex(memory, packet + 32, x1, y1, u + call.UvExtent, v + call.UvExtent, 0);
            memory.WriteU32(ot, packet & 0xFFFFFF);
            packet += 40;
        }
        memory.WriteU32(0x80097CA8, packet);
    }

    private static void WriteVertex(IMemory memory, uint p, int x, int y, int u, int v, ushort attribute)
    {
        memory.WriteU16(p, unchecked((ushort)x)); memory.WriteU16(p + 2, unchecked((ushort)y));
        memory.WriteU8(p + 4, unchecked((byte)u)); memory.WriteU8(p + 5, unchecked((byte)v));
        memory.WriteU16(p + 6, attribute);
    }
}
