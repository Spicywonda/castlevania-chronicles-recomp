# Hard Reset access violation — 0.3.4

Windows report: native access violation 0xC0000005 in InterpBackend.Replay ->
Settle -> Flush -> Runtime.ResetForBoot -> RunGame.

RunGame executes on the game worker. ResetForBoot called backend.Flush directly;
with interpolation enabled this replays graphics commands, including OpenGL
calls, on that worker without the window's current GL context. It also retained
previous, current, ready and recording frame graphs across the restart.

ResetForBoot now waits for a GpuJobs operation on the graphics owner. Runtime
teardown and the graphics reset run there, serialized with presentation. The
interpolation reset discards every old frame graph, resets its timing/groups,
releases frozen interpolation display state, and clears VRAM through the inner
backend directly. The clear is flushed before the game worker may boot again.
The interpolation reset rejects a call from any other claimed thread.

Verification:

- Three regression checks failed before the change: wrong thread with and without
  interpolation, and surviving queued frames.
- All pass after the change, including three consecutive resets followed by fresh
  frame publication/rendering and a full VRAM clear.
- Native Mac OpenGL 4.1 / Gl33: four palette round trips and three consecutive
  resets pass; each reset clears VRAM and accepts new image uploads/readback.
- Windows GL45 runtime confirmation remains to be performed on the user's PC.

Patch: patches/runtime/0007-hard-reset-graphics-owner.patch. It applies on top of
the existing runtime patches and is retained by the normal publishing scripts.
