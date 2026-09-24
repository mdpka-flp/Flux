using LibVLCSharp.Shared;
using Flux.Interfaces;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TagLib;

namespace Flux.Services;

public class AudioPlayerService : IAudioPlayerService
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;
    private Media? _currentMedia;
    private string? _currentFilePath;

    public bool IsPlaying => _mediaPlayer.IsPlaying;
    public TimeSpan Duration => TimeSpan.FromMilliseconds(Math.Max(0, _mediaPlayer.Length));
    public TimeSpan CurrentTime => TimeSpan.FromMilliseconds(Math.Max(0, _mediaPlayer.Time));
    public string? CurrentFilePath => _currentFilePath;

    public event EventHandler? PlaybackEnded;

    public AudioPlayerService()
    {
        Core.Initialize();
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);
        _mediaPlayer.EndReached += (_, _) => PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Play(string filePath)
    {
        _currentFilePath = filePath;
        _mediaPlayer.Stop();
        _currentMedia?.Dispose();

        // Кроссплатформенное создание Media — работает и на Windows, и на Linux
        _currentMedia = new Media(_libVlc, filePath, FromType.FromPath);

        try
        {
            _currentMedia.Parse(MediaParseOptions.ParseLocal, 2000).Wait();
        }
        catch
        {
            
        }

        _mediaPlayer.Play(_currentMedia);
    }

    public void Pause() => _mediaPlayer.SetPause(true);

    public void Resume()
    {
        if (_currentFilePath == null) return;

        var state = _mediaPlayer.State;
        if (state == VLCState.Paused)
        {
            _mediaPlayer.SetPause(false);
        }
        else if (state != VLCState.Playing)
        {
            Play(_currentFilePath);
        }
    }

    public void Stop()
    {
        if (_currentFilePath == null) return;

        _mediaPlayer.Stop();
        _currentMedia?.Dispose();

        _currentMedia = new Media(_libVlc, _currentFilePath, FromType.FromPath);
        _mediaPlayer.Media = _currentMedia;
    }

    public void Seek(TimeSpan position)
    {
        if (_currentMedia == null) return;
        _mediaPlayer.Time = (long)position.TotalMilliseconds;
    }

    public void SetVolume(int volume) => _mediaPlayer.Volume = volume;

    // --- Метаданные через TagLib ---

    private TagLib.File? OpenTagFile()
    {
        if (_currentFilePath == null || !System.IO.File.Exists(_currentFilePath))
            return null;

        try { return TagLib.File.Create(_currentFilePath); }
        catch { return null; }
    }

    public string? GetTitle()
    {
        using var file = OpenTagFile();
        return string.IsNullOrWhiteSpace(file?.Tag.Title) ? null : file!.Tag.Title;
    }

    public string? GetArtist()
    {
        using var file = OpenTagFile();
        if (file == null) return null;
        var performers = file.Tag.Performers;
        return performers is { Length: > 0 } && !string.IsNullOrWhiteSpace(performers[0])
            ? performers[0]
            : null;
    }

    public string? GetExtension()
    {
        if (_currentFilePath == null) return null;
        return Path.GetExtension(_currentFilePath);
    }

    public uint GetSampleRate()
    {
        using var file = OpenTagFile();
        return (uint)(file?.Properties.AudioSampleRate ?? 0);
    }

    public uint GetChannels()
    {
        using var file = OpenTagFile();
        return (uint)(file?.Properties.AudioChannels ?? 0);
    }

    public int GetBitrate()
    {
        using var file = OpenTagFile();
        return file?.Properties.AudioBitrate ?? 0;
    }

    public string? GetAlbumArtPath()
    {
        if (_currentFilePath == null) return null;

        try
        {
            using var file = TagLib.File.Create(_currentFilePath);
            var pictures = file.Tag.Pictures;
            if (pictures is not { Length: > 0 }) return null;

            var picture = pictures[0];

            var ext = picture.MimeType switch
            {
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                _ => ".img"
            };

            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Flux", "covers");
            Directory.CreateDirectory(cacheDir);

            var hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(_currentFilePath));
            var hash = Convert.ToHexString(hashBytes);
            var cachePath = Path.Combine(cacheDir, hash + ext);

            if (!System.IO.File.Exists(cachePath))
                System.IO.File.WriteAllBytes(cachePath, picture.Data.Data);

            return cachePath;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _currentMedia?.Dispose();
        _mediaPlayer.Dispose();
        _libVlc.Dispose();
    }
}