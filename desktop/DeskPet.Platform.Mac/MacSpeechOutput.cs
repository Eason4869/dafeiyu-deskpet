using System.Diagnostics;
using System.Text.RegularExpressions;
using Dafeiyu;

namespace DeskPet.Platform.Mac;

internal sealed class MacSpeechOutput(ApiClient api) : IReplySpeech
{
    private readonly object gate = new();
    private CancellationTokenSource? active;
    private Process? say;
    private IntPtr wave;
    private int volume = 50;
    private bool disposed;
    public void SetVolume(int value) { lock (gate) { volume = Math.Clamp(value, 0, 100); if (wave != IntPtr.Zero) Native.WaveVolume(wave, volume / 100f); } }
    public async Task SpeakAsync(string reply, PetSettings settings, CancellationToken cancel)
    {
        MacServices.EnsureMac();
        var text = Regex.Replace(reply ?? "", "```[\\s\\S]*?```", "（代码内容请看聊天窗口）");
        text = Regex.Replace(text, "[\\*#`]|https?://\\S+", "").Trim();
        if (text.Length > 1800) text = text[..1800] + "。剩下内容请看聊天窗口。";
        if (text.Length == 0 || settings.Volume == 0) return;
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); if (active != null) throw new InvalidOperationException("正在朗读，请先停止。"); active = reading; volume = Math.Clamp(settings.Volume, 0, 100); }
        reading.CancelAfter(TimeSpan.FromMinutes(4));
        try
        {
            if (settings.UseCloudVoice)
            {
                var bytes = await api.SpeechAsync(settings.SpeechConnection(), settings.VoiceName, text, reading.Token);
                reading.Token.ThrowIfCancellationRequested();
                lock (gate) { wave = Native.WaveStart(bytes, bytes.Length, volume / 100f); if (wave == IntPtr.Zero) throw new InvalidOperationException("无法播放模型语音，请检查返回音频和输出设备。"); }
                while (true)
                {
                    reading.Token.ThrowIfCancellationRequested();
                    int status; lock (gate) status = Native.WaveStatus(wave);
                    if (status < 0) throw new InvalidOperationException("模型语音播放中断，请检查输出设备。");
                    if (status == 0) break;
                    await Task.Delay(50, reading.Token);
                }
            }
            else await SpeakSystem(text, reading.Token);
        }
        finally
        {
            lock (gate)
            {
                if (wave != IntPtr.Zero) { Native.WaveStop(wave); wave = IntPtr.Zero; }
                say?.Dispose(); say = null;
                if (ReferenceEquals(active, reading)) active = null;
            }
        }
    }
    private async Task SpeakSystem(string text, CancellationToken token)
    {
        // No shell and no text in argv: send private reply text through stdin.
        var start = new ProcessStartInfo("/usr/bin/say") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add("--");
        using var process = new Process { StartInfo = start };
        lock (gate) { token.ThrowIfCancellationRequested(); if (!process.Start()) throw new InvalidOperationException("系统朗读无法启动。"); say = process; }
        using var kill = token.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } });
        // say's embedded speech control sets the utterance volume without changing system volume.
        int currentVolume; lock (gate) currentVolume = volume;
        await process.StandardInput.WriteAsync(("[[volm " + (currentVolume / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture) + "]]" + text.Replace("[[", "［［")).AsMemory(), token);
        process.StandardInput.Close();
        var errors = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token); await errors;
        if (process.ExitCode != 0) throw new InvalidOperationException($"系统朗读失败（状态 {process.ExitCode}），请检查系统语音及音频输出。");
    }
    public void Stop() { lock (gate) { try { active?.Cancel(); } catch (ObjectDisposedException) { } } }
    public void Dispose() { lock (gate) { if (disposed) return; disposed = true; Stop(); } }
}
