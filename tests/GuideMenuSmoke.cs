using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Dafeiyu.Tests
{
    public static class GuideMenuSmoke
    {
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
        [StructLayout(LayoutKind.Sequential)] struct NativePoint{public int X,Y;}
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(NativePoint point);
        static int passed;
        [STAThread] public static int Main()
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};PetWindow pet=null;Process blank=null;
            Point cursor=DesktopInterop.Cursor();
            try
            {
                blank=Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MenuBlankFixture.exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});Pump(.3);
                var settings=new PetSettings{SkinId="e-bao",Scale=1.25,X=600,Y=400,Topmost=true,HideOnFullscreen=true,FollowCursor=true,Wander=true,
                    AutoUpdate=false,GreetingEnabled=false,OnboardingState="deferred",OnboardingStep=4};
                foreach(string key in settings.Hotkeys.Keys.ToArray())settings.Hotkeys[key]="";
                string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"guide-menu-"+Guid.NewGuid().ToString("N"));
                pet=new PetWindow(null,settings,new SettingsStore(Path.Combine(folder,"settings.json")),null);
                pet.Show();Pump(.2);pet.ShowOnboarding();Pump(.5);
                var guide=app.Windows.OfType<OnboardingWindow>().Single();
                Check(guide.Step==4,"resume directly at right-click lesson with current user display settings");
                int opened=0,closed=0;
                pet.ContextMenu.Opened+=delegate{opened++;};
                pet.ContextMenu.Closed+=delegate{closed++;};
                Point target=OpaquePoint(pet);
                Console.WriteLine("INFO native target="+target+", visible="+pet.IsVisible+", autoHidden="+pet.AutoHidden+", guide="+guide.IsVisible+", hitPet="+(WindowFromPoint(new NativePoint{X=(int)target.X,Y=(int)target.Y})==new WindowInteropHelper(pet).Handle));
                SetCursorPos((int)target.X,(int)target.Y);Pump(.1);
                Console.WriteLine("INFO cursor="+DesktopInterop.Cursor()+", petVisible="+pet.IsVisible);
                mouse_event(8,0,0,0,UIntPtr.Zero);Pump(.25);mouse_event(16,0,0,0,UIntPtr.Zero);Pump(.8);
                Check(opened>0,"native right-click reaches and opens the pet menu");
                Check(pet.ContextMenu.IsOpen,"native right-button release leaves guided menu open");
                var menu=pet.ContextMenu;
                var surface=VisualTreeHelper.GetChild(menu,0) as UIElement;
                Check(surface!=null && surface.IsEnabled,"guided menu keeps its input surface enabled for capture and outside-click dismissal");
                var appearance=menu.Items.OfType<MenuItem>().Single(item=>item.Name=="QuickAppearanceMenu");
                Check(appearance.IsEnabled,"guided menu permits inspecting submenu categories");
                appearance.IsSubmenuOpen=true;Pump(.3);
                Check(appearance.IsSubmenuOpen && appearance.Items.OfType<MenuItem>().All(item=>!item.IsEnabled),"appearance submenu can be inspected without changing the companion");
                appearance.IsSubmenuOpen=false;Pump(.2);
                var outside=new Point(80,130); // Non-activating blank window in another process.
                SetCursorPos((int)outside.X,(int)outside.Y);Pump(.1);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(.4);
                Check(!menu.IsOpen && closed==1 && guide.IsVisible,"clicking outside closes the guided menu once but leaves the lesson open");
                guide.Close();Pump(.2);
                Check(menu.Items.OfType<MenuItem>().First().IsEnabled && appearance.Items.OfType<MenuItem>().All(item=>item.IsEnabled),"closing the guide restores menu actions");
                Console.WriteLine("Guide menu smoke: "+passed+" passed");return 0;
            }
            catch(Exception e){Console.WriteLine("FAIL guide menu: "+e);return 1;}
            finally{mouse_event(4,0,0,0,UIntPtr.Zero);mouse_event(16,0,0,0,UIntPtr.Zero);SetCursorPos((int)cursor.X,(int)cursor.Y);if(pet!=null)pet.Close();Pump(.3);if(blank!=null){blank.CloseMainWindow();if(!blank.WaitForExit(2000))blank.Kill();blank.Dispose();}app.Shutdown();}
        }
        static Point OpaquePoint(PetWindow pet)
        {
            Pump(.2);
            var atlas=pet.Skin.Load();var image=(Image)pet.Content;
            int row=(int)typeof(PetWindow).GetField("shownRow",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pet);
            int col=(int)typeof(PetWindow).GetField("shownCol",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pet);
            for(int y=220;y<340;y+=3)for(int x=190;x<300;x+=3) {
                bool solid=true;for(int dy=-12;dy<=12 && solid;dy+=3)for(int dx=-12;dx<=12;dx+=3)if(atlas.AlphaAt(row,col,x+dx,y+dy)<240){solid=false;break;}
                if(solid)return image.PointToScreen(new Point((x+.5)/480*image.ActualWidth,(y+.5)/520*image.ActualHeight));
            }
            throw new Exception("Missing opaque mouse target");
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
        static void Pump(double seconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    }
}
