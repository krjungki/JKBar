// The bar window: click-through, per-pixel alpha, pushed to the desktop with UpdateLayeredWindow.
using System.Drawing.Imaging;
using JKBar.App.Interop;
using JKBar.App.Rendering;
using JKBar.Core.Layout;

namespace JKBar.App;

internal sealed class NotchForm : Form
{
    private NotchMetrics _metrics = NotchMetrics.MacBookPro14;
    private NotchAlignment _alignment = NotchAlignment.Centre;

    internal NotchForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "JKBar";
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

    /// <summary>Logical width, before DPI scaling. The eventual settings screen will own this.</summary>
    internal int LogicalWidth => _metrics.Width;

    internal void SetLogicalWidth(int logicalWidth)
    {
        _metrics = _metrics with { Width = Math.Max(1, logicalWidth) };
        Redraw();
    }

    internal void Align(NotchAlignment alignment)
    {
        _alignment = alignment;
        Redraw();
    }

    /// <summary>Physical size and position of the bar right now, for reporting what a setting actually produced.</summary>
    internal NotchGeometry.Rect CurrentBounds() => ComputeBounds(Scaled());

    /// <summary>Host monitor width in logical pixels, so ratio-derived widths can be offered in the same unit.</summary>
    internal int LogicalScreenWidth() =>
        IsHandleCreated ? (int)Math.Round(Screen.FromHandle(Handle).Bounds.Width / ScaleFor()) : 0;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NotchWindowInterop.RaiseToTop(Handle);
        Redraw();
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

    private NotchMetrics Scaled() => _metrics.ScaledBy(ScaleFor());

    private NotchGeometry.Rect ComputeBounds(NotchMetrics scaled)
    {
        // The panel edge, not the work area: a notch imitates a hole in the bezel.
        var area = Screen.FromHandle(Handle).Bounds;
        var screen = new NotchGeometry.Rect(area.Left, area.Top, area.Right, area.Bottom);

        return NotchGeometry.Place(screen, scaled, _alignment);
    }

    /// <summary>GDI does not scale for DPI on its own, so every drawn size is multiplied by this.</summary>
    private double ScaleFor() => IsHandleCreated ? DeviceDpi / 96d : 1d;
}
