# macOS development plan

The initial target is the USA SLUS-01384 release on macOS arm64, followed by experimental widescreen and Windows x64. Successful code generation or compilation does not demonstrate working gameplay.

The project targets .NET 10 and references RecompOne pinned to `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`. Disc data, generated code, local tools, and build outputs are excluded from Git. Project-specific runtime patches are maintained separately.

## Progress

- [x] Set up the SDK and pinned dependency.
- [x] Probe the disc, generate configuration, and review the function map.
- [x] Generate C# and build a host invoking `Recompiled.Entry.Run`.
- [x] Reach the main menu after resolving the boot overlay and PadStartCom mapping.
- [x] Reach the first stage and confirm movement and combat.
- [x] Restore stage scenery by correcting VRAM readback ordering.
- [x] Confirm first-stage audio without audible cuts after coordinating mixing with DMA completion.
- [x] Fix the Start/Enter pause hang; pause/resume confirmed on macOS.
- [ ] Validate later stages, transitions, game modes, and saving separately.
- [ ] Correct the experimental widescreen corruption and object pop-in, retaining 4:3 as the reference.
- [ ] Validate a Windows x64 build on Windows hardware.

See [initial research](initial-research.md), [overlay investigation](overlays.md), [audio results](audio.md), and [widescreen limitations](widescreen.md). Patch addresses and overlay definitions must come from disc analysis and observed execution. Record new failures and their evidence before changing the compatibility claims.
