using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
namespace Dafeiyu.Tests
{
    public static class UpdateTests
    {
        static int passed;
        public static int Main(){try{Run().GetAwaiter().GetResult();Console.WriteLine("Update tests: "+passed+" passed");return 0;}catch(Exception e){Console.WriteLine("FAIL Update: "+e.Message);return 1;}}
        static async Task Run()
        {
            var previousTls=System.Net.ServicePointManager.SecurityProtocol;
            try{System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls;using(var standalone=new GitHubUpdates()){Check((System.Net.ServicePointManager.SecurityProtocol & System.Net.SecurityProtocolType.Tls12)!=0,"updates enable TLS 1.2 before any chat client is created");}}
            finally{System.Net.ServicePointManager.SecurityProtocol=previousTls;}
            Check(GitHubUpdates.Repository("https://github.com/Eason4869/deskpet")=="Eason4869/deskpet","repository URL resolves owner and repo");
            Check(typeof(GitHubUpdates).GetMethod("CheckAsync",new[]{typeof(string),typeof(bool),typeof(string),typeof(bool),typeof(CancellationToken)})!=null,"便携版可选择运行包，不改变其他安装记录");
            Check(!new UpdateRelease{VersionTag="v1.2.0.0"}.IsNewerThan(new Version(1,2,0,0)) && !new UpdateRelease{VersionTag="v1.2.0"}.IsNewerThan(new Version(1,2,0,0)) && new UpdateRelease{VersionTag="v1.2.0.1"}.IsNewerThan(new Version(1,2,0,0)),"three- and four-part release versions compare consistently");
            foreach(string url in new[]{"https://github.com/OWNER/REPO","https://example.com/o/r","https://user:pass@github.com/o/r","https://github.com/o/r/releases","https://github.com/o/r?key=secret"}){try{GitHubUpdates.Repository(url);throw new Exception("invalid repo accepted");}catch(ApiException){passed++;}}
            await PlatformAssetChecks();
            string json="{\"tag_name\":\"v1.3.0\",\"html_url\":\"https://github.com/test/pet/releases/tag/v1.3.0\",\"assets\":[{\"name\":\"deskpet-source.zip\",\"size\":10,\"browser_download_url\":\"https://github.com/test/pet/releases/download/v1.3.0/deskpet-source.zip\"},{\"name\":\"DeskPet-Setup.exe\",\"size\":4,\"browser_download_url\":\"https://github.com/test/pet/releases/download/v1.3.0/DeskPet-Setup.exe\"}]}";
            using(var server=new ApiTests.Fixture(json))using(var updater=new GitHubUpdates(new Uri(server.Url+"/")))
            {var release=await updater.CheckAsync("https://github.com/test/pet",false,CancellationToken.None);Check(release.AssetName=="DeskPet-Setup.exe" && release.IsNewerThan(new Version(1,2,0)),"latest release prefers installer and compares versions");Check(server.Path=="/repos/test/pet/releases/latest" && server.Authorization==null,"public release lookup sends no model key");}
            string portableJson=json.Replace("{\"name\":\"deskpet-source.zip\"","{\"name\":\"DeskPet-Windows.zip\"");
            using(var server=new ApiTests.Fixture(portableJson))using(var updater=new GitHubUpdates(new Uri(server.Url+"/")))
            {var release=await updater.CheckAsync("https://github.com/test/pet",false,ProxyConfiguration.DefaultAddress,true,CancellationToken.None);Check(release.AssetName=="DeskPet-Windows.zip","portable installation selects the runtime ZIP instead of running an installer");}
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"update-tests",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);string sentinel=Path.Combine(root,"existing.zip.part");File.WriteAllText(sentinel,"previous owner's bytes");
            using(var server=new ApiTests.Fixture("",500))using(var updater=new GitHubUpdates(new Uri(server.Url+"/")))
            {try{await updater.DownloadAsync(new UpdateRelease{AssetName="existing.zip",AssetUrl=server.Url+"/failure",Size=4},root,false,null,CancellationToken.None);throw new Exception("failed request accepted");}catch(ApiException){}Check(File.Exists(sentinel) && File.ReadAllText(sentinel)=="previous owner's bytes","failed download preserves a pre-existing unowned partial file");}
            using(var server=new ApiTests.Fixture(new byte[]{1,2,3,4},"application/octet-stream"))using(var updater=new GitHubUpdates(new Uri(server.Url+"/")))
            {var release=new UpdateRelease{VersionTag="v1.3.0",AssetName="DeskPet-Setup.exe",AssetUrl=server.Url+"/setup",Size=4};string path=await updater.DownloadAsync(release,root,false,null,CancellationToken.None);Check(File.ReadAllBytes(path).Length==4 && Directory.GetFiles(root,"DeskPet-Setup.exe*.part").Length==0,"download is complete before atomic publication");}
            using(var server=new ApiTests.Fixture(new byte[]{1,2,3,4},"application/octet-stream"))using(var updater=new GitHubUpdates(new Uri(server.Url+"/")))
            {server.DelayBodyMilliseconds=1000;using(var cancel=new CancellationTokenSource()){var task=updater.DownloadAsync(new UpdateRelease{AssetName="cancel.zip",AssetUrl=server.Url+"/cancel",Size=4},root,false,null,cancel.Token);await server.HeadersSent;cancel.Cancel();try{await task;throw new Exception("cancel ignored");}catch(OperationCanceledException){}Check(!File.Exists(Path.Combine(root,"cancel.zip")) && Directory.GetFiles(root,"cancel.zip*.part").Length==0,"canceled download removes the partial file");}}
            using(var server=new ApiTests.Fixture("null"))using(var updater=new GitHubUpdates(new Uri(server.Url+"/"))){try{await updater.CheckAsync("https://github.com/test/pet",false,CancellationToken.None);throw new Exception("bad release accepted");}catch(ApiException){passed++;}}
            await MirrorChecks();
        }
        static async Task PlatformAssetChecks()
        {
            string[] excluded={"DeskPet-Mac-x64.zip","DeskPet-MACOS-Setup-x64.zip","DeskPet-darwin-x64.zip","DeskPet-osx-x64.zip","DeskPet-macOS.dmg","DeskPet-dmg.zip","DeskPet-HighFPS-Template-v2-0.5.3.zip","DeskPet-创作模板.zip","DeskPet-Four-Skins-0.5.3.zip","DeskPet-Source-0.5.3.zip"};
            foreach(string asset in excluded)using(var handler=new AssetHandler(new[]{asset}))using(var updater=new GitHubUpdates(new Uri("https://api.github.com/"),handler)) {
                foreach(bool portable in new[]{false,true}) {
                    var release=await updater.CheckAsync(ReleaseInfo.RepositoryUrl,false,ProxyConfiguration.DefaultAddress,portable,CancellationToken.None);
                    Check(release.AssetName==null,"non-Windows attachment is never an update (portable="+portable+"): "+asset);
                }
            }
            foreach(bool reverse in new[]{false,true}) {
                var mixed=excluded.Concat(new[]{"DeskPet-Windows-0.5.3.zip","DeskPet-Setup-0.5.3.exe"});
                using(var handler=new AssetHandler((reverse?mixed.Reverse():mixed).ToArray()))using(var updater=new GitHubUpdates(new Uri("https://api.github.com/"),handler)) {
                    Check((await updater.CheckAsync(ReleaseInfo.RepositoryUrl,false,ProxyConfiguration.DefaultAddress,false,CancellationToken.None)).AssetName=="DeskPet-Setup-0.5.3.exe","mixed release keeps Windows installer regardless of asset order "+reverse);
                    Check((await updater.CheckAsync(ReleaseInfo.RepositoryUrl,false,ProxyConfiguration.DefaultAddress,true,CancellationToken.None)).AssetName=="DeskPet-Windows-0.5.3.zip","mixed release keeps Windows portable ZIP regardless of asset order "+reverse);
                }
            }
        }
        sealed class AssetHandler:HttpMessageHandler
        {
            readonly string json;
            public AssetHandler(string[] names) {
                json=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {
                    tag_name="v0.5.3",html_url="https://github.com/Eason4869/deskpet/releases/tag/v0.5.3",
                    assets=names.Select(name=>new {name=name,size=4,browser_download_url="https://github.com/Eason4869/deskpet/releases/download/v0.5.3/"+Uri.EscapeDataString(name)}).ToArray()
                });
            }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
            {return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)});}
        }
        static async Task MirrorChecks()
        {
            foreach(string value in new[]{"http://mirror.example","https://user:password@mirror.example","https://mirror.example/?token=secret","https://mirror.example/#fragment","https://mirror.example/https://github.com/o/r"})
            {try{GitHubUpdates.MirrorPrefix(value);throw new Exception("invalid mirror accepted");}catch(InvalidOperationException){passed++;}}
            var defaults=new PetSettings();Check(!defaults.UpdateUseMirror && !defaults.UpdateUseProxy,"network acceleration defaults to direct");
            var cfg=defaults.Copy();cfg.UpdateUseMirror=true;cfg.UpdateMirrorAddress="https://mirror.example/prefix";cfg.UpdateUseProxy=true;cfg.Normalize();Check(!cfg.UpdateUseProxy && cfg.Copy().UpdateMirrorAddress==cfg.UpdateMirrorAddress && cfg.Copy().UpdateUseMirror,"mirror settings copy and enforce exclusive routing");
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"mirror-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var store=new SettingsStore(Path.Combine(folder,"settings.json"));store.Save(cfg);Check(store.Load().UpdateUseMirror && store.Load().UpdateMirrorAddress==cfg.UpdateMirrorAddress,"mirror mode and custom address survive reload");
            foreach(string mode in new[]{"success","failure","corrupt"})using(var handler=new MirrorHandler{Mode=mode})using(var updater=new GitHubUpdates(new Uri("https://api.github.com/"),handler))
            {
                var notices=new RouteCapture();updater.ConfigureMirror(true,"https://mirror.example/prefix",notices);
                var release=await updater.CheckAsync(ReleaseInfo.RepositoryUrl,false,CancellationToken.None);
                Check(handler.Requests[0].Host=="api.github.com","mirror keeps version and digest lookup on official API: "+mode);
                string result=await updater.DownloadAsync(release,Path.Combine(folder,mode),false,null,CancellationToken.None);
                Check(File.ReadAllBytes(result).SequenceEqual(MirrorHandler.Bytes) && !Directory.GetFiles(Path.GetDirectoryName(result),"*.part").Any(),"verified final bytes and no partial files: "+mode);
                Check(handler.Requests[1].AbsoluteUri=="https://mirror.example/prefix/"+release.AssetUrl && !handler.SentAuthorization,"mirror prefixes canonical asset URL without credentials: "+mode);
                Check(mode=="success"?handler.Requests.Count==2:handler.Requests.Last().Host=="github.com" && notices.Last.Contains("回退"),"mirror routing and fallback are visible: "+mode);
                if(mode=="success"){await updater.TestMirrorAsync("https://mirror.example/prefix",CancellationToken.None);Check(handler.SawRange,"connectivity probe requests only a small range and validates executable header");}
            }
            using(var cancellation=new CancellationTokenSource())using(var handler=new MirrorHandler{Cancel=cancellation})using(var updater=new GitHubUpdates(new Uri("https://api.github.com/"),handler))
            {
                updater.ConfigureMirror(true,"https://mirror.example",null);var release=await updater.CheckAsync(ReleaseInfo.RepositoryUrl,false,CancellationToken.None);
                try{await updater.DownloadAsync(release,Path.Combine(folder,"cancel"),false,null,cancellation.Token);throw new Exception("cancel ignored");}catch(OperationCanceledException){}
                Check(handler.Requests.Count==2,"explicit cancellation never falls back or starts another download");
            }
            using(var handler=new MirrorHandler())using(var updater=new GitHubUpdates(new Uri("https://api.github.com/"),handler))
            {
                updater.ConfigureMirror(true,"https://mirror.example",null);var release=await updater.CheckAsync(ReleaseInfo.RepositoryUrl,false,CancellationToken.None);release.Digest=null;
                try{await updater.DownloadAsync(release,folder,false,null,CancellationToken.None);throw new Exception("unsigned mirror download accepted");}catch(ApiException){}
                Check(handler.Requests.Count==1,"mirror refuses a package without an official SHA-256 digest");
            }
        }
        sealed class RouteCapture:IProgress<string>{public string Last;public void Report(string value){Last=value;}}
        sealed class MirrorHandler:HttpMessageHandler
        {
            public static readonly byte[] Bytes={77,90,3,4};public string Mode="success";public CancellationTokenSource Cancel;public bool SentAuthorization,SawRange;public readonly List<Uri> Requests=new List<Uri>();
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
            {
                Requests.Add(request.RequestUri);SentAuthorization|=request.Headers.Authorization!=null;SawRange|=request.Headers.Range!=null;
                if(request.RequestUri.Host=="api.github.com")
                {
                    string digest;using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(Bytes)).Replace("-","").ToLowerInvariant();
                    string json="{\"tag_name\":\"v0.5.2\",\"html_url\":\"https://github.com/Eason4869/deskpet/releases/tag/v0.5.2\",\"assets\":[{\"name\":\"DeskPet-Setup-0.5.2.exe\",\"size\":4,\"digest\":\"sha256:"+digest+"\",\"browser_download_url\":\"https://github.com/Eason4869/deskpet/releases/download/v0.5.2/DeskPet-Setup-0.5.2.exe\"}]}";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)});
                }
                bool mirrored=request.RequestUri.Host=="mirror.example";
                if(mirrored && Cancel!=null){Cancel.Cancel();token.ThrowIfCancellationRequested();}
                return Task.FromResult(new HttpResponseMessage(mirrored && Mode=="failure"?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK){Content=new ByteArrayContent(mirrored && Mode=="corrupt"?new byte[]{9,9,9,9}:Bytes)});
            }
        }
        static void Check(bool result,string text){if(!result)throw new Exception(text);passed++;Console.WriteLine("PASS "+text);}
    }
}
