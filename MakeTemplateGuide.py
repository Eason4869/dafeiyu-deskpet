"""Create a code-native SVG grid reference; the usable example PNG is copied unchanged."""
from pathlib import Path
import html

root=Path(__file__).resolve().parent
counts=[6,8,8,4,5,8,6,6,6,8,8]
labels=['待机 idle','向右跑','向左跑','招呼 waving','跳跃 jumping','委屈 failed','等待 waiting','思考 running','检查 review','视线 0°–157.5°','视线 180°–337.5°']
parts=['<svg xmlns="http://www.w3.org/2000/svg" width="1140" height="1040" viewBox="0 0 1140 1040">','<rect width="1140" height="1040" fill="#f6f8fd"/>','<g font-family="Microsoft YaHei, sans-serif" fill="#203754">','<text x="30" y="40" font-size="25" font-weight="bold">桌面伙伴 · 8 × 11 动作布局</text>','<text x="30" y="69" font-size="14">每个格子是一帧，灰色格保持透明。此 SVG 是参考图；导入请使用 PNG。</text>']
for row,(count,label) in enumerate(zip(counts,labels)):
    y=100+row*78
    parts.append(f'<text x="25" y="{y+38}" font-size="14">{row+1}. {html.escape(label)}</text>')
    for col in range(8):
        x=205+col*112
        color='#edf3ff' if col<count else '#e3e7ee'
        value=f'{col+1}' if col<count else '透明'
        parts.append(f'<rect x="{x}" y="{y}" width="102" height="66" rx="9" fill="{color}" stroke="#ccd8ee"/><text x="{x+51}" y="{y+39}" text-anchor="middle" font-size="14">{value}</text>')
parts.append('<text x="30" y="1000" font-size="14">推荐单帧 480 × 520；完整 PNG 3840 × 5720。完整教程见 docs/skin-format.md。</text></g></svg>')
(root/'templates/grid-guide.svg').write_text('\n'.join(parts),encoding='utf-8')
print('PASS template reference SVG created')
