// Shows a news article inside the app instead of handing it to the browser.
using System.Runtime.Versioning;
using JKBar.App.Interop;
using JKBar.Core.News;
using JKBar.Core.Settings;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace JKBar.App;

[SupportedOSPlatform("windows")]
internal sealed class NewsArticleWindow : Form
{
    private const int Rounding = 12;
    private const int LogicalWidth = 720;
    private const int LogicalHeight = 560;

    /// <summary>
    /// How wide the frame around the content is, in logical pixels. WebView2 owns its own child window and
    /// swallows the pointer, so this margin is the only part of the panel left for Windows to resize by.
    /// </summary>
    private const int GripWidth = 5;

    private static readonly Color Surface = Color.FromArgb(252, 252, 253);
    private static readonly Color Edge = Color.FromArgb(214, 214, 220);
    private static readonly Color Ink = Color.FromArgb(24, 24, 27);
    private static readonly Color Muted = Color.FromArgb(118, 118, 128);

    private readonly WebView2 _view = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Surface };

    /// <summary>
    /// WebView2's own child window reaches a little past the control it is docked in, which would cover the
    /// bottom resize border. A plain panel in between clips it, because a child cannot paint outside its parent.
    /// </summary>
    private readonly Panel _body = new() { Dock = DockStyle.Fill, BackColor = Surface };
    private readonly ArticleHeader _header = new(Surface, Ink, Muted);

    private NewsItem? _article;
    private bool _ready;
    private bool _placed;
    private double _scale = 1d;

    internal NewsArticleWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        // The window is placed in real pixels, so WinForms must not rescale what this code sets.
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Surface;
        Padding = new Padding(GripWidth);
        MinimumSize = new Size(ArticleWindowSettings.MinimumWidth, ArticleWindowSettings.MinimumHeight);
        Text = "JKBar 기사";
        KeyPreview = true;

        _body.Controls.Add(_view);
        Controls.Add(_body);
        Controls.Add(_header);

        _header.Drag = () => NewsPopupInterop.DragByCaption(Handle);
        _header.CloseRequested += Hide;
        _header.BrowserRequested += () =>
        {
            if (_article is { } article)
            {
                Hide();
                OpenInBrowser?.Invoke(article.Link);
            }
        };

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Hide();
            }
        };
    }

    internal Action<Uri>? OpenInBrowser;

    /// <summary>Reads the size and position the user last left, so reopening lands in the same place.</summary>
    internal Func<ArticleWindowSettings>? LoadBounds;

    /// <summary>Called once the user finishes moving or resizing, and again when the panel is put away.</summary>
    internal Action<ArticleWindowSettings>? SaveBounds;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle |= NewsPopupInterop.CsDropShadow;
            return parameters;
        }
    }

    /// <param name="anchor">The headline on screen; the article hangs from its left edge, just below the band.</param>
    internal async Task ShowArticleAsync(NewsItem article, Rectangle anchor)
    {
        _article = article;
        _header.Describe(article.Title, article.Link.Host);

        var screen = Screen.FromPoint(new Point(anchor.Left, anchor.Bottom)).WorkingArea;
        var scale = MonitorScale.At(anchor.Left + (anchor.Width / 2), anchor.Bottom + 1);
        _scale = scale;
        Padding = new Padding(Edging());
        _header.ApplyScale(scale);

        Show();
        Bounds = Placement(anchor, screen, scale);
        _placed = true;

        NewsPopupInterop.RoundCorners(Handle, Width, Height, (int)Math.Round(Rounding * scale));
        Activate();

        if (!_ready)
        {
            // The runtime folder must be writable, and the executable's own folder may not be.
            // Software rendering is deliberate: with the GPU enabled the browser keeps a process that holds the
            // display driver for as long as JKBar runs, which is what blocks switching on a hybrid-graphics laptop.
            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = "--disable-gpu --disable-gpu-compositing"
            };
            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(Path.GetTempPath(), "JKBar.WebView"),
                options: options);
            await _view.EnsureCoreWebView2Async(environment);
            _view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _view.CoreWebView2.Settings.IsStatusBarEnabled = false;
            // Anything the page tries to open in a new window goes to the real browser instead.
            _view.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
                {
                    OpenInBrowser?.Invoke(uri);
                }
            };
            _ready = true;
        }

        _view.CoreWebView2.Navigate(article.Link.AbsoluteUri);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Edge);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    /// <summary>
    /// The remembered rectangle wins as long as it still lands on a screen that exists; a monitor that has been
    /// unplugged would otherwise put the panel out of reach, so that case falls back to the headline anchor.
    /// </summary>
    private Rectangle Placement(Rectangle anchor, Rectangle screen, double scale)
    {
        if (LoadBounds?.Invoke() is { HasBounds: true } saved)
        {
            var remembered = new Rectangle(saved.X, saved.Y, saved.Width, saved.Height);
            var area = Screen.FromRectangle(remembered).WorkingArea;
            if (area.IntersectsWith(remembered))
            {
                return new Rectangle(
                    Math.Clamp(remembered.X, area.Left, Math.Max(area.Left, area.Right - remembered.Width)),
                    Math.Clamp(remembered.Y, area.Top, Math.Max(area.Top, area.Bottom - remembered.Height)),
                    Math.Min(remembered.Width, area.Width),
                    Math.Min(remembered.Height, area.Height));
            }
        }

        var width = Math.Min((int)Math.Round(LogicalWidth * scale), screen.Width - 40);
        var height = Math.Min((int)Math.Round(LogicalHeight * scale), screen.Bottom - anchor.Bottom - 24);

        return new Rectangle(
            Math.Clamp(anchor.Left, screen.Left + 8, Math.Max(screen.Left + 8, screen.Right - width - 8)),
            Math.Min(anchor.Bottom + 4, screen.Bottom - height - 8),
            width,
            height);
    }

    private void Remember()
    {
        if (!IsHandleCreated || !_placed || Width < ArticleWindowSettings.MinimumWidth
            || Height < ArticleWindowSettings.MinimumHeight)
        {
            return;
        }

        SaveBounds?.Invoke(new ArticleWindowSettings { X = Left, Y = Top, Width = Width, Height = Height });
    }

    /// <summary>The rounded region is baked at a fixed size, so it has to be recut after every resize.</summary>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (IsHandleCreated && Width > 0 && Height > 0)
        {
            NewsPopupInterop.RoundCorners(Handle, Width, Height, (int)Math.Round(Rounding * _scale));
        }
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        Remember();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        // Hiding is how the panel is dismissed, so this is the moment a moved or resized window is worth keeping.
        if (!Visible)
        {
            Remember();
        }

        base.OnVisibleChanged(e);
    }

    /// <summary>
    /// A borderless window has no frame for Windows to resize, so the edges are reported as frame hits by hand.
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x0084;

        if (m.Msg == wmNcHitTest)
        {
            // The packed coordinates are signed: a monitor left of the primary one gives negative values.
            var packed = m.LParam.ToInt64();
            var point = PointToClient(new Point((short)(packed & 0xFFFF), (short)((packed >> 16) & 0xFFFF)));
            if (Grip(point) is { } grip)
            {
                m.Result = grip;
                return;
            }
        }

        base.WndProc(ref m);
    }

    private IntPtr? Grip(Point point)
    {
        const int htLeft = 10;
        const int htRight = 11;
        const int htTop = 12;
        const int htTopLeft = 13;
        const int htTopRight = 14;
        const int htBottom = 15;
        const int htBottomLeft = 16;
        const int htBottomRight = 17;

        var edge = Edging();
        var left = point.X <= edge;
        var right = point.X >= Width - edge;
        var top = point.Y <= edge;
        var bottom = point.Y >= Height - edge;

        return (left, right, top, bottom) switch
        {
            (true, _, true, _) => htTopLeft,
            (_, true, true, _) => htTopRight,
            (true, _, _, true) => htBottomLeft,
            (_, true, _, true) => htBottomRight,
            (true, _, _, _) => htLeft,
            (_, true, _, _) => htRight,
            (_, _, true, _) => htTop,
            (_, _, _, true) => htBottom,
            _ => null
        };
    }

    private int Edging() => Math.Max(GripWidth, (int)Math.Round(GripWidth * _scale));

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // The context owns this window for the life of the app; closing it only puts it away.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }
}
