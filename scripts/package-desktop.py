#!/usr/bin/env python3
"""Assemble a launcher and its private runtime, with no disc or user data."""
import hashlib, json, plistlib, shutil, struct, subprocess, sys, tarfile, zipfile
import xml.etree.ElementTree as ET
from pathlib import Path
build = Path(sys.argv[1]).resolve(strict=True)
rid = sys.argv[2]
root = Path(__file__).resolve().parents[1]
version = ET.parse(root/'Directory.Build.props').findtext('.//Version')
assert version
name = 'CastlevaniaChronicles-' + rid
package = build / name
package.mkdir()
mac = rid.startswith('osx')
win = rid.startswith('win')
launcher_name = 'Play.exe' if win else 'Play'
if mac:
    app = package/'Castlevania Chronicles.app'
    content = app/'Contents'
    target = content/'MacOS'
    target.mkdir(parents=True)
    (content/'Resources').mkdir()
    shutil.copy2(root/'launcher/DesktopLauncher/Assets/AppIcon.icns',content/'Resources/AppIcon.icns')
    (content/'Info.plist').write_bytes(plistlib.dumps({
        'CFBundleIdentifier':'com.spicywonda.castlevania-chronicles-recomp',
        'CFBundleName':'Castlevania Chronicles','CFBundleExecutable':'Play',
        'CFBundlePackageType':'APPL','CFBundleShortVersionString':version,
        'CFBundleIconFile':'AppIcon.icns','CFBundleVersion':version,'NSHighResolutionCapable':True,'LSMinimumSystemVersion':'14.0'}))
else: target = package
shutil.copy2(build/'launcher'/launcher_name,target/launcher_name)
(target/launcher_name).chmod(0o755)
if not win and not mac:
    shutil.copy2(root/'launcher/DesktopLauncher/Assets/AppIcon.png',target/'AppIcon.png')
    shutil.copy2(root/'launcher/DesktopLauncher/install-desktop.sh',target/'install-desktop.sh')
    (target/'install-desktop.sh').chmod(0o755)
runtime = target/'runtime'
shutil.copytree(build/'game',runtime)
shutil.copyfile(root/'vendor/RecompOne/RecompOne.Runtime/Host/Window/Assets/NotoSans-Regular.ttf',runtime/'LauncherFont.ttf')
shutil.copyfile(root/'vendor/RecompOne/LICENSE',runtime/'RECOMPONE-LICENSE.txt')
options=json.loads((runtime/'CastlevaniaChronicles.runtimeconfig.json').read_text())['runtimeOptions']
assert options.get('includedFrameworks') and not options.get('frameworks') and not options.get('framework'), 'Runtime must be self-contained'
def arch(path):
    with path.open('rb') as f:
        header=f.read(64)
        if win:
            assert header[:2]==b'MZ',path
            f.seek(struct.unpack_from('<I',header,60)[0]);pe=f.read(6)
            assert pe[:4]==b'PE\0\0' and struct.unpack_from('<H',pe,4)[0]==0x8664,path
        elif not mac:
            assert header[:6]==b'\x7fELF\x02\x01' and struct.unpack_from('<H',header,18)[0]==62,path
        else:
            assert 'arm64' in subprocess.check_output(['lipo','-archs',str(path)],text=True).split(),path
arch(target/launcher_name)
for filename in (['CastlevaniaChronicles.exe','coreclr.dll','hostfxr.dll','hostpolicy.dll','glfw3.dll','cimgui.dll','nfd.dll','SDL2.dll','soft_oal.dll'] if win else
                 ['CastlevaniaChronicles','libcoreclr.dylib','libhostfxr.dylib'] if mac else
                 ['CastlevaniaChronicles','libcoreclr.so','libhostfxr.so','libhostpolicy.so','libglfw.so.3','libcimgui.so','nfd.so','libSDL2-2.0.so','libopenal.so']): arch(runtime/filename)
for p in package.rglob('*'):
    assert not p.is_symlink() and p.suffix.lower() not in {'.cue','.bin','.iso','.chd','.sav','.mcr'},f'Unexpected user data: {p}'
(runtime/'README.txt').write_text(f'''CASTLEVANIA CHRONICLES RECOMP — {version}

Open Play, choose your USA SLUS-01384 CUE or CHD, then click Play.
Keep a CUE and its referenced BIN files together. You can also drop the CUE/CHD onto the launcher.
The selected disc is remembered. No .NET installation is required.
Starting a game prepares its code before opening the game window; allow a few seconds.
This preparation runs once per process and is not repeated by Hard Reset.
Keep the runtime folder alongside Play. Do not move individual files out of it.
Open logs in the launcher reveals run.log, validate.log and your settings.
During play, press F1, open Controls > Keyboard and gamepad...
Select Keyboard or Gamepad, click a binding and press the new key or button.
Changes are saved automatically. Reset defaults restores the selected input device.
During play, press F1 to show the menu bar and open Cheats.
Invincibility, infinite health, hearts and lives are optional and start disabled each session.
Cheats pause during transitions and stage-clear scoring.
Invincibility prevents contact damage and knockback without freezing player state.
Invincibility and infinite health do not prevent falls or instant death.

Movie frame waits avoid false read restarts that interrupt video audio.
Gallery images refresh correctly; interview selection no longer immediately skips it.
Play and direct game launch use the same save directory.
Use Import card (slot 1) for raw 128 KiB memory cards; existing cards are backed up.
Gameplay sound-bank transitions retain one refresh instead of three.
Audio: corrected manual SPU uploads and a shared 70 ms output reserve.
Windows, macOS and Linux include the same cheat, respawn and Hard Reset fixes.
The launcher and in-game menus start in English.
Audio queue/restart diagnostics are included in run.log.

This preview includes all eight stage overlay entries. Full-game compatibility
and the post-boss transition on Windows and Linux still require gameplay testing.
No game data is included.
''',encoding='utf-8')
# The manifest owns distributed files only; no saves, ROMs, or user settings.
install_root = app if mac else package
manifest_files = sorted(p for p in install_root.rglob('*') if p.is_file())
(install_root/('Contents/Resources/install-manifest.json' if mac else 'install-manifest.json')).write_text(json.dumps({
    'Schema': 1, 'Version': version, 'Rid': rid,
    'Files': [{'Path': p.relative_to(install_root).as_posix(), 'Size': p.stat().st_size,
               'Sha256': hashlib.sha256(p.read_bytes()).hexdigest(),
               'UnixMode': p.stat().st_mode & 0o777,
               'PlatformSigned': mac and p.read_bytes()[:4] in (bytes.fromhex('cffaedfe'),bytes.fromhex('cefaedfe'),bytes.fromhex('feedfacf'),bytes.fromhex('feedface'),bytes.fromhex('cafebabe'),bytes.fromhex('bebafeca'))} for p in manifest_files]
}))
if mac:
    subprocess.run(['codesign','--force','--deep','--sign','-',str(app)],check=True)
    subprocess.run(['codesign','--verify','--deep','--strict',str(app)],check=True)
archive=build/(f'CastlevaniaChronicles-{version}-{rid}'+('.zip' if win or mac else '.tar.gz'))
if mac: subprocess.run(['ditto','-c','-k','--sequesterRsrc','--keepParent',str(package),str(archive)],check=True)
elif win:
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in package.rglob('*'):
            if p.is_file(): z.write(p,Path(name)/p.relative_to(package))
else:
    with tarfile.open(archive,'w:gz') as t: t.add(package,arcname=name)
digest=hashlib.sha256(archive.read_bytes()).hexdigest()
archive.with_name(archive.name+'.sha256').write_text(f'{digest}  {archive.name}\n')
print(f'Package: {archive}\nSHA-256: {digest}')
