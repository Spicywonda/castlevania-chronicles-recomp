#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"
rid="${1:?Usage: publish-desktop.sh win-x64|osx-arm64|linux-x64}"
case "$rid" in win-x64|osx-arm64|linux-x64) ;; *) exit 2 ;; esac
scripts/apply-runtime-patches.sh
mkdir -p artifacts/desktop
build_root="$(mktemp -d "$project_root/artifacts/desktop/build-XXXXXXXX")"
scripts/dotnet.sh publish CastlevaniaChronicles.csproj -c Release -r "$rid" --self-contained true \
    -p:UseAppHost=true -p:PublishSingleFile=false -p:PublishTrimmed=false \
    -p:NuGetAudit=false -p:WarningLevel=0 -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false -o "$build_root/game"
scripts/dotnet.sh publish launcher/DesktopLauncher/DesktopLauncher.csproj -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false \
    -p:NuGetAudit=false -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false -o "$build_root/launcher"
python3 scripts/package-desktop.py "$build_root" "$rid"
