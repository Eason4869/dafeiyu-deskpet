using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace Dafeiyu.Tests
{
    public static class LiveTests
    {
        static int passed;
        public static int Main(){try{Run().GetAwaiter().GetResult();Console.WriteLine("Live tests: "+passed+" passed");return 0;}catch(Exception e){Console.WriteLine("FAIL Live: "+e.Message);return 1;}}
        static async Task Run()
        {
            var socket=new FakeSocket();var mic=new FakeMic();var speaker=new FakeOutput();var session=new GeminiLiveSession(()=>socket,()=>mic,()=>speaker);
            string user="",reply="";session.TurnCompleted+=(a,b)=>{user=a;reply=b;};
            var cfg=new PetSettings();cfg.Live.ApiKey="dummy-key";socket.Push("{\"setupComplete\":{}}");await session.StartAsync(cfg,"E包人格");
            var setup=(Dictionary<string,object>)((Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(socket.Sent[0]))["setup"];
            Check(((string)setup["model"]).StartsWith("models/") && socket.Sent[0].Contains("E包人格") && socket.Sent[0].Contains("inputAudioTranscription") && socket.Sent[0].Contains("speechConfig"),"Live setup includes persona, voice and transcriptions");
            Check(mic.Started && session.IsActive,"microphone starts after setup acknowledgement");
            mic.Emit(new byte[]{1,2,3,4});await Eventually(()=>socket.Sent.Count==2);Check(socket.Sent[1].Contains("audio/pcm;rate=16000") && socket.Sent[1].Contains("AQIDBA=="),"raw 16k PCM is sent to realtimeInput");
            socket.Push("{\"serverContent\":{\"inputTranscription\":{\"text\":\"你好\"},\"outputTranscription\":{\"text\":\"在呢\"},\"modelTurn\":{\"parts\":[{\"inlineData\":{\"mimeType\":\"audio/pcm;rate=24000\",\"data\":\"AQIDBA==\"}}]}}}");
            await Eventually(()=>speaker.Chunks==1);Check(speaker.Rate==24000 && speaker.Volume==50,"returned 24k PCM is played at saved volume");
            session.SetVolume(20);Check(speaker.Volume==20,"Live volume changes immediately");
            socket.Push("{\"serverContent\":{\"turnComplete\":true}}");await Eventually(()=>reply.Length>0);Check(user=="你好" && reply=="在呢","Live turn commits input and output subtitles together");
            socket.Push("{\"serverContent\":{\"interrupted\":true}}");await Eventually(()=>speaker.Interruptions>0);Check(speaker.Interruptions==1,"barge-in clears queued playback");
            await session.StopAsync();Check(!session.IsActive && mic.Disposed && speaker.Disposed && socket.Disposed,"stop releases socket, microphone and speaker");session.Dispose();
            socket=new FakeSocket();mic=new FakeMic();speaker=new FakeOutput();session=new GeminiLiveSession(()=>socket,()=>mic,()=>speaker);socket.Push("{\"error\":{\"code\":401,\"message\":\"dummy-key provider private details\"}}");
            try{await session.StartAsync(cfg,"角色");throw new Exception("server failure accepted");}catch(ApiException e){Check(!e.Message.Contains("dummy-key") && !e.Message.Contains("private") && e.Message.Contains("Live"),"Live service error is actionable without leaking response details");}
            await session.StopAsync();Check(!mic.Started && socket.Disposed,"failed setup never starts a microphone");session.Dispose();
            using(var server=new WebSocketFixture())
            {
                mic=new FakeMic();speaker=new FakeOutput();session=new GeminiLiveSession(()=>new GeminiLiveSocket(),()=>mic,()=>speaker);
                var local=new PetSettings();local.Live=new ConnectionSettings{BaseUrl=server.Url,Model="live-test",ApiKey="dummy-key"};string output="";session.TurnCompleted+=(a,b)=>output=b;
                await session.StartAsync(local,"真实握手角色");mic.Emit(new byte[]{1,2,3,4});await Eventually(()=>output=="真实 WS 回复");
                Check(server.Setup.Contains("真实握手角色") && server.Audio.Contains("audio/pcm;rate=16000") && speaker.Chunks==1,"actual WebSocket upgrade, fragmented JSON and bidirectional PCM work");await session.StopAsync();Check(mic.Disposed && speaker.Disposed,"actual WebSocket cancellation frees audio devices");session.Dispose();
            }
            foreach(string url in new[]{"ws://example.com/live","wss://user:pass@example.com/live","wss://example.com/live?key=secret"})
            {try{GeminiLiveSocket.Endpoint(new ConnectionSettings{BaseUrl=url,Model="live",ApiKey="dummy-key"});throw new Exception("unsafe Live URL accepted");}catch(ApiException){passed++;}}
        }
        static async Task Eventually(Func<bool> test){for(int i=0;i<100;i++){if(test())return;await Task.Delay(20);}throw new Exception("event timed out");}
        static void Check(bool yes,string description){if(!yes)throw new Exception(description);passed++;Console.WriteLine("PASS "+description);}
        sealed class FakeSocket : ILiveSocket
        {
            readonly Queue<string> received=new Queue<string>();readonly SemaphoreSlim ready=new SemaphoreSlim(0);public readonly List<string> Sent=new List<string>();public bool Disposed;
            public void Push(string value){lock(received)received.Enqueue(value);ready.Release();}
            public Task ConnectAsync(ConnectionSettings cfg,CancellationToken token){return Task.FromResult(0);}
            public Task SendTextAsync(string text,CancellationToken token){lock(Sent)Sent.Add(text);return Task.FromResult(0);}
            public async Task<string> ReceiveTextAsync(CancellationToken token){await ready.WaitAsync(token);lock(received)return received.Dequeue();}
            public void Abort(){} public void Dispose(){Disposed=true;}
        }
        sealed class FakeMic : IPcmMicrophone
        {public event Action<byte[]> PcmAvailable;public event Action<string> Failed;public bool Started,Disposed;public void Start(){Started=true;}public void Emit(byte[] bytes){PcmAvailable(bytes);}public void Dispose(){Disposed=true;}}
        sealed class FakeOutput : ILiveAudioOutput
        {public int Chunks,Rate,Volume,Interruptions;public bool Disposed;public void Enqueue(byte[] bytes,int rate){Chunks++;Rate=rate;}public void Interrupt(){Interruptions++;}public void SetVolume(int value){Volume=value;}public void Dispose(){Disposed=true;}}
        internal sealed class WebSocketFixture : IDisposable
        {
            readonly System.Net.Sockets.TcpListener listener;System.Net.Sockets.TcpClient client;public string Url,Setup,Audio;readonly Task work;
            public WebSocketFixture(){listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();Url="ws://127.0.0.1:"+((System.Net.IPEndPoint)listener.LocalEndpoint).Port+"/live";work=Task.Run((Func<Task>)Serve);}
            async Task Serve()
            {
                try
                {
                    client=await listener.AcceptTcpClientAsync();client.ReceiveTimeout=4000;using(var stream=client.GetStream())
                    {
                        var bytes=new System.IO.MemoryStream();int marker=0;byte[] end={13,10,13,10};while(marker<4){int next=stream.ReadByte();if(next<0 || bytes.Length>32768)throw new System.IO.IOException();bytes.WriteByte((byte)next);marker=next==end[marker]?marker+1:0;}
                        string headers=System.Text.Encoding.ASCII.GetString(bytes.ToArray());string key="";foreach(string line in headers.Split(new[]{"\r\n"},StringSplitOptions.None))if(line.StartsWith("Sec-WebSocket-Key:",StringComparison.OrdinalIgnoreCase))key=line.Substring(18).Trim();
                        string accept;using(var sha=System.Security.Cryptography.SHA1.Create())accept=Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                        byte[] response=System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "+accept+"\r\n\r\n");await stream.WriteAsync(response,0,response.Length);Setup=await ReadFrame(stream);
                        byte[] setup=System.Text.Encoding.UTF8.GetBytes("{\"setupComplete\":{},\"note\":\"鱼\"}");int split=setup.Length-3;await Frame(stream,1,false,setup,0,split);await Frame(stream,0,true,setup,split,setup.Length-split);
                        Audio=await ReadFrame(stream);byte[] turn=System.Text.Encoding.UTF8.GetBytes("{\"serverContent\":{\"inputTranscription\":{\"text\":\"真实麦克风输入\"},\"outputTranscription\":{\"text\":\"真实 WS 回复\"},\"modelTurn\":{\"parts\":[{\"inlineData\":{\"mimeType\":\"audio/pcm;rate=24000\",\"data\":\"AQIDBA==\"}}]},\"turnComplete\":true}}");await Frame(stream,1,true,turn,0,turn.Length);byte[] buffer=new byte[8192];while(await stream.ReadAsync(buffer,0,buffer.Length)>0){}
                    }
                }
                catch(System.IO.IOException){}catch(System.Net.Sockets.SocketException){}catch(ObjectDisposedException){}
            }
            static async Task<string> ReadFrame(System.IO.Stream stream)
            {
                byte[] first=await Read(stream,2);int length=first[1]&127;if(length==126){var ext=await Read(stream,2);length=ext[0]*256+ext[1];}if(length>65535)throw new System.IO.IOException();bool masked=(first[1]&128)!=0;byte[] mask=masked?await Read(stream,4):new byte[4];byte[] body=await Read(stream,length);if(masked)for(int i=0;i<body.Length;i++)body[i]^=mask[i%4];return System.Text.Encoding.UTF8.GetString(body);
            }
            static async Task<byte[]> Read(System.IO.Stream stream,int length){byte[] bytes=new byte[length];int offset=0;while(offset<length){int count=await stream.ReadAsync(bytes,offset,length-offset);if(count==0)throw new System.IO.IOException();offset+=count;}return bytes;}
            static async Task Frame(System.IO.Stream stream,int opcode,bool final,byte[] bytes,int offset,int length){var header=length<126?new byte[]{(byte)(opcode|(final?128:0)),(byte)length}:new byte[]{(byte)(opcode|(final?128:0)),126,(byte)(length>>8),(byte)length};await stream.WriteAsync(header,0,header.Length);await stream.WriteAsync(bytes,offset,length);}
            public void Dispose(){listener.Stop();if(client!=null)client.Close();}
        }
    }
}
