# DeskPet 内置 Agent 接入计划

日期：2026-10-16。状态：方案草案，尚未实现、未安装任何 agent、未调用真实模型。

**目标：**让当前桌宠同时具备桌面助手、编程助手、陪伴助手能力，优先嵌入现成开源 agent。

**架构：**首选验证 OpenCode 后台进程，由现有 C# 界面通过 HTTP/SSE 调用；复用上游的模型适配、工具循环、会话压缩与权限机制。DeskPet 负责伙伴人格、交互界面、应用生命周期，以及桌宠专用的记忆、提醒和互动能力。

**技术栈：**保留 Windows / WPF / .NET Framework 4.8 / PowerShell 构建；agent 使用独立运行时。无需 Docker，不把 WSL 作为产品前提。

**执行说明：**后续实现按本文任务顺序推进；当前只交付计划。下述接口与新增文件均为拟定方案，P0 验证结果可调整适配细节。

## 1. 当前代码与可复用部分

基线提交为 `c94898a`，当前工作区另有尚未提交的互动、动画等变更。实施时须保留这些变更，并先重新核对工作区。

| 已有模块 | 当前情况 | 接入方式 |
| --- | --- | --- |
| `src/ApiClient.cs`、`src/ChatStream.cs` | OpenAI Chat Completions / Anthropic 文字协议；返回文字，没有 agent 工具事件 | 保留普通聊天；新增 agent 适配器，不在此重写工具循环 |
| `src/ConversationSession.cs` | 会话、取消、流式回复、ASR/TTS、按伙伴保存历史 | 在文字回复位置分流到普通聊天或 agent；保留共同的生命周期 |
| `src/ConversationQueue.cs` | 排队、暂停、重排、引导；失败会把消息重新放回队列 | 复用入口；agent 失败不能直接重放已产生副作用的任务 |
| `src/ConversationComposer.cs`、`src/ChatWindow.cs`、`src/QuickChatWindow.cs` | 快速与完整聊天共享会话 | 增加模式、工作目录、执行状态与审批展示 |
| `src/Settings.cs` | 多服务商、模型、独立代理、DPAPI 密钥保存 | 转换选中连接的配置；不要求用户重复填 Key |
| `src/GreetingService.cs` 的 `CompanionMemory` | 取当前伙伴最近最多 4 个完整对话轮次 | 保留问候入口，扩充为可管理的长期记忆 |
| `src/HistoryStore.cs` | 按伙伴、DPAPI 加密聊天记录 | 继续保存展示历史；工具日志与上游原生会话单独管理 |
| `src/PetWindow.cs`、`src/UpdateCoordinator.cs` | 对话忙碌状态影响切换伙伴、Live、互动、退出与更新 | agent 运行、等待确认均纳入忙碌状态 |

## 2. 开源选型

以下能力来自本次查阅的上游仓库与文档；接入成本为针对本项目的判断，不代表已经本机运行通过。

| 候选 | 已确认的复用入口 | 本项目取舍 |
| --- | --- | --- |
| **[OpenCode](https://github.com/anomalyco/opencode)**，MIT | headless server、HTTP/SSE、会话取消、权限应答、多服务商、MCP | **优先验证。**C# 可直接调用后台服务，编程能力与工具基础较完整 |
| **[Goose](https://github.com/aaif-goose/goose)**，Apache-2.0 | 通用本地 agent、Windows、API、MCP、自定义发行版 | 首选备选；通用助手定位契合，须额外验证后台协议与打包适配 |
| **[Pi](https://github.com/earendil-works/pi)**，MIT | RPC、TypeScript SDK、扩展、Skills；Windows 可用 PowerShell 工具 | 嵌入接口清晰；当前文档要求 Node.js 22.19+，审批和桌面扩展的工作量需验证 |

推荐 OpenCode 是集成判断：现有 WPF 客户端最容易消费其服务接口。它的 Windows 文档仍推荐 WSL，因此**原生 Windows 无界面运行必须先验证**，不能据文档直接承诺无额外依赖。

Pi 的旧地址 `badlogic/pi-mono` 已跳转到 `earendil-works/pi`，Goose 的旧地址 `block/goose` 已跳转到 `aaif-goose/goose`。后续下载、锁版本均采用核实后的上游地址。

不同时维护三套引擎：先完成 OpenCode P0；若原生 Windows、配置隔离或权限交互有无法接受的问题，再用同一套验收题验证 Goose。只有两者均不适合时才评估 Pi。

## 3. 功能范围与用户入口

三类能力都在交付范围内，分阶段实现；前两个阶段只能标为预览，完成 P5 才算覆盖“都要”。

| 模式 | 用户能做什么 | 工具范围 |
| --- | --- | --- |
| 陪伴 | 日常聊天、记住明确偏好、创建提醒、主动问候 | 默认无 shell / 项目写入；记忆与提醒使用桌宠专用工具 |
| 桌面助手 | 查资料、总结网页、查找及整理用户选定目录中的文件、打开指定网页或文件夹 | 上游检索/文件工具 + 少量 Windows 专用工具 |
| 编程助手 | 选择项目、读代码、分析报错、修改文件、运行构建/测试、查看改动 | 上游代码与命令工具；显示工作目录和实际命令 |

完整聊天输入区增加模式选择和工作目录。快速聊天显示简要进度、停止和“展开处理”；复杂审批在完整聊天中处理，任务不会静默卡住。默认仍从陪伴聊天开始，用户选择助手模式后才开放相应工具。

已有 ASR 可把识别文本交给所选模式；TTS 只读最终自然语言答复。Gemini Live 初期继续走现有链路，不隐含具备 agent 工具能力；Live 中的任务需显式转到文字助手。

浏览器点击、任意软件 UI 操作、持续截屏不作为首批桌面助手能力；后续可评估现成 Windows 自动化 MCP，单独验收权限、兼容性与画面数据处理。

## 4. 接入架构与数据规则

```text
快速聊天 / 完整聊天 / ASR
            │
ConversationQueue → ConversationSession
                          ├─ 普通聊天 → 现有 ApiClient → TTS
                          └─ Agent → OpenCode 适配器 → 后台进程
                                         │              ├─ 上游模型与代码工具
                                         │              ├─ 检索 / MCP / Skills
                                         │              └─ DeskPet 专用 MCP
                                         └─ 文字增量 / 工具进度 / 审批 / 完成事件

DeskPet 专用 MCP → 伙伴记忆 / 本地提醒 / 桌宠动作 / 打开指定目标
```

1. **进程管理。**按需启动 DeskPet 自己的运行时，隐藏控制台；只监听 `127.0.0.1`，启用每次启动随机口令。客户端本地连接禁用代理，选择端口须处理冲突，不能接管用户已有 OpenCode 进程。
2. **最小运行时。**仅打包所需后台运行组件和许可证，不嵌入上游整个桌面客户端。锁定经验证版本与 SHA256；关闭独立自动升级，避免接口在桌宠未更新时改变。
3. **配置隔离。**后台进程的配置、数据、缓存使用 DeskPet 专用目录；禁用未经用户启用的项目插件、全局 MCP 和自动依赖下载。仅设置自定义配置文件路径不足以证明隔离，须实际检查上游合并配置、自动发现与加载行为。
4. **模型与代理。**把选中服务商的 URL、协议、真实模型 ID 和 Key 转换给上游，先验证工具调用能力；“能普通聊天”不等于“能运行 agent”。只向该子进程注入必要凭据，禁止写入命令行或普通配置。采用单一活动任务；连接或代理变化时等待当前任务结束，再重建后台连接环境。
5. **伙伴和项目隔离。**原生 agent 会话按伙伴 ID、模式、规范化工作目录、连接配置指纹及人格版本映射。指纹不保存明文 Key。切换目录、人格或伙伴后不得误续旧任务。
6. **唯一任务状态。**每次任务记录 `runId` 和上游 `sessionId`；按消息/事件 ID 去重。展示历史由 DeskPet 管理，工具上下文和压缩由上游管理，不重复把完整展示历史附加到已存在的原生会话。
7. **取消与故障。**停止先请求上游 abort，再等待终态；事件流断开后先查询状态、补齐消息和审批，不自动重复提交任务。退出或更新要终止自有进程树并刷新本地记录，不能仅关闭 UI 或 HTTP 连接。
8. **数据保护。**Key、DeskPet 新增的记忆/提醒/任务索引沿用 DPAPI。上游原生会话、数据库、日志是否明文必须在 P0 实查；不能把现有 HistoryStore 加密等同于上游数据也加密。无法关闭持久化或提供适用保护时，须在启用前清楚说明存储位置和性质，不宣称“所有记录加密”。

建议安装布局：程序旁 `runtime/opencode/<固定版本>/` 放运行组件，`data/agent/` 放隔离运行数据，`data/companion/` 放桌宠记忆和提醒。实际路径由 `SettingsStore.DirectoryPath` 派生，不写死开发机路径；只读安装目录继续使用既有可写数据位置策略并验证。

### 工具与授权规则

- 复用上游文件、代码修改、命令、检索与 MCP 调度；不自己实现第二套模型工具循环。
- 每个模式明确配置 allow / ask / deny；不沿用上游默认放开的权限。用户明确授权过的同一范围可以继续使用，避免每一步重复确认。
- 项目命令只在用户选择的工作目录运行；展示实际命令和工作目录。工具权限不是操作系统沙箱，授权 shell 后不承诺其绝对无法访问目录外内容。
- 文件改动显示涉及文件；上游审批事件能提供补丁时展示补丁，否则不伪造“执行前完整 diff”。执行后展示实际 diff，并在编辑前保护已有未提交改动。
- 删除、覆盖重要文件、运行未知命令、发送外部消息、提交/推送代码等按具体授权处理。只读问答不因此增加确认弹窗。
- 网页和文件内容当作任务资料，不能借其内容放宽工具权限、修改人格或自动写长期记忆。
- 网络搜索优先用引擎现有能力；若选定服务/检索 MCP 需要额外 Key 或付费，单独说明并可关闭。读取已知 URL 与搜索互联网分别验收。
- 长任务会消耗多轮模型额度；显示实际可获取的 token / 用量，缺失时标记未知，不估造费用。首版建议限制每任务 20 次工具调用、10 分钟活动执行时间；等待用户确认期间暂停计时，达到限制保留进度并由用户决定继续。

### 桌宠专用工具边界

通过小型 `DeskPet.Tools.exe` 提供 MCP stdio 入口，复用成熟 MCP 库或上游实现。若兼容 .NET Framework 4.8 的库不足，则使用自带运行时的独立桥接程序，不手写整套 MCP 协议。

桥接程序通过带当前用户访问控制的命名管道调用 WPF 主进程；暴露固定工具，不允许任意 C#、任意程序集或任意进程调用。工具拟定为：`memory.search`、`memory.propose`、`reminder.create/list/cancel`、`pet.play_action`、`desktop.open_target`。MCP 对外工具名按锁定版本允许的命名规则转换。

长期记忆按伙伴分开，可查看、编辑、删除；仅用户明确要求记住的内容直接保存，模型推断先形成待确认建议。首版按标签和关键词检索，不引入向量数据库服务；关键信息带来源、时间和状态。

提醒保存明确本地时间、时区、唯一 ID 和触发状态；时间含糊先澄清。触发由本地调度器完成，无需每次请求模型。遵守免打扰、锁屏与全屏策略，延迟提醒按 ID 去重。桌宠退出后不常驻服务，到下次启动只提示遗漏提醒，不补跑文件或命令任务。

## 5. 分阶段实施

### P0：验证开源引擎能否直接使用

**交付：**`docs/agent-engine-evaluation.md`、`tools/agent-smoke.ps1`；验证产物仅放 `work/agent-probe/`。

- [ ] 获取并锁定 OpenCode Windows 发行版、来源、SHA256、许可证；以该版本的 `/doc` 为接口依据，避免混用 dev 文档与稳定版。
- [ ] 在隔离目录验证后台启动、鉴权、健康检查、普通回复、工具调用、审批拒绝/允许、停止和退出；覆盖中文及带空格路径。
- [ ] 用本地可控模型协议夹具验证事件映射和故障；经用户现有配置验证一次真实模型端到端能力，未实测项明确列出。
- [ ] 验证无 Node.js、无 Docker、无 WSL 的最小用户环境能否运行；额外 shell/工具依赖按实际发现记录，PowerShell 优先。
- [ ] 检查凭据与会话实际落盘、配置/插件自动发现、代理、冷启动网络访问、包体大小、空闲和运行内存。
- [ ] Windows 原生执行或安全隔离不满足要求时，针对 Goose 做同题验证；形成选型记录后再实现适配器。

**通过标准：**真实 WPF/C# 客户端能获得文本和工具事件；拒绝操作未执行；取消后不再继续调用工具；清理后无自有残留进程。未通过前不承诺内置运行时免依赖。

### P1：接入现有会话与运行时生命周期

**新增：**`src/AgentContracts.cs`、`src/OpenCodeClient.cs`、`src/AgentRuntime.cs`、`src/AgentSessionStore.cs`。

**修改：**`src/ConversationSession.cs`、`src/ConversationQueue.cs`、`src/Settings.cs`、`src/PetWindow.cs`、`Build.ps1`。

**拟定接口：**`IAgentBackend.RunAsync(AgentRequest, IProgress<AgentEvent>, CancellationToken) : Task<AgentResult>`；`AbortAsync(string runId) : Task`。事件至少包含文字增量、工具开始/完成、权限请求、用户问题、用量、终态；请求绑定伙伴、模式、目录、连接快照和 runId。

- [ ] 先增加 `AgentRuntimeTests`：启动失败、端口占用、随机口令、父进程退出与进程树回收。
- [ ] 增加 `AgentSessionTests`：按伙伴/目录隔离，模型切换仅影响下一任务，重复事件不重复记账，断流不重发任务。
- [ ] 接入统一忙碌、停止、队列暂停、退出和更新流程；agent 待审批也不能被当成空闲自动升级。
- [ ] 队列消息固定模式和目录；原队列文件迁移后默认普通陪伴聊天。失败任务明确标记待处理，禁止沿用普通聊天的无条件重新发送。
- [ ] 普通聊天的现有 API、ASR/TTS、Live 保持原行为；agent 不可用时只回退无副作用的纯对话，已执行任务不静默重跑。

**验收：**运行 `AgentRuntimeTests`、`AgentSessionTests`，并回归 `ConversationTests`、`HistoryTests`、`QueueTests`、`StreamingTests`、`SteeringSmoke`、`ProviderTests`。运行方式统一为 `powershell -NoProfile -File .\Build.ps1 -Target <测试目标>`；新增目标须先注册至构建脚本。

### P2：完成编程助手与执行界面

**新增：**`src/AgentTaskPanel.cs`、`src/AgentApprovalPanel.cs`。

**修改：**`src/ConversationComposer.cs`、`src/ChatWindow.cs`、`src/QuickChatWindow.cs`、`src/MessagePresentation.cs`。

- [ ] 增加模式/目录选择、正在读取/执行/等待确认状态、可折叠工具结果、审批与用户问题应答、最终修改摘要。
- [ ] 直接使用上游项目读取、编辑、构建/测试工具；不重新实现代码检索和补丁算法。
- [ ] 确认和拒绝绑定 runId、会话、请求 ID；任务取消后审批按钮失效，迟到回包不影响下一任务。
- [ ] “引导”先安全停止当前任务，再带上新要求和已完成步骤继续；不能当作一条完全新指令重复先前写入。
- [ ] `AgentPermissionTests` 覆盖未知工具、目录越界请求、取消与确认竞争；`AgentUiSmoke` 覆盖快速窗口展开、工具卡片、审批、拒绝及长输出。

**验收题：**在临时项目中完成“定位一个失败测试 → 修改文件 → 执行测试 → 展示实际改动”，并验证预先存在的用户改动未被回滚。

### P3：完成桌面助手与扩展接入

**新增：**`src/CompanionToolHost.cs`、`src/DesktopAgentTools.cs`、`src/AgentToolsProgram.cs`；桥接程序及依赖的构建位置由 P0 结果确定。

**修改：**`Build.ps1`，以及 P0 确认所需的 agent 配置模板。

- [ ] 注册可控的 DeskPet MCP；加入命名管道鉴权、参数校验、主线程调度和取消。
- [ ] 提供打开指定网页/文件夹、请求播放已支持动作；动作服从暂停、隐藏和免打扰，不修改人格或系统设置。
- [ ] 先复用上游网页读取与搜索；只有实际缺能力时再接一个维护中的检索 MCP，并锁定版本与来源。
- [ ] 提供 MCP 的启停与状态，不默认执行项目带入的第三方扩展；聊天展示资料来源链接。
- [ ] `DesktopAgentTests` 验证授权目录内的文件任务、非法 URI、命名管道错误、网络失败、重复工具请求和隐藏状态下的动作处理。

**验收题：**总结一个指定网页并保留来源；在用户选择的临时目录中查找和归类文件；拒绝操作后文件保持原状。

### P4：完成陪伴记忆和提醒

**新增：**`src/CompanionMemoryStore.cs`、`src/ReminderStore.cs`、`src/ReminderService.cs`、`src/CompanionMemoryPanel.cs`。

**修改：**`src/GreetingService.cs`、`src/CompanionToolHost.cs`、`src/PetWindow.cs`、`src/SettingsWindow.cs`。

- [ ] 将现有 `CompanionMemory` 接到按伙伴保存的长期记忆；保留近期完整对话作为上下文来源，失败时仍使用原预设问候。
- [ ] 实现记忆查看/删除/修改和明确记忆工具；网页与代码内容不能自动成为用户偏好。
- [ ] 实现提醒创建、列表、取消、本地触发及漏过提醒；删除聊天与删除长期记忆分别操作，并解释影响范围。
- [ ] 主动互动优先由本地规则触发；AI 生成仍沿用用户现有开关和间隔，不自动增加持续后台模型请求。
- [ ] `CompanionMemoryTests`、`ReminderTests` 覆盖四宠隔离、加密文件、遗忘后不再召回、跨日/重启/改时区、免打扰延迟与重复触发。

**验收题：**让 E包记住明确偏好，重启后能召回且其他伙伴不会继承；删除后无法召回；创建短时提醒只触发一次，退出期间到期的提醒在下次启动提示遗漏。

### P5：设置、打包与最终回归

**新增：**`src/AgentConfigurationPanel.cs`、`docs/agent-setup.md`、运行时来源/校验清单和第三方声明。

**修改：**`src/SettingsWindow.cs`、`src/Settings.cs`、`src/OnboardingWindow.cs`、`Build.ps1`、`BuildInstaller.ps1`、`Package.py`、`README.md`、`CHANGELOG.md`、`docs/verification.md`。

- [ ] 设置页支持开关、运行时状态、复用/单独选择现有模型、权限、MCP、数据目录与清理；变更计入未保存状态并处理旧配置迁移。
- [ ] 运行时随完整安装包交付；提供关闭 agent 后继续使用桌宠和普通聊天的路径。安装/升级失败保留旧版组件，卸载不误删用户数据。
- [ ] `AgentPackagingSmoke` 验证固定版本、SHA、所需依赖、许可证、数据排除、升级回退及无系统级安装前提。
- [ ] 在干净 Windows 环境检查无控制台闪现、代理与中文路径、退出无残留、自有数据隔离，并记录启动耗时与资源占用。
- [ ] 回归 `CoreTests`、`ProfileTests`、`SettingsNavigationSmoke`、`ComposerSmoke`、`AiGreetingTests`、`AiGreetingUiSmoke`、`InteractionTests`、`LiveCallSmoke` 和更新相关测试。
- [ ] 按 `docs/verification.md` 要求，在最终推送/发布前检查当前构建的完整配置引导；界面测试、真实模型、真实语音、打包安装分别记录，未实测不记作通过。

**完成标准：**P2 编程题、P3 桌面题、P4 陪伴题在最终同一构建中通过；用户无需手动运行外部 agent 或重复配置模型。本计划不包含自动提交、推送或发布。

## 6. 顺序、工作量与待验证项

建议按 **P0 → P1 → P2 → P3 → P4 → P5** 串行推进。先获得可执行的内置 agent，再逐步补齐桌面和陪伴能力，避免同时引入多套运行时。

粗估有效开发/验证工作量：P0 1–2 天；P1 2–3 天；P2 2–3 天；P3 2–3 天；P4 3–4 天；P5 2–3 天。合计约 **12–18 个工作日**，这是范围估计，不是交付日期；P0 后按实际兼容性重估。

实施前需要用 P0 证据收敛的事项：

1. **[待确认] 固定运行时版本与接口。**本次网页显示 OpenCode Release 为 v1.19.1；实际采用版本须以下载工件、健康检查和契约测试共同确认。
2. **[待确认] 原生 Windows 依赖。**工具链是否需要 Git Bash，能否用 PowerShell 替代，以及纯用户环境是否可离线启动。
3. **[待确认] 原生存储保护。**会话、数据库、缓存、日志是否能满足既有数据保护预期；无法满足时须明确可接受的存储策略。
4. **[待确认] 现有模型的 agent 能力。**至少选一个用户现有的工具调用模型实测；不能根据服务商名字承诺全部模型兼容。
5. **[待确认] 搜索服务。**确定实际可用的搜索能力、额外账号/费用和失败回退；没有配置搜索时仍可使用指定 URL 与本地项目能力。

## 7. 上游资料

以下均为本次读取的上游仓库或官方文档，查询日期 2026-10-16；后续实现须以固定发行版源码和实际 `/doc` 为准。

- [OpenCode 仓库](https://github.com/anomalyco/opencode) / [MIT License](https://github.com/anomalyco/opencode/blob/dev/LICENSE) / [本次查询的 Release](https://github.com/anomalyco/opencode/releases/tag/v1.19.1)
- [OpenCode Server：HTTP、SSE、会话与权限应答](https://opencode.ai/docs/server/)
- [OpenCode Permissions：按工具限制能力](https://opencode.ai/docs/permissions/)
- [OpenCode Providers：自定义服务商及接口地址](https://opencode.ai/docs/providers/)
- [OpenCode MCP Servers](https://opencode.ai/docs/mcp-servers/)
- [OpenCode Config：配置来源、合并顺序和自动更新](https://opencode.ai/docs/config/)
- [OpenCode Network：代理及本地连接绕过](https://opencode.ai/docs/network/)
- [OpenCode Windows：原生可运行，官方推荐 WSL](https://opencode.ai/docs/windows-wsl/)
- [Goose README：通用 agent、API、MCP](https://github.com/aaif-goose/goose) / [Windows 安装](https://goose-docs.ai/docs/getting-started/installation/) / [定制发行版](https://github.com/aaif-goose/goose/blob/main/CUSTOM_DISTROS.md)
- [Pi README：SDK、RPC 与运行要求](https://github.com/earendil-works/pi/blob/main/packages/coding-agent/README.md) / [Windows 与 PowerShell](https://github.com/earendil-works/pi/blob/main/packages/coding-agent/docs/windows.md)
