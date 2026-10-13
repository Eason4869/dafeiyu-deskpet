using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Dafeiyu.Tests
{
    public static class MenuSmoke
    {
        [StructLayout(LayoutKind.Sequential)]struct PointNative{public int X,Y;}
        [DllImport("user32.dll")]static extern bool GetCursorPos(out PointNative point);
        [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,IntPtr extra);
        [STAThread]public static int Main()
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};PetWindow pet=null;Process blank=null;PointNative cursor;GetCursorPos(out cursor);
            try
            {
                blank=Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MenuBlankFixture.exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});Pump(.5);
                string data=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"menu-smoke","data");
                pet=new PetWindow(new SpriteAtlas(),new PetSettings{FollowCursor=false,AutoUpdate=false},new SettingsStore(Path.Combine(data,"settings.json")),null);pet.Show();Pump(.2);
                var handle=new WindowInteropHelper(pet).Handle;DesktopInterop.Move(handle,new Point(200,190));Pump(.2);var bounds=DesktopInterop.Bounds(handle);
                SetCursorPos((int)(bounds.Left+bounds.Width*.5),(int)(bounds.Top+bounds.Height*.68));mouse_event(8,0,0,0,IntPtr.Zero);mouse_event(16,0,0,0,IntPtr.Zero);Pump(.4);
                if(!pet.ContextMenu.IsOpen)throw new Exception("real right click did not open context menu");Console.WriteLine("PASS real right click opens the context menu");
                SetCursorPos(80,130);mouse_event(2,0,0,0,IntPtr.Zero);mouse_event(4,0,0,0,IntPtr.Zero);Pump(.4);
                if(pet.ContextMenu.IsOpen)throw new Exception("clicking blank space leaves the menu open");Console.WriteLine("PASS clicking blank space closes the context menu");
                Console.WriteLine("Menu smoke: 2 passed");return 0;
            }
            catch(Exception e){Console.WriteLine("FAIL Menu: "+e.Message);return 1;}
            finally{mouse_event(4,0,0,0,IntPtr.Zero);SetCursorPos(cursor.X,cursor.Y);if(pet!=null)pet.Close();if(blank!=null){blank.CloseMainWindow();if(!blank.WaitForExit(2000))blank.Kill();blank.Dispose();}app.Shutdown();}
        }
        static void Pump(double seconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    }
}
