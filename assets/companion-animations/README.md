# 原画参考动作优化

基于 0.5.4。四位伙伴的四项技能、投喂后开心、拖起、普通落地及踉跄共 32 条，另含 E包跳跃修复，共 33 条、264 帧。各 PNG 为 3840×520、8 格，每格 480×520 RGBA，8 FPS。`lifted` / `sleeping` 循环，其余 1 秒单次播放。

素材由内置 imagegen 逐行生成，每次附 `previews/{id}.png` 原画。使用洋红纯色源底；`tools/prepare_companion_animations.py` 调用 Pets create-pet 的 bundled extraction、despill、inspection 脚本，提取完整 connected components，整行共享缩放和定位，不逐帧归一化，不混合或生成补帧。完整原始源条、精确提示、处理参数、检查 JSON、原画对照及浅深背景动图位于仓库的 `work/{Ebao,Dafeiyu,Marfu,Xiaobei}-repair`。

`provenance.json` 记录选定源条 SHA256 和精确提示。透明／边缘检查不代表重绘与原图逐像素相同；最终仍需结合实际大小的动画观感验收。

运行时与自定义皮肤的原始图集协议分离。原有基础图集以及摸头、戳一戳未重绘。0.5.5 dev 同时保留早期 `assets/additional` 作为用户要求的旧版踉跄／第二招牌，与本目录新版随机选择；旧版其他互动从原始互动图／内置包读取。版本选择在开始时进行，拖放与喂食过渡保持同一版本。
