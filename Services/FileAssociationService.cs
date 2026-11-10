using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Flux.Services;

public enum AssociationResult
{
    Success,
    Failed,
    Unsupported
}

// Регистрация Flux как обработчика аудиофайлов. Работает без прав администратора.
public static class FileAssociationService
{
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mp3"] = "audio/mpeg",
        ["flac"] = "audio/flac",
        ["wav"] = "audio/x-wav",
        ["ogg"] = "audio/ogg",
        ["m4a"] = "audio/mp4",
        ["aac"] = "audio/aac",
        ["wma"] = "audio/x-ms-wma",
        ["opus"] = "audio/ogg"
    };

    public static AssociationResult Apply(IReadOnlyList<string> extensions)
    {
        if (OperatingSystem.IsWindows()) return ApplyWindows(extensions);
        if (OperatingSystem.IsLinux()) return ApplyLinux(extensions);
        return AssociationResult.Unsupported;
    }

    [SupportedOSPlatform("windows")]
    private static AssociationResult ApplyWindows(IReadOnlyList<string> extensions)
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrEmpty(executable)) return AssociationResult.Failed;

            foreach (var extension in extensions)
            {
                var progId = $"Flux.{extension}";

                using (var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\.{extension}"))
                    extensionKey?.SetValue(string.Empty, progId);

                using (var applicationKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
                    applicationKey?.SetValue(string.Empty, "Flux audio player");

                using (var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}\DefaultIcon"))
                    iconKey?.SetValue(string.Empty, $"\"{executable}\",0");

                using (var commandKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}\shell\open\command"))
                    commandKey?.SetValue(string.Empty, $"\"{executable}\" \"%1\"");
            }

            return AssociationResult.Success;
        }
        catch
        {
            return AssociationResult.Failed;
        }
    }

    [SupportedOSPlatform("linux")]
    private static AssociationResult ApplyLinux(IReadOnlyList<string> extensions)
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrEmpty(executable)) return AssociationResult.Failed;

            var mimeTypes = extensions
                .Select(extension => MimeTypes.TryGetValue(extension, out var mime) ? mime : null)
                .Where(mime => mime is not null)
                .Distinct()
                .ToArray();

            if (mimeTypes.Length == 0) return AssociationResult.Failed;

            var applicationsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "applications");

            Directory.CreateDirectory(applicationsDirectory);

            var desktopFile = Path.Combine(applicationsDirectory, "flux.desktop");
            var content =
                "[Desktop Entry]\n" +
                "Type=Application\n" +
                "Name=Flux\n" +
                "Comment=Flux audio player\n" +
                $"Exec=\"{executable}\" %f\n" +
                "Icon=flux\n" +
                "Terminal=false\n" +
                "Categories=AudioVideo;Audio;Player;\n" +
                $"MimeType={string.Join(";", mimeTypes)};\n";

            File.WriteAllText(desktopFile, content);

            var allApplied = true;
            foreach (var mimeType in mimeTypes)
                allApplied &= Run("xdg-mime", $"default flux.desktop {mimeType}");

            // Не обязательно: обновляет кэш MIME, но во многих системах утилиты нет.
            Run("update-desktop-database", applicationsDirectory);

            return allApplied ? AssociationResult.Success : AssociationResult.Failed;
        }
        catch
        {
            return AssociationResult.Failed;
        }
    }

    private static bool Run(string fileName, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is null) return false;

            process.WaitForExit(3000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
