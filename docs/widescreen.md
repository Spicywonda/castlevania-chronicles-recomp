# Experimental first-stage widescreen

**Current status:** opt-in and incomplete. The user reported corrupted imagery and objects appearing or disappearing at the side margins. A successful build or passing patch-state checks does not demonstrate correct rendering.

The goal is a 16:9 view with more scenery visible, original sprite proportions, and a 4:3 comparison option. The first rendering experiment preserves the reference physics and camera.

```sh
CHRONICLES_WIDESCREEN=1 ./scripts/run-macos.sh
```

Open the in-game menu with F1, then choose **Graphics → Widescreen (16:9, experimental)**. The same option is available in **System → Settings → Display**. F8 also toggles it; the choice is saved automatically and applies on the next game frame. A saved choice takes precedence over the environment variable. The default is 4:3. Menus retain 4:3 and HUD elements retain their central positions. The extension is restricted to the `stage1` overlay and gameplay states 15–22 with stage index 0. Introduction, loading and menu states retain 4:3. Background, terrain and actor margins share the gameplay guard, including when a previous frame still had a wide viewport.

## Rendering approach

RecompOne supports side surfaces through `Display.WideAspect`. The experiment adds 43 pixels on either side of the original 256-pixel surface, for a rounded width of 342. The backend extends the clipping region of a full-screen surface while preserving the central content's horizontal scale.

`GsSortFastBg` receives background strips with X at +4, width at +8, and horizontal map offset at +12. First-stage captures showed 256×32 strips and maps of 24 tiles, each 32 pixels wide. The patch temporarily extends these parameters and restores them after drawing. At the map start, it limits the left extension to avoid wrapping to the final map fragment.

`TileMargins` adds actual map columns to the 32-pixel tile routines. Early first-room captures showed continuous walls, floor, and platforms, but later user reports demonstrate that the experiment is not generally correct.

## Sprite visibility experiment

The September 21 experiment uses `scripts/patch-sprite-visibility.py` before compilation and checks the expected instruction sequences. It extends the direct drawing branch in `80106804` from [-16, 273) to [-59, 316). In `80022118`, it extends the strict limits (-16, 288) to (-59, 331), only for entries calling `80024678` and `80025568`.

Other entries include timers, particle movement, or unverified destinations and retain their original limits. Both copies of the delay-slot instructions and their PGXP tracking are preserved. Disabling the experiment with F8 also disables these margins.

Enemy activation, updates, and removal are unchanged. Extending draw limits alone cannot guarantee that enemies or objects exist in the newly visible area.

## Remaining validation

- Reproduce and fix corrupted imagery and side-margin pop-in.
- Visually verify introduction and gameplay separation (the state guard is covered by regression checks).
- Check other sprite types and enemy activation.
- Test room edges, transitions, and graphics packet capacity.
- Compare every change with the 4:3 reference.

The synthetic checks cover start, partial scroll, full scroll, and original-mode patch state. They do not cover the visual failures reported during gameplay.

## October 9 scope correction

Five new regression cases reproduced background writes in intro/loading states and unrelated stage overlays. Shared eligibility now prevents those writes and terrain-packet emission. All 12 widescreen checks pass. This establishes state isolation, not correct scenery or enemy behavior at every room edge.

## October 12: presentation transition correction

A native OpenGL regression reproduced a stale wide render target being presented
with width 342 and aspect 0 after switching back to 4:3. The presenter now clips
old side margins to the currently requested mode and offsets the texture origin
so the original 256-pixel center is preserved. This also avoids stretching a
normal render target before a wide replacement exists.

`tests/WidescreenGpuChecks` verifies both output dimensions/aspect and actual GPU
pixels using differently colored center/side regions. It passed on macOS Gl33.
Windows/Linux use the same corrected presenter but have not yet run this native
GPU regression. `tests/WidescreenReferenceChecks` compares 32 margin packets with
the game's original tile routines across scroll positions and all four flip
combinations; these cases pass. Neither suite proves full-stage correctness.

## Required before 1.0

Widescreen remains experimental until these checks are complete:

- Validate scenery and room boundaries in all eight stage overlays, in Original
  and Arrange; enable additional stages only after their drawing paths are checked.
- Verify enemy/item visibility and activation at both new edges without changing
  collision, timers, AI behavior or the 4:3 reference gameplay.
- Check camera locks, scrolling transitions, boss entrances and stage exits.
- Preserve HUD layout and sprite proportions; menus, artwork and movies retain
  their intended aspect ratio.
- Validate graphics packet capacity and repeated switches between modes.
- Repeat visual and performance checks on Windows, macOS and Linux.

The Graphics option and this transition correction do not satisfy the complete
1.0 gate. No 1.0 release should be published while the checks above are pending.
