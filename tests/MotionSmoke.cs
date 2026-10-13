using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    public static class MotionSmoke
    {
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
        [STAThread] public static int Main()
        {
            PetWindow pet=null;
            try {
                var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
                string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"motion-smoke");Directory.CreateDirectory(folder);
                var settings=new PetSettings {FollowCursor=false,GreetingEnabled=false,Wander=false,Scale=1.25};
                pet=new PetWindow(null,settings,new SettingsStore(Path.Combine(folder,"settings.json")),null);
                pet.Show();Pump(.3);
                var report=new List<string> {"pet,render_ticks_per_second,changed_frames_per_second"};
                foreach(string id in new[]{"e-bao","dafeiyu","marfu"}) {
                    Check(pet.SelectSkin(pet.Catalog.Find(id)),"switch "+id);
                    var atlas=(SpriteAtlas)typeof(PetWindow).GetField("atlas",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pet);
                    Check(atlas.FrameMultiplier==1 && atlas.CellWidth==480 && atlas.CellHeight==520,"original frame dimensions "+id);
                    int checkedFrames=0;
                    for(int row=0;row<11;row++) for(int col=0;col<(row<9 ? AnimationPlayer.Counts[row] : 8);col++) {
                        Check(atlas.GetFrame(row,col)!=null && atlas.AlphaAt(row,col,0,0)==0,"frame alpha "+id,false);checkedFrames++;
                    }
                    Check(checkedFrames==73,"73 original frames "+id);
                    foreach(string state in AnimationPlayer.States) {pet.PlayAction(state,0);Pump(.07);Check(pet.Animation.State==state,"state "+state,false);}
                    pet.PlayAction("running-right",0);Pump(.3);
                    var field=typeof(PetWindow).GetField("previousTick",BindingFlags.Instance|BindingFlags.NonPublic);
                    Image visual=(Image)pet.Content;ImageSource previous=visual.Source;double lastTick=(double)field.GetValue(pet);int ticks=0,changes=0;
                    EventHandler observer=delegate {
                        double current=(double)field.GetValue(pet);if(current!=lastTick){ticks++;lastTick=current;}
                        if(visual.Source!=previous){changes++;previous=visual.Source;}
                    };
                    CompositionTarget.Rendering+=observer;var clock=Stopwatch.StartNew();Pump(3);
                    double seconds=clock.Elapsed.TotalSeconds;CompositionTarget.Rendering-=observer;
                    report.Add(String.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:F1},{2:F1}",id,ticks/seconds,changes/seconds));
                    Check(changes>=10,"real WPF original-frame animation advances "+id);
                    double period=.22;
                    if(id=="marfu" || id=="dafeiyu") {
                        int[] stride=id=="marfu"?new[]{0,2,3,2,4,2,6,2}:new[]{0,1,2,3,4,5,6,7};
                        CaptureRun(pet,atlas,folder,"running-right",1,stride,period);
                        pet.PlayAction("running-right",0);ImageSource rightStart=visual.Source;pet.PlayAction("running-left",0);
                        Check(Object.ReferenceEquals(rightStart,visual.Source) && visual.RenderTransform.Value.M11==-1,"direction switch flips even when the original pose is identical "+id);
                        CaptureRun(pet,atlas,folder,"running-left",1,stride,period);
                    }
                    else {
                        CaptureRun(pet,atlas,folder,"running-right",1,new[]{0,1,2,3,4,5,6,7},period);
                        pet.PlayAction("running-right",0);ImageSource rightStart=visual.Source;pet.PlayAction("running-left",0);
                        Check(!Object.ReferenceEquals(rightStart,visual.Source) && Object.ReferenceEquals(visual.Source,atlas.GetFrame(2,0)) && visual.RenderTransform.Value.M11==1,"direction switch selects original left-facing artwork "+id);
                        CaptureRun(pet,atlas,folder,"running-left",2,new[]{0,1,2,3,4,5,6,7},period);
                    }
                    pet.PlayAction("running-left",0);ImageSource last=visual.Source;int leftChanges=0;
                    EventHandler leftObserver=delegate {if(visual.Source!=last){leftChanges++;last=visual.Source;}};
                    CompositionTarget.Rendering+=leftObserver;Pump(1.25);CompositionTarget.Rendering-=leftObserver;
                    Check(leftChanges>=4,"real WPF left stride advances at its selected cadence "+id);
                    pet.PlayAction("idle",0);Pump(.2);pet.SetPaused(true);
                    Check(visual.RenderTransform.Value.M11==1,"non-running action clears horizontal mirroring "+id);
                    var render=new RenderTargetBitmap((int)Math.Ceiling(pet.Width),(int)Math.Ceiling(pet.Height),96,96,PixelFormats.Pbgra32);render.Render(pet);
                    using(var stream=File.Create(Path.Combine(folder,id+".png"))) {var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(render));png.Save(stream);}
                    int frozen=pet.Animation.Frame;Pump(.2);Check(frozen==pet.Animation.Frame,"pause "+id);pet.SetPaused(false);
                    pet.HidePet();double hiddenTick=(double)field.GetValue(pet);Pump(.3);Check(hiddenTick==(double)field.GetValue(pet),"hidden pet detaches render callback "+id);
                    pet.RestorePet();Pump(.1);
                    pet.Settings.FollowCursor=true;
                    typeof(PetWindow).GetField("lastCursorMove",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,Double.MaxValue);
                    Pump(.25);
                    int gazeRow=(int)typeof(PetWindow).GetField("shownRow",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pet);
                    int gazeCol=(int)typeof(PetWindow).GetField("shownCol",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pet);
                    Check(gazeRow>=9 && gazeRow<=10 && gazeCol>=0 && gazeCol<8,"cursor selects one of sixteen original gaze frames "+id);
                    pet.Settings.FollowCursor=false;Pump(.1);
                }
                string legacy=Path.Combine(folder,"legacy.png");using(var input=typeof(SpriteAtlas).Assembly.GetManifestResourceStream("Dafeiyu.Atlas"))using(var output=File.Create(legacy))input.CopyTo(output);
                var imported=pet.Catalog.Import(legacy);Check(pet.SelectSkin(imported) && pet.Animation.FrameMultiplier==1,"legacy import retains original timing");
                Check(pet.SelectSkin(pet.Catalog.Find("marfu")) && pet.Animation.FrameMultiplier==1,"return to original built-in timing");
                File.WriteAllLines(Path.Combine(folder,"render-rates.csv"),report);foreach(string line in report)Console.WriteLine(line);
                pet.Close();Pump(.1);Check(app.Windows.Count==0,"close releases windows");
                Console.WriteLine("Motion smoke passed");return 0;
            } catch(Exception e) {Console.WriteLine("FAIL "+e);if(pet!=null)pet.Close();return 1;}
        }
        private static void CaptureRun(PetWindow pet,SpriteAtlas atlas,string folder,string action,int row,int[] expected,double frameSeconds)
        {
            pet.PlayAction(action,0);pet.SetPaused(true);
            var update=typeof(PetWindow).GetMethod("UpdateFrame",BindingFlags.Instance|BindingFlags.NonPublic);
            Image visual=(Image)pet.Content;
            string prefix=pet.Skin.Id=="e-bao"?"ebao":pet.Skin.Id;bool mirrored=(pet.Skin.Id=="marfu" || pet.Skin.Id=="dafeiyu") && action=="running-left";
            for(int phase=0;phase<8;phase++) {
                pet.Animation.Play(action,0);pet.Animation.Advance(phase*frameSeconds+.0001);update.Invoke(pet,new object[]{0d});Pump(.035);
                Check(Object.ReferenceEquals(visual.Source,atlas.GetFrame(row,expected[phase])),"actual WPF stride pose "+prefix+" "+action+" "+phase,false);
                Check(visual.RenderTransform.Value.M11==(mirrored?-1:1),"WPF horizontal direction matches original artwork "+prefix+" "+action,false);
                if(mirrored && phase==2)CheckMirroredHitTesting(pet,atlas,row,expected[phase]);
                var render=new RenderTargetBitmap((int)Math.Ceiling(pet.Width),(int)Math.Ceiling(pet.Height),96,96,PixelFormats.Pbgra32);render.Render(pet);
                using(var stream=File.Create(Path.Combine(folder,prefix+"-"+action+"-"+phase+".png"))) {var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(render));png.Save(stream);}
            }
            Check(true,"eight real WPF original poses captured at "+frameSeconds+" seconds: "+prefix+" "+action);pet.SetPaused(false);
        }
        private static void CheckMirroredHitTesting(PetWindow pet,SpriteAtlas atlas,int row,int col)
        {
            IntPtr handle=new WindowInteropHelper(pet).Handle;Rect bounds=DesktopInterop.Bounds(handle);bool opaque=false,transparent=false;
            for(int y=0;y<(int)bounds.Height && !(opaque && transparent);y++)for(int x=0;x<(int)bounds.Width && !(opaque && transparent);x++) {
                int sx=(int)(x/bounds.Width*atlas.CellWidth),sy=(int)(y/bounds.Height*atlas.CellHeight);
                bool expected=atlas.AlphaAt(row,col,atlas.CellWidth-1-sx,sy)>=12,original=atlas.AlphaAt(row,col,sx,sy)>=12;
                if(expected==original || (expected?opaque:transparent))continue;
                int screenX=(int)bounds.Left+x,screenY=(int)bounds.Top+y;
                IntPtr point=new IntPtr((screenY<<16)|(screenX&0xffff));bool passes=SendMessage(handle,0x84,IntPtr.Zero,point).ToInt32()==-1;
                Check(passes!=expected,"mirrored native alpha hit test "+(expected?"opaque body":"transparent background"));
                if(expected)opaque=true;else transparent=true;
            }
            Check(opaque && transparent,"mirrored hit tests cover both asymmetric body and background pixels");
        }
        private static void Check(bool condition,string name,bool print=true) {if(!condition)throw new Exception(name);if(print)Console.WriteLine("PASS "+name);}
        private static void Pump(double seconds) {
            var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(seconds)};
            timer.Tick+=delegate {timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
        }
    }
}
