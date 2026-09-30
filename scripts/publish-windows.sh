#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"
if [[ ! -f generated/Entry.cs ]]; then
    printf '%s\n' 'Generate the USA SLUS-01384 game first with ./scripts/recompile.sh.' >&2
    exit 1
fi
scripts/apply-runtime-patches.sh
mkdir -p artifacts/windows
build_root="$(mktemp -d "$project_root/artifacts/windows/build-XXXXXXXX")"
publish_dir="$build_root/CastlevaniaChronicles-win-x64"
scripts/dotnet.sh publish CastlevaniaChronicles.csproj \
    -c Release -r win-x64 --self-contained true \
    -p:UseAppHost=true -p:PublishSingleFile=false -p:PublishTrimmed=false \
    -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false \
    -o "$publish_dir"
scripts/dotnet.sh publish launcher/WindowsLauncher/WindowsLauncher.csproj \
    -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishTrimmed=false -p:PublishReadyToRun=false \
    -p:DebugType=None -p:DebugSymbols=false \
    -o "$build_root/launcher"
cp "$build_root/launcher/Play.exe" "$publish_dir/Play.exe"
python3 scripts/package-windows.py "$publish_dir"
