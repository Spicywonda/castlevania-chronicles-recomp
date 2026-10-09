using RecompOne.Runtime.Memory;
namespace Chronicles;

public static class SoundBankTransition
{
    // The original sound-bank close adds three VBlanks after stopping sequences
    // and freeing the bank. During gameplay retain one VBlank to service pending
    // sound updates, rather than holding the entire game for three refreshes.
    // This is only called at that bank-close site, never for general VSync waits.
    // VSync(0) waits one refresh; VSync(1) only queries elapsed time.
    public static uint WaitMode(IMemory memory)
    {
        var state = memory.ReadU8(0x80059286);
        return memory.ReadU8(0x800592C4) < 8 && (state == 15 || state == 20) ? 0u : 3u;
    }
}
