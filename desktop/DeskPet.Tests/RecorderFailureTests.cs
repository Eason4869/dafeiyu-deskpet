using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dafeiyu.Tests
{
    public static class RecorderFailureTests
    {
        public static int Main()
        {
            try { Run().GetAwaiter().GetResult();Console.WriteLine("Recorder failure tests: 5 passed");return 0; }
            catch(Exception error){Console.WriteLine("FAIL recorder failure: "+error.Message);return 1;}
        }
        static async Task Run()
        {
            var settings=new PetSettings{Text=new ConnectionSettings{BaseUrl="http://127.0.0.1:1/v1",Model="fixture"},Transcription=new ConnectionSettings{BaseUrl="http://127.0.0.1:1/v1",Model="fixture"}};
            var recorder=new Recorder();
            using(var session=new ConversationSession(new UnusedApi(),new UnusedSpeech(),()=>recorder))
            {
                session.StartRecording(settings);recorder.Fail();
                await Until(()=>!session.IsBusy);
                Check(session.Stage==ConversationStage.Idle && recorder.Disposed,"conversation recording failure releases ownership and microphone");
                Check(session.LastNotice.Contains("device disconnected"),"conversation recording failure remains visible in status");
                recorder.Fail();Check(!session.IsBusy && session.History.Messages.Count==0,"late recording failure cannot create a turn");
            }
            recorder=new Recorder();object owner=new object();
            using(var dictation=new DictationSession(new UnusedApi(),()=>recorder,()=>settings))
            {
                dictation.Start(owner);recorder.Fail();
                await Until(()=>!dictation.IsBusy);
                Check(dictation.Stage==DictationStage.Idle && !dictation.Owns(owner) && recorder.Disposed,"dictation device failure releases ownership without calling ASR");
                // An event racing with Finish must not prevent a subsequent fresh recording.
                recorder=new Recorder();dictation.Start(owner);await dictation.CancelAsync(owner);
                Check(!dictation.IsBusy,"new dictation can start after a failed device");
            }
        }
        static async Task Until(Func<bool> ready){for(int i=0;i<150;i++){if(ready())return;await Task.Delay(10);}throw new Exception("device failure left recording busy");}
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
        sealed class Recorder:IAudioRecorder,IPcmMicrophone
        {
            public bool Disposed;public bool IsRecording{get;private set;}
            public event Action<byte[]> PcmAvailable{add{}remove{}}
            public event Action<string> Failed;
            public void Start(){IsRecording=true;}
            public void Fail(){IsRecording=false;if(Failed!=null)Failed("device disconnected");}
            public Task<byte[]> FinishAsync(){throw new Exception("failed device must not be sent to ASR");}
            public void Dispose(){Disposed=true;IsRecording=false;}
        }
        sealed class UnusedApi:IConversationApi
        {
            public Task<string> ChatAsync(ConnectionSettings c,IList<ChatMessage> messages,CancellationToken t){throw new Exception("unexpected chat request");}
            public Task<string> TranscribeAsync(ConnectionSettings c,byte[] bytes,CancellationToken t){throw new Exception("unexpected ASR request");}
            public Task<byte[]> SpeechAsync(ConnectionSettings c,string voice,string text,CancellationToken t){throw new Exception("unexpected speech request");}
            public void Dispose(){}
        }
        sealed class UnusedSpeech:IReplySpeech
        {
            public Task SpeakAsync(string reply,PetSettings settings,CancellationToken token){throw new Exception("unexpected speech");}
            public void SetVolume(int volume){}public void Stop(){}public void Dispose(){}
        }
    }
}
