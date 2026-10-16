using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Dafeiyu;
using DeskPet.Desktop;

AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
int assertions = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); assertions++; }
void Reject(Action action, string name) { try { action(); } catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException or JsonException) { assertions++; return; } throw new Exception("FAIL: accepted " + name); }
var directory = Path.Combine(AppContext.BaseDirectory, "DeskPet-UI-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var skins = SpriteAssets.LoadSkins(directory);
    Check(skins.Count == 4, "four bundled companions");
    foreach (var skin in skins)
    {
        using var assets = new SpriteAssets();
        foreach (var action in SpriteAssets.PairedActions)
        foreach (var original in new[] { false, true })
        {
            var clip = assets.Resolve(skin, action, original); var bitmap = assets.Get(skin, clip.File);
            Check(clip.Fps == 8, skin.Id + "/" + action + " runs at 8FPS");
            for (int frame = 0; frame < clip.Frames; frame++)
            {
                var rect = SpriteAssets.Rect(clip, frame);
                Check(rect.Right <= bitmap.PixelSize.Width && rect.Bottom <= bitmap.PixelSize.Height, "frame inside atlas");
                Check(!assets.Hit(skin, clip, frame, -1, 0), "outside alpha is transparent");
                bool visible = false, transparent = false;
                for (int y = 0; y < 20; y++) for (int x = 0; x < 20; x++) { var hit = assets.Hit(skin, clip, frame, x / 20.0, y / 20.0); visible |= hit; transparent |= !hit; }
                Check(visible && transparent, skin.Id + "/" + action + " has visible alpha and transparent area");
            }
        }
        foreach (var original in new[] { false, true })
        {
            var player = new InteractionPlayback((action, version) => assets.Resolve(skin, action, version));
            player.Play("feeding", original);
            int length = player.Clip.Frames;
            player.Advance(60); Check(player.Frame == 1, "delayed clock displays next frame only");
            for (int i = 1; i < length; i++) player.Advance(.125);
            Check(player.Action == "happy" && player.Original == original && player.Frame == 0, "feeding keeps version into happy");
            player.Play("lifted", original); player.Land(false);
            Check(player.Action == "landing" && player.Original == original, "lift and normal landing paired");
            player.Play("lifted", original); player.Land(true);
            Check(player.Action == "stumble" && player.Original == original, "lift and stumble paired");
        }
    }
    var source = Path.Combine(directory, "source"); Directory.CreateDirectory(source);
    using (var bitmap = new WriteableBitmap(new PixelSize(512, 704), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul))
    {
        var pixels = new byte[512 * 704 * 4];
        for (int row = 0; row < 11; row++) for (int col = 0; col < 8; col++) for (int y = 16; y < 48; y++) for (int x = 16; x < 48; x++) { var i = ((row * 64 + y) * 512 + col * 64 + x) * 4; pixels[i] = 180; pixels[i + 3] = 255; }
        using (var buffer = bitmap.Lock()) Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
        bitmap.Save(Path.Combine(source, "spritesheet.png"), new PngBitmapEncoderOptions());
    }
    var manifest = Path.Combine(source, "pet.json");
    File.WriteAllText(manifest, "{\"version\":1,\"name\":\"大肥鱼\",\"atlas\":\"spritesheet.png\"}");
    var imported = SpriteAssets.Import(manifest, directory);
    Check(!imported.BuiltIn && imported.Id.StartsWith("skin-"), "same-name skin gets independent identity");
    using (var assets = new SpriteAssets()) Check(assets.Resolve(imported, "feeding", false).File == "spritesheet.png", "custom skin never uses bundled interaction");
    File.WriteAllText(manifest, "{\"version\":1,\"name\":\"bad\",\"atlas\":\"../escape.png\"}");
    Reject(() => SpriteAssets.Import(manifest, directory), "path traversal");
    Check(SpriteAssets.LoadSkins(directory).Count == 5, "rejected import preserves existing skins");
    var abandoned = Path.Combine(directory, "skins", ".import-abandoned"); Directory.CreateDirectory(abandoned);
    File.Copy(Path.Combine(source, "spritesheet.png"), Path.Combine(abandoned, "spritesheet.png"));
    File.WriteAllText(Path.Combine(abandoned, "pet.json"), "{\"version\":1,\"name\":\"interrupted\",\"atlas\":\"spritesheet.png\"}");
    Check(SpriteAssets.LoadSkins(directory).Count == 5, "interrupted staging import never appears as a companion");
    Check(AboutWindow.MatchesArchitecture("DeskPet-0.5.5-macos-x86_64-unsigned.zip", "x64"), "Intel release packaging name matches current architecture");
    Check(!AboutWindow.MatchesArchitecture("DeskPet-0.5.5-macos-arm64-unsigned.zip", "x64"), "Intel query excludes arm64 archive");

    var runtime = new TestRuntime(Path.Combine(directory, "data"));
    using var controller = new DesktopController(new ClassicDesktopStyleApplicationLifetime(), runtime);
    Check(controller.ShouldShowAutomaticGuide, "fresh install opens initial guide once");
    controller.Settings.OnboardingState = "completed"; controller.Save();
    using (var restarted = new DesktopController(new ClassicDesktopStyleApplicationLifetime(), new TestRuntime(Path.Combine(directory, "data")))) { Check(!restarted.ShouldShowAutomaticGuide, "same-series restart does not reopen completed guide"); restarted.Pet.Close(); }
    Check(!OnboardingUpgrade.Prepare(controller.Settings, "0.5.99").ShowGuide, "patch upgrade does not show UI guide");
    var upgrade = OnboardingUpgrade.Prepare(controller.Settings, "0.6.0"); Check(upgrade.ShowGuide && controller.Settings.OnboardingStep == 0, "series upgrade resets actual UI guide state");
    Check(!OnboardingUpgrade.Prepare(controller.Settings, "0.6.1").ShowGuide, "claimed series guide not retriggered");
    Check(!OnboardingUpgrade.Prepare(controller.Settings, "0.5.5").ShowGuide, "downgrade does not reopen UI guide");
    var windows = new Window[] { new ChatWindow(controller, true), new ChatWindow(controller, false), new SettingsWindow(controller), new AboutWindow(controller), new LiveWindow(controller) };
    foreach (var window in windows) { window.Show(); window.Measure(new Size(900, 800)); window.Arrange(new Rect(0, 0, 900, 800)); Dispatcher.UIThread.RunJobs(); Check(window.IsVisible && window.Bounds.Width > 0, window.GetType().Name + " opens with actual controls"); }
    var settings = (SettingsWindow)windows[2];
    foreach (var section in new[] { "partner", "behavior", "text", "asr", "speech", "live", "voice", "storage" }) { settings.SelectSection(section); Dispatcher.UIThread.RunJobs(); Check(settings.GetLogicalDescendants().OfType<TabControl>().Single().SelectedItem != null, "settings section " + section); }
    settings.SelectSection("text"); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    using (var rendered = settings.CaptureRenderedFrame())
    {
        Check(rendered != null && rendered.PixelSize.Width > 500, "actual themed settings window renders to bitmap");
        var artifacts = Path.Combine(AppContext.BaseDirectory, "artifacts"); Directory.CreateDirectory(artifacts);
        rendered!.Save(Path.Combine(artifacts, "settings.png"), new PngBitmapEncoderOptions());
    }
    var editor = new ProviderEditor(controller, "text");
    var editorHost = new Window { Content = editor }; editorHost.Show(); Dispatcher.UIThread.RunJobs();
    var add = editor.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "新增厂商");
    add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(controller.Settings.TextProviders.Count == 1, "provider drafts stay isolated before Save");
    var save = editor.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "保存并启用此厂商 / 模型");
    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(controller.Settings.TextProviders.Count == 2, "provider Save applies real business settings");
    var permissionChat = new ChatWindow(controller, true); permissionChat.Show();
    runtime.PendingPermission = new TaskCompletionSource<bool>();
    controller.Settings.Transcription.BaseUrl = "https://api.example.test/v1"; controller.Settings.Transcription.Model = "whisper-1"; controller.Settings.Transcription.ApiKey = "test-key";
    var record = permissionChat.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "听写 / 完成录音");
    record.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); permissionChat.Close(); runtime.PendingPermission.SetResult(true); Dispatcher.UIThread.RunJobs();
    Check(runtime.RecorderCreations == 0 && !controller.Dictation.IsBusy, "late microphone permission after chat close cannot open device");
    var guide = new GuideWindow(controller); guide.Show(); Dispatcher.UIThread.RunJobs();
    var later = guide.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "稍后继续"); later.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(controller.Settings.OnboardingState == "deferred", "guide deferred state persists");
    var nextGuide = new GuideWindow(controller); nextGuide.Show();
    var skip = nextGuide.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "跳过全部引导"); skip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Check(controller.Settings.OnboardingState == "skipped", "guide skip releases window");
    editor.Dispose(); editorHost.Close();
    foreach (var window in windows) window.Close();
    controller.Pet.Close(); Dispatcher.UIThread.RunJobs();
    Console.WriteLine("PASS: " + assertions + " Desktop UI/resource/animation assertions");
}
finally
{
    // Only remove this run's newly created GUID directory.
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
}

sealed class TestRuntime(string directory) : DesktopRuntime
{
    public TaskCompletionSource<bool>? PendingPermission { get; set; }
    public int RecorderCreations { get; private set; }
    public override string DataDirectory => directory;
    public override bool ShowTray => false;
    public override void InitializeProtection() => SecretProtection.Configure(new TestProtector());
    public override IReplySpeech CreateSpeech(ApiClient api) => new TestSpeech();
    public override IAudioRecorder CreateRecorder() { RecorderCreations++; return new TestRecorder(); }
    public override Task<bool> RequestMicrophoneAccessAsync() => PendingPermission?.Task ?? Task.FromResult(false);
    public override IPcmMicrophone CreateMicrophone() => throw new InvalidOperationException("UI smoke does not open real audio devices");
    public override ILiveAudioOutput CreateLiveOutput() => throw new InvalidOperationException("UI smoke does not open real audio devices");
}
sealed class TestRecorder : IAudioRecorder
{
    public bool IsRecording { get; private set; }
    public void Start() => IsRecording = true;
    public Task<byte[]> FinishAsync() => Task.FromResult(WaveData.EncodePcm(new byte[320]));
    public void Dispose() => IsRecording = false;
}
sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
}
sealed class TestSpeech : IReplySpeech
{
    public void Dispose() { }
    public void SetVolume(int volume) { }
    public void Stop() { }
    public Task SpeakAsync(string reply, PetSettings settings, CancellationToken cancel) => Task.CompletedTask;
}
sealed class TestProtector : ISecretProtector
{
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    public byte[] Protect(byte[] plain)
    {
        var bytes = new byte[28 + plain.Length]; RandomNumberGenerator.Fill(bytes.AsSpan(0, 12));
        using var aes = new AesGcm(key, 16); aes.Encrypt(bytes.AsSpan(0, 12), plain, bytes.AsSpan(28), bytes.AsSpan(12, 16)); return bytes;
    }
    public byte[] Unprotect(byte[] cipher)
    {
        var plain = new byte[cipher.Length - 28]; using var aes = new AesGcm(key, 16); aes.Decrypt(cipher.AsSpan(0, 12), cipher.AsSpan(28), cipher.AsSpan(12, 16), plain); return plain;
    }
}
