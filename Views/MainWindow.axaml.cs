using Avalonia.Controls;
using Avalonia.Input;
using Flux.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Flux.Views;

public partial class MainWindow : Window
{
    private static readonly string[] AudioExtensions =
        { ".mp3", ".flac", ".wav", ".ogg", ".m4a", ".aac", ".wma", ".opus" };

    public MainWindow()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);
    }

    private void OnTimelinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.IsSeeking = true;
    }

    private void OnTimelinePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.IsSeeking = false;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is not { } files) return;

        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!AudioExtensions.Contains(ext)) return;

        if (DataContext is MainViewModel vm)
            vm.PlayFileCommand.Execute(path);
    }
}