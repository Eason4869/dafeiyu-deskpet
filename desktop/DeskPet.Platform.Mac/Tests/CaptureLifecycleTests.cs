using Dafeiyu;
using DeskPet.Platform.Mac;

internal static class CaptureLifecycleTests
{
    internal static void Run()
    {
        var denied = new Capture { Access = MicrophonePermission.Denied };
        using (var recorder = new MacAudioRecorder(false, denied))
        {
            try { recorder.Start(); throw new Exception("Denied capture started"); }
            catch (InvalidOperationException error) when (error.Message.Contains("权限")) { }
            Assert(denied.Starts == 0, "denial never opens audio device");
        }
        var input = new Capture { Bytes = new byte[16000] };
        using (var recorder = new MacAudioRecorder(false, input))
        {
            var count = 0;
            recorder.PcmAvailable += bytes => Interlocked.Add(ref count, bytes.Length);
            recorder.Start();
            Assert(SpinWait.SpinUntil(() => Volatile.Read(ref count) == 16000, 3000), "capture drained");
            var wav = recorder.FinishAsync().GetAwaiter().GetResult();
            Assert(WaveData.IsWave(wav) && wav.Length == 16044, "recorded bytes become WAV");
            Assert(input.Stops == 1 && !recorder.IsRecording, "finish releases once");
            try { recorder.Start(); throw new Exception("Used recorder restarted"); }
            catch (InvalidOperationException) { Assert(true, "used recorder cannot restart"); }
        }
        Assert(input.Stops == 1, "dispose does not close twice");
        var broken = new Capture { Fail = true };
        using (var recorder = new MacAudioRecorder(true, broken))
        {
            using var notified = new ManualResetEventSlim();
            recorder.Failed += _ => notified.Set(); recorder.Start();
            Assert(notified.Wait(3000), "device failure is observable");
            Assert(SpinWait.SpinUntil(() => broken.Stops == 1, 3000), "device failure releases without UI disposal");
        }
        var reentrant = new Capture { Bytes = new byte[512] };
        var fromCallback = new MacAudioRecorder(false, reentrant);
        var failures = 0;
        fromCallback.Failed += _ => Interlocked.Increment(ref failures);
        fromCallback.PcmAvailable += _ => fromCallback.Dispose();
        fromCallback.Start();
        Assert(SpinWait.SpinUntil(() => reentrant.Stops == 1, 3000), "dispose from capture callback closes device");
        Thread.Sleep(100);
        Assert(failures == 0, "callback disposal is not reported as device failure");
        Console.WriteLine("10 capture lifecycle checks passed");
    }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private sealed class Capture : IMacMicrophone
    {
        public MicrophonePermission Access { get; set; } = MicrophonePermission.Authorized;
        public int Starts, Stops;
        public byte[] Bytes = [];
        public bool Fail;
        private int offset;
        public IntPtr Start(out int error) { Interlocked.Increment(ref Starts); error = 0; return new IntPtr(1); }
        public int Read(IntPtr device, byte[] buffer)
        {
            if (Fail) return -1;
            var count = Math.Min(buffer.Length, Bytes.Length - offset);
            Bytes.AsSpan(offset, count).CopyTo(buffer); offset += count; return count;
        }
        public void Stop(IntPtr device) => Interlocked.Increment(ref Stops);
    }
}
