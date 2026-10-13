using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Dafeiyu.Tests
{
    public static class MotionSmoke
    {
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
                    Check(atlas.FrameMultiplier==8 && atlas.CellWidth==192 && atlas.CellHeight==208,"motion dimensions "+id);
                    int checkedFrames=0;
                    for(int row=0;row<11;row++) for(int col=0;col<(row<9 ? AnimationPlayer.Counts[row] : 8)*8;col++) {
                        Check(atlas.GetFrame(row,col)!=null && atlas.AlphaAt(row,col,0,0)==0,"frame alpha "+id,false);checkedFrames++;
                    }
                    Check(checkedFrames==584,"584 frames "+id);
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
                    Check(changes>30,"real WPF animation advances "+id);
                    pet.PlayAction("idle",0);Pump(.2);pet.SetPaused(true);
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
                    Check(gazeRow>=9 && gazeRow<=10 && gazeCol>=0 && gazeCol<64,"cursor selects enhanced gaze atlas "+id);
                    pet.Settings.FollowCursor=false;Pump(.1);
                }
                string legacy=Path.Combine(folder,"legacy.png");using(var input=typeof(SpriteAtlas).Assembly.GetManifestResourceStream("Dafeiyu.Atlas"))using(var output=File.Create(legacy))input.CopyTo(output);
                var imported=pet.Catalog.Import(legacy);Check(pet.SelectSkin(imported) && pet.Animation.FrameMultiplier==1,"legacy import retains original timing");
                Check(pet.SelectSkin(pet.Catalog.Find("marfu")) && pet.Animation.FrameMultiplier==8,"return to enhanced built-in timing");
                File.WriteAllLines(Path.Combine(folder,"render-rates.csv"),report);foreach(string line in report)Console.WriteLine(line);
                pet.Close();Pump(.1);Check(app.Windows.Count==0,"close releases windows");
                Console.WriteLine("Motion smoke passed");return 0;
            } catch(Exception e) {Console.WriteLine("FAIL "+e);if(pet!=null)pet.Close();return 1;}
        }
        private static void Check(bool condition,string name,bool print=true) {if(!condition)throw new Exception(name);if(print)Console.WriteLine("PASS "+name);}
        private static void Pump(double seconds) {
            var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(seconds)};
            timer.Tick+=delegate {timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
        }
    }
}
