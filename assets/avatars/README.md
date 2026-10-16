# 内置伙伴头像

四个 PNG 使用内置 imagegen 生成，以现有角色图为参考。Windows 与 Avalonia 在控件中按 20% 半径裁剪为圆角，README 使用对应的自包含 SVG（嵌入相同 PNG 与圆角 clipPath），保留原图像素并确保浅色／深色背景都没有白色外角。不依赖 GitHub 会过滤的行内 CSS。桌宠动作及原始立绘不变。

| 文件 | 参考图 | 身份特征 |
| --- | --- | --- |
| e-bao.png | ../e-bao-pet-preview-hd.png | 白色蓬松短发、蓝眼睛、白蓝运动服的男孩 |
| dafeiyu.png | ../pet-preview-hd.png | 深蓝到浅蓝渐变长发、蓝眼睛、白色女仆发箍、蓝白鲸鱼娘的耳鳍 |
| marfu.png | ../marfu-pet-preview-hd.png | 金色长发、翠绿眼睛、绿色领带和黑绿学院长袍的魔法少女 |
| xiaobei.png | ../bundled-skins/xiaobei/preview.png | 黑灰渐变蓬松头发、黑白猫耳带青色机械装饰、豆豆眼、黑白青色赛博衣领的可爱男孩 |

生成提示词模板（每位角色分别生成，替换 name / identity）：

> Use case: identity-preserve. Create one production avatar logo for the desktop companion {name}, using the attached existing character illustration as the identity reference, not as an image to display inside the logo. Preserve the character's exact distinguishing identity: {identity}. Square head-and-shoulders close-up portrait centered, face and hair occupy 85% of the square, preserve entire top of hair/ears and head accessories within a small safe margin, crop below shoulders. Cute polished chibi anime illustration faithful to the reference linework and coloring. A simple very pale neutral background, extremely subtle soft color tint, no scene, no objects blocking face. Clear silhouette and readable facial features at 24px menu size, harmonious consistent avatar family with other desktop companion characters. No full body, no legs, no text, no letters, no watermark, no collage, no grids. Single avatar asset.

