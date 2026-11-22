using RecompOne.Runtime.Memory;

namespace Chronicles;

// Rendering-only framing for the verified outdoor room. Game camera and actor
// state remain untouched; only newly emitted world primitive coordinates move.
public static class WorldViewport
{
    public static int Shift(IMemory memory)
    {
        if (!Widescreen.CanExtend(memory) || memory.ReadU8(0x800592D0) != 0) return 0;
        if (memory.ReadU32(0x8010AC44) != 0x8010AC54 || memory.ReadU16(0x8010AC56) != 32) return 0;
        int camera = (short)memory.ReadU16(0x80059248);
        if (camera < 0 || camera > 768) return 0;
        return camera - Math.Clamp(camera, 43, 725);
    }

    public static Scope Begin(IMemory memory) => new(memory, Shift(memory));
    public readonly struct Scope : IDisposable
    {
        private readonly IMemory _memory;
        private readonly uint _start;
        private readonly int _shift;
        public Scope(IMemory memory, int shift)
        {
            _memory = memory; _shift = shift;
            _start = shift == 0 ? 0 : memory.ReadU32(0x80097CA8);
        }
        public void Dispose()
        {
            if (_shift == 0) return;
            uint end = _memory.ReadU32(0x80097CA8);
            if (end < _start || end - _start > 0x20000) return;
            for (uint p = _start; p < end;)
            {
                uint words = _memory.ReadU32(p) >> 24;
                uint next = p + (words + 1) * 4;
                if (next > end || words == 0) return;
                Translate(_memory, p + 4, next, _shift);
                p = next;
            }
        }
    }

    private static void Translate(IMemory memory, uint command, uint end, int shift)
    {
        // Validate the full packet before modifying it. Texture page/draw state
        // commands may precede a primitive in the same DMA packet.
        Span<uint> coordinates = stackalloc uint[64];
        int count = 0;
        while (command < end)
        {
            uint opcode = memory.ReadU32(command) >> 24;
            if (opcode is >= 0xE1 and <= 0xE6 || opcode == 0)
            {
                command += 4; continue;
            }
            if (opcode is >= 0x20 and <= 0x3F)
            {
                int vertices = (opcode & 8) != 0 ? 4 : 3;
                bool textured = (opcode & 4) != 0, shaded = (opcode & 16) != 0;
                uint cursor = command + 4;
                for (int i = 0; i < vertices; i++)
                {
                    if (count == coordinates.Length || cursor + 4 > end) return;
                    coordinates[count++] = cursor;
                    cursor += 4 + (textured ? 4u : 0u) + (shaded && i + 1 < vertices ? 4u : 0u);
                }
                command = cursor;
            }
            else if (opcode is >= 0x60 and <= 0x7F)
            {
                if (count == coordinates.Length) return;
                coordinates[count++] = command + 4;
                command += 8 + ((opcode & 4) != 0 ? 4u : 0u) + ((opcode & 0x18) == 0 ? 4u : 0u);
            }
            else return;
        }
        if (command != end) return;
        for (int i = 0; i < count; i++)
        {
            uint p = coordinates[i];
            memory.WriteU16(p, unchecked((ushort)((short)memory.ReadU16(p) + shift)));
            RecompOne.Runtime.Pgxp.PgxpCpu.InvalidateMem(p, memory.ReadU32(p));
        }
    }
}
