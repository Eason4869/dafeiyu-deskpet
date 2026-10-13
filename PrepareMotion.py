"""Offline motion atlases; users need only the embedded PNGs and .NET.

Install requirements-motion.txt, then: python PrepareMotion.py
Original HD atlases remain the import/export format. Motion atlases use 8x
temporal samples at the pet's native 192x208 display size.
"""
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent
COUNTS = [6, 8, 8, 4, 5, 8, 6, 6, 6]
MULTIPLIER = 8
W, H = 192, 208
GRID = np.stack(np.meshgrid(np.arange(W), np.arange(H)), -1).astype(np.float32)


def foot_center(frame):
    alpha = np.asarray(frame)[..., 3].astype(float)
    ys, xs = np.nonzero(alpha > 32)
    bottom = int(ys.max()) + 1
    weights = alpha[max(0, bottom - round(frame.height * 24 / 520)):bottom]
    return float((weights.sum(0) * np.arange(frame.width)).sum() / weights.sum()), bottom


def register(frames):
    """Anchor feet, never the hair/wand bounding box, across idle/look poses."""
    anchors = [foot_center(frame) for frame in frames]
    target_x, target_y = anchors[0]
    aligned = []
    for frame, (x, y) in zip(frames, anchors):
        canvas = Image.new('RGBA', frame.size)
        canvas.alpha_composite(frame, (round(target_x - x), round(target_y - y)))
        aligned.append(canvas)
    return aligned, {
        'foot_x_range_before_hd_px': round(float(np.ptp([x for x, _ in anchors])), 3),
        'foot_x_range_after_hd_px': round(float(np.ptp([foot_center(f)[0] for f in aligned])), 3),
    }


def premultiply(frame):
    pixels = np.asarray(frame.resize((W, H), Image.Resampling.LANCZOS)).astype(np.float32) / 255
    pixels[..., :3] *= pixels[..., 3:4]
    return pixels


def tween_pair(a, b):
    # Optical flow follows opaque shapes on a neutral background. Warp
    # premultiplied RGBA so transparent-edge RGB cannot create dark halos.
    def gray(p):
        composite = p[..., :3] + .7 * (1 - p[..., 3:4])
        return cv2.cvtColor(np.uint8(np.clip(composite * 255, 0, 255)), cv2.COLOR_RGB2GRAY)
    ga, gb = gray(a), gray(b)
    flow = cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
    ab = flow.calc(ga, gb, None)
    ba = flow.calc(gb, ga, None)
    # Disoccluded background has ambiguous flow. Intermediate silhouettes
    # must stay inside the union of the two endpoint bounds, with a clear
    # outer pixel for WPF hit testing and interpolation at the atlas edge.
    ys, xs = np.nonzero(np.maximum(a[..., 3], b[..., 3]) > 4 / 255)
    left, right = max(1, int(xs.min())-1), min(W-1, int(xs.max())+2)
    top, bottom = max(1, int(ys.min())-1), min(H-1, int(ys.max())+2)
    for step in range(MULTIPLIER):
        t = step / MULTIPLIER
        if step == 0:
            mixed = a.copy()
        else:
            ma, mb = GRID - t * ab, GRID - (1 - t) * ba
            warped_a = cv2.remap(a, ma[..., 0], ma[..., 1], cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
            warped_b = cv2.remap(b, mb[..., 0], mb[..., 1], cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
            mixed = warped_a * (1 - t) + warped_b * t
            # At occlusions (crossing feet / a moving wand), a dissolve leaves
            # two translucent limbs. Keep the nearer warped silhouette there;
            # blend color only where both endpoints have visible coverage.
            unmatched = (warped_a[..., 3] > .03) != (warped_b[..., 3] > .03)
            mixed[unmatched] = (warped_a if t < .5 else warped_b)[unmatched]
        mixed[..., :3] /= np.maximum(mixed[..., 3:4], 1 / 255)
        # Lanczos/flow can leave sub-1% opacity specks outside the silhouette.
        mixed[mixed[..., 3] <= 2 / 255] = 0
        mixed[:top] = 0
        mixed[bottom:] = 0
        mixed[:, :left] = 0
        mixed[:, right:] = 0
        yield Image.fromarray(np.uint8(np.clip(mixed * 255 + .5, 0, 255)))


def build(prefix, source):
    sheet = Image.open(ROOT / 'assets' / source).convert('RGBA')
    cw, ch = sheet.width // 8, sheet.height // 11
    rows = [[sheet.crop((c*cw, r*ch, (c+1)*cw, (r+1)*ch))
             for c in range(COUNTS[r] if r < 9 else 8)] for r in range(11)]
    # One common planted stance for idle and gaze removes source registration
    # wobble without suppressing intentional running/jumping motion.
    standing, metrics = register(rows[0] + rows[9] + rows[10])
    rows[0], rows[9], rows[10] = standing[:6], standing[6:14], standing[14:]
    output = Image.new('RGBA', (64*W, 11*H))
    populated = 0
    for row, sequence in enumerate(rows[:9] + [rows[9] + rows[10]]):
        pixels = [premultiply(f) for f in sequence]
        for i, a in enumerate(pixels):
            for j, frame in enumerate(tween_pair(a, pixels[(i+1) % len(pixels)])):
                index = i * MULTIPLIER + j
                target_row = row if row < 9 else 9 + index // 64
                output.paste(frame, ((index % 64)*W, target_row*H))
                populated += 1
        print(prefix, 'row', row, flush=True)
    destination = ROOT / 'assets' / (prefix + '-motion.png')
    output.save(destination, compress_level=9)
    report = dict(pet=prefix, size=list(output.size), cell=[W, H], frames=populated,
                  multiplier=MULTIPLIER, gaze_directions=128, registration=metrics)
    (ROOT / 'assets' / (prefix + '-motion.json')).write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report), flush=True)


if __name__ == '__main__':
    cv2.setNumThreads(2)
    for prefix, source in [('e-bao', 'e-bao-spritesheet.png'), ('dafeiyu', 'spritesheet.png'), ('marfu', 'marfu-spritesheet.png')]:
        build(prefix, source)
