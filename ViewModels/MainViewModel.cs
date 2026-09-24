using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flux.Interfaces;
using Flux.Services;
using System.IO;
using System.Threading.Tasks;

namespace Flux.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IFilePickerService _filePickerService;

    [ObservableProperty]
    private string _currentTrack = "Ничего не выбрано";

    [ObservableProperty]
    private bool _isPlaying;

    public MainViewModel()
    {
        _audioPlayerService = new AudioPlayerService();
        _filePickerService = new FilePickerService();
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        var path = await _filePickerService.PickAudioFileAsync();
        if (string.IsNullOrEmpty(path)) return;

        _audioPlayerService.Play(path);
        IsPlaying = true;
        CurrentTrack = $"▶ {Path.GetFileName(path)}";
    }

    [RelayCommand]
    private void PlayPause()
    {
        // В LibVLC Pause() работает как переключатель: если играет — пауза, если пауза — продолжает
        _audioPlayerService.Pause();
        IsPlaying = !IsPlaying;
    }

    [RelayCommand]
    private void Stop()
    {
        _audioPlayerService.Stop();
        IsPlaying = false;
        CurrentTrack = "Ничего не выбрано";
    }
}