#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
cue_path="${1:-$project_root/disc/chronicles.cue}"
cue_path="$(cd "$(dirname "$cue_path")" && pwd)/$(basename "$cue_path")"
mkdir -p "$project_root/artifacts/runtime"
cd "$project_root/artifacts/runtime"
exec "$project_root/scripts/dotnet.sh" "$project_root/bin/Release/net10.0/CastlevaniaChronicles.dll" "$cue_path"
