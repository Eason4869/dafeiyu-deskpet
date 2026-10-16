# macOS 功能对齐与发布设计

状态：设计待审阅，尚未实现 macOS 客户端，不能称为已具备发布能力。基线为 0.5.4 与当前本地互动改动。用户已确认“尽量与当前 Windows 功能对齐”。

## 目标与选择

推荐新增 Avalonia 12.1.4 / .NET 10 桌面客户端，保留现有 Windows WPF 入口。将协议、会话、队列、配置模型及互动状态机作为可跨平台核心复用，重做窗口与系统服务适配；所有伙伴直接复用原始资源与新旧随机动作规则。目标 macOS 14+，分别产出 Apple Silicon 与 Intel 的自包含应用，用户无需另装 .NET。

比较过三个方向：

| 方案 | 优点 | 主要成本 | 选择 |
| --- | --- | --- | --- |
| Avalonia + 共用 C# 核心 | 现有业务和测试可复用，适合长期保持两端一致 | WPF UI 和平台服务仍须迁移 | 推荐 |
| Swift/AppKit 全原生 | 系统集成自然 | 需要重写所有 C# 业务和绝大多数测试，双端容易分叉 | 不选 |
| Electron/Tauri | Web UI 生态成熟 | 现有 WPF UI 与 C# 服务都无法直接复用，引入额外运行桥接 | 不选 |

Avalonia 使用开源框架，不依赖商业 XPF 或收费打包服务。最终仍使用 Apple 原生签名、公证和磁盘映像工具。

## 项目与接口

- `desktop/DeskPet.Core`：共用业务，链接或提取现有网络协议、会话、队列、人格、空闲与互动规则；不引用 WPF、WinForms、System.Speech、user32、winmm、注册表或 DPAPI。
- `desktop/DeskPet.Desktop`：Avalonia 窗口、透明动画、轮盘、快速对话、完整聊天、设置、引导、托盘及平台服务装配。
- `desktop/DeskPet.Platform.Mac`：窗口行为、鼠标与空闲、快捷键、Keychain、录音、PCM 播放、系统朗读、登录启动与更新安装。
- `desktop/DeskPet.Tests`：跨平台状态机、协议、队列、资源、数据和打包契约测试。
- `installer/macos`：Info.plist、entitlements、图标、构建打包、签名公证与验证脚本。
- `.github/workflows/macos.yml`：两架构编译测试和 unsigned preview；正式制品必须另外通过签名、公证与发布门禁。

现有 WPF 与 `Build.ps1` 保留，公共改动必须重跑 Windows 回归。Mac 项目不能伪装复用 WPF，也不通过 Wine 运行 exe。

## 功能验收矩阵

| 功能 | macOS 要求 |
| --- | --- |
| 四宠与素材 | 四个内置伙伴；原画动作、新旧随机技能及配对拖放；8 FPS；透明像素不拦截鼠标 |
| 桌面行为 | 缩放、置顶、多屏边界、拖动、隐藏、暂停、散步、鼠标跟随、免打扰、空闲睡眠、全屏策略 |
| 操作入口 | 菜单栏图标、右键菜单、四格轮盘、双击快聊、完整聊天、快捷键与清楚的冲突提示 |
| 人格与数据 | 每宠独立人格、音色、历史、队列；导出；损坏配置恢复；原子保存 |
| 模型配置 | 厂商、独立地址/密钥/代理、模型列表、多选添加、显示名、手填 ID、请求取消及迟到响应隔离 |
| 对话 | OpenAI 与 Anthropic 协议；流式、停止、错误恢复、草稿、队列重排/编辑/删除/引导 |
| 语音 | 麦克风权限、听写、云端朗读、系统朗读、音量、音色与错误反馈 |
| Live | Gemini Live 双向 PCM、字幕、取消、独立配置、关闭后释放设备 |
| 引导 | 首次与重开引导、按实际 Mac 控件操作、分步保存、跳过/恢复/退出释放锁定 |
| 安装与更新 | 拖入 Applications；用户数据在 Application Support；升级保留数据；更新验证签名/哈希/架构/版本与回滚 |
| 自启 | 用户主动启用/禁用；适应移动应用路径；不在首次启动静默添加 |
| 自定义外观 | 原 v1/v2 格式与资源校验；不能因同名导入替换内置资源 |

Windows 组件替换：WPF→Avalonia；DPAPI→Keychain 管理的数据密钥及认证加密；注册表自启→macOS 登录项；waveIn/waveOut→CoreAudio/AVFoundation；System.Speech→系统朗读；user32→Mac 平台接口。Windows 密钥不能在 Mac 解密，迁移时明确要求重新录入，不把密钥写成明文或送到命令行参数。

## 系统边界与权限

数据默认保存在 `~/Library/Application Support/DeskPet`，日志保存在 `~/Library/Logs/DeskPet`；应用包内不写用户数据。支持环境变量指定隔离测试目录。日志禁止记录 Key、Authorization、音频和完整隐私对话。

只有点击听写或 Live 才申请麦克风权限；拒绝、撤销、没有输入设备时保持可用的文字功能。不要求屏幕录制权限。能通过系统快捷键接口实现的功能不申请辅助功能权限；若跨应用检测需要额外授权，必须明确说明并提供关闭能力。

两个 CPU 架构独立出包，不把两个完整 .NET publish 目录简单混拼为 Universal。系统最低版本、程序集版本、Info.plist 与附件版本统一来源于版本配置；预览包显著标记 unsigned，不能冒充正式版。

## 打包与正式发布门禁

1. 锁定 SDK、NuGet 依赖及恢复锁文件；运行状态机/协议/资源/设置/队列测试。
2. 在对应 macOS runner 上发布 `osx-arm64`、`osx-x64` 自包含程序；创建标准 `.app`，检查执行权限、动态库架构、资源及 Info.plist。
3. 验证版本、应用标识、图标、麦克风用途说明、entitlements 和依赖完整性。
4. 用 Developer ID Application 从内部二进制到外层 app 签名，使用 Hardened Runtime 和安全时间戳；不依赖 `codesign --deep` 自动签名。
5. 使用 `notarytool` 提交并检查 Accepted；保存可审计日志但不打印凭据；给 app staple 并 validate。
6. 制作 DMG 和 ZIP；签名/公证 DMG，挂载检查内容和 Applications 链接；导出 SHA256 与构建元数据。
7. Gatekeeper / 签名 / 公证 / 安装后启动均通过；两架构功能验收都有证据，才允许称为“随时能打包发布”。
8. 正式工作流默认不发布 Release；创建/上传公开发行物应在制品准备及验收后操作。

签名材料通过本地 Keychain 或 GitHub Secrets 配置，仓库只记录变量名与配置步骤。缺证书、缺公证凭据、任一架构测试失败、功能矩阵未完成时，正式构建应失败，不能降级为 unsigned 后仍报告成功。Apple Developer 账号及证书可能涉及付费，不能由脚本自动创建/购买。

## 当前验证条件与完成标准

当前主机只有 Windows，已装 .NET 8 运行时、没有 .NET SDK，也没有 Xcode。可在工作区安装独立 .NET 10 SDK进行构建与核心测试；无需 Docker、管理员权限或更改系统默认 SDK。Mac 原生运行、麦克风、登录项、多屏、签名和 Gatekeeper 必须在 Mac 或真实 macOS runner 验证。

因此“项目能交叉编译”“Mac CI 编译通过”“完整功能实测”“签名公证完成”“可公开发布”应分别记录。不能仅因生成 DMG 脚本就宣称任务已达到最终发布标准。

## 核对依据（2026-10-16）

- [Avalonia 12.1.4 正式发布](https://github.com/AvaloniaUI/Avalonia/releases/tag/12.1.4)
- [Avalonia macOS 部署](https://docs.avaloniaui.net/docs/deployment/macos)
- [.NET macOS 支持范围](https://learn.microsoft.com/en-us/dotnet/core/install/macos)
- [Apple 公证要求](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)
- [GitHub runner 架构与标签](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
