# 素材与平台说明

「大肥鱼」为本会话根据 DeepSeek 同人形象制作的生成式角色及动作图；参考形象项目：[YunYueSama/codex-deepseek-pet](https://github.com/YunYueSama/codex-deepseek-pet)。应用代码独立实现。

「E包」使用本地 E-bao-20261013 已完成的角色和动作原图，形象来自该会话的参考图。原始目录保持原样。本次分别从两套原始动作条重新提取透明人物，处理抠图边色，统一组内缩放与落地基线，并保留跳跃位移；不使用小精灵图作为高清像素来源。

Windows 平台依据：[WPF 透明窗口](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency)、[Windows 录音缓冲管理](https://learn.microsoft.com/en-us/windows/win32/multimedia/managing-waveform-audio-recording)。录音通过显式点击启动，使用默认输入设备，16 kHz 单声道 PCM。

文字与通用语音依据：[Chat Completions](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create)、[语音识别](https://developers.openai.com/api/reference/resources/audio/subresources/transcriptions/methods/create)、[语音合成](https://developers.openai.com/api/reference/resources/audio/subresources/speech/methods/create)、[Anthropic Messages](https://platform.claude.com/docs/en/api/messages/create)。

MiMo 对话音频：[识别](https://mimo.mi.com/docs/zh-CN/quick-start/usage-guide/audio/Speech-Recognition)、[合成](https://mimo.mi.com/docs/zh-CN/quick-start/usage-guide/audio/speech-synthesis-v2.5)。采用 input_audio 与 assistant 合成文本，解析 message.audio.data 的 Base64 WAV。

Gemini Live：[WebSocket 入门](https://ai.google.dev/gemini-api/docs/live-api/get-started-websocket)、[Live API 参考](https://ai.google.dev/api/live)。传输原始 PCM，支持转录、播放与打断。

服务预设参考：[Gemini](https://ai.google.dev/gemini-api/docs/openai)、[DeepSeek](https://api-docs.deepseek.com/)、[Kimi](https://platform.kimi.com/docs/get-api-key)、[MiniMax](https://platform.minimax.io/docs/api-reference/text-openai-api)、[火山](https://docs.volcengine.com/docs/ark/base-url-and-authentication?lang=en)、[千问](https://help.aliyun.com/zh/model-studio/model-calling-in-sub-workspace)、[智谱](https://docs.bigmodel.cn/cn/best-practice/case/ai-search-engine)、[Claude](https://platform.claude.com/docs/en/models/sonnet-4-6/overview)。实际模型权限由账号决定，预设均可编辑。

更新：[GitHub Releases REST API](https://docs.github.com/en/rest/releases/releases#get-the-latest-release)。仓库目前仅有占位地址，未建立或上传。所有在线协议使用本机模拟验证，未使用真实线上密钥验收。
