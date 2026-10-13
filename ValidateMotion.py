"""Verify source/motion grids and produce inspectable animation evidence."""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from PrepareMotion import COUNTS, W, H, ROOT, foot_center


def main():
    destination = ROOT / 'work' / 'motion-qa'
    destination.mkdir(parents=True, exist_ok=True)
    reports = []
    for name, source in [('e-bao', 'e-bao-spritesheet.png'), ('dafeiyu', 'spritesheet.png'), ('marfu', 'marfu-spritesheet.png')]:
        base = Image.open(ROOT / 'assets' / source).convert('RGBA')
        assert base.size == (3840, 5720)
        for row, count in enumerate(COUNTS + [8, 8]):
            for col in range(8):
                bbox = base.crop((col*480, row*520, (col+1)*480, (row+1)*520)).getbbox()
                if col < count:
                    assert bbox and bbox[0] > 0 and bbox[1] > 0 and bbox[2] < 480 and bbox[3] < 520, (name, 'HD', row, col, bbox)
                else:
                    assert bbox is None, (name, 'HD unused', row, col)
        motion = Image.open(ROOT / 'assets' / (name + '-motion.png')).convert('RGBA')
        assert motion.size == (64 * W, 11 * H)
        frames = []
        for row, count in enumerate(COUNTS + [8, 8]):
            sequence = []
            for col in range(64):
                frame = motion.crop((col*W, row*H, (col+1)*W, (row+1)*H))
                bbox = frame.getbbox()
                if col < count*8:
                    assert bbox and bbox[0] > 0 and bbox[1] > 0 and bbox[2] < W and bbox[3] < H, (name, row, col, bbox)
                    sequence.append(frame)
                else:
                    assert bbox is None, (name, row, col, 'unused cell')
            # No adjacent frames may be simple duplicate copies.
            for a, b in zip(sequence, sequence[1:]):
                assert not np.array_equal(a, b), (name, row, 'duplicate frame')
            frames.append(sequence)
        def idle_range(sheet, count, cw, ch):
            centers = [foot_center(sheet.crop((i*cw, 0, (i+1)*cw, ch)))[0] / cw for i in range(count)]
            return round(float(np.ptp(centers)) * W, 3)
        report = dict(pet=name, validated_frames=sum(map(len, frames)),
                      idle_foot_drift_before_display_px=idle_range(base, 6, 480, 520),
                      idle_foot_drift_after_display_px=idle_range(motion, 48, W, H))
        reports.append(report)
        gallery = Image.new('RGB', (8*W, 3*(H+24)), '#eeeeee')
        draw = ImageDraw.Draw(gallery)
        for slot, row in enumerate([0, 1, 9]):
            for col, index in enumerate([0, 2, 4, 6, 8, 10, 12, 14]):
                gallery.paste(frames[row][index], (col*W, slot*(H+24)+24), frames[row][index])
                draw.text((col*W+6, slot*(H+24)+5), '%s / %s' % (row, index), fill='#333333')
        gallery.save(destination / (name + '-contact.jpg'))
        for label, sequence, duration in [('idle', frames[0], 20), ('run', frames[1], 14), ('gaze', frames[9]+frames[10], 20)]:
            rgb = []
            for frame in sequence:
                canvas = Image.new('RGB', frame.size, '#eeeeee')
                canvas.paste(frame, mask=frame.getchannel('A'))
                rgb.append(canvas)
            rgb[0].save(destination / (name + '-' + label + '.gif'), save_all=True, append_images=rgb[1:], duration=duration, loop=0)
        print(json.dumps(report), flush=True)
    (destination / 'validation.json').write_text(json.dumps(reports, indent=2)+'\n')


if __name__ == '__main__':
    main()
