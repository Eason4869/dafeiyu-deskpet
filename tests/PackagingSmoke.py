"""Build release archives from a fresh source tree with no local work directory."""
from pathlib import Path
import re
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

PROJECT = Path(__file__).resolve().parents[1]
root = PROJECT / 'work' / ('source-smoke-' + uuid.uuid4().hex)
checkout = root / 'DafeiyuDeskPet'
shutil.copytree(PROJECT, checkout, ignore=shutil.ignore_patterns(
    '.git', 'work', 'bin', 'obj', 'data', '__pycache__', '.superpowers', 'superpowers'))
runtime = root / 'outputs' / 'DafeiyuDeskPet'
runtime.mkdir(parents=True)
for name in ['大肥鱼桌宠.exe', '大肥鱼桌宠.exe.config']:
    shutil.copy2(PROJECT.parent / 'outputs' / 'DafeiyuDeskPet' / name, runtime / name)
result = subprocess.run([sys.executable, str(checkout / 'Package.py')], cwd=checkout,
                        capture_output=True, text=True, encoding='utf-8', timeout=60)
if result.returncode:
    print(result.stdout + result.stderr)
    raise AssertionError('fresh source packaging needs no local test artifacts')
print('PASS fresh source packaging needs no local test artifacts')
readme = (runtime / 'README.md').read_text(encoding='utf-8-sig')
images = re.findall(r'<img\s+src="([^"]+)"|!\[[^\]]*\]\(([^)]+)\)', readme)
for alternatives in images:
    target = next(value for value in alternatives if value)
    assert (runtime / target).is_file(), 'installed README image missing: ' + target
    assert (checkout / target).is_file(), 'repository README image missing: ' + target
print('PASS README images resolve in the repository and installed runtime')
with ZipFile(root / 'outputs' / '大肥鱼桌宠-源码.zip') as bundle:
    assert not any(part in {'work', 'data', '.git', '.superpowers', 'superpowers'}
                   for name in bundle.namelist() for part in Path(name).parts)
print('PASS source archive excludes private and local test files')
print('Packaging smoke: 3 passed')
