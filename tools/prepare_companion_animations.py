"""Adapt the Pets component/extraction/edge-QA pipeline to DeskPet's 480x520 clips.

Only deterministic processing of imagegen artwork; never synthesizes poses.
"""
import argparse
import importlib.util
import json
import hashlib
import sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = Path.home() / '.codex/plugins/cache/openai-curated-remote/work-pets/0.1.6/skills/create-pet/scripts'
PETS = {'e-bao': 'Ebao', 'dafeiyu': 'Dafeiyu', 'marfu': 'Marfu', 'xiaobei': 'Xiaobei'}
ACTIONS = ['lifted', 'landing', 'feeding', 'happy', 'sleeping', 'special', 'special2', 'stumble']


def module(name):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / (name + '.py'))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    result.CELL_WIDTH, result.CELL_HEIGHT = 480, 520
    result.ROW_FRAME_COUNTS = {name: 8 for name in ACTIONS + ['jumping']}
    return result


def bounds(frame):
    return frame.getchannel('A').point(lambda a: 255 if a > 32 else 0).getbbox()


def prepare(pet, action, extract, despill):
    run = ROOT / 'work' / (PETS[pet] + '-repair')
    source = run / 'decoded' / (('finger-heart' if action == 'special2' and pet == 'e-bao' else action) + '.png')
    if not source.exists():
        return None
    destination = ROOT / 'assets/companion-animations' / pet / (action + '.png')
    if destination.exists() and destination.stat().st_mtime > source.stat().st_mtime:
        return json.loads((run / 'frames' / action / 'registration.json').read_text())
    # The bundled extractor finds complete connected pose groups, preserving
    # shared row scale and vertical movement; equal slot slicing is forbidden.
    row = extract.extract_state(source, action, run / 'extracted', (255, 0, 255), 96, 'components')
    frames = [Image.open(path).convert('RGBA') for path in row['frames']]
    reference = Image.open(ROOT / 'previews' / (pet + '.png')).convert('RGBA')
    canonical = bounds(reference)
    neutral = bounds(frames[0])
    scale = (canonical[3] - canonical[1]) / (neutral[3] - neutral[1])
    # One transform for the whole row; no pose-by-pose size or ground snapping.
    # Reserve the full observed motion envelope so generated rise is preserved.
    envelope = [bounds(frame) for frame in frames]
    vertical = max(b[3] for b in envelope) - min(b[1] for b in envelope)
    scale = min(scale, (canonical[3] - 3) / vertical)
    dx = (canonical[0] + canonical[2]) / 2 - (neutral[0] + neutral[2]) / 2 * scale
    dy = canonical[3] - neutral[3] * scale
    if min(b[1] * scale + dy for b in envelope) < 3:
        dy += 3 - min(b[1] * scale + dy for b in envelope)
    if max(b[3] * scale + dy for b in envelope) > 518:
        dy -= max(b[3] * scale + dy for b in envelope) - 518
    output = run / 'frames' / action
    output.mkdir(parents=True, exist_ok=True)
    atlas = Image.new('RGBA', (3840, 520))
    reports = []
    for i, frame in enumerate(frames):
        registered = frame.transform((480, 520), Image.Transform.AFFINE,
            (1 / scale, 0, -dx / scale, 0, 1 / scale, -dy / scale), Image.Resampling.BICUBIC)
        # One edge-local despill pass with the actual generation key; opaque
        # interior colors remain untouched, fully transparent RGB is cleared.
        cleaned, report = despill.decontaminate_image(registered, chroma_key=(255, 0, 255))
        cleaned.save(output / f'{i:02d}.png')
        atlas.alpha_composite(cleaned, (480 * i, 0))
        reports.append(report)
    destination.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(destination)
    registration = {'pet': pet, 'action': action, 'source': str(source), 'reference': str(ROOT / 'previews' / (pet + '.png')),
                    'scale': scale, 'dx': dx, 'dy': dy, 'fps': 8, 'frames': 8, 'method': 'components', 'edge_passes': 1,
                    'despill': reports}
    (output / 'registration.json').write_text(json.dumps(registration, indent=2), encoding='utf-8')
    return registration


def inspect(pet, actions, inspector):
    run = ROOT / 'work' / (PETS[pet] + '-repair')
    manifest = {'chroma_key': {'hex': '#FF00FF', 'rgb': [255, 0, 255]},
                'rows': [{'state': a, 'method': 'components'} for a in actions]}
    (run / 'frames' / 'frames-manifest.json').write_text(json.dumps(manifest), encoding='utf-8')
    sys.argv = ['inspect_frames', '--frames-root', str(run / 'frames'), '--json-out', str(run / 'inspection.json'),
                '--states', ','.join(actions), '--require-components']
    try:
        inspector.main()
    except SystemExit as error:
        if error.code:
            raise


def previews(pet, actions):
    run = ROOT / 'work' / (PETS[pet] + '-repair')
    original = Image.open(ROOT / 'previews' / (pet + '.png')).convert('RGBA')
    tiles = []
    for a in actions:
        frames = [Image.open(run / 'frames' / a / f'{i:02d}.png').convert('RGBA') for i in range(8)]
        contact = Image.new('RGB', (240 * 9, 280 * 2))
        for y, bg in enumerate(('#F6F6F6', '#20242C')):
            for i, frame in enumerate([original] + frames):
                tile = Image.new('RGBA', (480, 560), bg)
                tile.alpha_composite(frame)
                ImageDraw.Draw(tile).text((12, 530), 'original' if i == 0 else f'{a} {i-1}', fill='black' if y == 0 else 'white')
                contact.paste(tile.convert('RGB').resize((240, 280)), (i * 240, y * 280))
        contact.save(run / (a + '-contact.jpg'))
        tiles.append(contact.resize((1080, 280)))
        animation = []
        for frame in [original] * 4 + frames + [original] * 4:
            view = Image.new('RGBA', (960, 520), '#F6F6F6')
            view.paste(Image.new('RGBA', (480, 520), '#20242C'), (480, 0))
            view.alpha_composite(frame)
            view.alpha_composite(frame, (480, 0))
            animation.append(view.convert('RGB'))
        # GIF stores centiseconds: alternating 120/130 ms averages exactly 8 FPS.
        animation[0].save(run / (a + '-motion.gif'), save_all=True, append_images=animation[1:], duration=[120,130] * (len(animation)//2),
                          loop=0, disposal=2, optimize=False)
    sheet = Image.new('RGB', (1080, 280 * len(tiles)))
    for i, tile in enumerate(tiles):
        sheet.paste(tile, (0, i * 280))
    sheet.save(run / 'all-actions.jpg')


def provenance():
    records = []
    for pet, title in PETS.items():
        run = ROOT / 'work' / (title + '-repair')
        for action in ACTIONS + (['jumping'] if pet == 'e-bao' else []):
            name = 'finger-heart' if action == 'special2' and pet == 'e-bao' else action
            source = run / 'decoded' / (name + '.png')
            final = ROOT / 'assets/companion-animations' / pet / (action + '.png')
            if not final.exists():
                continue
            prompt = next((p for p in [run / 'prompts' / (name + '-repair.txt'), run / 'prompts' / (name + '.txt')] if p.exists()), None)
            if prompt is None:
                raise RuntimeError(f'Missing exact generation prompt: {pet}/{action}')
            records.append({'pet': pet, 'action': action, 'source': str(source.relative_to(ROOT)),
                            'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                            'asset_sha256': hashlib.sha256(final.read_bytes()).hexdigest(),
                            'reference': f'previews/{pet}.png',
                            'reference_sha256': hashlib.sha256((ROOT / 'previews' / (pet + '.png')).read_bytes()).hexdigest(),
                            'prompt': prompt.read_text(encoding='utf-8-sig')})
    (ROOT / 'assets/companion-animations/provenance.json').write_text(json.dumps({'generator': 'built-in imagegen',
        'pipeline': 'Pets create-pet 0.1.6 extraction/despill/inspection adapted to DeskPet cells', 'records': records}, ensure_ascii=False, indent=2), encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--pet', choices=list(PETS), required=True)
    args = parser.parse_args()
    ex, de, qa = module('extract_strip_frames'), module('despill_chroma_edges'), module('inspect_frames')
    actions = ACTIONS + (['jumping'] if args.pet == 'e-bao' else [])
    ready = [a for a in actions if prepare(args.pet, a, ex, de)]
    if ready:
        inspect(args.pet, ready, qa)
        previews(args.pet, ready)
        provenance()
    print(json.dumps({'pet': args.pet, 'ready': ready, 'pending': [a for a in actions if a not in ready]}))
