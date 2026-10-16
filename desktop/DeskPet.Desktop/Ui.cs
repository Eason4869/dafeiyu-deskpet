using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DeskPet.Desktop;

internal static class Ui
{
    public static StackPanel Column(params Control[] children)
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }
    public static StackPanel Row(params Control[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }
    public static TextBlock Text(string value, double size = 14) => new() { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap };
    public static TextBox Input(string value = "", string? hint = null) => new() { Text = value, Watermark = hint, MinWidth = 200 };
    public static Button Button(string label, Action action)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) => action();
        return button;
    }
    public static Button AsyncButton(string label, Func<Task> action, Action<Exception> error)
    {
        var button = new Button { Content = label };
        button.Click += async (_, _) => { button.IsEnabled = false; try { await action(); } catch (OperationCanceledException) { } catch (Exception e) { error(e); } finally { button.IsEnabled = true; } };
        return button;
    }
    public static CheckBox Check(string label, bool value, Action<bool> changed)
    {
        var check = new CheckBox { Content = label, IsChecked = value };
        check.IsCheckedChanged += (_, _) => changed(check.IsChecked == true);
        return check;
    }
    public static Window Window(string title, Control content, double width = 650, double height = 620) => new()
    {
        Title = title, Width = width, Height = height, MinWidth = Math.Min(360, width), MinHeight = 280,
        Content = new Border { Padding = new Thickness(20), Child = content }, WindowStartupLocation = WindowStartupLocation.CenterScreen
    };
    public static Control Field(string label, Control input) => Column(Text(label), input);
}
