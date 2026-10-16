using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dafeiyu
{
    public enum ConversationStage { Idle, Recording, Recognizing, Thinking, Speaking }
    public sealed class ConversationSession : IDisposable
    {
        private readonly IConversationApi api;
        private readonly IReplySpeech speech;
        private readonly Func<IAudioRecorder> microphone;
        private readonly HistoryStore store;
        private readonly AgentTaskStore taskStore;
        private readonly Dictionary<string,AgentTaskState> agentTasks=new Dictionary<string,AgentTaskState>();
        public event Action AgentTaskChanged;
        public string AgentTaskWarning {get{return taskStore==null?"":taskStore.Warning;}}
        public AgentTaskState CurrentAgentTask {get{lock(gate){AgentTaskState task;return agentTasks.TryGetValue(personaId,out task)?task.Copy():null;}}}
        private readonly Func<bool> saveEnabled;
        private readonly object gate=new object();
        private Operation active;
        private bool disposed;
        private readonly Dictionary<string,ConversationLog> histories=new Dictionary<string,ConversationLog>();
        private string personaId="";
        public ConversationLog History { get; private set; }
        public ConversationStage Stage { get; private set; }
        public bool IsBusy { get { lock(gate) return active!=null; } }
        public string PersonaId { get { lock(gate)return personaId; } }
        public string LastNotice { get; private set; }
        public string HistoryWarning { get { return store==null?"":store.Warning; } }
        public event Action StateChanged;
        public event Action<ChatMessage> MessageAdded;
        public event Action<ChatMessage> MessageUpdated;
        public event Action HistoryChanged;
        private sealed class Operation
        {
            public readonly CancellationTokenSource Cancel=new CancellationTokenSource();
            public readonly TaskCompletionSource<bool> Complete=new TaskCompletionSource<bool>();
            public PetSettings Snapshot,Live; public IAudioRecorder Recorder; public bool Finishing; public int Ended;
            public Action<string> RecordingFailure;
            public ConversationLog History;public string PersonaId,ReplyId,Source="text";public IList<ChatMessage> SteeringContext;
            public readonly StringBuilder Partial=new StringBuilder();public readonly Stopwatch Checkpoint=Stopwatch.StartNew();
            public readonly StringBuilder Reasoning=new StringBuilder();public readonly Stopwatch ThinkingTime=Stopwatch.StartNew();public readonly Stopwatch ProcessingTime=new Stopwatch();public List<ChatAttachment> Attachments;
            public AgentWorkspace Workspace;public AgentTaskState ResumeTask;
            public readonly List<AgentToolEvent> ToolEvents=new List<AgentToolEvent>();public bool Regenerating;
        }
        public ConversationSession(IConversationApi transport,IReplySpeech output,Func<IAudioRecorder> recorderFactory,HistoryStore historyStore=null,Func<bool> persistenceEnabled=null)
        { api=transport;speech=output;microphone=recorderFactory;store=historyStore;taskStore=store==null?null:new AgentTaskStore(System.IO.Path.Combine(store.DirectoryPath,"agent-tasks"));saveEnabled=persistenceEnabled??delegate{return true;};History=new ConversationLog();histories[""]=History;LastNotice="";if(store!=null)store.WarningChanged+=OnWarningChanged; }
        private void OnWarningChanged() { if(!disposed && StateChanged!=null)StateChanged(); }
        private ConversationLog GetHistory(string id)
        {
            ConversationLog history;
            lock(gate)if(histories.TryGetValue(id,out history))return history;
            // Storage completion can publish warnings back into the session/queue.
            // Never wait for that worker while holding the session gate.
            var loaded=new ConversationLog();if(store!=null)loaded.Replace(store.Load(id));
            lock(gate){if(histories.TryGetValue(id,out history))return history;histories[id]=loaded;return loaded;}
        }
        private void Save(string id,ConversationLog history)
        { if(store!=null && saveEnabled())store.SaveAsync(id,history.Messages); }
        public void SelectPersona(string id)
        {
            id=id??"";
            lock(gate){if(active!=null)throw new InvalidOperationException("请先停止对话再切换伙伴。");if(disposed)throw new ObjectDisposedException("ConversationSession");}
            var selected=GetHistory(id);
            lock(gate)
            {
                if(active!=null) throw new InvalidOperationException("请先停止对话再切换伙伴。");
                if(disposed)throw new ObjectDisposedException("ConversationSession");History=selected;personaId=id;
            }
            if(HistoryChanged!=null)HistoryChanged();
        }
        public void AddLiveTurn(string id,string user,string reply,string status="complete")
        {
            id=id??"";var history=GetHistory(id);
            lock(gate) {
                if(disposed || (String.IsNullOrWhiteSpace(reply) && (status=="complete" || String.IsNullOrWhiteSpace(user))))return;
                if(status!="complete" && status!="interrupted" && status!="cancelled" && status!="error")status="interrupted";
                ChatMessage input;var output=history.BeginTurn(String.IsNullOrWhiteSpace(user)?"（语音对话）":user,"live",out input);output=history.Update(output.Id,reply,status);Save(id,history);
                if(id==personaId){Emit(input);if(output!=null)Emit(output);}
            }
        }
        private Operation Begin(PetSettings settings,ConversationStage stage)
        {
            Operation op;
            lock(gate)
            {
                if(disposed) throw new ObjectDisposedException("ConversationSession");
                if(active!=null) throw new InvalidOperationException("正在处理当前对话，请先停止或等待完成。");
                op=new Operation { Snapshot=settings.Copy(),Live=settings,History=History,PersonaId=personaId }; active=op; LastNotice="";
            }
            SetStage(stage); return op;
        }
        private void SetStage(ConversationStage stage)
        { Stage=stage; if(!disposed && StateChanged!=null) StateChanged(); }
        private void Emit(ChatMessage message)
        { if(!disposed && MessageAdded!=null) MessageAdded(message.Copy()); }
        public async Task SendTextAsync(string text,PetSettings settings,string petName,IList<ChatMessage> steeringContext=null,IList<ChatAttachment> attachments=null,AgentTaskState resumeTask=null)
        {
            var files=ChatAttachment.Snapshot(attachments);if(String.IsNullOrWhiteSpace(text) && files.Count>0)text="请查看附件。";
            text=(text??"").Trim(); if(text.Length==0 || text.Length>8000) throw new ApiException("请输入 1～8000 个字符。");
            ApiClient.ValidateConnection(settings.Text,ApiClient.ChatPath(settings.Text));
            Operation op=Begin(settings,ConversationStage.Thinking);
            op.SteeringContext=steeringContext;
            op.Attachments=files;op.ResumeTask=resumeTask==null?null:resumeTask.Copy();
            try { await RequestReply(op,text,petName); }
            catch(OperationCanceledException) { LastNotice="已停止本次对话。"; throw; }
            finally { End(op); }
        }
        public void StartRecording(PetSettings settings)
        {
            ValidateVoice(settings); Operation op=Begin(settings,ConversationStage.Recording);
            try {
                op.Recorder=microphone();var failures=op.Recorder as IPcmMicrophone;
                if(failures!=null){op.RecordingFailure=message=>RecordingFailed(op,message);failures.Failed+=op.RecordingFailure;}
                op.Recorder.Start();
            }
            catch { End(op); throw; }
        }
        private void RecordingFailed(Operation op,string message)
        {
            lock(gate) {
                if(disposed || active!=op || op.Finishing || op.Ended!=0)return;
                op.Finishing=true;LastNotice=String.IsNullOrWhiteSpace(message)?"麦克风录音中断，请检查设备及权限。":message;
                op.Cancel.Cancel();
            }
            // A device may raise failure on its capture worker; disposing there can
            // deadlock when the recorder waits for that same worker to finish.
            Task.Run(delegate { End(op); });
        }
        private static void ValidateVoice(PetSettings settings)
        { ApiClient.ValidateConnection(settings.Text,ApiClient.ChatPath(settings.Text)); var connection=settings.TranscriptionConnection(); ApiClient.ValidateConnection(connection,ApiClient.AsrPath(connection)); }
        public async Task FinishRecordingAsync(string petName)
        {
            Operation op;
            lock(gate)
            {
                op=active;
                if(op==null || op.Recorder==null || op.Finishing) throw new InvalidOperationException("当前没有可发送的录音。");
                op.Finishing=true;
            }
            try
            {
                SetStage(ConversationStage.Recognizing); byte[] wav=await op.Recorder.FinishAsync();
                op.Cancel.Token.ThrowIfCancellationRequested();
                await RecognizeAndReply(op,wav,petName);
            }
            catch(Exception)
            { if(op.Cancel.IsCancellationRequested) { LastNotice="录音或对话已停止。"; throw new OperationCanceledException(op.Cancel.Token); } throw; }
            finally { End(op); }
        }
        public async Task SendVoiceAsync(byte[] wav,PetSettings settings,string petName)
        {
            if(!WaveData.IsWave(wav)) throw new ApiException("录音文件不完整，请重新录音。");
            ValidateVoice(settings); Operation op=Begin(settings,ConversationStage.Recognizing);
            try { await RecognizeAndReply(op,wav,petName); }
            catch(OperationCanceledException) { LastNotice="语音对话已停止。"; throw; }
            finally { End(op); }
        }
        private async Task RecognizeAndReply(Operation op,byte[] wav,string petName)
        {
            string text;try{text=await api.TranscribeAsync(op.Snapshot.TranscriptionConnection(),wav,op.Cancel.Token);}catch(ApiException e){throw new ApiException("语音识别："+e.Message);}
            op.Cancel.Token.ThrowIfCancellationRequested();op.Source="asr";await RequestReply(op,text,petName);
        }
        private async Task RequestReply(Operation op,string text,string petName)
        {
            CancellationToken token=op.Cancel.Token; token.ThrowIfCancellationRequested();
            string persona=(op.Snapshot.SystemPrompt??"").Replace("{pet_name}",petName??"桌面伙伴");
            if(!op.Snapshot.AgentEnabled)persona+="\n当前为纯对话模式，只能聊天和分析已提供的内容，没有文件、目录或其他 Agent 工具；不要声称已经操作电脑。";
            if(op.Snapshot.AgentEnabled && !String.IsNullOrWhiteSpace(op.Snapshot.AgentGoal))persona+="\n用户设置的持续目标（在当前请求范围内推进，不能声称未完成的操作成功）：\n"+op.Snapshot.AgentGoal;
            if(op.Snapshot.AgentEnabled && op.Snapshot.AgentPlanMode)persona+="\n当前为计划模式：仅分析、阅读并提出计划，不执行写入或修改文件。";
            if(op.ResumeTask!=null)persona+="\n"+op.ResumeTask.CheckpointText();
            var request=op.Regenerating?op.History.ForRegeneration(op.ReplyId,persona):op.History.ForRequest(persona,text);
            if(!op.Regenerating)request.Last().SetAttachments(op.Attachments);
            if(op.SteeringContext!=null)request.InsertRange(request.Count-1,op.SteeringContext.Select(m=>m.Copy()));
            ConversationLog.BudgetRequest(request);
            lock(gate) {
                token.ThrowIfCancellationRequested();if(disposed)throw new OperationCanceledException(token);
                op.ProcessingTime.Restart();
                if(op.Regenerating){op.History.StartProcessing(op.ReplyId,op.Snapshot.AgentEnabled);UpdateReply(op,"","pending",false);}
                else {ChatMessage user;var reply=op.History.BeginTurn(text,op.Source,out user);op.History.Attach(user.Id,op.Attachments);user.SetAttachments(op.Attachments);op.ReplyId=reply.Id;reply=op.History.StartProcessing(reply.Id,op.Snapshot.AgentEnabled);Emit(user);Emit(reply);}
                Save(op.PersonaId,op.History);op.Checkpoint.Restart();op.ThinkingTime.Restart();
            }
            SetStage(ConversationStage.Thinking);string answer;
            try {
                var streaming=api as IStreamingConversationApi;
                var reasoning=api as IReasoningConversationApi;
                var agent=api as IAgentConversationApi;
                if(op.Snapshot.AgentEnabled && Environment.OSVersion.Platform==PlatformID.Win32NT && agent!=null){
                    op.Workspace=OpenAgentWorkspace(op.Snapshot);
                    var state=op.ResumeTask??new AgentTaskState{PersonaId=op.PersonaId,Workspace=op.Workspace.Root,Goal=text};
                    if(state.PersonaId!=op.PersonaId)throw new InvalidOperationException("任务检查点不属于当前伙伴。");
                    op.Workspace.TaskChanged=task=>RecordAgentTask(op,task);op.Workspace.BeginTask(state);
                }
                answer=op.Workspace!=null?await agent.ChatAgentAsync(op.Snapshot.Text,request,op.Workspace,delta=>AppendDelta(op,delta),delta=>AppendReasoning(op,delta),item=>AppendToolEvent(op,item),op.Snapshot.Text.StreamReplies,token):reasoning!=null?await reasoning.ChatWithReasoningAsync(op.Snapshot.Text,request,delta=>AppendDelta(op,delta),delta=>AppendReasoning(op,delta),op.Snapshot.Text.StreamReplies,token):streaming!=null && op.Snapshot.Text.StreamReplies ? await streaming.ChatStreamAsync(op.Snapshot.Text,request,delta=>AppendDelta(op,delta),token) : await api.ChatAsync(op.Snapshot.Text,request,token);
                token.ThrowIfCancellationRequested();UpdateReply(op,answer,"complete",true);if(op.Workspace!=null)op.Workspace.FinishTask("complete","");
            }
            catch(Exception e) {
                if(op.Workspace!=null)op.Workspace.FinishTask(token.IsCancellationRequested || e is OperationCanceledException?"cancelled":"error",e is ApiException?e.Message:"任务中断，继续前请核对已完成操作。");
                UpdateReply(op,op.Partial.ToString(),token.IsCancellationRequested || e is OperationCanceledException?"cancelled":"error",true);
                if(e is ApiException)throw new ApiException("文字对话："+e.Message);throw;
            }
            if(op.Live.ReadReplies && op.Live.Volume>0 && !op.Live.QuietMode)
            {
                SetStage(ConversationStage.Speaking);
                try { await speech.SpeakAsync(MarkdownContent.PlainText(answer),op.Live.Copy(),token); }
                catch(OperationCanceledException) { if(token.IsCancellationRequested) throw; LastNotice="回复已保留，朗读已停止。"; }
                catch(Exception e)
                { if(!(e is ApiException || e is InvalidOperationException || e is System.IO.IOException || e is UnauthorizedAccessException)) throw; LastNotice="回复已保留；朗读失败："+(e is ApiException || e is InvalidOperationException ? e.Message : "请检查输出设备或音频缓存目录。"); }
            }
        }
        private void AppendDelta(Operation op,string delta)
        {
            if(String.IsNullOrEmpty(delta))return;
            lock(gate) {
                if(disposed || active!=op || op.Cancel.IsCancellationRequested)return;
                if(op.Partial.Length+delta.Length>50000)throw new ApiException("文字回复超过长度限制。");
                op.ThinkingTime.Stop();
                op.Partial.Append(delta);UpdateReply(op,op.Partial.ToString(),"pending",op.Checkpoint.ElapsedMilliseconds>=1000);
            }
        }
        private void UpdateReply(Operation op,string content,string status,bool persist)
        {
            lock(gate) {
                if(disposed || active!=op || op.ReplyId==null)return;
                if(status=="complete")op.Cancel.Token.ThrowIfCancellationRequested();
                if(status!="pending")
                {
                    op.ThinkingTime.Stop();op.ProcessingTime.Stop();
                    foreach(var item in op.ToolEvents.Where(e=>e.Status=="running")){item.Status="error";item.Summary+=(status=="cancelled"?" · 已停止":" · 已中断");}
                }
                var updated=op.History.Update(op.ReplyId,content,status,op.Reasoning.ToString(),op.ThinkingTime.ElapsedMilliseconds,op.ToolEvents,op.ProcessingTime.ElapsedMilliseconds);
                if(updated!=null && op.PersonaId==personaId && MessageUpdated!=null)MessageUpdated(updated);
                if(persist){Save(op.PersonaId,op.History);op.Checkpoint.Restart();}
            }
        }
        private void AppendReasoning(Operation op,string delta)
        {
            if(String.IsNullOrEmpty(delta))return;
            lock(gate){if(disposed || active!=op || op.Cancel.IsCancellationRequested)return;if(op.Reasoning.Length>50000-delta.Length)throw new ApiException("思考内容超过长度限制。");op.Reasoning.Append(delta);UpdateReply(op,op.Partial.ToString(),"pending",op.Checkpoint.ElapsedMilliseconds>=1000);}
        }
        private void AppendToolEvent(Operation op,AgentToolEvent item)
        {
            lock(gate)
            {
                if(disposed || active!=op || op.Cancel.IsCancellationRequested)return;
                if(item.Status=="running" && op.Partial.Length>0){if(op.Reasoning.Length+op.Partial.Length<50000)op.Reasoning.AppendLine(op.Partial.ToString());op.Partial.Clear();op.ThinkingTime.Start();}
                int index=op.ToolEvents.FindIndex(e=>e.Id==item.Id);if(index>=0)op.ToolEvents[index]=item.Copy();else if(op.ToolEvents.Count<100)op.ToolEvents.Add(item.Copy());
                UpdateReply(op,op.Partial.ToString(),"pending",op.Checkpoint.ElapsedMilliseconds>=1000);
            }
        }
        public static AgentWorkspace OpenAgentWorkspace(PetSettings settings)
        {
            string path=System.IO.Path.GetFullPath(settings.AgentWorkspace??"");
            string defaultPath=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data","agent-workspace"));
            if(!System.IO.Directory.Exists(path))
            {
                if(!String.Equals(path,defaultPath,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Agent 工作目录不存在，请在工作设置中重新选择。");
                for(var directory=new System.IO.DirectoryInfo(path);directory!=null;directory=directory.Parent)
                    if(directory.Exists && (directory.Attributes&System.IO.FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Agent 工作目录不能穿过符号链接。");
                System.IO.Directory.CreateDirectory(path);
            }
            try{var workspace=new AgentWorkspace(path,!settings.AgentFullAccess){ReadOnly=settings.AgentPlanMode,BackupEnabled=settings.AgentBackupFiles,MaximumTurns=settings.AgentMaxTurns,TimeoutMinutes=settings.AgentTimeoutMinutes,CommandTimeoutSeconds=settings.AgentCommandTimeoutSeconds};workspace.ConfigureCommands(settings.AgentCommandsEnabled,settings.AgentCommandRecipes);return workspace;}
            catch(Exception e){if(!(e is ArgumentException || e is UnauthorizedAccessException || e is System.IO.IOException || e is PlatformNotSupportedException))throw;throw new InvalidOperationException("Agent 工作目录不可用，请在工作设置中更改默认目录或权限，或切回对话模式。");}
        }
        void RecordAgentTask(Operation op,AgentTaskState task)
        {
            lock(gate){if(disposed || active!=op)return;agentTasks[op.PersonaId]=task.Copy();}
            if(taskStore!=null)taskStore.SaveAsync(task);var changed=AgentTaskChanged;if(changed!=null)changed();
        }
        public void LoadAgentTask(PetSettings settings)
        {
            if(taskStore==null || IsBusy)return;string id=PersonaId;string path;
            try{path=System.IO.Path.GetFullPath(settings.AgentWorkspace??"");}catch(ArgumentException){return;}
            var loaded=taskStore.Load(id,path);lock(gate){if(active!=null || personaId!=id)return;if(loaded!=null)agentTasks[id]=loaded;else agentTasks.Remove(id);}
            var changed=AgentTaskChanged;if(changed!=null)changed();
        }
        public Task ResumeAgentTaskAsync(PetSettings settings,string name)
        {
            if(!settings.AgentEnabled)throw new InvalidOperationException("请先切换到工作模式再继续任务。");
            var state=CurrentAgentTask;if(state==null)throw new InvalidOperationException("当前没有可继续的任务。");
            if(!String.Equals(System.IO.Path.GetFullPath(settings.AgentWorkspace).TrimEnd('\\','/'),System.IO.Path.GetFullPath(state.Workspace).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("当前项目与任务检查点不一致，请先切回原项目。");
            string text="继续任务："+state.Goal;if(text.Length>8000)text=text.Substring(0,8000);
            return SendTextAsync(text,settings,name,resumeTask:state);
        }
        public void ReportNotice(string message){LastNotice=message??"";if(!disposed && StateChanged!=null)StateChanged();}
        public async Task SpeakReplyAsync(string id,PetSettings settings)
        {
            ChatMessage reply;lock(gate)reply=History.Messages.FirstOrDefault(m=>m.Id==id && m.Role=="assistant");
            if(reply==null || String.IsNullOrWhiteSpace(reply.Content))throw new InvalidOperationException("这条回复没有可朗读的正文。");
            Operation op=Begin(settings,ConversationStage.Speaking);
            try{await speech.SpeakAsync(MarkdownContent.PlainText(reply.Content),op.Snapshot,op.Cancel.Token);}finally{End(op);}
        }
        public async Task RegenerateAsync(string id,PetSettings settings,string petName)
        {
            ApiClient.ValidateConnection(settings.Text,ApiClient.ChatPath(settings.Text));
            lock(gate)History.ForRegeneration(id,"");
            Operation op=Begin(settings,ConversationStage.Thinking);op.Regenerating=true;op.ReplyId=id;
            try{await RequestReply(op,"",petName);}catch(OperationCanceledException){LastNotice="已停止重新生成。";throw;}finally{End(op);}
        }
        public async Task PreviewSpeechAsync(PetSettings settings,string name)
        {
            Operation op=Begin(settings,ConversationStage.Speaking);
            try { await speech.SpeakAsync("你好，我是"+name+"。很高兴和你一起工作。",op.Snapshot,op.Cancel.Token); LastNotice="朗读测试完成。"; }
            finally { End(op); }
        }
        public void SetVolume(int volume) { speech.SetVolume(volume); }
        public void StopSpeech() { speech.Stop(); }
        public async Task StopAsync()
        {
            Operation op; lock(gate) op=active; if(op==null) { speech.Stop();await FlushHistoryAsync();return; }
            try { op.Cancel.Cancel(); } catch(ObjectDisposedException) { return; }
            speech.Stop();
            if(op.Recorder!=null && !op.Finishing)
            { await Task.Run(delegate { op.Recorder.Dispose(); }); LastNotice="录音已取消。"; End(op); }
            await op.Complete.Task;
            await FlushHistoryAsync();
        }
        private void End(Operation op)
        {
            if(Interlocked.Exchange(ref op.Ended,1)!=0) return;
            if(op.Recorder!=null) {
                var failures=op.Recorder as IPcmMicrophone;
                if(failures!=null && op.RecordingFailure!=null)failures.Failed-=op.RecordingFailure;
                op.Recorder.Dispose();
            }
            lock(gate) if(Object.ReferenceEquals(active,op)) active=null;
            op.Cancel.Dispose(); SetStage(ConversationStage.Idle); op.Complete.TrySetResult(true);
        }
        public void Clear() { lock(gate){if(active!=null)throw new InvalidOperationException("请先停止当前对话再清空。");History.Clear();LastNotice="";if(store!=null)store.ClearAsync(personaId);}if(HistoryChanged!=null)HistoryChanged(); }
        public Task FlushHistoryAsync()
        {
            lock(gate){if(active!=null && active.ReplyId!=null)Save(active.PersonaId,active.History);return Task.WhenAll(store==null?Task.FromResult(0):store.FlushAsync(),taskStore==null?Task.FromResult(0):taskStore.FlushAsync());}
        }
        public string ExportHistory()
        {
            IList<ChatMessage> messages;string id;lock(gate){messages=History.Messages;id=personaId;}
            var result=new StringBuilder();result.Append("# 桌面伙伴聊天记录 · ").Append(id).Append("\n\n");
            foreach(var message in messages)result.Append("## ").Append(message.Role=="user"?"你":"伙伴").Append(" · ").Append(message.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")).Append(" · ").Append(message.Status).Append(" · ").Append(message.Source).Append("\n\n").Append(message.Content).Append("\n\n");
            return result.ToString();
        }
        public void Dispose()
        {
            Operation op;lock(gate){if(disposed)return;disposed=true;op=active;if(op!=null && op.ReplyId!=null){var current=op.History.Messages.FirstOrDefault(m=>m.Id==op.ReplyId);if(current!=null && current.Status=="pending")op.History.Update(op.ReplyId,current.Content,"interrupted",processingMilliseconds:op.ProcessingTime.ElapsedMilliseconds);Save(op.PersonaId,op.History);}}
            if(store!=null)store.WarningChanged-=OnWarningChanged;
            if(op!=null)
            { try {op.Cancel.Cancel();}catch(ObjectDisposedException){} if(op.Recorder!=null) op.Recorder.Dispose(); if(!op.Finishing && Stage==ConversationStage.Recording) End(op); }
            speech.Dispose(); api.Dispose();
        }
    }
}
