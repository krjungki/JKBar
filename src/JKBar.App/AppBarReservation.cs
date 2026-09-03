// A full-width window whose job is to hold the appbar reservation and fill the band either side of the notch.
// It is separate from the bar because an appbar's window is tied to the reserved rect, and the bar has to be able
// to grow past it when an alert opens.
using System.Drawing.Imaging;
using JKBar.App.Interop;
using JKBar.Core.Layout;

namespace JKBar.App;

internal sealed class AppBarReservation : Form
{
    private NotchGeometry.Rect _screen;
    private NotchGeometry.Rect _band;
    private BandStyle _style = BandStyle.Default;
    private int _height;
    private bool _registered;

    /// <summary>Raised after the band takes a new position, so the bar can put itself back above it.</summary>
    internal Action? Claimed;

    internal AppBarReservation()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "JKBar reservation";
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= NotchWindowInterop.WsExLayered
                | NotchWindowInterop.WsExToolWindow
                | NotchWindowInterop.WsExTransparent
                | NotchWindowInterop.WsExNoActivate;

            return parameters;
        }
    }

    protected override bool ShowWithoutActivation => true;

    /// <summary>
    /// Showing applies WinForms' own bounds, which would undo a surface pushed before it. The bar hit exactly
    /// this and ended up at the top left, so the band repaints once it is really on screen.
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_registered)
        {
            PaintBand();
        }
    }

    internal void SetStyle(BandStyle style)
    {
        _style = style;

        if (_registered)
        {
            PaintBand();
        }
    }

    internal void Reserve(NotchGeometry.Rect screen, int height)
    {
        _screen = screen;
        _height = height;

        if (!Visible)
        {
            Show();
        }

        if (!_registered)
        {
            _registered = AppBarInterop.Register(Handle);
            if (!_registered)
            {
                Hide();
                return;
            }
        }

        Claim();
    }

    internal void Release()
    {
        if (_registered)
        {
            AppBarInterop.Unregister(Handle);
            _registered = false;
        }

        if (Visible)
        {
            Hide();
        }
    }

    private void Claim()
    {
        _band = AppBarInterop.Claim(Handle, _screen, _height);
        PaintBand();

        // The band shares the topmost group with the bar so a window dragged over the top edge cannot split them.
        NotchWindowInterop.RaiseToTop(Handle);
        Claimed?.Invoke();
    }

    private void PaintBand()
    {
        if (_band.Width <= 0 || _band.Height <= 0)
        {
            return;
        }

        using var bitmap = new Bitmap(_band.Width, _band.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(_style.ForLayeredSurface());
        }

        NotchWindowInterop.PushLayeredSurface(Handle, bitmap, _band.Left, _band.Top);
    }

    protected override void WndProc(ref Message m)
    {
        // The shell moves appbars around when another one appears or the taskbar changes; the band has to be
        // re-claimed or it silently stops reserving anything.
        if (m.Msg == AppBarInterop.CallbackMessage
            && _registered
            && m.WParam.ToInt32() == AppBarInterop.NotifyPositionChanged)
        {
            Claim();
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Release();
        }

        base.Dispose(disposing);
    }
}
