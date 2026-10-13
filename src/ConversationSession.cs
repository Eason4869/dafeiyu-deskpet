using System;
using System.Collections.Generic;
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
        private readonly object gate=new object();
        private Operation active;
        private bool disposed;
        private readonly Dictionary<string,ConversationLog> histories=new Dictionary<string,ConversationLog>();
        private string personaId="";
        public ConversationLog History { get; private set; }
        public ConversationStage Stage { get; private set; }
        public bool IsBusy { get { lock(gate) return active!=null; } }
        public string LastNotice { get; private set; }
        public event Action StateChanged;
        public event Action<ChatMessage> MessageAdded;
        private sealed class Operation
        {
            public readonly CancellationTokenSource Cancel=new CancellationTokenSource();
            public readonly TaskCompletionSource<bool> Complete=new TaskCompletionSource<bool>();
            public PetSettings Snapshot,Live; public IAudioRecorder Recorder; public bool Finishing; public int Ended;
        }
        public ConversationSession(IConversationApi transport,IReplySpeech output,Func<IAudioRecorder> recorderFactory)
        { api=transport; speech=output; microphone=recorderFactory; History=new ConversationLog(); LastNotice=""; }
        public void SelectPersona(string id)
        {
            lock(gate)
            {
                if(active!=null) throw new InvalidOperationException("请先停止对话再切换伙伴。");
                ConversationLog history; if(!histories.TryGetValue(id,out history)) histories[id]=history=new ConversationLog(); History=history;personaId=id;
            }
        }
        public void AddLiveTurn(string id,string user,string reply)
        {
            if(disposed)return;ConversationLog history;if(!histories.TryGetValue(id,out history))histories[id]=history=new ConversationLog();
            if(String.IsNullOrWhiteSpace(reply))return;history.Commit(String.IsNullOrWhiteSpace(user)?"（语音对话）":user,reply);
            if(id==personaId){if(!String.IsNullOrWhiteSpace(user))Emit("user",user);Emit("assistant",reply);}
        }
        private Operation Begin(PetSettings settings,ConversationStage stage)
        {
            Operation op;
            lock(gate)
            {
                if(disposed) throw new ObjectDisposedException("ConversationSession");
                if(active!=null) throw new InvalidOperationException("正在处理当前对话，请先停止或等待完成。");
                op=new Operation { Snapshot=settings.Copy(),Live=settings }; active=op; LastNotice="";
            }
            SetStage(stage); return op;
        }
        private void SetStage(ConversationStage stage)
        { Stage=stage; if(!disposed && StateChanged!=null) StateChanged(); }
        private void Emit(string role,string text)
        { if(!disposed && MessageAdded!=null) MessageAdded(new ChatMessage(role,text)); }
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
            op.Cancel.Token.ThrowIfCancellationRequested(); await RequestReply(op,text,petName);
        }
        private async Task RequestReply(Operation op,string text,string petName)
        {
            CancellationToken token=op.Cancel.Token; token.ThrowIfCancellationRequested();
            Emit("user",text); SetStage(ConversationStage.Thinking);
            string persona=(op.Snapshot.SystemPrompt??"").Replace("{pet_name}",petName??"桌面伙伴");
            string answer;try{answer=await api.ChatAsync(op.Snapshot.Text,History.ForRequest(persona,text),token);}catch(ApiException e){throw new ApiException("文字对话："+e.Message);}
            token.ThrowIfCancellationRequested(); History.Commit(text,answer); Emit("assistant",answer);
            if(op.Live.ReadReplies && op.Live.Volume>0)
            {
                SetStage(ConversationStage.Speaking);
                try { await speech.SpeakAsync(answer,op.Live.Copy(),token); }
                catch(OperationCanceledException) { if(token.IsCancellationRequested) throw; LastNotice="回复已保留，朗读已停止。"; }
                catch(Exception e)
                { if(!(e is ApiException || e is InvalidOperationException || e is System.IO.IOException || e is UnauthorizedAccessException)) throw; LastNotice="回复已保留；朗读失败："+(e is ApiException || e is InvalidOperationException ? e.Message : "请检查输出设备或音频缓存目录。"); }
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
            Operation op; lock(gate) op=active; if(op==null) { speech.Stop(); return; }
            try { op.Cancel.Cancel(); } catch(ObjectDisposedException) { return; }
            speech.Stop();
            if(op.Recorder!=null && !op.Finishing)
            { await Task.Run(delegate { op.Recorder.Dispose(); }); LastNotice="录音已取消。"; End(op); }
            await op.Complete.Task;
        }
        private void End(Operation op)
        {
            if(Interlocked.Exchange(ref op.Ended,1)!=0) return;
            if(op.Recorder!=null) op.Recorder.Dispose();
            lock(gate) if(Object.ReferenceEquals(active,op)) active=null;
            op.Cancel.Dispose(); SetStage(ConversationStage.Idle); op.Complete.TrySetResult(true);
        }
        public void Clear() { if(IsBusy) throw new InvalidOperationException("请先停止当前对话再清空。"); History.Clear(); LastNotice=""; }
        public void Dispose()
        {
            Operation op; lock(gate) { if(disposed) return; disposed=true; op=active; }
            if(op!=null)
            { try {op.Cancel.Cancel();}catch(ObjectDisposedException){} if(op.Recorder!=null) op.Recorder.Dispose(); if(!op.Finishing && Stage==ConversationStage.Recording) End(op); }
            speech.Dispose(); api.Dispose();
        }
    }
}
