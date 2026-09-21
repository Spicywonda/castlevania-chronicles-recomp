# Overlay and graphics investigation — SLUS-01384

This is a historical account of the path to the first playable stage. See the [README](../README.md) for current compatibility and known failures.

## Boot overlay

The initial runtime initialized OpenGL 4.1 on Apple M3 and selected the Gl33 backend. Execution reached `func_80011B04`, then failed when calling `0x8010C7E4`, outside the main segment.

Before that call, `func_80014A38` receives A0=9 and A1=0. It consults a 32-bit sector table at `0x8004EC28`, a 16-bit sector-length table at `0x8004EE2C`, and a read destination stored at `0x80010C90`.

In the crash dump, entry 9 specified LBA 650, 61 sectors, and destination `0x80100000`. `DRACULA.PAC` starts at LBA 171, giving archive offset `(650 - 171) * 2048 = 0xEF800` and length `61 * 2048 = 124928` bytes. The extracted MODE2/2352 sectors exactly matched all 124,928 bytes of the corresponding RAM region. This particular load showed no evidence of compression or relocation.

The configuration names this block `boot`, a project identifier. `0x8010C7E4` is an explicit entry point; automatic analysis discovers other functions, whose boundaries still require runtime validation.

## Controller initialization

After recompiling `boot`, the dispatcher loaded it at the corresponding LBA and execution passed `0x8010C7E4`. The next failure was an indirect call to zero inside `0x8004BA00`, immediately following `PadInitDirect`.

Its 51 instructions matched the masked 204-byte SDK 4.6/4.7 `PadStartCom` signature in RecompOne's `AutoConfigure/signatures/psyq.json`. Renaming the generic map entry to `PadStartCom` made the recompiler use `RecompOne.Runtime.Sdk.LibPad.PadStartCom`, consistent with the existing HLE controller initialization.

## Observed archive table

| Index | LBA | Sectors |
|---|---:|---:|
| 1 | 171 | 57 |
| 2 | 228 | 66 |
| 3 | 294 | 56 |
| 4 | 350 | 56 |
| 5 | 406 | 55 |
| 6 | 461 | 55 |
| 7 | 516 | 66 |
| 8 | 582 | 68 |
| 9 | 650 | 61 |

These are table values, not evidence that every block executed or consists entirely of code. Additional overlay definitions must be checked against their loading paths.

## SPU stream completion

The main menu became visible after the controller fix, but selecting a mode produced a black screen. A stack capture showed `func_80014B3C`, called from `boot.func_80107700`, waiting in a loop with `VSync` until `0x80059320` became zero.

`func_80015A38` writes that value as the stream-finished callback registered through `SpuStSetStreamFinishedCallback` in `func_800154D4`. The pinned runtime did not retain the SPU IRQ address register (`0x1F801DA4`), expose the SPUSTAT IRQ bit, or generate IRQ9 on ADPCM reads.

The local SPU patch adds these operations and IRQ handling for DMA writes. Interrupt delivery runs on the game thread so that the audio mixer does not execute game callbacks. Synthetic regression checks accompanied the change. This does not establish complete SPU accuracy; capture, reverb IRQs, and other accesses need separate validation if required.

Hardware reference used during the investigation: [PSX-SPX SPU interrupt documentation](https://psx-spx.consoledev.net/soundprocessingunitspu/#spu-interrupt).

## Sector cursor and function boundary

The initial IRQ patch did not resolve the transition. SPU RAM contained CD sector headers inside ADPCM blocks. `CdGetSector` used LibDs's LBA but an independent FIFO cursor, ignoring the 12 header bytes already skipped by LibDs. `0002-libds-sector-cursor.patch` shares the cursor. Two synthetic checks reproduced the defect in modes 0x80 and 0xA0 before the fix.

The following run wrote to `FFFF93EC` inside `80102030`. The function map incorrectly put an entry at `80042A54`, the `addiu sp,sp,32` delay-slot instruction following `jr ra` at `80042A50`. Bytes were verified directly against the disc executable. Extending `800429F8` from 92 to 96 bytes and moving the next entry to `80042A58` with length 184 restored the omitted stack adjustment.

## First-stage overlay

Execution then reached state 15, stage index 0, and failed at indirect call `801021F4`. RAM at `80100000..8011C7FF` exactly matched the 57 sectors beginning at LBA 171. The `stage1` overlay was added with archive offset 0, size 116,736, and entry `801021F4`.

The resulting build displayed Simon approaching the castle and logged `loaded overlay: stage1`. The user subsequently confirmed movement, enemies, objects, and combat. Stage scenery remained black, narrowing the next investigation to `GsSortFastBg`, which draws the stage layers through different routines from character sprites.

Optional pre/post hooks, limited to 64 calls and enabled with `CHRONICLES_DIAGNOSTICS=1`, recorded maps and packets. A VSync diagnostic also captured VRAM.

## Black scenery and VRAM readback ordering

`GsSortFastBg` captures contained valid E1 plus 32×32 sprite packets and texture data, but empty palettes. StoreImage diagnostics found discrepancies between the prior CPU shadow and GPU readback: 3,894/8,192 colors at x960 and 3,109/8,192 at x976. `tests/GpuChecks` reproduced palette loss in four synthetic regions without game data.

`InterpBackend.ReadVram` executed only `_current` while new writes remained in `_recording` or `_ready`. `0003-gpu-readback-order.patch` executes those queues in order before a synchronous read, clears consumed commands, and invalidates prior interpolation. The four GPU checks then recovered all expected colors, and the existing SPU/CD checks continued to pass.

Later visual testing showed STAGE 01 with floor, platforms, stone blocks, decorated walls, curtains, and background restored. The user continued playing while the time and score advanced. Audio was validated separately afterward; see [audio investigation](audio.md). Later stages and saving remain unvalidated.
