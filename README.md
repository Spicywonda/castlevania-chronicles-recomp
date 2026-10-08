# Castlevania Chronicles Recomp

An experimental recompilation of the PlayStation USA release **SLUS-01384**, built with [RecompOne](https://github.com/BlackLabelHQ/RecompOne).

**Early development:** the first stage is playable on macOS Apple Silicon, with movement, combat, scenery, and audio confirmed in local testing. Windows testing reached the bat boss; crashes after collecting the orb exposed missing transition and stage-two overlays. All eight stage entries are now registered for retesting. The Start/Enter pause hang is fixed; pause/resume was confirmed on macOS. Widescreen has reported visual corruption and object pop-in; it is experimental and disabled by default. Later stages, saving, and Linux gameplay have not been validated.

The Git tree contains project sources; development binaries are provided separately through [GitHub Releases](https://github.com/Spicywonda/castlevania-chronicles-recomp/releases). You must supply your own disc image to generate the game code and run the application. Disc images, extracted assets, generated code, memory dumps, local toolchains, and build outputs must stay outside Git.

## Latest preview: 0.3.6

The three platform packages share the same game code and runtime fixes:

- English desktop launcher and in-game menus.
- Keyboard and gamepad remapping with saved bindings.
- Contact invincibility, infinite health, hearts and lives.
- Stage transition coverage for all eight stage overlays.
- Respawn GPU-chain and Hard Reset fixes.
- Corrected manual SPU uploads and a shared audio output reserve.

50 automated checks passed for this preview. Physical controller input and Windows/Linux gameplay still need testing; full-game compatibility is not established.

## Downloads

Releases include separate downloads for Windows x64, macOS Apple Silicon, and Linux x64, each with a SHA-256 checksum. All include .NET and require your own USA disc image. Releases remain early development builds; the release label does not imply full-game or cross-platform validation.

- [Windows package instructions](docs/windows.md)
- [macOS app instructions](docs/macos.md)
- [Linux package instructions](docs/linux.md)
- [Download releases](https://github.com/Spicywonda/castlevania-chronicles-recomp/releases)

The desktop preview uses the same English launcher on all three platforms: choose a disc, then **Play**. Windows and Linux expose one launcher plus a private `runtime` folder; macOS uses an app bundle. Build with `scripts/publish-windows.sh`, `scripts/publish-macos.sh`, or `scripts/publish-linux.sh`; outputs are under `artifacts/desktop/`.

## Requirements

- macOS Apple Silicon for the currently tested development setup (Apple M3).
- .NET SDK **10.0.401**, as specified in `global.json`.
- Git, Bash, and Python 3.
- The USA SLUS-01384 disc image in CUE/BIN format.

Initialize the pinned RecompOne dependency:

```sh
git submodule update --init --recursive
```

Place the CUE at `disc/chronicles.cue` and its referenced BIN files alongside it, preserving the filenames in the CUE. The development BIN has SHA-256:

```text
71c988db8435848d70024f0af2f47dfb0f970782fe1a84098d3210c5d6d3a0d8
```

This is a locally measured fingerprint. Other editions require their own analysis and configuration.

## Build and run on macOS

From the repository root:

```sh
./scripts/recompile.sh
./scripts/run-macos.sh
```

Recompilation applies the local runtime patches, generates C# from the disc, and builds the application. `scripts/dotnet.sh` uses `.tools/dotnet/dotnet` if present, otherwise the SDK on your PATH. The application still needs the disc at runtime.

To run with another CUE of the same edition after building:

```sh
./scripts/run-macos.sh '/path/to/game.cue'
```

Settings and memory cards are stored in `artifacts/runtime/`. Saving has not yet been validated.

For a local app that opens by double-clicking:

```sh
python3 scripts/package-macos-dev.py
```

The output is `artifacts/Castlevania Chronicles Dev.app`. It must remain inside the project and requires the local SDK at `.tools/dotnet`; it is a development launcher, not a standalone release. Its log is `artifacts/run-app.log`.

Windows build instructions and validation limits are tracked in [the Windows guide](docs/windows.md).

## Controls

Press **F1 → Controls → Keyboard and gamepad...** to reassign keyboard keys and controller buttons. Changes are saved automatically. **Reset defaults** restores the selected input type and player port.

## Cheats

During play, press **F1** to show the menu bar, then choose **Cheats**.
Invincibility, infinite health, infinite hearts and infinite lives are optional, start disabled,
and can be switched off together with **Disable all**. Settings last for the current
session. Invincibility prevents contact damage and knockback. Neither invincibility nor infinite health prevents falls or instant death. See [cheat behavior and
validation](docs/cheats.md).

## Experimental widescreen

The default reference is 4:3. To try the incomplete first-stage extension:

```sh
CHRONICLES_WIDESCREEN=1 ./scripts/run-macos.sh
```

F8 toggles the experiment. Corrupted imagery and object pop-in have been reported. The introduction, room boundaries, transitions, and enemy activation still need work; this is not complete 16:9 support. See [widescreen research](docs/widescreen.md).

## Development checks

```sh
./scripts/apply-runtime-patches.sh
./scripts/dotnet.sh run --project tests/RuntimeChecks -c Release
./scripts/dotnet.sh run --project tests/AudioChecks -c Release
./scripts/dotnet.sh run --project tests/CheatChecks -c Release
./scripts/dotnet.sh run --project tests/ControlsChecks -c Release
./scripts/dotnet.sh run --project tests/RespawnChecks -c Release
./scripts/dotnet.sh run --project tests/HardResetChecks -c Release
./scripts/dotnet.sh run --project tests/WidescreenChecks -c Release
./scripts/dotnet.sh run --project tests/GpuChecks -c Release
```

Runtime and audio checks cover manual SPU transfers, the silent audio loop, SPU IRQ delivery, DMA synchronization, and mixed LibDs/CdGetSector reads with synthetic data. Widescreen checks cover patch state. GPU checks open a diagnostic window and exercise palette readback. Passing these checks does not establish full-game compatibility or correct widescreen rendering.

RecompOne is pinned to `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`; local runtime changes are maintained in `patches/runtime/`.

## Reporting problems

Include your OS, hardware, commit, disc edition, selected game mode, reproduction steps, and whether widescreen was enabled. See [CONTRIBUTING.md](CONTRIBUTING.md).

For additional local diagnostics:

```sh
mkdir -p artifacts
CHRONICLES_DIAGNOSTICS=1 CHRONICLES_DUMP_RAM=1 ./scripts/run-macos.sh > artifacts/diagnostic.log 2>&1
```

On an exception, RAM dumping writes `artifacts/runtime/crash-ram.bin`. Failure exits with code 1; invalid arguments exit with code 2. Review logs before sharing. Do not upload RAM dumps, audio captures, extracted game data, disc images, or generated code.

## Technical notes

- [Initial research](docs/initial-research.md)
- [macOS development plan](docs/plan-macos.md)
- [Overlay and graphics investigation](docs/overlays.md)
- [Audio investigation and validation](docs/audio.md)
- [Pause/resume fix](docs/pause.md)
- [Widescreen experiment](docs/widescreen.md)
