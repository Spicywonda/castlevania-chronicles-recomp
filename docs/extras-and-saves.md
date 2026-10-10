# Memory cards and extras

Both `Play` and the game executable now use the launcher's per-user data directory. `CHRONICLES_DATA_DIR` can override that location. The selected disc path is resolved before changing directories.

On startup, missing `carda.sav` and `cardb.sav` files are copied from the old working directory, executable directory or its parent. Existing destination cards and original files are preserved. If an existing card takes priority over a downloaded card, use **Import card (slot 1)** in the ROM selector while the game is stopped. The importer accepts raw 128 KB PlayStation memory cards (`.sav`, `.mcr`, `.mcd`); individual game-save containers need conversion to a raw card first. Replacing a card saves a uniquely named `.bak` beside it. A custom slot-one path in `settings.json` is respected.

The illustration viewer now presents a frame while polling input. The hook is restricted to its original call site; it preserves the guest CPU registers. The interview ignores its confirm/skip buttons until they have been released once, preventing the selection press from immediately skipping the movie. Later presses still skip normally. Opening and ending movie input is unchanged.

`tests/ExtrasChecks` covers frame pacing/register preservation, interview release/repress behavior, non-overwriting card adoption, import backup and invalid-card rejection. The user confirmed the extras opened in the macOS test package. These automated checks do not verify the full video/audio stream; Windows and Linux still need native playback checks.

## Movie audio polling fix

The game's `func_80028F5C` polls `StGetNext` up to 2000 times. At native execution speed this exhausted the retry count before the next paced STR frame arrived. The fallback calls `DsRead2`, whose command packet starts with `Pause`; the runtime then resets XA audio and discards queued sound. The patch yields for 1 ms between unsuccessful movie-frame polls, retaining the bounded retry for genuine stalls. This is confined to the movie routine; gameplay audio code is unchanged.

A 35-second live interview probe after the change recorded one initial read, no subsequent read restarts and no nonzero XA hold in sampled diagnostics. PCM comparisons at four positions maintained a constant offset against separately decoded disc audio, with correlations 0.913–0.987 at the analysis sample rate. The prior live probe showed repeated retries and nonzero XA holds. This verifies the reproduced streaming fault on macOS, not every movie or every output device. Windows and Linux use the same shared patch and still need native playback checks.
