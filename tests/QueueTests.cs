using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace Dafeiyu.Tests
{
    public static class QueueTests
    {
        static int passed;
        public static int Main(){try{Run().GetAwaiter().GetResult();Console.WriteLine("Queue tests: "+passed+" passed");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
        static async Task Run()
        {
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"queue-test-"+Guid.NewGuid().ToString("N"),"queue.json");
            var api=new Api();var cfg=new PetSettings{ReadReplies=false,Text=new ConnectionSettings{BaseUrl="http://127.0.0.1:1/v1",Model="fixture"}};
            using(var session=new ConversationSession(api,new Speech(),()=>null))
            using(var queue=new ConversationQueue(session,()=>cfg,()=>"E包",path))
            {
                session.SelectPersona("e-bao");queue.Enqueue("one");await Until(()=>api.Calls.Count==1);queue.Enqueue("two");queue.Enqueue("three");
                Check(queue.Items.Count==2,"busy sends enter visible queue");await Until(()=>api.Calls.Count==3 && !queue.IsRunning);
                Check(String.Join(",",api.Calls)=="one,two,three" && api.Maximum==1,"queue sends serially in order");
                queue.Enqueue("stop-me");queue.Enqueue("keep-me");await queue.PauseAsync();
                Check(queue.IsPaused && queue.Items.Single().Text=="keep-me","stop pauses and preserves remaining messages");
                string id=queue.Items.Single().Id;queue.Edit(id,"edited");Check(queue.Items.Single().Text=="edited","queued text is editable");queue.Resume();await Until(()=>!queue.IsRunning && queue.Items.Count==0);
                Check(api.Calls.Last()=="edited","resume sends edited text");
                queue.Enqueue("stop without waiting items");await Task.Delay(20);await queue.PauseAsync();queue.Enqueue("explicit new send");await Until(()=>!queue.IsRunning && queue.Items.Count==0);
                Check(api.Calls.Last()=="explicit new send","new send after stopping an empty queue starts normally");
                queue.Enqueue("original question");queue.Enqueue("after");await Task.Delay(20);await queue.InterruptAsync("make it shorter");await Until(()=>!queue.IsRunning && queue.Items.Count==0);
                Check(String.Join(",",api.Calls.Skip(api.Calls.Count-3))=="original question,make it shorter,after","steering takes priority then resumes previous queue");
                var request=api.Requests[api.Requests.Count-2];Check(request.Any(m=>m.Content=="original question") && request.Any(m=>m.Content.Contains("partial") && m.Content.Contains("未完成")),"steering includes original question and marked partial reply");
                api.CancelRelease=new TaskCompletionSource<bool>();queue.Enqueue("cancel race");await Until(()=>api.Calls.Last()=="cancel race");queue.Enqueue("keep after race");
                var steering=queue.InterruptAsync("priority race");var stopping=queue.PauseAsync();api.CancelRelease.SetResult(true);await Task.WhenAll(steering,stopping);
                Check(queue.IsPaused && !queue.IsRunning && String.Join(",",queue.Items.Select(m=>m.Text))=="priority race,keep after race","explicit stop during steering keeps priority and remaining entries paused");
                api.CancelRelease=null;foreach(var item in queue.Items)queue.Remove(item.Id);queue.Resume();
                api.Fail=true;queue.Enqueue("failure");queue.Enqueue("still waiting");await Until(()=>queue.IsPaused && !queue.IsRunning);
                Check(queue.Items.Count==2 && queue.Items[0].Text=="failure","failure keeps failed text and pauses following messages");
                queue.Remove(queue.Items[0].Id);Check(queue.Items.Single().Text=="still waiting","queued entry can be removed");
                Check(!File.ReadAllText(path).Contains("still waiting"),"disk queue protects pending text with current-user encryption");
                session.SelectPersona("other");Check(queue.Items.Count==0,"queues are isolated by companion");session.SelectPersona("e-bao");Check(queue.Items.Count==1,"companion queue survives switching");
            }
            using(var session=new ConversationSession(new Api(),new Speech(),()=>null))
            using(var queue=new ConversationQueue(session,()=>cfg,()=>"E包",path))
            {session.SelectPersona("e-bao");Check(queue.IsPaused && queue.Items.Single().Text=="still waiting" && !session.IsBusy,"restart restores queue paused without automatic network request");}
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
        static async Task Until(Func<bool> condition){for(int i=0;i<300;i++){if(condition())return;await Task.Delay(10);}throw new Exception("Timed out");}
        sealed class Api:IConversationApi,IStreamingConversationApi
        {
            public readonly List<string> Calls=new List<string>();public readonly List<IList<ChatMessage>> Requests=new List<IList<ChatMessage>>();public int Maximum;int active;public bool Fail;public TaskCompletionSource<bool> CancelRelease;
            public async Task<string> ChatStreamAsync(ConnectionSettings c,IList<ChatMessage> m,Action<string> delta,CancellationToken token)
            {int count=Interlocked.Increment(ref active);Maximum=Math.Max(Maximum,count);Calls.Add(m.Last().Content);Requests.Add(m);try{delta("partial");bool cancelled=false;try{await Task.Delay(120,token);}catch(OperationCanceledException){cancelled=true;}if(cancelled){if(CancelRelease!=null)await CancelRelease.Task;token.ThrowIfCancellationRequested();}if(Fail)throw new ApiException("fixture failure");return "partial done";}finally{Interlocked.Decrement(ref active);}}
            public Task<string> ChatAsync(ConnectionSettings c,IList<ChatMessage> m,CancellationToken t){return ChatStreamAsync(c,m,_=>{},t);}
            public Task<string> TranscribeAsync(ConnectionSettings c,byte[] b,CancellationToken t){throw new NotSupportedException();}public Task<byte[]> SpeechAsync(ConnectionSettings c,string v,string s,CancellationToken t){throw new NotSupportedException();}public void Dispose(){}
        }
        sealed class Speech:IReplySpeech{public Task SpeakAsync(string s,PetSettings p,CancellationToken t){return Task.FromResult(0);}public void Stop(){}public void SetVolume(int v){}public void Dispose(){}}
    }
}
