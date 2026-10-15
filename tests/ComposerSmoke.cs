using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Dafeiyu.Tests
{
    public static class ComposerSmoke
    {
        static int passed;static string folder;
        [STAThread] public static int Main()
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};PetWindow pet=null;
            try
            {
                folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"composer-smoke-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
                var cfg=new PetSettings{ReadReplies=false,AutoUpdate=false,FollowCursor=false,Wander=false,GreetingEnabled=false,HideOnFullscreen=false,Text=new ConnectionSettings{BaseUrl="http://127.0.0.1:1/v1",Model="fixture"}};
                pet=new PetWindow(null,cfg,new SettingsStore(Path.Combine(folder,"settings.json")),null);var session=new ConversationSession(new Api(),new Speech(),()=>null);session.SelectPersona(pet.Skin.Id);typeof(PetWindow).GetField("conversation",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(pet,session);
                cfg.Transcription=new ConnectionSettings{BaseUrl="http://127.0.0.1:1/v1",Model="asr-fixture"};Recorder recorder=null;var dictation=new DictationSession(new Api(),()=>recorder=new Recorder(),()=>cfg);typeof(PetWindow).GetField("dictation",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(pet,dictation);
                pet.Show();pet.ShowChat();pet.ShowQuickChat();Pump(.2);
                var chat=app.Windows.OfType<ChatWindow>().Single();var quick=app.Windows.OfType<QuickChatWindow>().Single();
                foreach(Window window in new Window[]{chat,quick})
                {
                    window.Show();window.UpdateLayout();Pump(.1);
                    bool small=window==quick;var input=Find<TextBox>(window,small?"QuickMessageInput":"MessageInput");var button=Find<Button>(window,small?"SendQuickMessage":"SendMessage");
                    if(input==null || button==null)throw new Exception("Composer controls missing in "+window.GetType().Name+"; visible="+window.IsVisible);
                    SaveHint(Find<Button>(window,"VoiceRecord"),small?"quick-dictation-hint.png":"chat-dictation-hint.png");
                    Check((string)button.Tag=="live" && Find<Button>(window,small?"StopQuickConversation":"StopConversation")==null,"idle composer has one live action");
                    input.Text="first";Check((string)button.Tag=="send","draft changes live action to send");Click(button);Pump(.05);
                    Check((string)button.Tag=="stop","empty busy composer shows stop");input.Text="queued";Check((string)button.Tag=="send" && button.IsEnabled,"busy composer with draft keeps send enabled");Click(button);Pump(.05);
                    Check(pet.InputQueue.Items.Count==1 && Find<Button>(window,"EditQueuedMessage")!=null && Find<Button>(window,"GuideQueuedMessage")!=null,"pending row exposes direct edit and guide actions");
                    Check(Find<Button>(small?(Window)chat:quick,"EditQueuedMessage")!=null,"both windows share queue immediately");
                    Check(!pet.CanApplyUpdate(true),"pending input blocks auto replacement");
                    Click(Find<Button>(window,"EditQueuedMessage"));var edit=Find<TextBox>(window,"QueueEditInput");edit.Text="edited while waiting";Click(Find<Button>(window,"SaveQueuedEdit"));
                    Check(pet.InputQueue.Items.Single().Text=="edited while waiting","inline queue edit updates stored entry");
                    Save(window,small?"quick-queue.png":"chat-queue.png");Click(button);Until(()=>!pet.InputQueue.IsRunning && !session.IsBusy);
                    Check(pet.InputQueue.IsPaused && pet.InputQueue.Items.Count==1,"stop pauses visible queue without discarding it");
                    Click(Find<Button>(window,"RemoveQueuedMessage"));Check(pet.InputQueue.Items.Count==0,"delete removes pending row");
                    pet.InputQueue.Resume();Pump(.1);
                    int history=session.History.Messages.Count;input.Text="已有草稿";window.UpdateLayout();Rect actionBounds=button.TransformToAncestor(window).TransformBounds(new Rect(button.RenderSize));Click(Find<Button>(window,"VoiceRecord"));Pump(.1);
                    var finish=Find<Button>(window,"FinishDictation");Rect finishBounds=finish.TransformToAncestor(window).TransformBounds(new Rect(finish.RenderSize));
                    Check(Math.Abs(actionBounds.X-finishBounds.X)<1 && Math.Abs(actionBounds.Y-finishBounds.Y)<1 && Math.Abs(actionBounds.Width-finishBounds.Width)<1,"blue and black action buttons retain the same position and size: "+actionBounds+" / "+finishBounds);
                    Check(dictation.IsBusy && !session.IsBusy && Find<Button>(window,"FinishDictation").IsVisible,"idle dictation opens waveform without starting AI");
                    SaveHint(Find<Button>(window,"StopDictation"),small?"quick-stop-hint.png":"chat-stop-hint.png");SaveHint(finish,small?"quick-send-hint.png":"chat-send-hint.png");
                    Save(window,small?"quick-dictation.png":"chat-dictation.png");Click(Find<Button>(window,"StopDictation"));Pump(.2);
                    Check(input.Text.Contains("已有草稿") && input.Text.Contains("识别文字") && session.History.Messages.Count==history && !dictation.IsBusy,"dictation appends to draft without sending");
                    input.Clear();Click(Find<Button>(window,"VoiceRecord"));Pump(.1);window.Hide();Pump(.2);window.Show();Pump(.1);
                    Check(recorder.Disposed && input.Text.Length==0 && !dictation.IsBusy,"hiding the host cancels dictation and releases microphone without filling draft");
                    input.Text="AI in progress";Click(button);Pump(.05);Click(Find<Button>(window,"VoiceRecord"));Pump(.1);
                    Check(session.IsBusy && dictation.IsBusy && Find<Button>(window,"StopDictation").IsVisible,"dictation can run while AI replies");
                    Save(window,small?"quick-dictation-busy.png":"chat-dictation-busy.png");Click(Find<Button>(window,"StopDictation"));Pump(.15);
                    Check(session.IsBusy && !dictation.IsBusy && input.Text=="识别文字","left dictation stop fills draft while AI continues");input.Clear();
                    Click(Find<Button>(window,"VoiceRecord"));Pump(.1);Click(Find<Button>(window,"FinishDictation"));Pump(.2);
                    Check(input.Text.Length==0 && pet.InputQueue.Items.Single().Text=="识别文字","right dictation arrow queues recognized text while AI is busy");Click(button);Pump(.1);Click(Find<Button>(window,"RemoveQueuedMessage"));pet.InputQueue.Resume();
                    input.Text="原草稿";Click(Find<Button>(window,"VoiceRecord"));Pump(.1);Click(Find<Button>(window,"FinishDictation"));Pump(.2);
                    Check(session.IsBusy && input.Text.Length==0 && session.History.Messages.Any(m=>m.Role=="user" && m.Content.Contains("原草稿") && m.Content.Contains("识别文字")),"idle right dictation arrow sends combined draft and transcription");Click(button);Pump(.1);pet.InputQueue.Resume();

                }
                Console.WriteLine("Composer smoke: "+passed+" passed; screenshots: "+folder);return 0;
            }
            catch(Exception e){Console.WriteLine(e);return 1;}
            finally{if(pet!=null)pet.Close();Pump(.3);app.Shutdown();}
        }
        static void Click(Button b){if(b==null || !b.IsEnabled)throw new Exception("Button unavailable");b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
        static void Check(bool c,string s){if(!c)throw new Exception(s);passed++;Console.WriteLine("PASS "+s);}
        static T Find<T>(DependencyObject n,string name)where T:FrameworkElement{var t=n as T;if(t!=null && t.Name==name)return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(n);i++){t=Find<T>(VisualTreeHelper.GetChild(n,i),name);if(t!=null)return t;}return null;}
        static void Until(Func<bool> ready){var deadline=DateTime.UtcNow.AddSeconds(5);while(!ready() && DateTime.UtcNow<deadline)Pump(.05);if(!ready())throw new Exception("Timed out waiting for operation cleanup");}
        static void Pump(double seconds){var f=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};t.Tick+=delegate{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
        static void SaveHint(Button button,string file){var hint=(ToolTip)button.ToolTip;hint.IsOpen=true;Pump(.1);Save(hint,file);hint.IsOpen=false;}
        static void Save(FrameworkElement w,string file){w.UpdateLayout();var b=new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth),(int)Math.Ceiling(w.ActualHeight),96,96,PixelFormats.Pbgra32);b.Render(w);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using(var s=File.Create(Path.Combine(folder,file)))png.Save(s);}
        sealed class Api:IConversationApi
        {public async Task<string> ChatAsync(ConnectionSettings s,IList<ChatMessage> m,CancellationToken t){await Task.Delay(30000,t);return "done";}public Task<string> TranscribeAsync(ConnectionSettings s,byte[] b,CancellationToken t){return Task.FromResult("识别文字");}public Task<byte[]> SpeechAsync(ConnectionSettings s,string v,string x,CancellationToken t){throw new NotSupportedException();}public void Dispose(){}}
        sealed class Recorder:IAudioRecorder,IPcmMicrophone
        {public bool Disposed;public bool IsRecording{get{return !Disposed;}}public event Action<byte[]> PcmAvailable;public event Action<string> Failed;public void Start(){if(PcmAvailable!=null){var data=new byte[3200];for(int i=0;i<data.Length;i+=2){short sample=(short)(Math.Sin(i*.04)*15000);data[i]=(byte)sample;data[i+1]=(byte)(sample>>8);}PcmAvailable(data);}}public Task<byte[]> FinishAsync(){return Task.FromResult(WaveData.EncodePcm(new byte[16000]));}public void Dispose(){Disposed=true;}}
        sealed class Speech:IReplySpeech{public Task SpeakAsync(string s,PetSettings p,CancellationToken t){return Task.FromResult(0);}public void Stop(){}public void SetVolume(int v){}public void Dispose(){}}
    }
}
