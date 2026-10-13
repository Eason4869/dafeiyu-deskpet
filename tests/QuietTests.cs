using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
namespace Dafeiyu.Tests
{
 public static class QuietTests
 {
  static int passed;
  [STAThread] public static int Main(string[] args)
  {
   try {
    if(args.Length==5 && args[0]=="--fullscreen-fixture")return Fixture(args);
    var settings=new PetSettings();
    Check(Property(settings,"SaveChatHistory"),"history enabled by default");
    Check(!Property(settings,"QuietMode"),"quiet disabled by default");
    Check(Property(settings,"HideOnFullscreen"),"fullscreen hiding enabled by default");
    Check(!Property(settings,"ClickThrough") && !Property(settings,"LockPosition"),"interactive movement defaults");
    Check(Property(settings.Text,"StreamReplies"),"streaming enabled by default");
    var json=new DataContractJsonSerializer(typeof(PetSettings));
    using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("{\"Text\":{\"Model\":\"legacy\"}}"))) {
     var old=(PetSettings)json.ReadObject(stream);old.Normalize();Check(Property(old,"SaveChatHistory") && Property(old.Text,"StreamReplies"),"old settings retain new defaults");
    }
    Check(Property(settings.Copy(),"SaveChatHistory") && Property(settings.Text.Copy(),"StreamReplies"),"copy preserves new defaults");
    var policy=new VisibilityPolicy(); policy.Fullscreen=true;Check(!policy.Visible && policy.AutoHidden,"fullscreen hides visible pet");
    policy.OverrideFullscreen=true;Check(policy.Visible,"explicit fullscreen override shows pet");
    policy.UserVisible=false;Check(!policy.Visible && !policy.AutoHidden,"manual hide overrides explicit override");
    policy.Fullscreen=false;Check(!policy.Visible,"fullscreen exit preserves manual hide");
    policy.UserVisible=true;policy.OverrideFullscreen=false;Check(policy.Visible,"manual show restores pet");
    HotkeyCombination combo;Check(HotkeyCombination.TryParse("Ctrl+Alt+Space",out combo) && combo.Key==32 && combo.Modifiers==3,"default key parsing");
    Check(!HotkeyCombination.TryParse("Ctrl+Ctrl+H",out combo) && !HotkeyCombination.TryParse("Ctrl+Banana",out combo),"invalid keys rejected");
    NativeRegistration();
    settings.SaveChatHistory=false;settings.QuietMode=true;settings.HideOnFullscreen=false;settings.ClickThrough=true;settings.LockPosition=true;settings.Text.StreamReplies=false;settings.Hotkeys["quick"]="";
    string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"quiet-settings",Guid.NewGuid().ToString("N"));var store=new SettingsStore(Path.Combine(folder,"settings.json"));store.Save(settings);var copy=store.Load().Copy();
    Check(!copy.SaveChatHistory && copy.QuietMode && !copy.HideOnFullscreen && copy.ClickThrough && copy.LockPosition && !copy.Text.StreamReplies && copy.Hotkeys["quick"]=="","new settings persist non-default values and disabled key");
    if(args.Length!=0 && (args[0]=="--desktop" || args[0]=="--native-desktop"))DesktopBehavior(args[0]=="--native-desktop");
    Console.WriteLine("Quiet tests: "+passed+" passed");return 0;
   }catch(Exception e){Console.WriteLine("FAIL quiet: "+e.Message);return 1;}
  }
  static bool Property(object value,string name){var property=value.GetType().GetProperty(name);Check(property!=null,"setting exists: "+name);return (bool)property.GetValue(value,null);}
  static void NativeRegistration()
  {
   using(var first=new HotkeyRegistry(IntPtr.Zero))using(var second=new HotkeyRegistry(IntPtr.Zero)) {
    string error;Check(first.TryBind("one","Ctrl+Alt+F23",out error),"native register error "+System.Runtime.InteropServices.Marshal.GetLastWin32Error());
    Check(second.TryBind("conflict","Ctrl+Alt+F23",out error)==false,"native conflict rejected");
    Check(second.TryBind("two","Ctrl+Alt+F24",out error),"second native register");
    Check(!first.TryBind("one","Ctrl+Alt+F24",out error),"conflicting replacement rejected");
    Check(!second.TryBind("conflict","Ctrl+Alt+F23",out error),"old native binding preserved");
    first.Dispose();Check(second.TryBind("conflict","Ctrl+Alt+F23",out error),"disposed binding released");
    Check(second.TryBind("two","",out error),"blank binding disables");
   }
  }
  static void DesktopBehavior(bool native)
  {
   var app=new System.Windows.Application{ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown};
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"quiet-desktop",Guid.NewGuid().ToString("N"));
   var settings=new PetSettings{AutoUpdate=false,GreetingEnabled=false,HideOnFullscreen=false,FollowCursor=false,Wander=true};
   var pet=new PetWindow(null,settings,new SettingsStore(Path.Combine(folder,"settings.json")),null);pet.Show();Pump(.25);
   try {
    var handle=new System.Windows.Interop.WindowInteropHelper(pet).Handle;
    if(native)FullscreenAndInput(pet,handle);
    pet.SetClickThrough(true);Check((GetWindowLong(handle,-20)&0x80020)==0x80020,"whole pet layered transparent native style");pet.SetClickThrough(false);Check((GetWindowLong(handle,-20)&0x20)==0,"tray recovery clears transparent style");
    pet.SetQuietMode(true);Check(settings.ReadReplies && settings.QuietMode,"DND retains speech preference");
    var walking=typeof(PetWindow).GetField("walking",BindingFlags.NonPublic|BindingFlags.Instance);walking.SetValue(pet,true);pet.SetQuietMode(true);Check(!(bool)walking.GetValue(pet),"DND stops wandering");
    pet.ShowSettings();Pump(.1);var prefs=(SettingsWindow)typeof(PetWindow).GetField("preferences",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pet);prefs.SelectPage(6);Check(prefs.IsOnAboutPage,"About navigation index preserved");prefs.SelectPage(7);Check(!prefs.IsOnAboutPage,"quiet page appended");
    var policy=(VisibilityPolicy)typeof(PetWindow).GetField("visibility",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pet);policy.Fullscreen=true;settings.HideOnFullscreen=true;
    typeof(PetWindow).GetMethod("ApplyVisibility",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,null);Check(pet.AutoHidden && !pet.IsVisible,"automatic hide applies to window");var ticks=typeof(PetWindow).GetField("previousTick",BindingFlags.NonPublic|BindingFlags.Instance);double frozen=(double)ticks.GetValue(pet);typeof(PetWindow).GetMethod("RenderFrame",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{null,EventArgs.Empty});Check(frozen==(double)ticks.GetValue(pet),"auto hidden renderer ignores callbacks");
    pet.HidePet();policy.Fullscreen=false;typeof(PetWindow).GetMethod("ApplyVisibility",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,null);Check(!pet.IsVisible && !pet.UserVisible,"fullscreen exit never undoes manual hide");
    pet.ShowQuickChat();Pump(.1);Check(pet.IsVisible && pet.UserVisible,"explicit quick chat restores manually hidden pet");
    var quick=(QuickChatWindow)typeof(PetWindow).GetField("quick",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pet);quick.Hide();Pump(.3);Check(!policy.OverrideFullscreen,"quick chat override ends when panel hides");
   }finally{pet.Close();Pump(.3);}
   Check(app.Windows.Count==0,"async close releases companion windows");
  }
  static int Fixture(string[] args)
  {
   var window=new System.Windows.Forms.Form{FormBorderStyle=System.Windows.Forms.FormBorderStyle.None,StartPosition=System.Windows.Forms.FormStartPosition.Manual,Bounds=new System.Drawing.Rectangle(Int32.Parse(args[1]),Int32.Parse(args[2]),Int32.Parse(args[3]),Int32.Parse(args[4]))};
   window.Shown+=delegate{Console.WriteLine(window.Handle.ToInt64());Console.Out.Flush();};System.Windows.Forms.Application.Run(window);return 0;
  }
  static void FullscreenAndInput(PetWindow pet,IntPtr handle)
  {
   var screen=System.Windows.Forms.Screen.FromHandle(handle).Bounds;
   var info=new System.Diagnostics.ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--fullscreen-fixture "+screen.Left+" "+screen.Top+" "+screen.Width+" "+screen.Height){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true};
   using(var fixture=System.Diagnostics.Process.Start(info))try {
    var ready=fixture.StandardOutput.ReadLineAsync();Check(ready.Wait(10000),"fullscreen fixture starts");IntPtr other=new IntPtr(Int64.Parse(ready.Result));SetForegroundWindow(other);Pump(.15);
    Check(DesktopInterop.ForegroundIsFullScreen(handle),"real foreground fullscreen on pet monitor detected fg="+GetForegroundWindow()+" fixture="+other+" bounds="+DesktopInterop.Bounds(other)+" screen="+screen);
    var bounds=DesktopInterop.Bounds(handle);var atlas=(SpriteAtlas)typeof(PetWindow).GetField("atlas",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pet);int row=(int)typeof(PetWindow).GetField("shownRow",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pet),col=(int)typeof(PetWindow).GetField("shownCol",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pet);int sx=0,sy=0;bool found=false;
    for(int y=20;y<atlas.CellHeight-20 && !found;y+=4)for(int x=20;x<atlas.CellWidth-20;x+=4)if(atlas.AlphaAt(row,col,x,y)>240){sx=x;sy=y;found=true;break;}
    Check(found,"opaque pet point found");var point=new NativePoint{X=(int)(bounds.Left+(sx+.5)/atlas.CellWidth*bounds.Width),Y=(int)(bounds.Top+(sy+.5)/atlas.CellHeight*bounds.Height)};
    Check(WindowFromPoint(point)==handle,"opaque pet receives pointer before passthrough actual="+WindowFromPoint(point)+" pet="+handle+" fixture="+other+" style="+GetWindowLong(handle,-20)+" point="+point.X+","+point.Y);pet.SetClickThrough(true);Pump(.1);Check(WindowFromPoint(point)==other,"pointer passes whole pet to separate process");pet.SetClickThrough(false);Pump(.1);Check(WindowFromPoint(point)==handle,"passthrough recovery returns pet hit target");
    pet.Settings.HideOnFullscreen=true;SetForegroundWindow(other);Pump(.35);Check(pet.AutoHidden && !pet.IsVisible,"real fullscreen poll hides pet");var ticks=typeof(PetWindow).GetField("previousTick",BindingFlags.NonPublic|BindingFlags.Instance);double frozen=(double)ticks.GetValue(pet);Pump(.3);Check(frozen==(double)ticks.GetValue(pet),"auto hidden renderer suspended");pet.HidePet();ShowWindow(other,6);Pump(.35);Check(!pet.IsVisible && !pet.UserVisible,"real fullscreen exit preserves manual hide");pet.Settings.HideOnFullscreen=false;pet.RestorePet();Pump(.1);
   }finally{if(!fixture.HasExited){fixture.Kill();fixture.WaitForExit(5000);}}
  }
  [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]struct NativePoint{public int X,Y;}
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr WindowFromPoint(NativePoint point);
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr handle);
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ShowWindow(IntPtr handle,int command);
  [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetWindowLong")]static extern int GetWindowLong(IntPtr handle,int index);
  static void Pump(double seconds){var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);}
  static void Check(bool ok,string label){if(!ok)throw new Exception(label);passed++;}
 }
}
