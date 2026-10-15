using System;
using System.Collections.Generic;
namespace Dafeiyu
{
    public sealed class ServicePreset
    {
        public string Name,Url,Model,Protocol,Note;
        public ServicePreset(string name,string url,string model,string protocol="openai",string note=""){Name=name;Url=url;Model=model;Protocol=protocol;Note=note;}
        public override string ToString(){return Name;}
    }
    public static class ServicePresets
    {
        public static IList<ServicePreset> For(string kind)
        {
            var items=new List<ServicePreset>{new ServicePreset("自定义","","","auto","选择预设会填入地址和示例模型；都可手动修改。")};
            if(kind=="live") {items.Add(new ServicePreset("Gemini Live","wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent","gemini-3.8-live","gemini-live","填写账号实际支持的 Live 模型；此地址使用 Gemini 原生协议。"));return items;}
            if(kind=="asr" || kind=="speech")
            {
                items.Add(new ServicePreset("MiMo","https://api.xiaomimimo.com/v1",kind=="asr" ? "mimo-v2.5-asr" : "mimo-v2.5-tts","mimo","MiMo 使用对话音频协议。Token Plan 用户可改为自己的 Token Plan 地址。"));
                items.Add(new ServicePreset("MiMo · 国内 Token Plan","https://token-plan-cn.xiaomimimo.com/v1",kind=="asr" ? "mimo-v2.5-asr" : "mimo-v2.5-tts","mimo","使用对应 Token Plan 的密钥；文字与语音服务分别保存配置。"));
                items.Add(new ServicePreset("OpenAI","https://api.openai.com/v1",kind=="asr" ? "whisper-1" : "tts-1","openai",kind=="asr" ? "兼容 audio/transcriptions，发送 WAV 录音。" : "兼容 audio/speech，要求返回 WAV。"));return items;
            }
            items.Add(new ServicePreset("ChatGPT / OpenAI","https://api.openai.com/v1","gpt-4.1-mini"));
            items.Add(new ServicePreset("Gemini","https://generativelanguage.googleapis.com/v1beta/openai","gemini-3.8-flash"));
            items.Add(new ServicePreset("DeepSeek","https://api.deepseek.com","deepseek-flash"));
            items.Add(new ServicePreset("MiMo","https://api.xiaomimimo.com/v1","mimo-v2.6-flash"));
            items.Add(new ServicePreset("MiMo · 国内 Token Plan","https://token-plan-cn.xiaomimimo.com/v1","mimo-v2.6-flash"));
            items.Add(new ServicePreset("火山方舟","https://ark.cn-beijing.volces.com/api/v3","","openai","模型请填写控制台的模型 ID 或推理接入点 ID。"));
            items.Add(new ServicePreset("Kimi","https://api.moonshot.cn/v1","kimi-k3"));
            items.Add(new ServicePreset("MiniMax","https://api.minimax.io/v1","MiniMax-M2.5"));
            items.Add(new ServicePreset("智谱","https://open.bigmodel.cn/api/paas/v4","","openai","请填写控制台中可用的 GLM 模型名称。"));
            items.Add(new ServicePreset("千问 / 阿里百炼","https://dashscope.aliyuncs.com/compatible-mode/v1","qwen-plus"));
            items.Add(new ServicePreset("Claude / Anthropic","https://api.anthropic.com/v1","claude-sonnet-4-6","anthropic"));
            items.Add(new ServicePreset("DeepSeek · Anthropic","https://api.deepseek.com/anthropic/v1","deepseek-flash","anthropic"));return items;
        }
        public static bool SameOrigin(string before,string after)
        {Uri a,b;return Uri.TryCreate(before,UriKind.Absolute,out a) && Uri.TryCreate(after,UriKind.Absolute,out b) && a.Scheme==b.Scheme && String.Equals(a.Host,b.Host,StringComparison.OrdinalIgnoreCase) && a.Port==b.Port;}
    }
}
