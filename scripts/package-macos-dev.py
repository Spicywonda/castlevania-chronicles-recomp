#!/usr/bin/env python3
from pathlib import Path
import plistlib
import shutil

root = Path(__file__).resolve().parent.parent
output = root / "bin/Release/net10.0"
if not (output / "CastlevaniaChronicles").is_file():
    raise SystemExit("Build CastlevaniaChronicles.csproj in Release first.")
contents = root / "artifacts/Castlevania Chronicles Dev.app/Contents"
shutil.copytree(output, contents / "MacOS", dirs_exist_ok=True)
(contents / "Info.plist").write_bytes(plistlib.dumps({
    "CFBundleIdentifier": "local.recomps.castlevania-chronicles-dev",
    "CFBundleName": "Castlevania Chronicles Dev",
    "CFBundleExecutable": "launch",
    "CFBundlePackageType": "APPL",
    "CFBundleVersion": "0.1",
    "NSHighResolutionCapable": True,
}))
launcher = contents / "MacOS/launch"
launcher.write_text('''#!/bin/bash
set -euo pipefail
app_bin="$(cd "$(dirname "$0")" && pwd)"
project_root="$(cd "$app_bin/../../../.." && pwd)"
export DOTNET_ROOT="$project_root/.tools/dotnet"
mkdir -p "$project_root/artifacts/runtime"
cd "$project_root/artifacts/runtime"
exec "$app_bin/CastlevaniaChronicles" "$project_root/disc/chronicles.cue" > "$project_root/artifacts/run-app.log" 2>&1
''')
launcher.chmod(0o755)
print(contents.parent)
