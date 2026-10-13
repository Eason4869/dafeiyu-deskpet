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
                settings.SelectPage(6);Pump();var repository=Find<TextBlock>(settings,"RepositoryUrl");
                Check(repository!=null && repository.Inlines.OfType<Hyperlink>().Any(link=>link.NavigateUri.AbsoluteUri=="https://github.com/Eason4869/dafeiyu-deskpet"),"repository is a fixed clickable hyperlink");
                Check(Find<TextBox>(settings,"RepositoryUrl")==null && Find<Button>(settings,"SaveRepository")==null,"repository has no edit or save controls");
                Check(!Find<Button>(settings,"DownloadUpdate").IsEnabled,"download waits for an explicit successful version check");
                Check(All<TextBlock>(settings).Any(item=>item.Text.Contains("彧晟Eason")),"author attribution is present");Save(settings,Path.Combine(folder,"about.png"));
                Console.WriteLine("Release UI: "+passed+" passed, "+failed+" failed");return failed==0?0:1;
            }
            finally{pet.Close();app.Shutdown();}
        }
        static void Check(bool value,string label){if(value){passed++;Console.WriteLine("PASS "+label);}else{failed++;Console.WriteLine("FAIL "+label);}}
        static T Find<T>(DependencyObject root,string name)where T:FrameworkElement{return All<T>(root).FirstOrDefault(item=>item.Name==name);}
        static System.Collections.Generic.IEnumerable<T> All<T>(DependencyObject root)where T:DependencyObject{if(root is T)yield return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in All<T>(VisualTreeHelper.GetChild(root,i)))yield return item;}
        static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        static void Save(FrameworkElement target,string path){var image=new RenderTargetBitmap((int)target.ActualWidth,(int)target.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(target);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path))png.Save(file);}
    }
}
