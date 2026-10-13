"""Real update worker + ZIP/Inno, using isolated AppId, mutex and shortcut group.

The parent and restarted EXE are small owned probes, so a running user's pet is
never stopped or awakened. Only the uninstall registry constant is substituted
in the installed fixture worker; all update implementation bodies are unchanged.
"""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import uuid
import winreg

PROJECT = Path(__file__).resolve().parents[1]
VERSION = re.search(r'DisplayVersion="([^"]+)"', (PROJECT / 'src/ReleaseInfo.cs').read_text(encoding='utf-8-sig')).group(1)
ROOT = PROJECT / 'work' / ('update-lifecycle-' + uuid.uuid4().hex)
ROOT.mkdir(parents=True)
CSC = Path(os.environ['WINDIR']) / 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
FRAMEWORK = CSC.parent
INNO = PROJECT / 'work/tools/inno/ISCC.exe'
APP_ID = '3B63C162-076E-4B2F-BDCC-2572664875A9'
passed = 0

def check(value, text):
    global passed
    if not value:
        raise AssertionError(text)
    passed += 1
    print('PASS ' + text, flush=True)

def start(command, cwd):
    info = subprocess.STARTUPINFO()
    info.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    info.wShowWindow = 0
    return subprocess.Popen([str(x) for x in command], cwd=str(cwd), startupinfo=info, creationflags=subprocess.CREATE_NO_WINDOW)

def run(command, cwd=ROOT, timeout=90):
    with start(command, cwd) as process:
        code = process.wait(timeout=timeout)
    if code:
        raise AssertionError('fixture command failed with exit code ' + str(code))

def wait_for(predicate, seconds=20):
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        if predicate():
            return
        time.sleep(.05)
    raise AssertionError('timed out waiting for update lifecycle')

def ticks(pid):
    # PowerShell reports precisely the same DateTime ticks as Process.StartTime.
    result = subprocess.run(['powershell.exe', '-NoProfile', '-Command',
        '[Diagnostics.Process]::GetProcessById([int]$env:DESKPET_TEST_PARENT_ID).StartTime.ToUniversalTime().Ticks'],
        env=dict(os.environ, DESKPET_TEST_PARENT_ID=str(pid)), capture_output=True, check=True)
    return int(result.stdout.decode('ascii').strip())

def compile_worker(destination, fixture_id=None):
    source = (PROJECT / 'src/UpdateInstall.cs').read_text(encoding='utf-8-sig')
    if fixture_id:
        original = source
        source = source.replace(APP_ID, fixture_id)
        check(source.replace(fixture_id, APP_ID) == original, 'installed fixture substitutes only its own registry AppId')
    local = destination.parent / 'UpdateInstall.fixture.cs'
    local.write_text(source, encoding='utf-8')
    refs = ['System.dll', 'System.Core.dll', 'System.Runtime.Serialization.dll', 'System.IO.Compression.dll']
    run([CSC, '/nologo', '/codepage:65001', '/utf8output', '/target:winexe', '/define:UPDATE_WORKER',
        '/main:Dafeiyu.UpdateWorkerProgram', '/out:' + str(destination),
        *['/reference:' + str(FRAMEWORK / x) for x in refs], local, PROJECT / 'src/UpdateWorker.cs', PROJECT / 'src/ReleaseInfo.cs'])
    shutil.copy2(PROJECT / 'app.config', Path(str(destination) + '.config'))

def compile_parent(destination, mutex, old):
    source = '''using System;using System.IO;using System.Threading;
        [assembly:System.Reflection.AssemblyVersion("%s")]
        public static class Probe{[STAThread]public static void Main(string[] args){
        string data=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");Directory.CreateDirectory(data);
        if(args.Length>0 && args[0]=="--update-result"){File.WriteAllText(Path.Combine(data,"restarted"),"%s");return;}
        using(var mutex=new Mutex(false,@"%s")){File.WriteAllText(Path.Combine(data,"parent-started"),"ready");
        while(!File.Exists(Path.Combine(data,"exit-parent")))Thread.Sleep(40);}}}''' % (
            '0.3.1.0' if old else VERSION + '.0', 'old' if old else VERSION, mutex)
    script = destination.parent / ('ParentOld.cs' if old else 'ParentNew.cs')
    script.write_text(source, encoding='utf-8')
    run([CSC, '/nologo', '/codepage:65001', '/utf8output', '/target:winexe', '/out:' + str(destination), script])
    shutil.copy2(PROJECT / 'app.config', Path(str(destination) + '.config'))

def setup(runtime, stage, version, fixture_id, mutex, group):
    installer = stage / 'installer'
    installer.mkdir(exist_ok=True)
    text = (PROJECT / 'installer/DeskPet.iss').read_text(encoding='utf-8-sig')
    text = text.replace(APP_ID, fixture_id).replace('AppMutex=Local\\Dafeiyu.DeskPet.Installation', 'AppMutex=' + mutex)
    text = text.replace('DefaultGroupName=桌面伙伴', 'DefaultGroupName=' + group).replace('AppName=桌面伙伴', 'AppName=DeskPet Update Fixture')
    # The fixture uses an absolute icon reference and its own start-menu group.
    text = text.replace('SetupIconFile=..\\assets\\pet.ico', 'SetupIconFile=' + str(PROJECT / 'assets/pet.ico'))
    script = installer / 'DeskPet.iss'
    script.write_text(text, encoding='utf-8-sig')
    shutil.copy2(PROJECT / 'installer/ChineseSimplified.isl', installer / 'ChineseSimplified.isl')
    run([INNO, '/Qp', '/DRuntimeDir=' + str(runtime), '/DOutputDir=' + str(stage), '/DProductVersion=' + version,
        '/DAssemblyVersion=' + version + '.0', '/DRepositoryUrl=https://github.com/Eason4869/deskpet', script])
    return stage / ('DeskPet-Setup-' + version + '.exe')

def scenario(mode, abandon=False, blocked_receipts=False, invalid_package=False):
    stage = ROOT / ('abandoned' if abandon else mode + ('-blocked' if blocked_receipts else '') + ('-invalid' if invalid_package else ''))
    stage.mkdir()
    directory = stage / '中文 app with spaces'
    runtime = stage / 'runtime'
    runtime.mkdir()
    fixture_id = str(uuid.uuid4()).upper()
    mutex = 'Local\\DeskPet.UpdateFixture.' + uuid.uuid4().hex
    group = 'DeskPet Update Fixture ' + uuid.uuid4().hex
    exe = runtime / '桌面伙伴.exe'
    compile_parent(exe, mutex, True)
    worker = runtime / 'DeskPet.Update.exe'
    compile_worker(worker, fixture_id if mode == 'installed' else None)
    for name in ['README.md', 'CHANGELOG.md']:
        shutil.copy2(PROJECT / name, runtime / name)
    for name in ['docs', 'templates', 'previews']:
        shutil.copytree(PROJECT / name, runtime / name, ignore=shutil.ignore_patterns('superpowers'))
    parent = updater = None
    registered = False
    try:
        if mode == 'installed':
            first = setup(runtime, stage, '0.3.1', fixture_id, mutex, group)
            run([first, '/VERYSILENT', '/SUPPRESSMSGBOXES', '/SP-', '/NORESTART', '/DIR=' + str(directory), '/TASKS='])
            registered = True
        else:
            shutil.copytree(runtime, directory, ignore=shutil.ignore_patterns('*.cs'))
        data = directory / 'data'
        data.mkdir(exist_ok=True)
        (data / 'settings.json').write_text('private configuration sentinel', encoding='utf-8')
        (data / 'skins' / 'custom').mkdir(parents=True)
        (data / 'skins/custom/pet.json').write_text('imported pet sentinel', encoding='utf-8')
        old_hash = hashlib.sha256((directory / '桌面伙伴.exe').read_bytes()).hexdigest()
        parent = start([directory / '桌面伙伴.exe'], directory)
        wait_for(lambda: (data / 'parent-started').exists())
        work = data / 'updates' / uuid.uuid4().hex
        work.mkdir(parents=True)
        if blocked_receipts:
            (data / 'update-result.json').mkdir()
            (work / 'result.json').mkdir()
        compile_parent(exe, mutex, False)
        (runtime / 'README.md').write_text('updated readme', encoding='utf-8')
        if mode == 'installed':
            built = setup(runtime, stage, VERSION, fixture_id, mutex, group)
            package = work / built.name
            shutil.copy2(built, package)
        else:
            package = work / ('DeskPet-Windows-' + VERSION + '.zip')
            with ZipFile(package, 'w', ZIP_DEFLATED) as bundle:
                for file in runtime.rglob('*'):
                    if file.is_file() and file.suffix != '.cs':
                        name = 'DafeiyuDeskPet/' + file.relative_to(runtime).as_posix()
                        if invalid_package and file.name == '桌面伙伴.exe':
                            bundle.writestr(name, b'invalid executable fixture')
                        else:
                            bundle.write(file, name)
        job = {'InstallationDirectory': str(directory), 'WorkDirectory': str(work), 'PackagePath': str(package),
            'Mode': mode, 'InstalledVersion': '0.3.1', 'TargetVersion': VERSION,
            'Size': package.stat().st_size, 'Digest': hashlib.sha256(package.read_bytes()).hexdigest(),
            'AssetUrl': 'https://github.com/Eason4869/deskpet/releases/download/v' + VERSION + '/' + package.name,
            'ParentPid': parent.pid, 'ParentStartedUtcTicks': ticks(parent.pid)}
        path = work / 'job.json'
        path.write_text(json.dumps(job), encoding='utf-8')
        shutil.copy2(directory / 'DeskPet.Update.exe', work / 'DeskPet.Update.exe')
        shutil.copy2(directory / 'DeskPet.Update.exe.config', work / 'DeskPet.Update.exe.config')
        updater = start([work / 'DeskPet.Update.exe', path], work)
        wait_for(lambda: (work / 'ready').exists() or updater.poll() is not None)
        check((work / 'ready').exists() and updater.poll() is None, mode + ': real helper validates and acknowledges handoff')
        time.sleep(.2)
        check(parent.poll() is None and hashlib.sha256((directory / '桌面伙伴.exe').read_bytes()).hexdigest() == old_hash,
              mode + ': helper waits for the old process without replacing a running EXE')
        if abandon:
            (work / 'abort').write_text('aborted handoff', encoding='ascii')
            wait_for(lambda: updater.poll() is not None, 3)
            check(updater.returncode != 0 and parent.poll() is None and
                  hashlib.sha256((directory / '桌面伙伴.exe').read_bytes()).hexdigest() == old_hash,
                  'abandoned handoff cannot apply later when the app normally exits')
            return
        (data / 'exit-parent').write_text('exit', encoding='ascii')
        parent.wait(timeout=10)
        code = updater.wait(timeout=90)
        check((code != 0) if invalid_package else (code == 0), mode + ': real update helper reports the correct result')
        wait_for(lambda: (data / 'restarted').exists())
        check((data / 'restarted').read_text() == ('old' if invalid_package else VERSION),
              mode + (': restarts the old executable after rejecting a corrupt package' if invalid_package else ': automatically restarts the updated executable'))
        check((data / 'settings.json').read_text() == 'private configuration sentinel' and
              (data / 'skins/custom/pet.json').read_text() == 'imported pet sentinel', mode + ': preserves settings and imported skins')
        if blocked_receipts:
            check((data / 'update-result.json').is_dir() and (work / 'result.json').is_dir(), 'failed receipt writes cannot prevent restart')
        else:
            result = json.loads((data / 'update-result.json').read_text(encoding='utf-8'))
            check(result['Success'] and result['Version'] == VERSION, mode + ': restart receives the successful update receipt')
    finally:
        if parent is not None and parent.poll() is None:
            (directory / 'data/exit-parent').write_text('exit', encoding='ascii')
            parent.wait(timeout=10)
        if updater is not None and updater.poll() is None:
            updater.wait(timeout=130)
        if registered:
            run([directory / 'unins000.exe', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'])
            check((directory / 'data/settings.json').exists(), 'isolated uninstall preserves user-data fixture')

if '--receipts-only' in sys.argv:
    scenario('portable', blocked_receipts=True)
    scenario('portable', blocked_receipts=True, invalid_package=True)
else:
    scenario('portable')
    scenario('portable', abandon=True)
    scenario('portable', blocked_receipts=True)
    scenario('portable', blocked_receipts=True, invalid_package=True)
    if not INNO.is_file():
        raise AssertionError('real Inno compiler is required for the installed lifecycle fixture')
    scenario('installed')
(ROOT / 'result.json').write_text(json.dumps({'passed': passed, 'version': VERSION}), encoding='utf-8')
print('Update lifecycle smoke: %d passed' % passed, flush=True)
