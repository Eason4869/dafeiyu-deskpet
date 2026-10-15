using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Dafeiyu.Tests
{
    public static class OnboardingSmoke
    {
        private static int passed;
        [STAThread] public static int Main()
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            PetWindow pet = null;
            bool simulated=Environment.GetEnvironmentVariable("DESKPET_TEST_SIMULATED_GESTURES")=="1";if(simulated)Console.WriteLine("INFO gesture completion uses explicit fixtures; native mouse input is NOT verified by this run.");
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "onboarding-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                Check(new PetSettings().OnboardingState == "new", "fresh settings request first-run guide");
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("{}")))
                {
                    var legacy = (PetSettings)new DataContractJsonSerializer(typeof(PetSettings)).ReadObject(stream); legacy.Normalize();
                    Check(legacy.OnboardingState == "completed", "legacy settings never auto-open a new guide");
                }
                var settings = new PetSettings { AutoUpdate = false, FollowCursor = false, Wander = false, GreetingEnabled = false, HideOnFullscreen = false };
                foreach (string key in settings.Hotkeys.Keys.ToArray()) settings.Hotkeys[key] = "";
                pet = new PetWindow(null, settings, new SettingsStore(Path.Combine(folder, "settings.json")), null);
                pet.Show(); Pump(.15);
                Check(Field(pet, "guide") == null, "constructing a pet does not auto-popup outside Program startup");
                pet.ShowOnboarding(); Pump(.25);
                var guide = (OnboardingWindow)Field(pet, "guide");
                var preferences = (SettingsWindow)Field(pet, "preferences");
                Check(guide.IsVisible && guide.Owner == preferences && !guide.ShowInTaskbar, "guide floats over real settings as owned window");
                Check(guide.Step == 0 && guide.TargetName == "SkinPicker" && guide.HighlightVisible, "first step outlines actual skin picker");
                CheckTargetUncovered(guide,preferences);
                Capture(preferences,Path.Combine(folder,"guide-partner-settings.png"));Capture(guide,Path.Combine(folder,"guide-partner-card.png"));
                Check(!preferences.FindControl("SettingsNavigation").IsEnabled && !((UIElement)pet.Content).IsEnabled,"only current settings target remains enabled");
                Check(!pet.CanApplyUpdate(true),"guide protects setup from automatic exit");
                var skins=(ComboBox)preferences.FindControl("SkinPicker");skins.SelectedItem=pet.Catalog.Find("dafeiyu");Check(pet.Skin.Id=="dafeiyu","actual partner selection works while navigation is locked");
                Press(guide,"GuideNext");Pump(.2);Check(guide.Step==1,"persona has its own detailed step");
                var prompt=(TextBox)preferences.FindControl("PersonaPrompt");string originalPrompt=prompt.Text;prompt.Clear();Pump(.2);Check(!Find<Button>(guide,"GuideNext").IsEnabled,"empty persona disables next");prompt.Text=originalPrompt;Press(guide,"GuideNext");
                Check(guide.Step==2,"behavior has its own step");((Slider)preferences.FindControl("SizeSlider")).Value=1.1;Press(guide,"GuideNext");
                Check(guide.Step==3 && !Find<Button>(guide,"GuideNext").IsEnabled && ((UIElement)pet.Content).IsEnabled,"drag step unlocks pet but requires actual movement");
                Point point=new Point();
                if(simulated)pet.NotifyGuideInteraction("drag");else{pet.ReturnHome();Pump(.2);point=OpaquePoint(pet);SetCursorPos((int)point.X,(int)point.Y);Pump(.08);mouse_event(2,0,0,0,UIntPtr.Zero);Pump(.06);SetCursorPos((int)point.X-70,(int)point.Y-40);Pump(.15);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(.2);}
                Check(Find<Button>(guide,"GuideNext").IsEnabled,"drag completion unlocks drag lesson");Press(guide,"GuideNext");
                Check(guide.Step==4 && !Find<Button>(guide,"GuideNext").IsEnabled,"right menu lesson starts incomplete");if(simulated){pet.ContextMenu.IsOpen=true;Pump(.2);}else{Pump(.6);point=OpaquePoint(pet);SetCursorPos((int)point.X,(int)point.Y);Pump(.08);mouse_event(8,0,0,0,UIntPtr.Zero);mouse_event(16,0,0,0,UIntPtr.Zero);Pump(.25);}
                Check((simulated || pet.ContextMenu.IsOpen) && Find<Button>(guide,"GuideNext").IsEnabled,"opening real menu completes menu lesson");pet.ContextMenu.IsOpen=false;Press(guide,"GuideNext");
                Check(guide.Step==5 && !Find<Button>(guide,"GuideNext").IsEnabled,"wheel lesson requires selecting an action");if(simulated){pet.ShowActionPalette();Pump(.2);}else{point=OpaquePoint(pet);SetCursorPos((int)point.X,(int)point.Y);Pump(.08);mouse_event(32,0,0,0,UIntPtr.Zero);mouse_event(64,0,0,0,UIntPtr.Zero);Pump(.2);}
                var wheel=app.Windows.OfType<ActionPaletteWindow>().Single();Check(wheel.IsVisible && !Find<Button>(guide,"GuideNext").IsEnabled,"opening wheel alone cannot complete lesson");Find<Button>(wheel,"PaletteInteraction_feeding").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(.2);Check(Find<Button>(guide,"GuideNext").IsEnabled,"choosing wheel action completes lesson");Press(guide,"GuideNext");
                Check(guide.Step==6 && !Find<Button>(guide,"GuideNext").IsEnabled,"click lesson starts incomplete");if(simulated)pet.NotifyGuideInteraction("click");else{Pump(.7);point=OpaquePoint(pet);SetCursorPos((int)point.X,(int)point.Y);Pump(.08);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(System.Windows.Forms.SystemInformation.DoubleClickTime/1000d+.3);}Check(Find<Button>(guide,"GuideNext").IsEnabled,"click completion unlocks interaction lesson");Press(guide,"GuideNext");
                Check(guide.Step==7 && !Find<Button>(guide,"GuideNext").IsEnabled,"missing text service disables next");
                var url=(TextBox)preferences.FindControl("TextServiceUrl");url.Text="bad address";Pump(.2);Check(!Find<Button>(guide,"GuideNext").IsEnabled,"invalid address remains blocked");
                preferences.Width=700;preferences.Height=430;Pump(.2);CheckTargetUncovered(guide,preferences);Capture(preferences,Path.Combine(folder,"guide-text-minimum-settings.png"));Capture(guide,Path.Combine(folder,"guide-text-minimum-card.png"));preferences.Width=850;preferences.Height=790;Pump(.2);
                Press(guide,"GuideLater");Check(guide.Step==8 && guide.IsVisible && preferences.HasUnsavedEdits,"later skips only current item and retains draft");
                Check(!Find<Button>(guide,"GuideNext").IsEnabled,"ASR requires configuration");Press(guide,"GuideLater");Check(guide.Step==9,"ASR and TTS have separate steps");
                ((CheckBox)preferences.FindControl("UseCloudVoice")).IsChecked=false;Press(guide,"GuideNext");Check(guide.Step==10 && !pet.Settings.UseCloudVoice,"system TTS saves independently of skipped invalid text draft");
                Check(!Find<Button>(guide,"GuideNext").IsEnabled,"Live requires complete setup");((TextBox)preferences.FindControl("LiveServiceUrl")).Text="ws://127.0.0.1/live";((TextBox)preferences.FindControl("LiveServiceModel")).Text="guide-model";((ComboBox)preferences.FindControl("LiveVoice")).Text="Kore";Press(guide,"GuideNext");
                Check(guide.Step==11 && pet.Settings.Live.Model=="guide-model","valid Live saves only its section without connecting");
                ((TextBox)preferences.FindControl("GreetingMinMinutes")).Text="100";((TextBox)preferences.FindControl("GreetingMaxMinutes")).Text="10";Pump(.2);Check(!Find<Button>(guide,"GuideNext").IsEnabled,"invalid greeting interval disables next");((TextBox)preferences.FindControl("GreetingMaxMinutes")).Text="120";Press(guide,"GuideNext");
                Check(guide.Step==12,"shortcut is final step");((TextBox)preferences.FindControl("Hotkeyquick")).Text="not-a-shortcut";Pump(.2);Check(!Find<Button>(guide,"GuideNext").IsEnabled,"invalid shortcut disables finish");((TextBox)preferences.FindControl("Hotkeyquick")).Clear();Capture(guide,Path.Combine(folder,"guide-final-step.png"));Press(guide,"GuideNext");
                Check(settings.OnboardingState=="completed" && Field(pet,"guide")==null && preferences.FindControl("SettingsNavigation").IsEnabled && ((UIElement)pet.Content).IsEnabled,"finish restores settings and pet interaction");
                Check(Field(pet,"live")==null && !pet.Conversation.IsBusy,"guide does not connect Live or start recording automatically");
                pet.ShowOnboarding();Pump(.2);guide=(OnboardingWindow)Field(pet,"guide");Check(guide.Step==0,"completed guide restarts at first step");Press(guide,"GuideSkip");Check(settings.OnboardingState=="skipped" && Field(pet,"guide")==null && preferences.FindControl("SettingsNavigation").IsEnabled,"skip ends whole guide and restores normal mode");
                pet.ShowOnboarding();Pump(.2);guide=(OnboardingWindow)Field(pet,"guide");Press(guide,"GuideLater");Check(guide.Step==1 && pet.Store.Load().OnboardingStep==1,"later advances and persists step");guide.Close();Pump(.2);Check(settings.OnboardingState=="skipped" && ((UIElement)pet.Content).IsEnabled,"closing guide also releases all interaction locks");
                Console.WriteLine("Onboarding smoke: " + passed + " passed; screenshot: " + folder);
                return 0;
            }
            catch (Exception error) { Console.WriteLine("FAIL onboarding: " + error); return 1; }
            finally { mouse_event(4,0,0,0,UIntPtr.Zero);if (pet != null) pet.Close(); Pump(.2); app.Shutdown(); }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
        static Point OpaquePoint(PetWindow pet)
        {
            var atlas=(SpriteAtlas)Field(pet,"atlas");int row=(int)Field(pet,"shownRow"),col=(int)Field(pet,"shownCol");bool supplemental=(bool)Field(pet,"shownSupplemental");var interaction=(InteractionAtlas)Field(pet,"interactionAtlas");var image=(Image)pet.Content;
            int width=supplemental?interaction.FrameWidth:atlas.CellWidth,height=supplemental?interaction.FrameHeight:atlas.CellHeight;
            for(int y=height/3;y<height*2/3;y+=3)for(int x=width/3;x<width*2/3;x+=3)
            {
                bool solid=true;for(int dy=-12;dy<=12 && solid;dy+=3)for(int dx=-12;dx<=12;dx+=3)if((supplemental?interaction.AlphaAt(row,col,x+dx,y+dy):atlas.AlphaAt(row,col,x+dx,y+dy))<240){solid=false;break;}
                if(solid)return image.PointToScreen(new Point((x+.5)/width*image.ActualWidth,(y+.5)/height*image.ActualHeight));
            }
            throw new Exception("No opaque target");
        }
        private static object Field(object target, string name) { return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }
        private static void CheckTargetUncovered(OnboardingWindow guide,SettingsWindow settings)
        {
            var target=settings.FindControl(guide.TargetName);Point a=target.PointToScreen(new Point()),b=target.PointToScreen(new Point(target.ActualWidth,target.ActualHeight));
            Rect targetBounds=new Rect(a,b),guideBounds=DesktopInterop.Bounds(new WindowInteropHelper(guide).Handle);
            Check(Rect.Intersect(targetBounds,guideBounds).IsEmpty,"floating guide leaves actual target uncovered");
            Check(DesktopInterop.WorkArea(new WindowInteropHelper(settings).Handle).Contains(guideBounds),"floating guide stays inside current monitor work area");
        }
        private static T Find<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            var match = root as T; if (match != null && match.Name == name) return match;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = Find<T>(VisualTreeHelper.GetChild(root, i), name); if (child != null) return child; }
            return null;
        }
        private static void Press(Window window, string name) { Pump(.15);var button = Find<Button>(window, name); Check(button != null && button.IsEnabled, "guide action available " + name); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
        private static void Capture(Window window, string path) { var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(window); using (var stream = File.Create(path)) { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); png.Save(stream); } }
        private static void Pump(double seconds) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) }; timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    }
}
