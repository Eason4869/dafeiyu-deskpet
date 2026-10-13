"""Re-extract HD sprites from original generated strips; never upscale the small atlas."""
from pathlib import Path
import hashlib
import importlib.util
import json
import sys
from statistics import median
from PIL import Image

PROJECT = Path(__file__).resolve().parent
ORIGINAL = Path(r'C:\Users\彧晟\Documents\Codex\2026-10-13\pets-plugin-work-pets-openai-curated')
SCRIPTS = Path(r'C:\Users\彧晟\.codex\plugins\cache\openai-curated-remote\work-pets\0.1.6\skills\create-pet\scripts')
DECODED = ORIGINAL / 'work/deepseek-dafeiyu/decoded'
EBAO = '--e-bao' in sys.argv
if EBAO:
    DECODED = PROJECT.parent / 'E-bao-20261013/decoded'
KEY = (255,0,255) if EBAO else (0,255,0)
PREFIX = 'e-bao-' if EBAO else ''
CELL = (480, 520)
FACTOR = 2.5
NAMES = ['idle', 'running-right', 'running-left', 'waving', 'jumping', 'failed', 'waiting', 'running', 'review', 'look-row-9', 'look-row-10']
COUNTS = [6, 8, 8, 4, 5, 8, 6, 6, 6, 8, 8]

def module(name):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / (name + '.py'))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result

extract = module('extract_strip_frames')
despill = module('despill_chroma_edges')

def source_groups(name, count):
    path = DECODED / (name + '.png')
    with Image.open(path) as raw:
        strip = extract.remove_chroma_background(raw, KEY, 96)
    groups = extract.component_frame_groups(strip, count)
    assert len(groups) == count, (name, len(groups))
    return strip, groups, [extract.component_bounds(g) for g in groups]

def main():
    assets = PROJECT / 'assets'
    work = PROJECT / ('work/hd-assets/' + ('e-bao' if EBAO else 'dafeiyu'))
    assets.mkdir(exist_ok=True)
    work.mkdir(parents=True, exist_ok=True)
    old_path = ORIGINAL / 'outputs/Pets/deepseek-dafeiyu/spritesheet.png'
    if EBAO:
        old_path = PROJECT.parent / 'E-bao-20261013/final/spritesheet-extended.png'
    else:
        assert hashlib.sha256(old_path.read_bytes()).hexdigest() == 'fbfa9194980692ccddb299676b6147007aa6c0d84bfa6994f60492eb5d1ba5b3'
    old = Image.open(old_path).convert('RGBA')
    atlas = Image.new('RGBA', (CELL[0] * 8, CELL[1] * 11))
    cardinal_strip, cardinal_groups, cardinal_bounds = source_groups('look-cardinals', 4)
    report = {'cell': CELL, 'atlas': atlas.size, 'frames': 73, 'source': 'original generated strips', 'rows': []}
    for row, (name, count) in enumerate(zip(NAMES, COUNTS)):
        strip, groups, bounds = source_groups(name, count)
        old_bounds = [old.crop((col*192, row*208, (col+1)*192, (row+1)*208)).getbbox() for col in range(count)]
        scale = median([b[3]-b[1] for b in old_bounds]) * FACTOR / median([b[3]-b[1] for b in bounds])
        source_ground = max(b[3] for b in bounds)
        target_ground = max(b[3] for b in old_bounds) * FACTOR
        row_report = {'state': name, 'raw_dimensions': strip.size, 'scale_from_original': scale, 'source_sha256': hashlib.sha256((DECODED / (name+'.png')).read_bytes()).hexdigest(), 'bounds': []}
        for col, (group, bbox, target_bbox) in enumerate(zip(groups, bounds, old_bounds)):
            original, ground, frame_scale = strip, source_ground, scale
            anchor = {(9,0):0, (9,4):1, (10,0):2, (10,4):3}.get((row,col))
            if anchor is not None:
                original = cardinal_strip
                group, bbox = cardinal_groups[anchor], cardinal_bounds[anchor]
                ground = bbox[3]
                frame_scale = (target_bbox[3]-target_bbox[1])*FACTOR/(bbox[3]-bbox[1])
            cropped = extract.component_group_image(original, group, padding=4)
            clean, _ = despill.decontaminate_image(cropped, chroma_key=KEY, edge_radius=3)
            resized = clean.resize((round(clean.width*frame_scale), round(clean.height*frame_scale)), Image.Resampling.LANCZOS)
            left = round((target_bbox[0]+target_bbox[2])/2*FACTOR - resized.width/2)
            top = round(target_ground - (ground-max(0,bbox[1]-4))*frame_scale)
            assert left >= 0 and top >= 0 and left+resized.width <= CELL[0] and top+resized.height <= CELL[1], (name,col,left,top,resized.size)
            frame = Image.new('RGBA', CELL)
            frame.alpha_composite(resized, (left,top))
            atlas.alpha_composite(frame, (col*CELL[0],row*CELL[1]))
            frame.save(work / ('%02d-%02d.png' % (row,col)))
            row_report['bounds'].append(frame.getbbox())
        report['rows'].append(row_report)
        print('HD row complete: ' + name, flush=True)
    atlas.save(assets / (PREFIX+'spritesheet.png'), optimize=True)
    frame = atlas.crop((0,0,*CELL))
    frame.save(assets/(PREFIX+'pet-preview-hd.png'))
    preview = Image.new('RGBA', (256,256))
    preview.alpha_composite(frame.resize((236,256),Image.Resampling.LANCZOS), (10,0))
    preview.save(assets/(PREFIX+'pet-icon.png'))
    if not EBAO:
        preview.save(assets/'pet.ico',sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
    report['atlas_sha256'] = hashlib.sha256((assets/(PREFIX+'spritesheet.png')).read_bytes()).hexdigest()
    (assets/(PREFIX+'hd-metadata.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    build = PROJECT/'Build.ps1'
    build.write_text(build.read_text(encoding='utf-8-sig'),encoding='utf-8-sig')
    print('HD atlas: 3840 x 5720; 73 frames at 480 x 520.',flush=True)

if __name__ == '__main__':
    main()
