# Windows launcher for Castlevania Chronicles

## Intent and success criteria

The Windows download should open a small native window. The player selects their own USA SLUS-01384 disc image and clicks **Jugar**. The selected path is remembered; the game and saves work without moving the image into the package or using a command prompt. A failed start leaves a readable log and an error message. This does not claim the existing gameplay crash is fixed until a Windows log or reproduction identifies it.

## Current behavior and evidence

`Play.cmd` requires a dragged CUE or `disc/chronicles.cue`. The game executable rejects zero arguments on Windows, though RecompOne already has an in-game disc picker used on macOS. The current Windows package was cross-published on macOS; no Windows execution result is recorded. Settings and memory cards currently depend on the process working directory. No Windows `run.log` is present in the repository, so the reported gameplay crash has no confirmed root cause yet.

## Approaches considered

1. **Separate Windows Forms launcher (selected).** A small `Play.exe` can select and validate a disc before launching the game, even when the game graphics cannot initialize. It gives clear errors and an independent log. It adds one Windows-only project and a Windows Desktop targeting pack to the build.
2. **Use RecompOne's in-game picker.** Few changes, but the graphics and native libraries must initialize before the player can select a disc. Failures before that point remain opaque.
3. **Keep a batch launcher and add a file dialog.** Less build work, but still exposes a console window and Windows script behavior to players.

## Architecture and data flow

- Add a Windows-only .NET 10 Windows Forms project under `launcher/`. It displays the current disc path, **Seleccionar ROM**, and **Jugar**, plus a short status/error line. The file dialog accepts `.cue` and `.chd`; CUE-referenced BIN files remain beside the CUE.
- Store the selected absolute disc path in `%LOCALAPPDATA%/CastlevaniaChroniclesRecomp/launcher.json`. Store game settings, memory cards, and `run.log` in the same directory. Never copy or distribute disc content. The launcher creates the directory before starting the game.
- Validate a candidate before enabling play: file exists, extension is CUE/CHD, the image opens with RecompOne's disc reader, and the disc contains `SLUS_013.84`. Report missing BIN, wrong edition, and unreadable image in the window. This check is for user feedback, not a substitute for full-game compatibility testing.
- Start `CastlevaniaChronicles.exe` with the selected path as one argument and the data directory as its working directory. Capture standard output and error into `run.log`, include exit code and timestamp, and show a concise failure message with a button to open the log folder. The launcher remains responsive while the game runs; repeated launches are disabled until it exits. It stays open to capture the log and may be minimized while playing.
- Keep direct command-line launch of the game for development. Make a zero-argument Windows game launch show the existing picker as a fallback, so opening the engine executable does not immediately exit with usage text. Existing macOS/Linux paths retain their current behavior.
- The Windows package puts a self-contained, single-file `Play.exe` at the top level and updates `Play.cmd` to forward an optional dropped image to the launcher for compatibility. The package remains self-contained and continues to exclude ROMs and saves.

## Error handling

A missing/invalid saved disc path keeps the launcher open and asks for a new selection. An invalid disc does not start the game. A game process failure, nonzero exit, or early termination points to the log. File permission failures when creating the data directory or log are shown in the launcher. Native crashes may not produce a managed stack trace; the launcher still records the exit code and any output received. Logs and user-provided crash evidence guide a separate runtime fix.

## Verification

- Unit tests for path persistence, candidate validation, and process argument/working-directory construction, including spaces and non-ASCII characters in paths.
- Publish the game and launcher for `win-x64`; verify PE architecture, self-contained runtime files, packaging, and absence of disc/save files.
- On a Windows computer, test first launch, selecting CUE/BIN and CHD, relaunch with remembered path, cancel, wrong edition, spaces in paths, game exit, and log/error display. A macOS cross-publish alone does not count as a Windows gameplay pass.
