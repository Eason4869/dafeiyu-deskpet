using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Dafeiyu.Tests
{
    public static class ApiTests
    {
        private static int passed, failed;
        public static int Main()
        {
            Run("proxy toggle actually routes HTTP through the selected local proxy",async delegate {
                using(var proxy=new Fixture("",502))using(var api=new ApiClient(TimeSpan.FromSeconds(5),new Uri(proxy.Url)))
                {var cfg=Connection("https://proxy-test.invalid/v1","chat");cfg.UseProxy=true;try{await api.ChatAsync(cfg,Messages(),CancellationToken.None);throw new Exception("fixture's rejected CONNECT accepted");}catch(ApiException){}await proxy.Done;Equal("proxy-test.invalid:443",proxy.Path);Assert(proxy.Headers.StartsWith("CONNECT ") && proxy.Authorization==null,"HTTPS did not reach the proxy tunnel or leaked auth before TLS");}
            });
            Run("Anthropic separates system, authenticates and collects text blocks",async delegate {
                using(var server=new Fixture("{\"content\":[{\"type\":\"thinking\",\"thinking\":\"private\"},{\"type\":\"text\",\"text\":\"你好\"},{\"type\":\"text\",\"text\":\"世界\"}]}"))using(var api=new ApiClient())
                {var cfg=Connection(server.Url+"/v1","claude-test");cfg.Protocol="anthropic";string result=await api.ChatAsync(cfg,new List<ChatMessage>{new ChatMessage("system","角色"),new ChatMessage("user","测试")},CancellationToken.None);await server.Done;Equal("你好世界",result);Equal("/v1/messages",server.Path);Assert(server.Headers.Contains("x-api-key: dummy-key") && server.Headers.Contains("anthropic-version: 2023-06-01") && server.Authorization==null,"wrong Anthropic authentication");var body=new JavaScriptSerializer().DeserializeObject(server.Body) as Dictionary<string,object>;Equal("角色",body["system"]);Equal(1,((object[])body["messages"]).Length);Assert((int)body["max_tokens"]>0,"missing token limit");}
            });
            Run("MiMo ASR uses chat audio input and reads assistant text",async delegate {
                byte[] wav=WaveData.EncodePcm(new byte[16000]);using(var server=new Fixture("{\"choices\":[{\"message\":{\"content\":\"测试语音\"}}]}"))using(var api=new ApiClient())
                {var cfg=Connection(server.Url+"/v1","mimo-v2.5-asr");string result=await api.TranscribeAsync(cfg,wav,CancellationToken.None);await server.Done;Equal("测试语音",result);Equal("/v1/chat/completions",server.Path);
                var json=new JavaScriptSerializer().DeserializeObject(server.Body) as Dictionary<string,object>;var input=(Dictionary<string,object>)((object[])((Dictionary<string,object>)((object[])json["messages"])[0])["content"])[0];var audio=(Dictionary<string,object>)input["input_audio"];Equal("input_audio",(string)input["type"]);Equal("data:audio/wav;base64,"+Convert.ToBase64String(wav),(string)audio["data"]);}
            });
            Run("MiMo TTS uses assistant text, named voice and base64 WAV",async delegate {
                byte[] wav=WaveData.EncodePcm(new byte[16000]);string json="{\"choices\":[{\"message\":{\"audio\":{\"data\":\""+Convert.ToBase64String(wav)+"\"}}}]}";
                using(var server=new Fixture(json))using(var api=new ApiClient()) {byte[] reply=await api.SpeechAsync(Connection(server.Url+"/v1","mimo-v2.5-tts"),"苏打","你好",CancellationToken.None);await server.Done;Equal(wav.Length,reply.Length);Equal("/v1/chat/completions",server.Path);Assert(server.Body.Contains("assistant") && server.Body.Contains("苏打") && server.Body.Contains("你好") && !server.Body.Contains("response_format"),"wrong MiMo synthesis shape");}
            });
            Run("null transcription response has an actionable error",async delegate {
                using(var server=new Fixture("null"))using(var api=new ApiClient()){try{await api.TranscribeAsync(Connection(server.Url,"stt-test"),WaveData.EncodePcm(new byte[16000]),CancellationToken.None);throw new Exception("null accepted");}catch(ApiException e){Assert(e.Message.Contains("识别"),"missing transcription context");}}
            });
            Run("text request keeps custom prefix, Unicode and authorization", async delegate {
                using (var server = new Fixture("{\"choices\":[{\"message\":{\"content\":\"你好，我是大肥鱼。\"}}]}"))
                using (var api = new ApiClient())
                {
                    var settings = Connection(server.Url + "/custom/v1", "chat-test");
                    string answer = await api.ChatAsync(settings, new List<ChatMessage> { new ChatMessage("system", "角色"), new ChatMessage("user", "你好") }, CancellationToken.None);
                    Equal("你好，我是大肥鱼。", answer); await server.Done;
                    Equal("/custom/v1/chat/completions", server.Path); Equal("Bearer dummy-key", server.Authorization);
                    var body = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(server.Body);
                    Equal("chat-test", (string)body["model"]);
                    var messages = (System.Collections.ArrayList)body["messages"];
                    Equal("你好", (string)((Dictionary<string, object>)messages[1])["content"]);
                }
            });
            Run("full endpoint does not get appended twice", async delegate {
                using (var server = new Fixture("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}"))
                using (var api = new ApiClient())
                {
                    await api.ChatAsync(Connection(server.Url + "/v1/chat/completions", "chat"), new List<ChatMessage> { new ChatMessage("user", "hello") }, CancellationToken.None);
                    await server.Done; Equal("/v1/chat/completions", server.Path);
                }
            });
            Run("recognition sends WAV multipart to its own service", async delegate {
                byte[] wav = WaveData.EncodePcm(new byte[16000]);
                using (var server = new Fixture("{\"text\":\"今天想聊设计\"}"))
                using (var api = new ApiClient())
                {
                    string text = await api.TranscribeAsync(Connection(server.Url + "/voice", "stt-test"), wav, CancellationToken.None);
                    Equal("今天想聊设计", text); await server.Done;
                    Equal("/voice/audio/transcriptions", server.Path);
                    Assert(server.ContentType.StartsWith("multipart/form-data"), "not multipart");
                    Assert(server.Body.Contains("stt-test") && server.Body.Contains("speech.wav") && server.Body.Contains("RIFF"), "missing model or actual audio");
                }
            });
            Run("cloud speech requests WAV and validates returned audio", async delegate {
                byte[] wav = WaveData.EncodePcm(new byte[16000]);
                using (var server = new Fixture(wav, "audio/wav"))
                using (var api = new ApiClient())
                {
                    byte[] result = await api.SpeechAsync(Connection(server.Url + "/v1", "tts-test"), "voice-test", "你好", CancellationToken.None);
                    Equal(wav.Length, result.Length); await server.Done;
                    var body = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(server.Body);
                    Equal("wav", (string)body["response_format"]); Equal("voice-test", (string)body["voice"]); Equal("你好", (string)body["input"]);
                }
            });
            Run("unauthorized response never exposes provider secrets", async delegate {
                using (var server = new Fixture("{\"error\":\"dummy-key private provider details\"}", 401))
                using (var api = new ApiClient())
                {
                    try { await api.ChatAsync(Connection(server.Url, "chat"), Messages(), CancellationToken.None); throw new Exception("401 accepted"); }
                    catch (ApiException e) { Assert(e.Message.Contains("401") && !e.Message.Contains("dummy-key") && !e.Message.Contains("private"), "unsafe error text"); }
                }
            });
            Run("redirect is rejected instead of forwarding a key", async delegate {
                using (var server = new Fixture("", 302))
                using (var api = new ApiClient())
                {
                    server.ExtraHeader = "Location: https://example.com/collect\r\n";
                    try { await api.ChatAsync(Connection(server.Url, "chat"), Messages(), CancellationToken.None); throw new Exception("redirect accepted"); }
                    catch (ApiException e) { Assert(e.Message.Contains("302"), "wrong redirect error"); }
                }
            });
            Run("malformed model reply fails clearly", async delegate {
                using (var server = new Fixture("{\"choices\":[]}"))
                using (var api = new ApiClient())
                {
                    try { await api.ChatAsync(Connection(server.Url, "chat"), Messages(), CancellationToken.None); throw new Exception("empty reply accepted"); }
                    catch (ApiException) { }
                }
            });
            Run("cancel interrupts an in-flight HTTP request", async delegate {
                using (var server = new Fixture("{\"choices\":[]}"))
                using (var api = new ApiClient())
                using (var cancel = new CancellationTokenSource())
                {
                    server.DelayMilliseconds = 1200; cancel.CancelAfter(120);
                    try { await api.ChatAsync(Connection(server.Url, "chat"), Messages(), cancel.Token); throw new Exception("cancel ignored"); }
                    catch (OperationCanceledException) { }
                }
            });
            Run("remote plaintext and credential URLs are refused", async delegate {
                using (var api = new ApiClient())
                {
                    foreach (string url in new[] { "http://example.com/v1", "https://user:pass@example.com/v1", "https://example.com/v1?key=secret" })
                    {
                        try { await api.ChatAsync(Connection(url, "chat"), Messages(), CancellationToken.None); throw new Exception("unsafe endpoint accepted"); }
                        catch (ApiException) { }
                    }
                }
            });
            Run("timeout includes an unfinished response body",async delegate {
                using(var server=new Fixture("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}"))
                using(var api=new ApiClient(TimeSpan.FromMilliseconds(180)))
                {
                    server.DelayBodyMilliseconds=3000; var watch=System.Diagnostics.Stopwatch.StartNew();
                    try{await api.ChatAsync(Connection(server.Url,"chat"),Messages(),CancellationToken.None);throw new Exception("body timeout ignored");}
                    catch(ApiException e){Assert(e.Message.Contains("超时"),"timeout message: "+e.Message);Assert(watch.ElapsedMilliseconds<1500,"response body lasted "+watch.ElapsedMilliseconds+" ms");}
                }
            });
            Run("cancel closes a body read after response headers arrive",async delegate {
                using(var server=new Fixture("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}"))
                using(var api=new ApiClient()) using(var cancel=new CancellationTokenSource())
                {
                    server.DelayBodyMilliseconds=3000; var task=api.ChatAsync(Connection(server.Url,"chat"),Messages(),cancel.Token);
                    await server.HeadersSent; var watch=System.Diagnostics.Stopwatch.StartNew(); cancel.Cancel();
                    try{await task;throw new Exception("cancel ignored");}catch(OperationCanceledException){Assert(watch.ElapsedMilliseconds<1500,"cancel waited for stalled body");}
                }
            });
            Run("PCM encoding has valid lengths and 16k mono format", async delegate {
                byte[] pcm = { 1, 2, 3, 4 }; byte[] wav = WaveData.EncodePcm(pcm);
                Equal(48, wav.Length); Equal(40, BitConverter.ToInt32(wav, 4)); Equal(16000, BitConverter.ToInt32(wav, 24));
                Equal((short)1, BitConverter.ToInt16(wav, 22)); Equal((short)16, BitConverter.ToInt16(wav, 34)); Equal((byte)1, wav[44]);
                Assert(WaveData.IsWave(wav), "invalid WAV");
                await Task.FromResult(0);
            });
            Console.WriteLine("API tests: " + passed + " passed, " + failed + " failed"); return failed == 0 ? 0 : 1;
        }
        private static List<ChatMessage> Messages() { return new List<ChatMessage> { new ChatMessage("user", "hello") }; }
        private static ConnectionSettings Connection(string url, string model) { return new ConnectionSettings { BaseUrl = url, Model = model, ApiKey = "dummy-key" }; }
        private static void Run(string name, Func<Task> test)
        {
            try { test().GetAwaiter().GetResult(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
        }
        private static void Assert(bool value, string reason) { if (!value) throw new Exception(reason); }
        private static void Equal<T>(T expected, T actual) { if (!Object.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }

        internal sealed class Fixture : IDisposable
        {
            private readonly TcpListener listener;
            private readonly byte[] reply;
            private readonly string replyType;
            private readonly int status;
            public string Url, Path, Body, Authorization, ContentType,Headers;
            public string ExtraHeader = "";
            public int DelayMilliseconds;
            public int DelayBodyMilliseconds;
            private readonly TaskCompletionSource<bool> headersSent=new TaskCompletionSource<bool>();
            public Task HeadersSent { get {return headersSent.Task;} }
            public Task Done;
            public Fixture(string json, int statusCode = 200) : this(Encoding.UTF8.GetBytes(json), "application/json", statusCode) { }
            public Fixture(byte[] bytes, string mime, int statusCode = 200)
            {
                reply = bytes; replyType = mime; status = statusCode;
                listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
                Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
                Done = Task.Run((Func<Task>)Serve);
            }
            private async Task Serve()
            {
                try
                {
                    using (TcpClient client = await listener.AcceptTcpClientAsync())
                    using (NetworkStream stream = client.GetStream())
                    {
                        client.ReceiveTimeout = 4000; client.SendTimeout = 4000;
                        var headerBytes = new MemoryStream(); int matched = 0;
                        byte[] terminal = { 13, 10, 13, 10 };
                        while (matched < 4 && headerBytes.Length < 32768)
                        {
                            int next = stream.ReadByte(); if (next < 0) throw new IOException("Request ended.");
                            headerBytes.WriteByte((byte)next); matched = next == terminal[matched] ? matched + 1 : 0;
                        }
                        string headers = Encoding.ASCII.GetString(headerBytes.ToArray());
                        Headers=headers;
                        string[] lines = headers.Split(new[] { "\r\n" }, StringSplitOptions.None); Path = lines[0].Split(' ')[1];
                        int length = 0;
                        foreach (string line in lines)
                        {
                            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = Int32.Parse(line.Substring(15).Trim());
                            if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) Authorization = line.Substring(14).Trim();
                            if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase)) ContentType = line.Substring(13).Trim();
                        }
                        if (headers.IndexOf("100-continue", StringComparison.OrdinalIgnoreCase) >= 0)
                        { byte[] interim = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"); await stream.WriteAsync(interim, 0, interim.Length); }
                        byte[] body = new byte[length]; int read = 0;
                        while (read < length) { int count = await stream.ReadAsync(body, read, length - read); if (count == 0) break; read += count; }
                        Body = Encoding.UTF8.GetString(body);
                        if (DelayMilliseconds > 0) await Task.Delay(DelayMilliseconds);
                        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Test\r\nContent-Type: " + replyType + "\r\nContent-Length: " + reply.Length + "\r\nConnection: close\r\n" + ExtraHeader + "\r\n");
                        await stream.WriteAsync(response, 0, response.Length); headersSent.TrySetResult(true);
                        if(DelayBodyMilliseconds>0) await Task.Delay(DelayBodyMilliseconds);
                        await stream.WriteAsync(reply, 0, reply.Length);
                    }
                }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
                catch (SocketException) { }
            }
            public void Dispose() { listener.Stop(); }
        }
    }
}
