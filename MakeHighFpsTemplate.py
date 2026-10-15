"""Create the authoring template for the v2 per-action illustrated atlas format."""
import csv
import hashlib
import json
from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parent
DEST=ROOT.parent/'high-fps-pet-template-v2'
CELL=(480,520)
ACTIONS=[
 ('idle','待机',32,24,True),('running-right','向右跑',48,30,True),('running-left','向左跑',48,30,True),
 ('waving','招呼',24,24,True),('jumping','跳跃',24,30,False),('failed','委屈',32,24,True),
 ('waiting','等待',32,24,True),('running','思考',32,24,True),('review','检查成果',32,24,True),
 ('lifted','拎起',24,24,True),('landing','落地',24,30,False),('petting','抚摸',40,24,False),
 ('poked','轻戳',24,30,False),('feeding','喂食',40,24,False),('happy','开心',32,24,False),
 ('sleeping','小憩',64,24,True),('special','专属动作',48,24,False)]

def guide(file,cols,rows,labels,title):
    w,h=CELL;im=Image.new('RGB',(cols*w,rows*h),'#edf1f6');d=ImageDraw.Draw(im)
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
    small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
    for i,label in enumerate(labels):
        x,y=i%cols*w,i//cols*h
        d.rectangle((x,y,x+w-1,y+h-1),outline='#98abc2',width=2)
        d.rectangle((x+25,y+28,x+w-25,y+h-18),outline='#cad5e3',width=1)
        d.line((x+w//2,y+45,x+w//2,y+h-15),fill='#b8c7da',width=1)
        d.line((x+30,y+500,x+w-30,y+500),fill='#668bcc',width=2)
        d.text((x+12,y+8),label,fill='#233955',font=font)
        d.text((x+14,y+h-45),'中心线 / 落脚线 500 px',fill='#6b7f97',font=small)
    im.save(file)

def main():
    (DEST/'animations').mkdir(parents=True,exist_ok=True);(DEST/'guides').mkdir(exist_ok=True)
    manifest=dict(version=2,name='我的新桌宠',format='illustrated-atlases-v1',cell_width=480,cell_height=520,
                  preview='preview.png',system_prompt='请在此填写桌宠的人格设定。',description='请填写一句角色介绍。',animations={})
    rows=[]
    for name,label,count,fps,loop in ACTIONS:
        cols=8;nr=(count+cols-1)//cols
        Image.new('RGBA',(cols*480,nr*520)).save(DEST/'animations'/f'{name}.png')
        guide(DEST/'guides'/f'{name}-guide.png',cols,nr,[f'{label}  {i+1:02d}/{count}' for i in range(count)],label)
        manifest['animations'][name]=dict(file=f'animations/{name}.png',columns=cols,frames=count,fps=fps,loop=loop)
        rows.append([name,label,count,fps,round(count/fps,3),cols,nr,cols*480,nr*520,'循环' if loop else '单次'])
    manifest['gaze']=dict(file='gaze.png',columns=4,frames=16,interpolation='none',order='clockwise-from-up')
    Image.new('RGBA',(1920,2080)).save(DEST/'gaze.png');Image.new('RGBA',CELL).save(DEST/'preview.png')
    directions=['正上','右上偏上','右上','右上偏右','正右','右下偏右','右下','右下偏下','正下','左下偏下','左下','左下偏左','正左','左上偏左','左上','左上偏上']
    guide(DEST/'guides/gaze-guide.png',4,4,[f'{i:02d}  {d}' for i,d in enumerate(directions)],'视线')
    guide(DEST/'guides/preview-guide.png',1,1,['角色预览'],'预览')
    (DEST/'pet.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    with (DEST/'动作表.csv').open('w',newline='',encoding='utf-8-sig') as f:
        writer=csv.writer(f);writer.writerow(['文件名','动作','真实帧数','播放FPS','秒/轮','列','行','整图宽','整图高','方式']);writer.writerows(rows)
    (DEST/'先读我.md').write_text('''# 高帧率桌宠素材模板 v2

这是新桌宠的**素材制作包**，其中的 PNG 都是空白透明画布，尚不是可使用的桌宠。
本次预览构建支持此 v2 格式；此前的原版 0.5.2 不支持。先完成全部画布，再在设置 → 伙伴 → 伙伴与人格 → 导入外观中选择 pet.json。空白模板会因缺少有效角色帧而被拒绝导入。

## 怎么开始

1. 先画 `preview.png`：480 × 520，完整角色，真实透明背景。
2. 先完成 `animations/idle.png`、`animations/jumping.png` 和 `gaze.png`，确认角色比例、透明边缘与实际逐帧效果，再扩展其他动作。
3. `guides/` 中是带编号的参考网格。将对应网格作为绘画软件的独立参考层，导出素材时隐藏网格、文字、中心线和落脚线。
4. 用完成的图替换 `animations/` 中同名 PNG。修改 `pet.json` 中的名字、人格与介绍。
5. 每组图从左到右、从上到下读取。`frames` 必须等于实际绘制的有效帧数；不足一行的末尾格子保持全透明。修改帧数时同步修改清单。

## 真实帧的要求

- 每一帧必须是独立完成的角色姿势。连续帧之间要有明确、细微、连续的动作变化。
- 不允许复制同一帧凑数；不允许把淡入淡出、重影混合、光流扭曲当作新绘制帧。
- 以约 24 张实绘帧/秒为制作目标；跑动、跳跃、轻戳、落地模板为 30 FPS。具体质量取决于动作连贯性，不能只看数字。
- 缓慢动作也要有足够的真实过渡；小憩模板提供 64 帧，不把四张静态姿势重复填满。
- 循环动作最后一帧应接近第一帧，但不需要复制第一帧。单次动作应有自然的准备、发力、缓冲和收尾。
- 当前模板不依赖任何帧间渐变。后续即便加入少量补间，也必须先验收纯实绘播放效果。

## 角色、位置和透明度

- 每格固定 480 × 520 px。身体中心约 x=240，正常站立的脚底约 y=500，左右及上方留出安全边距。
- 全部动作保持相同头身比、服装、画风、描线和角色尺寸。只移动身体位置，不随帧任意缩放。
- 跑步保留自然腾空；跳跃在本格内完成真实高度变化，不能切掉头发或鞋子。
- 输出 RGBA PNG，外部像素 alpha=0，边缘保留干净的抗锯齿。
- 禁止灰底、白底、棋盘格、外描灰圈、阴影底板、彩色抠图残边或半透明背景杂点。
- 分别放到白色、深灰、彩色背景上检查所有帧，不能只在透明棋盘格上检查。

## 视线是独立的真实插画

`gaze.png` 是 4 × 4，共 16 张完整角色插画，每格 480 × 520，总尺寸 1920 × 2080。
朝向指**角色看向的屏幕方向**，从正上开始顺时针，每格 22.5°；具体编号在 `guides/gaze-guide.png` 中。
所有朝向保持身体位置、脚底位置和比例一致。
光标停住后必须显示某一张真实朝向插画，严禁停在混合图、扭曲图或插值帧上。
移动时默认也只切换真实朝向；清单中的 `interpolation: "none"` 请保持不变。

## 文件结构

- `pet.json`：格式版本、角色信息、每组帧数、FPS 和循环设置。
- `preview.png`：单张角色预览。
- `gaze.png`：16 张真实视线插画。
- `animations/*.png`：9 组基础动作、8 组互动动作，具体尺寸见 `动作表.csv`。
- `guides/*.png`：仅供绘画对齐，不能作为最终素材导入。
- `AI绘制提示词.txt`：给绘图工具使用的提示词骨架。生成后的数量、透明度与连贯性仍须人工检查。

文件名 `running.png` 对应“思考”，`running-right.png` / `running-left.png` 才是跑动；这是现有桌宠动作标识的约定。
请勿把整个超大图集当作单张人物图；每格必须是一张完整角色，不能跨格绘制。
''',encoding='utf-8')
    (DEST/'AI绘制提示词.txt').write_text('''角色基准：使用我提供的 preview.png 作为唯一角色设计基准。严格保持头身比例、脸、发型、服装、配件和画风。
本次动作：[填写动作名和具体节奏]。
输出：[填写列数]列 × [填写行数]行，按从左到右、从上到下排列，共[填写真实帧数]张独立绘制的连续动画姿势。每格480×520，整图[填写总宽]×[填写总高]。
每格只出现一位完整角色，位置和大小稳定，正常脚底约y=500，所有头发、手脚、尾巴和配件必须在本格内。
逐张绘制细微但真实的动作变化。不是不同表情的拼图，不允许复制、交叉渐变、运动模糊、光流变形、半透明重影或重复肢体。
真正透明RGBA背景，无灰边、白圈、抠图彩边、阴影底板、背景杂点、网格线和文字。检查在白色和深色背景上的边缘。
循环动作：最后一帧自然接回第一帧。单次动作：完整覆盖准备、发力、缓冲、收尾。

建议分批绘制，每批8～12帧。提供前一批的末帧和统一角色基准，让下一批接续；最后按编号排列。不要一次要求绘图模型画几十帧后不做核对。

视线另做16张真实完整角色插画，编号0正上，顺时针每22.5度。禁止用转动、变形或混合已有图来冒充新朝向。
''',encoding='utf-8')
    zip_path=DEST.with_suffix('.zip')
    with ZipFile(zip_path,'w',ZIP_DEFLATED) as z:
        for file in sorted(DEST.rglob('*')):
            if file.is_file():z.write(file,DEST.name+'/'+file.relative_to(DEST).as_posix())
    with ZipFile(zip_path) as z:assert z.testzip() is None
    assert len(manifest['animations'])==17
    for item in manifest['animations'].values():
        im=Image.open(DEST/item['file']);assert im.mode=='RGBA' and not im.getchannel('A').getbbox()
        assert im.size==(item['columns']*480,math_ceil(item['frames'],item['columns'])*520)
    print(zip_path);print('17 action canvases + gaze + preview, JSON and ZIP validated.');print(hashlib.sha256(zip_path.read_bytes()).hexdigest())

def math_ceil(value,divisor):return (value+divisor-1)//divisor
if __name__=='__main__':main()
