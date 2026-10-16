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
            var controller = new DesktopController(desktop);
            desktop.MainWindow = controller.Pet;
            desktop.Exit += (_, _) => controller.Dispose();
            controller.Start();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
