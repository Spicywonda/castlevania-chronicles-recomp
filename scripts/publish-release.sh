#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python3 - "$@" <<'PY'
import hashlib,json,re,subprocess,sys
from pathlib import Path
import xml.etree.ElementTree as ET
if len(sys.argv)!=3: raise SystemExit('Usage: scripts/publish-release.sh PACKAGE_DIRECTORY RELEASE_NOTES.md')
def run(*args): return subprocess.check_output(args,text=True).strip()
repo='Spicywonda/castlevania-chronicles-recomp'
version=ET.parse('Directory.Build.props').findtext('.//Version')
assert re.fullmatch(r'\d+\.\d+\.\d+',version or ''),'Invalid release version'
assert run('gh','api','user','--jq','.login')=='Spicywonda','Wrong GitHub publisher'
subprocess.run(['git','diff','--quiet','HEAD','--ignore-submodules=all'],check=True)
assert run('git','rev-parse','HEAD')==run('git','rev-list','-n','1','v'+version),'Tag must point at the reviewed commit'
folder=Path(sys.argv[1]).resolve();notes=Path(sys.argv[2]).resolve();assert notes.is_file()
assets=[folder/f'CastlevaniaChronicles-{version}-{rid}{ext}' for rid,ext in [('win-x64','.zip'),('osx-arm64','.zip'),('linux-x64','.tar.gz')]]
for asset in assets: assert asset.is_file(),asset
checksums=folder/f'SHA256SUMS-{version}.txt'
checksums.write_text(''.join(f'{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}\n' for p in assets))
assets.append(checksums)
# Drafts are invisible to the launcher's stable-release lookup until all assets verify.
subprocess.run(['gh','release','create','v'+version,*map(str,assets),'--repo',repo,'--verify-tag','--draft','--title',f'Castlevania Chronicles Recomp {version}','--notes-file',str(notes)],check=True)
release=json.loads(run('gh','api',f'repos/{repo}/releases/tags/v{version}'))
assert release['draft'] and release['author']['login']=='Spicywonda'
remote={a['name']:a for a in release['assets']}
assert len(remote)==len(assets),'Unexpected release assets'
for p in assets:
 a=remote[p.name]
 assert a['state']=='uploaded' and a['size']==p.stat().st_size and a['digest']=='sha256:'+hashlib.sha256(p.read_bytes()).hexdigest(),p
subprocess.run(['gh','release','edit','v'+version,'--repo',repo,'--draft=false','--prerelease=false','--latest'],check=True)
print(f'https://github.com/{repo}/releases/tag/v{version}')
PY
