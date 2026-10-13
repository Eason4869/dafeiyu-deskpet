using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace Dafeiyu
{
    public sealed class AboutPanel : Grid,IDisposable
    {
        readonly PetWindow pet;readonly CheckBox proxy;readonly TextBlock status;readonly Button check,download,openFolder,cancel;readonly ProgressBar progress;readonly GitHubUpdates updates=new GitHubUpdates();
        CancellationTokenSource cancellation;UpdateRelease release;bool disposed;string downloaded;
        public AboutPanel(PetWindow companion)
        {
            pet=companion;Margin=new Thickness(24,22,24,20);var panel=new StackPanel();Children.Add(Ui.Scroll(panel));var heading=Ui.Text("关于桌面伙伴",25);heading.FontWeight=FontWeights.SemiBold;panel.Children.Add(heading);
            var avatars=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,14,0,6)};foreach(string id in new[]{"dafeiyu","e-bao"}){var image=new Image{Source=SkinCatalog.GetPreview(pet.Catalog.Find(id)),Width=105,Height=118,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,12,0)};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);avatars.Children.Add(image);}panel.Children.Add(avatars);
            panel.Children.Add(Ui.Text("版本 "+Assembly.GetExecutingAssembly().GetName().Version.ToString(3),13,Ui.Blue));panel.Children.Add(Ui.Text("作者 · "+ReleaseInfo.Author,16));panel.Children.Add(Ui.Text("大肥鱼与 E包 · 高清桌面伙伴\n陪你写代码、做设计，也陪你整理思路。",12,Ui.Muted));
            var repository=Ui.Text("",13,Ui.Blue);repository.Name="RepositoryUrl";repository.Margin=new Thickness(0,7,0,8);
            var link=new Hyperlink(new Run(ReleaseInfo.RepositoryUrl)){NavigateUri=new Uri(ReleaseInfo.RepositoryUrl)};link.RequestNavigate+=delegate(object sender,System.Windows.Navigation.RequestNavigateEventArgs args){args.Handled=true;Open(ReleaseInfo.RepositoryUrl);};repository.Inlines.Add(link);Ui.Label(panel,"GitHub 仓库",repository);
            panel.Children.Add(Ui.Text("点击地址在默认浏览器打开。更新始终来自这个仓库的正式 Release。",11,Ui.Muted));
            var copy=Ui.Button("复制仓库地址");copy.Name="CopyRepository";copy.HorizontalAlignment=HorizontalAlignment.Left;copy.Margin=new Thickness(0,10,0,8);copy.Click+=delegate{try{Clipboard.SetText(ReleaseInfo.RepositoryUrl);status.Text="仓库地址已复制。";}catch(System.Runtime.InteropServices.COMException){status.Text="剪贴板正忙，请稍后重试。";}};panel.Children.Add(copy);
            proxy=Ui.Toggle("更新通过本机代理 127.0.0.1:7890",pet.Settings.UpdateUseProxy,v=>{pet.Settings.UpdateUseProxy=v;pet.SaveSettings();});proxy.Name="UpdateProxy";panel.Children.Add(proxy);
            var buttons=new WrapPanel{Margin=new Thickness(0,16,0,0)};check=Ui.Button("检查更新",true);check.Name="CheckUpdates";check.Click+=async delegate {Begin();try{release=await updates.CheckAsync(ReleaseInfo.RepositoryUrl,pet.Settings.UpdateUseProxy,cancellation.Token);var current=Assembly.GetExecutingAssembly().GetName().Version;status.Text=release.IsNewerThan(current)?"发现新版本 "+release.VersionTag+(release.AssetName==null?"，尚无 Windows 下载附件。":" · "+release.AssetName):"当前版本已是最新。GitHub："+release.VersionTag;}catch(Exception e){status.Text=e is OperationCanceledException?"检查已取消。":e.Message;release=null;}finally{End();}};buttons.Children.Add(check);
            download=Ui.Button("下载更新包");download.Name="DownloadUpdate";download.IsEnabled=false;download.Click+=async delegate {Begin();try{var reporter=new Progress<double>(v=>{if(!disposed)progress.Value=v;});downloaded=await updates.DownloadAsync(release,Path.Combine(pet.Store.DirectoryPath,"downloads"),pet.Settings.UpdateUseProxy,reporter,cancellation.Token);status.Text="下载完成。打开下载文件夹，退出旧版桌宠后运行安装包；ZIP 请先解压。";}catch(Exception e){status.Text=e is OperationCanceledException?"下载已取消。":e.Message;}finally{End();}};buttons.Children.Add(download);
            cancel=Ui.Button("取消");cancel.Name="CancelUpdate";cancel.IsEnabled=false;cancel.Click+=delegate {if(cancellation!=null)cancellation.Cancel();};buttons.Children.Add(cancel);panel.Children.Add(buttons);
            progress=new ProgressBar{Name="UpdateProgress",Minimum=0,Maximum=100,Height=5,Margin=new Thickness(0,15,0,0),Visibility=Visibility.Collapsed};panel.Children.Add(progress);status=Ui.Text("更新来源：GitHub Releases。支持 EXE、MSI 和 ZIP；优先选择安装包。",12,Ui.Muted);status.Name="UpdateStatus";status.Margin=new Thickness(0,12,0,0);panel.Children.Add(status);
            openFolder=Ui.Button("打开下载文件夹");openFolder.Name="OpenDownloads";openFolder.IsEnabled=false;openFolder.Margin=new Thickness(0,14,0,0);openFolder.HorizontalAlignment=HorizontalAlignment.Left;openFolder.Click+=delegate {Open(Path.GetDirectoryName(downloaded));};panel.Children.Add(openFolder);
        }
        void Begin(){cancellation=new CancellationTokenSource();check.IsEnabled=download.IsEnabled=proxy.IsEnabled=false;cancel.IsEnabled=true;progress.Value=0;progress.Visibility=Visibility.Visible;status.Text="正在访问 GitHub…";}
        void End(){if(cancellation!=null){cancellation.Dispose();cancellation=null;}if(disposed)return;check.IsEnabled=proxy.IsEnabled=true;download.IsEnabled=release!=null && release.AssetName!=null && release.IsNewerThan(Assembly.GetExecutingAssembly().GetName().Version);openFolder.IsEnabled=downloaded!=null;cancel.IsEnabled=false;progress.Visibility=Visibility.Collapsed;}
        void Open(string path){try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(System.ComponentModel.Win32Exception){status.Text="Windows 暂时无法打开此地址或文件夹。";}}
        public void Dispose(){if(disposed)return;disposed=true;if(cancellation!=null)cancellation.Cancel();updates.Dispose();}
    }
}
