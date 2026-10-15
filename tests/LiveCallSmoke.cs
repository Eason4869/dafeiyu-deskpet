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
                Click(quick,"ExpandChat");Pump();
                Check(!quick.IsVisible && chat.IsVisible,"expanding quick chat dismisses the bubble");
                pet.ShowQuickChat();Pump();
                var speech=Find<CheckBox>(quick,"TtsOutput");speech.IsChecked=true;Pump();
                Check(cfg.ReadReplies && Find<CheckBox>(chat,"TtsOutput").IsChecked==true,"speaker icons synchronize across both chat headers");
                Find<CheckBox>(chat,"TtsOutput").IsChecked=false;Pump();Check(!cfg.ReadReplies && speech.IsChecked==false,"muted speaker state is shared and saved");
                Check(Find<ComboBox>(chat,"ChatVoiceMode")==null,"Live and speech recognition have separate entries without mode selector");
                Check(Find<Button>(chat,"StopConversation").IsVisible && !Find<Button>(chat,"StopConversation").IsEnabled,"stop remains visible and disabled while idle");
                CheckComposer(quick,"QuickMessageInput","SendQuickMessage","StopQuickConversation");CheckComposer(chat,"MessageInput","SendMessage","StopConversation");
                var portrait=Find<Image>(quick,"QuickAvatar");double portraitCenter=portrait.TransformToAncestor(quick).Transform(new Point(0,portrait.ActualHeight/2)).Y;
                Check(Find<TextBlock>(quick,"QuickTitle")==null && Find<TextBlock>(chat,"ChatTitle")==null,"chat headers use thumbnails without repeated pet names");
                Check(Math.Abs(portraitCenter-speech.TransformToAncestor(quick).Transform(new Point(0,speech.ActualHeight/2)).Y)<1,"quick avatar and speaker share a center line");
                cfg.Text=new ConnectionSettings{BaseUrl="http://127.0.0.1",Model="fixture"};cfg.Transcription=new ConnectionSettings{BaseUrl="http://127.0.0.1",Model="fixture"};cfg.VoiceMode="live";
                var recorder=new FakeRecorder();var api=new FakeApi();
                typeof(ConversationSession).GetField("microphone",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet.Conversation,(Func<IAudioRecorder>)(()=>recorder));
                typeof(ConversationSession).GetField("api",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet.Conversation,api);
                Click(quick,"VoiceRecord");Check(recorder.IsRecording && pet.Conversation.Stage==ConversationStage.Recording && !(quick.Content is LiveCallView),"quick mic records for ASR even when saved mode is Live");
                Click(quick,"VoiceRecord");Until(()=>!pet.Conversation.IsBusy);Pump();Check(api.Recognized==1 && recorder.Disposed && Find<TextBox>(quick,"QuickReply").Text.Contains("语音回复"),"quick mic finishes recognition and shows the reply");
                recorder=new FakeRecorder();Click(quick,"VoiceRecord");quick.Hide();Until(()=>!pet.Conversation.IsBusy);Check(recorder.Disposed && api.Recognized==1,"hiding quick chat cancels its recording without sending");pet.ShowQuickChat();Pump();
                recorder=new FakeRecorder();Click(chat,"VoiceRecord");Check(recorder.IsRecording && !(chat.Content is LiveCallView),"full chat mic also always records for ASR");Click(chat,"StopConversation");Until(()=>!pet.Conversation.IsBusy);Check(recorder.Disposed,"stop button releases the active recorder");pet.Conversation.Clear();
                Pump();Capture(chat,Path.Combine(folder,"chat.png"));Capture(quick,Path.Combine(folder,"quick.png"));
                socket.Push("{\"setupComplete\":{}}");Click(quick,"StartLiveCall");Until(()=>mic.Started);
                var call=quick.Content as LiveCallView;Check(call!=null && call.Host==quick && call.IsVisible,"quick entry embeds the call in its original window");
                Check(app.Windows.Count==3 && Find<TextBox>(quick,"QuickMessageInput")==null,"live covers quick text without adding a window");
                Check(All(quick).OfType<Button>().All(b=>Inside(b,quick)),"compact call controls fit the bubble");
                Check(!pet.CanApplyUpdate(true),"open call blocks automatic installation");
                var orbit=Find<VoiceOrbit>(call,"LiveVoiceOrbit");Check(orbit!=null,"dedicated screen contains graphical voice orbit");
                mic.Emit(new byte[]{0,32,0,224});Pump(.15);Check(orbit.Level>.1,"microphone PCM visibly moves waveform");
                Pump(.7);Check(orbit.Level<.01,"waveform returns to stillness after silence");
                output.AudioLevel=.65;Pump(.2);Check(orbit.Level>.4,"speaker playback also drives waveform");
                socket.Push("{\"serverContent\":{\"outputTranscription\":{\"text\":\"听见啦，一起聊聊吧。\"}}}");Pump(.2);
                Check(Find<TextBlock>(call,"LiveCallTranscript").Text.Contains("听见啦"),"call subtitles follow actual transcript events");Capture(quick,Path.Combine(folder,"live-quick.png"));
                quick.WindowState=WindowState.Minimized;Pump();Click(chat,"StartLiveCall");Pump();Check(quick.WindowState==WindowState.Normal && ReferenceEquals(quick.Content,call),"other entry restores the existing call without duplicating it");
                call.Close();call.Close();Until(()=>!call.IsVisible);Check(mic.Disposed && output.Disposed && !session.IsActive,"repeated close waits for microphone and playback release");
                Check(Find<TextBox>(chat,"MessageInput").Text=="完整对话草稿" && Find<TextBox>(quick,"QuickMessageInput").Text=="快速对话草稿","entering and leaving call preserves both text drafts");
                socket=new FakeSocket();mic=new FakeMic();output=new FakeOutput();socket.Push("{\"setupComplete\":{}}");Click(chat,"StartLiveCall");Until(()=>mic.Started);
                call=chat.Content as LiveCallView;Check(call!=null && !quick.IsVisible && app.Windows.Count==3,"full entry embeds the call and dismisses quick bubble");
                Capture(chat,Path.Combine(folder,"live-full.png"));chat.Width=520;chat.Height=560;Pump();Check(All(chat).OfType<Button>().All(b=>Inside(b,chat)),"full call controls fit minimum size");Capture(chat,Path.Combine(folder,"live-minimum.png"));
                Click(call,"LiveBackToText");Until(()=>!call.IsVisible);Check(!session.IsActive && chat.IsVisible,"back to text stops capture and restores full chat");
                Check(All(chat).OfType<Button>().Where(b=>b.IsVisible).All(b=>Inside(b,chat)),"text controls fit minimum size after returning");
                Capture(chat,Path.Combine(folder,"chat-minimum.png"));
                CheckComposer(chat,"MessageInput","SendMessage","StopConversation");
                socket=new FakeSocket();mic=new FakeMic();output=new FakeOutput();Click(chat,"StartLiveCall");Pump(.05);
                call=chat.Content as LiveCallView;Click(call,"LiveEndCall");Until(()=>!call.IsVisible);
                Check(!mic.Started && !session.IsActive,"hangup during connection never opens microphone");
                socket=new FakeSocket();mic=new FakeMic();output=new FakeOutput();socket.Push("{\"setupComplete\":{}}");Click(chat,"StartLiveCall");Until(()=>mic.Started);
                chat.Close();Until(()=>!chat.IsVisible);Check(mic.Disposed && output.Disposed && !session.IsActive,"closing host releases audio before closing window");
                pet.ShowChat();chat=app.Windows.OfType<ChatWindow>().Single();socket=new FakeSocket();mic=new FakeMic();output=new FakeOutput();socket.Push("{\"setupComplete\":{}}");Click(chat,"StartLiveCall");Until(()=>mic.Started);
                call=chat.Content as LiveCallView;call.Close();chat.Close();Until(()=>!chat.IsVisible);Check(mic.Disposed && !session.IsActive,"closing host during hangup still closes the host after audio cleanup");
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
        sealed class FakeRecorder:IAudioRecorder
        {public bool IsRecording{get;private set;}public bool Disposed;public void Start(){IsRecording=true;}public Task<byte[]> FinishAsync(){IsRecording=false;return Task.FromResult(WaveData.EncodePcm(new byte[16000]));}public void Dispose(){Disposed=true;IsRecording=false;}}
        sealed class FakeApi:IConversationApi
        {
            public int Recognized;public Task<string> ChatAsync(ConnectionSettings s,IList<ChatMessage> m,CancellationToken c){return Task.FromResult("语音回复");}
            public Task<string> TranscribeAsync(ConnectionSettings s,byte[] wav,CancellationToken c){Recognized++;return Task.FromResult("语音输入");}
            public Task<byte[]> SpeechAsync(ConnectionSettings s,string voice,string text,CancellationToken c){return Task.FromResult(WaveData.EncodePcm(new byte[16000]));}public void Dispose(){}
        }
        sealed class FakeMic:IPcmMicrophone
        {public event Action<byte[]> PcmAvailable;public event Action<string> Failed;public bool Started,Disposed;public void Start(){Started=true;}public void Emit(byte[] pcm){PcmAvailable(pcm);}public void Dispose(){Disposed=true;}}
        sealed class FakeOutput:ILiveAudioOutput,IAudioLevelSource
        {public bool Disposed;public double AudioLevel{get;set;}public void Enqueue(byte[] data,int rate){}public void Interrupt(){AudioLevel=0;}public void SetVolume(int value){}public void Dispose(){Disposed=true;AudioLevel=0;}}
        static void CheckComposer(Window host,string inputName,string sendName,string stopName)
        {
            var input=Find<TextBox>(host,inputName);var send=Find<Button>(host,sendName);var stop=Find<Button>(host,stopName);var live=Find<Button>(host,"StartLiveCall");var mic=Find<Button>(host,"VoiceRecord");
            Func<FrameworkElement,Rect> bounds=e=>e.TransformToAncestor(host).TransformBounds(new Rect(e.RenderSize));
            var field=bounds(input);var a=bounds(send);var b=bounds(stop);var voice=bounds(live);
            Check(voice.Right<bounds(mic).Left && bounds(mic).Right<field.Left && a.Left>field.Right && b.Left>a.Right,"composer places Live and microphone left, send/stop right of input");
            Check(a.Size==b.Size && Math.Abs(a.Top+a.Height/2-field.Top-field.Height/2)<1 && Math.Abs(b.Top-a.Top)<1,"composer actions share size and align with input center");
            Check(live.Content is Viewbox && send.Content is Viewbox && stop.Content is Viewbox,"composer controls use vector icons without text labels");
        }
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
