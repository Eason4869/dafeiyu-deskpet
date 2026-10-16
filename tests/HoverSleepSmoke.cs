using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Dafeiyu.Tests
{
    public static class HoverSleepSmoke
    {
        static int passed;
        [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
        [STAThread]public static int Main()
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};PetWindow pet=null;var original=DesktopInterop.Cursor();
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"hover-sleep-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                var cfg=new PetSettings{FollowCursor=false,Wander=false,GreetingEnabled=false,AutoUpdate=false,HideOnFullscreen=false,QuietMode=true,ReadReplies=false,SaveChatHistory=false};
                foreach(string key in cfg.Hotkeys.Keys.ToArray())cfg.Hotkeys[key]="";
                var store=new SettingsStore(Path.Combine(folder,"settings.json"));pet=new PetWindow(null,cfg,store,null);pet.Show();Pump(.2);
                foreach(string id in new[]{"e-bao","dafeiyu","marfu","xiaobei"})
                {
                    Check(pet.SelectSkin(pet.Catalog.Find(id)),"select "+id);
                    var handle=new WindowInteropHelper(pet).Handle;var area=DesktopInterop.WorkArea(handle);DesktopInterop.Move(handle,new Point(area.Left+area.Width/2,area.Top+area.Height/3));
                    string[] expected={"jumping","waving","special","special2"};
                    for(int i=0;i<4;i++)
                    {
                        Leave(pet);SetField(pet,"random",new ChoiceRandom(i));var point=OpaquePoint(pet);
                        Check(SetCursorPos((int)point.X,(int)point.Y),"native pointer available");Pump(.07);
                        Check(DesktopInterop.IsWindowAtPoint(handle,point),"hover belongs to fixture "+id);
                        Check(Current(pet)==expected[i],"native hover selects "+expected[i]+" "+id);
                        var source=((Image)pet.Content).Source;Pump(.07);Check(Current(pet)==expected[i],"same visit never starts a second action "+id);
                        // Consumed visit stays consumed even after a direct return to idle.
                        pet.PlayAction("idle",0);Pump(.08);Check(Current(pet)=="idle","same stationary hover cannot retrigger "+id);
                    }
                    var holiday=new HolidayFixture();pet.HolidayHoverActions=holiday;Leave(pet);SetField(pet,"random",new ChoiceRandom(4));var hp=OpaquePoint(pet);SetCursorPos((int)hp.X,(int)hp.Y);Pump(.1);
                    Check(holiday.PlayCount==1 && Current(pet)=="waving","future fifth holiday action uses actual hover path "+id);Pump(.15);Check(holiday.PlayCount==1,"holiday also plays only once per visit "+id);pet.HolidayHoverActions=null;
                    Leave(pet);SetField(pet,"random",new ChoiceRandom(3));var delayedPoint=OpaquePoint(pet);SetCursorPos((int)delayedPoint.X,(int)delayedPoint.Y);
                    SetField(pet,"previousTick",((System.Diagnostics.Stopwatch)Field(pet,"watch")).Elapsed.TotalSeconds-.2);SetField(pet,"nextRender",0d);
                    typeof(PetWindow).GetMethod("RenderFrame",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{null,EventArgs.Empty});
                    Check(Current(pet)=="special2" && (int)Field(pet,"shownSample")==0,"late hover render still draws first pose before advancing "+id);
                    if(id=="e-bao")
                    {
                        Leave(pet);SetField(pet,"random",new ChoiceRandom(0));var jumpPoint=OpaquePoint(pet);SetCursorPos((int)jumpPoint.X,(int)jumpPoint.Y);
                        SetField(pet,"previousTick",((System.Diagnostics.Stopwatch)Field(pet,"watch")).Elapsed.TotalSeconds-.2);SetField(pet,"nextRender",0d);
                        typeof(PetWindow).GetMethod("RenderFrame",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{null,EventArgs.Empty});
                        Check(Current(pet)=="jumping" && (int)Field(pet,"shownSample")==0,"late Ebao hover jump renders new pose zero");
                        var sequence=new System.Collections.Generic.List<int>();var until=DateTime.UtcNow.AddSeconds(3);
                        while(Current(pet)=="jumping" && DateTime.UtcNow<until){int frame=(int)Field(pet,"shownSample");if(sequence.Count==0 || sequence.Last()!=frame)sequence.Add(frame);Pump(.01);}
                        Check(sequence.SequenceEqual(Enumerable.Range(0,8)),"Ebao jump actually renders all eight poses in order: "+String.Join(",",sequence));
                        Check(Current(pet)=="idle","Ebao optimized jump completes at full clip duration");
                    }
                    Leave(pet);SetField(pet,"idleSleep",new IdleSleepSchedule(0,()=>0));
                    Check(pet.ProbeAutoSleep(900,0) && pet.IsSleeping,"long pet inactivity enters actual sleeping animation "+id);
                    pet.PlayInteraction("wake");Check(!pet.IsSleeping,"explicit wake clears automatic sleep "+id);
                    Check(!pet.ProbeAutoSleep(1,999),"wake resets idle time "+id);
                    Leave(pet);double actualNow=((System.Diagnostics.Stopwatch)Field(pet,"watch")).Elapsed.TotalSeconds;SetField(pet,"idleSleep",new IdleSleepSchedule(actualNow-901,()=>0));
                    Check(pet.ProbeAutoSleep(actualNow,0),"fixture sleeps before tray wake "+id);pet.ReturnHome();
                    Check(!pet.IsSleeping && !pet.ProbeAutoSleep(actualNow+1,999),"return home wakes and resets inactivity "+id);
                    SetField(pet,"idleSleep",new IdleSleepSchedule(actualNow-901,()=>0));pet.HidePet();pet.RestorePet();
                    Check(!pet.ProbeAutoSleep(actualNow+1,999),"manual show restarts inactivity countdown "+id);
                    Leave(pet);actualNow=((System.Diagnostics.Stopwatch)Field(pet,"watch")).Elapsed.TotalSeconds;SetField(pet,"idleSleep",new IdleSleepSchedule(actualNow-901,()=>0));
                    Check(pet.ProbeAutoSleep(actualNow,0),"fixture sleeps before pass-through toggle "+id);pet.SetClickThrough(true);
                    Check(!pet.IsSleeping && !pet.ProbeAutoSleep(actualNow+1,999),"pass-through shortcut wake resets inactivity "+id);pet.SetClickThrough(false);
                    SetField(pet,"idleSleep",new IdleSleepSchedule(0,()=>0));
                    Check(pet.ProbeAutoSleep(300,300) && pet.IsSleeping,"system inactivity enters sleep "+id);pet.PlayInteraction("wake");
                    Leave(pet);cfg.AutoSleepEnabled=false;SetField(pet,"idleSleep",new IdleSleepSchedule(0,()=>0));Check(!pet.ProbeAutoSleep(900,900),"disabled switch prevents sleep "+id);cfg.AutoSleepEnabled=true;
                    pet.SetPaused(true);Check(!pet.ProbeAutoSleep(900,900),"paused pet never auto sleeps "+id);pet.SetPaused(false);
                    pet.HidePet();Check(!pet.ProbeAutoSleep(900,900),"hidden pet never auto sleeps "+id);pet.RestorePet();
                    pet.PlayInteraction("feeding");Check(!pet.ProbeAutoSleep(900,900),"feeding never interrupted by automatic sleep "+id);pet.PlayAction("idle",0);
                    pet.ShowQuickChat();Check(!pet.ProbeAutoSleep(900,900),"open chat prevents sleep "+id);((QuickChatWindow)Field(pet,"quick")).Hide();
                    var menu=pet.ContextMenu.Items.OfType<MenuItem>().First(m=>(string)m.Header=="互动");pet.ContextMenu.IsOpen=true;Pump(.05);
                    string[] labels=menu.Items.OfType<MenuItem>().Where(m=>m.Visibility==Visibility.Visible).Select(m=>(string)m.Header).ToArray();
                    Check(labels.Contains(pet.Skin.SpecialAction) && labels.Contains(AdditionalInteractions.SpecialName(pet.Skin)) && !labels.Contains("专属动作"),"right menu shows both concrete unique names "+id);pet.ContextMenu.IsOpen=false;
                }
                cfg.AutoSleepEnabled=false;store.Save(cfg);Check(!store.Load().AutoSleepEnabled && !cfg.Copy().AutoSleepEnabled,"auto sleep switch persists and copies");
                double idle=DesktopInterop.UserIdleSeconds();Check(!Double.IsNaN(idle) && idle>=0,"Windows idle query returns real nonnegative elapsed time");
                Console.WriteLine("Hover/sleep smoke: "+passed+" passed; four native hover choices, fifth holiday hook and virtual-clock sleep verified.");return 0;
            }
            catch(Exception e){Console.WriteLine("FAIL hover/sleep "+e);return 1;}
            finally{SetCursorPos((int)original.X,(int)original.Y);if(pet!=null)pet.Close();Pump(.1);app.Shutdown();}
        }
        sealed class ChoiceRandom:Random{readonly int choice;public ChoiceRandom(int value){choice=value;}public override int Next(int max){return Math.Min(choice,max-1);}}
        sealed class HolidayFixture:IHolidayHoverActionProvider
        {public int PlayCount;public CompanionAction GetAction(SkinDefinition skin,DateTime date){return new CompanionAction("holiday-fixture","节日夹具",p=>{PlayCount++;p.PlayAction("waving",1.2);});}}
        static void Leave(PetWindow pet)
        {var area=DesktopInterop.WorkArea(new WindowInteropHelper(pet).Handle);SetCursorPos((int)area.Left+3,(int)area.Top+3);Pump(.05);pet.PlayAction("idle",0);Pump(.03);}
        static Point OpaquePoint(PetWindow pet)
        {
            var image=(Image)pet.Content;var source=(BitmapSource)image.Source;var bgra=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);int w=source.PixelWidth,h=source.PixelHeight;var pixels=new byte[w*h*4];bgra.CopyPixels(pixels,w*4,0);
            for(int y=h/2;y<h*3/4;y+=2)for(int x=w*2/5;x<w*3/5;x+=2)
            {bool opaque=true;for(int dy=-6;dy<=6 && opaque;dy+=2)for(int dx=-6;dx<=6;dx+=2)if(pixels[((y+dy)*w+x+dx)*4+3]<240){opaque=false;break;}if(opaque)return image.PointToScreen(new Point(x*image.ActualWidth/w,y*image.ActualHeight/h));}
            throw new Exception("No opaque hover target");
        }
        static string Current(PetWindow pet){var state=(InteractionAnimation)Field(pet,"interaction");return state.Active?state.Name:pet.Animation.State;}
        static object Field(PetWindow pet,string name){return typeof(PetWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pet);}
        static void SetField(PetWindow pet,string name,object value){typeof(PetWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(pet,value);}
        static void Pump(double seconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);passed++;}
    }
}
