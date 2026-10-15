using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Dafeiyu.Tests
{
    public static class LiveCallSmoke
    {
        static int passed;
        [STAThread] public static int Main()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};PetWindow pet=null;
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"live-call-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                var cfg=new PetSettings {FollowCursor=false,Wander=false,GreetingEnabled=false,HideOnFullscreen=false,AutoUpdate=false,SaveChatHistory=false,ReadReplies=false};
                foreach(var key in cfg.Hotkeys.Keys.ToArray())cfg.Hotkeys[key]="";
                cfg.Live=new ConnectionSettings {BaseUrl="ws://127.0.0.1/live",Model="fixture"};
                pet=new PetWindow(null,cfg,new SettingsStore(Path.Combine(folder,"settings.json")),null);pet.Show();
                var socket=new FakeSocket();var mic=new FakeMic();var output=new FakeOutput();var session=new GeminiLiveSession(()=>socket,()=>mic,()=>output);
                typeof(PetWindow).GetField("live",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,session);
                pet.ShowChat();pet.ShowQuickChat();Pump();
                var chat=app.Windows.OfType<ChatWindow>().Single();var quick=app.Windows.OfType<QuickChatWindow>().Single();
                Find<TextBox>(chat,"MessageInput").Text="完整对话草稿";Find<TextBox>(quick,"QuickMessageInput").Text="快速对话草稿";
                var speech=Find<CheckBox>(quick,"TtsOutput");speech.IsChecked=true;Pump();
                Check(cfg.ReadReplies && Find<CheckBox>(chat,"TtsOutput").IsChecked==true,"speaker icons synchronize across both chat headers");
                Find<CheckBox>(chat,"TtsOutput").IsChecked=false;Pump();Check(!cfg.ReadReplies && speech.IsChecked==false,"muted speaker state is shared and saved");
                Capture(chat,Path.Combine(folder,"chat.png"));Capture(quick,Path.Combine(folder,"quick.png"));
                socket.Push("{\"setupComplete\":{}}");Click(quick,"StartLiveCall");Until(()=>mic.Started);
                var call=app.Windows.OfType<LiveCallWindow>().Single();Check(call.IsVisible && call.Owner==pet,"quick entry opens dedicated call interface");
                Check(!pet.CanApplyUpdate(true),"open call blocks automatic installation");
                var orbit=Find<VoiceOrbit>(call,"LiveVoiceOrbit");Check(orbit!=null,"dedicated screen contains graphical voice orbit");
                mic.Emit(new byte[]{0,32,0,224});Pump(.15);Check(orbit.Level>.1,"microphone PCM visibly moves waveform");
                Pump(.7);Check(orbit.Level<.01,"waveform returns to stillness after silence");
                output.AudioLevel=.65;Pump(.2);Check(orbit.Level>.4,"speaker playback also drives waveform");
                socket.Push("{\"serverContent\":{\"outputTranscription\":{\"text\":\"听见啦，一起聊聊吧。\"}}}");Pump(.2);
                Check(Find<TextBlock>(call,"LiveCallTranscript").Text.Contains("听见啦"),"call subtitles follow actual transcript events");Capture(call,Path.Combine(folder,"live.png"));
                call.Width=360;call.Height=480;Pump();Check(All(call).OfType<Button>().All(b=>Inside(b,call)),"call controls fit minimum size");Capture(call,Path.Combine(folder,"live-minimum.png"));
                call.WindowState=WindowState.Minimized;Pump();Click(chat,"StartLiveCall");Pump();Check(call.WindowState==WindowState.Normal,"full chat restores minimized call");
                call.Close();call.Close();Until(()=>!call.IsVisible);Check(mic.Disposed && output.Disposed && !session.IsActive,"repeated close waits for microphone and playback release");
                Check(Find<TextBox>(chat,"MessageInput").Text=="完整对话草稿" && Find<TextBox>(quick,"QuickMessageInput").Text=="快速对话草稿","entering and leaving call preserves both text drafts");
                socket=new FakeSocket();mic=new FakeMic();output=new FakeOutput();socket.Push("{\"setupComplete\":{}}");Click(chat,"StartLiveCall");Until(()=>mic.Started);
                call=app.Windows.OfType<LiveCallWindow>().Single();Click(call,"LiveBackToText");Until(()=>!call.IsVisible);Check(!session.IsActive && chat.IsVisible,"back to text stops capture and restores full chat");
                Console.WriteLine("Live call smoke: "+passed+" passed; screenshots: "+folder);return 0;
            }
            catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}
            finally{if(pet!=null)pet.Close();Pump(.5);app.Shutdown();}
        }
        sealed class FakeSocket:ILiveSocket
        {
            readonly Queue<string> messages=new Queue<string>();readonly SemaphoreSlim ready=new SemaphoreSlim(0);
            public void Push(string text){lock(messages)messages.Enqueue(text);ready.Release();}
            public Task ConnectAsync(ConnectionSettings settings,CancellationToken token){return Task.FromResult(0);}
            public Task SendTextAsync(string text,CancellationToken token){return Task.FromResult(0);}
            public async Task<string> ReceiveTextAsync(CancellationToken token){bool cancelled=false;try{await ready.WaitAsync(token);}catch(OperationCanceledException){cancelled=true;}if(cancelled){await Task.Delay(180);throw new OperationCanceledException(token);}lock(messages)return messages.Dequeue();}
            public void Abort(){}public void Dispose(){}
        }
        sealed class FakeMic:IPcmMicrophone
        {public event Action<byte[]> PcmAvailable;public event Action<string> Failed;public bool Started,Disposed;public void Start(){Started=true;}public void Emit(byte[] pcm){PcmAvailable(pcm);}public void Dispose(){Disposed=true;}}
        sealed class FakeOutput:ILiveAudioOutput,IAudioLevelSource
        {public bool Disposed;public double AudioLevel{get;set;}public void Enqueue(byte[] data,int rate){}public void Interrupt(){AudioLevel=0;}public void SetVolume(int value){}public void Dispose(){Disposed=true;AudioLevel=0;}}
        static void Click(DependencyObject root,string name){Find<Button>(root,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
        static T Find<T>(DependencyObject root,string name) where T:FrameworkElement{return All(root).OfType<T>().FirstOrDefault(e=>e.Name==name);}
        static IEnumerable<DependencyObject> All(DependencyObject root){yield return root;foreach(object item in LogicalTreeHelper.GetChildren(root)){var child=item as DependencyObject;if(child!=null)foreach(var node in All(child))yield return node;}}
        static bool Inside(FrameworkElement element,Window window){var rect=element.TransformToAncestor(window).TransformBounds(new Rect(element.RenderSize));return rect.Left>=0 && rect.Top>=0 && rect.Right<=window.ActualWidth && rect.Bottom<=window.ActualHeight;}
        static void Check(bool condition,string name){if(!condition)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
        static void Until(Func<bool> condition){var end=DateTime.UtcNow.AddSeconds(5);while(!condition() && DateTime.UtcNow<end)Pump(.05);if(!condition())throw new Exception("Timed out");}
        static void Pump(double seconds=.12){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        static void Capture(Window window,string path){var image=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(window);using(var file=File.Create(path)){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));png.Save(file);}}
    }
}
