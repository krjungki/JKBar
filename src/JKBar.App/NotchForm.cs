// The bar window: click-through, per-pixel alpha, pushed to the desktop with UpdateLayeredWindow.
using System.Drawing.Imaging;
using JKBar.App.Interop;
using JKBar.App.Rendering;
using JKBar.Core.Layout;

namespace JKBar.App;

internal sealed class NotchForm : Form
{
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 15 };
    private readonly System.Windows.Forms.Timer _dwell = new();
    private readonly System.Diagnostics.Stopwatch _clock = new();

    private bool _proportionalWidth = true;
    private int _fixedLogicalWidth = NotchMetrics.MacBookPro14.Width;
    private NotchAlignment _alignment = NotchAlignment.Centre;

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

    /// <summary>Resting width in logical pixels, before DPI scaling. The eventual settings screen will own this.</summary>
    internal int LogicalWidth => Resting().Width;

    internal bool WidthFollowsScreen => _proportionalWidth;

    /// <summary>Ties the resting width to Apple's share of screen width, so it holds on any display.</summary>
    internal void UseProportionalWidth()
    {
        _proportionalWidth = true;
        Settle();
    }

    internal void SetLogicalWidth(int logicalWidth)
    {
        _proportionalWidth = false;
        _fixedLogicalWidth = Math.Max(1, logicalWidth);
        Settle();
    }

    internal void Align(NotchAlignment alignment)
    {
        _alignment = alignment;
        Redraw();
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

    /// <summary>Jumps to the size a settings change implies, without animating a change the user just made.</summary>
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

    private NotchMetrics Resting()
    {
        var width = _proportionalWidth
            ? NotchMetrics.ProportionalWidth(LogicalScreenWidth())
            : _fixedLogicalWidth;

        return NotchMetrics.MacBookPro14 with { Width = width };
    }

    /// <summary>What the window manager thinks, which is the only way to confirm the bar really reaches the edge.</summary>
    internal NotchGeometry.Rect ActualBounds() =>
        IsHandleCreated ? NotchWindowInterop.GetBounds(Handle) : default;

    /// <summary>Host monitor width in logical pixels, so ratio-derived widths can be offered in the same unit.</summary>
    internal int LogicalScreenWidth() =>
        IsHandleCreated ? (int)Math.Round(Screen.FromHandle(Handle).Bounds.Width / ScaleFor()) : 0;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NotchWindowInterop.RaiseToTop(Handle);
        Settle();
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NotchWindowInterop.WmDpiChanged:
            case NotchWindowInterop.WmDisplayChange:
            case NotchWindowInterop.WmSettingChange:
                // Moving to another monitor changes the DPI, and a shell restart rearranges the z-order.
                base.WndProc(ref m);
                EnsureTopMost();
                Redraw();
                return;

            case NotchWindowInterop.WmWindowPosChanging:
                NotchWindowInterop.PinToTop(m.LParam);
                break;
        }

        base.WndProc(ref m);
    }

    private void EnsureTopMost()
    {
        if (IsHandleCreated && !NotchWindowInterop.IsTopMost(Handle))
        {
            NotchWindowInterop.RaiseToTop(Handle);
        }
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

        return NotchGeometry.Place(screen, scaled, _alignment);
    }

    /// <summary>GDI does not scale for DPI on its own, so every drawn size is multiplied by this.</summary>
    private double ScaleFor() => IsHandleCreated ? DeviceDpi / 96d : 1d;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animation.Dispose();
            _dwell.Dispose();
        }

        base.Dispose(disposing);
    }
}
