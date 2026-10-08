# Audio investigation

Investigation recorded September 20, 2026. **Historical result:** the user confirmed no audible cuts in the tested reference after the mixer was synchronized with completion of the SPU IRQ callback's DMA refill. This validates the tested audio, not every stage or mode.

## Optional capture

`patches/runtime/0004-audio-capture-diagnostics.patch` captures SPU output before device gain when `RECOMP_AUDIO_CAPTURE=1`. Capture is disabled by default and records at most 60 seconds of stereo 16-bit PCM at 44,100 Hz to `audio-capture.pcm` in the runtime directory. It does not mix additional samples or advance SPU state to capture audio.

```sh
RECOMP_AUDIO_CAPTURE=1 CHRONICLES_DIAGNOSTICS=1 ./scripts/run-macos.sh
```

Capture files remain under the ignored `artifacts/` directory. Do not publish audio captures or extracted game data.

The first complete capture contained 10,584,000 bytes, an absolute peak of 25,837/32,768, no samples at or above 32,760, and RMS 3,520.14. These measurements establish signal level, not musical fidelity or timing.

## Audible cuts and rejected VSync experiment

The user reported small cuts in a low-volume preview. The PCM contained near-silent intervals of 424.63 ms at 23.8991 s and 459.30 ms at 32.2699 s, using an absolute sample threshold below 8 on both channels. Because these gaps existed before OpenAL, device buffer starvation could not explain them in that capture. Whether they matched game transitions was unresolved. A later preview without gaps meeting that threshold still sounded discontinuous.

IRQ timing diagnostics compared delivery delay against 896 samples, corresponding to 32 ADPCM blocks of 28 samples in one 512-byte half-buffer. This was a conservative reference because pitch was slightly below 4096.

| Capture | IRQ deliveries above 896 samples | Maximum delay |
|---|---:|---:|
| Baseline | 77 / 1,559 (4.94%) | 1,401 samples (31.77 ms) |
| Shorter VSync sleeps | 1 / 2,072 (0.048%) | 1,913 samples (43.38 ms) |

The shorter-sleep patch polled every 2 ms while waiting for VSync. Despite fewer late deliveries, the user heard more cuts. The experiment was removed from automatic patch application.

Polling also pumps LibCd and LibDs. `LibDs.Pump` can advance a streaming sector on each call without checking elapsed time, making polling frequency an uncontrolled factor. This was a hypothesis to investigate, not a demonstrated cause. `scripts/check-audio-timing.py` remains a latency diagnostic, not a sufficient musical-fidelity test.

## ADPCM block tracing

The optional capture records voice 22/23 reads in `audio-blocks.bin`: output sample, SPU address, DMA generation, voice, and 16 ADPCM bytes. Records are little-endian `<qIIiQQ`, 36 bytes each, capped at 200,000 and written when the 60-second capture completes.

`scripts/analyze-audio-blocks.py` identifies repeated block reads without a refill. `scripts/match-audio-blocks.py` matches content against the local disc's `MUSIC.VB2`, ignoring the flags byte modified by streaming and excluding ambiguous matches.

The baseline recorded 141,638 reads, of which 140,656 matched uniquely. It contained 8,099 reads without a new DMA generation, 924 source-position reversals of 1,008 bytes, and 850 subsequent jumps of 1,040 bytes. Reversals appeared in both channels, establishing reuse of earlier data while leaving intentional transitions to be distinguished.

## Synchronization experiments

First, the output mixer waited up to 20 ms between buffers while an SPU IRQ remained pending. It released the register/DMA lock while waiting and woke when the IRQ was acknowledged. Synthetic checks covered acknowledgement and the timeout when the CPU did not service the interrupt.

This reduced reads without refill to 4,150 and eliminated deliveries above 896 samples, but increased source reversals to 2,141. Fewer repeated blocks did not mean fewer discontinuities.

Next, waiting moved to the start of each 16-sample chunk within `Spu.Mix`, enabled only for the output mixer. A check reproduced an IRQ arising inside a buffer and verified that the mixer yielded before consuming the entire buffer. The resulting capture had 2,722 reads without refill and 1,535 source reversals across 163,836 blocks, with a maximum IRQ delay of 28 samples. Continuity was still incomplete.

The final correction addressed a race: acknowledging the IRQ cleared its bit before the callback finished its DMA transfers. The mixer now resumes after `Interrupts.PollSlow` completes the callback, restores CPU state, and verifies acknowledgement. A synchronization check reproduced premature resumption before the DMA refill and passed after this change.

## Result after waiting for DMA completion

The recorded 60-second capture in `artifacts/audio-complete-refill/` contained:

- 168,358 observed blocks, 168,046 uniquely identified on the disc.
- **Zero reads without refill and zero source-position reversals.**
- 167,958 normal 16-byte advances and 74 jumps of 32,784 bytes, consistent with the file's interleaved stereo chunks.
- 2,625 IRQ deliveries, a maximum delay of 25 samples, and none above 896.
- One OpenAL restart, the same as the baseline; the counter includes startup and cannot locate an audible pause by itself.

At that point, ten SPU/CD checks and four GPU checks passed, and compilation completed without errors. Existing generated-code warnings and a NuGet audit connectivity warning remained. These are historical validation results, not a claim that checks have been rerun for every later change.

The user then confirmed that the preview had no cuts. Active patches 0001 and 0004 include CPU/mixer coordination and the optional diagnostics. Further gameplay, stages, and modes still require audio validation.


## October 2026: manual SPU transfers and shared preview 0.3.5

The user reported microcuts on all platforms. A 60-second macOS baseline recorded
an OpenAL restart after startup. Increasing the output queue alone was insufficient:
a repeat capture still had 1,548 backward/repeated disc-source steps while the
queue remained healthy. Waking CPU polling early or changing mixer cadence did
not reliably solve the source-order problem; neither experiment is included.

The SPU register dispatcher ignored writes to **0x1F801DA8**, the manual transfer
port. The original game driver `func_8002D630` writes its data through this port,
including the silent ADPCM loop at SPU RAM **0x1000**. Captures showed zero bytes
there instead of the loop marker. Silent/effect voices could then walk into the
music buffers; a trace recorded a music refill IRQ before voices 22/23 reached
its address. Such an IRQ changes the streaming buffer order prematurely.

The runtime now accepts little-endian manual writes, advances a separate internal
byte cursor with SPU RAM wrapping, and retains the programmed transfer-address
register for readback. Manual writes use the existing synchronous RAM/IRQ path.
DMA advances the same internal cursor. This matches the runtime's existing
immediate-transfer model; it is not cycle-accurate FIFO emulation. The register
semantics were also checked against the [DuckStation SPU implementation](https://github.com/stenzek/duckstation/blob/master/src/core/spu.cpp).

Two synthetic checks failed before the correction and passed afterwards: a manual
upload crossing the RAM boundary, and a silent voice that must never enter the
music IRQ buffer. Two integration checks execute the original game's manual
transfer driver for 16-byte and 80-byte transfers; both failed before and passed
afterwards. The remaining runtime checks continue to pass.

The shared OpenAL queue also increases from 8 to 12 buffers of 256 stereo frames
at 44,100 Hz: **46.4 to 69.7 ms**, adding about 23.2 ms of queued audio. The original
5 ms refill cadence and SPU IRQ/DMA synchronization are retained. `[Audio]` lines
in `run.log` report queue reserve, refill passes, restarts including startup,
fully drained queues after startup, and maximum mixing time. Diagnostic timer
callbacks read counters only and never call OpenAL.

The first corrected 60-second gameplay capture (`artifacts/audio-20261007-pio-fixed`)
contained 151,526 music blocks (150,502 unique disc matches), **zero backward or
repeated source steps**, and no effect-voice reads into the music buffer. All
2,365 recorded refill IRQs came from music voice 22. Temporary full-voice/event
tracing used for diagnosis was removed afterwards; optional normal captures stay
limited to voices 22/23. Neither captures nor game data are packaged.

The automated `tests/AudioCapture` harness selects a supplied local disc and uses
the emulated controller to enter play and exercise effects. Menu-only, reset, and
interrupted captures are not validation. These are bounded macOS checks, not
full-game or physical Windows/Linux device validation. All three packages use the
same runtime and patch series, including the prior cheat, respawn and reset fixes.

Final source verification passed 41 checks: 21 runtime, 2 original-driver audio,
11 cheat/diagnostic, 4 respawn, and 3 reset checks. A subsequent capture included
a hard reset, so it is not counted as a second uninterrupted minute; its traced
post-reset music still had zero backward/repeated source steps. The timing check
now rejects captures whose SPU sample count does not match the capture duration.
