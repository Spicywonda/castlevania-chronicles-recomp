# macOS Apple Silicon package

Download `CastlevaniaChronicles-macos-arm64.zip` from GitHub Releases and extract it. Move `Castlevania Chronicles.app` to your preferred location and open it. The app includes .NET and does not need the development workspace or an SDK installation.

On first launch, select your USA SLUS-01384 CUE file in the disc picker. Keep its referenced BIN files alongside the CUE. The disc is required at runtime and is not included in the download.

Settings, memory cards, and `run.log` are stored in `~/Library/Application Support/Castlevania Chronicles Recomp`. Preserve that folder when updating. The app starts in 4:3 with English menus.

This package targets Apple Silicon and macOS 14 or later. It is ad-hoc signed for local execution, not Developer ID signed or notarized. macOS may require an explicit user approval to open a downloaded application. Intel Macs are not supported by this package.

First-stage gameplay and pause/resume have been tested on the development Mac. Later stages and saving remain unvalidated. Widescreen is experimental and disabled by default.

To build the release package after generating the game code:

```sh
./scripts/publish-macos.sh
```

The build requires the pinned .NET SDK, Python 3, and the macOS `lipo`, `codesign`, and `ditto` tools. Build outputs stay under `artifacts/macos/` and are excluded from Git.

## Package verification

The release app was extracted under a temporary directory outside the development workspace and launched with local .NET SDK variables removed. The disc picker configuration selected the USA disc and the game reached its load screen. The relocated app passed `codesign --verify --deep --strict`. This is an Apple Silicon smoke test, not full-game compatibility validation.
