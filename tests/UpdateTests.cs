using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace Dafeiyu.Tests
{
    public static class UpdateTests
    {
        static int passed;
        public static int Main(){try{Run().GetAwaiter().GetResult();Console.WriteLine("Update tests: "+passed+" passed");return 0;}catch(Exception e){Console.WriteLine("FAIL Update: "+e.Message);return 1;}}
        static async Task Run()
        {
            Check(GitHubUpdates.Repository("https://github.com/Eason4869/deskpet")=="Eason4869/deskpet","repository URL resolves owner and repo");
            Check(!new UpdateRelease{VersionTag="v1.2.0.0"}.IsNewerThan(new Version(1,2,0,0)) && !new UpdateRelease{VersionTag="v1.2.0"}.IsNewerThan(new Version(1,2,0,0)) && new UpdateRelease{VersionTag="v1.2.0.1"}.IsNewerThan(new Version(1,2,0,0)),"three- and four-part release versions compare consistently");
            foreach(string url in new[]{"https://github.com/OWNER/REPO","https://example.com/o/r","https://user:pass@github.com/o/r","https://github.com/o/r/releases","https://github.com/o/r?key=secret"}){try{GitHubUpdates.Repository(url);throw new Exception("invalid repo accepted");}catch(ApiException){passed++;}}
            string json="{\"tag_name\":\"v1.3.0\",\"html_url\":\"https://github.com/test/pet/releases/tag/v1.3.0\",\"assets\":[{\"name\":\"deskpet-source.zip\",\"size\":10,\"browser_download_url\":\"https://github.com/test/pet/releases/download/v1.3.0/deskpet-source.zip\"},{\"name\":\"DeskPet-Setup.exe\",\"size\":4,\"browser_download_url\":\"https://github.com/test/pet/releases/download/v1.3.0/DeskPet-Setup.exe\"}]}";
            using(var server=new ApiTests.Fixture(json))using(var updater=new GitHubUpdates(new Uri(server.Url+"/")))
            {var release=await updater.CheckAsync("https://github.com/test/pet",false,CancellationToken.None);Check(release.AssetName=="DeskPet-Setup.exe" && release.IsNewerThan(new Version(1,2,0)),"latest release prefers installer and compares versions");Check(server.Path=="/repos/test/pet/releases/latest" && server.Authorization==null,"public release lookup sends no model key");}
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"update-tests",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);string sentinel=Path.Combine(root,"existing.zip.part");File.WriteAllText(sentinel,"previous owner's bytes");
            using(var server=new ApiTests.Fixture("",500))using(var updater=new GitHubUpdates(new Uri(server.Url+"/")))
            {try{await updater.DownloadAsync(new UpdateRelease{AssetName="existing.zip",AssetUrl=server.Url+"/failure",Size=4},root,false,null,CancellationToken.None);throw new Exception("failed request accepted");}catch(ApiException){}Check(File.Exists(sentinel) && File.ReadAllText(sentinel)=="previous owner's bytes","failed download preserves a pre-existing unowned partial file");}
            using(var server=new ApiTests.Fixture(new byte[]{1,2,3,4},"application/octet-stream"))using(var updater=new GitHubUpdates(new Uri(server.Url+"/")))
            {var release=new UpdateRelease{VersionTag="v1.3.0",AssetName="DeskPet-Setup.exe",AssetUrl=server.Url+"/setup",Size=4};string path=await updater.DownloadAsync(release,root,false,null,CancellationToken.None);Check(File.ReadAllBytes(path).Length==4 && Directory.GetFiles(root,"DeskPet-Setup.exe*.part").Length==0,"download is complete before atomic publication");}
            using(var server=new ApiTests.Fixture(new byte[]{1,2,3,4},"application/octet-stream"))using(var updater=new GitHubUpdates(new Uri(server.Url+"/")))
            {server.DelayBodyMilliseconds=1000;using(var cancel=new CancellationTokenSource()){var task=updater.DownloadAsync(new UpdateRelease{AssetName="cancel.zip",AssetUrl=server.Url+"/cancel",Size=4},root,false,null,cancel.Token);await server.HeadersSent;cancel.Cancel();try{await task;throw new Exception("cancel ignored");}catch(OperationCanceledException){}Check(!File.Exists(Path.Combine(root,"cancel.zip")) && Directory.GetFiles(root,"cancel.zip*.part").Length==0,"canceled download removes the partial file");}}
            using(var server=new ApiTests.Fixture("null"))using(var updater=new GitHubUpdates(new Uri(server.Url+"/"))){try{await updater.CheckAsync("https://github.com/test/pet",false,CancellationToken.None);throw new Exception("bad release accepted");}catch(ApiException){passed++;}}
        }
        static void Check(bool result,string text){if(!result)throw new Exception(text);passed++;Console.WriteLine("PASS "+text);}
    }
}
