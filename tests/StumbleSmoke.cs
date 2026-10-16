using System;
using System.Collections.Generic;
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
    public static class StumbleSmoke
    {
        static int passed;
        [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [STAThread]public static int Main(string[] args)
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};PetWindow pet=null;
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"stumble-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            var original=DesktopInterop.Cursor();
            try
            {
                var cfg=new PetSettings{FollowCursor=false,Wander=false,GreetingEnabled=false,AutoUpdate=false,QuietMode=true,HideOnFullscreen=false,ReadReplies=false,SaveChatHistory=false};
                foreach(string key in cfg.Hotkeys.Keys.ToArray())cfg.Hotkeys[key]="";
                var store=new SettingsStore(Path.Combine(root,"settings.json"));
                cfg.StumbleEnabled=false;store.Save(cfg);Check(!store.Load().StumbleEnabled,"disabled preference survives save/load");
                cfg.StumbleEnabled=true;pet=new PetWindow(null,cfg,store,null);pet.Show();Pump(.2);
                HideDuringNativeCapture(pet);
                if(args.Contains("--restore-only"))
                {
                    foreach(string id in new[]{"e-bao","dafeiyu","marfu","xiaobei"})
                    {
                        Check(pet.SelectSkin(pet.Catalog.Find(id)),"restore regression preset "+id);
                        HideAndRestore(pet);NativeDrop(pet,true,"stumble");NativeDrop(pet,false,"landing",true);
                    }
                    Console.WriteLine("Restore regression: "+passed+" passed; real native clicks after four hide/restore cycles.");return 0;
                }
                foreach(string id in new[]{"e-bao","dafeiyu","marfu","xiaobei"})
                {
                    typeof(PetWindow).GetField("random",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,new VariantRandom(false));
                    Check(pet.SelectSkin(pet.Catalog.Find(id)) && pet.Skin.Id==id,"load exact preset "+id);
                    Check(AdditionalInteractions.Supports(pet.Skin),"additional artwork available "+id);
                    var art=new AdditionalInteractions();
                    foreach(string name in AdditionalInteractions.Actions)
                    {
                        bool loop=name=="lifted" || name=="sleeping";
                        var clip=art.Get(pet.Skin,name);Check(clip.Count==8 && clip.Duration==1 && clip.Loop==loop,"eight original poses at 8 FPS "+id+name);
                        var hashes=new HashSet<string>();int lowest=520,highest=0;
                        for(int i=0;i<8;i++)
                        {
                            Check(clip.Sample(i/8.0)==i,"original sequence frame "+i+" "+id+name);
                            var frame=clip.Frame(i);Check(frame.IsFrozen && frame.PixelWidth==480 && frame.PixelHeight==520,"full immutable cell "+id+name+i);
                            var bgra=new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);var pixels=new byte[480*520*4];bgra.CopyPixels(pixels,480*4,0);
                            using(var sha=System.Security.Cryptography.SHA256.Create())hashes.Add(Convert.ToBase64String(sha.ComputeHash(pixels)));
                            int top=520,bottom=-1;bool clearBorder=true;
                            for(int y=0;y<520;y++)for(int x=0;x<480;x++)
                            {
                                byte alpha=clip.AlphaAt(i,x,y);if(alpha>32){top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
                                if(x==0 || y==0 || x==479 || y==519)clearBorder &= alpha==0;
                            }
                            Check(clearBorder,"no clipped border "+id+name+i);Check(bottom>top,"visible character "+id+name+i);lowest=Math.Min(lowest,bottom);highest=Math.Max(highest,bottom);
                        }
                        Check(hashes.Count==8,"all eight poses are distinct, no duplicated filler "+id+name);
                        Check(highest-lowest<=12,"motion stays registered without large foot jumps "+id+name+" drift="+(highest-lowest));
                        pet.PlayInteraction(name);Check(State(pet).Name==name && Field(pet,"shownMotion")!=null,"WPF renders new clip "+id+name);
                        var samples=new List<int>();var deadline=DateTime.UtcNow.AddSeconds(3);
                        while(State(pet).Active && State(pet).Name==name && samples.Count<8 && DateTime.UtcNow<deadline){int sample=(int)Field(pet,"shownSample");if(samples.Count==0 || samples.Last()!=sample)samples.Add(sample);Pump(.01);}
                        Check(samples.SequenceEqual(Enumerable.Range(0,8)),"real renderer visits all eight poses exactly once "+id+name+" actual="+String.Join(",",samples));
                        Pump(.15);Check(loop?State(pet).Name==name:name=="feeding"?State(pet).Name=="happy":!State(pet).Active,"new clip completion or loop "+id+name);pet.PlayAction("idle",0);
                        // Export all frames beside an exact original idle pose for visual review.
                        Contact(pet,clip,Path.Combine(root,id+"-"+name+".png"));
                    }
                    pet.PlayInteraction("stumble");pet.SetPaused(true);Check(!State(pet).Active,"pause cancels stumble "+id);pet.SetPaused(false);
                    pet.PlayInteraction("stumble");HideAndRestore(pet);
                    foreach(bool originalStyle in new[]{false,true}){NativeDrop(pet,true,"stumble",originalStyle);NativeDrop(pet,false,"landing",originalStyle);}
                    cfg.StumbleEnabled=false;NativeDrop(pet,true,"landing");cfg.StumbleEnabled=true;
                }
                string legacy=Path.Combine(root,"legacy.png");using(var input=typeof(SpriteAtlas).Assembly.GetManifestResourceStream("Dafeiyu.Atlas"))using(var output=File.Create(legacy))input.CopyTo(output);
                Check(pet.SelectSkin(pet.Catalog.Import(legacy)),"load custom skin without new artwork");
                Check(!AdditionalInteractions.Supports(pet.Skin),"custom skin not mistaken for preset");NativeDrop(pet,true,"landing");
                Console.WriteLine("Stumble smoke: "+passed+" passed; native mouse verified; review sheets: "+root);return 0;
            }
            catch(Exception e){Console.WriteLine("FAIL stumble "+e);return 1;}
            finally{mouse_event(4,0,0,0,UIntPtr.Zero);SetCursorPos((int)original.X,(int)original.Y);if(pet!=null)pet.Close();Pump(.1);app.Shutdown();}
        }
        static void HideDuringNativeCapture(PetWindow pet)
        {
            pet.ContextMenu.IsOpen=true;Pump(.05);Check(pet.ContextMenu.IsOpen,"menu opened before hide regression");
            pet.HidePet();Check(!pet.ContextMenu.IsOpen,"hide closes the context menu popup");pet.RestorePet();Pump(.1);
            pet.PlayAction("idle",0);var point=OpaquePoint(pet);
            ((HoverInteraction)Field(pet,"hover")).Update(true,true,false);
            Check(SetCursorPos((int)point.X,(int)point.Y),"native capture regression pointer available");Pump(.1);
            Pump(System.Windows.Forms.SystemInformation.DoubleClickTime/1000d+.06);
            point=OpaquePoint(pet);SetCursorPos((int)point.X,(int)point.Y);Pump(.03);
            try
            {
                mouse_event(2,0,0,0,UIntPtr.Zero);WaitUntil(()=> (bool)Field(pet,"dragging"),.8);
                Check((bool)Field(pet,"dragging") && ((Image)pet.Content).IsMouseCaptured,"actual native down captures mouse before hide");
                SetCursorPos((int)point.X-40,(int)point.Y-15);WaitUntil(()=> (bool)Field(pet,"moved"),.8);
                Check((bool)Field(pet,"moved"),"native held pointer actually drags before hide");
                pet.HidePet();
                Check(!(bool)Field(pet,"dragging") && !((Image)pet.Content).IsMouseCaptured && System.Windows.Input.Mouse.Captured==null,"hide while dragging releases native mouse capture");
            }
            finally{mouse_event(4,0,0,0,UIntPtr.Zero);}
            pet.RestorePet();Pump(.1);NativeDrop(pet,false,"landing");
        }
        static void HideAndRestore(PetWindow pet)
        {
            var hwnd=new WindowInteropHelper(pet).Handle;var foreground=GetForegroundWindow();
            var point=OpaquePoint(pet);pet.HidePet();Pump(.1);
            Check(!State(pet).Active,"hide cancels interaction "+pet.Skin.Id);
            Check(!pet.IsPetVisible && pet.Opacity==0 && !pet.IsHitTestVisible,"hidden pet has no visible or interactive visual "+pet.Skin.Id);
            Check(!DesktopInterop.IsWindowAtPoint(hwnd,point),"hidden pet passes native mouse through "+pet.Skin.Id);
            double tick=(double)Field(pet,"previousTick");Pump(.1);
            Check(tick==(double)Field(pet,"previousTick"),"hidden pet does not render "+pet.Skin.Id);
            pet.RestorePet();Pump(.1);
            Check(pet.IsPetVisible && pet.Opacity==1 && pet.IsHitTestVisible,"restored pet resumes visible interaction "+pet.Skin.Id);
            Check(new WindowInteropHelper(pet).Handle==hwnd,"restore preserves window and session "+pet.Skin.Id);
            Check(GetForegroundWindow()==foreground,"hide and restore preserve foreground focus "+pet.Skin.Id);
        }
        sealed class VariantRandom:Random
        {readonly bool original;public int Calls;public VariantRandom(bool useOriginal){original=useOriginal;}public override int Next(int max){Calls++;return original?0:max-1;}}
        static void NativeDrop(PetWindow pet,bool trigger,string expected,bool originalStyle=false)
        {
            var choice=new VariantRandom(originalStyle);typeof(PetWindow).GetField("random",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,choice);
            typeof(PetWindow).GetField("dropReaction",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,new DropReaction(()=>trigger?0:.9));
            pet.PlayAction("idle",0);var hwnd=new WindowInteropHelper(pet).Handle;var area=DesktopInterop.WorkArea(hwnd);
            DesktopInterop.Move(hwnd,new Point(area.Left+area.Width/2,area.Top+area.Height/3));
            // This fixture tests dragging; consume unrelated random hover before pumping.
            Point point=OpaquePoint(pet);
            Check(SetCursorPos((int)point.X,(int)point.Y),"native pointer available");
            ((HoverInteraction)Field(pet,"hover")).Update(true,true,false);Pump(.1);
            Pump(System.Windows.Forms.SystemInformation.DoubleClickTime/1000d+.06);
            point=OpaquePoint(pet);Check(SetCursorPos((int)point.X,(int)point.Y),"current pose native pointer available");Pump(.03);
            Check(DesktopInterop.IsWindowAtPoint(hwnd,point),"native drag belongs to fixture "+pet.Skin.Id);
            bool delivered=false;
            System.Windows.Input.MouseButtonEventHandler observe=delegate(object sender,System.Windows.Input.MouseButtonEventArgs e){if(e.ChangedButton==System.Windows.Input.MouseButton.Left)delivered=true;};
            pet.AddHandler(UIElement.PreviewMouseDownEvent,observe,true);
            try
            {
            mouse_event(2,0,0,0,UIntPtr.Zero);WaitUntil(()=> (bool)Field(pet,"dragging"),.8);
            Check(delivered,"real native button down reaches restored window "+pet.Skin.Id);
            Check((bool)Field(pet,"dragging"),"native button down starts dragging "+pet.Skin.Id+" state="+State(pet).Name);
            Check(SetCursorPos((int)point.X-60,(int)point.Y-25),"native pointer moves during drag");WaitUntil(()=> (bool)Field(pet,"moved"),.8);
            Check(State(pet).Name=="lifted","native drag enters lifted "+pet.Skin.Id);
            Check((bool)Field(pet,"originalInteraction")==originalStyle,"drag chooses expected artwork version "+pet.Skin.Id);
            double releaseY=DesktopInterop.Bounds(hwnd).Top,fallHeight=DesktopInterop.Bounds(hwnd).Height;
            mouse_event(4,0,0,0,UIntPtr.Zero);Pump(.03);
            Check(State(pet).Name==expected,"native drop selects "+expected+" "+pet.Skin.Id);
            Check((bool)Field(pet,"originalInteraction")==originalStyle,"drop keeps held artwork version "+pet.Skin.Id);
            Check(choice.Calls==(AdditionalInteractions.Supports(pet.Skin)?1:0),"drop never rolls artwork a second time "+pet.Skin.Id);
            Pump(.35);Check(Math.Abs(DesktopInterop.Bounds(hwnd).Top-releaseY-fallHeight*24/208)<1.1,"all drop versions have identical physical displacement "+pet.Skin.Id);
            pet.PlayAction("idle",0);
            }
            finally{mouse_event(4,0,0,0,UIntPtr.Zero);pet.RemoveHandler(UIElement.PreviewMouseDownEvent,observe);}
        }
        static object Field(PetWindow pet,string name){return typeof(PetWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pet);}
        static Point OpaquePoint(PetWindow pet)
        {
            var image=(Image)pet.Content;var source=(BitmapSource)image.Source;var bgra=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);int w=source.PixelWidth,h=source.PixelHeight;
            var pixels=new byte[w*h*4];bgra.CopyPixels(pixels,w*4,0);
            for(int y=h/2;y<h*3/4;y+=2)for(int x=w*2/5;x<w*3/5;x+=2)
            {
                bool opaque=true;for(int dy=-6;dy<=6 && opaque;dy+=2)for(int dx=-6;dx<=6;dx+=2)if(pixels[((y+dy)*w+x+dx)*4+3]<240){opaque=false;break;}
                if(opaque)return image.PointToScreen(new Point(x*image.ActualWidth/w,y*image.ActualHeight/h));
            }
            throw new Exception("No opaque native drag target");
        }
        static InteractionAnimation State(PetWindow pet){return (InteractionAnimation)Field(pet,"interaction");}
        static void Pump(double seconds){var f=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};t.Tick+=delegate{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
        static void WaitUntil(Func<bool> condition,double seconds){var deadline=DateTime.UtcNow.AddSeconds(seconds);while(!condition() && DateTime.UtcNow<deadline)Pump(.01);}
        static void Check(bool condition,string name){if(!condition)throw new Exception(name);passed++;}
        static void Contact(PetWindow pet,MotionClip clip,string path)
        {
            Contact(pet,clip,path,Brushes.White);
            Contact(pet,clip,Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+"-dark.png"),new SolidColorBrush(Color.FromRgb(34,38,45)));
        }
        static void Contact(PetWindow pet,MotionClip clip,string path,Brush background)
        {
            pet.PlayAction("idle",0);var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(background,null,new Rect(0,0,1200,520));
                dc.DrawImage(((Image)pet.Content).Source,new Rect(0,0,240,260));
                for(int i=0;i<8;i++)dc.DrawImage(clip.Frame(i),new Rect((i%4+1)*240,i/4*260,240,260));
            }
            var image=new RenderTargetBitmap(1200,520,96,96,PixelFormats.Pbgra32);image.Render(visual);using(var file=File.Create(path)){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));png.Save(file);}
        }
    }
}
