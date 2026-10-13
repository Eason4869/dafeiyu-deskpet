using System;
using System.Collections.Generic;

namespace Dafeiyu.Tests
{
    public static class VoicePresetTests
    {
        private static int passed, failed;
        public static int Main()
        {
            Run("MiMo model on direct and Token Plan hosts exposes only actual built-in voice IDs", delegate {
                foreach(string url in new[] { "https://api.xiaomimimo.com/v1", "https://token-plan-cn.xiaomimimo.com/v1" })
                    Equal("mimo_default,冰糖,茉莉,苏打,白桦,Mia,Chloe,Milo,Dean", Ids(For("speech", url, "mimo-v2.5-tts", "mimo")));
            });
            Run("MiMo clone and design models cannot suggest unsupported preset IDs", delegate {
                foreach(string model in new[] { "mimo-v2.5-tts-voiceclone", "mimo-v2.5-tts-voicedesign", "mimo-v2.5-asr" })
                    Equal("", Ids(For("speech", "https://api.xiaomimimo.com/v1", model, "mimo")));
            });
            Run("OpenAI legacy TTS models use their documented smaller nine-voice set", delegate {
                foreach(string model in new[] { "tts-1", "tts-1-hd" })
                    Equal("alloy,ash,coral,echo,fable,onyx,nova,sage,shimmer", Ids(For("speech", "https://api.openai.com/v1", model, "openai")));
            });
            Run("OpenAI mini TTS adds voices unavailable on tts-1", delegate {
                Equal("alloy,ash,ballad,coral,echo,fable,nova,onyx,sage,shimmer,verse,marin,cedar", Ids(For("speech", "https://api.openai.com/v1", "gpt-4o-mini-tts", "auto")));
            });
            Run("Gemini native Live exposes documented thirty IDs with Kore supported default", delegate {
                IList<VoicePreset> values = For("live", "wss://generativelanguage.googleapis.com/ws/test", "gemini-3.8-live", "gemini-live");
                Equal(30, values.Count); Equal("Kore", values[0].Id);
                Assert(Ids(values).Contains("Puck") && Ids(values).Contains("Vindemiatrix"), "native voice list incomplete");
                Assert(values[0].Description.Contains("坚定"), "missing official style");
            });
            Run("official host and model conflicts cannot mix manufacturers", delegate {
                Equal("", Ids(For("speech", "https://api.openai.com/v1", "mimo-v2.5-tts", "auto")));
                Equal("", Ids(For("speech", "https://api.xiaomimimo.com/v1", "tts-1", "auto")));
                Equal("", Ids(For("speech", "https://generativelanguage.googleapis.com/v1", "tts-1", "openai")));
                Equal("", Ids(For("live", "wss://api.openai.com/live", "gemini-3.8-live", "gemini-live")));
            });
            Run("explicit incompatible protocols cannot silently select a different vendor", delegate {
                Equal("", Ids(For("speech", "https://proxy.example/v1", "tts-1", "mimo")));
                Equal("", Ids(For("speech", "https://api.openai.com/v1", "tts-1", "anthropic")));
                Equal("", Ids(For("live", "wss://generativelanguage.googleapis.com/ws", "gemini-3.8-live", "openai")));
            });
            Run("custom proxy can infer actual voice family from supported model and protocol", delegate {
                Equal(9, For("speech", "https://proxy.example/v1", "mimo-v2.5-tts", "auto").Count);
                Equal(9, For("speech", "http://127.0.0.1:8080/v1", "tts-1", "openai").Count);
                Equal(30, For("live", "wss://proxy.example/live", "models/gemini-3.8-live", "gemini-live").Count);
            });
            Run("unknown models and unsupported service kinds allow manual entry", delegate {
                Equal(0, For("speech", "https://api.openai.com/v1", "custom-tts", "openai").Count);
                Equal(0, For("live", "wss://generativelanguage.googleapis.com/ws", "gemini-unknown-live", "gemini-live").Count);
                Equal(0, For("asr", "https://api.openai.com/v1", "tts-1", "openai").Count);
                Equal(0, For("speech", "https://proxy.example/v1", "", "auto").Count);
            });
            Run("MiMo descriptions use documented gender and mark unauditioned style", delegate {
                IList<VoicePreset> values = For("speech", "https://api.xiaomimimo.com/v1", "mimo-v2.5-tts", "mimo");
                Assert(values[3].Description.Contains("男声") && values[3].Description.Contains("试听"), "unsupported MiMo style implied");
                Assert(values[3].ToString().Contains("苏打") && values[3].ToString().Contains("男声"), "label loses ID or description");
            });
            Run("changing one returned list cannot contaminate later vendor lists", delegate {
                IList<VoicePreset> first = For("speech", "https://api.openai.com/v1", "tts-1", "openai"); first.Clear();
                Equal(9, For("speech", "https://api.openai.com/v1", "tts-1", "openai").Count);
            });
            Console.WriteLine("Voice preset tests: " + passed + " passed, " + failed + " failed"); return failed == 0 ? 0 : 1;
        }
        private static IList<VoicePreset> For(string kind, string url, string model, string protocol)
        {
            return VoicePresets.For(kind, new ConnectionSettings {BaseUrl=url,Model=model,Protocol=protocol});
        }
        private static string Ids(IList<VoicePreset> values) { var ids = new List<string>(); foreach(VoicePreset item in values) ids.Add(item.Id); return String.Join(",", ids); }
        private static void Run(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch(Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
        private static void Equal<T>(T expected, T actual) { Assert(Object.Equals(expected, actual), "Expected " + expected + ", got " + actual); }
        private static void Assert(bool value, string reason) { if(!value) throw new Exception(reason); }
    }
}
