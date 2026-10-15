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
                Check(Find<Button>(guide, "GuideNext").IsDefault, "next button is default keyboard action");
                Check(!pet.CanApplyUpdate(true), "visible guide protects an unfinished setup from auto exit");
                var skins = (ComboBox)preferences.FindControl("SkinPicker");
                skins.SelectedItem = pet.Catalog.Find("dafeiyu");
                Check(pet.Skin.Id == "dafeiyu", "guide uses the actual live skin selection");
                Press(guide, "GuideNext"); Pump(.12);
                Check(guide.Step == 1 && guide.TargetName == "SizeSlider" && guide.HighlightVisible, "behavior step highlights actual size control");
                ((Slider)preferences.FindControl("SizeSlider")).Value = 1.1;
                Check(Math.Abs(pet.Settings.Scale - 1.1) < .001, "guide size change applies to real pet");
                Press(guide, "GuideTryInteraction");
                Check(((InteractionAnimation)Field(pet, "interaction")).Active, "try interaction controls the actual pet");
                Press(guide, "GuideNext"); Pump(.12);
                Check(guide.Step == 2 && preferences.FindControl("TextServiceUrl") != null, "text step opens real AI editor");
                preferences.Width=700;preferences.Height=430;Pump(.2);CheckTargetUncovered(guide,preferences);
                Capture(preferences,Path.Combine(folder,"guide-text-minimum-settings.png"));Capture(guide,Path.Combine(folder,"guide-text-minimum-card.png"));
                preferences.Width=850;preferences.Height=790;Pump(.15);
                var url = (TextBox)preferences.FindControl("TextServiceUrl");
                url.Text = "bad address";
                Press(guide, "GuideNext");
                Check(guide.Step == 2 && preferences.HasUnsavedEdits, "invalid real form keeps guide on current step");
                url.Text = "http://127.0.0.1:9/v1";
                ((TextBox)preferences.FindControl("TextServiceModel")).Text = "guide-test";
                Check(!pet.SelectSkin(pet.Catalog.Find("e-bao")), "unsaved service draft protects skin persona change");
                Check(pet.SelectSkin(pet.Skin), "same skin remains selectable with unsaved drafts");
                Press(guide, "GuideLater"); Pump(.1);
                Check(pet.Settings.OnboardingState == "deferred" && pet.Settings.OnboardingStep == 2 && preferences.IsVisible && preferences.HasUnsavedEdits, "later remembers step and retains real form draft");
                Check(pet.Store.Load().OnboardingState == "deferred" && pet.Store.Load().Copy().OnboardingStep == 2, "guide progress survives save/load/copy");
                pet.ShowOnboarding(); Pump(.15); guide = (OnboardingWindow)Field(pet, "guide");
                Check(guide.Step == 2 && ((TextBox)preferences.FindControl("TextServiceModel")).Text == "guide-test", "manual resume keeps draft and returns to saved step");
                Press(guide, "GuideNext"); Pump(.12);
                Check(guide.Step == 3 && pet.Settings.Text.Model == "guide-test", "next saves actual text configuration");
                Check(guide.TargetName == "SpeechVoice", "voice step targets real editable TTS voice");
                Press(guide, "GuideAsr"); Pump(.1);
                Check(preferences.FindControl("TranscriptionModel") != null, "voice guide exposes real ASR configuration");
                Press(guide, "GuideTts"); Pump(.1);
                ((CheckBox)preferences.FindControl("UseCloudVoice")).IsChecked = true;
                ((ComboBox)preferences.FindControl("SpeechVoice")).Text = "guide-custom-voice";
                Press(guide, "GuideNext"); Pump(.12);
                Check(guide.Step == 4 && pet.Settings.VoiceName == "guide-custom-voice", "voice step saves actual selected TTS value");
                ((ComboBox)preferences.FindControl("LiveVoice")).Text = "Kore";
                Press(guide, "GuideNext"); Pump(.12);
                Check(guide.Step == 5 && pet.Settings.LiveVoice == "Kore" && preferences.FindControl("Hotkeyquick") != null, "live step saves and opens real usage shortcuts");
                Check(Field(pet, "conversation") == null && Field(pet, "live") == null, "guide never starts API requests, microphone, or voice playback automatically");
                Capture(guide, Path.Combine(folder, "guide-final-step.png"));
                Press(guide, "GuideNext"); Pump(.1);
                Check(pet.Settings.OnboardingState == "completed" && Field(pet, "guide") == null, "finish marks completed and closes guide");
                pet.ShowOnboarding(); Pump(.1); guide = (OnboardingWindow)Field(pet, "guide");
                Check(guide.Step == 0, "completed guide can be restarted manually");
                Press(guide, "GuideSkip"); Pump(.1);
                Check(pet.Store.Load().OnboardingState == "skipped" && preferences.IsVisible, "skip persists and leaves settings open");
                pet.ShowOnboarding(); Pump(.1); guide = (OnboardingWindow)Field(pet, "guide");
                Press(guide, "GuideNext"); Press(guide, "GuideNext"); Pump(.1);
                ((TextBox)preferences.FindControl("TextServiceModel")).Text = "unsaved-after-close";
                guide.Close(); Pump(.1);
                Check(pet.Settings.OnboardingState == "deferred" && preferences.HasUnsavedEdits && ((TextBox)preferences.FindControl("TextServiceModel")).Text == "unsaved-after-close", "window close defers without discarding draft");
                Console.WriteLine("Onboarding smoke: " + passed + " passed; screenshot: " + folder);
                return 0;
            }
            catch (Exception error) { Console.WriteLine("FAIL onboarding: " + error); return 1; }
            finally { if (pet != null) pet.Close(); Pump(.2); app.Shutdown(); }
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
        private static void Press(Window window, string name) { var button = Find<Button>(window, name); Check(button != null && button.IsEnabled, "guide action available " + name); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
        private static void Capture(Window window, string path) { var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(window); using (var stream = File.Create(path)) { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); png.Save(stream); } }
        private static void Pump(double seconds) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) }; timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    }
}
