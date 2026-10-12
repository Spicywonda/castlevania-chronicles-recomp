# Pause and resume

The original first-stage pause loop at `800181D4` repeatedly calls `PadGetState` and reads the direct controller buffer without presenting another frame. RecompOne refreshed that buffer on frame presentation, so the loop kept reading the original Start press and never observed its release and next press. A managed stack capture located the busy loop; this reproduced as a hang rather than an exception.

`patches/runtime/0005-pad-pause-refresh.patch` refreshes the registered direct controller buffers in `LibPad.PadGetState`. It uses the current host controller state without changing game pause logic.

The regression test changes Start from pressed to released to pressed without presenting a frame and checks the buffer on every query. It failed before the patch and passes afterward. The macOS build succeeded, and the user confirmed that Start/Enter now pauses and resumes gameplay. Windows execution remains unvalidated.

## Held-gamepad regression (issue #1)

A second problem was reproduced after the 0.3.9 release: `InputManager.Poll`
published keyboard buttons before merging gamepad buttons. The game thread can
read that intermediate state inside the original busy pause loop, observing a
false Start release followed by a second press from the still-held gamepad.

`0009-input-button-snapshot.patch` collects keyboard and gamepad buttons locally
and publishes the combined value once per port. A controlled run of the actual
`func_800181D4` stayed paused with steady input, but returned when the old
keyboard-only publication was inserted before the same held gamepad value.
With the patch it stayed paused through that boundary. The regression checks in
`tests/InputSnapshotChecks` failed before the change and passed afterward.
Physical-controller validation on Windows is still pending. This patch is not
included in the already published 0.3.9 archives.

The same issue also reports incorrect boss-defeat/orb sound effects. That audio
report is separate and is not claimed fixed by this input change.
