using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace Dafeiyu.Tests
{
 public static class AiGreetingTests
 {
  static int passed;
  static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
  public static int Main(){try{Run().GetAwaiter().GetResult();Console.WriteLine("AI greetings: "+passed+" passed");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
  static PetSettings Config(){return new PetSettings{AiGreetingEnabled=true,Text=new ConnectionSettings{BaseUrl="http://127.0.0.1:1/v1",Model="model-real-id"},SystemPrompt="你是{pet_name}，语气温柔。"};}
  static Task<string> Generate(GreetingService service,PetSettings cfg,IList<ChatMessage> history=null,IList<string> recent=null,CancellationToken cancel=default(CancellationToken))
  {return service.GenerateAsync(cfg,"dafeiyu","大肥鱼",history,new DateTime(2026,10,16,9,0,0),0,recent,cancel);}
  static async Task Run()
  {
   Check(!new PetSettings().AiGreetingEnabled,"AI option defaults off");
   var now=DateTime.UtcNow;var schedule=new GreetingSchedule(now,30,60,()=>0);schedule.Defer(now);Check(!schedule.TryTake(now.AddSeconds(4),true) && schedule.TryTake(now.AddSeconds(5),true),"cancelled due greeting retries once after a short delay");
   var folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ai-greeting-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   var store=new SettingsStore(Path.Combine(folder,"settings.json"));var cfg=Config();store.Save(cfg);Check(store.Load().Copy().AiGreetingEnabled,"opt-in survives save/load/copy");
   File.WriteAllText(Path.Combine(folder,"old.json"),"{\"GreetingEnabled\":true}");Check(!new SettingsStore(Path.Combine(folder,"old.json")).Load().AiGreetingEnabled,"old settings stay opted out");
   foreach(var id in new[]{"e-bao","dafeiyu","marfu","custom"})Check(Enumerable.Range(0,GreetingPhrases.Count).Select(i=>GreetingPhrases.Choose(id,"星星",DateTime.Now,i)).Distinct().Count()==12,"12 distinct role presets: "+id);
   var recent=Enumerable.Range(0,8).Select(i=>GreetingPhrases.Choose("dafeiyu","大肥鱼",new DateTime(2026,10,16,9,0,0),i)).ToList();
   Check(!recent.Contains(GreetingService.Fallback("dafeiyu","大肥鱼",new DateTime(2026,10,16,9,0,0),0,recent)),"fallback avoids recent eight lines");
   var log=new ConversationLog();for(int i=0;i<8;i++)log.Commit("话题"+i,"回答"+i);ChatMessage u;log.BeginTurn("尚未回答","text",out u);
   var memory=CompanionMemory.Recent(log.Messages);Check(memory.Count==8 && memory[0].Content=="话题4" && !memory.Any(m=>m.Content.Contains("尚未")),"memory takes latest four completed turns");
   var longLog=new ConversationLog();longLog.Commit(new string('甲',5000),new string('乙',5000));Check(CompanionMemory.Recent(longLog.Messages).Sum(m=>m.Content.Length)==1200,"memory bounds long messages");
   using(var session=new ConversationSession(new FakeApi(),new SilentSpeech(),()=>null)) {
    session.SelectPersona("e-bao");session.History.Commit("E包的私聊","E包回复");var source=new CompanionMemory(()=>session);
    Check(source.Capture("dafeiyu").Count==0,"memory rejects another companion");
    session.SelectPersona("dafeiyu");session.History.Commit("一起画海边","可以画浪花");Check(source.Capture("dafeiyu")[0].Content=="一起画海边","memory follows active companion history");
   }
   var fake=new FakeApi{Reply="今天想继续画小浪花吗？我陪你慢慢来。"};var service=new GreetingService(()=>fake,TimeSpan.FromSeconds(1));
   cfg.AiGreetingEnabled=false;await Generate(service,cfg,memory);Check(fake.Calls==0,"disabled never creates a model request");
   cfg.AiGreetingEnabled=true;cfg.Text.Model="";await Generate(service,cfg,memory);Check(fake.Calls==0,"missing config uses local preset");cfg=Config();
   string line=await Generate(service,cfg,memory);Check(line==fake.Reply && fake.Calls==1,"valid AI reply is returned");
   Check(fake.Connection.Model=="model-real-id" && fake.Messages[0].Content.Contains("你是大肥鱼") && fake.Messages[1].Content.Contains("话题7"),"request uses actual active model, persona and recent context");
   Check(log.Messages.Count==18,"generation never writes greeting or fallback into chat history");
   fake.Reply="重复的问候。";line=await Generate(service,cfg,memory,new[]{"重复的问候！"});Check(line!=fake.Reply,"AI punctuation-only duplicate uses fallback");
   foreach(var invalid in new[]{"",new string('长',81),"一行\n二行","```代码```"}){fake.Reply=invalid;Check(await Generate(service,cfg)!=invalid,"invalid/oversized output uses fallback");}
   fake.Failure=new ApiException("fixture failure");Check((await Generate(service,cfg)).Contains("大肥鱼"),"API error falls back without exposing error");fake.Failure=null;
   fake.Completion=new TaskCompletionSource<string>();service=new GreetingService(()=>fake,TimeSpan.FromMilliseconds(35));
   Check((await Generate(service,cfg)).Contains("大肥鱼") && fake.Token.IsCancellationRequested,"timeout falls back and cancels transport even if it ignores cancellation");fake.Completion.SetResult("too late");
   fake.Completion=new TaskCompletionSource<string>();service=new GreetingService(()=>fake,TimeSpan.FromSeconds(1));using(var cancel=new CancellationTokenSource()) {
    var task=Generate(service,cfg,memory,null,cancel.Token);cancel.Cancel();bool cancelled=false;try{await task;}catch(OperationCanceledException){cancelled=true;}
    Check(cancelled && fake.Token.IsCancellationRequested,"caller cancellation does not return fallback");fake.Completion.SetResult("late");
   }
   fake.Completion=new TaskCompletionSource<string>();var pending=Generate(service,cfg,memory);cfg.Text.Model="new-selection";cfg.Text.BaseUrl="http://127.0.0.1:2/v1";fake.Completion.SetResult("你好呀。");await pending;
   Check(fake.Connection.Model=="model-real-id" && fake.Connection.BaseUrl.EndsWith(":1/v1"),"active request retains independent model/connection snapshot");
  }
  sealed class FakeApi:IConversationApi
  {
   public int Calls;public string Reply="你好呀。";public Exception Failure;public TaskCompletionSource<string> Completion;public ConnectionSettings Connection;public IList<ChatMessage> Messages;public CancellationToken Token;
   public Task<string> ChatAsync(ConnectionSettings settings,IList<ChatMessage> messages,CancellationToken cancel){Calls++;Connection=settings;Messages=messages;Token=cancel;if(Failure!=null)throw Failure;return Completion==null?Task.FromResult(Reply):Completion.Task;}
   public Task<string> TranscribeAsync(ConnectionSettings s,byte[] w,CancellationToken c){throw new NotSupportedException();}
   public Task<byte[]> SpeechAsync(ConnectionSettings s,string v,string t,CancellationToken c){throw new NotSupportedException();}
   public void Dispose(){}
  }
  sealed class SilentSpeech:IReplySpeech
  {public Task SpeakAsync(string t,PetSettings s,CancellationToken c){throw new Exception("Greeting must not speak");}public void Stop(){}public void SetVolume(int value){}public void Dispose(){}}
 }
}
