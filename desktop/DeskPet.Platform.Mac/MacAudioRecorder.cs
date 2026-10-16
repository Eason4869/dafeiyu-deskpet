using System.Diagnostics;
using Dafeiyu;

namespace DeskPet.Platform.Mac;

internal sealed class MacAudioRecorder(bool streaming) : IAudioRecorder, IPcmMicrophone
{
    private readonly object lifecycle = new();
    private readonly MemoryStream pcm = new();
    private IntPtr device;
    private Thread? worker;
    private volatile bool stopping;
    private bool finished, disposed;
    private Exception? failure;
    public event Action<byte[]>? PcmAvailable;
    public event Action<string>? Failed;
    public bool IsRecording => device != IntPtr.Zero && !stopping;

    public void Start()
    {
        MacServices.EnsureMac();
        lock (lifecycle)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (finished || device != IntPtr.Zero) throw new InvalidOperationException("录音器已经使用。");
            if (MacServices.MicrophoneAccess != MicrophonePermission.Authorized)
                throw new InvalidOperationException("没有麦克风权限，请在系统设置 > 隐私与安全性 > 麦克风中允许 DeskPet；文字对话仍可使用。");
            device = Native.MicrophoneStart(out var error);
            if (device == IntPtr.Zero) throw new InvalidOperationException($"无法启动麦克风（状态 {error}），请检查默认输入设备。");
            worker = new Thread(Drain) { IsBackground = true, Name = "DeskPet Mac microphone" };
            worker.Start();
        }
    }
    private void Drain()
    {
        var buffer = new byte[8192];
        var progress = Stopwatch.StartNew();
        try
        {
            while (!stopping)
            {
                var count = Native.MicrophoneRead(device, buffer, buffer.Length);
                if (count < 0) throw new InvalidOperationException("麦克风设备已断开或录音已中断。");
                if (count > 0)
                {
                    progress.Restart();
                    if (!streaming) count = Math.Min(count, 60 * 32000 - (int)pcm.Length);
                    if (count > 0)
                    {
                        var bytes = buffer.AsSpan(0, count).ToArray();
                        if (!streaming) pcm.Write(bytes);
                        PcmAvailable?.Invoke(bytes);
                    }
                    if (!streaming && pcm.Length >= 60 * 32000) break;
                }
                else if (progress.Elapsed > TimeSpan.FromSeconds(5)) throw new InvalidOperationException("麦克风连续 5 秒没有输入数据，请检查权限及输入设备。");
                Thread.Sleep(20);
            }
        }
        catch (Exception ex)
        {
            failure = ex;
            var handler = Failed;
            if (handler != null) ThreadPool.QueueUserWorkItem(_ => handler("麦克风录音中断，请检查输入设备与权限。"));
        }
        finally
        {
            stopping = true;
            // The capture worker owns the handle until joined, including failure paths.
            // Device disappearance must release AVAudioEngine even before the UI reacts.
            if (device != IntPtr.Zero) { Native.MicrophoneStop(device); device = IntPtr.Zero; }
        }
    }
    public Task<byte[]> FinishAsync() => Task.Run(() =>
    {
        lock (lifecycle)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Release();
            if (failure != null) throw new InvalidOperationException("录音中断，请检查麦克风后重试。", failure);
            var bytes = pcm.ToArray();
            if (bytes.Length < 12800) throw new InvalidOperationException("录音太短，请至少说半秒后再发送。");
            return WaveData.EncodePcm(bytes);
        }
    });
    private void Release()
    {
        if (finished) return;
        stopping = true;
        if (worker != Thread.CurrentThread) worker?.Join();
        if (device != IntPtr.Zero) { Native.MicrophoneStop(device); device = IntPtr.Zero; }
        finished = true;
    }
    public void Dispose() { lock (lifecycle) { if (disposed) return; Release(); pcm.Dispose(); disposed = true; } }
}
