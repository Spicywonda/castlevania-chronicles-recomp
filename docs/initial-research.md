# Initial research

Historical baseline: September 13, 2026, before the first working build. See the [README](../README.md) for current status.

## Scope and architecture

The target is Castlevania Chronicles for PlayStation, first on macOS Apple Silicon and then Windows. The first proposed enhancement is a wider view that preserves sprite proportions. The original 4:3 presentation remains the comparison baseline.

The initial inspection used RecompOne revision `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`. Its recompiler and runtime target `net10.0`. `HostWindow.ApiChain` requests OpenGL 4.1 Core ForwardCompatible on macOS; this established a platform path, not runtime compatibility. The CLI offers `--probe-disc` and `--autoconfigure`, whose output still requires review against the game.

The proposed workflow was to identify the disc, inspect the executable and overlays, create a separate game project, and establish playable 4:3 behavior before extending rendering. Native window, input, audio, and UI dependencies also required validation on arm64.

## Disc findings

The supplied CUE/BIN was read without modifying the originals.

| Field | Observed value |
|---|---|
| CUE layout | One MODE2/2352 track, index 00:00:00 |
| Boot identifier in `SYSTEM.CNF` | `SLUS_013.84` |
| Executable header | `PS-X EXE` |
| Entry PC | `0x8003a810` |
| Load address | `0x80010000` |
| Load size | `0x49800` |
| `DRACULA.PAC` size | 17,719,296 bytes |

BIN SHA-256: `71c988db8435848d70024f0af2f47dfb0f970782fe1a84098d3210c5d6d3a0d8`. This is a local fingerprint, not an external catalog comparison.

The ISO9660 root contains `DRACULA.PAC`, `MUSIC.VB2`, three STR files, `LICENSEA.DAT`, `DUMMY.DAT`, the executable, and `SYSTEM.CNF`. The directory listing alone does not identify code overlays; the archive and loading paths require separate analysis.

## Design constraints

Keep the disc and generated code outside Git. Preserve project patches independently of the pinned dependency. Verify boot, menus, input, graphics, audio, level loading, and saving separately.

A true wider view requires work on the camera, backgrounds, clipping, HUD, and enemy activation, including room boundaries and transitions. Stretching or cropping the original image does not fulfill that goal. Windows packaging follows the macOS reference and requires its own runtime testing.

## References

- [RecompOne repository](https://github.com/BlackLabelHQ/RecompOne)
- [RecompOne recompilation guide](https://github.com/BlackLabelHQ/RecompOne/wiki/How-to-recompile-a-game%3F)
- Local inspection of the pinned source revision above.
