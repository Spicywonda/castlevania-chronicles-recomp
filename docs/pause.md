# Pause and resume

The original first-stage pause loop at `800181D4` repeatedly calls `PadGetState` and reads the direct controller buffer without presenting another frame. RecompOne refreshed that buffer on frame presentation, so the loop kept reading the original Start press and never observed its release and next press. A managed stack capture located the busy loop; this reproduced as a hang rather than an exception.

`patches/runtime/0005-pad-pause-refresh.patch` refreshes the registered direct controller buffers in `LibPad.PadGetState`. It uses the current host controller state without changing game pause logic.

The regression test changes Start from pressed to released to pressed without presenting a frame and checks the buffer on every query. It failed before the patch and passes afterward. The macOS build succeeded, and the user confirmed that Start/Enter now pauses and resumes gameplay. Windows execution remains unvalidated.
