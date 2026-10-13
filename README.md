<div align="center">
  <img src="previews/logo.png" width="96" alt="桌面伙伴 LOGO">
  <h1>桌面伙伴 · DeskPet</h1>
  <p>把 E包放在桌面，偶尔问候，随时聊代码、设计和想法。</p>
  <p><a href="https://github.com/Eason4869/deskpet/releases/latest">下载安装包</a> · <a href="CHANGELOG.md">更新日志</a> · <a href="https://github.com/Eason4869/deskpet/issues">反馈问题</a></p>
  <p>Windows 10/11 x64 · .NET Framework 4.8 · 作者：彧晟Eason</p>
</div>

**dev 开发版：0.3.2**，仅提供源码与 `v0.3.2` tag，本次不发布 Release。下方下载链接仍指向现有稳定版；体验新功能请从 dev 构建。

## 安装

下载并运行 **DeskPet-Setup-0.3.1.exe**，按中文向导安装，无需管理员权限。也可下载 **DeskPet-Windows-0.3.1.zip**，完整解压后运行 **桌面伙伴.exe**。

首次启动默认 **E包**，第二套内置形象是 **大肥鱼**。已有用户保留原先选择；无需接入 AI 就能使用桌宠。

![使用流程示意图](previews/workflow.svg)

## 使用

| 操作 | 功能 |
| --- | --- |
| 左键拖动 / 单击角色 | 移动 / 打招呼 |
| 双击角色 | 打开头顶快速对话；Enter 发送，Shift + Enter 换行 |
| 气泡中点击“展开聊天” | 打开完整对话，共享当前伙伴的会话 |
| 对话界面的“TTS 输出” | 开关自动朗读；与气泡和设置同步，关闭立即停止朗读 |
| 设置 → 伙伴与外观 / 人格 | 切换形象和对应人格，导入自己的桌宠 |
| 设置 → 伙伴与外观 | 调节大小、散步、置顶、开启自启（默认关闭） |
| 设置 → 伙伴与外观 → 主动问候 | 默认每 30–60 分钟打招呼，可关闭、调节时间及预览；气泡 12 秒后收起 |
| 右键菜单外点击空白 | 立即关闭菜单 |
| 托盘“显示 / 隐藏伙伴” | 勾选表示角色可见 |
| 设置 → 关于与更新 | 启动自动检查或手动“检查并更新”，下载校验后空闲时自动安装、重启；支持取消 |

![实际对话界面](previews/chat-window.png)

<img src="previews/greeting.png" width="320" alt="E包主动问候气泡，点击聊两句进入快速对话">
<img src="previews/update-notice.png" width="340" alt="后台更新倒计时，可取消本次安装与重启">

*截图回复来自本地模拟服务。每位伙伴的最近 10 轮成功会话分别保存在内存，退出后不保留。*

## 配置 AI 与语音

设置中选择服务预设，填入地址、模型和自己的密钥，点击“保存服务设置”。支持 **OpenAI** 与 **Anthropic** 文字协议；内置 OpenAI、Gemini、DeepSeek、MiMo、火山、Kimi、MiniMax、智谱、千问等填表示例。

- **ASR + TTS**：点击说话 → 结束并发送 → 识别 → 模型回复 → 朗读。支持 MiMo / OpenAI 音频接口，也可使用 Windows 系统朗读。
- **Gemini Live**：单独配置 Live 服务，在聊天中切换为 Live，点击开始；持续语音、字幕和打断，再次点击结束。TTS 开关控制分步朗读，Live 音频随 Live 会话输出。
- 每项服务有独立代理开关，默认地址 **127.0.0.1:7890**，可编辑。音量默认 **50%**，立即生效并保存。

启动不会请求模型或打开麦克风。密钥使用当前 Windows 用户 DPAPI 加密，配置与导入外观保存在程序旁的 `data`。真实模型权限、网络可达性和声音质量由所用服务决定；安装包暂未签名。

主动问候使用本地台词，不消耗模型额度、不自动朗读，也不加入聊天历史。聊天、语音、全屏、暂停或隐藏期间保持安静；恢复后重新等待。自动更新保留 `data`，对话窗口打开或设置草稿未保存时等待；重启前显示 5 秒倒计时气泡，可直接取消。更新只读取正式 Release，dev tag 本身不会触发升级。

详见 [对话配置](docs/dialogue-setup.md)。

## 自定义外观

下载 Release 中的高清外观包，解压后在设置中导入 `pet.json`。制作自己的桌宠可从程序旁 **templates/pet.json** 开始；模板、Schema 和动作网格图都在 **templates** 文件夹。

每套内置形象包含 73 帧，单帧 480×520，透明图 3840×5720。模板可设置 `system_prompt`，切换形象同时切换人格。

详见 [外观格式](docs/skin-format.md) 和 [模板说明](templates/README.md)。

## 构建与升级

源码使用 C# / WPF / .NET Framework 4.8；打包需要 Python + Pillow，安装器需要 Inno Setup 6.7+。

```powershell
.\Build.ps1 -Target App
python .\Package.py
.\BuildInstaller.ps1 -Compiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

产物位于上一级 `outputs`。已有 1.3.x 用户请手动安装 0.3.1 完成版本编号调整；安装升级保留个人 `data`，并迁移本安装位置已开启的自启。免安装版更换 EXE 名称后需重新开启自启。

[安装说明](docs/installation.md) · [验证范围](docs/verification.md) · [素材与协议来源](docs/sources.md)
