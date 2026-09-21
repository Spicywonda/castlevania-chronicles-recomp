# Linux x64 package

Extract `CastlevaniaChronicles-linux-x64.tar.gz` and run:

```sh
cd CastlevaniaChronicles-linux-x64
./Play.sh '/path/to/Castlevania Chronicles (USA).cue'
```

Keep the BIN files referenced by the CUE beside it. Alternatively, place the CUE at `disc/chronicles.cue` inside the extracted package and run `./Play.sh` without arguments. Disc images are not included.

The package contains .NET and the native application libraries. It still requires a compatible glibc-based Linux desktop, working OpenGL graphics drivers, and the system libraries used by the bundled native dependencies. Direct native dependencies include glibc, libstdc++, libgcc, and GTK 3/GLib for the file dialog. The desktop graphics/audio backends and .NET may also load system libraries dynamically. Use `ldd ./CastlevaniaChronicles` and `ldd ./nfd.so` to diagnose missing direct dependencies. It is not a static binary or an AppImage. Linux ARM and musl-based distributions are not targets of this package.

Settings, memory cards, and `run.log` are stored under `${XDG_DATA_HOME:-$HOME/.local/share}/castlevania-chronicles-recomp`. Keep this directory when updating the application. `CHRONICLES_DATA_DIR` overrides the storage directory. The launcher accepts paths containing spaces.

The default view is 4:3. Widescreen remains experimental and must be explicitly enabled with `CHRONICLES_WIDESCREEN=1`. First-stage gameplay and pause/resume have been tested on macOS; Linux startup, input, audio, graphics, and gameplay still require testing on Linux hardware. A cross-build and valid ELF headers do not establish runtime compatibility.

To build after generating the game code:

```sh
./scripts/publish-linux.sh
```

The build creates a fresh folder under `artifacts/linux/`, a compressed archive preserving executable permissions, a per-file checksum manifest, and a SHA-256 checksum for the archive. Keep the complete extracted directory together.
