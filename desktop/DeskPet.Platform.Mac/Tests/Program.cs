using System.Security.Cryptography;
using System.Text;
using DeskPet.Platform.Mac;

var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
void Reject<T>(Action action, string name) where T : Exception { try { action(); } catch (T) { Check(true, name); return; } throw new Exception(name); }
var key = RandomNumberGenerator.GetBytes(32);
using (var protector = new MacSecretProtector(() => key.ToArray()))
{
    var plain = Encoding.UTF8.GetBytes("中文配置 secret queue history");
    var encrypted = protector.Protect(plain);
    Check(!encrypted.AsSpan().SequenceEqual(plain), "config ciphertext");
    Check(protector.Unprotect(encrypted).AsSpan().SequenceEqual(plain), "authenticated round trip");
    Check(!encrypted.AsSpan().SequenceEqual(protector.Protect(plain)), "fresh nonce");
    encrypted[^1] ^= 1;
    Reject<CryptographicException>(() => protector.Unprotect(encrypted), "tamper rejected");
    Reject<CryptographicException>(() => protector.Unprotect(plain), "foreign Windows data rejected");
    Check(protector.Unprotect(protector.Protect([])).Length == 0, "empty payload round trip");
    Reject<CryptographicException>(() => protector.Unprotect("DPM1"u8.ToArray()), "truncated header rejected");
    Reject<ArgumentNullException>(() => protector.Protect(null!), "null plaintext rejected");
    Reject<ArgumentNullException>(() => protector.Unprotect(null!), "null envelope rejected");
}
var disposed = new MacSecretProtector(() => key.ToArray());
disposed.Dispose();
Reject<ObjectDisposedException>(() => disposed.Protect([1]), "disposed protector rejected");
using (var wrong = new MacSecretProtector(() => RandomNumberGenerator.GetBytes(32)))
    Reject<CryptographicException>(() => wrong.Unprotect(new MacSecretProtector(() => key.ToArray()).Protect([1,2,3])), "wrong key rejected");
if (!OperatingSystem.IsMacOS())
{
    Reject<PlatformNotSupportedException>(() => MacServices.GetIdleTime(), "idle guarded before native load");
    Reject<PlatformNotSupportedException>(() => MacServices.CreateRecorder(), "microphone guarded before native load");
    Reject<PlatformNotSupportedException>(() => MacServices.CreateLiveOutput(), "playback guarded before native load");
    Reject<PlatformNotSupportedException>(() => MacServices.InstallSecretProtection(), "Keychain guarded before native load");
    Reject<PlatformNotSupportedException>(() => MacServices.RequestMicrophoneAccessAsync(), "permission request guarded");
    Reject<PlatformNotSupportedException>(() => MacServices.GetDisplays(), "display query guarded");
    Reject<PlatformNotSupportedException>(() => MacServices.GetPointerPosition(), "pointer guarded");
    Reject<PlatformNotSupportedException>(() => MacServices.SetLoginAtStartup(true), "login registration guarded");
    Reject<PlatformNotSupportedException>(() => MacServices.RegisterHotKey(0, MacHotKeyModifiers.Command, () => {}), "hotkey guarded");
    Reject<PlatformNotSupportedException>(() => MacServices.ConfigureWindow(new IntPtr(1), true, false), "window guarded");
}
var old = Environment.GetEnvironmentVariable("DESKPET_DATA_DIR");
try { Environment.SetEnvironmentVariable("DESKPET_DATA_DIR", Path.GetTempPath()); Check(MacServices.DataDirectory == Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), "isolated data directory"); }
finally { Environment.SetEnvironmentVariable("DESKPET_DATA_DIR", old); }
if (args.Contains("--native-smoke"))
{
    Check(OperatingSystem.IsMacOS(), "native smoke requires Mac");
    Check(Enum.IsDefined(MacServices.MicrophoneAccess), "microphone status native ABI");
    Check(MacServices.GetIdleTime() >= TimeSpan.Zero, "native idle query");
    var pointer = MacServices.GetPointerPosition();
    Check(double.IsFinite(pointer.X) && double.IsFinite(pointer.Y), "native pointer query");
    var displays = MacServices.GetDisplays();
    Check(displays.Length > 0 && displays.All(d => d.Width > 0 && d.Height > 0 && d.Scale > 0), "native display ABI");
    Check(MacServices.LoginAtStartupStatus >= 0, "native login status query without mutation");
    Console.WriteLine("Native smoke does not request microphone permission or modify login items.");
}
Console.WriteLine($"{checks} platform checks passed");
