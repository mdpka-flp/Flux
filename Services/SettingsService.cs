using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flux.Services;

public class AppSettings
{
    public static readonly string[] SupportedExtensions =
        { "mp3", "flac", "wav", "ogg", "m4a", "aac", "wma", "opus" };

    public int Volume { get; set; } = 80;

    // Код языка из localization.json.
    public string Language { get; set; } = "ru";

    // Расширения без точки, которые плеер считает аудиофайлами.
    public List<string> Extensions { get; set; } = new(SupportedExtensions);

    public bool MicaEnabled { get; set; } = true;

    // Прозрачность фона в процентах (0 — непрозрачный, 100 — полностью прозрачный).
    public int BackgroundTransparency { get; set; } = 40;

    // Основной цвет фона в формате #RRGGBB.
    public string BackgroundColor { get; set; } = "#111111";

    // Акцентный цвет ползунков и других активных элементов (#RRGGBB).
    public string AccentColor { get; set; } = "#0078D7";

    // Цвет текста и значков кнопок (#RRGGBB).
    public string TextColor { get; set; } = "#FFFFFF";

    // Допустимые расширения из текущих настроек; никогда не пустой —
    // если пользователь оставил пустой список, берутся все поддерживаемые.
    [JsonIgnore]
    public IReadOnlyList<string> EnabledExtensions
    {
        get
        {
            var result = (Extensions ?? new List<string>())
                .Select(e => (e ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant())
                .Where(e => e.Length > 0 && SupportedExtensions.Contains(e))
                .Distinct()
                .ToList();

            return result.Count > 0 ? result : SupportedExtensions.ToList();
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

public static class SettingsService
{
    private static bool _loaded;

    public static AppSettings Current { get; private set; } = new();

    // Вызывается после сохранения настроек (в UI-потоке).
    public static event Action? Changed;

    public static AppSettings Load()
    {
        if (_loaded) return Current;
        _loaded = true;

        Current = ReadSettings() ?? MigrateLegacySettings() ?? new AppSettings();
        return Current;
    }

    public static void Save()
    {
        WriteSettings(Current);
        Changed?.Invoke();
    }

    private static AppSettings? ReadSettings()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsFile)) return null;

            var json = File.ReadAllText(AppPaths.SettingsFile);
            return JsonSerializer.Deserialize(json, SourceGenerationContext.Default.AppSettings);
        }
        catch
        {
            return null;
        }
    }

    private static AppSettings? MigrateLegacySettings()
    {
        try
        {
            if (!File.Exists(AppPaths.LegacySettingsFile)) return null;

            var legacy = JsonSerializer.Deserialize(
                File.ReadAllText(AppPaths.LegacySettingsFile),
                SourceGenerationContext.Default.AppSettings);

            if (legacy is null) return null;

            var migrated = new AppSettings { Volume = legacy.Volume };
            WriteSettings(migrated);
            return migrated;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteSettings(AppSettings settings)
    {
        try
        {
            AppPaths.EnsureConfigDirectory();
            var json = JsonSerializer.Serialize(settings, SourceGenerationContext.Default.AppSettings);
            File.WriteAllText(AppPaths.SettingsFile, json);
        }
        catch
        {
            // Настройки не критичны для воспроизведения.
        }
    }
}
