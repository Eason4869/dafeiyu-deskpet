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
        private readonly Func<bool> saveEnabled;
        private readonly object gate=new object();
        private Operation active;
        private bool disposed;
        private readonly Dictionary<string,ConversationLog> histories=new Dictionary<string,ConversationLog>();
        private string personaId="";
        public ConversationLog History { get; private set; }
        public ConversationStage Stage { get; private set; }
        public bool IsBusy { get { lock(gate) return active!=null; } }
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
            public ConversationLog History;public string PersonaId,ReplyId,Source="text";
            public readonly StringBuilder Partial=new StringBuilder();public readonly Stopwatch Checkpoint=Stopwatch.StartNew();
        }
        public ConversationSession(IConversationApi transport,IReplySpeech output,Func<IAudioRecorder> recorderFactory,HistoryStore historyStore=null,Func<bool> persistenceEnabled=null)
        { api=transport;speech=output;microphone=recorderFactory;store=historyStore;saveEnabled=persistenceEnabled??delegate{return true;};History=new ConversationLog();histories[""]=History;LastNotice="";if(store!=null)store.WarningChanged+=OnWarningChanged; }
        private void OnWarningChanged() { if(!disposed && StateChanged!=null)StateChanged(); }
        private ConversationLog GetHistory(string id)
        { ConversationLog history;if(!histories.TryGetValue(id,out history)){history=new ConversationLog();if(store!=null)history.Replace(store.Load(id));histories[id]=history;}return history; }
        private void Save(string id,ConversationLog history)
        { if(store!=null && saveEnabled())store.SaveAsync(id,history.Messages); }
        public void SelectPersona(string id)
        {
            lock(gate)
            {
                if(active!=null) throw new InvalidOperationException("请先停止对话再切换伙伴。");
                if(disposed)throw new ObjectDisposedException("ConversationSession");id=id??"";History=GetHistory(id);personaId=id;
            }
            if(HistoryChanged!=null)HistoryChanged();
        }
        public void AddLiveTurn(string id,string user,string reply,string status="complete")
        {
            lock(gate) {
                if(disposed || (String.IsNullOrWhiteSpace(reply) && (status=="complete" || String.IsNullOrWhiteSpace(user))))return;
                if(status!="complete" && status!="interrupted" && status!="cancelled" && status!="error")status="interrupted";
                id=id??"";var history=GetHistory(id);ChatMessage input;var output=history.BeginTurn(String.IsNullOrWhiteSpace(user)?"（语音对话）":user,"live",out input);output=history.Update(output.Id,reply,status);Save(id,history);
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
        public async Task SendTextAsync(string text,PetSettings settings,string petName)
        {
            text=(text??"").Trim(); if(text.Length==0 || text.Length>8000) throw new ApiException("请输入 1～8000 个字符。");
            ApiClient.ValidateConnection(settings.Text,ApiClient.ChatPath(settings.Text));
            Operation op=Begin(settings,ConversationStage.Thinking);
            try { await RequestReply(op,text,petName); }
            catch(OperationCanceledException) { LastNotice="已停止本次对话。"; throw; }
            finally { End(op); }
        }
        public void StartRecording(PetSettings settings)
        {
            ValidateVoice(settings); Operation op=Begin(settings,ConversationStage.Recording);
            try { op.Recorder=microphone(); op.Recorder.Start(); }
            catch { End(op); throw; }
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
            var request=op.History.ForRequest(persona,text);
            lock(gate) {
                token.ThrowIfCancellationRequested();if(disposed)throw new OperationCanceledException(token);
                ChatMessage user;var reply=op.History.BeginTurn(text,op.Source,out user);op.ReplyId=reply.Id;Emit(user);Emit(reply);Save(op.PersonaId,op.History);op.Checkpoint.Restart();
            }
            SetStage(ConversationStage.Thinking);string answer;
            try {
                var streaming=api as IStreamingConversationApi;
                answer=streaming!=null && op.Snapshot.Text.StreamReplies ? await streaming.ChatStreamAsync(op.Snapshot.Text,request,delta=>AppendDelta(op,delta),token) : await api.ChatAsync(op.Snapshot.Text,request,token);
                token.ThrowIfCancellationRequested();UpdateReply(op,answer,"complete",true);
            }
            catch(Exception e) {
                UpdateReply(op,op.Partial.ToString(),token.IsCancellationRequested || e is OperationCanceledException?"cancelled":"error",true);
                if(e is ApiException)throw new ApiException("文字对话："+e.Message);throw;
            }
            if(op.Live.ReadReplies && op.Live.Volume>0 && !op.Live.QuietMode)
            {
                SetStage(ConversationStage.Speaking);
                try { await speech.SpeakAsync(answer,op.Live.Copy(),token); }
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
                op.Partial.Append(delta);UpdateReply(op,op.Partial.ToString(),"pending",op.Checkpoint.ElapsedMilliseconds>=1000);
            }
        }
        private void UpdateReply(Operation op,string content,string status,bool persist)
        {
            lock(gate) {
                if(disposed || active!=op || op.ReplyId==null)return;
                if(status=="complete")op.Cancel.Token.ThrowIfCancellationRequested();
                var updated=op.History.Update(op.ReplyId,content,status);
                if(updated!=null && op.PersonaId==personaId && MessageUpdated!=null)MessageUpdated(updated);
                if(persist){Save(op.PersonaId,op.History);op.Checkpoint.Restart();}
            }
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
            if(op.Recorder!=null) op.Recorder.Dispose();
            lock(gate) if(Object.ReferenceEquals(active,op)) active=null;
            op.Cancel.Dispose(); SetStage(ConversationStage.Idle); op.Complete.TrySetResult(true);
        }
        public void Clear() { lock(gate){if(active!=null)throw new InvalidOperationException("请先停止当前对话再清空。");History.Clear();LastNotice="";if(store!=null)store.ClearAsync(personaId);}if(HistoryChanged!=null)HistoryChanged(); }
        public Task FlushHistoryAsync()
        {
            lock(gate){if(active!=null && active.ReplyId!=null)Save(active.PersonaId,active.History);return store==null?Task.FromResult(0):store.FlushAsync();}
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
            Operation op;lock(gate){if(disposed)return;disposed=true;op=active;if(op!=null && op.ReplyId!=null){var current=op.History.Messages.FirstOrDefault(m=>m.Id==op.ReplyId);if(current!=null && current.Status=="pending")op.History.Update(op.ReplyId,current.Content,"interrupted");Save(op.PersonaId,op.History);}}
            if(store!=null)store.WarningChanged-=OnWarningChanged;
            if(op!=null)
            { try {op.Cancel.Cancel();}catch(ObjectDisposedException){} if(op.Recorder!=null) op.Recorder.Dispose(); if(!op.Finishing && Stage==ConversationStage.Recording) End(op); }
            speech.Dispose(); api.Dispose();
        }
    }
}
