using Avalonia;

namespace DeskPet.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try { SmokeRun.Configure(args); return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch (Exception error) { if (SmokeRun.Options != null) SmokeRun.Failure(error); Console.Error.WriteLine(error.GetType().Name + ": " + error.Message); return 1; }
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
