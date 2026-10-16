using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DeskPet.Platform.Mac;

namespace DeskPet.Desktop;

internal sealed class LiveWindow : Window
{
    private readonly DesktopController owner;
    private readonly TextBlock status = Ui.Text("");
    private readonly TextBlock user = Ui.Text(""), reply = Ui.Text("");
    private readonly ProgressBar input = new() { Minimum = 0, Maximum = 1 }, output = new() { Minimum = 0, Maximum = 1 };
    private readonly DispatcherTimer meter;
    private bool closing;
    public LiveWindow(DesktopController controller)
    {
        owner = controller; Title = "DeskPet · Gemini Live"; Width = 560; Height = 490;
        Content = new Border { Padding = new Thickness(20), Child = Ui.Column(Ui.Text("Gemini Live 实时语音", 22), status,
            Ui.Text("麦克风"), input, Ui.Text("伙伴音量"), output,
            new ScrollViewer { Content = Ui.Column(Ui.Text("你", 12), user, Ui.Text("伙伴", 12), reply), MaxHeight = 200 },
            Ui.Row(Ui.AsyncButton("开始对话", StartAsync, owner.Error), Ui.AsyncButton("结束对话", owner.Live.StopAsync, owner.Error), Ui.Button("Live 设置", () => owner.ShowSettingsSection("live"))),
            Ui.Text("开始时申请麦克风权限。音频发送至你配置的 Gemini Live 服务；结束或关闭窗口时释放麦克风和播放设备。")) };
        owner.Live.StateChanged += Refresh; owner.Live.PartialTranscript += Transcript;
        Closing += async (_, e) =>
        {
            if (closing) return;
            e.Cancel = true; closing = true;
            try { await owner.Live.StopAsync(); Close(); } catch (Exception error) { closing = false; owner.Error(error); }
        };
        meter = new DispatcherTimer(TimeSpan.FromMilliseconds(80), DispatcherPriority.Background, (_, _) => { input.Value = owner.Live.InputLevel; output.Value = owner.Live.OutputLevel; });
        Closed += (_, _) => { meter.Stop(); owner.Live.StateChanged -= Refresh; owner.Live.PartialTranscript -= Transcript; };
        Refresh();
    }
    private async Task StartAsync()
    {
        var expectedPersona = owner.Skin.Id;
        if (owner.Live.IsActive) return;
        await owner.Queue.PauseAsync(); await owner.Dictation.CancelAsync(); await owner.Session.StopAsync();
        if (!IsVisible || closing || owner.Skin.Id != expectedPersona) return;
        var granted = await owner.RequestMicrophoneAccessAsync();
        if (!IsVisible || closing || owner.Skin.Id != expectedPersona) return;
        if (!granted) throw new InvalidOperationException("麦克风权限未授予。请在系统设置 → 隐私与安全性 → 麦克风中允许 DeskPet。");
        if (owner.Live.IsActive) return;
        if (owner.Dictation.IsBusy || owner.Session.IsBusy) throw new InvalidOperationException("另一处对话或听写已经开始，请先结束它。");
        await owner.Live.StartAsync(owner.Settings, owner.Settings.SystemPrompt);
    }
    private void Refresh() => Dispatcher.UIThread.Post(() => status.Text = owner.Live.Status);
    private void Transcript(string role, string text) => Dispatcher.UIThread.Post(() => { if (role == "user") user.Text = text; else reply.Text = text; });
}
