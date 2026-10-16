using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace Dafeiyu.Tests
{
    public static class ProxyTests
    {
        static int passed;
        public static int Main(){try{Run().GetAwaiter().GetResult();Console.WriteLine("Proxy tests: "+passed+" passed");return 0;}catch(Exception e){Console.WriteLine("FAIL proxy: "+e.Message);return 1;}}
        static async Task Run()
        {
            var defaults=new PetSettings();
            Check(defaults.Text.ProxyAddress=="http://127.0.0.1:7890" && defaults.Transcription.ProxyAddress==defaults.Text.ProxyAddress && defaults.Speech.ProxyAddress==defaults.Text.ProxyAddress && defaults.Live.ProxyAddress==defaults.Text.ProxyAddress && defaults.UpdateProxyAddress==defaults.Text.ProxyAddress,"all services default to 127.0.0.1:7890");
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"proxy-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var store=new SettingsStore(Path.Combine(folder,"settings.json"));File.WriteAllText(Path.Combine(folder,"settings.json"),"{\"Text\":{\"UseProxy\":true},\"UpdateUseProxy\":true}");var legacy=store.Load();Check(legacy.Text.UseProxy && legacy.Text.ProxyAddress==defaults.Text.ProxyAddress && legacy.UpdateProxyAddress==defaults.Text.ProxyAddress,"legacy proxy settings retain enabled state and gain the default address");
            defaults.Text.ProxyAddress="127.0.0.1:17890";defaults.Transcription.ProxyAddress="localhost:17891";defaults.Speech.ProxyAddress="http://127.0.0.1:17892";defaults.Live.ProxyAddress="[::1]:17893";defaults.UpdateProxyAddress="localhost:17894";store.Save(defaults);var loaded=store.Load().Copy();Check(loaded.Text.ProxyAddress==defaults.Text.ProxyAddress && loaded.Transcription.ProxyAddress==defaults.Transcription.ProxyAddress && loaded.Speech.ProxyAddress==defaults.Speech.ProxyAddress && loaded.Live.ProxyAddress==defaults.Live.ProxyAddress && loaded.UpdateProxyAddress==defaults.UpdateProxyAddress,"independent proxy addresses persist and survive snapshot copies");
            loaded.Text.UseProxy=true;loaded.VoiceUsesTextService=loaded.SpeechUsesTextService=true;loaded.Normalize();Check(!loaded.VoiceUsesTextService && !loaded.SpeechUsesTextService && loaded.TranscriptionConnection().ProxyAddress==loaded.Text.ProxyAddress && loaded.SpeechConnection().ProxyAddress==loaded.Text.ProxyAddress && loaded.SpeechConnection().UseProxy,"old shared proxy settings migrate into independent ASR and TTS proxies");
            Check(ProxyConfiguration.Endpoint("127.0.0.1:17890").AbsoluteUri=="http://127.0.0.1:17890/" && ProxyConfiguration.Endpoint("http://proxy.example:8080").Host=="proxy.example" && ProxyConfiguration.Endpoint("[::1]:7890").Port==7890,"proxy addresses accept host:port, full HTTP URL and IPv6");
            foreach(string bad in new[]{"socks5://127.0.0.1:7890","https://127.0.0.1:7890","http://user:secret@localhost:7890","http://localhost:7890/path","http://localhost:7890?key=secret","http://localhost:7890/#fragment","localhost:0","localhost:99999"}){try{ProxyConfiguration.Endpoint(bad);throw new Exception("invalid proxy accepted");}catch(InvalidOperationException){}}
            Check(true,"invalid protocols, credentials, paths and ports are rejected safely");
            using(var api=new ApiClient(TimeSpan.FromSeconds(5)))
            {
                using(var proxy=new ApiTests.Fixture("",502))
                {var cfg=new ConnectionSettings{BaseUrl="https://first-proxy.invalid/v1",Model="chat",ApiKey="dummy-key",UseProxy=true,ProxyAddress=proxy.Url.Replace("http://","")};try{await api.ChatAsync(cfg,new[]{new ChatMessage("user","hello")},CancellationToken.None);throw new Exception("rejected proxy accepted");}catch(ApiException){}Check(proxy.Path=="first-proxy.invalid:443" && proxy.Authorization==null,"HTTP uses the configured bare proxy address without exposing the key before TLS");}
                using(var proxy=new ApiTests.Fixture("",502))
                {var cfg=new ConnectionSettings{BaseUrl="https://edited-proxy.invalid/v1",Model="chat",ApiKey="dummy-key",UseProxy=true,ProxyAddress=proxy.Url};try{await api.ChatAsync(cfg,new[]{new ChatMessage("user","hello")},CancellationToken.None);throw new Exception("rejected proxy accepted");}catch(ApiException){}Check(proxy.Path=="edited-proxy.invalid:443","editing the proxy port takes effect in the same API client");}
                using(var server=new ApiTests.Fixture("{\"choices\":[{\"message\":{\"content\":\"direct\"}}]}"))
                {var cfg=new ConnectionSettings{BaseUrl=server.Url,Model="chat",UseProxy=false,ProxyAddress="unused invalid proxy"};Check(await api.ChatAsync(cfg,new[]{new ChatMessage("user","hello")},CancellationToken.None)=="direct","disabled proxy uses a direct connection");}
            }
            using(var proxy=new ApiTests.Fixture("",502))using(var socket=new GeminiLiveSocket())using(var cancel=new CancellationTokenSource(5000))
            {try{await socket.ConnectAsync(new ConnectionSettings{BaseUrl="wss://custom-live.invalid/ws",Model="live",ApiKey="dummy-key",UseProxy=true,ProxyAddress=proxy.Url},cancel.Token);throw new Exception("rejected Live proxy accepted");}catch(System.Net.WebSockets.WebSocketException){}catch(OperationCanceledException){}Check(proxy.Path=="custom-live.invalid:443" && proxy.Authorization==null,"Live WSS connects through its configured proxy");}
            using(var proxy=new ApiTests.Fixture("",502))using(var updater=new GitHubUpdates())using(var cancel=new CancellationTokenSource(5000))
            {try{await updater.CheckAsync(ReleaseInfo.RepositoryUrl,true,proxy.Url,cancel.Token);throw new Exception("rejected update proxy accepted");}catch(ApiException){}catch(OperationCanceledException){}Check(proxy.Path=="api.github.com:443","release lookup uses the editable update proxy");}
            using(var proxy=new ApiTests.Fixture("",502))using(var updater=new GitHubUpdates())using(var cancel=new CancellationTokenSource(5000))
            {try{await updater.DownloadAsync(new UpdateRelease{AssetName="DeskPet-Setup.exe",AssetUrl="https://github.com/test/pet/releases/download/v1/DeskPet-Setup.exe",Size=4},folder,true,proxy.Url,null,cancel.Token);throw new Exception("rejected download proxy accepted");}catch(ApiException){}catch(OperationCanceledException){}Check(proxy.Path=="github.com:443","installer downloads use the editable update proxy");}
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
    }
}
