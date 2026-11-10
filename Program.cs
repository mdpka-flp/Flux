using Avalonia;
using Flux.Services;
using System;

namespace Flux;

sealed class Program
{
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    public static SingleInstanceService? SingleInstance { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        StartupArgs = args;
        var instance = new SingleInstanceService();

        if (!instance.TryStart(args))
        {
            instance.Dispose();
            return;
        }

        SingleInstance = instance;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            SingleInstance = null;
            instance.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseWaylandWithFallback();
    }
}
