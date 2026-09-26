using Avalonia.Threading;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flux.Interfaces;
using Flux.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Flux.ViewModels;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public partial class MainViewModel : ViewModelBase
{
    private static readonly string[] AudioExtensions =
        { ".mp3", ".flac", ".wav", ".ogg", ".m4a", ".aac", ".wma", ".opus" };

    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IFilePickerService _filePickerService;
    private readonly DispatcherTimer _positionTimer;

    private List<string> _playlist = new();
    private int _currentIndex = -1;
    private bool _suppressSeek;

    private string? _currentOriginalArtPath;

    [ObservableProperty] private string _trackTitle = "Ничего не выбрано";
    [ObservableProperty] private string _trackArtist = "Unknown";
    [ObservableProperty] private string _trackInfo = "";
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private int _volume = 80;
    [ObservableProperty] private Bitmap? _albumArt;

    [ObservableProperty] private double _position;
    [ObservableProperty] private string _currentTimeText = "00:00";
    [ObservableProperty] private string _totalTimeText = "00:00";

    public bool IsSeeking { get; set; }

    public MainViewModel()
    {
        _audioPlayerService = new AudioPlayerService();
        _filePickerService = new FilePickerService();

        _audioPlayerService.PlaybackEnded += OnPlaybackEnded;

        MprisService.Start();

        var settings = SettingsService.Load();
        Volume = settings.Volume;
        _audioPlayerService.SetVolume(Volume);

        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _positionTimer.Tick += (_, _) => UpdatePositionFromPlayer();
        _positionTimer.Start();
    }

    private void OnPlaybackEnded(object? sender, EventArgs e)
    {
        IsPlaying = false;
        _suppressSeek = true;
        Position = 100;
        _suppressSeek = false;

        MprisService.RegisterStatus("Stopped"); 
    }

    private void UpdatePositionFromPlayer()
    {
        if (_audioPlayerService.CurrentFilePath == null) return;
        if (IsSeeking) return;

        var duration = _audioPlayerService.Duration;
        var current = _audioPlayerService.CurrentTime;

        TotalTimeText = FormatTime(duration);

        _suppressSeek = true;
        CurrentTimeText = FormatTime(current);
        if (duration.TotalMilliseconds > 0)
            Position = current.TotalMilliseconds / duration.TotalMilliseconds * 100.0;
        _suppressSeek = false;
    }

    private static string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";

    partial void OnPositionChanged(double value)
    {
        if (_suppressSeek) return;
        var duration = _audioPlayerService.Duration;
        if (duration.TotalMilliseconds <= 0) return;

        var target = TimeSpan.FromMilliseconds(value / 100.0 * duration.TotalMilliseconds);
        _audioPlayerService.Seek(target);
        CurrentTimeText = FormatTime(target);
    }

    partial void OnVolumeChanged(int value)
    {
        _audioPlayerService.SetVolume(value);
        SettingsService.Save(new AppSettings { Volume = value });
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        var path = await _filePickerService.PickAudioFileAsync();
        if (string.IsNullOrEmpty(path)) return;

        BuildPlaylist(path);
        PlayCurrent();
    }

    [RelayCommand]
    private void PlayFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        BuildPlaylist(path);
        PlayCurrent();
    }

    private void BuildPlaylist(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir)) return;

        _playlist = Directory.EnumerateFiles(dir)
            .Where(f => AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _currentIndex = _playlist.IndexOf(filePath);
        if (_currentIndex < 0) _currentIndex = 0;
    }

    private void PlayCurrent()
    {
        if (_currentIndex < 0 || _currentIndex >= _playlist.Count) return;

        var path = _playlist[_currentIndex];
        _audioPlayerService.Play(path);
        UpdateTrackInfo(path);
        IsPlaying = true;

        _suppressSeek = true;
        Position = 0;
        CurrentTimeText = "00:00";
        _suppressSeek = false;
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (_audioPlayerService.IsPlaying)
        {
            _audioPlayerService.Pause();
            IsPlaying = false;

            MprisService.RegisterStatus("Paused"); 
        }
        else
        {
            _audioPlayerService.Resume();
            IsPlaying = true;

            MprisService.RegisterStatus("Playing"); 
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _audioPlayerService.Stop();
        IsPlaying = false;

        _suppressSeek = true;
        Position = 0;
        CurrentTimeText = "00:00";
        _suppressSeek = false;

        MprisService.RegisterStatus("Stopped"); 
    }

    [RelayCommand]
    private void Next()
    {
        if (_playlist.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _playlist.Count;
        PlayCurrent();
    }

    [RelayCommand]
    private void Previous()
    {
        if (_playlist.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _playlist.Count) % _playlist.Count;
        PlayCurrent();
    }

    [RelayCommand]
    private void Rewind()
    {
        var target = _audioPlayerService.CurrentTime - TimeSpan.FromSeconds(5);
        if (target < TimeSpan.Zero) target = TimeSpan.Zero;
        _audioPlayerService.Seek(target);
    }

    [RelayCommand]
    private void Forward()
    {
        var target = _audioPlayerService.CurrentTime + TimeSpan.FromSeconds(5);
        var duration = _audioPlayerService.Duration;
        if (target > duration) target = duration;
        _audioPlayerService.Seek(target);
    }

    private void UpdateTrackInfo(string filePath)
    {
        var title = _audioPlayerService.GetTitle();
        var artist = _audioPlayerService.GetArtist();

        TrackTitle = !string.IsNullOrEmpty(title)
            ? title
            : Path.GetFileNameWithoutExtension(filePath);

        TrackArtist = !string.IsNullOrEmpty(artist) ? artist : "Unknown";

        var ext = _audioPlayerService.GetExtension()?.TrimStart('.').ToUpper() ?? "UNKNOWN";
        var rate = _audioPlayerService.GetSampleRate();
        var channels = _audioPlayerService.GetChannels();
        var bitrate = _audioPlayerService.GetBitrate();

        var bitratePart = bitrate > 0 ? $" • {bitrate} kbps" : "";
        TrackInfo = $"{ext} • {rate} Hz • {channels} ch{bitratePart}";

        LoadAlbumArt();

        MprisService.UpdateMetadata(TrackTitle, TrackArtist, _audioPlayerService.GetAlbumArtPath() ?? "");
        MprisService.RegisterStatus("Playing");
    }

    private void LoadAlbumArt()
    {
        var path = _audioPlayerService.GetAlbumArtPath();
        _currentOriginalArtPath = path;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            AlbumArt = null;
            return;
        }

        try
        {
            AlbumArt?.Dispose();
            using var stream = File.OpenRead(path);
            AlbumArt = Bitmap.DecodeToWidth(stream, 120); 
        }
        catch
        {
            AlbumArt = null;
        }
    }

    [RelayCommand]
    private void OpenOriginalArt()
    {
        if (string.IsNullOrEmpty(_currentOriginalArtPath) || !File.Exists(_currentOriginalArtPath)) 
            return;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "explorer.exe" : "xdg-open",
                Arguments = $"\"{_currentOriginalArtPath}\"",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch { }
    }

}