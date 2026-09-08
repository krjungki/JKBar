// A full-width window whose job is to hold the appbar reservation and fill the band either side of the notch.
// It is separate from the bar because an appbar's window is tied to the reserved rect, and the bar has to be able
// to grow past it when an alert opens.
using System.Drawing.Imaging;
using JKBar.App.Diagnostics;
using JKBar.App.Interop;
using JKBar.App.Rendering;
using JKBar.Core.Layout;
using JKBar.Core.News;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;
using JKBar.Core.Stocks;

namespace JKBar.App;

internal sealed class AppBarReservation : Form
{
    private NotchGeometry.Rect _screen;
    private NotchGeometry.Rect _band;
    private NotchGeometry.Rect _notch;
    private int _notchCornerRadius;
    private BandStyle _style = BandStyle.Default;
    private BandTypographySettings _typography = new();
    private IReadOnlyList<BandItem> _items = [];
    private IReadOnlyList<RunningProcess> _runningProcesses = [];
    private NewsItem? _news;
    private StockQuote? _quote;
    private Rectangle _newsBounds;
    private IReadOnlyList<ProcessIcon> _processIcons = [];
    private Image? _image;
    private int _imageScalePercent = 100;
    private string? _activeApp;
    private Bitmap? _surface;
    private Size _surfaceSize;
    private int _height;
    private bool _registered;
    private bool _reportedNewsCramped;

    /// <summary>Raised after the band takes a new position, so the bar can put itself back above it.</summary>
    internal Action? Claimed;
    internal Action<Rectangle>? NewsClicked;
    internal Action<WatchedProcess>? ProcessClicked;

    /// <summary>Raised once when the slot turns out to be too narrow for a headline.</summary>
    internal Action? NewsRoomExhausted;

    /// <summary>
    /// The bar's window. An open alert reaches past the cutout the band stamps black, so the band has to stay
    /// behind the bar for that overhang to keep its own colour.
    /// </summary>
    internal IntPtr Below;

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

    internal void SetTypography(BandTypographySettings typography)
    {
        _typography = typography.Normalized();

        if (_registered)
        {
            PaintBand();
        }
    }

    /// <param name="notch">The resting bar, in band coordinates. Using the resting size rather than the animated
    /// one keeps a full-width surface from being redrawn on every frame of an alert.</param>
    internal void SetContent(
        Image? image,
        int imageScalePercent,
        string? activeApp,
        StockQuote? quote,
        NewsItem? news,
        IReadOnlyList<BandItem> items,
        IReadOnlyList<RunningProcess> runningProcesses,
        NotchGeometry.Rect notch,
        int notchCornerRadius)
    {
        _image = image;
        _imageScalePercent = imageScalePercent;
        _activeApp = activeApp;
        _quote = quote;
        _news = news;
        _items = items;
        _runningProcesses = runningProcesses;
        _notch = notch;
        _notchCornerRadius = notchCornerRadius;

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

    /// <summary>
    /// Hides the band while keeping the reservation. Giving it up would hand the strip back to the desktop and
    /// shuffle every maximised window, which is not what hiding for a full-screen app should cost.
    /// </summary>
    internal void Suspend()
    {
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

    /// <summary>
    /// A display change can hand back a band far wider than any surface GDI+ will allocate. Skipping the repaint
    /// keeps the app alive until the next claim brings a sane rectangle.
    /// </summary>
    private Bitmap? TryCreateSurface()
    {
        try
        {
            return new Bitmap(_band.Width, _band.Height, PixelFormat.Format32bppArgb);
        }
        catch (Exception error) when (error is ArgumentException or OutOfMemoryException)
        {
            CrashLog.Write($"띠 그림판 {_band.Width}x{_band.Height}", error);
            return null;
        }
    }

    private void PaintBand()
    {
        if (_band.Width <= 0 || _band.Height <= 0)
        {
            return;
        }

        // Kept between repaints: the readouts change every second and this is as wide as the screen. The size is
        // remembered separately because asking a disposed bitmap for its width throws the same ArgumentException
        // a bad allocation does, which is how this used to fail during a display change.
        if (_surface is null || _surfaceSize.Width != _band.Width || _surfaceSize.Height != _band.Height)
        {
            var replacement = TryCreateSurface();
            if (replacement is null)
            {
                return;
            }

            _surface?.Dispose();
            _surface = replacement;
            _surfaceSize = replacement.Size;
        }

        bool cramped;
        using (var graphics = Graphics.FromImage(_surface))
        {
            var areas = BandRenderer.Paint(
                graphics,
                _surfaceSize,
                _notch,
                _notchCornerRadius,
                _style,
                _typography,
                _image,
                _imageScalePercent,
                _activeApp,
                _quote,
                _news,
                _items,
                _runningProcesses);

            _newsBounds = areas.News;
            _processIcons = areas.ProcessIcons;
            cramped = areas.NewsCramped;
        }

        NotchWindowInterop.PushLayeredSurface(Handle, _surface, _band.Left, _band.Top);

        if (!cramped)
        {
            _reportedNewsCramped = false;
            return;
        }

        if (!_reportedNewsCramped)
        {
            _reportedNewsCramped = true;

            // Turning the news off repaints the band, so it cannot happen inside this paint.
            BeginInvoke(() => NewsRoomExhausted?.Invoke());
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x0084;
        const int htTransparent = -1;
        const int htClient = 1;

        if (m.Msg == NotchWindowInterop.WmWindowPosChanging && Below != IntPtr.Zero)
        {
            NotchWindowInterop.PinBehind(m.LParam, Below);
        }

        if (m.Msg == wmNcHitTest)
        {
            var screenPoint = new Point((short)(m.LParam.ToInt64() & 0xffff), (short)(m.LParam.ToInt64() >> 16));
            var client = PointToClient(screenPoint);
            var clickable = _newsBounds.Contains(client) || ProcessIconHitTest.At(_processIcons, client) is not null;
            m.Result = clickable ? htClient : htTransparent;
            return;
        }

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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var clickable = _newsBounds.Contains(e.Location) || ProcessIconHitTest.At(_processIcons, e.Location) is not null;
        Cursor = clickable ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (ProcessIconHitTest.At(_processIcons, e.Location) is { } process)
        {
            ProcessClicked?.Invoke(process);
            return;
        }

        if (_news is not null && _newsBounds.Contains(e.Location))
        {
            NewsClicked?.Invoke(RectangleToScreen(_newsBounds));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Release();
            _surface?.Dispose();
        }

        base.Dispose(disposing);
    }
}
