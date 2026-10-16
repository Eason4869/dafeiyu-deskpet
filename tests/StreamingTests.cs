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
    public static class StreamingTests
    {
        private static int passed, failed;
        private const string First = "data: {\"choices\":[{\"delta\":{\"content\":\"你好🌙\"}}]}\n\n";
        private const string Done = "data: [DONE]\n\n";
        public static int Main()
        {
            Run("reasoning streams separately before the answer for both protocols", async delegate {
                foreach(bool anthropic in new[]{false,true})
                {
                    string reasoning="先核对条件🌙", actual="", answer="";
                    string body=anthropic
                        ? "data: {\"type\":\"content_block_start\",\"content_block\":{\"type\":\"thinking\",\"thinking\":\"先\"}}\n\ndata: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"thinking_delta\",\"thinking\":\"核对条件🌙\"}}\n\ndata: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"答复\"}}\n\ndata: {\"type\":\"message_stop\"}\n\n"
                        : "data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"先核对\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"reasoning\":\"条件🌙\",\"content\":\"答复\"}}]}\n\n"+Done;
                    var method=typeof(ChatStream).GetMethod("ReadAsync",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(Stream),typeof(bool),typeof(Action<string>),typeof(CancellationToken),typeof(int),typeof(Action<string>)},null);
                    Assert(method!=null,"missing separate reasoning stream callback");
                    using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(body)))
                    {
                        var task=(Task<string>)method.Invoke(null,new object[]{stream,anthropic,(Action<string>)(d=>{Assert(actual==reasoning,"answer arrived before reasoning");answer+=d;}),CancellationToken.None,2*1024*1024,(Action<string>)(d=>actual+=d)});
                        Equal("答复",await task);Equal(reasoning,actual);Equal("答复",answer);
                    }
                }
            });
            Run("reasoning has its own size bound and survives an interrupted stream", async delegate {
                foreach(bool oversized in new[]{false,true})
                {
                    string text=oversized?new string('x',50001):"部分思考",received="";
                    string data="data: {\"choices\":[{\"delta\":{\"reasoning_content\":\""+text+"\"}}]}\n\n";
                    using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(data)))
                    {
                        await Fails(()=>ChatStream.ReadAsync(stream,false,null,CancellationToken.None,2*1024*1024,d=>received+=d),oversized?"过长":"中断");
                        Equal(oversized?"":text,received);
                    }
                }
            });
            Run("OpenAI exposes split Unicode before the final network packet", async delegate {
                using(var fixture = new Fixture(First, Done, true)) using(var api = new ApiClient(TimeSpan.FromSeconds(4)))
                {
                    var arrived = new TaskCompletionSource<string>();
                    Task<string> request = Chat(api, Config(fixture), delegate(string delta) { arrived.TrySetResult(delta); }, CancellationToken.None);
                    Equal("你好🌙", await Within(arrived.Task)); Assert(!request.IsCompleted, "reply completed before terminal packet");
                    fixture.Release(); Equal("你好🌙", await request); await fixture.Done;
                    var body = (Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(fixture.Body);
                    Equal(true, body["stream"]); Assert(fixture.Headers.Contains("Accept: text/event-stream"), "SSE accept missing");
                    Assert(fixture.Headers.Contains("Authorization: Bearer dummy-key"), "OpenAI auth missing");
                }
            });
            Run("SSE joins multiline data and accepts comments CRLF CR and heartbeats", async delegate {
                string data = ": ping\r\nevent: message\r\ndata: {\"choices\":\r\ndata: [{\"delta\":{\"content\":\"first\"}}]}\r\n\r\n: next\r\revent: ignored\rdata: definitely not JSON\r\rdata: {\"choices\":[{\"delta\":{\"content\":\" second\"}}]}\r\r";
                Equal("first second", await Reply(data, Done, "auto"));
            });
            Run("Anthropic shows only text deltas and requires message_stop", async delegate {
                string data = "event: message_start\ndata: {\"type\":\"message_start\",\"message\":{\"content\":[]}}\n\n" +
                    "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"thinking_delta\",\"thinking\":\"secret\"}}\n\n" +
                    "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":1,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"tool\"}}\n\n" +
                    "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":2,\"delta\":{\"type\":\"text_delta\",\"text\":\"你好🌙\"}}\n\n";
                using(var fixture = new Fixture(data, "event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n", true)) using(var api = new ApiClient())
                {
                    var config = Config(fixture); config.Protocol = "anthropic";
                    Equal("你好🌙", await Chat(api, config, delegate { fixture.Release(); }, CancellationToken.None)); await fixture.Done;
                    Assert(fixture.Headers.Contains("x-api-key: dummy-key") && fixture.Headers.Contains("anthropic-version: 2023-06-01") && !fixture.Headers.Contains("Authorization:"), "wrong Anthropic auth");
                    Assert(fixture.Headers.StartsWith("POST /v1/messages "), "wrong Anthropic endpoint");
                }
            });
            Run("OpenAI tool and reasoning fields never become display text", async delegate {
                Equal("visible", await Reply("data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"private\",\"tool_calls\":[{}]}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"visible\"}}]}\n\n", Done, "auto"));
            });
            Run("caller cancellation after a visible delta interrupts a stalled body", async delegate {
                using(var fixture = new Fixture(First, Done, true)) using(var api = new ApiClient()) using(var cancel = new CancellationTokenSource())
                {
                    string partial = ""; var watch = System.Diagnostics.Stopwatch.StartNew();
                    try { await Chat(api, Config(fixture), delegate(string delta) { partial += delta; cancel.Cancel(); }, cancel.Token); throw new Exception("cancellation ignored"); }
                    catch(OperationCanceledException) { Equal("你好🌙", partial); Assert(watch.ElapsedMilliseconds < 1800, "cancellation waited for EOF"); }
                }
            });
            Run("bounded timeout interrupts a stream after partial output", async delegate {
                using(var fixture = new Fixture(First, Done, true)) using(var api = new ApiClient(TimeSpan.FromMilliseconds(350)))
                { string partial = ""; await Fails(delegate { return Chat(api, Config(fixture), delegate(string delta) { partial += delta; }, CancellationToken.None); }, "超时"); Equal("你好🌙", partial); }
            });
            Run("EOF after partial OpenAI text fails without DONE", async delegate { await Bad(First, "", "auto", "中断"); });
            Run("finish_reason alone cannot turn a truncated stream into success", async delegate { await Bad(First + "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n", "", "auto", "中断"); });
            Run("Anthropic message_delta alone cannot turn EOF into success", async delegate { await Bad("data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"partial\"}}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}\n\n", "", "anthropic", "中断"); });
            Run("malformed known event produces a safe compatibility error", async delegate { await Bad("data: {invalid secret-body\n\n", Done, "auto", "流式"); });
            Run("OpenAI error payload stays private", async delegate { await Bad("data: {\"error\":{\"message\":\"secret-key-and-body\"}}\n\n", Done, "auto", "流式"); });
            Run("Anthropic error event stays private", async delegate { await Bad("event: error\ndata: {\"type\":\"error\",\"error\":{\"message\":\"secret-key-and-body\"}}\n\n", "", "anthropic", "流式"); });
            Run("full JSON on the same response works with no second request", async delegate {
                using(var fixture = new Fixture("{\"choices\":[{\"message\":{\"content\":\"你好🌙\"}}]}", "", false, "application/json")) using(var api = new ApiClient())
                { string partial = ""; Equal("你好🌙", await Chat(api, Config(fixture), delegate(string delta) { partial += delta; }, CancellationToken.None)); Equal("你好🌙", partial); Equal(1, fixture.Requests); }
            });
            Run("Anthropic JSON compatibility ignores thinking blocks", async delegate {
                using(var fixture = new Fixture("{\"content\":[{\"type\":\"thinking\",\"thinking\":\"private\"},{\"type\":\"text\",\"text\":\"ok\"}]}", "", false)) using(var api = new ApiClient())
                { var config = Config(fixture); config.Protocol = "anthropic"; Equal("ok", await Chat(api, config, null, CancellationToken.None)); }
            });
            Run("stream refusal gives actionable guidance without retry or leaked body", async delegate {
                using(var fixture = new Fixture("secret-key-and-body", "", false, "application/json", 400)) using(var api = new ApiClient())
                { await Fails(delegate { return Chat(api, Config(fixture), null, CancellationToken.None); }, "关闭流式"); Equal(1, fixture.Requests); }
            });
            Run("redirect never forwards credentials or exposes response content", async delegate {
                using(var fixture = new Fixture("secret-key-and-body", "", false, "application/json", 302)) using(var api = new ApiClient())
                { await Fails(delegate { return Chat(api, Config(fixture), null, CancellationToken.None); }, "重定向"); Equal(1, fixture.Requests); }
            });
            Run("reply size is enforced before an overlimit delta callback", async delegate {
                using(var fixture = new Fixture("data: {\"choices\":[{\"delta\":{\"content\":\"" + new string('x', 50001) + "\"}}]}\n\n", Done, false)) using(var api = new ApiClient())
                { int callbacks = 0; await Fails(delegate { return Chat(api, Config(fixture), delegate { callbacks++; }, CancellationToken.None); }, "过长"); Equal(0, callbacks); }
            });
            Run("one unbounded SSE event is rejected even without newline", async delegate { await Bad("data: " + new string('x', 140000), "", "auto", "过大"); });
            Run("overall stream byte budget bounds heartbeat-only responses", async delegate {
                var body = new StringBuilder(); for(int i = 0; i < 24000; i++) body.Append(":" + new string('x', 90) + "\n\n");
                await Bad(body.ToString(), "", "auto", "过大");
            });
            Run("UTF8 decoder keeps capacity for a carried supplementary codepoint", async delegate {
                string text = "🌙" + new string('x', 8191);
                string body = "data: {\"choices\":[{\"delta\":{\"content\":\"" + text + "\"}}]}\n\n" + Done;
                byte[] bytes = Encoding.UTF8.GetBytes(body); int split = Encoding.UTF8.GetByteCount(body.Substring(0, body.IndexOf("🌙"))) + 3;
                using(var stream = new SplitReadStream(bytes, split)) Equal(text, await ChatStream.ReadAsync(stream, false, null, CancellationToken.None, 2*1024*1024));
            });
            Run("invalid UTF8 is rejected without exposing undecodable payload", async delegate {
                using(var stream = new MemoryStream(new byte[] {100,97,116,97,58,32,255,10,10}))
                    await Fails(delegate { return ChatStream.ReadAsync(stream, false, null, CancellationToken.None, 2*1024*1024); }, "流式");
            });
            Run("streaming uses configured HTTPS proxy without leaking auth before TLS", async delegate {
                using(var fixture = new Fixture("", "", false, "application/json", 502)) using(var api = new ApiClient(TimeSpan.FromSeconds(4)))
                {
                    var config = Config(fixture); config.BaseUrl = "https://proxy-test.invalid/v1"; config.UseProxy = true; config.ProxyAddress = fixture.Url;
                    await Fails(delegate { return Chat(api, config, null, CancellationToken.None); }, "代理"); await fixture.Done;
                    Assert(fixture.Headers.StartsWith("CONNECT proxy-test.invalid:443 ") && !fixture.Headers.Contains("dummy-key"), "wrong proxy tunnel or credential leakage");
                }
            });
            Console.WriteLine("Streaming tests: " + passed + " passed, " + failed + " failed"); return failed == 0 ? 0 : 1;
        }
        private static Task<string> Chat(ApiClient api, ConnectionSettings settings, Action<string> delta, CancellationToken cancel)
        {
            return ((IStreamingConversationApi)api).ChatStreamAsync(settings, new List<ChatMessage> { new ChatMessage("system", "角色"), new ChatMessage("user", "hello") }, delta, cancel);
        }
        private static ConnectionSettings Config(Fixture fixture) { return new ConnectionSettings { BaseUrl = fixture.Url + "/v1", Model = "test", ApiKey = "dummy-key" }; }
        private static async Task<string> Reply(string first, string last, string protocol)
        { using(var fixture = new Fixture(first, last, false)) using(var api = new ApiClient()) { var config = Config(fixture); config.Protocol = protocol; return await Chat(api, config, null, CancellationToken.None); } }
        private static async Task Bad(string first, string last, string protocol, string expected)
        { await Fails(delegate { return Reply(first, last, protocol); }, expected); }
        private static async Task Fails(Func<Task<string>> request, string expected)
        { try { await request(); throw new Exception("invalid response accepted"); } catch(ApiException e) { Assert(e.Message.Contains(expected), "missing guidance: " + e.Message); Assert(!e.Message.Contains("secret") && !e.Message.Contains("dummy-key"), "response or credentials leaked"); } }
        private static async Task<T> Within<T>(Task<T> task)
        { if(await Task.WhenAny(task, Task.Delay(1800)) != task) throw new Exception("first delta was buffered until EOF"); return await task; }
        private static void Run(string name, Func<Task> test)
        { try { test().GetAwaiter().GetResult(); passed++; Console.WriteLine("PASS " + name); } catch(Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
        private static void Equal<T>(T expected, T actual) { Assert(Object.Equals(expected, actual), "Expected " + expected + ", got " + actual); }
        private static void Assert(bool value, string reason) { if(!value) throw new Exception(reason); }

        private sealed class SplitReadStream : MemoryStream
        {
            private readonly int first; private bool initial = true;
            internal SplitReadStream(byte[] bytes, int first) : base(bytes) { this.first = first; }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancel)
            { if(initial) { count = Math.Min(count, first); initial = false; } return base.ReadAsync(buffer, offset, count, cancel); }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly TcpListener listener;
            private readonly string first, last, type;
            private readonly bool gated;
            private readonly int status;
            private readonly TaskCompletionSource<bool> release = new TaskCompletionSource<bool>();
            public string Url, Headers, Body;
            public int Requests;
            public Task Done;
            public Fixture(string first, string last, bool gated, string type = "text/event-stream", int status = 200)
            {
                this.first = first; this.last = last; this.gated = gated; this.type = type; this.status = status;
                listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
                Done = Task.Run((Func<Task>)Serve);
            }
            private async Task Serve()
            {
                try
                {
                    using(var client = await listener.AcceptTcpClientAsync()) using(var stream = client.GetStream())
                    {
                        client.ReceiveTimeout = 4000; client.SendTimeout = 4000; Requests++;
                        var header = new MemoryStream(); int matched = 0; byte[] end = {13,10,13,10};
                        while(matched < 4 && header.Length < 32768) { int next = stream.ReadByte(); if(next < 0) throw new IOException(); header.WriteByte((byte)next); matched = next == end[matched] ? matched + 1 : 0; }
                        Headers = Encoding.ASCII.GetString(header.ToArray()); int length = 0;
                        foreach(string line in Headers.Split(new[] {"\r\n"}, StringSplitOptions.None)) if(line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = Int32.Parse(line.Substring(15).Trim());
                        if(Headers.IndexOf("100-continue", StringComparison.OrdinalIgnoreCase) >= 0) await Write(stream, Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"));
                        byte[] body = new byte[length]; int offset = 0; while(offset < length) { int read = await stream.ReadAsync(body, offset, length-offset); if(read == 0) throw new IOException(); offset += read; }
                        Body = Encoding.UTF8.GetString(body);
                        await Write(stream, Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Test\r\nContent-Type: " + type + "\r\nTransfer-Encoding: chunked\r\nConnection: close\r\nLocation: http://127.0.0.1:1/should-not-follow\r\n\r\n"));
                        byte[] bytes = Encoding.UTF8.GetBytes(first);
                        // The first fixture deliberately slices through every UTF-8 code point and SSE line.
                        int step = first.Length < 5000 ? 1 : 4093;
                        for(int i = 0; i < bytes.Length; i += step) await Chunk(stream, bytes, i, Math.Min(step, bytes.Length-i));
                        if(gated) await release.Task;
                        bytes = Encoding.UTF8.GetBytes(last); if(bytes.Length > 0) await Chunk(stream, bytes, 0, bytes.Length);
                        await Write(stream, Encoding.ASCII.GetBytes("0\r\n\r\n"));
                    }
                }
                catch(IOException) { } catch(ObjectDisposedException) { } catch(SocketException) { }
            }
            private static async Task Chunk(NetworkStream stream, byte[] bytes, int offset, int length)
            { await Write(stream, Encoding.ASCII.GetBytes(length.ToString("x") + "\r\n")); await stream.WriteAsync(bytes, offset, length); await Write(stream, Encoding.ASCII.GetBytes("\r\n")); }
            private static Task Write(NetworkStream stream, byte[] bytes) { return stream.WriteAsync(bytes, 0, bytes.Length); }
            public void Release() { release.TrySetResult(true); }
            public void Dispose() { Release(); listener.Stop(); }
        }
    }
}
