using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Flux.Interfaces;
using System.Threading.Tasks;

namespace Flux.Services;

public class FilePickerService : IFilePickerService
{
    public async Task<string?> PickAudioFileAsync()
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите аудиофайл",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Аудиофайлы")
                {
                    Patterns = new[] { "*.mp3", "*.flac", "*.wav", "*.ogg", "*.m4a", "*.aac" }
                },
                new FilePickerFileType("Все файлы")
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