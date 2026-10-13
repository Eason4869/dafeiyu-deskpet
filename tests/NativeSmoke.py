"""Run the actual portable EXE; interact only with the exact process started here."""
from pathlib import Path
import ctypes
from ctypes import wintypes
import json
import shutil
import subprocess
import time

PROJECT=Path(__file__).resolve().parents[1]
WORK=PROJECT/'work/native-smoke'
WORK.mkdir(parents=True,exist_ok=True)
EXE=WORK/'DafeiyuDeskPet.exe'
SHIPPED=PROJECT.parent/'outputs/DafeiyuDeskPet/大肥鱼桌宠.exe'
shutil.copy2(SHIPPED,EXE)
shutil.copy2(Path(str(SHIPPED)+'.config'),Path(str(EXE)+'.config'))
user=ctypes.WinDLL('user32',use_last_error=True)
try:
    user.SetProcessDpiAwarenessContext.argtypes=[ctypes.c_void_p]
    user.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
except AttributeError:
    user.SetProcessDPIAware()
ENUM=ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
user.EnumWindows.argtypes=[ENUM,wintypes.LPARAM]
user.GetWindowThreadProcessId.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.DWORD)]
user.GetWindowRect.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.RECT)]
user.GetClientRect.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.RECT)]
user.ClientToScreen.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.POINT)]
user.IsWindowVisible.argtypes=[wintypes.HWND]
user.GetWindowTextW.argtypes=[wintypes.HWND,wintypes.LPWSTR,ctypes.c_int]
user.SendMessageW.argtypes=[wintypes.HWND,wintypes.UINT,wintypes.WPARAM,wintypes.LPARAM]
user.SendMessageW.restype=wintypes.LPARAM
user.PostMessageW.argtypes=[wintypes.HWND,wintypes.UINT,wintypes.WPARAM,wintypes.LPARAM]
user.SetCursorPos.argtypes=[ctypes.c_int,ctypes.c_int]
user.GetCursorPos.argtypes=[ctypes.POINTER(wintypes.POINT)]
user.mouse_event.argtypes=[wintypes.DWORD,wintypes.DWORD,wintypes.DWORD,wintypes.DWORD,ctypes.c_void_p]
checks=[]
skipped=[]
def require(value,label):
    if not value: raise AssertionError(label)
    checks.append(label)
    print('PASS '+label,flush=True)
def windows(pid):
    result=[]
    @ENUM
    def each(hwnd,_):
        process=wintypes.DWORD()
        user.GetWindowThreadProcessId(hwnd,ctypes.byref(process))
        if process.value==pid and user.IsWindowVisible(hwnd):
            text=ctypes.create_unicode_buffer(512); user.GetWindowTextW(hwnd,text,512)
            result.append((hwnd,text.value))
        return True
    user.EnumWindows(each,0)
    return result
def bounds(hwnd):
    r=wintypes.RECT(); user.GetWindowRect(hwnd,ctypes.byref(r)); return r
def launch():
    startup=subprocess.STARTUPINFO(); startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW; startup.wShowWindow=0
    return subprocess.Popen([str(EXE)],cwd=str(WORK),startupinfo=startup,creationflags=subprocess.CREATE_NO_WINDOW)

process=None
cursor=wintypes.POINT(); user.GetCursorPos(ctypes.byref(cursor))
try:
    process=launch()
    deadline=time.monotonic()+15
    pets=[]
    while time.monotonic()<deadline:
        pets=[w for w in windows(process.pid) if w[1].startswith('桌面伙伴 ·') and '设置' not in w[1]]
        if pets: break
        time.sleep(.1)
    require(bool(pets),'actual portable EXE starts a visible pet')
    hwnd=pets[0][0]; time.sleep(.3)
    r=bounds(hwnd)
    point=((r.top+2)<<16)|((r.left+2)&0xffff)
    require(user.SendMessageW(hwnd,0x84,0,point)==-1,'actual EXE has transparent native hit testing')
    x=r.left+(r.right-r.left)//2; y=r.top+int((r.bottom-r.top)*.68)
    placed=user.SetCursorPos(x,y); time.sleep(.1)
    actual=wintypes.POINT(); user.GetCursorPos(ctypes.byref(actual))
    hit=user.SendMessageW(hwnd,0x84,0,(y<<16)|(x&0xffff))
    print('Drag diagnostic: initial=%s cursor=%s set=%s hit=%s'%((r.left,r.top,r.right,r.bottom),(actual.x,actual.y),placed,hit),flush=True)
    if abs(actual.x-x)>2 or abs(actual.y-y)>2:
        skipped.append('real drag: external cursor movement before mouse down')
    else:
        user.mouse_event(0x0002,0,0,0,None); time.sleep(.08)
        placed2=user.SetCursorPos(x-70,y-35); time.sleep(.2)
        user.GetCursorPos(ctypes.byref(actual))
        user.mouse_event(0x0004,0,0,0,None); time.sleep(.2)
        after=bounds(hwnd)
        print('Drag diagnostic: after=%s cursor=%s set=%s'%((after.left,after.top,after.right,after.bottom),(actual.x,actual.y),placed2),flush=True)
        if abs(actual.x-(x-70))>2 or abs(actual.y-(y-35))>2:
            skipped.append('real drag: external cursor movement while mouse held')
        else:
            require(abs(after.left-(r.left-70))<=3 and abs(after.top-(r.top-35))<=3,'real left-button drag moves the pet')
    for reason in skipped: print('SKIP '+reason,flush=True)
    second=launch(); second.wait(timeout=8)
    require(second.returncode==0 and process.poll() is None,'second launch exits and preserves the first instance')
    deadline=time.monotonic()+8
    settings=[]
    while time.monotonic()<deadline:
        settings=[w for w in windows(process.pid) if '设置' in w[1]]
        if settings: break
        time.sleep(.1)
    require(bool(settings),'second launch opens the existing settings window')
    # A native window screenshot contains only this app, not the user's desktop.
    from PIL import ImageGrab
    time.sleep(1)
    client=wintypes.RECT(); origin=wintypes.POINT()
    user.GetClientRect(settings[0][0],ctypes.byref(client)); user.ClientToScreen(settings[0][0],ctypes.byref(origin))
    screenshot=ImageGrab.grab(bbox=(origin.x,origin.y,origin.x+client.right,origin.y+client.bottom))
    screenshot.save(WORK/'actual-settings.png')
    require(len(screenshot.getcolors(screenshot.width*screenshot.height) or [])>100,'actual settings content renders on the Windows desktop')
    user.PostMessageW(hwnd,0x10,0,0); process.wait(timeout=8)
    require(process.returncode==0 and not windows(process.pid),'native close releases all application windows')
    saved=json.loads((WORK/'data/settings.json').read_text(encoding='utf-8-sig'))
    require(saved.get('X') is not None and saved.get('Y') is not None,'native window position is saved in portable data')
    (WORK/'result.json').write_text(json.dumps({'passed':len(checks),'checks':checks,'skipped':skipped},ensure_ascii=False,indent=2),encoding='utf-8')
    print('Native smoke: %d passed, %d skipped'%(len(checks),len(skipped)),flush=True)
finally:
    user.mouse_event(0x0004,0,0,0,None)
    user.SetCursorPos(cursor.x,cursor.y)
    if process is not None and process.poll() is None:
        for hwnd,_ in windows(process.pid): user.PostMessageW(hwnd,0x10,0,0)
        try: process.wait(timeout=4)
        except subprocess.TimeoutExpired:
            process.terminate(); process.wait(timeout=4)
