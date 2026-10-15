using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Dafeiyu.Tests
{
    public static class UiSmoke
    {
        private static int passed;
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
        [STAThread] public static int Main(string[] args)
        {
            PetWindow pet = null;
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-smoke"); Directory.CreateDirectory(folder);
                // Foreground/fullscreen policy has its own tests; a lock screen
                // must not hide the animation and settings fixture mid-check.
                var settings = new PetSettings { FollowCursor=false, HideOnFullscreen=false };
                var store = new SettingsStore(Path.Combine(folder,"settings.json"));
                var atlas = new SpriteAtlas();
                Require(atlas.CellWidth == 480 && atlas.CellHeight == 520,"HD source frames are 480 x 520");
                int frames = 0;
                for(int row=0;row<11;row++) for(int col=0;col<(row<9 ? AnimationPlayer.Counts[row]:8);col++)
                { Require(atlas.GetFrame(row,col).PixelWidth == 480,"HD frame " + row + "/" + col); frames++; }
                Require(frames == 73,"nine actions and sixteen gaze frames load");
                pet = new PetWindow(atlas,settings,store,null);
                pet.Show(); Pump(.5);
                Require(pet.AllowsTransparency && pet.WindowStyle == WindowStyle.None && !pet.ShowInTaskbar,"transparent frameless desktop window");
                Save(pet,Path.Combine(folder,"pet-transparent.png"));
                byte[] pixel=new byte[4]; Render(pet).CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);
                Require(pixel[3] == 0,"rendered corner is actually transparent");
                pet.PlayAction("jumping",.7); Pump(.2); Require(pet.Animation.State == "jumping","jump action starts");
                Pump(.7); Require(pet.Animation.State == "idle","jump action returns to idle");
                pet.SetPaused(true); int frozen=pet.Animation.Frame; Pump(.4);
                Require(pet.Animation.Frame == frozen,"pause freezes animation"); pet.SetPaused(false);
                pet.HidePet(); Pump(.1); Require(!pet.IsVisible,"tray hide works");
                pet.RestorePet(); Pump(.2); Require(pet.IsVisible,"tray restore works");
                pet.ShowSettings(); Pump(.3);
                var preferences=app.Windows.OfType<SettingsWindow>().Single();
                preferences.Width=700;preferences.Height=430;preferences.SelectPage(6);Pump(.15);var nav=Find<ListBox>(preferences,"SettingsNavigation");nav.ScrollIntoView(nav.Items[3]);Pump(.1);var about=(ListBoxItem)nav.Items[3];Rect navBounds=about.TransformToAncestor(preferences).TransformBounds(new Rect(about.RenderSize));Require(navBounds.Bottom<=preferences.ActualHeight,"About navigation remains reachable at minimum window height");preferences.Width=850;preferences.Height=790;preferences.SelectPage(0);Pump(.1);
                preferences.SelectCompanionSection(1);Pump(.1);
                var slider=Find<Slider>(preferences,"SizeSlider");
                Require(slider != null,"settings contains size control");
                slider.Value=1.5; Pump(.2);
                Require(Math.Abs(pet.Width-288)<.01 && Math.Abs(store.Load().Scale-1.5)<.01,"size applies immediately and persists");
                var follow=Find<CheckBox>(preferences,"FollowCursor"); follow.IsChecked=true; Pump(.1);
                Require(pet.Settings.FollowCursor && store.Load().FollowCursor,"cursor setting applies and persists");
                follow.IsChecked=false;
                Find<Button>(preferences,"ReturnHome").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(.2);
                IntPtr handle=new WindowInteropHelper(pet).Handle;
                Require(DesktopInterop.WorkArea(handle).Contains(DesktopInterop.Bounds(handle)),"return home keeps pet inside screen work area");
                Rect nativeBounds=DesktopInterop.Bounds(handle);
                Require(SendMessage(handle,0x84,IntPtr.Zero,ScreenPoint(nativeBounds.Left+2,nativeBounds.Top+2)).ToInt32()==-1,"transparent pixels pass native hit testing");
                Save(preferences,Path.Combine(folder,"settings-window.png"));
                preferences.SelectCompanionSection(0);Pump(.1);
                var picker=Find<ComboBox>(preferences,"SkinPicker"); picker.SelectedItem=pet.Catalog.Find("e-bao"); Pump(.3);
                Require(pet.Skin.Id=="e-bao" && store.Load().SkinId=="e-bao","E包 switches through the settings control and persists");
                Require(pet.Catalog.Find("e-bao").Load().FrameMultiplier==1,"E包 uses original real-frame artwork");
                Save(pet,Path.Combine(folder,"pet-e-bao.png")); Save(preferences,Path.Combine(folder,"settings-e-bao.png"));
                string project=Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).FullName;
                var imported=pet.Catalog.Import(Path.Combine(project,"assets","spritesheet.png"));
                Require(File.Exists(imported.Source) && pet.SelectSkin(imported),"PNG design import copies and activates a working pet");
                Require(store.Load().SkinId==imported.Id,"imported selection persists");
                var catalogAgain=new SkinCatalog(store.DirectoryPath);
                Require(catalogAgain.Find(imported.Id).Id==imported.Id,"imported design survives catalog reload");
                string pack=Path.Combine(folder,"pet.json");
                File.WriteAllText(pack,"{\"version\":1,\"name\":\"测试伙伴\",\"atlas\":\"preview.png\"}");
                File.Copy(Path.Combine(project,"assets","e-bao-spritesheet.png"),Path.Combine(folder,"preview.png"),true);
                var fromJson=pet.Catalog.Import(pack);
                Require(fromJson.Name=="测试伙伴" && pet.SelectSkin(fromJson),"named pet.json design imports and activates");
                int beforeImport=pet.Catalog.Skins.Count; string previousSkin=pet.Skin.Id;
                File.WriteAllText(pack,"{\"version\":1,\"name\":\"invalid\",\"atlas\":\"../preview.png\"}");
                bool rejected=false; try { pet.Catalog.Import(pack); } catch(InvalidOperationException) { rejected=true; }
                Require(rejected && pet.Catalog.Skins.Count==beforeImport && pet.Skin.Id==previousSkin,"invalid path is rejected without changing current pet");
                string bad=Path.Combine(folder,"bad.png"); File.WriteAllText(bad,"not a png"); rejected=false;
                try { pet.Catalog.Import(bad); } catch(InvalidOperationException) { rejected=true; }
                Require(rejected && pet.Catalog.Skins.Count==beforeImport,"invalid image does not add a broken preset");
                Require(pet.SelectSkin(pet.Catalog.Find("dafeiyu")),"built-in appearance restores after import");
                var failures=new System.Collections.Generic.List<string>();
                Regression("damaged PNG import keeps the active pet",delegate
                {
                    string truncated=Path.Combine(folder,"truncated.png");
                    byte[] header=File.ReadAllBytes(Path.Combine(project,"assets","spritesheet.png")).Take(33).ToArray(); File.WriteAllBytes(truncated,header);
                    bool refused=false; try { pet.Catalog.Import(truncated); } catch(InvalidOperationException) { refused=true; }
                    if(!refused || pet.Skin.Id!="dafeiyu") throw new Exception("truncated PNG was not rejected safely");
                },failures);
                Regression("return home ends indefinite running",delegate
                {
                    pet.PlayAction("running-right",0); pet.ReturnHome(); Pump(.08);
                    if(pet.Animation.State!="idle") throw new Exception("return home left infinite running state");
                },failures);
                Regression("resizing while walking stays inside the work area",delegate
                {
                    pet.SetPaused(true); pet.Settings.Scale=.75; pet.ApplySettings(); Pump(.15); pet.ReturnHome();
                    Rect oldBounds=DesktopInterop.Bounds(handle),area=DesktopInterop.WorkArea(handle);
                    DesktopInterop.Move(handle,new Point(area.Right-oldBounds.Width-8,oldBounds.Top));
                    typeof(PetWindow).GetField("walking",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,true);
                    typeof(PetWindow).GetField("walkTarget",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,area.Right-oldBounds.Width);
                    pet.Animation.Play("running-right",0); pet.Settings.Wander=true; pet.Settings.Scale=2.25; pet.ApplySettings(); Pump(.15);
                    pet.SetPaused(false); Pump(.4);
                    if(!DesktopInterop.WorkArea(handle).Contains(DesktopInterop.Bounds(handle))) throw new Exception("stale walk target moved the enlarged pet outside the screen");
                    pet.Settings.Wander=false; pet.Settings.Scale=1.5; pet.ApplySettings(); Pump(.15); pet.ReturnHome();
                },failures);
                Regression("right-click option changes refresh open settings",delegate
                {
                    pet.Settings.FollowCursor=true; pet.ApplySettings(); Pump(.08);
                    if(follow.IsChecked!=true) throw new Exception("follow cursor control remained stale");
                    pet.Settings.FollowCursor=false; pet.ApplySettings();
                },failures);
                if(failures.Count>0) throw new Exception("Regression failures: "+String.Join(", ",failures));
                pet.PlayAction("waving",1); Pump(.2); Save(pet,Path.Combine(folder,"pet-waving.png"));
                pet.Close(); Pump(.2);
                Require(app.Windows.Count == 0,"exit closes settings and pet windows");
                Console.WriteLine("UI smoke: " + passed + " passed"); return 0;
            }
            catch(Exception e) { Console.WriteLine("FAIL UI smoke: " + e); if(pet != null) pet.Close(); return 1; }
        }
        private static void Require(bool condition,string name) { if(!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
        private static void Regression(string name,Action test,System.Collections.Generic.List<string> failures)
        { try { test(); Require(true,name); } catch(Exception e) { failures.Add(name); Console.WriteLine("REGRESSION FAIL "+name+": "+e.GetType().Name+" "+e.Message); } }
        private static IntPtr ScreenPoint(double x,double y) { return new IntPtr(((int)Math.Round(y)<<16)|((int)Math.Round(x)&0xffff)); }
        private static T Find<T>(DependencyObject root,string name) where T:FrameworkElement { return All<T>(root).FirstOrDefault(item=>item.Name == name); }
        private static System.Collections.Generic.IEnumerable<T> All<T>(DependencyObject root) where T:DependencyObject
        { if(root is T) yield return (T)root; for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(T child in All<T>(VisualTreeHelper.GetChild(root,i))) yield return child; }
        public static void Pump(double seconds)
        {
            var frame=new DispatcherFrame(); var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(Math.Max(.01,seconds)) };
            timer.Tick+=delegate { timer.Stop(); frame.Continue=false; }; timer.Start(); Dispatcher.PushFrame(frame);
        }
        private static RenderTargetBitmap Render(FrameworkElement window)
        { var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32); bitmap.Render(window); return bitmap; }
        private static void Save(FrameworkElement window,string path)
        { var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Render(window))); using(var stream=File.Create(path)) encoder.Save(stream); }
    }
}
