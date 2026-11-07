using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
namespace LaserCraft.Desktop;

internal static class Program
{
    [STAThread] public static void Main(string[] args) => AppBuilder.Configure<LaserCraftApp>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
}
public sealed class LaserCraftApp : Avalonia.Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark; }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new MainWindow(desktop.Args?.FirstOrDefault());
        base.OnFrameworkInitializationCompleted();
    }
}
