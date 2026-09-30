# Windows x64 development package

This experimental package contains the Castlevania Chronicles recompilation, a small Windows launcher, and a self-contained .NET runtime. You need your own USA SLUS-01384 disc image. Keep the extracted folder together; the EXE files alone are insufficient. The first stage reached the bat boss on a GTX 1060 in Windows testing. A crash after collecting the boss orb exposed a missing archive overlay; this build includes that overlay, but the transition has not yet been retested on Windows. Later stages, saving, and full-game compatibility remain unverified.

## Play

1. Extract the ZIP to a local folder.
2. Double-click `Play.exe`.
3. Click **Seleccionar ROM** and choose your USA CUE or CHD, then click **Jugar**. Keep every BIN named by a CUE beside it. The launcher remembers your selection on later starts.

The disc is validated before the game starts. The launcher stores its selection, game settings, memory cards, and the latest `run.log` in `%LOCALAPPDATA%\CastlevaniaChroniclesRecomp`. **Abrir carpeta de logs** opens that folder. If disc validation fails, read `validate.log` in the same folder. After a gameplay failure, send `run.log` and the steps that led to it; do not send the ROM or a RAM dump. The old `Play.cmd` still forwards a dragged CUE/CHD to the new launcher. Direct `CastlevaniaChronicles.exe <image>` startup remains available for development, and opening the game EXE without arguments shows the in-game picker.

No separate .NET installation is needed. A working Windows graphics driver and compatible OpenGL backend are required. This is an unsigned development executable.

## Build on macOS or a compatible Bash environment

Prerequisites: .NET SDK 10.0.401 (selected by `scripts/dotnet.sh`), Python 3, the pinned RecompOne submodule, and the USA disc linked at `disc/chronicles.cue` with its BIN files. The initial publish needs NuGet access to restore Windows runtime and Windows Desktop targeting packs.

```sh
./scripts/recompile.sh
./scripts/publish-windows.sh
```

Each invocation creates a separate `artifacts/windows/build-XXXXXXXX/` directory with the package, ZIP, and SHA-256 checksum. The script applies the repository's runtime patches, publishes the game and single-file launcher for `win-x64`, and checks the PE headers and self-contained game runtime. Disc images, memory cards, and saves are excluded. Do not build another target concurrently because the projects share intermediate files.

The compiled game assembly is derived from the disc used for recompilation. This package is for development testing with your own matching USA image; it does not include a disc.

## Verification limits

A macOS cross-publish checks structure and dependencies, not Windows gameplay. On Windows, test the launcher with CUE/BIN and CHD, a path containing spaces, relaunch with the remembered path, first-stage gameplay, the boss orb transition, audio, controller/keyboard input, and memory-card save/load. Report the executable version, selected mode, reproduction steps, and `run.log` for a failure.
