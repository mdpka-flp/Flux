using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Flux.ViewModels;
using Flux.Views;
using System.IO;
using System.Linq;

namespace Flux;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;

            // Если приложение запущено с аргументами (например, "Открыть с помощью"),
            // берём первый существующий аудиофайл и запускаем его.
            var audioFile = Program.StartupArgs.FirstOrDefault(File.Exists);
            if (!string.IsNullOrEmpty(audioFile))
            {
                vm.PlayFileCommand.Execute(audioFile);
            }
        }   

        base.OnFrameworkInitializationCompleted();
    }
}