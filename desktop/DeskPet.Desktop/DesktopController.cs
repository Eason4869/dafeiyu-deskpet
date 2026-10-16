using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Dafeiyu;
using DeskPet.Platform.Mac;

namespace DeskPet.Desktop;

internal sealed class DesktopController : IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime lifetime;
    private readonly SettingsStore store;
    private readonly TrayIcon tray;
    private readonly List<IDisposable> hotkeys = new();
    private ChatWindow? quickChat, fullChat;
    private SettingsWindow? settingsWindow;
    private GuideWindow? guide;
    private bool exiting;
    public PetSettings Settings { get; }
    public List<PetSkin> Skins { get; }
    public PetSkin Skin { get; private set; }
    public ConversationSession Session { get; }
    public ConversationQueue Queue { get; }
    public GeminiLiveSession Live { get; }
    public ApiClient Api { get; }
    public PetWindow Pet { get; }
    public string DataDirectory { get; }
    public bool GuideLocked => guide != null;
    public event Action? Changed;
    public DesktopController(IClassicDesktopStyleApplicationLifetime desktop)
    {
        lifetime = desktop;
        DataDirectory = Environment.GetEnvironmentVariable("DESKPET_DATA_DIR") ?? MacServices.DataDirectory;
        Directory.CreateDirectory(DataDirectory);
        MacServices.InstallSecretProtection();
        store = new SettingsStore(Path.Combine(DataDirectory, "settings.json")); Settings = store.Load();
        Skins = SpriteAssets.LoadSkins(DataDirectory); Skin = Skins.FirstOrDefault(s => s.Id == Settings.SkinId) ?? Skins[0];
        PersonaProfiles.Activate(Settings, Skin.Id, Skin.Prompt);
        Api = new ApiClient();
        Session = new ConversationSession(Api, MacServices.CreateSpeech(Api), MacServices.CreateRecorder, new HistoryStore(Path.Combine(DataDirectory, "history")), () => Settings.SaveChatHistory);
        Session.SelectPersona(Skin.Id);
        Queue = new ConversationQueue(Session, () => Settings, () => Skin.Name, Path.Combine(DataDirectory, "queue.json"));
        Live = new GeminiLiveSession(() => new GeminiLiveSocket(), MacServices.CreateMicrophone, MacServices.CreateLiveOutput);
        Live.TurnCommitted += (user, reply, status) => Session.AddLiveTurn(Session.PersonaId, user, reply, status);
        Session.StateChanged += Notify; Session.MessageAdded += _ => Notify(); Session.MessageUpdated += _ => Notify(); Session.HistoryChanged += Notify;
        Queue.Changed += Notify; Live.StateChanged += Notify;
        Pet = new PetWindow(this);
        using var icon = SpriteAssets.OpenAsset("pet.ico");
        tray = new TrayIcon { Icon = new WindowIcon(icon), ToolTipText = "DeskPet · " + Skin.Name, IsVisible = true };
        tray.Clicked += (_, _) => ShowPet();
        var menu = new NativeMenu();
        void Add(string text, Action action) { var item = new NativeMenuItem(text); item.Click += (_, _) => action(); menu.Items.Add(item); }
        Add("显示伙伴", ShowPet); Add("快速对话", () => ShowChat(true)); Add("完整聊天", () => ShowChat(false));
        Add("互动轮盘", () => Pet.ShowPalette()); Add("设置", ShowSettings); Add("使用引导", ShowGuide); Add("关于与更新", ShowAbout); Add("退出", () => Run(ExitAsync));
        tray.Menu = menu;
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { tray });
    }
    public void Start()
    {
        RegisterHotkeys();
        if (Settings.OnboardingState != "complete" && Settings.OnboardingState != "skipped") Dispatcher.UIThread.Post(ShowGuide);
        if (!string.IsNullOrWhiteSpace(store.Warning)) Dispatcher.UIThread.Post(() => Error(new InvalidOperationException(store.Warning)));
    }
    private void Notify() => Dispatcher.UIThread.Post(() => Changed?.Invoke());
    public void Save() { store.Save(Settings); Notify(); }
    public async Task SelectSkinAsync(PetSkin skin)
    {
        if (GuideLocked) throw new InvalidOperationException("请先完成或跳过当前引导。");
        await Queue.PauseAsync(); await Live.StopAsync(); await Session.StopAsync();
        PersonaProfiles.SaveVoices(Settings); Skin = skin;
        PersonaProfiles.Activate(Settings, skin.Id, skin.Prompt); Session.SelectPersona(skin.Id);
        Pet.SelectSkin(); Save(); tray.ToolTipText = "DeskPet · " + skin.Name;
    }
    public void ShowPet() { if (!Pet.IsVisible) Pet.Show(); Pet.Clamp(); }
    public void ShowChat(bool quick)
    {
        var window = quick ? quickChat : fullChat;
        if (window == null)
        {
            window = new ChatWindow(this, quick);
            if (quick) quickChat = window; else fullChat = window;
            window.Closed += (_, _) => { if (quick) quickChat = null; else fullChat = null; };
        }
        window.Show(); window.Activate();
    }
    public void ShowSettings()
    {
        if (settingsWindow == null) { settingsWindow = new SettingsWindow(this); settingsWindow.Closed += (_, _) => settingsWindow = null; }
        settingsWindow.Show(); settingsWindow.Activate();
    }
    public void ShowGuide()
    {
        if (guide != null) { guide.Activate(); return; }
        guide = new GuideWindow(this); guide.Closed += (_, _) => { guide = null; Notify(); }; guide.Show();
    }
    public void ShowAbout() => new AboutWindow(this).Show();
    public void RegisterHotkeys()
    {
        foreach (var binding in hotkeys) binding.Dispose(); hotkeys.Clear();
        if (!OperatingSystem.IsMacOS()) return;
        // Carbon key codes: Space, D, H. Defaults remain explicit in Settings UI.
        foreach (var binding in new (string Id, uint Key, Action Action)[] { ("chat", 49, () => ShowChat(true)), ("show", 2, ShowPet), ("hide", 4, Pet.Hide) })
        {
            if (!Settings.Hotkeys.TryGetValue(binding.Id, out var value) || value != "enabled") continue;
            try { hotkeys.Add(MacServices.RegisterHotKey(binding.Key, MacHotKeyModifiers.Command | MacHotKeyModifiers.Shift, () => Dispatcher.UIThread.Post(binding.Action))); }
            catch (Exception e) { Error(new InvalidOperationException("快捷键注册失败（" + binding.Id + "）：" + e.Message)); }
        }
    }
    public void Run(Func<Task> action) => _ = RunSafe(action);
    private async Task RunSafe(Func<Task> action) { try { await action(); } catch (OperationCanceledException) { } catch (Exception e) { Error(e); } }
    public void Error(Exception e)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(() => Error(e)); return; }
        var text = e is ApiException or InvalidOperationException or InvalidDataException or PlatformNotSupportedException ? e.Message : "操作失败（" + e.GetType().Name + "）。请检查网络、权限和数据目录。";
        var window = Ui.Window("DeskPet · 操作提示", Ui.Text(text), 460, 250); window.Show();
    }
    public async Task ExitAsync()
    {
        if (exiting) return; exiting = true;
        try { await Queue.PauseAsync(); await Live.StopAsync(); await Session.StopAsync(); Save(); await Session.FlushHistoryAsync(); lifetime.Shutdown(); }
        catch { exiting = false; throw; }
    }
    public void Dispose()
    {
        tray.Dispose(); foreach (var binding in hotkeys) binding.Dispose();
        Queue.Dispose(); Live.Dispose(); Session.Dispose();
    }
}
