using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dafeiyu.Tests
{
    public static class AgentSessionTests
    {
        public static int Main(){try{Run().GetAwaiter().GetResult();Console.WriteLine("Agent session tests: 15 passed");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
        static async Task Run()
        {
            var settings=new PetSettings{AgentEnabled=true,ReadReplies=false,Text=new ConnectionSettings{BaseUrl="http://127.0.0.1:9876",Model="test"}};
            var api=new Model();var voice=new Speech();using(var session=new ConversationSession(api,voice,()=>null))
            {
                settings.AgentGoal="整理本项目的使用文档";settings.AgentPlanMode=true;
                var task=session.SendTextAsync("写一份文档",settings,"E包");Check(api.ReadOnly && api.System.Contains(settings.AgentGoal),"persistent goal and planning mode reach the actual model request");api.Think("先检查工作目录。");api.Answer("我会先读取文件。");api.Tool("running");
                var pending=session.History.Messages.Last();settings.AgentEnabled=false;await Task.Delay(1050);
                Check(pending.ProcessingStartedAt.HasValue && pending.WorkMode && pending.ProcessingLabel(DateTime.UtcNow).StartsWith("处理时间 1秒"),"timer advances without stream tokens and retains request mode after switching settings");settings.AgentEnabled=true;
                Check(session.History.Messages.Last().Content=="" && session.History.Messages.Last().Reasoning.Contains("先读取文件"),"provisional explanation moves into thought when tool starts");
                Check(session.History.Messages.Last().ToolEvents.Single().Status=="running","running tool reaches the same assistant message");
                api.Tool("complete");api.Answer("# 已完成\n\n**文档**已写入。");api.Finish();await task;
                var reply=session.History.Messages.Last();Check(reply.Status=="complete" && reply.Content.StartsWith("# 已完成") && !reply.Content.Contains("先读取"),"final answer contains only final round Markdown");
                Check(reply.ProcessingMilliseconds>=1000 && reply.ProcessingLabel(DateTime.UtcNow)==reply.ProcessingLabel(DateTime.UtcNow.AddHours(1)),"completed response freezes its full processing duration");
                Check(reply.ToolEvents.Single().Status=="complete" && reply.Reasoning.Contains("工作目录"),"thought and tools are retained separately");
                await session.SpeakReplyAsync(reply.Id,settings);Check(voice.Text.Contains("已完成") && !voice.Text.Contains("**") && !voice.Text.Contains("工作目录"),"manual speech reads plain final answer only");
                string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"agent-session-history",Guid.NewGuid().ToString("N"));var store=new HistoryStore(path);await store.SaveAsync("pet",session.History.Messages);var saved=store.Load("pet");Check(saved.Last().ToolEvents.Single().Status=="complete","encrypted history retains completed tool records");
                Check(saved.Last().WorkMode && saved.Last().ProcessingMilliseconds==reply.ProcessingMilliseconds && saved.Last().ProcessingStartedAt.HasValue && Math.Abs((saved.Last().ProcessingStartedAt.Value-reply.ProcessingStartedAt.Value).TotalMilliseconds)<1,"encrypted history retains request mode start time and final duration at JSON millisecond precision");
                task=session.SendTextAsync("继续",settings,"E包");api.Tool("running");await session.StopAsync();try{await task;}catch(OperationCanceledException){}
                Check(session.History.Messages.Last().Status=="cancelled" && session.History.Messages.Last().ToolEvents.Single().Status=="error","cancel marks running tool interrupted and keeps history");
                var cancelled=session.History.Messages.Last();Check(cancelled.ProcessingLabel(DateTime.UtcNow)==cancelled.ProcessingLabel(DateTime.UtcNow.AddMinutes(5)),"cancelled timer remains frozen");
                settings.AgentEnabled=false;settings.AgentWorkspace=Path.Combine(Path.GetTempPath(),"not-created-"+Guid.NewGuid().ToString("N"));await session.SendTextAsync("普通聊天",settings,"E包");Check(api.PlainCalls==1,"disabled agent uses ordinary conversation without tools");Check(api.AgentCalls==2 && !Directory.Exists(settings.AgentWorkspace) && api.PlainSystem.Contains("纯对话模式") && !api.PlainSystem.Contains(settings.AgentGoal),"pure chat ignores work permissions goals and invalid workspace without creating or invoking tools");
                Check(!session.History.Messages.Last().WorkMode && session.History.Messages.Last().ProcessingLabel(DateTime.UtcNow).StartsWith("思考时间 "),"pure chat uses thinking duration label");
            }
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
        sealed class Model:IConversationApi,IAgentConversationApi
        {
            Action<string> delta,reasoning;Action<AgentToolEvent> tools;TaskCompletionSource<string> done;string text="";public int PlainCalls,AgentCalls;public string PlainSystem;public bool ReadOnly;public string System;
            public Task<string> ChatAgentAsync(ConnectionSettings s,IList<ChatMessage> m,AgentWorkspace w,Action<string> a,Action<string> r,Action<AgentToolEvent> t,bool stream,CancellationToken c){AgentCalls++;ReadOnly=w.ReadOnly;System=m.First().Content;text="";delta=a;reasoning=r;tools=t;done=new TaskCompletionSource<string>();c.Register(()=>done.TrySetCanceled());return done.Task;}
            public void Think(string value){reasoning(value);}public void Answer(string value){text+=value;delta(value);}public void Tool(string status){if(status=="running")text="";tools(new AgentToolEvent{Id="call",Name="read_file",Summary="已读取 note.md",Status=status});}public void Finish(){done.SetResult(text);}
            public Task<string> ChatAsync(ConnectionSettings s,IList<ChatMessage> m,CancellationToken c){PlainCalls++;PlainSystem=m.First().Content;return Task.FromResult("普通回复");}public Task<string> TranscribeAsync(ConnectionSettings s,byte[] w,CancellationToken c){throw new NotSupportedException();}public Task<byte[]> SpeechAsync(ConnectionSettings s,string v,string t,CancellationToken c){throw new NotSupportedException();}public void Dispose(){}
        }
        sealed class Speech:IReplySpeech{public string Text="";public Task SpeakAsync(string text,PetSettings s,CancellationToken c){Text=text;return Task.FromResult(0);}public void SetVolume(int n){}public void Stop(){}public void Dispose(){}}
    }
}
