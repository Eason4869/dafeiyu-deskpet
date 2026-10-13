"""Build release archives from a fresh source tree with no local work directory."""
from pathlib import Path
import argparse
import hashlib
import os
import re
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

PROJECT = Path(__file__).resolve().parents[1]
version = re.search(r'DisplayVersion="([^"]+)"', (PROJECT / 'src/ReleaseInfo.cs').read_text(encoding='utf-8-sig')).group(1)
arguments = argparse.ArgumentParser()
arguments.add_argument('--compiler', help='Optional Inno ISCC.exe for full fresh installer verification')
options = arguments.parse_args()
root = PROJECT / 'work' / ('source-smoke-' + uuid.uuid4().hex)
checkout = root / 'DafeiyuDeskPet'
shutil.copytree(PROJECT, checkout, ignore=shutil.ignore_patterns(
    '.git', 'work', 'bin', 'obj', 'data', '__pycache__', '.superpowers', 'superpowers'))
runtime = root / 'outputs' / 'DafeiyuDeskPet'
result = subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass',
                         '-File', str(checkout / 'Build.ps1'), '-Target', 'App'],
                        cwd=checkout, capture_output=True, timeout=60)
assert result.returncode == 0, result.stdout.decode('utf-8', errors='replace') + result.stderr.decode('utf-8', errors='replace')
print('PASS application compiles from the fresh public source tree')
# A reused portable folder must not leak previous executables or private loose files.
(runtime / '大肥鱼桌宠.exe').write_bytes(b'legacy binary, do not ship')
(runtime / 'private-key.txt').write_text('private sentinel', encoding='utf-8')
(runtime / 'data').mkdir()
(runtime / 'data/settings.json').write_text('private settings sentinel', encoding='utf-8')
result = subprocess.run([sys.executable, str(checkout / 'Package.py')], cwd=checkout,
                        env=dict(os.environ,PYTHONIOENCODING='utf-8'),
                        capture_output=True, text=True, encoding='utf-8', timeout=60)
if result.returncode:
    print(result.stdout + result.stderr)
    raise AssertionError('fresh source packaging needs no local test artifacts')
print('PASS fresh source packaging needs no local test artifacts')
with ZipFile(root / 'outputs' / ('DeskPet-Windows-' + version + '.zip')) as bundle:
    assert not any(name.endswith('大肥鱼桌宠.exe') or 'private-key' in name or '/data/' in name for name in bundle.namelist())
    for name in ['桌面伙伴.exe', '桌面伙伴.exe.config', 'DeskPet.Update.exe', 'DeskPet.Update.exe.config']:
        assert bundle.read('DafeiyuDeskPet/' + name) == (runtime / name).read_bytes()
print('PASS portable archive excludes legacy binaries and private loose files')
print('PASS portable archive includes the exact application and independent update helper')
result = subprocess.run(['powershell.exe', '-NoProfile', '-Command',
    '[Reflection.AssemblyName]::GetAssemblyName($env:DESKPET_TEST_HELPER).Version.ToString()'],
    env=dict(os.environ, DESKPET_TEST_HELPER=str(runtime / 'DeskPet.Update.exe')),
    capture_output=True, check=True)
assert result.stdout.decode('ascii').strip() == version + '.0'
assert (runtime / 'DeskPet.Update.exe').stat().st_size < 1024 * 1024, 'helper should not embed pet sprite assets'
print('PASS update helper has the declared version and excludes sprite resources')
readme = (runtime / 'README.md').read_text(encoding='utf-8-sig')
images = re.findall(r'<img\s+src="([^"]+)"|!\[[^\]]*\]\(([^)]+)\)', readme)
for alternatives in images:
    target = next(value for value in alternatives if value)
    assert (runtime / target).is_file(), 'installed README image missing: ' + target
    assert (checkout / target).is_file(), 'repository README image missing: ' + target
print('PASS README images resolve in the repository and installed runtime')
with ZipFile(root / 'outputs' / ('DeskPet-Source-' + version + '.zip')) as bundle:
    assert not any(part in {'work', 'data', '.git', '.superpowers', 'superpowers'}
                   for name in bundle.namelist() for part in Path(name).parts)
print('PASS source archive excludes private and local test files')
with ZipFile(root / 'outputs' / ('DeskPet-Source-' + version + '.zip')) as bundle:
    for name in bundle.namelist():
        assert bundle.read(name) == (root / name).read_bytes(), 'source archive differs: ' + name
print('PASS source archive contains the exact public source files')
if options.compiler:
    result = subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass',
                             '-File', str(checkout / 'BuildInstaller.ps1'),
                             '-Compiler', str(Path(options.compiler).resolve())],
                            cwd=checkout, capture_output=True, timeout=60)
    assert result.returncode == 0, result.stdout.decode('utf-8', errors='replace') + result.stderr.decode('utf-8', errors='replace')
    print('PASS installer builds from the fresh public source tree')
    manifest = (root / 'outputs' / 'SHA256.txt').read_text(encoding='utf-8-sig').splitlines()
    version = re.search(r'DisplayVersion="([^"]+)"', (checkout / 'src/ReleaseInfo.cs').read_text(encoding='utf-8-sig')).group(1)
    installer_name = 'DeskPet-Setup-' + version + '.exe'
    assert len([line for line in manifest if line.endswith('  ' + installer_name)]) == 1, 'installer must appear exactly once in the final manifest'
    for line in manifest:
        digest, name = line.split('  ', 1)
        assert digest == hashlib.sha256((root / 'outputs' / name).read_bytes()).hexdigest(), 'stale digest: ' + name
    print('PASS installer build leaves a complete matching SHA256 manifest')
print('Packaging smoke:', 10 if options.compiler else 8, 'passed')
