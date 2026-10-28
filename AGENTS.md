# Flux

Cross-platform desktop audio player: Avalonia 12 / .NET 10 / LibVLCSharp. Single project (`Flux.csproj`), no solution, no tests, no CI.

## Commands

- `dotnet build` — verified clean. Output goes to `bin/Debug/net10.0/<rid>/` because the project is always `SelfContained`; don't expect the binary directly under `bin/Debug/net10.0/`.
- `dotnet run` — launches the GUI; needs a graphical session.
- `dotnet publish -c Release -r win-x64 -o <dir>` — Windows build. Without `-r win-x64`, publish targets Linux and omits the `VideoLAN.LibVLC.Windows` natives.
- No test project exists. Don't invent `dotnet test` workflows; verify changes with `dotnet build` and a manual run.

## Runtime prerequisites

- Linux: LibVLCSharp uses the system libvlc (the native NuGet package is conditioned on `win-x64`), so VLC/libvlc must be installed. MPRIS calls shell out to `busctl` and fail silently when unavailable.
- Windows publish output contains a `libvlc/` folder next to `Flux.exe`; keep them together when distributing. `Flux_Windows.zip` in the repo root is a local publish artifact (untracked, ~145 MB) — don't commit it.

## Architecture

- Manual wiring, no DI container: `MainViewModel` constructs `AudioPlayerService` and `FilePickerService` directly. New services go in `Interfaces/` and are created in the VM constructor.
- Playback/position come from LibVLC; all metadata (title, artist, sample rate, channels, bitrate, cover) is read from the file with TagLib# on demand. Album art is extracted to `<LocalApplicationData>/Flux/covers/<sha1-of-path>.<ext>` and reused.
- `ViewLocator` maps `Flux.ViewModels.FooViewModel` to `Flux.Views.FooView` by string replacement + reflection. Keep that naming when adding screens.
- Playlist behavior is implicit: opening/dropping a file scans its whole directory for supported extensions, sorts case-insensitively, and Next/Previous wrap. There is no persisted queue.
- Settings (`Volume` only) are saved to `<LocalApplicationData>/Flux/settings.json` on every volume change (`~/.local/share/Flux/` on Linux).
- A file path passed on startup (`Program.StartupArgs`, e.g. "Open with") is auto-played via `PlayFileCommand`.
- MPRIS state/metadata is pushed manually from `MainViewModel`; if you add playback actions, update `MprisService.*` calls alongside them.

## UI conventions

- User-facing strings and code comments are Russian; match that when editing.
- Compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault`); every view needs `x:DataType` and valid binding paths or the build fails.
- Keyboard shortcuts and drag-and-drop live in `Views/MainWindow.axaml.cs`, not in the view model.
- `MainViewModel` is marked `[DynamicallyAccessedMembers(All)]` and `ViewLocator` is annotated `[RequiresUnreferencedCode]` for trimming. Keep both annotations.

## Publish/trimming

`PublishTrimmed`, `PublishSingleFile`, and `SelfContained` are always enabled. `dotnet publish` prints trim warnings from `ViewLocator`, TagLibSharp, and Avalonia.DesignerSupport; these are pre-existing and not caused by your change.
