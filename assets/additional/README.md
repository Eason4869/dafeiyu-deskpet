# 新增互动素材

> 作为用户要求保留的旧版踉跄／第二招牌动作，现与 [新版动作](../companion-animations/README.md) 一起构建并随机选择；下文是旧制作记录，不代表所有旧图的灰边已修复。

四张最终 PNG 均为 1920×2080：4 列 × 4 行，单格 480×520，真实透明背景。前两行为 8 个踉跄姿态，后两行为 8 个新增招牌姿态，各按 8 FPS 播放。

| 文件 | 新招牌 |
| --- | --- |
| e-bao.png | 比心鼓励 |
| dafeiyu.png | 泡泡环游 |
| marfu.png | 荧光咒语 |
| xiaobei.png | 全息键盘 |

第二招牌和三位伙伴的踉跄由内置 imagegen 生成，使用仓库中各自的原版预览图作身份／画风参考。通过 `PackAdditionalActions.cs` 拆格、统一组内比例和落脚线，不做混合或光流补帧。E包踉跄最终改为 `MakeEbaoStumble.cs` 驱动 0.5.4 已有落地／待机原帧，小幅左右摆动，保留原始颜色与轮廓；生成式踉跄草稿未采用。

## 提示词集

通用约束：

```text
Production desktop-pet animation sprite atlas. The original reference is the authoritative identity and style. Preserve face, hair, clothes, colors and head/body proportions. Exactly four columns by four rows, sixteen complete full-body poses, transparent background and generous empty gutters. Same scale throughout, grounded feet at a fixed baseline. No text, grid, checkerboard, background or ground shadow. Gentle eight-frame sequences at eight FPS, small adjacent pose changes, no jumping between poses, no teleporting or body scaling, no interpolation or ghosted overlays. First two rows: neutral landing, knees bend, slight left imbalance, balance left, return through center, slight right counterbalance, settle, neutral. Last two rows: the character-specific action below, returning to neutral at the end.
```

- E包：`Use the original silver-white-haired blue-eyed boy in the blue/white sport shirt and navy shorts. Bottom eight frames: neutral arms down, hand rises to chest, thumb/forefinger join, small finger-heart gesture, gentle wink with a tiny pale-blue heart, eyes open and heart fades, hand lowers, exact original neutral. No folded-arms pose. No gray halo or background remnants around hair.`
- 大肥鱼：`Blue-haired blue-eyed whale maid in the original navy/white dress. Lift cupped hands, make a few small pale-blue soap bubbles, guide a bubble arc close to the hands, watch them drift away, lower hands and return to neutral.`
- 马尔芙：`Long blonde hair, green eyes, original black robe with green trim and wand. Slowly raise wand, trace one small green glowing spiral above the other palm, hold briefly, spiral dims, lower wand to the original pose.`
- 小贝：`Original male technology boy, black/gray hair, black/cyan cat-ear headphones, black/white jacket. Open a small cyan holographic keyboard at waist, lightly type with both hands, show a small check, fade keyboard and return to neutral.`

素材回归检查 8 个姿态各自存在且不同、透明边界、落脚线和 WPF 实际顺序。E包恢复姿态另与原版待机的不透明像素及透明区域比较。浅／深背景对照由 `StumbleSmoke` 导出到隔离的 `work/stumble-*` 目录。
