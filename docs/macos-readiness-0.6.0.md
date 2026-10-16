# macOS 0.6.0 验证与功能矩阵

本版保留 Windows WPF 客户端，增加基于相同业务代码的 Avalonia macOS 适配。Mac 附件是 **ad-hoc 签名、未经 Apple 公证的 preview**；不具备 Developer ID 信任，不代表已通过全部人工设备验收。用户无需另装 .NET，也不需要 Docker。

## 版本与附件

- Windows 与 Mac 版本均为 `0.6.0`；最终 Windows 业务基线须在 `0.5.6` 完成后合入并再次回归。
- Apple Silicon：`DeskPet-macOS-0.6.0-arm64-preview.dmg`、同名 `.zip`。
- Intel：`DeskPet-macOS-0.6.0-x86_64-preview.dmg`、同名 `.zip`。
- Mac 从“关于与更新”只能启用与本机 CPU 对应的 Mac 附件；Windows 安装包、另一 CPU、模板与未知命名不能成为更新入口。
- 最终 GitHub Release 的 Windows、Mac、源码及素材共用根发布流程生成的版本化 SHA256 清单。每架构 CI 中的 `SHA256SUMS.txt` 仅供构建核验，不作为第二份公开发布清单。

## 已取得的本地证据

`codex/macos-0.6.0` 在 Windows 隔离工作树上执行：

| 检查 | 结果 | 覆盖边界 |
| --- | --- | --- |
| `tests/MacPackagingTests.py` | 13 项通过 | bundle 布局、版本、Mach-O 架构、私密文件拒绝、保留已有输出、preview 签名策略、托管文件签名顺序、正式 helper Hardened Runtime、正式凭据缺失拒绝 |
| `DeskPet.Tests.dll --report-json` | 291 项通过；报告 `success=true` | 共用协议、代理、流式、会话、历史、队列、听写、Live、问候、录音故障与引导升级 |
| `DeskPet.Desktop.Tests.dll` | 1474 项通过 | 实际 Avalonia 控件、资源与动画、混合平台附件选择、0.5.6→0.6.0 引导升级；这是 Headless 渲染和可注入服务测试 |
| `DeskPet.Platform.Mac.Tests.dll` | 23 项平台边界、10 项录音生命周期、4 项朗读进程检查通过 | 本机为 Windows；不把这些结果计作 Mac 原生执行 |

## 功能矩阵

| 功能 | 实现与自动验证 | 仍需人工设备验收 |
| --- | --- | --- |
| 四宠、原画与互动 | 复用四宠原资源；Headless 检查帧边界、8 FPS、新旧配对、喂食后开心、拖放落地；同名自定义外观独立 | 不同缩放率下的拖动、透明像素点击与交互手感 |
| 桌面行为 | 暂停、隐藏、散步、跟随、免打扰、空闲睡眠、全屏策略与原生窗口桥接 | 多显示器、Spaces、实际前台全屏、睡眠唤醒 |
| 操作入口 | 菜单栏、右键、互动轮盘、快聊、完整聊天、设置、引导、关于与更新 | 跨应用快捷键触发与菜单栏恢复穿透操作 |
| 人格与数据 | 每宠人格、音色、历史和队列共用业务；认证加密与损坏数据拒绝；Mac 使用 Keychain 支撑密钥 | 实际安装后 Keychain 授权与预览升级授权连续性 |
| 模型与对话 | OpenAI/Anthropic、独立厂商/地址/Key/代理、模型列表、取消、流式、队列编辑/重排/引导 | 用户实际服务商及网络环境 |
| 听写与朗读 | 按需申请权限；拒绝与设备失败保留文字功能；原生输入转换、PCM 输出、云端 WAV、`say` 子进程 | 首次授权/拒绝/撤销、输入设备变化、系统与云端实际听感 |
| Gemini Live | 共用双向 PCM、字幕、打断、音量和取消；退出释放输入输出 | 真实 Gemini 服务与双向实际听感 |
| 引导升级 | 新安装首次显示；同 `Major.Minor` 系列补丁不重复；跨系列触发一次；0.5.6→0.6.0 会触发新版引导；可手动重开 | 已有用户数据目录的真实安装升级 |
| 安装与更新 | 自包含 `.app`，DMG 的 `/Applications` 链接，按本机 CPU 选择 Mac 附件；下载后由用户替换应用 | Gatekeeper 提示、拖入 Applications、替换后数据保留与回退 |
| 登录启动 | 用户主动启用/禁用，调用 SMAppService；失败显示错误 | 登录项系统批准、注销/重启、移动应用路径后的行为 |
| 自定义外观 | 原格式导入、路径穿越/损坏文件拒绝；导入中断不出现半成品 | 用户自有外观的显示效果 |

## 双架构 CI 与正式签名边界

首次真实 CI [38846284163](https://github.com/Eason4869/deskpet/actions/runs/38846284163) 中，两架构共用业务通过，原生桥均编译成功；随后 `lipo -verify_arch` 把输入文件错误地识别为架构名。已修复为输入文件放前。

第二轮 [38846523644](https://github.com/Eason4869/deskpet/actions/runs/38846523644) 中，两架构分别通过原生自检 50 项、原生 P/Invoke 23 项、录音生命周期 10 项、朗读子进程 4 项和 Headless UI 1449 项。打包随后因 `codesign` 把 `Contents/MacOS` 中未签名的托管 DLL 识别为嵌套代码而失败。已按 Avalonia 官方部署方式显式签署全部 payload 文件，并增加 ZIP 解压后的严格签名验证；DMG staging 使用 `ditto` 保留签名元数据。最终 0.6.0 CI 尚待完成。

完整 CI 还会运行原生隔离 Keychain/窗口/快捷键/音频边界、自包含发布、签名完整性、DMG 挂载，以及隔离数据目录中的真实 Avalonia 应用启动。运行完成后应在本文件记录每架构结果和下载制品 SHA256；尚无结果的项不得标为通过。

`release` 打包模式要求 Developer ID Application 身份、Keychain 中的公证 profile 和两架构人工验收证据。缺少凭据或证据直接失败；`release-evidence.example.json` 保持未通过的模板。preview 模式关闭 Hardened Runtime 以允许无 Team ID 的 ad-hoc 运行库加载；正式签名模式保留 Hardened Runtime 和 JIT entitlement，按原生库→app→app 公证/staple→归档→DMG 签名公证/staple 的顺序执行。
