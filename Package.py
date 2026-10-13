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
release_source=(PROJECT/'src/ReleaseInfo.cs').read_text(encoding='utf-8-sig')
version=re.search(r'DisplayVersion="([^"]+)"',release_source).group(1)
PREVIEWS=APP/'previews'
PREVIEWS.mkdir(parents=True,exist_ok=True)
(APP/'docs').mkdir(exist_ok=True)
shutil.copytree(PROJECT/'templates',APP/'templates',dirs_exist_ok=True)
for name in ['README.md','CHANGELOG.md']:
    shutil.copy2(PROJECT/name,APP/name)
for name in ['skin-format.md','dialogue-setup.md','verification.md','sources.md','installation.md']:
    shutil.copy2(PROJECT/'docs'/name,APP/'docs'/name)
shutil.copytree(PROJECT/'previews',PREVIEWS,dirs_exist_ok=True)

counts=[6,8,8,4,5,8,6,6,6,8,8]
for prefix in ['', 'e-bao-', 'marfu-']:
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

runtime_files=[APP/name for name in ['桌面伙伴.exe','桌面伙伴.exe.config','DeskPet.Update.exe','DeskPet.Update.exe.config','README.md','CHANGELOG.md']]
runtime_files += [APP/'docs'/name for name in ['skin-format.md','dialogue-setup.md','verification.md','sources.md','installation.md']]
for folder in ['templates','previews']:
    runtime_files += [APP/file.relative_to(PROJECT) for file in (PROJECT/folder).rglob('*') if file.is_file()]
runtime_zip=OUTPUT/('DeskPet-Windows-'+version+'.zip')
with ZipFile(runtime_zip,'w',ZIP_DEFLATED,compresslevel=6) as bundle:
    for file in sorted(runtime_files): bundle.write(file,'DafeiyuDeskPet/'+file.relative_to(APP).as_posix())
source_zip=OUTPUT/('DeskPet-Source-'+version+'.zip')
with ZipFile(source_zip,'w',ZIP_DEFLATED,compresslevel=6) as bundle:
    for file in sorted(PROJECT.rglob('*')):
        relative=file.relative_to(PROJECT)
        if file.is_file() and not any(part in {'work','bin','obj','data','__pycache__','.git','.superpowers'} for part in relative.parts) and not relative.as_posix().startswith('docs/superpowers/'):
            bundle.write(file,'DafeiyuDeskPet/'+relative.as_posix())

persona_source=(PROJECT/'src/PersonaProfiles.cs').read_text(encoding='utf-8-sig')
personas={name:re.search(r'public const string '+field+r'="([^"]+)";',persona_source).group(1) for name,field in [('E包','Ebao'),('大肥鱼','Dafeiyu'),('马尔芙','Marfu')]}
for name,prefix in [('E包','e-bao-'),('大肥鱼',''),('马尔芙','marfu-')]:
    with ZipFile(OUTPUT/(name+'-高清外观包.zip'),'w',ZIP_DEFLATED,compresslevel=6) as bundle:
        bundle.writestr('pet.json',json.dumps({'version':1,'name':name,'atlas':'spritesheet.png','system_prompt':personas[name],'description':'傲娇毒舌 · 白发正太' if name=='E包' else '温和俏皮 · 蓝白鲸鱼娘' if name=='大肥鱼' else '聪明俏皮 · 金发绿眼魔法少女'},ensure_ascii=False,indent=2))
        bundle.write(PROJECT/'assets'/(prefix+'spritesheet.png'),'spritesheet.png')
        bundle.write(PROJECT/'assets'/(prefix+'pet-preview-hd.png'),'preview.png')
        bundle.write(PROJECT/'docs/skin-format.md','外观格式说明.md')

with ZipFile(runtime_zip) as bundle:
    assert bundle.testzip() is None
    assert not any('/data/' in name for name in bundle.namelist())
    assert 'DafeiyuDeskPet/templates/pet.json' in bundle.namelist() and 'DafeiyuDeskPet/templates/grid-guide.svg' in bundle.namelist()
    exe_name='DafeiyuDeskPet/桌面伙伴.exe'
    assert hashlib.sha256(bundle.read(exe_name)).digest()==hashlib.sha256((APP/'桌面伙伴.exe').read_bytes()).digest()
with ZipFile(source_zip) as bundle: assert bundle.testzip() is None
artifacts=[APP/'桌面伙伴.exe',runtime_zip,source_zip,OUTPUT/'E包-高清外观包.zip',OUTPUT/'大肥鱼-高清外观包.zip',OUTPUT/'马尔芙-高清外观包.zip']
setup=OUTPUT/('DeskPet-Setup-'+version+'.exe')
if setup.exists(): artifacts.append(setup)
(OUTPUT/'SHA256.txt').write_text('\n'.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+str(p.relative_to(OUTPUT)) for p in artifacts)+'\n',encoding='utf-8')
print('PASS archive integrity, executable identity and clean settings')
for file in artifacts: print(str(file)+' (%0.1f MB)'%(file.stat().st_size/(1024*1024)))
