# Windows stage-two falling-death slowdown — 0.3.1 diagnostic

Report: Windows, GTX 1060 3GB, GL45, infinite health enabled, stage two rising-water
section, slowdown on falling death affecting the desktop. The user closed the game.
The supplied 0.3 log reaches stage2 and ends with exit=0; this reflects the manual
close and does not diagnose the preceding slowdown. There is no logged exception.

No root cause is confirmed. The cheat helper excludes state 22 (death), and the
player update explicitly selects that state for a fall. That observation alone does
not rule out another interaction or a GPU/runtime problem.

0.3.1 adds observation only; gameplay and graphics configuration remain as in 0.3:

- [State]: rate-limited state/stage/room changes, zero-health transitions and cheat mask.
- [Performance]: process CPU (100% = one core), working/private/managed memory,
  VSync-call rate and age of last sampled VSync. VSync calls are not rendered FPS.
- [Snapshot]: the latest immutable game-thread snapshot, including player flags,
  health, lives, fixed-point Y, caller return address and cheat mask.

RAM sampling is capped near 60 Hz and never occurs on the timer thread. Messages
are bounded (at most four state messages per second, performance every five seconds).
Tests verify no RAM mutation and that the sampler works without game progress.

Request run.log after the next affected session. Stop promptly if desktop slowdown
returns. Windows reproduction and a confirmed fix remain outstanding.

## Received 0.3.1 log (2026-09-30, started 21:53 UTC)

Stage 2 room 2 enters death state 22 at 414294 ms, then returns to playing
state 15 at 417577 ms with lives reduced from 3 to 2. The stall follows respawn.
At 424986 ms private memory is 1721.8 MiB and working set 1598.2 MiB; at
439985 ms those are 3996.0 and 3730.5 MiB. Managed heap remains about 101.4 MiB,
CPU approaches two full cores, and VSync calls stop. This indicates runaway
memory outside the reported managed heap, not a confirmed allocation site.
The last sampled RA 80044C00 is a VSync(-1) caller in func_80044BF0, not a
sampled current instruction pointer or a stack trace of the blocked thread.

## 0.3.2 diagnostic protection

Source inspection and regression tests found that both DrawOTag and GPU DMA
silently replayed cyclic packet lists up to a million times. A pooled RAM-word
bitmap now rejects the first repeated header, including mirrored addresses,
with an exception naming the chain start, repeated address and packet count.
Eight regression cases cover valid, self-cycle, two-node and mirrored chains
through both entry points. Six cycle cases failed before the fix and pass after.
This is a confirmed runtime protection defect; whether it triggers in the
reported stage-two respawn remains unconfirmed.

The diagnostic sampler checks once per second, retaining five-second routine
logging. It exits with code 3 when VSync samples have stalled for at least two
seconds, private memory is at least 1 GiB, and growth since the prior sample is
at least 128 MiB; or when stalled for ten seconds with at least 2 GiB private
memory. The thresholds are a protective heuristic, not a guaranteed memory cap.
The terminal guard message and latest snapshot are flushed before process exit.
Tests cover runaway, stable memory, active progress and normal memory use.

[GpuWork] adds lock-free approximate replay operation/index/count and native
batch start/completion counters. These are breadcrumbs, not atomic snapshots.
No cheats or game state transitions were changed. Windows reproduction and
confirmation of the original cause remain outstanding; this is a diagnostic
build, not a claim that stage-two progression has been repaired.

## Received 0.3.2 log (started 2026-10-01 02:08 UTC)

Stage 2 room 2 death at 544692 ms, respawn at 547976 ms, followed by:
`cyclic GPU packet chain: start=0x0005A804 repeated=0x00097B28 packets=2`.
The stack is DrawOTag -> 80041C80 -> 80012640 -> stage2 80101124. CPU RA
80041C94 and A0 8005A804 agree with the draw submission. The cycle detector
fired before the earlier runaway; last routine memory sample was 214 MiB
working set / 376.5 MiB private. Cheat mask 5 means health and lives enabled.

80097B28 and 80097B38 are the two reusable three-word FILL packets owned by
GsSortClear. GsSortClear calls AddPrim at return address 80041230. If its
packet is already the OT head, the ordinary insertion overwrites its next
link with itself. A synthetic test using the actual generated GsSortClear
and AddPrim functions reproduces this self-cycle for both display buffers.

0.3.3 adds a narrowly scoped AddPrim prehook: only that GsSortClear caller,
those two packets, valid FILL headers, and an already identical OT head.
It preserves the existing link, including the tail, while GsSortClear still
updates the color and geometry. It does not hide pre-existing self-cycles
or alter ordinary primitive insertion. General cycle/runaway guards remain.
The hook is recorded in the recompiler configuration, not only generated code.
Local synthetic reproduction is verified separately from Windows gameplay;
confirmation of the complete falling-death sequence on Windows is still needed.
