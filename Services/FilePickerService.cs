using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Flux.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace Flux.Services;

public class FilePickerService : IFilePickerService
{
    public async Task<string?> PickAudioFileAsync()
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var localization = LocalizationService.Instance;
        var patterns = SettingsService.Current.EnabledExtensions
            .Select(extension => "*." + extension)
            .ToArray();

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = localization.DialogOpenAudio,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(localization.DialogAudioFiles)
                {
                    Patterns = patterns
                },
                new FilePickerFileType(localization.DialogAllFiles)
                {
                    Patterns = new[] { "*" }
                }
            }
        });

        if (files.Count == 0) return null;

        return files[0].Path.LocalPath;
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime 
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}
