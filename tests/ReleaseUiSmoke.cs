using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Dafeiyu.Tests
{
    public static class ReleaseUiSmoke
    {
        static int passed,failed;
        [STAThread]public static int Main()
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"release-ui");Directory.CreateDirectory(folder);
            var pet=new PetWindow(new SpriteAtlas(),new PetSettings{ReadReplies=false,FollowCursor=false},new SettingsStore(Path.Combine(folder,"settings.json")),null);
            try
            {
                pet.Show();pet.ShowSettings();Pump();var settings=app.Windows.OfType<SettingsWindow>().Single();
                Check(Find<CheckBox>(settings,"AutoStart")!=null,"settings expose the autostart switch");Save(settings,Path.Combine(folder,"companion.png"));
                var hello=Find<CheckBox>(settings,"GreetingEnabled");Check(hello!=null && hello.IsChecked==true,"proactive greeting switch is enabled by default");
                var minimum=Find<TextBox>(settings,"GreetingMinMinutes");var maximum=Find<TextBox>(settings,"GreetingMaxMinutes");Check(minimum.Text=="30" && maximum.Text=="60","greeting interval defaults are visible");
                minimum.Text="10";maximum.Text="20";maximum.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));Check(pet.Store.Load().GreetingMinMinutes==10 && pet.Store.Load().GreetingMaxMinutes==20,"edited greeting interval persists immediately");
                minimum.Text="50";maximum.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));Check(pet.Store.Load().GreetingMinMinutes==10,"invalid greeting interval does not overwrite saved settings");minimum.Text="30";maximum.Text="60";maximum.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
                hello.IsChecked=false;Check(!pet.Store.Load().GreetingEnabled,"greetings can be turned off");hello.IsChecked=true;
                Find<Button>(settings,"PreviewGreeting").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();var bubble=app.Windows.OfType<GreetingBubbleWindow>().Single();Check(bubble.IsVisible && !bubble.ShowActivated && settings.IsActive,"greeting preview appears without stealing focus");Save(bubble,Path.Combine(folder,"greeting.png"));
                settings.Close();
                var fullscreen=new Window{Title="DeskPet fullscreen test",WindowStyle=WindowStyle.None,WindowState=WindowState.Maximized,ShowInTaskbar=false,Topmost=true,Background=Brushes.White};
                try{fullscreen.Show();fullscreen.Activate();Pump(1.3);Check(DesktopInterop.ForegroundIsFullScreen(),"fullscreen fixture covers its monitor");Check(!bubble.IsVisible,"an existing greeting is dismissed when fullscreen begins");}finally{fullscreen.Close();}
                pet.PreviewGreeting();Pump();
                Find<Button>(bubble,"GreetingQuickChat").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();Check(!bubble.IsVisible && app.Windows.OfType<QuickChatWindow>().Any(w=>w.IsVisible),"greeting opens quick chat when clicked");foreach(var quick in app.Windows.OfType<QuickChatWindow>().ToArray())quick.Close();
                pet.ShowSettings();Pump();settings=app.Windows.OfType<SettingsWindow>().Single();settings.SelectPage(6);Pump();var repository=Find<TextBlock>(settings,"RepositoryUrl");
                Check(repository!=null && repository.Inlines.OfType<Hyperlink>().Any(link=>link.NavigateUri.AbsoluteUri=="https://github.com/Eason4869/deskpet"),"repository is a fixed clickable hyperlink");
                Check(Find<TextBox>(settings,"RepositoryUrl")==null && Find<Button>(settings,"SaveRepository")==null,"repository has no edit or save controls");
                Check(Find<Button>(settings,"CheckUpdates").IsEnabled && Find<Button>(settings,"DownloadUpdate")==null,"checking updates starts the automatic download and apply flow");
                Check(Find<CheckBox>(settings,"AutoUpdate").IsChecked==true,"startup automatic updates are enabled and configurable");
                Check(All<TextBlock>(settings).Any(item=>item.Text.Contains("彧晟Eason")),"author attribution is present");
                var updateProxy=Find<TextBox>(settings,"UpdateProxyAddress");Check(updateProxy!=null && updateProxy.IsEnabled && updateProxy.Text=="127.0.0.1:7890","update proxy is editable and defaults to 127.0.0.1:7890");
                updateProxy.Text="localhost:17894";updateProxy.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));Check(pet.Store.Load().UpdateProxyAddress=="localhost:17894","edited update proxy persists through the settings UI");
                updateProxy.Text="socks5://localhost:17894";updateProxy.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));Check(pet.Store.Load().UpdateProxyAddress=="localhost:17894" && Find<TextBlock>(settings,"UpdateStatus").Text.Contains("HTTP 代理"),"invalid update proxy shows a clear error without overwriting the saved address");
                updateProxy.Text=ProxyConfiguration.DefaultAddress;updateProxy.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));Save(settings,Path.Combine(folder,"about.png"));
                Console.WriteLine("Release UI: "+passed+" passed, "+failed+" failed");return failed==0?0:1;
            }
            finally{pet.Close();app.Shutdown();}
        }
        static void Check(bool value,string label){if(value){passed++;Console.WriteLine("PASS "+label);}else{failed++;Console.WriteLine("FAIL "+label);}}
        static T Find<T>(DependencyObject root,string name)where T:FrameworkElement{return All<T>(root).FirstOrDefault(item=>item.Name==name);}
        static System.Collections.Generic.IEnumerable<T> All<T>(DependencyObject root)where T:DependencyObject{if(root is T)yield return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in All<T>(VisualTreeHelper.GetChild(root,i)))yield return item;}
        static void Pump(double seconds=.2){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        static void Save(FrameworkElement target,string path){var image=new RenderTargetBitmap((int)target.ActualWidth,(int)target.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(target);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path))png.Save(file);}
    }
}
