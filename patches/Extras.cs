using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Sdk;

namespace Chronicles;

public static class Extras
{
    private static bool _waitForMovieRelease;

    public static void BeginMovie(uint index) => _waitForMovieRelease = index == 2;

    public static uint FilterMovieButtons(uint buttons)
    {
        // The extras menu and movie share the confirm/skip button. Do not let
        // the press that opens the interview immediately close it again.
        if (!_waitForMovieRelease) return buttons;
        if ((buttons & 0x60) == 0) _waitForMovieRelease = false;
        return buttons & ~0x60u;
    }

    public static void WaitForMovieFrame()
    {
        // StGetNext is non-blocking. A native-speed 2000-iteration spin expires
        // before the paced STR reader delivers its next frame. The game's retry
        // then issues DsRead2 -> Pause, discarding queued XA audio. Yield between
        // polls; the original bounded retry remains available for a real stall.
        Thread.Sleep(1);
    }

    public static void PresentGallery(CpuContext cpu, IMemory memory)
    {
        // Original hardware scans out continuously. This viewer uploads its
        // framebuffer once and then polls input without another VSync.
        var saved = cpu.Snapshot();
        try { cpu.A0 = 0; LibEtc.VSync(cpu, memory); }
        finally { cpu.Restore(saved); }
    }
}
