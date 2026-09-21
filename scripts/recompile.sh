#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"
scripts/apply-runtime-patches.sh
scripts/dotnet.sh build vendor/RecompOne/RecompOne.Recompiler -c Release
scripts/dotnet.sh vendor/RecompOne/RecompOne.Recompiler/bin/Release/net10.0/recompone.dll config/CastlevaniaChronicles.json
scripts/dotnet.sh build CastlevaniaChronicles.csproj -c Release
