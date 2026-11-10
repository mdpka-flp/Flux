using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flux.Services;

namespace Flux.ViewModels;

public partial class ExtensionOption : ObservableObject
{
    private readonly Action<ExtensionOption> _changed;

    public string Extension { get; }

    public string DisplayName => "." + Extension;

    [ObservableProperty]
    private bool _isEnabled;

    public ExtensionOption(string extension, bool isEnabled, Action<ExtensionOption> changed)
    {
        Extension = extension;
        _isEnabled = isEnabled;
        _changed = changed;
    }

    partial void OnIsEnabledChanged(bool value) => _changed(this);
}

public enum ColorRole
{
    Main,
    Accent,
    Text
}

public partial class PaletteColor : ObservableObject
{
    public string Hex { get; }

    public ColorRole Role { get; }

    // Первый кружок палитры: открывает редактор произвольного цвета.
    public bool IsCustom { get; }

    public IBrush Swatch { get; }

    public ICommand SelectCommand { get; }

    [ObservableProperty]
    private IBrush _borderBrush = Brushes.Transparent;

    public PaletteColor(string hex, ColorRole role, ICommand selectCommand, bool isCustom = false)
    {
        Hex = hex;
        Role = role;
        SelectCommand = selectCommand;
        IsCustom = isCustom;
        Swatch = isCustom ? CreateRainbowBrush() : new SolidColorBrush(Color.Parse(hex));
    }

    private static IBrush CreateRainbowBrush()
    {
        var brush = new ConicGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse("#FF0000"), 0.0),
                new GradientStop(Color.Parse("#FFFF00"), 1.0 / 6),
                new GradientStop(Color.Parse("#00FF00"), 2.0 / 6),
                new GradientStop(Color.Parse("#00FFFF"), 3.0 / 6),
                new GradientStop(Color.Parse("#0000FF"), 4.0 / 6),
                new GradientStop(Color.Parse("#FF00FF"), 5.0 / 6),
                new GradientStop(Color.Parse("#FF0000"), 1.0)
            }
        };

        return brush;
    }
}

public partial class SettingsViewModel : ViewModelBase
{
    private static readonly string[] PaletteHexes =
    {
        "#111111", "#1E1E2E", "#101B2D", "#1B2A1B",
        "#2A1B1B", "#2B1B3A", "#3A2B10", "#0E2A2A",
        "#3949AB", "#7B1FA2", "#C62828", "#2E7D32"
    };

    private static readonly string[] TextPaletteHexes = { "#FFFFFF", "#000000" };

    private readonly Action _close;

    private bool _suppressEditorSync;
    private ColorRole _editingRole;
    private Color _editingColor = Colors.White;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private bool _micaEnabled;

    [ObservableProperty]
    private double _backgroundTransparency;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isColorEditorOpen;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editorHex = "#FFFFFF";

    [ObservableProperty]
    private double _editorRed = 255;

    [ObservableProperty]
    private double _editorGreen = 255;

    [ObservableProperty]
    private double _editorBlue = 255;

    [ObservableProperty]
    private IBrush _editorPreviewBrush = Brushes.White;

    [ObservableProperty]
    private Color _editorColor = Colors.White;

    public SettingsViewModel(Action close)
    {
        _close = close;

        var settings = SettingsService.Current;

        var enabledExtensions = settings.EnabledExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in AppSettings.SupportedExtensions)
            Extensions.Add(new ExtensionOption(extension, enabledExtensions.Contains(extension), OnExtensionChanged));

        Languages = LocalizationService.Instance.Languages;
        _selectedLanguage = Languages.FirstOrDefault(
            language => string.Equals(language.Code, settings.Language, StringComparison.OrdinalIgnoreCase))
            ?? Languages.FirstOrDefault();

        _micaEnabled = settings.MicaEnabled;
        _backgroundTransparency = settings.BackgroundTransparency;

        BuildPalette(MainPalette, ColorRole.Main, PaletteHexes);
        BuildPalette(AccentPalette, ColorRole.Accent, PaletteHexes);
        BuildPalette(TextPalette, ColorRole.Text, TextPaletteHexes);

        RefreshPaletteSelection();
    }

    public LocalizationService Loc => LocalizationService.Instance;

    public IReadOnlyList<LanguageOption> Languages { get; }

    public ObservableCollection<ExtensionOption> Extensions { get; } = new();

    public ObservableCollection<PaletteColor> MainPalette { get; } = new();

    public ObservableCollection<PaletteColor> AccentPalette { get; } = new();

    public ObservableCollection<PaletteColor> TextPalette { get; } = new();

    [RelayCommand]
    private void Close() => _close();

    [RelayCommand]
    private void SelectColor(PaletteColor? color)
    {
        if (color is null) return;

        if (color.IsCustom)
        {
            OpenColorEditor(color.Role);
            return;
        }

        SetRoleColor(color.Role, color.Hex);
    }

    [RelayCommand]
    private void ApplyColorEditor()
    {
        SetRoleColor(_editingRole, ToHex(_editingColor));
        IsColorEditorOpen = false;
    }

    [RelayCommand]
    private void CancelColorEditor() => IsColorEditorOpen = false;

    [RelayCommand]
    private async Task ApplyAssociations()
    {
        var extensions = SettingsService.Current.EnabledExtensions;
        var result = await Task.Run(() => FileAssociationService.Apply(extensions));

        StatusText = result switch
        {
            AssociationResult.Success => Loc.SettingsAssociationsOk,
            AssociationResult.Unsupported => Loc.SettingsAssociationsUnsupported,
            _ => Loc.SettingsAssociationsFailed
        };
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null) return;

        var settings = SettingsService.Current;
        if (string.Equals(settings.Language, value.Code, StringComparison.OrdinalIgnoreCase)) return;

        settings.Language = value.Code;
        SettingsService.Save();

        StatusText = string.Empty;
        LocalizationService.Instance.SetLanguage(value.Code);
    }

    partial void OnMicaEnabledChanged(bool value)
    {
        SettingsService.Current.MicaEnabled = value;
        SettingsService.Save();
    }

    partial void OnBackgroundTransparencyChanged(double value)
    {
        SettingsService.Current.BackgroundTransparency = (int)Math.Round(value);
        SettingsService.Save();
    }

    partial void OnEditorHexChanged(string value)
    {
        if (_suppressEditorSync) return;
        if (!TryParseHex(value, out var color)) return;

        _editingColor = Color.FromRgb(color.R, color.G, color.B);

        _suppressEditorSync = true;
        EditorColor = _editingColor;
        EditorRed = _editingColor.R;
        EditorGreen = _editingColor.G;
        EditorBlue = _editingColor.B;
        EditorPreviewBrush = new SolidColorBrush(_editingColor);
        _suppressEditorSync = false;
    }

    partial void OnEditorRedChanged(double value) => SyncEditorFromSliders();

    partial void OnEditorGreenChanged(double value) => SyncEditorFromSliders();

    partial void OnEditorBlueChanged(double value) => SyncEditorFromSliders();

    private void SyncEditorFromSliders()
    {
        if (_suppressEditorSync) return;

        _editingColor = Color.FromRgb(
            ToByte(EditorRed),
            ToByte(EditorGreen),
            ToByte(EditorBlue));

        _suppressEditorSync = true;
        EditorColor = _editingColor;
        EditorHex = ToHex(_editingColor);
        EditorPreviewBrush = new SolidColorBrush(_editingColor);
        _suppressEditorSync = false;
    }

    partial void OnEditorColorChanged(Color value)
    {
        if (_suppressEditorSync) return;

        _editingColor = value;

        _suppressEditorSync = true;
        EditorHex = ToHex(value);
        EditorRed = value.R;
        EditorGreen = value.G;
        EditorBlue = value.B;
        EditorPreviewBrush = new SolidColorBrush(value);
        _suppressEditorSync = false;
    }

    private void OpenColorEditor(ColorRole role)
    {
        _editingRole = role;
        _editingColor = ParseColor(GetRoleColor(role));

        _suppressEditorSync = true;
        EditorColor = _editingColor;
        EditorHex = ToHex(_editingColor);
        EditorRed = _editingColor.R;
        EditorGreen = _editingColor.G;
        EditorBlue = _editingColor.B;
        EditorPreviewBrush = new SolidColorBrush(_editingColor);
        _suppressEditorSync = false;

        EditorTitle = $"{Loc.SettingsColorEditor} — {GetRoleName(role)}";
        IsColorEditorOpen = true;
    }

    private void BuildPalette(ObservableCollection<PaletteColor> target, ColorRole role, IEnumerable<string> hexes)
    {
        target.Add(new PaletteColor(GetRoleColor(role), role, SelectColorCommand, isCustom: true));

        foreach (var hex in hexes)
            target.Add(new PaletteColor(hex, role, SelectColorCommand));
    }

    private void SetRoleColor(ColorRole role, string hex)
    {
        var settings = SettingsService.Current;

        switch (role)
        {
            case ColorRole.Accent:
                settings.AccentColor = hex;
                break;
            case ColorRole.Text:
                settings.TextColor = hex;
                break;
            default:
                settings.BackgroundColor = hex;
                break;
        }

        SettingsService.Save();
        RefreshPaletteSelection();
    }

    private static string GetRoleColor(ColorRole role) => role switch
    {
        ColorRole.Accent => SettingsService.Current.AccentColor,
        ColorRole.Text => SettingsService.Current.TextColor,
        _ => SettingsService.Current.BackgroundColor
    };

    private string GetRoleName(ColorRole role) => role switch
    {
        ColorRole.Accent => Loc.SettingsAccentColor,
        ColorRole.Text => Loc.SettingsTextColor,
        _ => Loc.SettingsMainColor
    };

    private void RefreshPaletteSelection()
    {
        RefreshPalette(MainPalette, SettingsService.Current.BackgroundColor);
        RefreshPalette(AccentPalette, SettingsService.Current.AccentColor);
        RefreshPalette(TextPalette, SettingsService.Current.TextColor);
    }

    private static void RefreshPalette(IEnumerable<PaletteColor> palette, string current)
    {
        var colors = palette.ToList();
        var currentIsStandard = colors
            .Where(color => !color.IsCustom)
            .Any(color => string.Equals(color.Hex, current, StringComparison.OrdinalIgnoreCase));

        foreach (var color in colors)
        {
            var selected = color.IsCustom
                ? !currentIsStandard
                : string.Equals(color.Hex, current, StringComparison.OrdinalIgnoreCase);

            color.BorderBrush = selected ? GetSelectionBrush(color) : Brushes.Transparent;
        }
    }

    private static IBrush GetSelectionBrush(PaletteColor color)
    {
        if (color.IsCustom) return Brushes.White;

        var swatch = Color.Parse(color.Hex);
        var luminance = 0.299 * swatch.R + 0.587 * swatch.G + 0.114 * swatch.B;

        // На светлом кружке белая обводка не видна — используем тёмную.
        return luminance > 150 ? Brushes.Black : Brushes.White;
    }

    private void OnExtensionChanged(ExtensionOption option)
    {
        if (!option.IsEnabled && Extensions.All(extension => !extension.IsEnabled))
        {
            // Нельзя отключить все форматы — иначе плеер перестанет открывать файлы.
            option.IsEnabled = true;
            StatusText = Loc.SettingsAtLeastOne;
            return;
        }

        SettingsService.Current.Extensions = Extensions
            .Where(extension => extension.IsEnabled)
            .Select(extension => extension.Extension)
            .ToList();

        SettingsService.Save();

        if (StatusText == Loc.SettingsAtLeastOne)
            StatusText = string.Empty;
    }

    private static Color ParseColor(string? value) =>
        Color.TryParse(value, out var color) ? color : Colors.White;

    private static bool TryParseHex(string? value, out Color color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        if (!text.StartsWith('#')) text = "#" + text;

        return Color.TryParse(text, out color);
    }

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
