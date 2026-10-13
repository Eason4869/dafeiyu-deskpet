using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace Dafeiyu
{
    public sealed class UpdateRelease
    {
        public string VersionTag,PageUrl,AssetName,AssetUrl,Digest;public long Size;
        public bool IsNewerThan(Version installed){Version next;string tag=(VersionTag??"").TrimStart('v','V');return Version.TryParse(tag,out next) && NormalizeVersion(next)>NormalizeVersion(installed);}
        static Version NormalizeVersion(Version value){return new Version(value.Major,value.Minor,Math.Max(0,value.Build),Math.Max(0,value.Revision));}
    }
    public sealed class GitHubUpdates : IDisposable
    {
        readonly Uri apiRoot;readonly HttpClient direct,proxy;
        public GitHubUpdates() : this(new Uri("https://api.github.com/")){}
        public GitHubUpdates(Uri root){if(root==null || !(root.Scheme=="https" || root.IsLoopback))throw new ArgumentException("Invalid API root.");apiRoot=root;direct=Create(false);proxy=Create(true);}
        static HttpClient Create(bool useProxy){var client=new HttpClient(new HttpClientHandler{UseProxy=useProxy,Proxy=useProxy?new WebProxy("http://127.0.0.1:7890",false):null,AllowAutoRedirect=true});client.Timeout=Timeout.InfiniteTimeSpan;client.DefaultRequestHeaders.UserAgent.ParseAdd("DafeiyuDeskPet/1.2");return client;}
        public static string Repository(string address)
        {
            Uri uri;if(!Uri.TryCreate((address??"").Trim(),UriKind.Absolute,out uri) || uri.Scheme!="https" || uri.Host!="github.com" || !uri.IsDefaultPort || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0)throw new ApiException("请填写 https://github.com/作者/仓库 格式的地址。");
            string path=uri.AbsolutePath.Trim('/');if(path.EndsWith(".git",StringComparison.OrdinalIgnoreCase))path=path.Substring(0,path.Length-4);
            if(!Regex.IsMatch(path,@"^[A-Za-z0-9][A-Za-z0-9_.-]*/[A-Za-z0-9][A-Za-z0-9_.-]*$") || path.Equals("OWNER/REPO",StringComparison.OrdinalIgnoreCase))throw new ApiException("仓库地址目前是占位符。建立 GitHub 仓库并发布 Release 后，在这里填写实际地址。");return path;
        }
        public async Task<UpdateRelease> CheckAsync(string repository,bool useProxy,CancellationToken cancel)
        {
            string repo=Repository(repository);
            using(var bounded=CancellationTokenSource.CreateLinkedTokenSource(cancel))using(var request=new HttpRequestMessage(HttpMethod.Get,new Uri(apiRoot,"repos/"+repo+"/releases/latest")))
            {
                bounded.CancelAfter(TimeSpan.FromSeconds(30));var token=bounded.Token;request.Headers.Accept.ParseAdd("application/vnd.github+json");
                try
                {
                    using(var response=await (useProxy?proxy:direct).SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false))using(token.Register(response.Dispose))
                    {
                        if(response.StatusCode==HttpStatusCode.NotFound)throw new ApiException("未找到公开的正式 Release。请检查仓库地址，并发布版本及安装包附件。");
                        if(response.StatusCode==HttpStatusCode.Forbidden || (int)response.StatusCode==429)throw new ApiException("GitHub 检查更新受到频率限制，请稍后再试。");
                        if(!response.IsSuccessStatusCode)throw new ApiException("GitHub 暂时无法查询更新（HTTP "+(int)response.StatusCode+"）。");
                        using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var memory=new MemoryStream())
                        {
                            byte[] block=new byte[8192];int count;while((count=await stream.ReadAsync(block,0,block.Length,token).ConfigureAwait(false))>0){if(memory.Length+count>1024*1024)throw new ApiException("GitHub 版本信息过大。");memory.Write(block,0,count);}
                            var json=new JavaScriptSerializer{MaxJsonLength=1024*1024}.DeserializeObject(Encoding.UTF8.GetString(memory.ToArray())) as Dictionary<string,object>;if(json==null)throw new FormatException();
                            var release=new UpdateRelease{VersionTag=(string)json["tag_name"],PageUrl=(string)json["html_url"]};Uri page;if(!Uri.TryCreate(release.PageUrl,UriKind.Absolute,out page) || page.Host!="github.com" || page.Scheme!="https")throw new FormatException();int best=-1;
                            foreach(object raw in (object[])json["assets"])
                            {
                                var asset=(Dictionary<string,object>)raw;string name=(string)asset["name"];int score=Score(name);if(score<0 || score<=best)continue;string url=(string)asset["browser_download_url"];if(!SafeAsset(url))continue;long size=Convert.ToInt64(asset["size"]);if(size<=0 || size>300L*1024*1024)continue;
                                best=score;release.AssetName=name;release.AssetUrl=url;release.Size=size;object digest;if(asset.TryGetValue("digest",out digest))release.Digest=digest as string;
                            }
                            if(String.IsNullOrWhiteSpace(release.VersionTag))throw new FormatException();return release;
                        }
                    }
                }
                catch(Exception e){throw SafeError(e,cancel,token,"检查更新");}
            }
        }
        static int Score(string name)
        {
            string lower=(name??"").ToLowerInvariant();if(lower.Contains("source") || lower.Contains("源码") || lower.Contains("arm64") || lower.Contains("外观") || lower.Contains("skin"))return -1;
            string ext=Path.GetExtension(lower);int score=ext==".msi"?40:ext==".exe"?30:ext==".zip"?10:-1;if(score<0)return -1;if(lower.Contains("setup") || lower.Contains("install") || lower.Contains("安装"))score+=20;if(Environment.Is64BitOperatingSystem && lower.Contains("x64"))score+=5;return score;
        }
        bool SafeAsset(string address){Uri uri;if(!Uri.TryCreate(address,UriKind.Absolute,out uri) || uri.UserInfo.Length>0 || uri.Fragment.Length>0)return false;if(apiRoot.IsLoopback && uri.IsLoopback && uri.Scheme=="http")return true;return uri.Scheme=="https" && uri.Host=="github.com" && uri.AbsolutePath.Contains("/releases/download/");}
        public async Task<string> DownloadAsync(UpdateRelease release,string folder,bool useProxy,IProgress<double> progress,CancellationToken cancel)
        {
            if(release==null || !SafeAsset(release.AssetUrl) || Score(release.AssetName)<0 || release.AssetName!=Path.GetFileName(release.AssetName) || release.AssetName.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || release.AssetName.Length>150)throw new ApiException("没有可下载的 Windows EXE、MSI 或 ZIP 附件。");
            Directory.CreateDirectory(folder);string target=Path.Combine(folder,release.AssetName);if(File.Exists(target))target=Path.Combine(folder,Path.GetFileNameWithoutExtension(release.AssetName)+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+Path.GetExtension(release.AssetName));string partial=target+"."+Guid.NewGuid().ToString("N")+".part";bool complete=false,ownsPartial=false;
            using(var bounded=CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                bounded.CancelAfter(TimeSpan.FromMinutes(10));var token=bounded.Token;
                try
                {
                    using(var response=await (useProxy?proxy:direct).GetAsync(release.AssetUrl,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false))using(token.Register(response.Dispose))
                    {
                        if(!response.IsSuccessStatusCode)throw new ApiException("更新包下载失败（HTTP "+(int)response.StatusCode+"）。");
                        using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var file=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,8192,true))
                        {
                            ownsPartial=true;
                            byte[] block=new byte[32768];int count;long total=0;while((count=await stream.ReadAsync(block,0,block.Length,token).ConfigureAwait(false))>0){total+=count;if(total>300L*1024*1024)throw new ApiException("更新包超过 300 MB 限制。");await file.WriteAsync(block,0,count,token).ConfigureAwait(false);if(progress!=null && release.Size>0)progress.Report(Math.Min(99,total*100.0/release.Size));}
                            if(total==0 || (release.Size>0 && total!=release.Size))throw new ApiException("更新包不完整，请重新下载。");file.Flush(true);
                        }
                    }
                    token.ThrowIfCancellationRequested();if(!String.IsNullOrEmpty(release.Digest) && release.Digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)){using(var file=File.OpenRead(partial))using(var hash=SHA256.Create()){string actual=BitConverter.ToString(hash.ComputeHash(file)).Replace("-","");if(!String.Equals(actual,release.Digest.Substring(7),StringComparison.OrdinalIgnoreCase))throw new ApiException("更新包校验失败，请重新下载。");}}
                    File.Move(partial,target);complete=true;if(progress!=null)progress.Report(100);return target;
                }
                catch(Exception e){throw SafeError(e,cancel,token,"下载更新");}
                finally{if(ownsPartial && !complete && File.Exists(partial))File.Delete(partial);}
            }
        }
        static Exception SafeError(Exception e,CancellationToken cancel,CancellationToken timeout,string action){if(cancel.IsCancellationRequested)return new OperationCanceledException(cancel);if(timeout.IsCancellationRequested)return new ApiException(action+"超时，请重试或检查代理。");if(e is ApiException)return e;return new ApiException(action+"未完成，请检查 GitHub 地址、网络、代理及下载目录权限。");}
        public void Dispose(){direct.Dispose();proxy.Dispose();}
    }
}
