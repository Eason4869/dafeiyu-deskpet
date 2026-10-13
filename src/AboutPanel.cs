using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Dafeiyu
{
    public sealed class AboutPanel : Grid,IDisposable
    {
        readonly PetWindow pet;
        readonly UpdateCoordinator updates;
        readonly CheckBox proxy,automatic;
        readonly TextBox proxyAddress;
        readonly TextBlock status,progressLabel;
        readonly Button check,cancel,openFolder;
        readonly ProgressBar progress;
        bool disposed;
        public bool HasUnsavedChanges {get{return ProxyConfiguration.Normalize(proxyAddress.Text)!=ProxyConfiguration.Normalize(pet.Settings.UpdateProxyAddress);}}
        public AboutPanel(PetWindow companion)
        {
            pet=companion;updates=pet.Updates;Margin=new Thickness(26,24,26,20);
            var panel=new StackPanel();Children.Add(Ui.Scroll(panel));
            var hero=new DockPanel{Margin=new Thickness(0,0,0,22)};
            var logo=new Image{Source=Ui.ApplicationIcon(),Width=72,Height=72,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,18,0)};RenderOptions.SetBitmapScalingMode(logo,BitmapScalingMode.HighQuality);DockPanel.SetDock(logo,Dock.Left);hero.Children.Add(logo);
            var brand=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var title=Ui.Text("桌面伙伴",27);title.FontWeight=FontWeights.SemiBold;brand.Children.Add(title);brand.Children.Add(Ui.Text("版本 "+ReleaseInfo.DisplayVersion+"   ·   "+ReleaseInfo.Author,12,Ui.Muted));hero.Children.Add(brand);panel.Children.Add(hero);
            panel.Children.Add(Ui.Text("写代码、做设计，或聊聊今天的小想法。",13,Ui.Muted));
            var update=new StackPanel();var heading=Ui.Text("版本更新",17);heading.FontWeight=FontWeights.SemiBold;update.Children.Add(heading);
            automatic=Ui.Toggle("启动时自动检查并更新",pet.Settings.AutoUpdate,v=>{pet.Settings.AutoUpdate=v;updates.AutomaticChanged();});automatic.Name="AutoUpdate";update.Children.Add(automatic);
            update.Children.Add(Ui.Text("启动 30 秒后检查，每 24 小时最多自动检查一次。新版下载并校验后，空闲时自动安装和重启；聊天与 Live 期间会等待。",12,Ui.Muted));
            var actions=new WrapPanel{Margin=new Thickness(0,16,0,0)};
            check=Ui.Button("检查并更新",true);check.Name="CheckUpdates";check.Click+=async delegate {if(SaveProxy())await updates.CheckAndUpdateAsync(true);};actions.Children.Add(check);
            cancel=Ui.Button("取消更新");cancel.Name="CancelUpdate";cancel.Click+=delegate {updates.Cancel();};actions.Children.Add(cancel);update.Children.Add(actions);
            progress=new ProgressBar{Name="UpdateProgress",Minimum=0,Maximum=100,Height=6,Margin=new Thickness(0,16,0,0),Visibility=Visibility.Collapsed};update.Children.Add(progress);
            progressLabel=Ui.Text("",11,Ui.Blue);progressLabel.Margin=new Thickness(0,5,0,0);update.Children.Add(progressLabel);
            status=Ui.Text("",12);status.Name="UpdateStatus";status.Margin=new Thickness(0,12,0,0);update.Children.Add(status);
            var updateCard=Ui.Card(update);updateCard.Margin=new Thickness(0,20,0,18);panel.Children.Add(updateCard);

            var network=new Expander{Header="更新网络与下载文件",IsExpanded=true,Margin=new Thickness(0,0,0,20)};
            var networkBody=new StackPanel{Margin=new Thickness(0,8,0,0)};
            proxy=Ui.Toggle("更新使用代理",pet.Settings.UpdateUseProxy,v=>{pet.Settings.UpdateUseProxy=v;pet.SaveSettings();});proxy.Name="UpdateProxy";networkBody.Children.Add(proxy);
            proxyAddress=Ui.Field(ProxyConfiguration.Normalize(pet.Settings.UpdateProxyAddress));proxyAddress.Name="UpdateProxyAddress";proxyAddress.LostFocus+=delegate {SaveProxy();};Ui.Label(networkBody,"代理地址",proxyAddress);networkBody.Children.Add(Ui.Text("默认 127.0.0.1:7890；修改后移开焦点即可保存。",11,Ui.Muted));
            openFolder=Ui.Button("打开更新文件夹");openFolder.Name="OpenDownloads";openFolder.Margin=new Thickness(0,12,0,0);openFolder.HorizontalAlignment=HorizontalAlignment.Left;openFolder.Click+=delegate {Open(Path.Combine(pet.Store.DirectoryPath,"updates"));};networkBody.Children.Add(openFolder);network.Content=networkBody;panel.Children.Add(network);

            var repository=Ui.Text("",12,Ui.Blue);repository.Name="RepositoryUrl";
            var link=new Hyperlink(new Run(ReleaseInfo.RepositoryUrl)){NavigateUri=new Uri(ReleaseInfo.RepositoryUrl)};link.RequestNavigate+=delegate(object sender,System.Windows.Navigation.RequestNavigateEventArgs args){args.Handled=true;Open(ReleaseInfo.RepositoryUrl);};repository.Inlines.Add(link);
            panel.Children.Add(Ui.Text("项目仓库",12,Ui.Muted));panel.Children.Add(repository);
            var links=new WrapPanel{Margin=new Thickness(0,12,0,0)};var copy=Ui.Button("复制地址");copy.Name="CopyRepository";copy.Click+=delegate {try{Clipboard.SetText(ReleaseInfo.RepositoryUrl);updates.ReportValidation("仓库地址已复制。");}catch(System.Runtime.InteropServices.COMException){updates.ReportValidation("剪贴板正忙，请稍后重试。");}};links.Children.Add(copy);
            var releases=Ui.Button("查看版本记录");releases.Click+=delegate {Open(ReleaseInfo.RepositoryUrl+"/releases");};links.Children.Add(releases);panel.Children.Add(links);
            panel.Children.Add(Ui.Text("更新仅来自上述仓库的正式 Release。安装版原目录升级；便携版替换运行文件，data 始终保留。",11,Ui.Muted));
            updates.Changed+=Refresh;Refresh();
        }
        bool SaveProxy()
        {
            try{string value=ProxyConfiguration.Normalize(proxyAddress.Text);ProxyConfiguration.Endpoint(value);proxyAddress.Text=value;pet.Settings.UpdateProxyAddress=value;pet.SaveSettings();return true;}
            catch(InvalidOperationException e){updates.ReportValidation(e.Message);return false;}
        }
        void Refresh()
        {
            if(disposed)return;check.IsEnabled=!updates.IsBusy;proxy.IsEnabled=proxyAddress.IsEnabled=!updates.IsBusy;cancel.IsEnabled=updates.CanCancel;
            progress.Visibility=updates.IsBusy?Visibility.Visible:Visibility.Collapsed;progress.IsIndeterminate=updates.IsBusy && !updates.IsDownloading && updates.Progress<100;progress.Value=updates.Progress;
            progressLabel.Text=updates.IsDownloading?"下载进度 "+Math.Round(updates.Progress)+"%":"";status.Text=updates.Status;openFolder.IsEnabled=Directory.Exists(Path.Combine(pet.Store.DirectoryPath,"updates"));
        }
        void Open(string address){try{Process.Start(new ProcessStartInfo(address){UseShellExecute=true});}catch(System.ComponentModel.Win32Exception){updates.ReportValidation("Windows 暂时无法打开此地址或文件夹。");}}
        public void Dispose(){if(disposed)return;disposed=true;updates.Changed-=Refresh;}
    }
}
