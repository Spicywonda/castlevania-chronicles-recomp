#!/usr/bin/env python3
import hashlib
import json
from pathlib import Path
import shutil
import struct
import sys
import zipfile


def require_x64_pe(path):
    with path.open("rb") as stream:
        if stream.read(2) != b"MZ":
            raise ValueError(f"Not a Windows PE file: {path}")
        stream.seek(0x3C)
        offset = struct.unpack("<I", stream.read(4))[0]
        stream.seek(offset)
        if stream.read(4) != b"PE\0\0" or struct.unpack("<H", stream.read(2))[0] != 0x8664:
            raise ValueError(f"Not an x64 Windows PE file: {path}")


def main():
    if len(sys.argv) != 2:
        raise SystemExit("Usage: package-windows.py <self-contained-win-x64-publish-directory>")
    publish_dir = Path(sys.argv[1]).resolve(strict=True)
    required_native = (
        "CastlevaniaChronicles.exe", "coreclr.dll", "hostfxr.dll", "hostpolicy.dll",
        "SDL2.dll", "glfw3.dll", "cimgui.dll", "nfd.dll", "soft_oal.dll",
    )
    for name in required_native:
        require_x64_pe(publish_dir / name)
    for name in ("CastlevaniaChronicles.dll", "RecompOne.Runtime.dll", "System.Private.CoreLib.dll"):
        if not (publish_dir / name).is_file():
            raise ValueError(f"Missing published dependency: {name}")
    config = json.loads((publish_dir / "CastlevaniaChronicles.runtimeconfig.json").read_text())
    options = config["runtimeOptions"]
    if options.get("framework") or options.get("frameworks") or not options.get("includedFrameworks"):
        raise ValueError("The publish directory is not self-contained.")
    for path in publish_dir.rglob("*"):
        if path.is_symlink() or path.suffix.lower() in {".cue", ".bin", ".iso", ".chd", ".mcr", ".sav"}:
            raise ValueError(f"Unexpected disc, save, or symbolic link in publish directory: {path}")
    launcher = r'''@echo off
setlocal
set "cue_path=%~1"
if not defined cue_path set "cue_path=%~dp0disc\chronicles.cue"
for %%I in ("%cue_path%") do set "cue_path=%%~fI"
if not exist "%cue_path%" (
    echo Drag your USA SLUS-01384 CUE file onto this launcher.
    echo Or put chronicles.cue and its referenced BIN files in the disc folder.
    pause
    exit /b 2
)
if not exist "%~dp0runtime" mkdir "%~dp0runtime"
pushd "%~dp0runtime" || exit /b 1
"%~dp0CastlevaniaChronicles.exe" "%cue_path%" > run.log 2>&1
set "game_exit=%errorlevel%"
if not "%game_exit%"=="0" (
    type run.log
    echo Game exited with code %game_exit%. See runtime\run.log.
    pause
)
popd
exit /b %game_exit%
'''
    (publish_dir / "Play.cmd").write_bytes(launcher.replace("\n", "\r\n").encode("utf-8"))
    project_root = Path(__file__).resolve().parent.parent
    shutil.copyfile(project_root / "docs/windows.md", publish_dir / "WINDOWS.md")
    shutil.copyfile(project_root / "vendor/RecompOne/LICENSE", publish_dir / "RECOMPONE-LICENSE.txt")
    files = sorted(path for path in publish_dir.rglob("*") if path.is_file() and path.name != "SHA256SUMS.txt")
    manifest = "".join(f"{hashlib.sha256(path.read_bytes()).hexdigest()}  {path.relative_to(publish_dir).as_posix()}\n" for path in files)
    (publish_dir / "SHA256SUMS.txt").write_text(manifest, encoding="utf-8")
    archive_path = publish_dir.with_suffix(".zip")
    with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(publish_dir.rglob("*")):
            if path.is_file():
                archive.write(path, Path(publish_dir.name) / path.relative_to(publish_dir))
    archive_hash = hashlib.sha256(archive_path.read_bytes()).hexdigest()
    archive_path.with_suffix(".zip.sha256").write_text(f"{archive_hash}  {archive_path.name}\n", encoding="utf-8")
    print(f"Verified Windows x64 PE files and self-contained runtime: {publish_dir}")
    print(f"Package: {archive_path}")
    print(f"SHA-256: {archive_hash}")
    print("Windows execution remains unvalidated; this package was cross-published on macOS.")


if __name__ == "__main__":
    main()
