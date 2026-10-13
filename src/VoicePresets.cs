using System;
using System.Collections.Generic;

namespace Dafeiyu
{
    public sealed class VoicePreset
    {
        public string Id { get; private set; }
        public string Description { get; private set; }
        public VoicePreset(string id, string description) { Id = id; Description = description; }
        public override string ToString() { return Id + " · " + Description; }
    }
    public static class VoicePresets
    {
        // IDs and neutral labels are sourced in docs/voice-presets.md; custom input stays available.
        public static IList<VoicePreset> For(string kind, ConnectionSettings settings)
        {
            var items = new List<VoicePreset>(); if(settings == null) return items;
            Uri uri; if(!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out uri)) return items;
            string model = (settings.Model ?? "").Trim().ToLowerInvariant();
            if(model.StartsWith("models/")) model = model.Substring(7);
            string protocol = (settings.Protocol ?? "auto").Trim().ToLowerInvariant();
            bool generic = protocol == "auto" || protocol == "" || protocol == "openai";
            string host = uri.Host.TrimEnd('.').ToLowerInvariant();
            bool mimoHost = host == "xiaomimimo.com" || host.EndsWith(".xiaomimimo.com");
            bool openaiHost = host == "api.openai.com";
            bool geminiHost = host == "generativelanguage.googleapis.com";
            if(kind == "speech")
            {
                if(model == "mimo-v2.5-tts" && (generic || protocol == "mimo") && !openaiHost && !geminiHost)
                {
                    items.Add(new VoicePreset("mimo_default", "默认音色；国内冰糖，其他集群 Mia"));
                    foreach(string id in new[] {"冰糖", "茉莉"}) items.Add(new VoicePreset(id, "中文女声；风格请试听"));
                    foreach(string id in new[] {"苏打", "白桦"}) items.Add(new VoicePreset(id, "中文男声；风格请试听"));
                    foreach(string id in new[] {"Mia", "Chloe"}) items.Add(new VoicePreset(id, "英文女声；风格请试听"));
                    foreach(string id in new[] {"Milo", "Dean"}) items.Add(new VoicePreset(id, "英文男声；风格请试听"));
                }
                else if(generic && !mimoHost && !geminiHost && (model == "tts-1" || model == "tts-1-hd" || model == "gpt-4o-mini-tts"))
                {
                    string[] voices = model == "gpt-4o-mini-tts" ? new[] {"alloy","ash","ballad","coral","echo","fable","nova","onyx","sage","shimmer","verse","marin","cedar"} : new[] {"alloy","ash","coral","echo","fable","onyx","nova","sage","shimmer"};
                    foreach(string id in voices) items.Add(new VoicePreset(id, "英语优化；声线与风格请试听"));
                }
            }
            else if(kind == "live" && (protocol == "gemini-live" || protocol == "auto" || protocol == "") && !mimoHost && !openaiHost && NativeLiveModel(model))
            {
                string[,] voices = {
                    {"Kore","坚定"}, {"Zephyr","明亮"}, {"Puck","欢快"}, {"Charon","解说感"}, {"Fenrir","热情兴奋"},
                    {"Leda","年轻感"}, {"Orus","坚定"}, {"Aoede","轻松轻盈"}, {"Callirrhoe","随和"}, {"Autonoe","明亮"},
                    {"Enceladus","气声"}, {"Iapetus","清晰"}, {"Umbriel","随和"}, {"Algieba","顺滑"}, {"Despina","顺滑"},
                    {"Erinome","清晰"}, {"Algenib","沙哑"}, {"Rasalgethi","解说感"}, {"Laomedeia","欢快"}, {"Achernar","柔和"},
                    {"Alnilam","坚定"}, {"Schedar","平稳"}, {"Gacrux","成熟"}, {"Pulcherrima","直率"}, {"Achird","亲切"},
                    {"Zubenelgenubi","随意自然"}, {"Vindemiatrix","温和"}, {"Sadachbia","活泼"}, {"Sadaltager","博学感"}, {"Sulafat","温暖"}
                };
                for(int i = 0; i < voices.GetLength(0); i++) items.Add(new VoicePreset(voices[i,0], voices[i,1]));
            }
            return items;
        }
        private static bool NativeLiveModel(string model)
        {
            return model == "gemini-3.8-live" || model == "gemini-3.8-live-extended-thinking" || model == "gemini-3.1-flash-live-preview" ||
                model == "gemini-2.5-flash-native-audio" || model.StartsWith("gemini-2.5-flash-native-audio-preview-") || model == "gemini-live-2.5-flash-native-audio";
        }
    }
}
