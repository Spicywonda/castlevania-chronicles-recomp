#!/usr/bin/env python3
import hashlib
import json
from pathlib import Path
import plistlib
import shutil
import subprocess
import sys

publish = Path(sys.argv[1]).resolve(strict=True)
options = json.loads((publish / "CastlevaniaChronicles.runtimeconfig.json").read_text())["runtimeOptions"]
if not options.get("includedFrameworks") or options.get("frameworks") or options.get("framework"):
    raise SystemExit("A self-contained publish directory is required.")
for name in ("CastlevaniaChronicles", "libcoreclr.dylib", "libhostfxr.dylib"):
    output = subprocess.check_output(["lipo", "-archs", str(publish / name)], text=True)
    if "arm64" not in output.split():
        raise SystemExit(f"Missing arm64 architecture: {name}")
for path in publish.rglob("*"):
    if path.is_symlink() or path.suffix.lower() in {".cue", ".bin", ".iso", ".sav", ".mcr", ".chd"}:
        raise SystemExit(f"Unexpected package data: {path}")
package = publish.parent / "CastlevaniaChronicles-macos-arm64"
app = package / "Castlevania Chronicles.app"
contents = app / "Contents"
if package.exists():
    raise SystemExit("Package directory already exists; use a fresh publish directory.")
shutil.copytree(publish, contents / "MacOS")
(contents / "Info.plist").write_bytes(plistlib.dumps({
    "CFBundleIdentifier": "com.spicywonda.castlevania-chronicles-recomp",
    "CFBundleName": "Castlevania Chronicles",
    "CFBundleExecutable": "launch",
    "CFBundlePackageType": "APPL",
    "CFBundleShortVersionString": "0.1.0",
    "CFBundleVersion": "2",
    "NSHighResolutionCapable": True,
    "LSMinimumSystemVersion": "14.0",
}))
launcher = contents / "MacOS/launch"
launcher.write_text('''#!/bin/bash
set -euo pipefail
app_bin="$(cd "$(dirname "$0")" && pwd)"
if [[ $# -gt 0 && -f "$1" ]]; then
    cue_path="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
    shift
    set -- "$cue_path" "$@"
fi
data_dir="${CHRONICLES_DATA_DIR:-$HOME/Library/Application Support/Castlevania Chronicles Recomp}"
mkdir -p "$data_dir"
cd "$data_dir"
exec "$app_bin/CastlevaniaChronicles" "$@" > run.log 2>&1
''')
launcher.chmod(0o755)
root = Path(__file__).resolve().parent.parent
shutil.copyfile(root / "docs/macos.md", package / "MACOS.md")
shutil.copyfile(root / "vendor/RecompOne/LICENSE", package / "RECOMPONE-LICENSE.txt")
subprocess.run(["codesign", "--force", "--deep", "--sign", "-", str(app)], check=True)
subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
archive = package.with_suffix(".zip")
subprocess.run(["ditto", "-c", "-k", "--sequesterRsrc", "--keepParent", str(package), str(archive)], check=True)
digest = hashlib.sha256(archive.read_bytes()).hexdigest()
archive.with_suffix(".zip.sha256").write_text(f"{digest}  {archive.name}\n")
print(archive)
print(f"SHA-256: {digest}")
