using System;

namespace Flux.Interfaces;

public interface IAudioPlayerService : IDisposable
{
    bool IsPlaying { get; }
    TimeSpan Duration { get; }
    TimeSpan CurrentTime { get; }
    string? CurrentFilePath { get; }

    void Play(string filePath);
    void Pause();
    void Resume();
    void Stop();
    void Seek(TimeSpan position);
    void SetVolume(int volume);

    event EventHandler? PlaybackEnded;

    string? GetTitle();
    string? GetArtist();
    string? GetAlbumArtPath();
    string? GetExtension();
    uint GetSampleRate();
    uint GetChannels();
    int GetBitrate();
}