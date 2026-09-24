using LibVLCSharp.Shared;
using Flux.Interfaces;
using System;

namespace Flux.Services;

public class AudioPlayerService : IAudioPlayerService, IDisposable
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;

    public AudioPlayerService()
    {
        Core.Initialize();
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);
    }

    public void Play(string filePath)
    {
        using var media = new Media(_libVlc, new Uri(filePath));
        _mediaPlayer.Play(media);
    }

    public void Pause() => _mediaPlayer.Pause();
    public void Stop() => _mediaPlayer.Stop();

    public void Dispose()
    {
        _mediaPlayer.Dispose();
        _libVlc.Dispose();
    }
}