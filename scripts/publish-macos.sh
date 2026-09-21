#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"
scripts/apply-runtime-patches.sh
mkdir -p artifacts/macos
build_root="$(mktemp -d "$project_root/artifacts/macos/build-XXXXXXXX")"
scripts/dotnet.sh publish CastlevaniaChronicles.csproj \
    -c Release -r osx-arm64 --self-contained true \
    -p:UseAppHost=true -p:PublishSingleFile=false -p:PublishTrimmed=false \
    -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false \
    -o "$build_root/publish"
python3 scripts/package-macos.py "$build_root/publish"
