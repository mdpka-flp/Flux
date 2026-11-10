using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Platform;

namespace Flux.Services;

public sealed record LanguageOption(string Code, string Name)
{
    public override string ToString() => Name;
}

// Локализация интерфейса. Тексты берутся из localization.json в папке конфигурации:
// сначала список языков, затем для каждого ключа массив переводов в том же порядке.
// Чтобы добавить язык, достаточно дописать его в список и добавить значение в каждый массив.
public sealed class LocalizationService : INotifyPropertyChanged
{
    private const string EmbeddedLocalizationUri = "avares://Flux/Assets/localization.json";

    private static readonly Lazy<LocalizationService> LazyInstance = new(() => new LocalizationService());

    public static LocalizationService Instance => LazyInstance.Value;

    private readonly Dictionary<string, string[]> _strings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> _fallbackStrings = new(StringComparer.Ordinal);
    private readonly List<LanguageOption> _languages = new();
    private int _languageIndex;

    private LocalizationService() { }

    public IReadOnlyList<LanguageOption> Languages => _languages;

    public string LanguageCode =>
        _languages.Count == 0 ? "en" : _languages[Math.Clamp(_languageIndex, 0, _languages.Count - 1)].Code;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Load(string? preferredLanguage)
    {
        _languages.Clear();
        _strings.Clear();
        _fallbackStrings.Clear();

        try
        {
            Parse(ReadConfigFile(), _strings, _languages);
        }
        catch
        {
            // Повреждённый пользовательский файл: ниже подхватятся встроенные тексты.
        }

        // Встроенный файл дополняет ключи, которых ещё нет в старом конфиге
        // (например, после обновления приложения с новыми строками интерфейса).
        try
        {
            var fallbackLanguages = new List<LanguageOption>();
            Parse(ReadEmbeddedConfig(), _fallbackStrings, fallbackLanguages);

            if (_languages.Count == 0)
                _languages.AddRange(fallbackLanguages);
        }
        catch
        {
            // Без встроенного файла вместо текстов будут ключи — приложение не падает.
        }

        if (_languages.Count == 0)
            _languages.Add(new LanguageOption("en", "English"));

        SetLanguage(preferredLanguage, notify: false);
        RaiseAllChanged();
    }

    public void SetLanguage(string? code)
    {
        SetLanguage(code, notify: true);
    }

    private void SetLanguage(string? code, bool notify)
    {
        var index = _languages.FindIndex(
            l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

        _languageIndex = index >= 0 ? index : 0;

        if (notify) RaiseAllChanged();
    }

    private static readonly string[] LocalizedPropertyNames =
    {
        nameof(PlayerNothingSelected), nameof(PlayerUnknownArtist), nameof(PlayerUnknownFormat),
        nameof(TooltipPrevious), nameof(TooltipRewind), nameof(TooltipStop), nameof(TooltipPlayPause),
        nameof(TooltipForward), nameof(TooltipNext), nameof(TooltipOpenCover), nameof(TooltipSettings),
        nameof(TooltipMute),
        nameof(DialogOpenAudio), nameof(DialogAudioFiles), nameof(DialogAllFiles),
        nameof(SettingsTitle), nameof(SettingsBack), nameof(SettingsLanguage), nameof(SettingsLanguageHint),
        nameof(SettingsExtensions), nameof(SettingsExtensionsHint), nameof(SettingsApplyAssociations),
        nameof(SettingsAssociationsOk), nameof(SettingsAssociationsFailed), nameof(SettingsAssociationsUnsupported),
        nameof(SettingsAtLeastOne), nameof(SettingsAppearance), nameof(SettingsMica),
        nameof(SettingsTransparency), nameof(SettingsTransparencyHint),
        nameof(SettingsMainColor), nameof(SettingsAccentColor), nameof(SettingsTextColor),
        nameof(SettingsColorEditor), nameof(SettingsColorHex),
        nameof(SettingsRed), nameof(SettingsGreen), nameof(SettingsBlue),
        nameof(SettingsApply), nameof(SettingsCancel),
        nameof(SettingsSaved)
    };

    private void RaiseAllChanged()
    {
        var handler = PropertyChanged;
        if (handler is null) return;

        // Явно уведомляем каждое свойство, затем пустым именем — "все изменились".
        foreach (var propertyName in LocalizedPropertyNames)
            handler(this, new PropertyChangedEventArgs(propertyName));

        handler(this, new PropertyChangedEventArgs(string.Empty));
    }

    private string Get(string key)
    {
        if (!_strings.TryGetValue(key, out var values) || values.Length == 0)
        {
            if (!_fallbackStrings.TryGetValue(key, out values) || values.Length == 0)
                return key;
        }

        var index = Math.Clamp(_languageIndex, 0, values.Length - 1);
        var value = values[index];

        if (string.IsNullOrEmpty(value))
            value = values.FirstOrDefault(v => !string.IsNullOrEmpty(v));

        return string.IsNullOrEmpty(value) ? key : value;
    }

    private static string ReadConfigFile()
    {
        var path = AppPaths.LocalizationFile;

        if (!File.Exists(path))
        {
            var defaults = ReadEmbeddedConfig();
            try
            {
                AppPaths.EnsureConfigDirectory();
                File.WriteAllText(path, defaults);
            }
            catch { }
            return defaults;
        }

        return File.ReadAllText(path);
    }

    private static string ReadEmbeddedConfig()
    {
        using var stream = AssetLoader.Open(new Uri(EmbeddedLocalizationUri));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void Parse(string json, Dictionary<string, string[]> strings, List<LanguageOption> languages)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("languages", out var languagesElement) && languagesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var language in languagesElement.EnumerateArray())
            {
                var code = language.TryGetProperty("code", out var codeElement)
                    ? codeElement.GetString()
                    : null;
                var name = language.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString()
                    : null;

                if (!string.IsNullOrWhiteSpace(code))
                    languages.Add(new LanguageOption(code!, string.IsNullOrWhiteSpace(name) ? code! : name!));
            }
        }

        if (root.TryGetProperty("strings", out var stringsElement) && stringsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in stringsElement.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Array) continue;

                var values = entry.Value.EnumerateArray()
                    .Select(v => v.GetString() ?? string.Empty)
                    .ToArray();

                strings[entry.Name] = values;
            }
        }
    }

    public string PlayerNothingSelected => Get("player.nothing_selected");
    public string PlayerUnknownArtist => Get("player.unknown_artist");
    public string PlayerUnknownFormat => Get("player.unknown_format");

    public string TooltipPrevious => Get("tooltip.previous");
    public string TooltipRewind => Get("tooltip.rewind");
    public string TooltipStop => Get("tooltip.stop");
    public string TooltipPlayPause => Get("tooltip.play_pause");
    public string TooltipForward => Get("tooltip.forward");
    public string TooltipNext => Get("tooltip.next");
    public string TooltipOpenCover => Get("tooltip.open_cover");
    public string TooltipSettings => Get("tooltip.settings");
    public string TooltipMute => Get("tooltip.mute");

    public string DialogOpenAudio => Get("dialog.open_audio");
    public string DialogAudioFiles => Get("dialog.audio_files");
    public string DialogAllFiles => Get("dialog.all_files");

    public string SettingsTitle => Get("settings.title");
    public string SettingsBack => Get("settings.back");
    public string SettingsLanguage => Get("settings.language");
    public string SettingsLanguageHint => Get("settings.language_hint");
    public string SettingsExtensions => Get("settings.extensions");
    public string SettingsExtensionsHint => Get("settings.extensions_hint");
    public string SettingsApplyAssociations => Get("settings.apply_associations");
    public string SettingsAssociationsOk => Get("settings.associations_ok");
    public string SettingsAssociationsFailed => Get("settings.associations_failed");
    public string SettingsAssociationsUnsupported => Get("settings.associations_unsupported");
    public string SettingsAtLeastOne => Get("settings.at_least_one");
    public string SettingsAppearance => Get("settings.appearance");
    public string SettingsMica => Get("settings.mica");
    public string SettingsTransparency => Get("settings.transparency");
    public string SettingsTransparencyHint => Get("settings.transparency_hint");
    public string SettingsMainColor => Get("settings.main_color");
    public string SettingsAccentColor => Get("settings.accent_color");
    public string SettingsTextColor => Get("settings.text_color");
    public string SettingsColorEditor => Get("settings.color_editor");
    public string SettingsColorHex => Get("settings.color_hex");
    public string SettingsRed => Get("settings.red");
    public string SettingsGreen => Get("settings.green");
    public string SettingsBlue => Get("settings.blue");
    public string SettingsApply => Get("settings.apply");
    public string SettingsCancel => Get("settings.cancel");
    public string SettingsSaved => Get("settings.saved");
}
