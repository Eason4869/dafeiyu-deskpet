<div align="center">
  <img src="previews/logo.png" width="88" alt="桌面伙伴 Logo">
  <h1>桌面伙伴 · DeskPet</h1>
  <p>四位桌面伙伴，陪你工作，也陪你闲聊。</p>
  <p>原画动作与互动 · 多服务商 AI 对话 · 听写、朗读与 Live</p>
  <p><a href="https://github.com/Eason4869/deskpet/releases/latest">下载 Windows 版</a> · <a href="#开始使用">开始使用</a> · <a href="docs/dialogue-setup.md">模型配置指南</a> · <a href="CHANGELOG.md">更新日志</a></p>
  <p>当前版本 <a href="https://github.com/Eason4869/deskpet/releases/tag/v0.5.4">0.5.4</a> · Windows 10/11 x64 · .NET Framework 4.8</p>
</div>

## 选一位伙伴

无需配置 AI，就能把伙伴放在桌面上：看向鼠标、偶尔散步，接受摸头和投喂，也会打招呼、跳跃和小憩。

<table>
  <tr><th>E包</th><th>大肥鱼</th><th>马尔芙</th><th>小贝</th></tr>
  <tr>
    <td align="center"><img src="previews/ebao-running.gif" width="180" alt="E包原画跑动预览"></td>
    <td align="center"><img src="previews/dafeiyu-running.gif" width="180" alt="大肥鱼原画跑动预览"></td>
    <td align="center"><img src="previews/marfu-running.gif" width="180" alt="马尔芙原画跑动预览"></td>
    <td align="center"><img src="previews/xiaobei-running.gif" width="180" alt="小贝原画跑动预览"></td>
  </tr>
  <tr><td align="center">傲娇毒舌的白发正太</td><td align="center">温和俏皮的鲸鱼娘</td><td align="center">聪明俏皮的魔法少女</td><td align="center">机灵温柔的科技少年</td></tr>
</table>

首次启动默认 E包。四位伙伴各有独立的人格、聊天记录、待发送队列与音色偏好。你可以编辑人格，为每位伙伴选择不同的声音，或导入自己制作的外观。

## 看看能做什么

### 随时聊两句，也能把想法慢慢展开

双击伙伴打开头顶快速对话，点展开图标进入完整聊天。两个窗口共享会话，支持流式回复、停止生成、历史记录与 Markdown 导出。

<img src="previews/conversation.png" width="1000" alt="0.5.4 完整聊天与快速对话：共享回复、待发送队列和右下角模型选择">

回复还没结束时，也可以继续发送：消息进入队列，按顺序处理。拖动六点柄调整顺序，还能编辑、删除，或用“引导”优先处理新要求。停止或出错后队列暂停，重启后可手动继续。

### 打个招呼，或直接开口说

中键动作轮盘提供喂零食、小憩和专属动作，也可切换基础动作。想开口聊天时，可以选择“听写 → 文字回复 → 朗读”，或使用 Gemini Live 连续通话。

<table>
  <tr><th>桌面互动</th><th>Live 实时语音</th></tr>
  <tr>
    <td align="center"><img src="previews/action-palette.png" width="360" alt="马尔芙动作轮盘：喂零食、专属动作和小憩，打开时没有固定高亮"></td>
    <td align="center"><img src="previews/live-call.png" width="360" alt="E包 Live 通话界面：音量波形、字幕、结束通话与返回文字对话"></td>
  </tr>
</table>

*截图来自 0.5.4 实际界面；对话内容、候选模型与通话状态使用演示数据。*

## 开始使用

1. **下载安装。**在 [Release](https://github.com/Eason4869/deskpet/releases/tag/v0.5.4) 下载 `DeskPet-Setup-0.5.4.exe`，按中文向导安装，无需管理员权限。免安装版下载 `DeskPet-Windows-0.5.4.zip`，完整解压后运行“桌面伙伴.exe”。
2. **认识伙伴。**首次启动会打开 16 步配置引导，带你切换伙伴、编辑人格、拖动和互动，再配置可选的 AI 与语音服务。
3. **按需接入 AI。**只想使用桌宠可以跳过模型与语音步骤；想聊天时，按下方流程添加服务商和模型。

需要 .NET Framework 4.8，无需安装 Node.js。当前仅提供 Windows 安装包，macOS 将在后续版本推进。

### 配置引导就在真实设置页里

每一步只开放当前需要的区域。文字模型步骤支持完整的厂商选择、获取列表和多选添加；实际添加并选中模型后，“下一步”才会启用，并保存当前页。

<img src="previews/onboarding.png" width="1100" alt="0.5.4 配置引导第9步：模型管理页面与引导卡片，展示新增厂商、填Key、获取列表和多选添加的流程">

“跳过这一步”进入下一项，“跳过引导”结束整个流程。已有配置升级后不会自动弹出；可从设置左下角的“配置引导”重新开始。引导期间暂停朗读与 Live 测试，完成或退出后在对应页面测试。

## 接入模型与语音

右键伙伴打开“设置”，进入 **AI 配置**。文字、语音识别、声音与朗读、Live 各自管理服务商，分别保存地址、Key 和代理；切换页面会保留未保存的填写内容。

### 文字模型：先选厂商，再添加模型

1. 点击 **新增**，选择 DeepSeek、MiMo、MiniMax 等厂商，或选择自定义接口。
2. 确认服务地址，填入自己的 **API Key**，点击 **保存并获取模型列表**。
3. 勾选一个或多个模型，点击 **添加所选模型**，选中要使用的模型，最后点击 **保存配置**。

<img src="previews/provider-picker.png" width="520" alt="新增服务商选择器：各厂商Logo，小米MiMo使用黑色小米标志">

<img src="previews/connection-settings.png" width="1000" alt="0.5.4 文字模型管理：厂商Logo侧栏、连接设置、模型中文名称与真实ID、当前选择标记">

已保存且配置有效的文字服务，在进入页面后可自动获取候选列表，也可手动刷新。支持按模型 ID／中文名搜索；获取失败保留已添加模型，仍可手填 ID 和中文别名。候选列表不会自动全部加入配置。

<details>
<summary>查看多选添加模型的界面</summary>

<img src="previews/models-select.png" width="1000" alt="候选模型多选列表和添加所选模型按钮">

</details>

支持 **OpenAI Chat Completions** 与 **Anthropic** 文字协议。输入框右下角可按服务商切换已配置模型，悬停查看真实 ID；切换从下一条请求生效。模型是否支持文字对话，以服务商能力与账号权限为准。

### 语音服务：预填模型，分别测试

| 页面 | 配置方式 | 使用入口 |
| --- | --- | --- |
| 语音识别 | 选厂商后预填可编辑的模型名，填写独立 Key | 输入框的麦克风按钮 |
| 声音与朗读 | Windows 系统语音，或云端 TTS、音色与音量 | 本页“测试朗读”；对话头像旁的喇叭开关 |
| Live 实时语音 | Gemini Live、独立 Key 与 Live 音色 | 本页“测试 Live 通话”；输入框空闲且为空时的通话按钮 |

<table>
  <tr><th>声音与朗读</th><th>Live 实时语音</th></tr>
  <tr>
    <td><img src="previews/speech-settings.png" width="500" alt="朗读页：MiMo厂商、音色、音量和页面内的测试朗读按钮"></td>
    <td><img src="previews/live-settings.png" width="500" alt="Live页：Gemini厂商、Live音色和独立的测试Live通话按钮"></td>
  </tr>
</table>

语音页直接使用可编辑的模型名，不获取模型列表。TTS 与 Live 音色按伙伴和服务模型分别保存，支持下拉选择和手填 Voice ID。音量默认 50%，修改后立即生效并保存。

<details>
<summary>查看语音识别的配置界面</summary>

<img src="previews/asr-settings.png" width="1000" alt="语音识别页：MiMo厂商、独立Key和预填的可编辑识别模型名">

</details>

听写时，停止按钮将识别结果填入草稿，发送按钮识别后发送或排队。Live 连接成功后才打开麦克风，挂断或关闭窗口会停止收音并返回原页面。两种语音方式的配置与测试相互独立。

详细步骤见 [对话与语音配置](docs/dialogue-setup.md) · [音色预设](docs/voice-presets.md)。

## 日常陪伴与操作

| 操作 | 会发生什么 |
| --- | --- |
| 拖动伙伴，再松开 | 拎起、移动和落地 |
| 单击头部／身体 | 摸摸头／戳一戳 |
| 鼠标移入伙伴 | 空闲时跳跃一次；悬停期间暂停视线跟随 |
| 鼠标中键 | 打开动作轮盘，悬停后选择技能或动作 |
| 双击伙伴 | 打开快速对话；Enter 发送，Shift + Enter 换行 |
| 右键伙伴 | 互动、切换外观，或进入设置 |
| 托盘图标 | 显示／隐藏伙伴、暂停、关闭鼠标穿透或退出 |

**安静地陪着你。**免打扰会暂停问候、散步和自动朗读；默认在伙伴所在屏幕全屏时隐藏。鼠标穿透让点击落到下方应用，锁定位置可禁止拖动和散步。自启默认关闭，可在“伙伴 → 陪伴方式”开启。

**偶尔主动打个招呼。**默认每 30–60 分钟显示一条角色预设问候，不消耗模型额度。可关闭或调整间隔；开启“使用 AI 生成主动问候”后，会结合当前伙伴最近的聊天生成，失败时回退预设。问候不自动朗读，也不加入历史。

<img src="previews/greeting.png" width="320" alt="E包主动问候气泡，点击聊两句进入快速对话">

<details>
<summary>默认快捷键、聊天记录与数据</summary>

| 快捷键 | 功能 |
| --- | --- |
| Ctrl + Alt + Space | 唤出快速对话 |
| Ctrl + Alt + H | 显示／隐藏伙伴 |
| Ctrl + Alt + D | 开关免打扰 |
| Ctrl + Alt + P | 开关整体鼠标穿透 |

快捷键可在设置中编辑或禁用，冲突时会提示。聊天记录默认按伙伴保存，最多最近 200 轮／8 MiB，以先到的上限为准。配置、密钥、记录和导入外观保存在程序旁的 `data`；密钥与本机聊天记录使用当前 Windows 用户加密。导出的 Markdown 为普通可读文件，可自行备份 `data`。

启动不会发起 AI 对话或打开麦克风；进入已有效配置的文字模型页面时会请求模型列表。使用云端 AI 或语音时，相应内容会发送给你选择的服务商。

</details>

更多操作见 [日常使用](docs/daily-use.md)。升级可在“设置 → 关于”检查并更新，自动校验下载内容并在空闲时安装；重启前可取消。安装包暂未签名，发布页提供 SHA256。详见 [安装、升级与备份](docs/installation.md)。

## 制作自己的桌宠

在 [Release](https://github.com/Eason4869/deskpet/releases/tag/v0.5.4) 下载四位伙伴的高清外观包，解压后在“伙伴与人格”页导入 `pet.json`。也可下载 `DeskPet-Skin-Template-v2-0.5.4.zip`，从完整创作模板开始。

- **v1**：8 列 × 11 行透明 PNG，高清模板为 3840 × 5720，单帧 480 × 520。
- **v2**：按动作分别绘制 PNG，自定义帧数、播放速率和循环；提供 17 组动作与 16 方向注视模板。
- 在 `pet.json` 填写角色名称、简介、人格和专属动作名称，随外观一起导入。

详见 [外观格式](docs/skin-format.md) · [模板说明](templates/README.md)。

## 接下来的计划

- [ ] macOS 版本。
- [ ] 每位伙伴独立的好感度与互动反馈。
- [ ] 拖动放下后的概率踉跄动作。
- [ ] 节日限定服饰与动作。

以上尚未实现，具体规则见 [开发路线图](docs/roadmap.md)。

---

作者：**彧晟Eason** · [反馈问题](https://github.com/Eason4869/deskpet/issues) · [素材与协议来源](docs/sources.md)
