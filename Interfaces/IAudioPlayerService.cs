namespace Flux.Interfaces;

public interface IAudioPlayerService
{
    void Play(string filePath);
    void Pause();
    void Stop();
}