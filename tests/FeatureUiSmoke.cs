using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Dafeiyu.Tests
{
    public static class FeatureUiSmoke
    {
        static int passed;
        [STAThread] public static int Main()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            string folder=Path.Combine(Path.GetTempPath(),"deskpet-feature-ui-"+Guid.NewGuid().ToString("N"));
            PetWindow pet=null;ChatWindow chat=null;QuickChatWindow quick=null;Window editorHost=null;
            try
            {
                var cfg=new PetSettings {SaveChatHistory=false,HideOnFullscreen=false,GreetingEnabled=false};
                foreach(string action in cfg.Hotkeys.Keys.ToArray())cfg.Hotkeys[action]="";
                pet=new PetWindow(null,cfg,new SettingsStore(Path.Combine(folder,"settings.json")),null);
                pet.Show();Pump();
                pet.Conversation.Dispose();var api=new UiApi();var session=new ConversationSession(api,new SilentSpeech(),delegate {throw new InvalidOperationException("No microphone in this test");});session.SelectPersona(pet.Skin.Id);typeof(PetWindow).GetField("conversation",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(pet,session);
                for(int i=0;i<55;i++)pet.Conversation.AddLiveTurn(pet.Skin.Id,"问题 "+i,"回复 "+i);
                chat=new ChatWindow(pet);chat.Show();quick=new QuickChatWindow(pet);quick.Show();Pump();
                var historyScroll=Find<ScrollViewer>(chat,"ChatHistoryScroll");Check(historyScroll!=null && historyScroll.ScrollableHeight-historyScroll.VerticalOffset<2,"reopened history scrolls to latest reply");
                var earlier=Find<Button>(chat,"HistoryEarlier");var export=Find<Button>(chat,"ExportChat");
                Check(earlier!=null && earlier.IsEnabled,"earlier-history control for records beyond fifty turns");
                Check(export!=null && export.IsEnabled,"current-pet export control");
                earlier.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
                Check(All(chat).OfType<TextBox>().Any(t=>t.Text=="问题 0"),"earlier history loads oldest retained turn");
                pet.Conversation.Clear();Pump();
                Check(!All(chat).OfType<TextBox>().Any(t=>t.Text=="问题 0"),"clear rebuilds full conversation");
                Check(Find<TextBox>(quick,"QuickReply").Text.Contains("写下一句话"),"clear also resets quick bubble");
                pet.Conversation.AddLiveTurn(pet.Skin.Id,"同步问题","同步回复");Pump();
                Check(Find<TextBox>(quick,"QuickReply").Text=="同步回复","quick bubble shares committed replies");
                var editor=new ConnectionSettingsPanel(pet);
                editorHost=new Window {Content=editor};Ui.Window(editorHost,"音色显示回归",650,780);editorHost.Show();Pump();
                var stream=Find<CheckBox>(editor,"TextServiceStreamReplies");
                Check(stream!=null && stream.IsChecked==true,"stream setting defaults enabled");
                stream.IsChecked=false;Check(editor.HasUnsavedChanges,"stream edit participates in dirty-state protection");
                bool saved=editor.Save();if(!saved)Console.WriteLine("Save diagnostic: "+Find<TextBlock>(editor,"ConnectionNotice").Text+" / "+pet.Notice);Check(saved && !pet.Settings.Text.StreamReplies,"stream preference persists through save");
                editor.ShowSection(2);var voices=Find<ComboBox>(editor,"SpeechVoice");
                Find<CheckBox>(editor,"UseCloudVoice").IsChecked=true;Pump();
                Check(voices!=null && voices.IsEditable,"TTS accepts dropdown and manual voice");
                Find<ComboBox>(editor,"SpeechPreset").SelectedIndex=1;Pump();
                Check(voices.Items.Cast<VoicePreset>().Any(v=>v.Id=="苏打") && voices.Text=="mimo_default","MiMo preset supplies voice choices and default");
                voices.SelectedItem=voices.Items.Cast<VoicePreset>().First(v=>v.Id=="苏打");Pump();
                Check(voices.Text=="苏打","choosing a voice stores only the API ID");
                var speechText=voices.Template.FindName("PART_EditableTextBox",voices) as TextBox;
                Check(speechText!=null && speechText.IsVisible && speechText.Text=="苏打","themed TTS dropdown visibly displays the selected voice");
                Capture(editorHost,voices,"tts-selected.png");
                speechText.Text="my-custom-voice";Pump();Check(voices.Text=="my-custom-voice","visible TTS editor updates the saved value");Find<ComboBox>(editor,"SpeechPreset").SelectedIndex=3;Pump();
                Check(voices.Text=="my-custom-voice" && voices.Items.Cast<VoicePreset>().Any(v=>v.Id=="alloy"),"provider change preserves manually typed voice");
                voices.SelectedItem=voices.Items.Cast<VoicePreset>().First(v=>v.Id=="alloy");Pump();
                Find<ComboBox>(editor,"SpeechPreset").SelectedIndex=1;Pump();Check(voices.Text=="mimo_default","provider change replaces an incompatible known preset");
                editor.ShowSection(3);var liveVoice=Find<ComboBox>(editor,"LiveVoice");Check(liveVoice.IsEditable && liveVoice.Items.Cast<VoicePreset>().Any(v=>v.Id=="Kore" && v.Description.Length>0),"Live offers described editable Gemini voices");
                liveVoice.SelectedItem=liveVoice.Items.Cast<VoicePreset>().First(v=>v.Id=="Kore");Pump();
                var liveText=liveVoice.Template.FindName("PART_EditableTextBox",liveVoice) as TextBox;
                Check(liveText!=null && liveText.IsVisible && liveText.Text=="Kore","themed Live dropdown visibly displays the selected voice");
                Check(editor.Save() && pet.Store.Load().LiveVoice=="Kore","selected Live voice is saved to disk without its description");
                Capture(editorHost,liveVoice,"live-selected.png");
                liveText.Text="manual-live-voice";Pump();Check(editor.Save() && pet.Settings.LiveVoice=="manual-live-voice" && pet.Settings.VoiceName=="mimo_default","manual Live and selected TTS values persist");
                editorHost.Content=null;var reopened=new ConnectionSettingsPanel(pet);editorHost.Content=reopened;reopened.ShowSection(3);Pump();
                var restored=Find<ComboBox>(reopened,"LiveVoice");
                Check(((TextBox)restored.Template.FindName("PART_EditableTextBox",restored)).Text=="manual-live-voice","saved voice is visible after reopening the settings panel");
                editorHost.Close();editorHost=null;
                pet.SetQuietMode(true);Check(pet.Settings.ReadReplies && pet.Settings.QuietMode,"DND keeps saved TTS preference");
                cfg.QuietMode=false;cfg.ReadReplies=false;cfg.Text=new ConnectionSettings{BaseUrl="http://127.0.0.1:9876",Model="ui-fixture",StreamReplies=true};
                session.Clear();var request=session.SendTextAsync("开始流式",cfg,pet.Skin.Name);Wait(()=>api.Delta!=null);
                Task.Run(()=>api.Emit("第一段")).GetAwaiter().GetResult();Pump();
                Check(!request.IsCompleted && All(chat).OfType<TextBox>().Any(t=>t.Text=="第一段") && Find<TextBox>(quick,"QuickReply").Text.Contains("第一段"),"both windows show partial text before completion");
                api.Finish("第一段完整");Wait(()=>request.IsCompleted);request.GetAwaiter().GetResult();
                // Begin again before the final queued update is flushed: the old ID must not replace the new pending reply.
                api.Delta=null;var second=session.SendTextAsync("继续",cfg,pet.Skin.Name);Wait(()=>api.Delta!=null);Pump();
                Check(Find<TextBox>(quick,"QuickReply").Text.Contains("等待回复"),"old final callback cannot replace the latest pending bubble");
                Task.Run(()=>api.Emit("保留部分")).GetAwaiter().GetResult();Pump();var stopped=session.StopAsync();Wait(()=>stopped.IsCompleted && second.IsCompleted);try{second.GetAwaiter().GetResult();}catch(OperationCanceledException){}Pump();
                Check(Find<TextBox>(quick,"QuickReply").Text.Contains("保留部分") && Find<TextBox>(quick,"QuickReply").Text.Contains("已停止") && All(chat).OfType<TextBlock>().Any(t=>t.Text=="已停止"),"stopped stream retains text and status in both windows");
                session.Clear();Pump();Check(Find<TextBox>(quick,"QuickReply").Text.Contains("写下一句话") && !All(chat).OfType<TextBox>().Any(t=>t.Text=="保留部分"),"clear discards queued stream updates");
                Console.WriteLine("Feature UI smoke: "+passed+" passed");return 0;
            }
            catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}
            finally {if(editorHost!=null)editorHost.Close();if(quick!=null)quick.Close();if(chat!=null)chat.Close();if(pet!=null)pet.Close();Pump();app.Shutdown();}
        }
        sealed class UiApi:IConversationApi,IStreamingConversationApi
        {
            public Action<string> Delta;TaskCompletionSource<string> result;
            public async Task<string> ChatStreamAsync(ConnectionSettings cfg,IList<ChatMessage> messages,Action<string> delta,CancellationToken token)
            {result=new TaskCompletionSource<string>();Delta=delta;using(token.Register(()=>result.TrySetCanceled()))return await result.Task;}
            public void Emit(string delta){Delta(delta);}public void Finish(string text){result.SetResult(text);}
            public Task<string> ChatAsync(ConnectionSettings cfg,IList<ChatMessage> messages,CancellationToken token){throw new InvalidOperationException("Expected streaming");}
            public Task<string> TranscribeAsync(ConnectionSettings cfg,byte[] wav,CancellationToken token){throw new InvalidOperationException("No ASR fixture");}
            public Task<byte[]> SpeechAsync(ConnectionSettings cfg,string voice,string text,CancellationToken token){throw new InvalidOperationException("No TTS fixture");}
            public void Dispose(){}
        }
        sealed class SilentSpeech:IReplySpeech {public Task SpeakAsync(string text,PetSettings cfg,CancellationToken token){return Task.FromResult(0);}public void SetVolume(int v){}public void Stop(){}public void Dispose(){}}
        static void Wait(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(5);while(!done() && DateTime.UtcNow<end)Pump();if(!done())throw new Exception("UI stream timed out");}
        static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
        static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(160)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        static void Capture(Window window,FrameworkElement field,string name)
        {
            field.BringIntoView();Pump();
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(window);
            var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"voice-display");Directory.CreateDirectory(directory);
            using(var stream=File.Create(Path.Combine(directory,name)))png.Save(stream);
        }
        static T Find<T>(DependencyObject root,string name) where T:FrameworkElement {return All(root).OfType<T>().FirstOrDefault(x=>x.Name==name);}
        static IEnumerable<DependencyObject> All(DependencyObject root)
        {yield return root;foreach(object child in LogicalTreeHelper.GetChildren(root)){var node=child as DependencyObject;if(node!=null)foreach(var item in All(node))yield return item;}}
    }
}
