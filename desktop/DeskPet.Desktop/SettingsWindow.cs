using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Dafeiyu;
using DeskPet.Platform.Mac;

namespace DeskPet.Desktop;

internal sealed class SettingsWindow : Window
{
    private readonly DesktopController owner;
    private readonly List<ProviderEditor> providers = new();
    private readonly TabControl tabs = new();
    public SettingsWindow(DesktopController controller)
    {
        owner = controller; Title = "DeskPet · 设置"; Width = 790; Height = 760; MinWidth = 570; MinHeight = 420;
        var pages = new List<TabItem>();
        void Add(string title, Control content) => pages.Add(new TabItem { Header = title, Content = new ScrollViewer { Content = new Border { Padding = new Thickness(20), Child = content } } });
        Add("伙伴", Partner()); Add("桌面", Behavior());
        foreach (var item in new[] { ("文字服务", "text"), ("听写", "asr"), ("云朗读", "speech"), ("Live", "live") }) { var editor = new ProviderEditor(owner, item.Item2); providers.Add(editor); Add(item.Item1, editor); }
        Add("语音", Voice()); Add("数据", Storage()); tabs.ItemsSource = pages; Content = tabs;
        Closed += (_, _) => { foreach (var provider in providers) provider.Dispose(); };
    }
    public void SelectSection(string section) => tabs.SelectedIndex = section switch { "partner" => 0, "behavior" => 1, "text" => 2, "asr" => 3, "speech" => 4, "live" => 5, "voice" => 6, "storage" => 7, _ => 0 };
    private Control Partner()
    {
        var choices = new ComboBox { ItemsSource = owner.Skins, SelectedItem = owner.Skin };
        var prompt = Ui.Input(owner.Settings.SystemPrompt); prompt.AcceptsReturn = true; prompt.Height = 250; prompt.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        choices.SelectionChanged += (_, _) => { if (choices.SelectedItem is PetSkin skin) owner.Run(async () => { await owner.SelectSkinAsync(skin); prompt.Text = owner.Settings.SystemPrompt; }); };
        return Ui.Column(Ui.Text("当前伙伴", 20), choices, Ui.Field("人格（此伙伴独立保存，最多 8000 字）", prompt),
            Ui.Button("保存人格", () => { try { PersonaProfiles.Save(owner.Settings, owner.Skin.Id, owner.Skin.Prompt, prompt.Text ?? ""); owner.Save(); } catch (Exception e) { owner.Error(e); } }),
            Ui.Button("恢复默认人格", () => { PersonaProfiles.Reset(owner.Settings, owner.Skin.Id, owner.Skin.Prompt); prompt.Text = owner.Settings.SystemPrompt; owner.Save(); }),
            Ui.AsyncButton("导入自定义外观 pet.json（v1 / v2）", async () =>
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "选择外观 pet.json", AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("外观说明") { Patterns = new[] { "*.json" } } } });
                var file = files.FirstOrDefault(); if (file == null) return;
                var local = file.TryGetLocalPath() ?? throw new InvalidOperationException("请选择本地外观文件。");
                var skin = await Task.Run(() => SpriteAssets.Import(local, owner.DataDirectory)); owner.Skins.Add(skin); await owner.SelectSkinAsync(skin); choices.ItemsSource = null; choices.ItemsSource = owner.Skins; choices.SelectedItem = skin;
            }, owner.Error), Ui.Text("同名外观以独立 ID 保存；不会覆盖内置伙伴。外观文件需与资源一起放在同一目录。"));
    }
    private Control Behavior()
    {
        var s = owner.Settings;
        void Apply() { owner.Pet.ApplySettings(); owner.Save(); }
        var scale = new Slider { Minimum = .5, Maximum = 2.5, Value = s.Scale, TickFrequency = .1 };
        scale.ValueChanged += (_, _) => { s.Scale = scale.Value; Apply(); };
        var panel = Ui.Column(Ui.Text("桌面行为", 20), Ui.Field("伙伴大小", scale),
            Ui.Check("始终置顶", s.Topmost, v => { s.Topmost = v; Apply(); }),
            Ui.Check("锁定位置", s.LockPosition, v => { s.LockPosition = v; Apply(); }),
            Ui.Check("前台应用全屏时隐藏伙伴", s.HideOnFullscreen, v => { s.HideOnFullscreen = v; Apply(); }),
            Ui.Check("鼠标穿透（从菜单栏恢复）", s.ClickThrough, v => { s.ClickThrough = v; Apply(); }),
            Ui.Check("跟随鼠标", s.FollowCursor, v => { s.FollowCursor = v; Apply(); }),
            Ui.Check("偶尔散步（90～180 秒）", s.Wander, v => { s.Wander = v; Apply(); }),
            Ui.Check("落地时可能踉跄（40%，冷却 10 秒）", s.StumbleEnabled, v => { s.StumbleEnabled = v; Apply(); }),
            Ui.Check("空闲时小憩", s.AutoSleepEnabled, v => { s.AutoSleepEnabled = v; Apply(); }),
            Ui.Check("免打扰（关闭回复朗读）", s.QuietMode, v => { s.QuietMode = v; Apply(); }),
            Ui.Button("暂停 / 恢复动画", () => owner.Pet.Paused = !owner.Pet.Paused),
            Ui.Button("显示伙伴并移回屏幕", owner.ShowPet));
        panel.Children.Add(Ui.Text("全局快捷键", 18));
        foreach (var entry in new[] { ("chat", "⌘ ⇧ Space · 快聊"), ("show", "⌘ ⇧ D · 显示伙伴"), ("hide", "⌘ ⇧ H · 隐藏伙伴") })
            panel.Children.Add(Ui.Check(entry.Item2, s.Hotkeys.GetValueOrDefault(entry.Item1) == "enabled", v => { s.Hotkeys[entry.Item1] = v ? "enabled" : "disabled"; owner.Save(); owner.RegisterHotkeys(); }));
        panel.Children.Add(Ui.Button("启用登录启动", () => { try { MacServices.SetLoginAtStartup(true); } catch (Exception e) { owner.Error(e); } }));
        panel.Children.Add(Ui.Button("关闭登录启动", () => { try { MacServices.SetLoginAtStartup(false); } catch (Exception e) { owner.Error(e); } }));
        var min = new NumericUpDown { Minimum = 1, Maximum = 1440, Value = s.GreetingMinMinutes };
        var max = new NumericUpDown { Minimum = 1, Maximum = 1440, Value = s.GreetingMaxMinutes };
        panel.Children.Add(Ui.Check("偶尔主动打招呼", s.GreetingEnabled, v => { s.GreetingEnabled = v; owner.Save(); owner.Pet.ConfigureGreetings(); }));
        panel.Children.Add(Ui.Check("使用 AI 生成问候（未配置时使用本地问候）", s.AiGreetingEnabled, v => { s.AiGreetingEnabled = v; owner.Save(); owner.Pet.ConfigureGreetings(); }));
        panel.Children.Add(Ui.Field("问候最短间隔（分钟）", min)); panel.Children.Add(Ui.Field("问候最长间隔（分钟）", max));
        panel.Children.Add(Ui.Button("保存问候间隔", () => { s.GreetingMinMinutes = (int)(min.Value ?? 30); s.GreetingMaxMinutes = Math.Max(s.GreetingMinMinutes, (int)(max.Value ?? 60)); owner.Save(); owner.Pet.ConfigureGreetings(); }));
        return panel;
    }
    private Control Voice()
    {
        var s = owner.Settings;
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = s.Volume };
        volume.ValueChanged += (_, _) => { s.Volume = (int)volume.Value; owner.Session.SetVolume(s.Volume); owner.Live.SetVolume(s.Volume); owner.Save(); };
        var speech = Ui.Input(s.VoiceName); var live = Ui.Input(s.LiveVoice);
        var voices = new ComboBox { ItemsSource = VoicePresets.For("speech", s.SpeechConnection()).Select(v => v.Id).ToList() };
        voices.SelectionChanged += (_, _) => { if (voices.SelectedItem is string choice) speech.Text = choice; };
        return Ui.Column(Ui.Text("语音与音色", 20), Ui.Check("自动朗读回复", s.ReadReplies, v => { s.ReadReplies = v; owner.Save(); }), Ui.Check("使用云端朗读（关闭时使用 macOS 系统朗读）", s.UseCloudVoice, v => { s.UseCloudVoice = v; owner.Save(); }), Ui.Field("音量", volume), Ui.Field("服务音色", voices), Ui.Field("朗读音色 / 系统语音名称", speech), Ui.Field("Gemini Live 音色", live),
            Ui.Button("保存当前伙伴音色", () => { s.VoiceName = speech.Text?.Trim() ?? ""; s.LiveVoice = live.Text?.Trim() ?? "Aoede"; PersonaProfiles.SaveVoices(s); owner.Save(); }),
            Ui.AsyncButton("试听朗读", () => owner.Session.PreviewSpeechAsync(s, owner.Skin.Name), owner.Error), Ui.Button("停止朗读", owner.Session.StopSpeech), Ui.Text("只有点击聊天听写或 Live 开始时才申请麦克风权限。云语音与 Live 的服务和密钥在对应标签独立配置。"));
    }
    private Control Storage() => Ui.Column(Ui.Text("数据与隐私", 20), Ui.Text(owner.DataDirectory), Ui.Check("在本机加密保存聊天记录", owner.Settings.SaveChatHistory, v => { owner.Settings.SaveChatHistory = v; owner.Save(); }), Ui.Text("升级应用保留此目录；删除应用不会主动清除人格、配置和记录。Windows 加密配置无法在 Mac 直接解密，迁移后请重新录入密钥。"), Ui.Button("打开数据目录", () => owner.Run(async () => { await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(owner.DataDirectory)); })), Ui.Button("重开使用引导", owner.ShowGuide), Ui.Button("关于与检查更新", owner.ShowAbout));
}
