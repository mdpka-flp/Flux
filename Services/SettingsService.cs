using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flux.Services;

public class AppSettings
{
    public int Volume { get; set; } = 80;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

public static class SettingsService
{
    private static readonly string SettingsPath = BuildPath();

    private static string BuildPath()
    {
        // Linux:   ~/.local/share/Flux/settings.json
        // Windows: %LOCALAPPDATA%\Flux\settings.json
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Flux");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
        
            return JsonSerializer.Deserialize(json, SourceGenerationContext.Default.AppSettings) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, SourceGenerationContext.Default.AppSettings);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
        
        }
    }

}