using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Dafeiyu.Tests
{
 public static class ProviderUiSmoke
 {
  static int passed;
  [STAThread] public static int Main()
  {
   var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"provider-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   PetWindow pet=null;SettingsWindow window=null;
   try {
    var cfg=new PetSettings{GreetingEnabled=false,AutoUpdate=false,FollowCursor=false,HideOnFullscreen=false,SaveChatHistory=false};foreach(string key in cfg.Hotkeys.Keys.ToArray())cfg.Hotkeys[key]="";
    cfg.Text=new ConnectionSettings{BaseUrl="https://first.example/v1",Model="first-a",ApiKey="first-secret"};
    var store=new SettingsStore(Path.Combine(folder,"settings.json"));pet=new PetWindow(null,cfg,store,null);pet.Show();window=new SettingsWindow(pet);window.Show();window.SelectAiSection(0);Pump();
    Check(Find<ListBox>(window,"TextProviderList").Items.Count==1,"legacy provider shown");
    Find<TextBox>(window,"TextProviderName").Text="主要服务";Find<TextBox>(window,"TextProviderNewModel").Text="first-b";Find<TextBox>(window,"TextModelDisplayName").Text="深度思考";Click(window,"AddTextModel");
    Check(Find<ListBox>(window,"TextProviderModels").Items.Count==2,"second model added to first provider");
    Click(window,"AddTextProvider");Find<TextBox>(window,"TextProviderName").Text="备用服务";Find<TextBox>(window,"TextServiceUrl").Text="https://second.example/v1";Find<PasswordBox>(window,"TextServiceKey").Password="second-secret";Find<TextBox>(window,"TextProviderNewModel").Text="second-a";Click(window,"AddTextModel");
    Find<TextBox>(window,"TextServiceProxyAddress").Text="unfinished proxy";
    Find<ListBox>(window,"TextProviderList").SelectedIndex=0;Pump();
    Check(Find<TextBox>(window,"TextServiceModel").Text=="first-b" && Find<PasswordBox>(window,"TextServiceKey").Password=="first-secret","provider switch preserves separate model and key drafts");
    Find<ListBox>(window,"TextProviderList").SelectedIndex=1;Pump();Check(Find<TextBox>(window,"TextServiceProxyAddress").Text=="unfinished proxy","invalid unfinished draft survives navigation");
    Find<TextBox>(window,"TextServiceProxyAddress").Text="127.0.0.1:7890";Click(window,"SaveConnections");
    Check(pet.Settings.TextProviders.Count==2 && pet.Settings.Text.Model=="second-a" && pet.Settings.Text.ApiKey=="second-secret","save activates selected provider connection");
    Check(!window.HasUnsavedEdits,"saved providers are clean");
    Find<TextBox>(window,"TextProviderNewModel").Text="second-pending";Find<TextBox>(window,"TextModelDisplayName").Text="待保存模型";
    pet.Settings.SelectTextModel(pet.Settings.TextProviders[0].Id,"first-b");window.RefreshTextProviders();Pump();
    Check(Find<TextBox>(window,"TextProviderNewModel").Text=="second-pending" && Find<TextBox>(window,"TextModelDisplayName").Text=="待保存模型" && window.HasUnsavedEdits,"external selection preserves pending new model ID and alias before Add");
    Find<ListBox>(window,"TextProviderList").SelectedIndex=0;Find<ListBox>(window,"TextProviderList").SelectedIndex=1;Pump();
    Check(Find<TextBox>(window,"TextProviderNewModel").Text=="second-pending" && Find<TextBox>(window,"TextModelDisplayName").Text=="待保存模型","provider navigation preserves staged model ID and alias");
    Find<ListBox>(window,"TextProviderModels").SelectedValue="second-pending";Click(window,"RemoveTextModel");Click(window,"SaveConnections");
    Check(pet.Settings.TextProviders[0].DisplayName("first-b")=="深度思考","Chinese model alias saved independently from ID");
    pet.Settings.SelectTextModel(pet.Settings.TextProviders[0].Id,"first-b");window.RefreshTextProviders();Pump();Check(Find<TextBox>(window,"TextProviderName").Text=="主要服务" && !window.HasUnsavedEdits,"external model selection refreshes a clean settings draft");
    Find<TextBox>(window,"TextModelDisplayName").Text="长文思考";pet.Settings.SelectTextModel(pet.Settings.TextProviders[1].Id,"second-a");window.RefreshTextProviders();Pump();Check(Find<TextBox>(window,"TextModelDisplayName").Text=="长文思考" && window.HasUnsavedEdits,"external model selection preserves an edited display-name draft");
    Click(window,"SaveConnections");Check(pet.Settings.TextProviders[0].DisplayName("first-b")=="长文思考","editing existing model alias saves without changing ID");
    Find<ListBox>(window,"TextProviderList").SelectedIndex=1;Click(window,"SaveConnections");
    var saved=store.Load();Check(saved.TextProviders.Count==2 && saved.TextProviders[0].Models.Count==2 && saved.TextProviders[1].Connection.ApiKey=="second-secret","provider drafts persisted on disk");
    window.Close();window=new SettingsWindow(pet);window.Show();window.SelectAiSection(0);Pump();Check(Find<TextBox>(window,"TextProviderName").Text=="备用服务","reopen restores active provider");
    Capture(window,Path.Combine(folder,"providers-normal.png"));
    window.Width=700;window.Height=430;Pump();Check(Inside(Find<Button>(window,"SaveConnections"),window),"save visible at minimum dimensions");Check(Inside(Find<Button>(window,"AiLiveTab"),window),"all service categories remain reachable");
    Check(window.FocusGuideTarget("TextServiceModel")!=null,"existing guide locates editable model field");Pump();Capture(window,Path.Combine(folder,"providers-minimum.png"));
    window.Width=960;window.Height=790;window.SelectAiSection(0);Pump();
    Find<ListBox>(window,"TextProviderList").SelectedIndex=0;Pump();Find<ListBox>(window,"TextProviderModels").SelectedValue="first-b";Click(window,"RemoveTextModel");Click(window,"SaveConnections");
    Check(pet.Settings.Text.Model=="first-a" && pet.Settings.TextProviders[0].Models.Count==1,"remove active model falls back within provider");
    Find<ListBox>(window,"TextProviderList").SelectedIndex=1;Click(window,"RemoveTextProvider");Click(window,"SaveConnections");Check(pet.Settings.TextProviders.Count==1 && pet.Settings.Text.ApiKey=="first-secret","remove provider retains valid remaining provider");
    Check(!Find<Button>(window,"RemoveTextProvider").IsEnabled,"last provider cannot be removed");
    Console.WriteLine("ProviderUiSmoke: "+passed+" passed; previews: "+folder);return 0;
   }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}
   finally{if(window!=null)window.Close();if(pet!=null)pet.Close();Pump();app.Shutdown();}
  }
  static T Find<T>(SettingsWindow w,string name) where T:FrameworkElement {var result=w.FindControl(name) as T;if(result==null)throw new Exception("Missing "+name);return result;}
  static void Click(SettingsWindow w,string name){Find<Button>(w,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
  static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
  static bool Inside(FrameworkElement e,Window w){Rect r=e.TransformToAncestor(w).TransformBounds(new Rect(e.RenderSize));return e.IsVisible && r.Left>=0 && r.Top>=0 && r.Right<=w.ActualWidth && r.Bottom<=w.ActualHeight;}
  static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
  static void Capture(Window w,string path){var bitmap=new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth),(int)Math.Ceiling(w.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(w);using(var stream=File.Create(path)){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));png.Save(stream);}}
 }
}
