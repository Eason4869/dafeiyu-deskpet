<div align="center">
  <img src="previews/logo.png" width="96" alt="桌面伙伴 LOGO">
  <h1>桌面伙伴 · DeskPet</h1>
  <p>把 E包放在桌面，偶尔问候，随时聊代码、设计和想法。</p>
  <p><a href="https://github.com/Eason4869/deskpet/releases/latest">下载安装包</a> · <a href="CHANGELOG.md">更新日志</a> · <a href="https://github.com/Eason4869/deskpet/issues">反馈问题</a></p>
  <p>Windows 10/11 x64 · .NET Framework 4.8 · 作者：彧晟Eason</p>
</div>

**当前版本：0.4.0** · 聊天历史保存、流式文字回复、免打扰与全局快捷键。

## 安装

下载并运行 **DeskPet-Setup-0.4.0.exe**，按中文向导安装，无需管理员权限。也可下载 **DeskPet-Windows-0.4.0.zip**，完整解压后运行 **桌面伙伴.exe**。

首次启动默认 **E包**，还可切换 **大肥鱼** 和 **马尔芙**（金发绿眼的魔法少女）。已有用户保留原先选择；无需接入 AI 就能使用桌宠。

<table>
  <tr><th>E包</th><th>大肥鱼</th><th>马尔芙</th></tr>
  <tr>
    <td align="center"><img src="previews/e-bao.png" width="190" alt="E包：白发蓝眸的桌面伙伴"></td>
    <td align="center"><img src="previews/dafeiyu.png" width="190" alt="大肥鱼：蓝白鲸鱼形象的桌面伙伴"></td>
    <td align="center"><img src="previews/marfu.png" width="190" alt="马尔芙：金发绿眼、黑绿魔法袍的桌面伙伴"></td>
  </tr>
  <tr><td align="center">傲娇毒舌 · 白发正太</td><td align="center">温和俏皮 · 蓝白鲸鱼娘</td><td align="center">聪明俏皮 · 魔法少女</td></tr>
</table>

三位伙伴都能切换人格、保存独立记录；展示图来自项目实际角色素材。

<table>
  <tr><td align="center"><img src="previews/ebao-running.gif" width="190" alt="E包实际窗口跑动预览"></td><td align="center"><img src="previews/dafeiyu-running.gif" width="190" alt="大肥鱼实际窗口跑动预览"></td><td align="center"><img src="previews/marfu-running.gif" width="190" alt="马尔芙实际窗口跑动预览"></td></tr>
</table>

跑动预览来自真实 WPF 窗口。马尔芙每步约 0.88 秒，左跑采用清晰步态的镜像原帧；三宠跑动统一为每帧 0.22 秒、完整循环 1.76 秒；大肥鱼左跑朝向也已修正。

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
| 完整聊天 → 加载更早记录 / 导出记录 | 每次加载 50 轮；导出当前伙伴的 Markdown 记录 |
| 设置 → 免打扰与快捷键 | 保存历史、免打扰、全屏隐藏、鼠标穿透、锁定位置与自定义快捷键 |
| 托盘“显示 / 隐藏伙伴” | 控制手动显示意愿；退出全屏不会恢复手动隐藏的角色 |
| 设置 → 关于与更新 | 启动自动检查或手动“检查并更新”，下载校验后空闲时自动安装、重启；支持取消 |

![实际对话界面](previews/chat-window.png)

<img src="previews/greeting.png" width="320" alt="E包主动问候气泡，点击聊两句进入快速对话">
<img src="previews/update-notice.png" width="340" alt="后台更新倒计时，可取消本次安装与重启">

*聊天截图回复来自本地模拟服务。0.4.0 默认按伙伴加密保存历史，重启后可继续查看；模型请求只携带有限的最近成功会话。*

## 历史、流式与免打扰

- **历史保存**：默认开启，每位伙伴最近 200 轮／8 MiB，上限先到者生效。保存在 `data/history`，使用当前 Windows 用户 DPAPI 加密；退出后恢复。关闭保存不删除已有记录，清空删除当前伙伴的磁盘和内存记录。导出 Markdown 是可阅读的普通文件。
- **流式回复**：对话模型设置中默认开启，快速气泡与完整聊天同步显示。接口不支持时可关闭；停止／断线保留已输出内容并标注状态，未完成轮次不作为下次完整上下文。TTS 在文字完成后朗读。
- **免打扰**：暂停主动问候、散步和自动朗读，保留 TTS 偏好；主动聊天与 Live 仍可使用。
- **全屏隐藏**：默认开启，仅检测角色所在屏幕。主动唤出快速聊天可临时显示，手动隐藏不会被自动恢复。
- **鼠标穿透与锁定**：穿透让角色区域的点击落到下方应用，托盘可关闭；锁定禁止拖动和散步。

| 默认快捷键 | 功能 |
| --- | --- |
| Ctrl + Alt + Space | 唤出快速对话 |
| Ctrl + Alt + H | 显示／隐藏伙伴 |
| Ctrl + Alt + D | 开关免打扰 |
| Ctrl + Alt + P | 开关整体鼠标穿透 |

快捷键可编辑或禁用；注册冲突会提示，托盘入口始终可用。详见 [日常使用](docs/daily-use.md)。

## 配置 AI 与语音

TTS 和 Live 音色提供可编辑下拉框，按厂商及模型显示 Voice ID 与简短说明，也可手动填写。见[音色预设依据](docs/voice-presets.md)。

设置中选择服务预设，填入地址、模型和自己的密钥，点击“保存服务设置”。支持 **OpenAI** 与 **Anthropic** 文字协议；内置 OpenAI、Gemini、DeepSeek、MiMo、火山、Kimi、MiniMax、智谱、千问等填表示例。

- **ASR + TTS**：点击说话 → 结束并发送 → 识别 → 模型回复 → 朗读。支持 MiMo / OpenAI 音频接口，也可使用 Windows 系统朗读。
- **Gemini Live**：单独配置 Live 服务，在聊天中切换为 Live，点击开始；持续语音、字幕和打断，再次点击结束。TTS 开关控制分步朗读，Live 音频随 Live 会话输出。
- 每项服务有独立代理开关，默认地址 **127.0.0.1:7890**，可编辑。音量默认 **50%**，立即生效并保存。

启动不会请求模型或打开麦克风。密钥使用当前 Windows 用户 DPAPI 加密，配置与导入外观保存在程序旁的 `data`。真实模型权限、网络可达性和声音质量由所用服务决定；安装包暂未签名。

主动问候使用本地台词，不消耗模型额度、不自动朗读，也不加入聊天历史。聊天、语音、全屏、暂停或隐藏期间保持安静；恢复后重新等待。自动更新保留 `data`，对话窗口打开或设置草稿未保存时等待；重启前显示 5 秒倒计时气泡，可直接取消。更新只读取正式 Release，dev tag 本身不会触发升级。

详见 [对话配置](docs/dialogue-setup.md)。

## 自定义外观

下载 Release 中的高清外观包，解压后在设置中导入 `pet.json`。制作自己的桌宠可从程序旁 **templates/pet.json** 开始；模板、Schema 和动作网格图都在 **templates** 文件夹。

三位内置伙伴各含 **73 个原始帧**，覆盖九组动作与 **16 个视线方向**。动作直接切换，已删除造成重影的混合补帧。播放按真实时间推进，窗口渲染目标 60 Hz；实际素材换帧率取决于动作节奏，不代表每秒 60 张不同图像。

<img src="previews/marfu.png" width="240" alt="马尔芙：金发绿眼的魔法少女桌宠">

每套保留 73 帧的高清原图，单帧 480×520，透明图 3840×5720；旧版外观导入格式及播放节奏保持兼容。内置与导入外观均直接播放原始高清帧。模板可设置 `system_prompt`，切换形象同时切换人格。

详见 [外观格式](docs/skin-format.md) 和 [模板说明](templates/README.md)。

## 构建与升级

源码使用 C# / WPF / .NET Framework 4.8；正常编译不需要重新生成动作图。打包使用 Python + Pillow，安装器使用 Inno Setup 6.7+。可用 `-OutputDirectory .\work\app` 指定隔离编译目录。

内置素材直接播放原始动作帧，不使用混合补帧，避免重影。打包需要 Python + Pillow，安装器需要 Inno Setup 6.7+。

```powershell
.\Build.ps1 -Target App
python .\Package.py
.\BuildInstaller.ps1 -Compiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

产物位于上一级 `outputs`。已有 1.3.x-beta 用户请手动安装 0.4.0 完成版本编号调整；安装升级保留个人 `data`，并迁移本安装位置已开启的自启。免安装版更换 EXE 名称后需重新开启自启。

[安装说明](docs/installation.md) · [验证范围](docs/verification.md) · [素材与协议来源](docs/sources.md)
