from pathlib import Path
import hashlib
import json
import shutil
import re
from zipfile import ZipFile, ZIP_DEFLATED
from PIL import Image

PROJECT=Path(__file__).resolve().parent
OUTPUT=PROJECT.parent/'outputs'
APP=OUTPUT/'DafeiyuDeskPet'
PREVIEWS=APP/'previews'
PREVIEWS.mkdir(parents=True,exist_ok=True)
(APP/'docs').mkdir(exist_ok=True)
shutil.copytree(PROJECT/'templates',APP/'templates',dirs_exist_ok=True)
for name in ['README.md']:
    shutil.copy2(PROJECT/name,APP/name)
for name in ['skin-format.md','dialogue-setup.md','verification.md','sources.md']:
    shutil.copy2(PROJECT/'docs'/name,APP/'docs'/name)
for source,destination in [
    (PROJECT/'work/native-smoke/actual-settings.png','settings.png'),
    (PROJECT/'work/ui-smoke/settings-e-bao.png','settings-e-bao.png'),
    (PROJECT/'work/chat-smoke/chat-window.png','chat-window.png'),
    (PROJECT/'work/chat-smoke/connection-settings.png','connection-settings.png'),
    (PROJECT/'work/chat-smoke/quick-chat.png','quick-chat.png'),
    (PROJECT/'work/chat-smoke/persona-settings.png','persona-settings.png'),
    (PROJECT/'work/chat-smoke/live-settings.png','live-settings.png'),
    (PROJECT/'work/chat-smoke/about.png','about.png'),
    (PROJECT/'assets/pet-preview-hd.png','dafeiyu.png'),
    (PROJECT/'assets/e-bao-pet-preview-hd.png','e-bao.png')]:
    shutil.copy2(source,PREVIEWS/destination)

counts=[6,8,8,4,5,8,6,6,6,8,8]
for prefix in ['', 'e-bao-']:
    with Image.open(PROJECT/'assets'/(prefix+'spritesheet.png')) as image:
        assert image.size==(3840,5720) and image.mode=='RGBA'
        for row,count in enumerate(counts):
            for col in range(8):
                frame=image.crop((col*480,row*520,(col+1)*480,(row+1)*520))
                bbox=frame.getbbox()
                if col<count:
                    assert bbox and bbox[0]>0 and bbox[1]>0 and bbox[2]<480 and bbox[3]<520, (prefix,row,col,bbox)
                else:
                    assert bbox is None, (prefix,row,col,'unused cell')
    print('PASS HD source validation: '+(prefix or 'dafeiyu'))

runtime_files=[file for file in APP.rglob('*') if file.is_file() and 'data' not in file.relative_to(APP).parts]
runtime_zip=OUTPUT/'大肥鱼桌宠-Windows.zip'
with ZipFile(runtime_zip,'w',ZIP_DEFLATED,compresslevel=6) as bundle:
    for file in sorted(runtime_files): bundle.write(file,'DafeiyuDeskPet/'+file.relative_to(APP).as_posix())
source_zip=OUTPUT/'大肥鱼桌宠-源码.zip'
with ZipFile(source_zip,'w',ZIP_DEFLATED,compresslevel=6) as bundle:
    for file in sorted(PROJECT.rglob('*')):
        if file.is_file() and not any(part in {'work','bin','obj','data','__pycache__','.git'} for part in file.relative_to(PROJECT).parts):
            bundle.write(file,'DafeiyuDeskPet/'+file.relative_to(PROJECT).as_posix())
    for file in PREVIEWS.glob('*.png'): bundle.write(file,'DafeiyuDeskPet/previews/'+file.name)

persona_source=(PROJECT/'src/PersonaProfiles.cs').read_text(encoding='utf-8-sig')
personas={name:re.search(r'public const string '+field+r'="([^"]+)";',persona_source).group(1) for name,field in [('E包','Ebao'),('大肥鱼','Dafeiyu')]}
for name,prefix in [('E包','e-bao-'),('大肥鱼','')]:
    with ZipFile(OUTPUT/(name+'-高清外观包.zip'),'w',ZIP_DEFLATED,compresslevel=6) as bundle:
        bundle.writestr('pet.json',json.dumps({'version':1,'name':name,'atlas':'spritesheet.png','system_prompt':personas[name],'description':'傲娇毒舌 · 白发正太' if name=='E包' else '温和俏皮 · 蓝白鲸鱼娘'},ensure_ascii=False,indent=2))
        bundle.write(PROJECT/'assets'/(prefix+'spritesheet.png'),'spritesheet.png')
        bundle.write(PROJECT/'assets'/(prefix+'pet-preview-hd.png'),'preview.png')
        bundle.write(PROJECT/'docs/skin-format.md','外观格式说明.md')

with ZipFile(runtime_zip) as bundle:
    assert bundle.testzip() is None
    assert not any('/data/' in name for name in bundle.namelist())
    assert 'DafeiyuDeskPet/templates/pet.json' in bundle.namelist() and 'DafeiyuDeskPet/templates/grid-guide.svg' in bundle.namelist()
    exe_name='DafeiyuDeskPet/大肥鱼桌宠.exe'
    assert hashlib.sha256(bundle.read(exe_name)).digest()==hashlib.sha256((APP/'大肥鱼桌宠.exe').read_bytes()).digest()
with ZipFile(source_zip) as bundle: assert bundle.testzip() is None
artifacts=[APP/'大肥鱼桌宠.exe',runtime_zip,source_zip,OUTPUT/'E包-高清外观包.zip',OUTPUT/'大肥鱼-高清外观包.zip']
(OUTPUT/'SHA256.txt').write_text('\n'.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+str(p.relative_to(OUTPUT)) for p in artifacts)+'\n',encoding='utf-8')
print('PASS archive integrity, executable identity and clean settings')
for file in artifacts: print(str(file)+' (%0.1f MB)'%(file.stat().st_size/(1024*1024)))
