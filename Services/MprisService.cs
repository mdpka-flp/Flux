using System;
using System.Diagnostics;

namespace Flux.Services;

public static class MprisService
{
    private const string ServiceName = "org.mpris.MediaPlayer2.Flux";
    private const string ObjectPath = "/org/mpris/MediaPlayer2";
    private const string InterfaceName = "org.mpris.MediaPlayer2.Player";

    private static bool _isRegistered;

    public static void Start()
    {
        if (!OperatingSystem.IsLinux() || _isRegistered) return;

        RunCommand("busctl", $"--user --nopager call org.freedesktop.DBus /org/freedesktop/DBus org.freedesktop.DBus.RequestName string:{ServiceName} uint32:4");
        _isRegistered = true;
    }

    public static void RegisterStatus(string status)
    {
        if (!OperatingSystem.IsLinux()) return;
        Start(); // Подстраховка инициализации

        string lowerStatus = status.ToLowerInvariant();
        if (lowerStatus == "stop") lowerStatus = "stopped";

        string args = $"--user emit-signal {ObjectPath} org.freedesktop.DBus.Properties PropertiesChanged " +
                      $"string:{InterfaceName} " +
                      $"dict:string:variant:\"PlaybackStatus\",string:\"{status}\" " +
                      "array:string:";
        
        RunCommand("busctl", args);
    }

    public static void UpdateMetadata(string title, string artist, string albumArtPath)
    {
        if (!OperatingSystem.IsLinux()) return;
        Start();

        string safeTitle = title.Replace("\"", "\\\"");
        string safeArtist = artist.Replace("\"", "\\\"");
        string artUrl = !string.IsNullOrEmpty(albumArtPath) ? $"file://{albumArtPath}" : "";

        string args = $"--user emit-signal {ObjectPath} org.freedesktop.DBus.Properties PropertiesChanged " +
                      $"string:{InterfaceName} " +
                      $"dict:string:variant:\"Metadata\",dict:string:variant:\"xesam:title\",string:\"{safeTitle}\",\"xesam:artist\",string:\"{safeArtist}\",\"mpris:artUrl\",string:\"{artUrl}\" " +
                      "array:string:";

        RunCommand("busctl", args);
    }

    private static void RunCommand(string command, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(startInfo);
            process?.WaitForExit(30); 
        }
        catch { }
    }
}
