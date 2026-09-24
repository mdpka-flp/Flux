using System.Threading.Tasks;

namespace Flux.Interfaces;

public interface IFilePickerService
{
    Task<string?> PickAudioFileAsync();
}