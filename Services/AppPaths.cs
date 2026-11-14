using System;
using System.IO;

namespace Flux.Services;

// Пути к конфигурации: ~/.config/Flux на Linux, %APPDATA%\Flux на Windows.
// Если система не отдаёт папку профиля, откатываемся на домашний каталог или
// временную папку, чтобы приложение всё равно запустилось.
internal static class AppPaths
{
    public static string ConfigDirectory { get; } = Path.Combine(
        ResolveBaseFolder(Environment.SpecialFolder.ApplicationData),
        "Flux");

    public static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");

    public static string LocalizationFile => Path.Combine(ConfigDirectory, "localization.json");

    public static string CoversDirectory => Path.Combine(
        ResolveBaseFolder(Environment.SpecialFolder.LocalApplicationData),
        "Flux", "covers");

    // Старое расположение (только громкость) для миграции.
    public static string LegacySettingsFile { get; } = Path.Combine(
        ResolveBaseFolder(Environment.SpecialFolder.LocalApplicationData),
        "Flux", "settings.json");

    private static string ResolveBaseFolder(Environment.SpecialFolder folder)
    {
        var path = Environment.GetFolderPath(folder);
        if (!string.IsNullOrWhiteSpace(path)) return path;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home)) return home;

        return Path.GetTempPath();
    }

    public static void EnsureConfigDirectory() => Directory.CreateDirectory(ConfigDirectory);
}
