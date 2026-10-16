using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Dafeiyu.Tests
{
    public static class AgentWorkbenchSmoke
    {
        static int checks;static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;Console.WriteLine("PASS "+why);}
        [STAThread]public static int Main(string[] args)
        {
            if(args.Length>0 && args[0]=="--command-fixture"){Console.WriteLine("verified by fixture");return 0;}
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};PetWindow pet=null;ChatWindow chat=null;
            try{
                string output=Path.Combine(Directory.GetCurrentDirectory(),"work","agent-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);string project=Path.Combine(output,"project");Directory.CreateDirectory(project);File.WriteAllText(Path.Combine(project,"one.txt"),"before",new UTF8Encoding(false));
                var cfg=new PetSettings{AgentEnabled=true,AgentWorkspace=project,AgentCommandsEnabled=true,ReadReplies=false,GreetingEnabled=false,AutoUpdate=false};foreach(string key in cfg.Hotkeys.Keys.ToArray())cfg.Hotkeys[key]="";cfg.Text.BaseUrl="http://127.0.0.1:1";cfg.Text.Model="fixture";
                cfg.AgentCommandRecipes=new JavaScriptSerializer().Serialize(new[]{new AgentCommand{Id="check",Executable=Assembly.GetExecutingAssembly().Location,Arguments=new[]{"--command-fixture"},Workspace=project}});
                pet=new PetWindow(null,cfg,new SettingsStore(Path.Combine(output,"data","settings.json")),null);
                var session=new ConversationSession(new Model(),new Speech(),()=>null,new HistoryStore(Path.Combine(output,"data","history")));
                session.SelectPersona(pet.Skin.Id);typeof(PetWindow).GetField("conversation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pet,session);
                var panelType=typeof(PetWindow).Assembly.GetType("Dafeiyu.AgentTaskPanel");Check(panelType!=null,"new task controls are available without replacing the chat layout");
                chat=new ChatWindow(pet);chat.Show();Pump();Check(All<Expander>(chat).Any(p=>p.Name=="AgentTaskDetails"),"full chat mounts the additive task panel");
                var send=session.SendTextAsync("修改 one.txt 并执行检查",cfg,pet.Skin.Name);Wait(()=>send.IsCompleted);send.GetAwaiter().GetResult();Pump();
                var panel=All<Expander>(chat).Single(p=>p.Name=="AgentTaskDetails");panel.IsExpanded=true;Pump();
                Check(File.ReadAllText(Path.Combine(project,"one.txt"))=="after","model-selected tool modifies the real fixture file");
                Check(All<TextBlock>(panel).Any(t=>t.Text.Contains("one.txt")),"actual changed file appears in task details");
                Check(All<TextBlock>(panel).Any(t=>t.Text.Contains("check") && t.Text.Contains("0")),"actual command exit zero appears in task details");
                Check(All<Button>(panel).Any(b=>b.Name=="ContinueAgentTask"),"checkpoint continuation is an explicit action");
                Save(chat,Path.Combine(output,"chat-700.png"));chat.Width=520;chat.Height=720;Pump();Save(chat,Path.Combine(output,"chat-520.png"));
                var input=All<TextBox>(chat).Single(t=>t.Name=="MessageInput");input.Text="keep this draft";
                chat.WindowState=WindowState.Maximized;Pump();Check(All<FrameworkElement>(chat).Any(e=>e.Name=="AgentWorkspaceSidebar" && e.IsVisible),"maximized chat reveals functional project and task navigation");
                Check(All<FrameworkElement>(chat).Any(e=>e.Name=="AgentWorkspaceInspector" && e.IsVisible) && All<Expander>(chat).Count(e=>e.Name=="AgentTaskDetails")==1,"maximized inspector reuses the same live task controls");Save(chat,Path.Combine(output,"chat-maximized.png"));
                panel.IsExpanded=false;All<Button>(chat).Single(b=>b.Name=="WorkspaceTaskDetails").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(panel.IsExpanded,"sidebar task navigation expands the actual inspector");
                All<Button>(chat).Single(b=>b.Name=="WorkspaceConversation").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(input.IsKeyboardFocusWithin && input.Text=="keep this draft","sidebar conversation navigation focuses the existing draft");
                All<Button>(chat).Single(b=>b.Name=="WorkspaceProjectSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();var preferences=app.Windows.OfType<SettingsWindow>().Single();Check(All<WorkSettingsPanel>(preferences).Any(p=>p.IsVisible),"sidebar project action opens the real work settings section");preferences.Close();

                chat.WindowState=WindowState.Normal;Pump();Check(!All<FrameworkElement>(chat).Any(e=>e.Name=="AgentWorkspaceSidebar" && e.IsVisible) && input.Text=="keep this draft" && All<Expander>(chat).Single(e=>e.Name=="AgentTaskDetails")==panel,"restoring normal size keeps draft and original compact task layout");Save(chat,Path.Combine(output,"chat-restored.png"));
                var diff=All<Button>(panel).First(b=>b.Name=="ShowAgentDiff");diff.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
                var viewer=app.Windows.Cast<Window>().FirstOrDefault(w=>w.Name=="AgentChangePreview");Check(viewer!=null,"change details open actual before/current content");Save(viewer,Path.Combine(output,"change.png"));viewer.Close();
                session.FlushHistoryAsync().GetAwaiter().GetResult();string checkpoint=Directory.GetFiles(Path.Combine(output,"data","history","agent-tasks"),"*.task").Single();
                using(var held=new FileStream(checkpoint,FileMode.Open,FileAccess.Read,FileShare.Read)){
                    var restore=All<Button>(panel).First(b=>b.Name=="RestoreAgentFile");restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Wait(()=>!session.IsBusy && File.ReadAllText(Path.Combine(project,"one.txt"))=="before");session.FlushHistoryAsync().GetAwaiter().GetResult();
                }
                session.LoadAgentTask(cfg);Pump();Check(session.CurrentAgentTask.Changes.Any(c=>c.Status=="restored") && session.AgentTaskWarning.Length>0,"UI reload preserves unsaved restore state and its warning");
                All<Button>(panel).Single(b=>b.Name=="RetryAgentCheckpoint").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Wait(()=>session.AgentTaskWarning.Length==0);session.FlushHistoryAsync().GetAwaiter().GetResult();
                Check(new AgentTaskStore(Path.GetDirectoryName(checkpoint)).Load(pet.Skin.Id,project).Changes.Any(c=>c.Status=="restored"),"explicit restore persists after storage becomes writable");
                Console.WriteLine("Agent workbench smoke: "+checks+" passed; screenshots="+output);return 0;
            }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}finally{if(chat!=null)chat.Close();if(pet!=null)pet.Close();app.Shutdown();}
        }
        static IEnumerable<T> All<T>(DependencyObject root)where T:DependencyObject{if(root is T)yield return(T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in All<T>(VisualTreeHelper.GetChild(root,i)))yield return item;}
        static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        static void Wait(Func<bool> done){var deadline=DateTime.UtcNow.AddSeconds(15);while(!done() && DateTime.UtcNow<deadline)Pump();if(!done())throw new Exception("fixture timed out");}
        static void Save(Window w,string path){w.UpdateLayout();var image=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(w);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(path))encoder.Save(stream);}
        sealed class Model:IConversationApi,IAgentConversationApi
        {
            public async Task<string> ChatAgentAsync(ConnectionSettings c,IList<ChatMessage> m,AgentWorkspace w,Action<string>d,Action<string>r,Action<AgentToolEvent>t,bool stream,CancellationToken token){
                var serializer=new JavaScriptSerializer();await w.ExecuteAsync("update_plan",serializer.Serialize(new{steps=new[]{new{text="修改文件并验证",status="in_progress"}}}),token);
                await w.ExecuteAsync("read_file","{\"path\":\"one.txt\"}",token);await w.ExecuteAsync("apply_patch",serializer.Serialize(new{edits=new[]{new{path="one.txt",old_text="before",new_text="after"}}}),token);
                await w.ExecuteAsync("run_command","{\"command_id\":\"check\"}",token);await w.ExecuteAsync("update_plan",serializer.Serialize(new{steps=new[]{new{text="修改文件并验证",status="complete"}}}),token);d("文件已修改，检查已完成。");return "文件已修改，检查已完成。";
            }
            public Task<string> ChatAsync(ConnectionSettings c,IList<ChatMessage>m,CancellationToken t){throw new NotSupportedException();}public Task<string> TranscribeAsync(ConnectionSettings c,byte[]w,CancellationToken t){throw new NotSupportedException();}public Task<byte[]> SpeechAsync(ConnectionSettings c,string v,string x,CancellationToken t){throw new NotSupportedException();}public void Dispose(){}
        }
        sealed class Speech:IReplySpeech{public Task SpeakAsync(string t,PetSettings s,CancellationToken c){return Task.FromResult(0);}public void SetVolume(int n){}public void Stop(){}public void Dispose(){}}
    }
}
