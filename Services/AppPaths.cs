using System;
using System.IO;

namespace Flux.Services;

// Пути к конфигурации: ~/.config/Flux на Linux, %APPDATA%\Flux на Windows.
internal static class AppPaths
{
    public static string ConfigDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Flux");

    public static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");

    public static string LocalizationFile => Path.Combine(ConfigDirectory, "localization.json");

    // Старое расположение (только громкость) для миграции.
    public static string LegacySettingsFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Flux", "settings.json");

    public static void EnsureConfigDirectory() => Directory.CreateDirectory(ConfigDirectory);
}
