using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Dafeiyu
{
    public sealed class AboutPanel : Grid,IDisposable
    {
        readonly PetWindow pet;readonly TextBox repository;readonly CheckBox proxy;readonly TextBlock status;readonly Button check,download,openFolder,cancel;readonly ProgressBar progress;readonly GitHubUpdates updates=new GitHubUpdates();
        CancellationTokenSource cancellation;UpdateRelease release;bool disposed,busy;string downloaded;
        public AboutPanel(PetWindow companion)
        {
            pet=companion;Margin=new Thickness(24,22,24,20);var panel=new StackPanel();Children.Add(Ui.Scroll(panel));var heading=Ui.Text("关于桌面伙伴",25);heading.FontWeight=FontWeights.SemiBold;panel.Children.Add(heading);
            var avatars=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,14,0,6)};foreach(string id in new[]{"dafeiyu","e-bao"}){var image=new Image{Source=SkinCatalog.GetPreview(pet.Catalog.Find(id)),Width=105,Height=118,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,12,0)};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);avatars.Children.Add(image);}panel.Children.Add(avatars);
            panel.Children.Add(Ui.Text("版本 "+Assembly.GetExecutingAssembly().GetName().Version.ToString(3),13,Ui.Blue));panel.Children.Add(Ui.Text("作者 · 彧晟Eason",16));panel.Children.Add(Ui.Text("大肥鱼与 E包 · 高清桌面伙伴\n陪你写代码、做设计，也陪你整理思路。",12,Ui.Muted));
            repository=Ui.Field(pet.Settings.RepositoryUrl);repository.Name="RepositoryUrl";Ui.Label(panel,"GitHub 仓库",repository);panel.Children.Add(Ui.Text("当前地址先用 OWNER/REPO 占位。建立仓库并发布 Release 后，填入实际地址即可检查与下载更新。",11,Ui.Muted));
            var links=new WrapPanel{Margin=new Thickness(0,10,0,0)};var save=Ui.Button("保存仓库地址");save.Name="SaveRepository";save.Click+=delegate {SaveRepository();};links.Children.Add(save);var open=Ui.Button("打开 GitHub 仓库");open.Name="OpenRepository";open.Click+=delegate {try{GitHubUpdates.Repository(repository.Text);Open(repository.Text);}catch(ApiException e){status.Text=e.Message;}};links.Children.Add(open);panel.Children.Add(links);
            proxy=Ui.Toggle("更新通过本机代理 127.0.0.1:7890",pet.Settings.UpdateUseProxy,v=>{});proxy.Name="UpdateProxy";panel.Children.Add(proxy);
            var buttons=new WrapPanel{Margin=new Thickness(0,16,0,0)};check=Ui.Button("检查更新",true);check.Name="CheckUpdates";check.Click+=async delegate {if(!SaveRepository())return;Begin();try{release=await updates.CheckAsync(pet.Settings.RepositoryUrl,pet.Settings.UpdateUseProxy,cancellation.Token);var current=Assembly.GetExecutingAssembly().GetName().Version;status.Text=release.IsNewerThan(current)?"发现新版本 "+release.VersionTag+(release.AssetName==null?"，尚无 Windows 下载附件。":" · "+release.AssetName):"当前版本已是最新。GitHub："+release.VersionTag;}catch(Exception e){status.Text=e is OperationCanceledException?"检查已取消。":e.Message;release=null;}finally{End();}};buttons.Children.Add(check);
            download=Ui.Button("下载更新包");download.Name="DownloadUpdate";download.IsEnabled=false;download.Click+=async delegate {Begin();try{var reporter=new Progress<double>(v=>{if(!disposed)progress.Value=v;});downloaded=await updates.DownloadAsync(release,Path.Combine(pet.Store.DirectoryPath,"downloads"),pet.Settings.UpdateUseProxy,reporter,cancellation.Token);status.Text="下载完成。打开下载文件夹，退出旧版桌宠后运行安装包；ZIP 请先解压。";}catch(Exception e){status.Text=e is OperationCanceledException?"下载已取消。":e.Message;}finally{End();}};buttons.Children.Add(download);
            cancel=Ui.Button("取消");cancel.Name="CancelUpdate";cancel.IsEnabled=false;cancel.Click+=delegate {if(cancellation!=null)cancellation.Cancel();};buttons.Children.Add(cancel);panel.Children.Add(buttons);
            progress=new ProgressBar{Name="UpdateProgress",Minimum=0,Maximum=100,Height=5,Margin=new Thickness(0,15,0,0),Visibility=Visibility.Collapsed};panel.Children.Add(progress);status=Ui.Text("更新来源：GitHub Releases。支持 EXE、MSI 和 ZIP；优先选择安装包。",12,Ui.Muted);status.Name="UpdateStatus";status.Margin=new Thickness(0,12,0,0);panel.Children.Add(status);
            openFolder=Ui.Button("打开下载文件夹");openFolder.Name="OpenDownloads";openFolder.IsEnabled=false;openFolder.Margin=new Thickness(0,14,0,0);openFolder.HorizontalAlignment=HorizontalAlignment.Left;openFolder.Click+=delegate {Open(Path.GetDirectoryName(downloaded));};panel.Children.Add(openFolder);repository.TextChanged+=delegate {if(!busy){release=null;download.IsEnabled=false;}};
        }
        bool SaveRepository(){try{if(repository.Text.Trim()!="https://github.com/OWNER/REPO")GitHubUpdates.Repository(repository.Text);pet.Settings.RepositoryUrl=repository.Text.Trim();pet.Settings.UpdateUseProxy=proxy.IsChecked==true;pet.SaveSettings();status.Text=String.IsNullOrEmpty(pet.Notice)?"仓库地址已保存。":pet.Notice;return String.IsNullOrEmpty(pet.Notice);}catch(ApiException e){status.Text=e.Message;return false;}}
        void Begin(){busy=true;cancellation=new CancellationTokenSource();check.IsEnabled=download.IsEnabled=repository.IsEnabled=proxy.IsEnabled=false;cancel.IsEnabled=true;progress.Value=0;progress.Visibility=Visibility.Visible;status.Text="正在访问 GitHub…";}
        void End(){busy=false;if(cancellation!=null){cancellation.Dispose();cancellation=null;}if(disposed)return;check.IsEnabled=repository.IsEnabled=proxy.IsEnabled=true;download.IsEnabled=release!=null && release.AssetName!=null && release.IsNewerThan(Assembly.GetExecutingAssembly().GetName().Version);openFolder.IsEnabled=downloaded!=null;cancel.IsEnabled=false;progress.Visibility=Visibility.Collapsed;}
        void Open(string path){try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(System.ComponentModel.Win32Exception){status.Text="Windows 暂时无法打开此地址或文件夹。";}}
        public void Dispose(){if(disposed)return;disposed=true;if(cancellation!=null)cancellation.Cancel();updates.Dispose();}
    }
}
