# macOS 功能对齐与打包 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完成可验证的 Mac 客户端和两架构发布链路，使功能、安装及发布门禁可以逐项验收。

**Architecture:** 新增 Avalonia 前端并复用 C# 业务。隔离 Windows 专有窗口、加密、音频、自启与更新实现，Mac 通过平台服务接入。现有 Windows 入口与本地预览继续可用。

**Tech Stack:** .NET 10、Avalonia 12.1.4、C#、macOS 原生系统接口、Python 打包工具、GitHub Actions。

**Spec:** [macos-design.md](macos-design.md)，用户于 2026-10-16 确认采用。

## Global Constraints

- 基于 0.5.4；保留当前所有互动改动，不发布 Windows 未验证的新版本。
- macOS 14+；分别构建 osx-arm64、osx-x64；自包含 .NET 运行时。
- 不使用 Docker，不安装系统级工具；Windows SDK 可安装在 `work/dotnet`。
- 不使用商业 XPF，不将 WPF 伪装为跨平台控件。
- 新旧动作每次只随机一次；拎起与落地配对，四种落地共用不向上跳的下落曲线；8 FPS。
- 用户数据与密钥不写入应用包；正式包缺签名、公证或任一验收证据时失败。

## Review Focus

- 自定义同名外观不得读取内置动作，损坏图或越界路径应拒绝且保留原伙伴。
- 断网、流式取消、晚到的模型列表、伙伴切换时不可污染另一宠的历史/队列/配置。
- 麦克风拒绝/撤销或设备消失时停止录音并释放资源，文字功能继续可用。
- 重启、睡眠、屏幕变化、跨显示器与暂停不得产生拖放跳跃、忙碌自动睡眠或不可点中的伙伴。
- 缺签名凭据、错误架构、非 Accepted 公证和被修改的制品必须阻止正式发布。

### Task 1: 可跨平台业务与测试

**Files:** `desktop/DeskPet.Core/DeskPet.Core.csproj`、`desktop/DeskPet.Tests`、必要的 `src` 业务类型/接口拆分与 `Build.ps1` 源清单。

**Interfaces:** 保留现有 `Dafeiyu.ConnectionSettings`、`PetSettings`、`ConversationSession`、`ConversationQueue`、`ApiClient`、`GeminiLiveSession`、音频接口及动作规则的公开签名。添加平台加密接口，Windows 默认 DPAPI，Mac 在启动时注册 Keychain 支撑的实现。

- [ ] 先建立可执行的核心测试：配置/历史/队列、OpenAI/Anthropic流式、代理、取消、拖放与睡眠规则；缺业务实现时确实失败。
- [ ] 提取无窗口业务，兼容 System.Web JSON 调用但不引入 Windows 引用；链接共享源文件，避免两份业务实现。
- [ ] 在工作区安装固定 .NET 10 SDK并恢复依赖，编译核心并运行测试。
- [ ] 重跑 Windows 原有相关测试；检查 API Key 与历史不退化为明文保存。

### Task 2: Mac 平台服务

**Files:** `desktop/DeskPet.Platform.Mac`；平台接口放置在 Core，原生桥接源属于 Mac 项目。

**Interfaces:** 实现核心已有录音/PCM输出/朗读契约；提供空闲查询、鼠标位置、窗口策略、全局快捷键、登录启动、凭据保护及数据目录。系统调用只有在 Mac 运行时装配。

- [ ] 写边界测试：非 Mac 不加载原生库；拒绝/缺设备返回可显示错误；路径和参数不能作为 shell 命令拼接。
- [ ] 接入 Keychain 与认证加密，明确 Windows 加密配置不能直接跨机解密。
- [ ] 接入录音、PCM播放、系统朗读、权限状态及资源关闭。
- [ ] 实现快捷键、自启、空闲、屏幕与窗口策略；权限应按需申请。
- [ ] 在 Mac runner 构建原生桥接并运行平台 smoke；不能将 Windows 编译通过写为 Mac 实测通过。

### Task 3: Avalonia 客户端与功能对齐

**Files:** `desktop/DeskPet.Desktop`，引用 Core/Mac 平台项目及仓库现有素材。

**Interfaces:** 使用现有会话、队列、配置、人格与协议；UI 持有单一共享会话，快聊与完整聊天不重复发送。

- [ ] 添加入口、透明桌宠、原画资源读取、原有视线/跑动与最新两套互动播放；时钟延迟不跳帧。
- [ ] 添加原生菜单栏、右键、四格轮盘、两种聊天窗口与队列操作。
- [ ] 添加厂商配置、独立语音/Live设置、人格、音色、存储和自定义皮肤。
- [ ] 接入听写、朗读与Live可视界面、停止/释放与错误显示。
- [ ] 实现 Mac 实际控件的首次/重开引导、单步保存、跳过/恢复与释放锁。
- [ ] UI smoke 覆盖每类入口，功能矩阵各项记录完成/未完成/实测证据，不用空按钮或假成功替代能力。

### Task 4: 两架构打包与发布检查

**Files:** `installer/macos`、`tools` Mac 打包/验证脚本、`.github/workflows/macos.yml`、相关 Python 测试。

**Interfaces:** 发布入口接受目标架构、输出目录、preview/release模式。版本来自 `src/ReleaseInfo.cs`；输出标准 `.app`、`.dmg`、`.zip`、SHA256、构建清单。

- [ ] 为 app布局、plist、版本、权限、架构、资源、符号链接与发布模式写失败用例。
- [ ] 实现两种 RID publish、app bundle 与图标，拒绝错误架构或缺资源。
- [ ] 实现嵌套二进制签名、Hardened Runtime、notarytool Accepted校验、staple/validate与DMG验证。
- [ ] 添加两个真实 macOS runner；preview无凭据可构建，release缺凭据失败。记录运行与制品校验结果。
- [ ] 默认仅上传 workflow artifact，不自动公开发布，不把 unsigned 命名为 release-ready。

### Task 5: 最终验收与交付

**Files:** `docs/macos-readiness.md`、README、installation、roadmap、verification。

- [ ] Windows 原有构建与相关回归通过，本地预览可运行。
- [ ] 核心/Avalonia编译与两RID发布通过；资源和源文件清单正确，无开发机配置/密钥混入。
- [ ] 两架构 Mac 运行、麦克风、登录项、多屏、Gatekeeper、安装后启动与更新保留数据均有实测证据。
- [ ] 签名、公证、最终DMG/ZIP校验均通过后方可报告“随时能打包发布”。
- [ ] 如果缺 Mac 环境或签名材料，保留可运行部分并精确列出阻塞项；不能把未验证项勾选为完成。

## 执行方式

推荐当前会话并行处理 Core、Mac平台、Avalonia UI，父流程负责接口协调、Windows改动、打包及最终验收。若选择顺序执行，则按 Task 1→2→3→4→5推进。现有本地改动未提交；不把用户的其他文档或素材草稿一并提交。
