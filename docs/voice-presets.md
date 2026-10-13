# 音色预设依据

核对日期：2026-10-14。设置中的音色下拉框可编辑；选择预设后保存真实 Voice ID，也可手动输入服务商或代理支持的其他 ID。列表按服务类型、协议、实际地址与模型筛选；已知厂商地址与其他厂商模型冲突时不显示预设。自定义代理可按明确的模型名称匹配。未知模型仍可手动填写。

## MiMo

仅 `mimo-v2.5-tts` 使用预置列表：`mimo_default`、`冰糖`、`茉莉`、`苏打`、`白桦`、`Mia`、`Chloe`、`Milo`、`Dean`。国内集群默认冰糖，其他集群默认 Mia；中文女声为冰糖、茉莉，中文男声为苏打、白桦，英文女声为 Mia、Chloe，英文男声为 Milo、Dean。官方没有为这些固定音色逐项定义柔和、叙述等风格，因此标签提示试听，不推测声线特点。国内 Token Plan 使用同组 ID。音色设计和复刻模型需要其他输入，不显示这组预设。依据：[MiMo 语音合成使用指南](https://mimo.mi.com/docs/zh-CN/quick-start/usage-guide/audio/speech-synthesis-v2.5)。

## OpenAI

当前官方文档明确 `tts-1` 和 `tts-1-hd` 支持 9 个音色：`alloy`、`ash`、`coral`、`echo`、`fable`、`onyx`、`nova`、`sage`、`shimmer`。`gpt-4o-mini-tts` 支持 13 个，在上述集合基础上增加 `ballad`、`verse`、`marin`、`cedar`。标签说明英语优化并提示试听；官方指南没有逐项给出性别与固定风格，应用不作保证。依据：[OpenAI Text to speech — Voice options](https://developers.openai.com/api/docs/guides/text-to-speech#voice-options)，可在 [OpenAI.fm](https://www.openai.fm/) 试听。

## Gemini Live

Gemini 原生音频 Live 模型使用 30 个预置 ID；Kore 是应用默认。中文标签根据官方风格翻译，不把其他 Google TTS 产品的性别属性套用到 Live。核对同时使用 [Gemini Live capabilities — Change voice and language](https://ai.google.dev/gemini-api/docs/live-api/capabilities#change-voice-and-language)、[Google Cloud Live — Voices supported](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/live-api/configure-language-voice#voices-supported) 和 [Gemini 预置音色表](https://ai.google.dev/gemini-api/docs/speech-generation#prebuilt-voices)，后两者列出 30 个名称和风格，Live 指南确认原生音频模型的适用范围。

| Voice ID | 中文风格 | Voice ID | 中文风格 | Voice ID | 中文风格 |
| --- | --- | --- | --- | --- | --- |
| Zephyr | 明亮 | Puck | 欢快 | Charon | 解说感 |
| Kore | 坚定 | Fenrir | 热情兴奋 | Leda | 年轻感 |
| Orus | 坚定 | Aoede | 轻松轻盈 | Callirrhoe | 随和 |
| Autonoe | 明亮 | Enceladus | 气声 | Iapetus | 清晰 |
| Umbriel | 随和 | Algieba | 顺滑 | Despina | 顺滑 |
| Erinome | 清晰 | Algenib | 沙哑 | Rasalgethi | 解说感 |
| Laomedeia | 欢快 | Achernar | 柔和 | Alnilam | 坚定 |
| Schedar | 平稳 | Gacrux | 成熟 | Pulcherrima | 直率 |
| Achird | 亲切 | Zubenelgenubi | 随意自然 | Vindemiatrix | 温和 |
| Sadachbia | 活泼 | Sadaltager | 博学感 | Sulafat | 温暖 |

预设适用于已知原生音频模型：Gemini 3.8 Live（含 Extended Thinking）、3.1 Flash Live Preview、2.5 Flash Native Audio（含日期版预览和 Vertex 同名模型）。旧式半级联及未知模型不猜测音色集合，可手动输入。ID 大小写按官方原样保留。音色风格会受语言、输入及服务实现影响；本次核验是文档核验，没有使用真实密钥逐一试听。
