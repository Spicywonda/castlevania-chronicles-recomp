#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"
if [[ ! -f generated/Entry.cs ]]; then
    printf '%s\n' 'Generate the USA SLUS-01384 game first with ./scripts/recompile.sh.' >&2
    exit 1
fi
scripts/apply-runtime-patches.sh
mkdir -p artifacts/linux
build_root="$(mktemp -d "$project_root/artifacts/linux/build-XXXXXXXX")"
scripts/dotnet.sh publish CastlevaniaChronicles.csproj \
    -c Release -r linux-x64 --self-contained true \
    -p:UseAppHost=true -p:PublishSingleFile=false -p:PublishTrimmed=false \
    -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false \
    -o "$build_root/CastlevaniaChronicles-linux-x64"
python3 scripts/package-linux.py "$build_root/CastlevaniaChronicles-linux-x64"
