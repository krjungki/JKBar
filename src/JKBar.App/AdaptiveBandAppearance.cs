// Keeps the band's adaptive look and frosted backdrop in step with the wallpaper. The work runs off the UI thread.
using JKBar.App.Diagnostics;
using JKBar.App.Interop;
using JKBar.App.Rendering;
using JKBar.Core.Layout;
using Microsoft.Win32;

namespace JKBar.App;

internal sealed class AdaptiveBandAppearance : IDisposable
{
    // Slideshows and Spotlight swap the picture without always broadcasting a settings change.
    private readonly System.Windows.Forms.Timer _recheck = new() { Interval = 60_000 };
    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 20 };
    // The shell rewrites its transcoded copy in several writes, so a burst of changes settles into one refresh.
    private readonly System.Windows.Forms.Timer _settle = new() { Interval = 750 };
    private readonly AppearanceTransition _transition = new();
    private SynchronizationContext? _ui;
    private FileSystemWatcher? _themes;

    private bool _adaptive;
    private bool _blur;
    private bool _opaque;
    private bool _subscribed;
    private bool _disposed;
    private Rectangle _band;
    private string? _signature;
    private int _generation;
    private BackdropAnalysis? _analysis;

    /// <summary>Raised whenever the band should be repainted with a new look or backdrop.</summary>
    internal Action? Changed;

    /// <summary>Opaque and exactly band sized, or null when the band is drawn with its plain tint.</summary>
    internal Bitmap? Backdrop { get; private set; }

    /// <summary>Null means the user's own opacity and text colour apply.</summary>
    internal AdaptiveLook? Look => _transition.Current(DateTimeOffset.Now);

    internal AdaptiveBandAppearance()
    {
        _recheck.Tick += (_, _) => Refresh();
        _settle.Tick += (_, _) =>
        {
            _settle.Stop();
            Refresh();
        };
        _frames.Tick += (_, _) =>
        {
            if (!_transition.IsRunning(DateTimeOffset.Now))
            {
                _frames.Stop();
            }

            Changed?.Invoke();
        };
    }

    internal void Configure(bool adaptive, bool blur)
    {
        if (adaptive == _adaptive && blur == _blur)
        {
            return;
        }

        _adaptive = adaptive;
        _blur = blur;
        Refresh();
    }

    /// <summary>An empty rectangle means the band is not reserved and nothing needs watching.</summary>
    internal void SetBand(Rectangle band)
    {
        if (band == _band)
        {
            return;
        }

        _band = band;
        Refresh();
    }

    private bool Wanted => (_adaptive || _blur) && _band.Width > 0 && _band.Height > 0;

    private bool Active => Wanted && !SystemInformation.HighContrast;

    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        // Still listening while high contrast suspends the effect, so switching it off brings the effect back.
        _recheck.Enabled = Wanted;
        Subscribe(Wanted);
        if (!Active)
        {
            Clear();
            return;
        }

        _opaque = !DesktopWallpaperInterop.TransparencyEffectsEnabled();
        var source = DesktopWallpaperInterop.Read(_band);
        if (source is null)
        {
            Clear();
            return;
        }

        var blur = _blur && !_opaque;
        var signature = Signature(source, _band, blur);
        if (signature == _signature)
        {
            Retarget();
            return;
        }

        _signature = signature;
        _ = BuildAsync(source, _band, blur, ++_generation);
    }

    private async Task BuildAsync(WallpaperSource source, Rectangle band, bool blur, int generation)
    {
        (Bitmap? Backdrop, BackdropAnalysis Analysis) result;
        try
        {
            result = await Task.Run(() => BandBackdrop.Build(source, band, blur));
        }
        catch (Exception error)
        {
            // Appearance is cosmetic: whatever went wrong, the band falls back to the user's own settings.
            CrashLog.Write("바탕화면 배경 분석", error);
            if (generation == _generation)
            {
                Clear();
            }

            return;
        }

        if (_disposed || generation != _generation)
        {
            result.Backdrop?.Dispose();
            return;
        }

        Backdrop?.Dispose();
        Backdrop = result.Backdrop;
        _analysis = result.Analysis;
        Retarget();
        Changed?.Invoke();
    }

    private void Retarget()
    {
        var target = _adaptive && Active && _analysis is { } analysis
            ? AdaptiveAppearance.ResolveAutomatic(analysis, _opaque, _transition.Target)
            : null;
        var now = DateTimeOffset.Now;
        var previous = _transition.Target;
        _transition.Retarget(target, now);

        if (_transition.IsRunning(now))
        {
            _frames.Start();
        }
        else if (previous != target)
        {
            Changed?.Invoke();
        }
    }

    private void Clear()
    {
        _generation++;
        _signature = null;
        _analysis = null;
        var hadBackdrop = Backdrop is not null;
        Backdrop?.Dispose();
        Backdrop = null;

        var hadLook = _transition.Target is not null;
        _transition.Retarget(null, DateTimeOffset.Now);
        _frames.Stop();

        if (hadBackdrop || hadLook)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Everything that changes what the strip looks like; the file's size and time catch an in-place rewrite.</summary>
    private static string Signature(WallpaperSource source, Rectangle band, bool blur)
    {
        static string Stamp(string? path)
        {
            if (path is null)
            {
                return "-";
            }

            var file = new FileInfo(path);
            return file.Exists ? $"{path}:{file.Length}:{file.LastWriteTimeUtc.Ticks}" : $"{path}:missing";
        }

        return string.Join('|',
            Stamp(source.ImagePath),
            source.ImagePath is null ? "-" : Stamp(DesktopWallpaperInterop.TranscodedPath),
            source.Fit,
            source.Background.ToArgb(),
            source.Monitor,
            source.VirtualDesktop,
            band,
            blur);
    }

    private void Subscribe(bool subscribe)
    {
        if (subscribe == _subscribed)
        {
            return;
        }

        _subscribed = subscribe;
        if (subscribe)
        {
            // Taken here rather than at construction, which happens before any control has installed the UI context.
            _ui = SynchronizationContext.Current;
            SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
            _themes = WatchThemes();
        }
        else
        {
            SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
            _themes?.Dispose();
            _themes = null;
            _settle.Stop();
        }
    }

    /// <summary>
    /// Changing the picture through Settings does not reliably broadcast a settings change, but the shell always
    /// rewrites its transcoded copies, one per monitor, in this folder.
    /// </summary>
    private FileSystemWatcher? WatchThemes()
    {
        var ui = _ui;
        var folder = Path.GetDirectoryName(DesktopWallpaperInterop.TranscodedPath);
        if (ui is null || folder is null || !Directory.Exists(folder))
        {
            return null;
        }

        try
        {
            var watcher = new FileSystemWatcher(folder, "Transcoded*")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            FileSystemEventHandler changed = (_, _) => ui.Post(_ => Settle(), null);
            watcher.Changed += changed;
            watcher.Created += changed;
            watcher.Renamed += (_, _) => ui.Post(_ => Settle(), null);
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // The minute check still notices the change, only later.
            return null;
        }
    }

    private void Settle()
    {
        if (_disposed || !_subscribed)
        {
            return;
        }

        _settle.Stop();
        _settle.Start();
    }

    /// <summary>A wallpaper change arrives as Desktop; transparency and high contrast as General or Accessibility.</summary>
    private void OnPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.General
            or UserPreferenceCategory.Accessibility or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
        {
            // Deferred so it runs on the UI thread after the broadcast, which can precede the shell's new picture.
            if (_ui is { } ui)
            {
                ui.Post(_ =>
                {
                    Refresh();
                    Settle();
                }, null);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Subscribe(false);
        _recheck.Dispose();
        _frames.Dispose();
        _settle.Dispose();
        _generation++;
        Backdrop?.Dispose();
        Backdrop = null;
    }
}
