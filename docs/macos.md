# macOS preview — 0.3.6

Download and extract the osx-arm64 package. Open **Castlevania Chronicles.app**.
Choose your USA SLUS-01384 CUE or CHD, then click **Play**. Keep CUE and referenced BIN files together.
You can drop a disc image onto the launcher. It remembers your selection.

The runtime is included; no .NET installation is required. All dependencies are inside the application bundle.
Use **Open logs** after a crash and share `run.log`. Disc validation errors are recorded in `validate.log`.

The build includes all eight stage entrypoints. Full-game completion and the post-bat transition still need gameplay validation on this platform.
No ROM or game assets are included.

Build: `scripts/publish-macos.sh`.

## Shared audio update

Version 0.3.5 uses the same audio implementation on Windows, macOS and Linux.
It also fixes ignored manual SPU uploads, which left the silent audio loop empty.
The launcher and in-game menus, including Cheats, start in English.
The output reserve increases from 46.4 to 69.7 ms to tolerate short scheduling stalls,
while retaining the original mixer cadence and SPU interrupt/DMA synchronization.
This adds approximately 23 ms of queued audio. `run.log` now includes `[Audio]`
queue/restart counters to help diagnose any remaining cuts.
Local playback validation is on macOS; Windows and Linux require device testing.
See [audio investigation](audio.md) for measurements and limitations.

## Controls and invincibility (0.3.6)

Press **F1 → Controls → Keyboard and gamepad...** to reassign keys and controller buttons. Changes save automatically for this game. Use **Reset defaults** to restore the selected input type and player port.

**F1 → Cheats → Invincibility** prevents contact damage and knockback. It starts disabled each session. Falls and instant death still apply. The same implementation ships on all three platforms.
