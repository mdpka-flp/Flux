using Avalonia;
using System;

namespace Flux;

sealed class Program
{
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    [STAThread]
    public static void Main(string[] args)
    {
        StartupArgs = args;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        builder.UseWaylandWithFallback();

        return builder;
    }
}