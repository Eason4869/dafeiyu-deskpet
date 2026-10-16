using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace DeskPet.Desktop;

public sealed class App : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var controller = new DesktopController(desktop, SmokeRun.Options is { } smoke ? new SmokeRuntime(smoke.DataDirectory) : null);
            desktop.MainWindow = controller.Pet;
            desktop.Exit += (_, _) => controller.Dispose();
            if (SmokeRun.Options != null) SmokeRun.Start(controller); else controller.Start();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
