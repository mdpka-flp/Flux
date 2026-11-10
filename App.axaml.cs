using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Flux.Services;
using Flux.ViewModels;
using Flux.Views;
using System;
using System.IO;
using System.Linq;

namespace Flux;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Name = "Flux";
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SettingsService.Load();
            LocalizationService.Instance.Load(SettingsService.Current.Language);

            var vm = new MainViewModel();
            var window = new MainWindow { DataContext = vm, Icon = LoadWindowIcon() };
            desktop.MainWindow = window;

            if (Program.SingleInstance is { } singleInstance)
            {
                singleInstance.FileRequested += path => Dispatcher.UIThread.Post(() =>
                {
                    vm.IsSettingsOpen = false;

                    if (!window.IsVisible) window.Show();
                    if (window.WindowState == WindowState.Minimized)
                        window.WindowState = WindowState.Normal;

                    window.Activate();

                    if (!string.IsNullOrEmpty(path))
                        vm.PlayFileCommand.Execute(path);
                });

                singleInstance.DeliverPending();
            }

            var audioFile = Program.StartupArgs.FirstOrDefault(File.Exists);
            if (!string.IsNullOrEmpty(audioFile))
            {
                vm.PlayFileCommand.Execute(audioFile);
            }
        }   

        base.OnFrameworkInitializationCompleted();
    }

    private static WindowIcon? LoadWindowIcon()
    {
        // Под Windows иконка окна берётся из ресурса исполняемого файла,
        // под Linux её нужно задать явно.
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Flux/Assets/Flux.png"));
            return new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }
}
