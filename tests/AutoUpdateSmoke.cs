using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Dafeiyu.Tests
{
    public static class AutoUpdateSmoke
    {
        static int passed;
        [STAThread]public static int Main()
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};PetWindow pet=null;
            try
            {
                string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"auto-update-smoke",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
                File.Copy(Assembly.GetExecutingAssembly().Location,Path.Combine(directory,UpdateInstall.ExecutableName));
                pet=new PetWindow(new SpriteAtlas(),new PetSettings{AutoUpdate=false,FollowCursor=false,ReadReplies=false},new SettingsStore(Path.Combine(directory,"data","settings.json")),null);pet.Show();pet.ShowChat();Pump(.2);
                using(var feed=new Feed())using(var controller=new UpdateCoordinator(pet,feed,job=>{throw new Exception("must not apply while chat is visible");},ReleaseInfo.DisplayVersion))
                {
                    Task task=null;pet.Dispatcher.BeginInvoke(new Action(()=>task=controller.CheckAndUpdateAsync(true)));Wait(()=>task!=null && task.IsCompleted,6);task.GetAwaiter().GetResult();Pump(.4);
                    Check(feed.Checks==1 && feed.Downloads==1 && controller.IsBusy && controller.CanCancel,"手动检查自动下载并等待空闲");
                    Check(controller.Status.Contains("对话"),"打开对话窗口时自动安装延期");
                    Check(feed.Repository==ReleaseInfo.RepositoryUrl && feed.Portable,"使用固定仓库且为便携目录选择 ZIP");
                    controller.Cancel();Pump(.1);Check(!controller.IsBusy && !controller.CanCancel && pet.IsVisible,"下载完成后取消，不退出桌宠");
                    feed.DownloadDelay=3000;task=null;pet.Dispatcher.BeginInvoke(new Action(()=>task=controller.CheckAndUpdateAsync(true)));Wait(()=>feed.Downloads==2,3);controller.Cancel();Wait(()=>task.IsCompleted,4);
                    Check(!controller.IsBusy && controller.Status.Contains("取消"),"下载过程中取消，不进入安装交接");
                    feed.DownloadDelay=0;feed.Release.Digest=null;task=null;pet.Dispatcher.BeginInvoke(new Action(()=>task=controller.CheckAndUpdateAsync(true)));Wait(()=>task!=null && task.IsCompleted,4);
                    Check(!controller.IsBusy && feed.Downloads==2 && controller.Status.Contains("验证"),"缺少可信摘要的附件不下载、不执行");
                    feed.Release.VersionTag="v"+ReleaseInfo.DisplayVersion;task=null;pet.Dispatcher.BeginInvoke(new Action(()=>task=controller.CheckAndUpdateAsync(true)));Wait(()=>task!=null && task.IsCompleted,4);
                    Check(controller.Status.Contains("最新") && feed.Downloads==2,"当前版本无需下载");
                    pet.Settings.AutoUpdate=true;pet.Settings.LastUpdateCheckUtcTicks=DateTime.UtcNow.Ticks;int before=feed.Checks;
                    var timer=(DispatcherTimer)typeof(UpdateCoordinator).GetField("startup",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);timer.Interval=TimeSpan.FromMilliseconds(50);controller.StartAutomaticChecks();Pump(.2);
                    Check(feed.Checks==before,"同日不重复后台检查");
                }
                foreach(var chat in app.Windows.OfType<ChatWindow>().ToArray())chat.Close();
                pet.ShowSettings();Pump(.2);var settings=app.Windows.OfType<SettingsWindow>().Single();
                settings.SelectPage(6);Pump(.1);Check(pet.CanApplyUpdate(true),"未编辑的关于页允许手动安装");
                settings.SelectPage(2);Pump(.1);var model=Find<TextBox>(settings,"TextServiceModel");string original=model.Text;model.Text="unsaved-model";
                settings.SelectPage(6);Pump(.1);Check(!pet.CanApplyUpdate(true),"切换到关于页仍保留模型草稿，安装等待保存");
                settings.SelectPage(2);Pump(.1);model=Find<TextBox>(settings,"TextServiceModel");model.Text=original;
                var key=Find<PasswordBox>(settings,"TextServiceKey");key.Password="test-only-unsaved-key";settings.SelectPage(6);Pump(.1);Check(!pet.CanApplyUpdate(true),"密钥草稿也会阻止更新重启");
                settings.SelectPage(2);Pump(.1);key.Password="";settings.SelectPage(1);Pump(.1);var prompt=Find<TextBox>(settings,"PersonaPrompt");string persona=prompt.Text;prompt.Text="未保存人格";
                settings.SelectPage(6);Pump(.1);Check(!pet.CanApplyUpdate(true),"人格草稿跨页面保留时阻止重启");
                settings.SelectPage(1);prompt.Text=persona;settings.SelectPage(6);Pump(.1);Check(pet.CanApplyUpdate(true),"恢复全部草稿后允许手动更新");settings.Close();
                pet.Updates.Dispose();
                using(var feed=new Feed())
                {
                    var ready=new TaskCompletionSource<bool>();UpdateJob handoff=null;
                    using(var controller=new UpdateCoordinator(pet,feed,job=>{handoff=job;return ready.Task;},ReleaseInfo.DisplayVersion))
                    {
                        typeof(PetWindow).GetField("updates",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,controller);
                        Task task=controller.CheckAndUpdateAsync(false);Wait(()=>task.IsCompleted,4);Wait(()=>controller.Status.Contains("秒"),3);
                        var notice=app.Windows.Cast<Window>().FirstOrDefault(w=>w.Title=="伙伴更新" && w.IsVisible);
                        Check(notice!=null && !notice.ShowActivated && Find<Button>(notice,"CancelPendingUpdate")!=null,"后台更新显示不抢焦点的倒计时与取消按钮");
                        var image=new RenderTargetBitmap((int)notice.ActualWidth,(int)notice.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(notice);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"update-notice.png")))png.Save(file);
                        Find<Button>(notice,"CancelPendingUpdate").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(.1);
                        Check(!controller.IsBusy && handoff==null && pet.IsVisible,"气泡取消阻止安装与退出");
                        task=controller.CheckAndUpdateAsync(false);Wait(()=>task.IsCompleted,4);Wait(()=>handoff!=null,8);
                        pet.ShowChat();pet.ShowQuickChat();pet.ShowSettings();Pump(.1);
                        Check(!app.Windows.OfType<ChatWindow>().Any(w=>w.IsVisible) && !app.Windows.OfType<QuickChatWindow>().Any(w=>w.IsVisible) && !app.Windows.OfType<SettingsWindow>().Any(w=>w.IsVisible),"更新交接期间不能开始新的对话或设置编辑");
                        typeof(PetWindow).GetField("sessionLocked",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,true);
                        ready.SetResult(true);Wait(()=>!controller.IsBusy,3);
                        Check(pet.IsVisible && File.Exists(Path.Combine(handoff.WorkDirectory,"abort")),"助手就绪后再次检查活动，锁屏变化会中止交接并保留桌宠");
                        typeof(PetWindow).GetField("sessionLocked",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,false);
                    }
                }
                Console.WriteLine("Auto update smoke: "+passed+" passed");return 0;
            }
            catch(Exception e){Console.WriteLine("FAIL Auto update: "+e.Message);return 1;}
            finally{if(pet!=null)pet.Close();app.Shutdown();}
        }
        static void Check(bool value,string text){if(!value)throw new Exception(text);passed++;Console.WriteLine("PASS "+text);}
        static T Find<T>(DependencyObject root,string name)where T:FrameworkElement{if(root is T && ((T)root).Name==name)return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=Find<T>(VisualTreeHelper.GetChild(root,i),name);if(found!=null)return found;}return null;}
        static void Wait(Func<bool> ready,int seconds){var until=DateTime.UtcNow.AddSeconds(seconds);while(!ready() && DateTime.UtcNow<until)Pump(.05);if(!ready())throw new Exception("timed out waiting for update state");}
        static void Pump(double seconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        sealed class Feed : IUpdateFeed
        {
            readonly byte[] bytes={1,2,3,4};public int Checks,Downloads,DownloadDelay;public string Repository;public bool Portable;
            public UpdateRelease Release;
            public Feed()
            {
                string digest;using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
                var current=new Version(ReleaseInfo.DisplayVersion);
                string next=new Version(current.Major,current.Minor,current.Build+1).ToString();
                Release=new UpdateRelease{VersionTag="v"+next,AssetName="DeskPet-Windows-"+next+".zip",Size=4,Digest="sha256:"+digest,AssetUrl=ReleaseInfo.RepositoryUrl+"/releases/download/v"+next+"/DeskPet-Windows-"+next+".zip"};
            }
            public Task<UpdateRelease> CheckAsync(string repository,bool useProxy,string address,bool portable,CancellationToken token){Checks++;Repository=repository;Portable=portable;return Task.FromResult(Release);}
            public async Task<string> DownloadAsync(UpdateRelease release,string folder,bool proxy,string address,IProgress<double> progress,CancellationToken token)
            {Downloads++;if(DownloadDelay>0)await Task.Delay(DownloadDelay,token);token.ThrowIfCancellationRequested();Directory.CreateDirectory(folder);string path=Path.Combine(folder,release.AssetName);File.WriteAllBytes(path,bytes);progress.Report(100);return path;}
            public void Dispose(){}
        }
    }
}
