// The bar window: click-through, per-pixel alpha, pushed to the desktop with UpdateLayeredWindow.
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml;
using JKBar.App.Alerts;
using JKBar.App.Interop;
using JKBar.App.Network;
using JKBar.App.Rendering;
using JKBar.App.Stocks;
using JKBar.App.Sync;
using JKBar.Core.Alerts;
using JKBar.Core.Devices;
using JKBar.Core.Layout;
using JKBar.Core.Metrics;
using JKBar.Core.Network;
using JKBar.Core.News;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;
using JKBar.Core.Stocks;
using JKBar.Core.Sync;

namespace JKBar.App;

internal sealed class NotchForm : Form
{
    /// <summary>The cutout is always solid black; the band's own colour and opacity never reach it.</summary>
    private static readonly Color NotchFill = Color.FromArgb(255, 0, 0, 0);
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 15 };
    private readonly System.Windows.Forms.Timer _dwell = new() { Interval = 250 };
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private readonly AppBarReservation _reservation = new();
    private readonly System.Windows.Forms.Timer _content =
        new() { Interval = BehaviourSettings.DefaultMetricsRefreshSeconds * 1000 };
    private readonly System.Windows.Forms.Timer _notchClock = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _petAnimation = new() { Interval = 125 };
    private readonly System.Diagnostics.Stopwatch _petClock = new();
    // Display reconfiguration is not finished when the message arrives, so the layout is redone once it settles.
    private readonly System.Windows.Forms.Timer _displaySettle = new() { Interval = 1500 };
    private readonly SystemMetricsCollector _metrics = new();
    private readonly MetricTrails _trails = new();
    // The foreground window changes far more often than the readouts, so it has its own light poll.
    private readonly System.Windows.Forms.Timer _activeApp = new() { Interval = 400 };
    private readonly ActiveAppWatcher _activeApps = new();
    private readonly AlertQueue _alerts = new(AlertPolicy.Default);
    private readonly SystemThresholdWatcher _thresholds = new();
    private readonly PowerWatcher _power = new();
    // Volume feedback has to feel instant, so the endpoint is polled faster than the metrics.
    private readonly System.Windows.Forms.Timer _audio = new() { Interval = 250 };
    private readonly AudioWatcher _audioWatcher = new();
    private readonly System.Windows.Forms.Timer _media = new() { Interval = 2000 };
    private readonly MediaWatcher _mediaWatcher = new();
    // The first check runs soon after start; the interval then follows the verdict.
    private readonly System.Windows.Forms.Timer _network = new() { Interval = 3000 };
    private readonly ConnectivityProbe _connectivityProbe = new();
    private readonly ConnectivityWatcher _connectivity = new();
    private readonly System.Windows.Forms.Timer _syncStatus = new() { Interval = 5000 };
    private readonly SyncStatusPoller _syncPoller = new();
    private readonly SyncStatusWatcher _syncWatcher = new();
    private readonly System.Windows.Forms.Timer _newsRefresh = new();
    private readonly System.Windows.Forms.Timer _newsRotation = new();
    private readonly NewsFeedClient _newsClient = new();
    private readonly System.Windows.Forms.Timer _stockRefresh = new();
    private readonly System.Windows.Forms.Timer _stockRotation = new();
    private readonly NaverStockClient _stockClient = new();
    private readonly CancellationTokenSource _shutdown = new();

    private OverlapMode _overlap = OverlapMode.ReserveTopEdge;
    private BandStyle _band = BandStyle.Default;
    private string _monitorDeviceName = string.Empty;
    private Image? _image;
    private int _imageScalePercent = 100;
    private string _contentSignature = string.Empty;
    private NotchMetrics _shown = NotchMetrics.MacBookPro14;
    private NotchMetrics _from = NotchMetrics.MacBookPro14;
    private NotchMetrics _to = NotchMetrics.MacBookPro14;
    private NewsSettings _newsSettings = new();
    private StockWatchSettings _stockSettings = new();
    private IReadOnlyList<StockQuote> _quotes = [];
    private int _quoteIndex;
    private bool _stocksLoading;
    private BandTypographySettings _typography = new();
    private BandItemsSettings _bandItems = new();
    private NotchSettings _notchSettings = new();
    private BehaviourSettings _behaviour = new();
    private ProcessWatchSettings _processWatch = new();
    private bool _fullscreenSuppressed;
    private NotchAlert? _alert;
    private NowPlaying? _nowPlaying;
    private Image? _nowPlayingIcon;
    private string _notchContentSignature = string.Empty;
    private IReadOnlyList<NewsItem> _newsItems = [];
    private int _newsIndex;
    private bool _newsConfigured;
    private bool _newsLoading;
    private bool _mediaLoading;
    private bool _connectivityChecking;
    private bool _syncLoading;
    private bool _expanded;

    internal event Action<Rectangle>? NewsClicked;

    internal event Action<WatchedProcess>? ProcessClicked;

    internal IReadOnlyList<NewsItem> NewsItems => _newsItems;

    internal NotchForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "JKBar";

        _animation.Tick += (_, _) => Advance();
        _dwell.Tick += (_, _) => AdvanceAlerts();
        _content.Tick += (_, _) => RefreshContent(force: false);
        _activeApp.Tick += (_, _) =>
        {
            UpdateFullscreenSuppression();
            if (!_fullscreenSuppressed && _activeApps.Refresh())
            {
                RefreshContent(force: true);
            }
        };
        _audio.Tick += (_, _) => ObserveAudio();
        _media.Tick += async (_, _) => await ObserveMediaAsync();
        _network.Tick += async (_, _) => await CheckConnectivityAsync();
        _syncStatus.Tick += async (_, _) => await RefreshSyncStatusAsync();
        _notchClock.Tick += (_, _) => RefreshIdleNotch();
        _petAnimation.Tick += (_, _) => Redraw();
        _displaySettle.Tick += (_, _) =>
        {
            _displaySettle.Stop();
            ApplyOverlap();
            Redraw();
        };
        _newsRefresh.Tick += async (_, _) => await RefreshNewsAsync();
        _newsRotation.Tick += (_, _) => RotateNews();
        _stockRefresh.Tick += async (_, _) => await RefreshStocksAsync();
        _stockRotation.Tick += (_, _) => RotateStocks();
        _reservation.Claimed += () => NotchWindowInterop.RaiseToTop(Handle);
        _reservation.NewsClicked += anchor => NewsClicked?.Invoke(anchor);
        _reservation.ProcessClicked += process => ProcessClicked?.Invoke(process);
    }

    /// <summary>
    /// The styles must exist before the first paint, so they go in the creation parameters. WS_EX_LAYERED is what
    /// makes UpdateLayeredWindow legal on this window.
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= NotchWindowInterop.WsExLayered
                | NotchWindowInterop.WsExTransparent
                | NotchWindowInterop.WsExToolWindow
                | NotchWindowInterop.WsExNoActivate;

            return parameters;
        }
    }

    protected override bool ShowWithoutActivation => true;

    /// <summary>Resting width in logical pixels, before DPI scaling.</summary>
    internal int LogicalWidth => Resting().Width;

    internal OverlapMode Overlap => _overlap;

    /// <summary>Only visible in the reserving mode, where the band shows either side of the bar.</summary>
    internal BandStyle Band => _band;

    internal void SetBand(BandStyle style)
    {
        _band = style;
        _reservation.SetStyle(style);
    }

    internal bool HasImage => _image is not null;

    internal NewsSettings NewsSettings => _newsSettings;

    internal BandTypographySettings Typography => _typography;

    internal NotchSettings NotchSettings => _notchSettings;

    internal BehaviourSettings Behaviour => _behaviour;

    internal void SetBehaviour(BehaviourSettings settings)
    {
        _behaviour = settings.Normalized();
        _content.Interval = _behaviour.MetricsRefreshSeconds * 1000;
        UpdateFullscreenSuppression();
    }

    /// <summary>
    /// A game or a video owning the display is when the bar is least welcome, so it hides and stops sampling.
    /// The foreground poll keeps running while suppressed; it is what notices the display coming back.
    /// </summary>
    private void UpdateFullscreenSuppression()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var (isShell, foregroundMonitor, window, bounds) = ForegroundWindowInterop.Foreground();
        var barMonitor = ForegroundWindowInterop.MonitorOf(Handle);
        var suppress = FullscreenGate.ShouldSuppress(
            _behaviour.HideWhenFullscreen,
            ForegroundWindowInterop.NotificationState(),
            isShell,
            barMonitor != IntPtr.Zero && foregroundMonitor == barMonitor,
            window,
            bounds);

        if (suppress == _fullscreenSuppressed)
        {
            return;
        }

        _fullscreenSuppressed = suppress;
        if (suppress)
        {
            StopSampling();
            _reservation.Suspend();
            Hide();
            return;
        }

        // Everything sampled through the pause is stale, so the readouts are rebuilt before anything is shown.
        _contentSignature = string.Empty;
        _notchContentSignature = string.Empty;
        Show();
        ApplyOverlap();
        Settle();
    }

    private void StopSampling()
    {
        _petAnimation.Stop();
        _petClock.Stop();
        _content.Stop();
        _audio.Stop();
        _media.Stop();
        _network.Stop();
        _syncStatus.Stop();
        _notchClock.Stop();
        _newsRefresh.Stop();
        _newsRotation.Stop();
    }

    internal void SetNotchSettings(NotchSettings settings)
    {
        if (_notchSettings.IdleContent != settings.IdleContent)
        {
            _petClock.Reset();
        }
        _notchSettings = settings.Normalized();
        _notchContentSignature = string.Empty;
        _nowPlaying = _notchSettings.ShowNowPlaying ? _mediaWatcher.Current : null;
        Redraw();
    }

    internal void SetBandItems(BandItemsSettings settings)
    {
        _bandItems = settings.Normalized();
        RefreshContent(force: true);
    }

    internal void SetProcessWatch(ProcessWatchSettings settings)
    {
        _processWatch = settings.Normalized();
        RefreshContent(force: true);
    }

    internal void SetTypography(BandTypographySettings typography)
    {
        _typography = typography.Normalized();
        _reservation.SetTypography(_typography);
        RefreshContent(force: true);
    }

    internal void SetNewsSettings(NewsSettings settings)
    {
        var normalized = settings.Normalized();
        var sourceChanged = !string.Equals(
            normalized.FeedUrl,
            _newsSettings.FeedUrl,
            StringComparison.OrdinalIgnoreCase);
        var justEnabled = normalized.Enabled && !_newsSettings.Enabled;

        _newsSettings = normalized;
        _newsConfigured = true;
        _newsRefresh.Interval = checked(normalized.RefreshMinutes * 60 * 1000);
        _newsRotation.Interval = checked(normalized.RotationSeconds * 1000);

        if (sourceChanged || !normalized.Enabled)
        {
            _newsItems = [];
            _newsIndex = 0;
            RefreshContent(force: true);
        }

        UpdateNewsTimers(fetchImmediately: sourceChanged || justEnabled || _newsItems.Count == 0);
    }

    /// <summary>Returns false when the file could not be read as a picture, so the caller can say so.</summary>
    internal bool SetImage(string path)
    {
        var loaded = BandImage.Load(path);
        if (loaded is null)
        {
            return false;
        }

        _image?.Dispose();
        _image = loaded;
        RefreshContent(force: true);

        return true;
    }

    internal void SetImageScale(int percent)
    {
        _imageScalePercent = percent;
        RefreshContent(force: true);
    }

    internal void ClearImage()
    {
        _image?.Dispose();
        _image = null;
        RefreshContent(force: true);
    }

    /// <summary>
    /// Rebuilds the band's readouts. The surface is the full screen width, so it is only pushed when something
    /// visible actually changed rather than on every tick.
    /// </summary>
    private void RefreshContent(bool force)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var snapshot = _metrics.Read();
        _trails.Observe(snapshot);

        foreach (var alert in _thresholds.Observe(snapshot))
        {
            Notify(alert);
        }

        if (_power.Observe() is { } powerAlert)
        {
            Notify(powerAlert);
        }

        if (_overlap != OverlapMode.ReserveTopEdge)
        {
            return;
        }

        var news = CurrentNews;
        var quote = CurrentQuote;
        var items = _bandItems.Apply(
        [
            .. MetricsSource.Items(snapshot, _trails),
            .. _syncPoller.Items,
            ClockSource.Item(now, CultureInfo.CurrentCulture)
        ]);
        // Listing every process is only worth doing once the user has actually asked to watch something.
        var runningProcesses = _processWatch.Items.Length == 0
            ? []
            : ProcessWatchSource.Running(_processWatch, RunningProcesses.Counts());
        var signature = string.Join(
                '|',
                items.Select(item =>
                    $"{item.Kind}:{item.Layout}:{item.Label}:{item.Accent?.ToArgb()}:{item.Badge}:"
                    + string.Join(',', item.Values.Select(value => value.Text))
                    + TrailSignature(item)))
            + news?.Link.AbsoluteUri
            + news?.Title
            + (quote is null ? string.Empty : StockPresentation.Line(quote))
            + ProcessWatchSource.Signature(runningProcesses);
        if (!force && signature == _contentSignature)
        {
            return;
        }

        _contentSignature = signature;
        var resting = Resting().ScaledBy(ScaleFor());
        _reservation.SetContent(
            _image,
            _imageScalePercent,
            _activeApps.Current,
            quote,
            news,
            items,
            runningProcesses,
            RestingNotchInBand(),
            resting.BottomCornerRadius);
    }

    /// <summary>The headline the band is showing right now, which is what a click on it means.</summary>
    internal NewsItem? CurrentNews =>
        _newsSettings.Enabled && _newsItems.Count > 0 ? _newsItems[_newsIndex % _newsItems.Count] : null;

    /// <summary>A graph moves even when its number reads the same, so its readings have to count as content.</summary>
    private static string TrailSignature(BandItem item) =>
        item.Layout is BandItemLayout.VerticalLabelGraph or BandItemLayout.VerticalLabelValueGraph
            ? ":" + string.Join(',', (item.Trail ?? []).Select(reading => (int)Math.Round(reading)))
            : string.Empty;

    private void RefreshIdleNotch()
    {
        if (_notchSettings.IdleContent != IdleNotchContent.DateTime || _nowPlaying is not null)
        {
            return;
        }

        var signature = IdleNotchSource.Signature(DateTimeOffset.Now);
        if (signature == _notchContentSignature)
        {
            return;
        }

        _notchContentSignature = signature;
        Redraw();
    }

    private void UpdateNewsTimers(bool fetchImmediately)
    {
        var active = _newsConfigured && _newsSettings.Enabled && _overlap == OverlapMode.ReserveTopEdge;
        _newsRefresh.Enabled = active;
        _newsRotation.Enabled = active;

        if (active && fetchImmediately)
        {
            _ = RefreshNewsAsync();
        }
    }

    private async Task RefreshNewsAsync()
    {
        if (_newsLoading || !_newsSettings.TryGetFeedUri(out var feedUri))
        {
            return;
        }

        _newsLoading = true;
        try
        {
            var items = await _newsClient.FetchAsync(feedUri, _shutdown.Token);
            if (items.Count > 0)
            {
                _newsItems = items;
                _newsIndex = 0;
                RefreshContent(force: true);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // The app is closing.
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or XmlException
            || exception is TaskCanceledException)
        {
            // A feed is optional. Keep the last successful headline and retry on the normal schedule.
        }
        finally
        {
            _newsLoading = false;
        }
    }

    private void RotateNews()
    {
        if (_newsItems.Count <= 1)
        {
            return;
        }

        _newsIndex = (_newsIndex + 1) % _newsItems.Count;
        RefreshContent(force: true);
    }

    internal void SetStocks(StockWatchSettings settings)
    {
        var normalized = settings.Normalized();
        var listChanged = !_stockSettings.Items.SequenceEqual(normalized.Items);
        _stockSettings = normalized;
        _stockRefresh.Interval = checked(normalized.RefreshSeconds * 1000);
        _stockRotation.Interval = checked(normalized.RotationSeconds * 1000);

        if (listChanged || !normalized.IsActive)
        {
            _quotes = [];
            _quoteIndex = 0;
        }

        UpdateStockTimers(fetchImmediately: normalized.IsActive && _quotes.Count == 0);
        RefreshContent(force: true);
    }

    private void UpdateStockTimers(bool fetchImmediately)
    {
        var active = _stockSettings.IsActive && _overlap == OverlapMode.ReserveTopEdge;
        _stockRefresh.Enabled = active;
        _stockRotation.Enabled = active;

        if (active && fetchImmediately)
        {
            _ = RefreshStocksAsync();
        }
    }

    private async Task RefreshStocksAsync()
    {
        if (_stocksLoading || !_stockSettings.IsActive)
        {
            return;
        }

        _stocksLoading = true;
        try
        {
            var quotes = await _stockClient.QuotesAsync(_stockSettings.Items, _shutdown.Token);
            if (quotes.Count > 0)
            {
                _quotes = quotes;
                _quoteIndex %= quotes.Count;
                RefreshContent(force: true);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // The app is closing.
        }
        finally
        {
            _stocksLoading = false;
        }
    }

    private void RotateStocks()
    {
        if (_quotes.Count <= 1)
        {
            return;
        }

        _quoteIndex = (_quoteIndex + 1) % _quotes.Count;
        RefreshContent(force: true);
    }

    /// <summary>The symbol the band is showing right now.</summary>
    private StockQuote? CurrentQuote =>
        _stockSettings.IsActive && _quotes.Count > 0 ? _quotes[_quoteIndex % _quotes.Count] : null;

    /// <summary>The resting bar in band coordinates, which is the gap the band's content has to work around.</summary>
    private NotchGeometry.Rect RestingNotchInBand()
    {
        var screen = HostBounds();
        var notch = NotchGeometry.Place(screen, Resting().ScaledBy(ScaleFor()));

        return new NotchGeometry.Rect(notch.Left - screen.Left, 0, notch.Right - screen.Left, notch.Height);
    }

    internal void SetOverlap(OverlapMode mode)
    {
        _overlap = mode;
        ApplyOverlap();
        Redraw();
    }

    internal void SetMonitor(string deviceName)
    {
        _monitorDeviceName = deviceName;
        ApplyOverlap();
        Redraw();
    }

    /// <summary>
    /// The chosen display, or whichever one the window is already on when nothing is chosen and when the chosen
    /// one is unplugged. Panel bounds rather than the work area, because the bar imitates a hole in the bezel.
    /// </summary>
    private NotchGeometry.Rect HostBounds()
    {
        if (MonitorCatalog.BoundsOf(_monitorDeviceName) is { } chosen)
        {
            return chosen;
        }

        var area = Screen.FromHandle(Handle).Bounds;

        return new NotchGeometry.Rect(area.Left, area.Top, area.Right, area.Bottom);
    }

    /// <summary>
    /// Reserving the edge and floating both keep the bar on top; only the desktop mode drops it, and it has to be
    /// pushed down again whenever the shell rearranges the z-order.
    /// </summary>
    private void ApplyOverlap()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        // Sampling continues in every mode: system and power alerts do not depend on the band being visible.
        _content.Start();
        _activeApp.Start();
        _audio.Start();
        _media.Start();
        _network.Start();
        _syncStatus.Start();

        if (_overlap == OverlapMode.ReserveTopEdge)
        {
            _reservation.Below = Handle;
            _reservation.SetStyle(_band);
            // The resting height, never the animated one: a display change during an alert would otherwise
            // freeze the reserved strip at the expanded size.
            _reservation.Reserve(HostBounds(), Resting().ScaledBy(ScaleFor()).Height);
            RefreshContent(force: true);
            UpdateNewsTimers(fetchImmediately: _newsItems.Count == 0);
            UpdateStockTimers(fetchImmediately: _quotes.Count == 0);
        }
        else
        {
            UpdateNewsTimers(fetchImmediately: false);
            UpdateStockTimers(fetchImmediately: false);
            _reservation.Release();
        }

        if (_overlap == OverlapMode.PinnedToDesktop)
        {
            NotchWindowInterop.SendToBottom(Handle);
        }
        else
        {
            NotchWindowInterop.RaiseToTop(Handle);
        }
    }

    /// <summary>
    /// Opens the bar into its alert panel and closes it again once the queue has nothing left to show.
    /// </summary>
    internal void Notify(NotchAlert alert)
    {
        if (_fullscreenSuppressed)
        {
            return;
        }

        _alerts.Submit(alert, DateTimeOffset.Now);
        AdvanceAlerts();
    }

    private void ObserveAudio()
    {
        foreach (var alert in _audioWatcher.Observe())
        {
            Notify(alert);
        }
    }

    private async Task ObserveMediaAsync()
    {
        if (_mediaLoading)
        {
            return;
        }

        _mediaLoading = true;
        try
        {
            var alert = await _mediaWatcher.ObserveAsync();
            if (IsDisposed)
            {
                return;
            }

            RefreshNowPlaying();
            if (alert is not null)
            {
                Notify(alert);
            }
        }
        finally
        {
            _mediaLoading = false;
        }
    }

    private void RefreshNowPlaying()
    {
        var playing = _notchSettings.ShowNowPlaying ? _mediaWatcher.Current : null;
        if (playing?.Signature == _nowPlaying?.Signature)
        {
            return;
        }

        _nowPlaying = playing;
        // Resolved once per track at the expanded size: a miss walks every running process, which is far too
        // costly per repaint, and the notch scales the same bitmap down when it is resting.
        _nowPlayingIcon = playing is null
            ? null
            : MediaAppIcon.Resolve(playing.AppId, NowPlayingIconSize());

        if (_alert is null || _alert.Category == AlertCategory.Media)
        {
            Redraw();
        }
    }

    private int NowPlayingIconSize() =>
        Math.Max(32, (int)Math.Round(Resting().Expanded().ScaledBy(ScaleFor()).Height * 0.46));

    private async Task CheckConnectivityAsync()
    {
        if (_connectivityChecking)
        {
            return;
        }

        _connectivityChecking = true;
        try
        {
            var state = await _connectivityProbe.CheckAsync(_shutdown.Token);
            if (IsDisposed)
            {
                return;
            }

            // A broken link is re-checked often so recovery shows up quickly.
            _network.Interval = state == ConnectivityState.Online ? 30000 : 5000;
            if (_connectivity.Observe(state) is { } alert)
            {
                Notify(alert);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _connectivityChecking = false;
        }
    }

    private async Task RefreshSyncStatusAsync()
    {
        if (_syncLoading)
        {
            return;
        }

        _syncLoading = true;
        try
        {
            await _syncPoller.RefreshAsync(_shutdown.Token);
            if (IsDisposed)
            {
                return;
            }

            foreach (var alert in _syncWatcher.Observe(_syncPoller.Snapshots, _bandItems.IsVisible))
            {
                Notify(alert);
            }

            RefreshContent(force: false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _syncLoading = false;
        }
    }

    private void AdvanceAlerts()
    {
        var current = _alerts.Tick(DateTimeOffset.Now);
        if (ReferenceEquals(current, _alert))
        {
            return;
        }

        _alert = current;
        if (current is null)
        {
            _expanded = false;
            _dwell.Stop();
            StartTransition(Resting());
            return;
        }

        _dwell.Start();
        if (_expanded)
        {
            Redraw();
        }
        else
        {
            _expanded = true;
            StartTransition(Resting().Expanded());
        }
    }

    /// <summary>Jumps to the size a change of display implies, without animating something the user did not ask for.</summary>
    private void Settle()
    {
        _animation.Stop();
        _shown = _expanded ? Resting().Expanded() : Resting();
        Redraw();
    }

    private void StartTransition(NotchMetrics target)
    {
        _from = _shown;
        _to = target;
        _clock.Restart();
        _animation.Start();
    }

    private void Advance()
    {
        var progress = _clock.Elapsed.TotalMilliseconds / NotchAnimation.Duration.TotalMilliseconds;
        if (progress >= 1d)
        {
            _animation.Stop();
            _clock.Stop();
            _shown = _to;
        }
        else
        {
            _shown = NotchAnimation.Between(_from, _to, progress);
        }

        Redraw();
    }

    private NotchMetrics Resting() =>
        NotchMetrics.MacBookPro14 with { Width = NotchMetrics.ProportionalWidth(LogicalScreenWidth()) };

    /// <summary>What the window manager thinks, which is the only way to confirm the bar really reaches the edge.</summary>
    internal NotchGeometry.Rect ActualBounds() =>
        IsHandleCreated ? NotchWindowInterop.GetBounds(Handle) : default;

    /// <summary>Host monitor width in logical pixels, so ratio-derived widths can be offered in the same unit.</summary>
    internal int LogicalScreenWidth() =>
        IsHandleCreated ? (int)Math.Round(HostBounds().Width / ScaleFor()) : 0;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyOverlap();
        Settle();
    }

    /// <summary>
    /// Showing the window applies WinForms' own idea of its bounds, which would undo the layered surface pushed
    /// while the handle was still hidden. The size and position only stick once this has run.
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _notchClock.Start();
        Settle();
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NotchWindowInterop.WmDpiChanged:
            case NotchWindowInterop.WmDisplayChange:
            case NotchWindowInterop.WmSettingChange:
                // Moving to another monitor changes the DPI, and a shell restart rearranges the z-order. The
                // reserved band is in physical pixels, so it has to be claimed again at the new scale.
                base.WndProc(ref m);
                ApplyOverlap();
                Redraw();
                _displaySettle.Stop();
                _displaySettle.Start();
                return;

            case NotchWindowInterop.WmWindowPosChanging:
                if (_overlap == OverlapMode.PinnedToDesktop)
                {
                    NotchWindowInterop.PinToBottom(m.LParam);
                }
                else
                {
                    NotchWindowInterop.PinToTop(m.LParam);
                }

                break;

            case NotchWindowInterop.WmDeviceChange:
                AnnounceVolumeChange(m.WParam.ToInt64(), m.LParam);
                break;
        }

        base.WndProc(ref m);
    }

    private void AnnounceVolumeChange(long change, IntPtr broadcast)
    {
        if (broadcast == IntPtr.Zero
            || change is not (NotchWindowInterop.DeviceArrived or NotchWindowInterop.DeviceRemoved))
        {
            return;
        }

        var volume = Marshal.PtrToStructure<NotchWindowInterop.VolumeBroadcast>(broadcast);
        if (volume.DeviceType != NotchWindowInterop.DeviceTypeVolume)
        {
            return;
        }

        foreach (var letter in VolumeChange.Letters(volume.UnitMask))
        {
            Notify(change == NotchWindowInterop.DeviceArrived
                ? VolumeChange.Arrived(letter)
                : VolumeChange.Removed(letter));
        }
    }

    private void Redraw()
    {
        var animatePet = PixelPetAnimation.ShouldAnimate(
            _notchSettings.IdleContent, Visible, _fullscreenSuppressed,
            _alert is not null, _nowPlaying is not null, _expanded || _animation.Enabled);
        _petAnimation.Enabled = animatePet;
        if (animatePet) _petClock.Start();
        else _petClock.Stop();

        if (!IsHandleCreated)
        {
            return;
        }

        var scaled = Scaled();
        var bounds = ComputeBounds(scaled);

        using var bitmap = new Bitmap(scaled.Width, scaled.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            NotchRenderer.Paint(
                graphics,
                scaled,
                NotchFill,
                CurrentNotchContent(),
                _typography,
                NotchTextSize(scaled),
                NotchIcon(),
                NotchMark());
            if (animatePet)
            {
                PixelPetRenderer.Paint(graphics, scaled, _notchSettings.IdleContent, _petClock.Elapsed);
            }
        }

        NotchWindowInterop.PushLayeredSurface(Handle, bitmap, bounds.Left, bounds.Top);
    }

    /// <summary>
    /// An alert is sized from the panel it is drawn in, so it grows as the notch opens. Resting text keeps the
    /// band's own lettering, which does not change with the animation.
    /// </summary>
    private float NotchTextSize(NotchMetrics scaled) => _alert is null
        ? Resting().ScaledBy(ScaleFor()).Height * _typography.FontSizePercent / 100f
        : scaled.Height * _notchSettings.AlertFontSizePercent / 100f;

    /// <summary>The playing app stays named while its own alert is open, and whenever nothing else is showing.</summary>
    /// <summary>
    /// The playing app stays named while its own alert is open, and whenever nothing else is showing. A sync
    /// alert carries that provider's own icon so the notch says which application changed.
    /// </summary>
    private Image? NotchIcon() => _alert switch
    {
        null => _nowPlayingIcon,
        { Category: AlertCategory.Media } => _nowPlayingIcon,
        { Category: AlertCategory.Sync } => SyncAlertIcon(),
        _ => null
    };

    /// <summary>The resolver owns the bitmap and hands out the same one again, so it is never disposed here.</summary>
    private Image? SyncAlertIcon() =>
        _alert is { } alert && SyncStatusWatcher.ProviderIdOf(alert) is { } providerId
            ? ProviderIconResolver.Resolve(providerId, NowPlayingIconSize())
            : null;

    /// <summary>Only an open alert carries a mark; resting content is text alone.</summary>
    private NotchGlyph NotchMark() =>
        _alert is { } alert ? NotchGlyphRenderer.For(alert.Category, alert.Severity) : NotchGlyph.None;

    private NotchContent? CurrentNotchContent()
    {
        if (_alert is { } alert)
        {
            return new NotchContent(alert.Title, alert.Detail);
        }

        if (_nowPlaying is { } playing)
        {
            return new NotchContent(playing.Title);
        }

        var idle = IdleNotchSource.Content(
            _notchSettings.IdleContent,
            hasActiveAlert: false,
            DateTimeOffset.Now,
            CultureInfo.CurrentCulture);

        return idle is null ? null : new NotchContent(idle);
    }

    private NotchMetrics Scaled() => _shown.ScaledBy(ScaleFor());

    private NotchGeometry.Rect ComputeBounds(NotchMetrics scaled) => NotchGeometry.Place(HostBounds(), scaled);

    /// <summary>GDI does not scale for DPI on its own, so every drawn size is multiplied by this.</summary>
    private double ScaleFor()
    {
        if (!IsHandleCreated)
        {
            return 1d;
        }

        var area = HostBounds();

        return MonitorScale.At(area.Left + (area.Width / 2), area.Top + (area.Height / 2));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Before the timers, so a reservation cannot outlive the process and leave the work area shrunk.
            _reservation.Dispose();
            _animation.Dispose();
            _dwell.Dispose();
            _content.Dispose();
            _activeApp.Dispose();
            _audio.Dispose();
            _audioWatcher.Dispose();
            _media.Dispose();
            _network.Dispose();
            _connectivityProbe.Dispose();
            _syncStatus.Dispose();
            _syncPoller.Dispose();
            _notchClock.Dispose();
            _petAnimation.Dispose();
            _displaySettle.Dispose();
            _newsRefresh.Dispose();
            _newsRotation.Dispose();
            _stockRefresh.Dispose();
            _stockRotation.Dispose();
            _shutdown.Cancel();
            _shutdown.Dispose();
            _newsClient.Dispose();
            _stockClient.Dispose();
            _metrics.Dispose();
            _image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
