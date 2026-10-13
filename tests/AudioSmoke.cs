using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Dafeiyu.Tests
{
    public static class AudioSmoke
    {
        private static int passed,skipped;
        [STAThread] public static int Main()
        {
            try
            {
                new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
                CheckRingOrder();
                using(var recorder=new AudioRecorder())
                {
                    try{recorder.Start();}
                    catch(InvalidOperationException e){Console.WriteLine("SKIP default microphone unavailable: "+e.Message);skipped++;}
                    if(recorder.IsRecording)
                    {Pump(1);var task=recorder.FinishAsync();Wait(task,5);byte[] wave=task.GetAwaiter().GetResult();Check(WaveData.IsWave(wave) && wave.Length>=12844 && !recorder.IsRecording,"real microphone captures valid PCM and releases device; samples stay in memory");}
                }
                if(skipped==0)
                {
                    int chunks=0;using(var recorder=new AudioRecorder(true)){recorder.PcmAvailable+=delegate(byte[] bytes){if(bytes.Length>0 && bytes.Length%2==0)Interlocked.Increment(ref chunks);};recorder.Start();Pump(.7);recorder.Dispose();Check(chunks>=3 && !recorder.IsRecording,"real Live capture emits ordered 16k PCM chunks and releases microphone");}
                    using(var playback=new PcmOutput()){playback.SetVolume(0);playback.Enqueue(new byte[9600],24000);Pump(.08);playback.Interrupt();playback.Enqueue(new byte[4800],24000);Pump(.15);Check(true,"real Live PCM output opens, plays silence, interrupts and reopens playback");}
                    using(var recorder=new AudioRecorder()){recorder.Start();Pump(.6);var finish=recorder.FinishAsync();var dispose=Task.Run(delegate{recorder.Dispose();});Wait(Task.WhenAll(finish.ContinueWith(delegate{}),dispose),5);try{finish.GetAwaiter().GetResult();}catch(InvalidOperationException){}Check(!recorder.IsRecording,"concurrent finish and dispose release the native input device");}
                    using(var recorder=new AudioRecorder()){recorder.Start();recorder.Dispose();Check(!recorder.IsRecording,"microphone reopens after cancellation and closes immediately");}
                }
                string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"audio-smoke",Guid.NewGuid().ToString("N"));
                using(var api=new ApiClient())
                using(var output=new SpeechOutput(api,folder,Dispatcher.CurrentDispatcher))
                {
                    bool installed;using(var native=new SpeechSynthesizer())installed=native.GetInstalledVoices().Any(v=>v.Enabled);
                    if(installed)
                    {
                        var settings=new PetSettings {UseCloudVoice=false,Volume=50};var read=output.SpeakAsync("你好。",settings,CancellationToken.None);Wait(read,20);read.GetAwaiter().GetResult();Check(true,"Windows system speech completes on the default output device");
                        string longText=String.Concat(Enumerable.Repeat("这是一段用来验证停止朗读的声音。",18));var stopped=output.SpeakAsync(longText,settings,CancellationToken.None);Pump(.15);output.Stop();Wait(stopped,3);try{stopped.GetAwaiter().GetResult();throw new Exception("speech was not cancelled");}catch(OperationCanceledException){}Check(true,"stop interrupts real system speech without waiting for the utterance");
                        var again=output.SpeakAsync("好了。",settings,CancellationToken.None);Wait(again,20);again.GetAwaiter().GetResult();Check(true,"system speech reopens after cancellation without stale callbacks");
                    }
                    else {Console.WriteLine("SKIP no enabled Windows speech voice");skipped++;}
                }
                using(var api=new ApiClient())using(var output=new SpeechOutput(api,folder,Dispatcher.CurrentDispatcher))
                using(var synthesis=new ApiTests.Fixture(WaveData.EncodePcm(new byte[160000]),"audio/wav"))
                {
                    synthesis.DelayBodyMilliseconds=500;var settings=new PetSettings {UseCloudVoice=true,Volume=50,VoiceName="test",Speech=new ConnectionSettings {BaseUrl=synthesis.Url,Model="test"}};
                    var play=output.SpeakAsync("测试。",settings,CancellationToken.None);Wait(synthesis.HeadersSent,3);output.SetVolume(0);
                    var field=typeof(SpeechOutput).GetField("activePlayer",BindingFlags.Instance|BindingFlags.NonPublic);DateTime until=DateTime.UtcNow.AddSeconds(3);while(field.GetValue(output)==null && DateTime.UtcNow<until)Pump(.03);
                    var player=(System.Windows.Media.MediaPlayer)field.GetValue(output);Check(player!=null && player.Volume==0,"volume changed during pending synthesis applies when playback begins");output.Stop();Wait(play,3);try{play.GetAwaiter().GetResult();}catch(OperationCanceledException){}
                }
                using(var api=new ApiClient())using(var output=new SpeechOutput(api,folder,Dispatcher.CurrentDispatcher))
                using(var synthesis=new ApiTests.Fixture(WaveData.EncodePcm(new byte[160000]),"audio/wav"))
                {
                    var settings=new PetSettings {UseCloudVoice=true,Volume=50,VoiceName="test",Speech=new ConnectionSettings {BaseUrl=synthesis.Url,Model="test"}};
                    var play=output.SpeakAsync("测试。",settings,CancellationToken.None);Pump(.15);output.Dispose();Wait(play,3);try{play.GetAwaiter().GetResult();throw new Exception("cloud playback was not cancelled");}catch(OperationCanceledException){}
                    Check(!Directory.Exists(folder) || !Directory.GetFiles(folder,"*.wav").Any(),"closing audio output cancels cloud playback and removes temporary WAV");
                }
                Console.WriteLine("Audio smoke: "+passed+" passed, "+skipped+" skipped");return 0;
            }
            catch(Exception e){Console.WriteLine("FAIL Audio smoke: "+e);return 1;}
        }
        private static void Check(bool value,string message){if(!value)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
        private static void CheckRingOrder()
        {
            using(var recorder=new AudioRecorder())
            {
                Type kind=typeof(AudioRecorder);var flags=BindingFlags.Instance|BindingFlags.NonPublic;Type bufferType=kind.GetNestedType("Buffer",BindingFlags.NonPublic),headerType=kind.GetNestedType("WaveHeader",BindingFlags.NonPublic);
                var buffers=(IList)kind.GetField("buffers",flags).GetValue(recorder);
                for(int i=0;i<4;i++)
                {
                    object buffer=Activator.CreateInstance(bufferType,true);buffers.Add(buffer);IntPtr data=(IntPtr)bufferType.GetField("Data").GetValue(buffer),header=(IntPtr)bufferType.GetField("Header").GetValue(buffer);
                    Marshal.WriteInt16(data,(short)(i+1));Marshal.WriteInt32(header,(int)Marshal.OffsetOf(headerType,"BytesRecorded"),2);Marshal.WriteInt32(header,(int)Marshal.OffsetOf(headerType,"Flags"),1);
                }
                var next=kind.GetField("nextBufferIndex",flags);if(next!=null)next.SetValue(recorder,2);
                kind.GetMethod("DrainReady",flags).Invoke(recorder,new object[]{false});byte[] dataBytes=((MemoryStream)kind.GetField("pcm",flags).GetValue(recorder)).ToArray();
                Check(dataBytes.SequenceEqual(new byte[]{3,0,4,0,1,0,2,0}),"native completed buffers retain submitted order across the ring boundary");
                kind.GetMethod("DrainReady",flags).Invoke(recorder,new object[]{false});Check(((MemoryStream)kind.GetField("pcm",flags).GetValue(recorder)).Length==8,"already collected input buffers are never appended twice");
            }
        }
        private static void Wait(Task task,double timeout){DateTime until=DateTime.UtcNow.AddSeconds(timeout);while(!task.IsCompleted && DateTime.UtcNow<until)Pump(.03);if(!task.IsCompleted)throw new Exception("audio operation timed out");}
        private static void Pump(double seconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(seconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    }
}
