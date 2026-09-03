// The bar window: click-through, per-pixel alpha, pushed to the desktop with UpdateLayeredWindow.
using System.Drawing.Imaging;
using System.Globalization;
using JKBar.App.Interop;
using JKBar.App.Rendering;
using JKBar.Core.Layout;
using JKBar.Core.Metrics;
using JKBar.Core.Presentation;

namespace JKBar.App;

internal sealed class NotchForm : Form
{
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 15 };
    private readonly System.Windows.Forms.Timer _dwell = new();
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private readonly AppBarReservation _reservation = new();
    private readonly System.Windows.Forms.Timer _content = new() { Interval = 1000 };
    private readonly SystemMetricsCollector _metrics = new();

    private OverlapMode _overlap = OverlapMode.ReserveTopEdge;
    private BandStyle _band = BandStyle.Default;
    private Image? _image;
    private string _contentSignature = string.Empty;
    private NotchMetrics _shown = NotchMetrics.MacBookPro14;
    private NotchMetrics _from = NotchMetrics.MacBookPro14;
    private NotchMetrics _to = NotchMetrics.MacBookPro14;
    private bool _expanded;

    internal NotchForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "JKBar";

        _animation.Tick += (_, _) => Advance();
        _dwell.Tick += (_, _) => Collapse();
        _content.Tick += (_, _) => RefreshContent(force: false);
        _reservation.Claimed += () => NotchWindowInterop.RaiseToTop(Handle);
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
        if (!IsHandleCreated || _overlap != OverlapMode.ReserveTopEdge)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var snapshot = _metrics.Read();
        var signature = ClockSource.Signature(now) + MetricsSource.Signature(snapshot);
        if (!force && signature == _contentSignature)
        {
            return;
        }

        _contentSignature = signature;
        _reservation.SetContent(
            _image,
            [.. MetricsSource.Items(snapshot), ClockSource.Item(now, CultureInfo.CurrentCulture)],
            RestingNotchInBand());
    }

    /// <summary>The resting bar in band coordinates, which is the gap the band's content has to work around.</summary>
    private NotchGeometry.Rect RestingNotchInBand()
    {
        var area = Screen.FromHandle(Handle).Bounds;
        var screen = new NotchGeometry.Rect(area.Left, area.Top, area.Right, area.Bottom);
        var notch = NotchGeometry.Place(screen, Resting().ScaledBy(ScaleFor()));

        return new NotchGeometry.Rect(notch.Left - area.Left, 0, notch.Right - area.Left, notch.Height);
    }

    internal void SetOverlap(OverlapMode mode)
    {
        _overlap = mode;
        ApplyOverlap();
        Redraw();
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

        if (_overlap == OverlapMode.ReserveTopEdge)
        {
            var area = Screen.FromHandle(Handle).Bounds;
            _reservation.SetStyle(_band);
            _reservation.Reserve(
                new NotchGeometry.Rect(area.Left, area.Top, area.Right, area.Bottom),
                Scaled().Height);
            RefreshContent(force: true);
            _content.Start();
        }
        else
        {
            _content.Stop();

            // A stopped collector would otherwise divide the whole pause by one interval on the next read.
            _metrics.ResetBaseline();
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
    /// Opens the bar into its alert panel and closes it again after <paramref name="dwell"/>. The content that
    /// belongs inside is a later phase; this is the motion the content will ride on.
    /// </summary>
    internal void Announce(TimeSpan dwell)
    {
        _expanded = true;
        StartTransition(Resting().Expanded());

        _dwell.Stop();
        _dwell.Interval = Math.Max(1, (int)dwell.TotalMilliseconds);
        _dwell.Start();
    }

    private void Collapse()
    {
        _dwell.Stop();
        _expanded = false;
        StartTransition(Resting());
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
        IsHandleCreated ? (int)Math.Round(Screen.FromHandle(Handle).Bounds.Width / ScaleFor()) : 0;

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
        }

        base.WndProc(ref m);
    }

    private void Redraw()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var scaled = Scaled();
        var bounds = ComputeBounds(scaled);

        using var bitmap = new Bitmap(scaled.Width, scaled.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            NotchRenderer.Paint(graphics, scaled, Color.Black);
        }

        NotchWindowInterop.PushLayeredSurface(Handle, bitmap, bounds.Left, bounds.Top);
    }

    private NotchMetrics Scaled() => _shown.ScaledBy(ScaleFor());

    private NotchGeometry.Rect ComputeBounds(NotchMetrics scaled)
    {
        // The panel edge, not the work area: a notch imitates a hole in the bezel.
        var area = Screen.FromHandle(Handle).Bounds;
        var screen = new NotchGeometry.Rect(area.Left, area.Top, area.Right, area.Bottom);

        return NotchGeometry.Place(screen, scaled);
    }

    /// <summary>GDI does not scale for DPI on its own, so every drawn size is multiplied by this.</summary>
    private double ScaleFor() => IsHandleCreated ? DeviceDpi / 96d : 1d;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Before the timers, so a reservation cannot outlive the process and leave the work area shrunk.
            _reservation.Dispose();
            _animation.Dispose();
            _dwell.Dispose();
            _content.Dispose();
            _metrics.Dispose();
            _image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
