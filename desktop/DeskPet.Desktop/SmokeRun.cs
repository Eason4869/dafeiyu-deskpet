using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Dafeiyu;

namespace DeskPet.Desktop;

internal sealed record SmokeOptions(string DataDirectory, string Report);

internal static class SmokeRun
{
    public static SmokeOptions? Options { get; private set; }
    public static void Configure(string[] args)
    {
        if (!args.Contains("--smoke-ui")) return;
        string Required(string name)
        {
            var index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length || args[index + 1].StartsWith("--")) throw new ArgumentException("smoke-ui requires " + name);
            return Path.GetFullPath(args[index + 1]);
        }
        var directory = Required("--data-dir");
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()) throw new ArgumentException("smoke-ui data directory must be new or empty; production data is never opened");
        Options = new SmokeOptions(directory, Required("--report"));
    }
    public static void Start(DesktopController controller)
    {
        var windows = new List<Window>();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        var phase = 0;
        timer.Tick += (_, _) =>
        {
            try
            {
                if (phase++ == 0)
                {
                    windows.AddRange(new Window[] { new ChatWindow(controller, true), new ChatWindow(controller, false), new SettingsWindow(controller), new GuideWindow(controller), new AboutWindow(controller), new LiveWindow(controller) });
                    foreach (var window in windows) window.Show();
                    return;
                }
                timer.Stop();
                if (!controller.Pet.IsVisible || windows.Any(w => !w.IsVisible || w.Bounds.Width <= 0)) throw new InvalidOperationException("A required native UI window did not open");
                using var assets = new SpriteAssets(); var clips = 0;
                foreach (var skin in controller.Skins.Where(s => s.BuiltIn)) foreach (var action in SpriteAssets.PairedActions) foreach (var original in new[] { false, true })
                {
                    var clip = assets.Resolve(skin, action, original); var bitmap = assets.Get(skin, clip.File);
                    if (SpriteAssets.Rect(clip, clip.Frames - 1).Bottom > bitmap.PixelSize.Height) throw new InvalidDataException("Invalid animation asset");
                    clips++;
                }
                Report(new { success = true, version = typeof(App).Assembly.GetName().Version?.ToString(3), os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), windows = windows.Select(w => w.GetType().Name).Prepend(nameof(PetWindow)).ToArray(), validatedClips = clips, microphoneRequested = false, keychainAccessed = false, dataDirectory = controller.DataDirectory });
                controller.Run(controller.ExitAsync);
            }
            catch (Exception error) { timer.Stop(); Failure(error); (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown(1); }
        };
        timer.Start();
    }
    public static void Failure(Exception error) => Report(new { success = false, error = error.GetType().Name, message = error.Message });
    private static void Report(object value)
    {
        if (Options == null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Options.Report)!);
        File.WriteAllText(Options.Report, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed class SmokeRuntime(string directory) : DesktopRuntime
{
    public override string DataDirectory => directory;
    public override bool ShowTray => false;
    public override void InitializeProtection() => SecretProtection.Configure(new EphemeralProtector());
    public override IReplySpeech CreateSpeech(ApiClient api) => new UnavailableSpeech();
    public override IAudioRecorder CreateRecorder() => throw new InvalidOperationException("Audio is not exercised by UI smoke");
    public override IPcmMicrophone CreateMicrophone() => throw new InvalidOperationException("Audio is not exercised by UI smoke");
    public override ILiveAudioOutput CreateLiveOutput() => throw new InvalidOperationException("Audio is not exercised by UI smoke");
    private sealed class UnavailableSpeech : IReplySpeech
    {
        public Task SpeakAsync(string reply, PetSettings settings, CancellationToken cancel) => throw new InvalidOperationException("Audio is not exercised by UI smoke");
        public void SetVolume(int volume) { }
        public void Stop() { }
        public void Dispose() { }
    }
    private sealed class EphemeralProtector : ISecretProtector
    {
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        public byte[] Protect(byte[] plain)
        {
            var result = new byte[plain.Length + 28]; RandomNumberGenerator.Fill(result.AsSpan(0, 12));
            using var aes = new AesGcm(key, 16); aes.Encrypt(result.AsSpan(0, 12), plain, result.AsSpan(28), result.AsSpan(12, 16)); return result;
        }
        public byte[] Unprotect(byte[] cipher)
        {
            if (cipher.Length < 28) throw new CryptographicException("Invalid smoke ciphertext");
            var result = new byte[cipher.Length - 28]; using var aes = new AesGcm(key, 16); aes.Decrypt(cipher.AsSpan(0, 12), cipher.AsSpan(28), cipher.AsSpan(12, 16), result); return result;
        }
    }
}
