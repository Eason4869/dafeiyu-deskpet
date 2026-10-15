using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Dafeiyu
{
    [DataContract] public sealed class QueuedMessage
    {
        [DataMember] public string Id {get;set;}
        [DataMember] public string Text {get;set;}
        [DataMember] public List<ChatMessage> Context {get;set;}
        public QueuedMessage Copy(){return new QueuedMessage{Id=Id,Text=Text,Context=Context==null?null:Context.Select(m=>m.Copy()).ToList()};}
    }
    // One queue belongs to the shared session, so closing or expanding a chat
    // window cannot duplicate requests. Only explicit Resume restarts disk queues.
    public sealed class ConversationQueue : IDisposable
    {
        [DataContract] sealed class StoredQueue{[DataMember] public int Version=1;[DataMember] public string Payload;}
        readonly ConversationSession session;readonly Func<PetSettings> settings;readonly Func<string> name;readonly string path;
        readonly object gate=new object();readonly Dictionary<string,List<QueuedMessage>> queues;
        readonly HashSet<string> paused=new HashSet<string>();bool running,steering,disposed,loadFailed;QueuedMessage inFlight;
        Task runTask=Task.FromResult(0);public string Notice {get;private set;}int pauseRevision;
        public int PauseRevision {get{lock(gate)return pauseRevision;}}
        public event Action Changed;
        public ConversationQueue(ConversationSession session,Func<PetSettings> settings,Func<string> name,string path)
        {
            this.session=session;this.settings=settings;this.name=name;this.path=path;Notice="";
            queues=new Dictionary<string,List<QueuedMessage>>();
            if(File.Exists(path))try
            {
                StoredQueue saved;using(var stream=File.OpenRead(path))saved=(StoredQueue)new DataContractJsonSerializer(typeof(StoredQueue)).ReadObject(stream);
                if(saved==null || saved.Version!=1 || String.IsNullOrEmpty(saved.Payload))throw new SerializationException();
                byte[] data=ProtectedData.Unprotect(Convert.FromBase64String(saved.Payload),null,DataProtectionScope.CurrentUser);
                using(var stream=new MemoryStream(data))queues=(Dictionary<string,List<QueuedMessage>>)new DataContractJsonSerializer(typeof(Dictionary<string,List<QueuedMessage>>)).ReadObject(stream);
                if(queues==null || queues.Any(p=>p.Value==null || p.Value.Count>20 || p.Value.Any(m=>m==null || String.IsNullOrEmpty(m.Id) || String.IsNullOrWhiteSpace(m.Text) || m.Text.Length>8000)))throw new SerializationException();
                foreach(var pair in queues)paused.Add(pair.Key);
            }
            catch(Exception e){if(!(e is IOException || e is UnauthorizedAccessException || e is SerializationException || e is CryptographicException || e is FormatException))throw;queues=new Dictionary<string,List<QueuedMessage>>();loadFailed=true;Notice="排队内容读取失败，原文件已保留。请检查数据目录及 Windows 账户后重启。";}
            session.StateChanged+=SessionChanged;session.HistoryChanged+=SessionChanged;
        }
        List<QueuedMessage> Current{get{List<QueuedMessage> list;string id=session.PersonaId;if(!queues.TryGetValue(id,out list))queues[id]=list=new List<QueuedMessage>();return list;}}
        public IList<QueuedMessage> Items{get{lock(gate)return Current.Select(m=>m.Copy()).ToList().AsReadOnly();}}
        public bool IsRunning{get{lock(gate)return running || steering;}}
        public bool IsPaused{get{lock(gate)return paused.Contains(session.PersonaId);}}
        public bool HasPending{get{lock(gate)return loadFailed || queues.Any(p=>p.Value.Count>0);}}
        static string Validate(string text){text=(text??"").Trim();if(text.Length==0 || text.Length>8000)throw new ApiException("请输入 1～8000 个字符。");return text;}
        void Writable(){if(disposed)throw new ObjectDisposedException("ConversationQueue");if(loadFailed)throw new InvalidOperationException(Notice);}
        void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
            byte[] data;using(var memory=new MemoryStream()){new DataContractJsonSerializer(typeof(Dictionary<string,List<QueuedMessage>>)).WriteObject(memory,queues);data=memory.ToArray();}
            var saved=new StoredQueue{Payload=Convert.ToBase64String(ProtectedData.Protect(data,null,DataProtectionScope.CurrentUser))};
            using(var stream=File.Create(temp))new DataContractJsonSerializer(typeof(StoredQueue)).WriteObject(stream,saved);
            for(int attempt=0;;attempt++)try
            {
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);break;
            }
            catch(IOException e)
            {
                // File scanners can briefly hold the just-written queue between
                // adjacent submissions. Keep the atomic replacement and retry only
                // transient Windows sharing / replacement errors, never permissions.
                int code=e.HResult&0xffff;if(attempt>=3 || (code!=32 && code!=33 && code!=1175) || !File.Exists(temp))throw;
                System.Threading.Thread.Sleep(25*(attempt+1));
            }
        }
        void Announce(){if(!disposed && Changed!=null)Changed();}
        public void Enqueue(string text)
        {
            text=Validate(text);ApiClient.ValidateConnection(settings().Text,ApiClient.ChatPath(settings().Text));
            lock(gate){Writable();if(Current.Count>=20)throw new InvalidOperationException("最多排队 20 条，请先处理或删除一些消息。");bool fresh=Current.Count==0 && !running && !session.IsBusy;var entry=new QueuedMessage{Id=Guid.NewGuid().ToString("N"),Text=text};Current.Add(entry);try{Save();}catch{Current.Remove(entry);throw;}if(fresh)paused.Remove(session.PersonaId);Notice="";}
            Announce();Kick();
        }
        public void Edit(string id,string text)
        {text=Validate(text);lock(gate){Writable();var entry=Current.SingleOrDefault(m=>m.Id==id);if(entry==null)throw new InvalidOperationException("这条消息已开始发送。");string previous=entry.Text;entry.Text=text;try{Save();}catch{entry.Text=previous;throw;}}Announce();}
        public void Remove(string id)
        {lock(gate){Writable();var entry=Current.SingleOrDefault(m=>m.Id==id);if(entry==null)return;int index=Current.IndexOf(entry);Current.Remove(entry);try{Save();}catch{Current.Insert(index,entry);throw;}}Announce();}
        // Resolve stable IDs under the same lock as dequeue. A stale drag must
        // never reinsert a request that has started, or target a removed row.
        public bool MoveBefore(string id,string beforeId)
        {
            lock(gate)
            {
                Writable();var list=Current;var entry=list.SingleOrDefault(m=>m.Id==id);
                if(entry==null || (beforeId!=null && !list.Any(m=>m.Id==beforeId)))return false;
                if(id==beforeId)return true;
                int from=list.IndexOf(entry),to=beforeId==null?list.Count:list.FindIndex(m=>m.Id==beforeId);
                if(from<to)to--;if(from==to)return true;
                list.RemoveAt(from);list.Insert(to,entry);
                try{Save();}catch{list.RemoveAt(to);list.Insert(from,entry);throw;}
            }
            Announce();return true;
        }
        public void Resume(){lock(gate){Writable();paused.Remove(session.PersonaId);Notice="";}Announce();Kick();}
        public void Hold(){lock(gate)paused.Add(session.PersonaId);Announce();}
        public async Task PauseAsync()
        {
            Task wait;lock(gate){pauseRevision++;paused.Add(session.PersonaId);wait=runTask;}Announce();await session.StopAsync();await wait;
        }
        public async Task InterruptAsync(string text,string queuedId=null)
        {
            text=Validate(text);ApiClient.ValidateConnection(settings().Text,ApiClient.ChatPath(settings().Text));
            string persona;Task wait;QueuedMessage entry;List<ChatMessage> preceding;int revision;
            lock(gate)
            {
                Writable();if(steering)throw new InvalidOperationException("正在切换回复，请稍候。");persona=session.PersonaId;
                entry=queuedId==null?new QueuedMessage{Id=Guid.NewGuid().ToString("N"),Text=text}:Current.SingleOrDefault(m=>m.Id==queuedId);
                if(entry==null)throw new InvalidOperationException("这条消息已开始发送。");if(queuedId==null && Current.Count>=20)throw new InvalidOperationException("队列已满，请先删除一条消息。");
                // Persist the new instruction before stopping a request or clearing its draft.
                if(queuedId==null){Current.Add(entry);try{Save();}catch{Current.Remove(entry);throw;}}
                preceding=inFlight==null || inFlight.Context==null?new List<ChatMessage>():inFlight.Context.Select(m=>m.Copy()).ToList();revision=pauseRevision;steering=true;paused.Add(persona);wait=runTask;
            }
            Announce();
            try
            {
                await session.StopAsync();await wait;
                lock(gate)
                {
                    if(disposed)return;
                    if(session.PersonaId!=persona)throw new InvalidOperationException("伙伴已切换，消息保留在原队列。");
                    var history=session.History.Messages;var last=history.LastOrDefault();
                    if(last!=null && last.Role=="assistant" && last.Status!="complete")
                    {
                        var original=history.LastOrDefault(m=>m.TurnId==last.TurnId && m.Role=="user");entry.Context=preceding;if(original!=null)entry.Context.Add(original.Copy());
                        if(!String.IsNullOrEmpty(last.Content))entry.Context.Add(new ChatMessage("assistant",last.Content+"\n[以上是被用户打断的未完成回复，请按新的要求继续。]"));
                        while(entry.Context.Count>2 && (entry.Context.Count>20 || entry.Context.Sum(m=>(long)m.Content.Length)>100000))entry.Context.RemoveRange(0,2);
                    }
                    Current.Remove(entry);Current.Insert(0,entry);Save();if(revision==pauseRevision)paused.Remove(persona);Notice="";
                }
            }
            catch{lock(gate){paused.Add(persona);}throw;}
            finally{lock(gate)steering=false;Announce();Kick();}
        }
        void SessionChanged(){Announce();Kick();}
        void Kick()
        {
            lock(gate){if(disposed || running || steering || session.IsBusy || paused.Contains(session.PersonaId) || Current.Count==0)return;running=true;runTask=RunAsync(session.PersonaId);}
        }
        async Task RunAsync(string persona)
        {
            // Yield once so runTask is assigned before cancellation or event handlers run.
            await Task.Yield();
            try
            {
                while(true)
                {
                    QueuedMessage entry;
                    lock(gate)
                    {
                        if(disposed || steering || paused.Contains(persona) || session.PersonaId!=persona || Current.Count==0 || session.IsBusy)return;
                        entry=Current[0];Current.RemoveAt(0);try{Save();}catch{Current.Insert(0,entry);paused.Add(persona);Notice="排队内容保存失败，已暂停。请检查数据目录权限。";return;}inFlight=entry;
                    }
                    Announce();
                    try{await session.SendTextAsync(entry.Text,settings(),name(),entry.Context);}
                    catch(OperationCanceledException){lock(gate)paused.Add(persona);return;}
                    catch(Exception e)
                    {
                        lock(gate){queues[persona].Insert(0,entry);pauseRevision++;paused.Add(persona);Notice=e is ApiException || e is InvalidOperationException?e.Message:"发送未完成，队列已暂停。";try{Save();}catch(IOException){Notice+=" 队列保存失败，请检查数据目录。";}catch(UnauthorizedAccessException){Notice+=" 队列保存失败，请检查数据目录权限。";}}return;
                    }
                }
            }
            finally{lock(gate){running=false;inFlight=null;}Announce();Kick();}
        }
        public void Dispose(){lock(gate){disposed=true;foreach(var key in queues.Keys)paused.Add(key);}session.StateChanged-=SessionChanged;session.HistoryChanged-=SessionChanged;}
    }
}
