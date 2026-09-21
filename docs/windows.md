# Windows x64 development package

This is an experimental Windows x64 build of the USA SLUS-01384 Castlevania Chronicles recompilation. The package includes its .NET runtime and native libraries. Keep the complete folder together; the EXE alone is insufficient. Windows execution, graphics, sound, input, pause/resume, and saving still require testing on a Windows computer. Cross-publishing and checking PE headers on macOS do not establish that the game runs on Windows.

## Play

1. Extract the ZIP into a writable local folder.
2. Drag your legally obtained USA SLUS-01384 CUE file onto `Play.cmd`. Keep all BIN files referenced by the CUE beside that CUE, preserving their names. Alternatively, create a `disc` folder beside `Play.cmd`, place the CUE there as `chronicles.cue` with its BIN files, and double-click `Play.cmd`.
3. The launcher stores settings, memory cards, and its latest `run.log` in the package's `runtime` folder. Preserve that folder when replacing the build.

No separate .NET installation is needed. A working Windows graphics driver and compatible OpenGL backend are required. This is an unsigned development executable.

From a command prompt, the executable also accepts the disc path directly:

```bat
CastlevaniaChronicles.exe "C:\Games\Castlevania Chronicles\chronicles.cue"
```

Direct execution stores settings in the current working directory. Starting the EXE without a CUE argument prints usage and exits. Use the launcher for double-click or drag-and-drop startup and persistent error logs.

## Build on macOS or a compatible Bash environment

Prerequisites: .NET SDK 10.0.401 (the repository's local installation is selected by `scripts/dotnet.sh`), Python 3, the pinned RecompOne submodule, and the generated C# game from the USA disc. The initial publish needs NuGet access to restore the Windows .NET runtime and apphost packs.

From the repository root, generate the game if it has not already been generated:

```sh
./scripts/recompile.sh
```

Then publish and package:

```sh
./scripts/publish-windows.sh
```

Each invocation creates a separate `artifacts/windows/build-XXXXXXXX/` directory containing `CastlevaniaChronicles-win-x64/`, its ZIP, and a SHA-256 checksum. The folder also contains a per-file checksum manifest. Do not build another target concurrently: the root project and runtime share intermediate build files.

The script applies the repository's runtime patches, publishes Release for `win-x64` with a self-contained runtime, and disables trimming, single-file bundling, and ReadyToRun. The runtime uses reflection and dynamic code; keeping ordinary assemblies and native DLLs avoids deployment assumptions that have not been validated. Packaging checks the executable, .NET host/runtime, SDL2, GLFW, ImGui, native file dialog, and OpenAL libraries for x64 Windows PE headers, and checks the self-contained runtime configuration.

The ZIP excludes disc images and saves, but its compiled game assembly is derived from the original game. Development packages are published separately as release assets; build outputs remain excluded from the source tree. You must provide the USA disc image at runtime.

## Windows validation still needed

Check startup with a disc path containing spaces, first-stage gameplay, audio, keyboard/controller input, the pause menu, pause/resume, fullscreen, and memory-card save/load. Confirm that failed starts leave a useful `runtime/run.log`. Report the executable version and log with any failure; do not publicly upload disc data, generated game code, or RAM dumps.

## Cross-publish verification

On September 21, 2026, the publish command completed successfully on macOS for `win-x64`. Packaging verified x64 PE headers for the EXE, .NET host/runtime, SDL2, GLFW, ImGui, native file dialog, and OpenAL, and verified that the .NET runtime configuration is self-contained. The build includes the pause input-refresh patch and English UI defaults; widescreen remains disabled unless enabled through `CHRONICLES_WIDESCREEN=1`, which also enables the F8 comparison toggle. Compiler output included existing nullable warnings in runtime debug panels and unreachable-code warnings in generated game code. No Windows execution test has been performed.
