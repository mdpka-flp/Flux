using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Flux.Services;
using Flux.ViewModels;
using System;
using System.Linq;

namespace Flux.Views;

public partial class MainWindow : Window
{
    private (bool Mica, int Transparency, string Color, string Accent, string Text)? _appliedAppearance;

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => ApplyAppearance();
        SettingsService.Changed += ApplyAppearance;
        Closed += (_, _) =>
        {
            SettingsService.Changed -= ApplyAppearance;
            _layoutRefreshTimer.Stop();
            _layoutRefreshTimer.Tick -= OnLayoutRefreshTick;
        };

        _layoutRefreshTimer.Interval = LayoutRefreshDelay;
        _layoutRefreshTimer.Tick += OnLayoutRefreshTick;

        _volumeHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _volumeHideTimer.Tick += (_, _) => HideVolumeSliderIfIdle();

        VolumeSlider.Opacity = 0;
        VolumeSlider.IsHitTestVisible = false;

        VolumeContainer.PointerEntered += OnVolumePointerEntered;
        VolumeContainer.PointerMoved += OnVolumePointerMoved;
        VolumeContainer.PointerExited += OnVolumePointerExited;
        VolumeSlider.PointerPressed += OnVolumeSliderPointerPressed;
        VolumeSlider.PointerReleased += OnVolumeSliderPointerReleased;
        VolumeSlider.PointerCaptureLost += OnVolumeSliderPointerCaptureLost;

        // К моменту Opened уже известен фактический ActualTransparencyLevel,
        // поэтому пересчитываем режим и фон ещё раз.
        Opened += (_, _) =>
        {
            ApplyAppearance();
            System.IO.File.AppendAllText("/tmp/opencode/layout.log", $"{DateTime.Now:HH:mm:ss.fff} opened\n");
            RefreshLayoutSoon();
        };

        // Hyprland иногда показывает устаревший буфер после переноса окна на
        // другое рабочее пространство. Принудительный проход компоновки с
        // задержкой (пока идёт анимация) заставляет поверхность перерисоваться.
        Activated += (_, _) => { System.IO.File.AppendAllText("/tmp/opencode/layout.log", $"{DateTime.Now:HH:mm:ss.fff} activated\n"); RefreshLayoutSoon(); };
    }

    // Задержка даёт композитору время применить анимацию воркспейса.
    private static readonly TimeSpan LayoutRefreshDelay = TimeSpan.FromMilliseconds(70);
    private readonly DispatcherTimer _layoutRefreshTimer = new() { IsEnabled = false };

    private readonly DispatcherTimer _volumeHideTimer;
    private bool _volumePointerOver;
    private bool _volumeDragging;
    private Point? _volumeLastPointerPosition;

    // Класс платформенного окна называется просто WindowImpl, поэтому
    // backend определяется по полному имени типа (пространству имён).
    private bool IsWayland =>
        PlatformImpl?.GetType().FullName?.Contains("Wayland", StringComparison.Ordinal) == true;

    private bool IsX11 =>
        PlatformImpl?.GetType().FullName?.Contains("X11", StringComparison.Ordinal) == true;

    private void RefreshLayoutSoon()
    {
        // Не плодим несколько проходов: повторные активации (например, при
        // быстром переключении воркспейсов) только перезапускают таймер.
        _layoutRefreshTimer.Stop();
        _layoutRefreshTimer.Start();
    }

    private void OnLayoutRefreshTick(object? sender, EventArgs e)
    {
        _layoutRefreshTimer.IsEnabled = false;
        RefreshLayout();
    }

    // Имитирует ручной ресайз изнутри: сбрасывает измерение и раскладку окна
    // с потомками. Width/Height не меняются, поэтому тайлинг Hyprland цел.
    private void RefreshLayout()
    {
        System.IO.File.AppendAllText("/tmp/opencode/layout.log", $"{DateTime.Now:HH:mm:ss.fff} refresh\n");
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();

        // Принудительно запускаем задачи отрисовки в очереди диспетчера
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
    }

    private void OnVolumePointerEntered(object? sender, PointerEventArgs e)
    {
        _volumePointerOver = true;
        _volumeLastPointerPosition = e.GetPosition(this);
        _volumeHideTimer.Stop();
        ShowVolumeSlider();
    }

    private void OnVolumePointerMoved(object? sender, PointerEventArgs e)
    {
        _volumeLastPointerPosition = e.GetPosition(this);
    }

    private void OnVolumePointerExited(object? sender, PointerEventArgs e)
    {
        _volumeLastPointerPosition = e.GetPosition(this);
        _volumePointerOver = false;
        // Во время перетаскивания слайдер не прячем: указатель может выйти
        // за пределы контейнера, но ползунок остаётся захваченным.
        ScheduleVolumeHide();
    }

    private void OnVolumeSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _volumeDragging = true;
        _volumeHideTimer.Stop();
        ShowVolumeSlider();
    }

    private void OnVolumeSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _volumeDragging = false;
        ScheduleVolumeHide();
    }

    private void OnVolumeSliderPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _volumeDragging = false;
        ScheduleVolumeHide();
    }

    private void ScheduleVolumeHide()
    {
        if (_volumePointerOver || _volumeDragging) return;

        _volumeHideTimer.Stop();
        _volumeHideTimer.Start();
    }

    private void HideVolumeSliderIfIdle()
    {
        // Сверяемся с реальным состоянием указателя: события выхода могут
        // не прийти, пока слайдер удерживает pointer capture. Позицию
        // переводим в область контейнера, а не через IsPointerOver:
        // контейнер — предок мута, и его состояние путается с появлением
        // слайдера прямо под курсором.
        var pointer = _volumeLastPointerPosition is { } last
            ? VolumeContainer.TranslatePoint(last, this)
            : null;
        var pointerStillHere = _volumePointerOver || _volumeDragging ||
                               (pointer is { } p && new Rect(VolumeContainer.Bounds.Size).Contains(p));

        _volumeHideTimer.Stop();
        if (pointerStillHere) return;

        HideVolumeSlider();
    }

    private void ShowVolumeSlider()
    {
        VolumeSlider.Opacity = 1;
        VolumeSlider.IsHitTestVisible = true;
    }

    private void HideVolumeSlider()
    {
        VolumeSlider.Opacity = 0;
        VolumeSlider.IsHitTestVisible = false;
    }

    private void ApplyAppearance()
    {
        var settings = SettingsService.Current;

        var state = (settings.MicaEnabled, settings.BackgroundTransparency,
            settings.BackgroundColor, settings.AccentColor, settings.TextColor);
        if (_appliedAppearance == state) return;

        // Бэкенд известен только после создания платформенного окна
        // (PlatformImpl в конструкторе пуст), поэтому режим прозрачности
        // подбираем по месту, а не кэшируем.
        var color = ParseColor(settings.BackgroundColor, Color.Parse("#111111"));
        ApplyPalette(settings);

        ApplyTransparencyHint(settings);
        ApplyBackground(settings, color);

        _appliedAppearance = state;
    }

    // Подбирает способ прозрачности под платформу: Mica/Blur на Windows,
    // Blur->Transparent на X11 (блюр делает композитор, например picom),
    // Transparent на Wayland (размытие настраивается в самом композиторе).
    private void ApplyTransparencyHint(AppSettings settings)
    {
        var hint = settings.MicaEnabled ? ResolveTransparencyHint() : NoneHint;

        if (!hint.SequenceEqual(TransparencyLevelHint))
            TransparencyLevelHint = hint;
    }

    private static readonly WindowTransparencyLevel[] NoneHint =
        { WindowTransparencyLevel.None };

    private WindowTransparencyLevel[] ResolveTransparencyHint()
    {
        if (IsWayland)
        {
            // На Wayland размытие контролирует композитор (например, Hyprland),
            // а приложению нужно лишь отдать альфа-канал, чтобы буфер поверхности
            // не ломался.
            return new[] { WindowTransparencyLevel.Transparent };
        }

        if (IsX11)
        {
            // С композитором (picom и т. п.) получим блюр, без него — обычную
            // прозрачность.
            return new[] { WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent };
        }

        // Windows и остальные бэкенды: Mica, при отсутствии — AcrylicBlur.
        return new[]
        {
            WindowTransparencyLevel.Mica,
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.Blur,
            WindowTransparencyLevel.Transparent
        };
    }

    private void ApplyBackground(AppSettings settings, Color color)
    {
        System.IO.File.AppendAllText("/tmp/opencode/bg.log", $"hint={TransparencyLevelHint.Count}:{string.Join(",", TransparencyLevelHint)} actual={ActualTransparencyLevel} mica={settings.MicaEnabled} sizes={string.Join(";", SizeToContent.ToString())}\n");
        if (!settings.MicaEnabled)
        {
            Background = new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B));
            return;
        }

        // Прозрачный фон имеет смысл, только если платформа его поддержала
        // (Mica, Blur или Transparent). При None окно остаётся непрозрачным.
        var supported = ActualTransparencyLevel != WindowTransparencyLevel.None;

        var transparency = Math.Clamp(settings.BackgroundTransparency, 0, 100);
        var alpha = supported
            ? (byte)Math.Round(255 * (100 - transparency) / 100.0)
            : (byte)255;

        Background = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        System.IO.File.AppendAllText("/tmp/opencode/bg.log", $"  -> alpha={alpha}\n");
    }

    private static void ApplyPalette(AppSettings settings)
    {
        if (Application.Current is not { } app) return;

        var accent = ParseColor(settings.AccentColor, Color.Parse("#0078D7"));
        var text = ParseColor(settings.TextColor, Colors.White);
        var background = ParseColor(settings.BackgroundColor, Color.Parse("#111111"));

        // Те же шаги, что и в Avalonia.Themes.Fluent.Accents.SystemAccentColors.
        var hsl = accent.ToHsl();

        Color Shade(double delta) =>
            new HslColor(hsl.A, hsl.H, hsl.S, Math.Clamp(hsl.L + delta, 0, 1)).ToRgb();

        var resources = app.Resources;
        resources["SystemAccentColor"] = Color.FromRgb(accent.R, accent.G, accent.B);
        resources["SystemAccentColorDark1"] = Shade(-28.5 / 255);
        resources["SystemAccentColorDark2"] = Shade(-49.0 / 255);
        resources["SystemAccentColorDark3"] = Shade(-74.5 / 255);
        resources["SystemAccentColorLight1"] = Shade(39.0 / 255);
        resources["SystemAccentColorLight2"] = Shade(70.0 / 255);
        resources["SystemAccentColorLight3"] = Shade(103.0 / 255);

        resources["FluxAccentBrush"] = new SolidColorBrush(accent);
        resources["FluxTextBrush"] = new SolidColorBrush(text);
        resources["FluxSecondaryTextBrush"] =
            new SolidColorBrush(Color.FromArgb(0xAA, text.R, text.G, text.B));
        resources["FluxDividerBrush"] =
            new SolidColorBrush(Color.FromArgb(0x33, text.R, text.G, text.B));

        resources["FluxPanelBrush"] = new SolidColorBrush(
            IsLight(background)
                ? Blend(background, Colors.Black, 0.10)
                : Blend(background, Colors.White, 0.08));
    }

    private static Color ParseColor(string? value, Color fallback) =>
        Color.TryParse(value, out var color) ? color : fallback;

    private static bool IsLight(Color color) =>
        0.299 * color.R + 0.587 * color.G + 0.114 * color.B > 150;

    private static Color Blend(Color from, Color to, double amount) =>
        Color.FromRgb(
            (byte)Math.Round(from.R + (to.R - from.R) * amount),
            (byte)Math.Round(from.G + (to.G - from.G) * amount),
            (byte)Math.Round(from.B + (to.B - from.B) * amount));

    private void OnTimelinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.IsSeeking = true;
    }

    private void OnTimelinePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.IsSeeking = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            base.OnKeyDown(e);
            return;
        }

        bool isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;

        // В настройках не перехватываем управление плеером; Esc возвращает назад.
        if (vm.IsSettingsOpen)
        {
            if (e.Key == Key.Escape)
            {
                if (vm.Settings.IsColorEditorOpen)
                    vm.Settings.CancelColorEditorCommand.Execute(null);
                else
                    vm.CloseSettingsCommand.Execute(null);

                e.Handled = true;
            }

            base.OnKeyDown(e);
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
                vm.PlayPauseCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up:
                vm.Volume = Math.Min(100, vm.Volume + 5);
                e.Handled = true;
                break;

            case Key.Down:
                vm.Volume = Math.Max(0, vm.Volume - 5);
                e.Handled = true;
                break;

            case Key.Left:
                if (isCtrl) vm.PreviousCommand.Execute(null); // Ctrl + Left = Назад
                else vm.RewindCommand.Execute(null);          // Left = Перемотка -5с
                e.Handled = true;
                break;

            case Key.Right:
                if (isCtrl) vm.NextCommand.Execute(null);     // Ctrl + Right = Вперед
                else vm.ForwardCommand.Execute(null);         // Right = Перемотка +5с
                e.Handled = true;
                break;
        }

        base.OnKeyDown(e);
    }
}