# Experimental first-stage widescreen

**Current status:** opt-in and incomplete. The user reported corrupted imagery and objects appearing or disappearing at the side margins. A successful build or passing patch-state checks does not demonstrate correct rendering.

The goal is a 16:9 view with more scenery visible, original sprite proportions, and a 4:3 comparison option. The first rendering experiment preserves the reference physics and camera.

```sh
CHRONICLES_WIDESCREEN=1 ./scripts/run-macos.sh
```

F8 toggles the experiment. The default is 4:3. Menus retain 4:3 and HUD elements retain their central positions. The extension is restricted to the `stage1` overlay, which also includes the introduction; the introduction still needs separate handling.

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
- Separate introduction rendering from gameplay.
- Check other sprite types and enemy activation.
- Test room edges, transitions, and graphics packet capacity.
- Compare every change with the 4:3 reference.

The synthetic checks cover start, partial scroll, full scroll, and original-mode patch state. They do not cover the visual failures reported during gameplay.
