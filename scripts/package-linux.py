#!/usr/bin/env python3
import hashlib
import json
from pathlib import Path
import shutil
import struct
import sys
import tarfile


def require_x64_elf(path):
    with path.open("rb") as stream:
        header = stream.read(20)
    if len(header) != 20 or header[:6] != b"\x7fELF\x02\x01" or struct.unpack_from("<H", header, 18)[0] != 62:
        raise ValueError(f"Not an x64 Linux ELF file: {path}")


def main():
    if len(sys.argv) != 2:
        raise SystemExit("Usage: package-linux.py <self-contained-linux-x64-publish-directory>")
    publish = Path(sys.argv[1]).resolve(strict=True)
    for name in ("CastlevaniaChronicles", "libcoreclr.so", "libhostfxr.so", "libhostpolicy.so",
                 "libSDL2-2.0.so", "libglfw.so.3", "libcimgui.so", "nfd.so", "libopenal.so"):
        require_x64_elf(publish / name)
    options = json.loads((publish / "CastlevaniaChronicles.runtimeconfig.json").read_text())["runtimeOptions"]
    if options.get("framework") or options.get("frameworks") or not options.get("includedFrameworks"):
        raise ValueError("The publish directory is not self-contained.")
    for path in publish.rglob("*"):
        if path.is_symlink() or path.suffix.lower() in {".cue", ".bin", ".iso", ".chd", ".mcr", ".sav"}:
            raise ValueError(f"Unexpected package data: {path}")
    launcher = publish / "Play.sh"
    launcher.write_text('''#!/usr/bin/env bash
set -euo pipefail
app_dir="$(cd "$(dirname "$0")" && pwd)"
cue_path="${1:-$app_dir/disc/chronicles.cue}"
if [[ $# -gt 1 || ! -f "$cue_path" ]]; then
    printf '%s\\n' 'Usage: ./Play.sh /path/to/USA-disc.cue' >&2
    exit 2
fi
cue_path="$(cd "$(dirname "$cue_path")" && pwd)/$(basename "$cue_path")"
data_dir="${CHRONICLES_DATA_DIR:-${XDG_DATA_HOME:-$HOME/.local/share}/castlevania-chronicles-recomp}"
mkdir -p "$data_dir"
cd "$data_dir"
exec "$app_dir/CastlevaniaChronicles" "$cue_path" > run.log 2>&1
''')
    launcher.chmod(0o755)
    (publish / "CastlevaniaChronicles").chmod(0o755)
    root = Path(__file__).resolve().parent.parent
    shutil.copyfile(root / "docs/linux.md", publish / "LINUX.md")
    shutil.copyfile(root / "vendor/RecompOne/LICENSE", publish / "RECOMPONE-LICENSE.txt")
    files = sorted(p for p in publish.rglob("*") if p.is_file() and p.name != "SHA256SUMS.txt")
    (publish / "SHA256SUMS.txt").write_text("".join(
        f"{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.relative_to(publish).as_posix()}\n" for p in files))
    archive = publish.with_suffix(".tar.gz")
    with tarfile.open(archive, "w:gz", format=tarfile.PAX_FORMAT) as output:
        output.add(publish, arcname=publish.name)
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    archive.with_suffix(".gz.sha256").write_text(f"{digest}  {archive.name}\n")
    print(f"Package: {archive}")
    print(f"SHA-256: {digest}")
    print("Linux execution remains unvalidated; the package was cross-published on macOS.")


if __name__ == "__main__":
    main()
